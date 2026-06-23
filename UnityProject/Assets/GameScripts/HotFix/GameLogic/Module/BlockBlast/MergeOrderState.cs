using System;
using System.Collections.Generic;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 一张订单：要求 (元素类型, 等级, 数量)。例「◆ Lv2 ×1」。
    /// </summary>
    public readonly struct Order
    {
        public readonly MergeElement Type;
        public readonly int Level;
        public readonly int Count;

        public Order(MergeElement type, int level, int count)
        {
            Type = type;
            Level = level;
            Count = count;
        }

        /// <summary>难度量 d = 数量 × 2^(等级-1)（= 折算基础元素数）。</summary>
        public int Difficulty => Count * (1 << (Level - 1));

        public bool IsValid => Type != MergeElement.None && Level >= 1 && Count > 0;
    }

    /// <summary>
    /// 一次神庙修复的结果详情（设计 13），供 UI 弹字展示。修复失败时 Success=false、其余字段为 0/默认。
    /// </summary>
    public readonly struct TempleRepairResult
    {
        /// <summary>是否成功修复（三前置全满足）。</summary>
        public readonly bool Success;
        /// <summary>本次修复的厅序号。</summary>
        public readonly int HallIndex;
        /// <summary>扣除的虔诚币（= 该厅造价）。</summary>
        public readonly int PietySpent;
        /// <summary>获得的经验（= 该厅造价）。</summary>
        public readonly int ExpGained;
        /// <summary>实际增加的体力（受软上限约束后的净增量，修复 + 升级累加）。</summary>
        public readonly int EnergyGained;
        /// <summary>本次修复跨越的守护者等级数（0 表示未升级）。</summary>
        public readonly int LevelsGained;
        /// <summary>修复后的守护者等级。</summary>
        public readonly int NewLevel;

        public TempleRepairResult(bool success, int hallIndex, int pietySpent, int expGained,
            int energyGained, int levelsGained, int newLevel)
        {
            Success = success;
            HallIndex = hallIndex;
            PietySpent = pietySpent;
            ExpGained = expGained;
            EnergyGained = energyGained;
            LevelsGained = levelsGained;
            NewLevel = newLevel;
        }
    }

    /// <summary>
    /// 合成+订单+体力 切片的新系统状态机（组合，独立于 BlockGameState 以免污染 Classic）。
    /// 纯逻辑、可单测：合成区自动配对升级、订单队列交付/刷新、体力扣/返/补、保底计数器。
    /// 棋盘 / 元素层 / 候选槽仍住在 BlockGameState。
    /// </summary>
    public sealed class MergeOrderState
    {
        /// <summary>当前体力。</summary>
        public int Energy;

        /// <summary>
        /// 上次时基恢复结算时刻（Unix 秒，本地时钟）。时基恢复（含离线）按「此刻 → now」真实秒差补算（设计 49 §3.2）。
        /// 跨会话随元层一同落盘（设计 14 §3.7）。0 = 尚无记录（首次 / 旧档），
        /// 首次结算以 now 初始化、本次不补（设计 49 §3.2 崩法三）。
        /// </summary>
        public long LastEnergyRegenTime;

        /// <summary>
        /// 合成区库存：键 (类型, 等级) → 数量。自动配对使每 (类型,等级) 数量恒 ≤1（满 2 即合），
        /// 故天然紧凑、无需硬上限。计数为 0 的键即时移除。
        /// </summary>
        public readonly Dictionary<(MergeElement type, int level), int> Inventory =
            new Dictionary<(MergeElement, int), int>();

        /// <summary>当前激活订单（长度 = MergeOrderConfig.ActiveOrders）。</summary>
        public Order[] ActiveOrders;

        /// <summary>循环订单池游标（下一张待取的索引，按池长取模）。</summary>
        public int OrderCursor;

        /// <summary>已完成单数。</summary>
        public int CompletedOrders;

        /// <summary>本局累计交付得分（用于通关 / 结算摘要；窗口可镜像到 BlockGameState.Score）。</summary>
        public int TotalScore;

        /// <summary>
        /// 元素预算队列：消除按得分算出的 k 个元素压入此处，补牌（BuildPiece）时 FIFO 抽干填入新候选块。
        /// 无消除→队列不增长→候选块纯方块；得分越高→积压越多→后续候选块携带更多元素。
        /// </summary>
        public readonly Queue<MergeElement> PendingElements = new Queue<MergeElement>();

        /// <summary>所需类型轮转游标：多个所需类型时按此取模均摊，避免长期偏科某一类型。</summary>
        private int _needRotor;

        // ── 结算状态（连消链 / 全清武装位）─ 设计 11 §5.5 ────────
        /// <summary>当前连消链长（连续触发消除的落子次数）。无消除时归 1。</summary>
        public int ComboChain;

        /// <summary>
        /// 全清武装位：true 时下次全清可发奖。一次全清发奖后置 false，
        /// 须中间至少一次非全清落子才重新武装（防连续 2 次全清刷奖）。开局 true。
        /// </summary>
        public bool AllClearArmed;

        // ── 单货币灵力 + 祈愿 ─ 设计 11 §四 / §7.1 ───────────────
        /// <summary>灵力（唯一软货币）。订单/宝箱/女神奖励 + 兑体力 + 养成消耗。</summary>
        public int Soul;

        /// <summary>今日已用祈愿次数（兑体力）。</summary>
        public int WishUsedToday;

        // ── 女神（全清累计好评进度）─ 设计 11 §十 ────────────────
        /// <summary>当前档好评条计数（每全清 +1，满 GoddessRatingGoal 清零升档）。</summary>
        public int GoddessRating;

        /// <summary>女神好感等级（只升不降，从 1 起）。</summary>
        public int GoddessLevel;

        // ── 神秘塔罗盲盒（持有式即时开盒）─ 设计 12 ──────────────
        /// <summary>
        /// 盲盒持有计数（设计 12）。连消/全清挑战与特殊订单交付积累，玩家自选时机一键开出。
        /// 持有式、无倒计时、无硬上限（设计 12 §3.3）。不单独磁盘存盘。
        /// </summary>
        public int BlindBoxCount;

        /// <summary>特殊订单轨（设计 11 §三：0/1 占槽 + 等待队列，剧情&gt;加急&gt;黄金时段）。</summary>
        public readonly SpecialOrderTrack SpecialTrack = new SpecialOrderTrack();

        // ── 方块皮肤态（全清触发单色换皮，设计 50）──────────────────
        /// <summary>
        /// 棋盘皮肤状态机（设计 50）：彩色 / 单色 + 当前单色标识。全清事件推进（窗口在结算后调 <see cref="BlockSkinState.OnAllClear"/>）；
        /// 跨会话续存随元层一同走 <see cref="ExportMeta"/>/<see cref="ImportMeta"/>（皮肤态是「已达成全清」只增成就标记，
        /// 语义同元层只增字段，设计 50 §六）。开局缺省 = 彩色态（设计 50 §三 规则 1）。
        /// </summary>
        public readonly BlockSkinState Skin = new BlockSkinState();

        // ── 长期主线：虔诚币 + 神庙修复 + 经验·守护者等级（设计 13）──
        /// <summary>
        /// 虔诚币（长期主线货币，设计 13）。唯一来源 = 订单交付的主要奖励；唯一出口 = 修复神庙大厅。
        /// 只增不减地累积（<see cref="AddPiety"/> 仅接受正数），仅在 <see cref="RepairTemple"/> 处扣减。
        /// 不单独磁盘存盘——与 灵力/盲盒计数 同口径。
        /// </summary>
        public int Piety;

        /// <summary>累积经验（设计 13）。唯一来源 = 修复神庙；守护者等级是其纯函数。只增不减。</summary>
        public int Exp;

        /// <summary>已解锁剧情章节数（设计 13）。每升一级 +1，本轮只做计数标记，无剧情内容/UI。</summary>
        public int UnlockedChapter;

        /// <summary>
        /// 下一座待修神庙的序号（0-indexed）。顺序解锁：第 i 厅「可修」⟺ 前 i 厅全部已修，即 i==NextRepairIndex。
        /// 修完一厅 +1；达 <see cref="TempleConfig.HallCount"/> 表示全部修完（主线完成）。
        /// </summary>
        public int NextRepairIndex;

        /// <summary>各神庙是否已修复（长度 = TempleConfig.HallCount）。</summary>
        public bool[] TempleRepaired;

        /// <summary>各神庙是否已获装饰摆件（修复成功即置 true，本轮只 bool 标记、无美术）。</summary>
        public bool[] TempleDecorated;

        /// <summary>守护者等级（派生：经验的纯函数，不单独存值以免两份状态漂移，设计 13 §3.4）。</summary>
        public int GuardianLevel => TempleConfig.GuardianLevelFor(Exp);

        /// <summary>全 12 厅是否修复完毕（主线长期通关标记）。</summary>
        public bool IsTempleAllRepaired => NextRepairIndex >= TempleConfig.HallCount;

        /// <summary>开新一局：起始体力 + 空合成区 + 初始订单 + 清计数器。</summary>
        public void Reset()
        {
            Energy = MergeOrderConfig.EnergyStart;
            LastEnergyRegenTime = 0; // 尚无记录：首次 ApplyTimeRegen(now) 以 now 初始化、本次不补（设计 49 §3.2）
            Inventory.Clear();
            OrderCursor = 0;
            CompletedOrders = 0;
            TotalScore = 0;
            PendingElements.Clear(); // 开局队空 → 首手纯方块
            _needRotor = 0;

            ComboChain = 1;     // 链长 1 = 无加成基准
            AllClearArmed = true;
            Soul = 0;
            WishUsedToday = 0;
            GoddessRating = 0;
            GoddessLevel = 1;
            BlindBoxCount = 0;

            // 长期主线（设计 13）：开局虔诚币/经验/章节归零，12 厅全部未修、未装饰，从第 0 厅开始。
            Piety = 0;
            Exp = 0;
            UnlockedChapter = 0;
            NextRepairIndex = 0;
            TempleRepaired = new bool[TempleConfig.HallCount];
            TempleDecorated = new bool[TempleConfig.HallCount];

            ActiveOrders = new Order[MergeOrderConfig.ActiveOrders];
            for (int i = 0; i < ActiveOrders.Length; i++) ActiveOrders[i] = NextOrder();

            ResetSpecialTrack();

            // 皮肤态缺省 = 彩色（设计 50 §三 规则 1）；存档若有皮肤态由 ImportMeta 覆盖（同元层口径）。
            Skin.Reset();
        }

        // ── 体力 ──────────────────────────────────────────────

        /// <summary>是否付得起一次落子。</summary>
        public bool CanAffordPlace => Energy >= MergeOrderConfig.PlaceCost;

        /// <summary>落子扣体力（调用方已确认 CanAffordPlace）。</summary>
        public void SpendPlaceCost() => Energy -= MergeOrderConfig.PlaceCost;

        /// <summary>消除返还体力 = 本次被清行列数；受软上限约束，不溢出。</summary>
        public void RefundEnergy(int lines)
        {
            if (lines <= 0) return;
            Energy = Math.Min(MergeOrderConfig.EnergyCap, Energy + lines);
        }

        // ── 时基恢复（含离线，设计 49 §3.2 / 设计 14 §3.7）──────────────
        // 无条件、纯时间驱动：按「LastEnergyRegenTime → now」真实秒差补算应恢复点数，封顶软上限不溢出。
        // 纯方法、注入 now（Unix 秒）供单测，不依赖真实时钟。不接落子/消除/交付触发——卡死时也能恢复（脱困保险）。

        /// <summary>
        /// 按真实时差补算时基恢复（设计 49 §3.2）。<paramref name="nowUnixSec"/> = 当前 Unix 秒（生产传真实时刻，单测注入）。
        /// 行为：① 首次无记录（<see cref="LastEnergyRegenTime"/>==0）→ 以 now 初始化、本次不补（不凭空给）；
        /// ② 负时差（now &lt; 上次记录，玩家回调时钟）→ 不倒扣、不抛、不更新记录时刻（待时间走正再补）；
        /// ③ 应恢复点数 = floor(Δ / RegenIntervalSec) × RegenPerTick，加到体力后夹到软上限（不溢出；
        ///    体力本就 &gt; 软上限（订单奖励溢出）则不动）；④ 仅把「已整除掉的秒数」推进记录时刻，
        ///    不满一个 tick 的余秒留到下次累计（避免短间隔反复进入吞掉零头永不恢复）。
        /// </summary>
        public void ApplyTimeRegen(long nowUnixSec)
        {
            if (LastEnergyRegenTime == 0)
            {
                LastEnergyRegenTime = nowUnixSec; // 首次：初始化记录时刻，本次不补（设计 49 §3.2 崩法三）
                return;
            }

            long deltaSec = nowUnixSec - LastEnergyRegenTime;
            if (deltaSec <= 0) return; // 负时差 / 同刻：不倒扣、不抛、不更新记录（设计 49 §3.2 崩法二）

            int interval = (int)MergeOrderConfig.RegenIntervalSec;
            if (interval <= 0) return; // 防御：间隔非法不恢复（配置错不崩）

            long ticks = deltaSec / interval;
            if (ticks <= 0) return; // 不满一个 tick：记录时刻不动，余秒留到下次累计

            // 仅在低于软上限时补，封顶软上限不溢出；体力本就 > 软上限（订单溢出）则保持不动。
            if (Energy < MergeOrderConfig.EnergyCap)
            {
                long restored = ticks * MergeOrderConfig.RegenPerTick;
                long newEnergy = Energy + restored;
                Energy = newEnergy > MergeOrderConfig.EnergyCap ? MergeOrderConfig.EnergyCap : (int)newEnergy;
            }

            // 只推进「已整除掉的秒数」，余秒（deltaSec % interval）留到下次，不被吞掉。
            LastEnergyRegenTime += ticks * interval;
        }

        // ── 消除道具（脱困兜底，设计 49 §3.1）─────────────────────────
        // 主动清「一行一列」让卡死棋盘重新可落；只 gate 体力、无限可用（不持有计数、不限次数）。
        // 棋盘清除走 BlockGameState（持 SaveArr/ElementArr/BinaryBoard）；本类只管体力 gate + 扣 cost。

        /// <summary>体力是否够用一次消除道具（设计 49 §3.1：≥ ClearToolCost 可用，&lt; 置灰）。</summary>
        public bool CanUseClearTool => Energy >= MergeOrderConfig.ClearToolCost;

        /// <summary>
        /// 扣除一次消除道具代价体力（调用方已确认 <see cref="CanUseClearTool"/>）。返回 false = 体力不足、未扣。
        /// 体力恒 ≥ 0（cost ≤ EnergyCap 不变量保证不会扣成负，且仅在 CanUseClearTool 时扣）。
        /// </summary>
        public bool SpendClearToolCost()
        {
            if (!CanUseClearTool) return false;
            Energy -= MergeOrderConfig.ClearToolCost;
            return true;
        }

        // ── 合成区（自动配对升级）─────────────────────────────

        /// <summary>查询合成区某 (类型,等级) 持有量。</summary>
        public int InventoryCount(MergeElement type, int level)
            => Inventory.TryGetValue((type, level), out var n) ? n : 0;

        /// <summary>
        /// 摄入一个 Lv1 元素，随即向上级联自动配对：任一 (类型,等级) 数量≥2 即
        /// 数量-2、上一级+1，直到无法再合并或封顶 MaxLevel。
        /// </summary>
        public void IngestElement(MergeElement type)
        {
            if (type == MergeElement.None) return;
            AddToInventory(type, 1, 1);
        }

        /// <summary>
        /// 直接向收集区注入指定等级的图案（多消里程碑加码 / 全清奖 / 宝箱奖用，跳过逐级合成）。
        /// 仍走向上级联：若该级注入后达 2 个，照常合并升级（封顶 Lv3 不再升、堆积）。
        /// </summary>
        public void AddDirect(MergeElement type, int level, int count)
        {
            if (type == MergeElement.None || level < 1 || count <= 0) return;
            if (level > MergeOrderConfig.MaxLevel) level = MergeOrderConfig.MaxLevel;
            AddToInventory(type, level, count);
        }

        private void AddToInventory(MergeElement type, int level, int amount)
        {
            int cur = InventoryCount(type, level) + amount;
            Inventory[(type, level)] = cur;

            // 向上级联合并
            while (level < MergeOrderConfig.MaxLevel && Inventory[(type, level)] >= 2)
            {
                int left = Inventory[(type, level)] - 2;
                if (left == 0) Inventory.Remove((type, level));
                else Inventory[(type, level)] = left;

                level += 1;
                Inventory[(type, level)] = InventoryCount(type, level) + 1;
            }
        }

        // ── 订单 ──────────────────────────────────────────────

        /// <summary>从循环订单池取下一张（游标自增、到尾循环）。</summary>
        public Order NextOrder()
        {
            var pool = MergeOrderConfig.OrderPool;
            var o = pool[OrderCursor % pool.Length];
            OrderCursor++;
            return o;
        }

        /// <summary>合成区库存是否满足该订单槽要求。</summary>
        public bool CanDeliver(int slot)
        {
            if (ActiveOrders == null || slot < 0 || slot >= ActiveOrders.Length) return false;
            var o = ActiveOrders[slot];
            if (!o.IsValid) return false;
            return InventoryCount(o.Type, o.Level) >= o.Count;
        }

        /// <summary>
        /// 交付订单：扣除对应合成物 → 发奖（体力可溢出上限 + 分数）→ 完成单数+1 → 该槽刷新下一单。
        /// 返回 false 表示库存不足、未交付。
        /// </summary>
        public bool Deliver(int slot)
        {
            if (!CanDeliver(slot)) return false;
            var o = ActiveOrders[slot];

            int left = InventoryCount(o.Type, o.Level) - o.Count;
            if (left == 0) Inventory.Remove((o.Type, o.Level));
            else Inventory[(o.Type, o.Level)] = left;

            Energy += MergeOrderConfig.OrderRewardEnergy;            // 奖励可溢出软上限
            TotalScore += o.Level * o.Count * MergeOrderConfig.OrderScoreFactor;
            CompletedOrders += 1;

            // 长期主线（设计 13 §3.1）：虔诚币 = 订单难度 × 旋钮。纯追加，不动上方旧发奖。
            AddPiety(o.Difficulty * TempleConfig.PietyPerDifficulty);

            ActiveOrders[slot] = NextOrder();
            return true;
        }

        /// <summary>激活订单所需的去重元素类型集合（注入类型池来源 = 此并集）。</summary>
        public List<MergeElement> NeededTypes()
        {
            var list = new List<MergeElement>();
            if (ActiveOrders == null) return list;
            foreach (var o in ActiveOrders)
            {
                if (o.IsValid && !list.Contains(o.Type)) list.Add(o.Type);
            }
            return list;
        }

        /// <summary>
        /// 把该次消除得分算出的 k 个元素压入预算队列：类型沿用需求拉动——只从 <see cref="NeededTypes"/>
        /// 按轮转游标均摊取得。受 <see cref="MergeOrderConfig.MaxPendingElements"/> 截断；NeededTypes 空则跳过。
        /// </summary>
        public void EnqueueScoreElements(int k)
        {
            if (k <= 0) return;
            var needed = NeededTypes();
            if (needed.Count == 0) return;
            for (int i = 0; i < k && PendingElements.Count < MergeOrderConfig.MaxPendingElements; i++)
            {
                PendingElements.Enqueue(needed[_needRotor % needed.Count]);
                _needRotor++;
            }
        }

        // ── 特殊订单轨（设计 11 §三）─────────────────────────────

        /// <summary>开新一局/进入：清空特殊轨（demo 默认特殊轨为空，触发器由窗口按条件投放）。</summary>
        public void ResetSpecialTrack() => SpecialTrack.Reset();

        /// <summary>特殊槽当前订单可交付（库存满足）。槽空返回 false。</summary>
        public bool CanDeliverSpecial()
        {
            if (!SpecialTrack.HasOccupied) return false;
            var o = SpecialTrack.Occupied.Req;
            return InventoryCount(o.Type, o.Level) >= o.Count;
        }

        /// <summary>
        /// 交付特殊订单：扣库存 → 发奖（体力 + 分数 + 灵力，加急单附宝箱由窗口处理）→ 完成数+1 →
        /// 腾空特殊槽并从等待队列升起最高优先级单。返回 false 表示不可交付。
        /// </summary>
        public bool DeliverSpecial()
        {
            if (!CanDeliverSpecial()) return false;
            var o = SpecialTrack.Occupied.Req;

            int left = InventoryCount(o.Type, o.Level) - o.Count;
            if (left == 0) Inventory.Remove((o.Type, o.Level));
            else Inventory[(o.Type, o.Level)] = left;

            Energy += MergeOrderConfig.OrderRewardEnergy;
            TotalScore += o.Level * o.Count * MergeOrderConfig.OrderScoreFactor;
            CompletedOrders += 1;

            // 盲盒附赠（设计 12 §3.5）：须在 OnDelivered() 腾空槽之前读 Kind。
            AddBlindBox(BlindBoxPerSpecial(SpecialTrack.Occupied.Kind));

            // 长期主线（设计 13 §3.1）：特殊订单虔诚币 = 难度 × 旋钮 × 特殊溢价倍率。纯追加。
            AddPiety(o.Difficulty * TempleConfig.PietyPerDifficulty * TempleConfig.SpecialPietyMult);

            SpecialTrack.OnDelivered();
            return true;
        }

        /// <summary>特殊订单各 Kind 交付附赠的盲盒数（设计 12 §3.5）。None/未知 = 0。</summary>
        private static int BlindBoxPerSpecial(SpecialOrderKind kind)
        {
            switch (kind)
            {
                case SpecialOrderKind.Express: return TarotBlindBoxConfig.BoxPerExpress;
                case SpecialOrderKind.Story: return TarotBlindBoxConfig.BoxPerStory;
                case SpecialOrderKind.GoldenHour: return TarotBlindBoxConfig.BoxPerGolden;
                default: return 0;
            }
        }

        // ── 灵力 + 祈愿兑体力（设计 11 §四 / §7.1）────────────────

        /// <summary>增加灵力（订单/宝箱/女神奖励）。</summary>
        public void AddSoul(int amount) { if (amount > 0) Soul += amount; }

        /// <summary>本次是否可祈愿兑体力：灵力足且今日未达上限。</summary>
        public bool CanWishForEnergy()
            => Soul >= MergeOrderConfig.WishSoulCost
               && WishUsedToday < MergeOrderConfig.WishPerDayLimit;

        /// <summary>
        /// 祈愿兑体力（去变现续命）：扣灵力、+体力（封顶软上限，不溢出）、今日次数+1。
        /// 返回 false 表示灵力不足或今日已达上限。纯灵力消耗，无任何付费/广告。
        /// </summary>
        public bool WishForEnergy()
        {
            if (!CanWishForEnergy()) return false;
            Soul -= MergeOrderConfig.WishSoulCost;
            Energy = Math.Min(MergeOrderConfig.EnergyCap, Energy + MergeOrderConfig.WishEnergyGain);
            WishUsedToday += 1;
            return true;
        }

        // ── 女神（全清累计好评进度，设计 11 §十）──────────────────

        /// <summary>
        /// 推进女神好评条 1 格（每次全清调用）。满 GoddessRatingGoal → 清零、好感等级+1（只升不降）。
        /// 返回 true 表示本次推进触发了升档（窗口据此发档位奖励 + 换背景）。
        /// </summary>
        public bool AdvanceGoddess()
        {
            GoddessRating += 1;
            if (GoddessRating >= MergeOrderConfig.GoddessRatingGoal)
            {
                GoddessRating = 0;
                GoddessLevel += 1;
                return true;
            }
            return false;
        }

        // ── 神秘塔罗盲盒（持有式即时开盒，设计 12）──────────────

        /// <summary>增加盲盒持有计数（连消/全清挑战、特殊订单附赠调用）。负数/0 不增。</summary>
        public void AddBlindBox(int amount) { if (amount > 0) BlindBoxCount += amount; }

        /// <summary>是否可开盒（持有计数 &gt; 0）。</summary>
        public bool CanOpenBlindBox => BlindBoxCount > 0;

        /// <summary>
        /// 开一个盲盒（设计 12 §3.2/§3.3）：扣 1 计数 → 掷奖池恰好 1 项（含 NeededHigh 降级保底）→
        /// 按 Kind 发放（图案走 <see cref="AddDirect"/> 触发级联合并；体力走 <see cref="RefundEnergy"/> 受 EnergyCap）。
        /// 掷出的奖励经 <paramref name="reward"/> 返回供 UI 展示。计数为 0 时返回 false 且计数不变（前置 <see cref="CanOpenBlindBox"/>）。
        /// </summary>
        public bool OpenBlindBox(out BlindBoxReward reward)
        {
            reward = default;
            if (!CanOpenBlindBox) return false;

            BlindBoxCount -= 1;
            reward = TarotBlindBoxConfig.RollReward(this);

            if (reward.IsEnergy)
            {
                // 体力受软上限约束，不溢出（与消除返还同规则）。
                Energy = Math.Min(MergeOrderConfig.EnergyCap, Energy + reward.EnergyGain);
            }
            else if (reward.IsPattern)
            {
                // 图案进收集区，走级联合并（产 Lv1 若该类已有 1 个 → 自动升 Lv2，预期行为）。
                AddDirect(reward.PatternType, reward.PatternLevel, reward.PatternCount);
            }
            return true;
        }

        // ── 长期主线：虔诚币 + 神庙修复 + 经验·等级（设计 13）──────

        /// <summary>增加虔诚币（订单交付的主要奖励）。负数/0 不增，仿 <see cref="AddSoul"/>。</summary>
        public void AddPiety(int amount) { if (amount > 0) Piety += amount; }

        /// <summary>
        /// 第 index 厅是否可修（设计 13 §3.2 三前置全满足）：
        /// ①index==NextRepairIndex（顺序解锁，不可跳修）；②该厅未修；③虔诚币 ≥ 造价。
        /// 任一不满足返回 false。
        /// </summary>
        public bool CanRepairTemple(int index)
        {
            if (index < 0 || index >= TempleConfig.HallCount) return false;
            if (index != NextRepairIndex) return false;          // 顺序：只能修下一座
            if (TempleRepaired == null || TempleRepaired[index]) return false; // 已修不可回修
            return Piety >= TempleConfig.Cost(index);            // 币足
        }

        /// <summary>
        /// 修复第 index 厅（设计 13 §3.2/§3.3）：三前置全满足才执行——
        /// 扣虔诚币 → 标记已修 + 装饰 → 推进 NextRepairIndex → 发奖（经验 = 造价、体力受软上限）→
        /// 经验抬等级时逐级（含跨多级）施加升级奖励（每级 +1 章节 + 一份升级体力，体力累加受软上限）。
        /// 不满足前置时返回 false 且状态全不变（币不扣、不标记）。result 返回奖励详情供 UI 弹字。
        /// </summary>
        public bool RepairTemple(int index, out TempleRepairResult result)
        {
            result = default;
            if (!CanRepairTemple(index)) return false;

            int cost = TempleConfig.Cost(index);
            int energyBefore = Energy;
            int levelBefore = GuardianLevel;

            // 扣币 + 标记 + 推进
            Piety -= cost;
            TempleRepaired[index] = true;
            TempleDecorated[index] = true;
            NextRepairIndex = index + 1;

            // 经验（= 造价，1:1，造价越高经验越「大量」）→ 抬等级
            Exp += cost;
            RefundEnergy(TempleConfig.TempleRepairEnergy); // 修复回血，受软上限不溢出

            // 升级：经验跨了几级就发几份升级奖励（章节解锁计数 + 升级体力）。
            int levelAfter = GuardianLevel;
            int levelsGained = levelAfter - levelBefore;
            if (levelsGained > 0)
            {
                UnlockedChapter += levelsGained;
                // 升级体力一次性按跨级累加，受软上限（与现状 RefundEnergy 同规则）。
                RefundEnergy(levelsGained * TempleConfig.LevelUpEnergy);
            }

            int energyGained = Energy - energyBefore;
            result = new TempleRepairResult(true, index, cost, cost, energyGained, levelsGained, levelAfter);
            return true;
        }

        // ── 跨会话磁盘存档:导出/导入元层(设计 14 §3.3-§3.6)─────────
        // ExportMeta/ImportMeta 是纯方法:无 IO、无 UniTask,可在纯 C# 单测里同步调用。
        // 磁盘外壳(SaveAsync/LoadAsync)在 MergeMetaPersistence,本类不 using UniTask、不碰磁盘。

        /// <summary>
        /// 落盘脏位:元变更后由窗口侧 <see cref="RequestSave"/> 标脏,合并一串连带变更成一次落盘(§3.4)。
        /// 不进 Export/Import(纯调度,不持久)。
        /// </summary>
        private bool _saveDirty;

        /// <summary>标脏:有意义元变更后由窗口侧调用,延迟到合适节点合并落盘(§3.4)。不立即写盘。</summary>
        public void RequestSave() => _saveDirty = true;

        /// <summary>当前是否有未落盘的元变更(窗口侧据此决定是否 SaveAsync)。</summary>
        public bool IsSaveDirty => _saveDirty;

        /// <summary>清脏(落盘成功后由窗口侧调用)。</summary>
        public void ClearSaveDirty() => _saveDirty = false;

        /// <summary>
        /// 导出元层进度到 DTO(设计 14 §3.1 进盘 11 项 + version + 当前祈愿重置日期)。纯方法、无 IO。
        /// version 置 <see cref="MergeMetaPersistence.CurrentVersion"/>;lastWishResetDate 取 <paramref name="today"/>
        /// (默认本地日期),保证落盘的日期与「今日祈愿」语义一致(§3.6)。
        /// </summary>
        public MergeMetaSave ExportMeta(string today = null)
        {
            var dto = new MergeMetaSave
            {
                version = MergeMetaPersistence.CurrentVersion,
                soul = Soul,
                piety = Piety,
                exp = Exp,
                unlockedChapter = UnlockedChapter,
                nextRepairIndex = NextRepairIndex,
                templeRepaired = (bool[])TempleRepaired?.Clone(),   // 深拷贝:DTO 不与现场共享引用
                templeDecorated = (bool[])TempleDecorated?.Clone(),
                blindBoxCount = BlindBoxCount,
                goddessRating = GoddessRating,
                goddessLevel = GoddessLevel,
                wishUsedToday = WishUsedToday,
                lastWishResetDate = today ?? MergeMetaPersistence.Today(),
                energy = Energy,                            // 体力进盘（设计 14 §3.7）
                lastEnergyRegenTime = LastEnergyRegenTime,  // 上次时基恢复结算时刻（离线补算依据，设计 49 §3.2）
            };
            Skin.Export(dto); // 皮肤态随元层一同落盘（设计 50 §六）
            return dto;
        }

        /// <summary>
        /// 用 DTO 覆盖元层进度(设计 14 §3.5 逐字段保底 + §3.6 跨天重置)。纯方法、无 IO。
        /// 仅覆盖元字段(§3.1 进盘 11 项),不动局内瞬态(棋盘/手牌/订单/合成区)——调用前须先 <see cref="Reset"/>
        /// 建好局内瞬态(两者字段不重叠,§3.4)。<paramref name="dto"/> 为 null 直接返回(保持 Reset 缺省,等价首次)。
        ///
        /// 逐字段保底:即使 version 匹配,本地文件仍可能被篡改/截断,故对任意输入夹值到合法不变量
        /// (神庙数组长 12、女神等级≥1、NextRepairIndex∈[0,HallCount]),不把脏数据带进玩法。
        ///
        /// 跨天重置(§3.6):以 <paramref name="today"/>(默认本地日期)判定。LoadAsync 已对从盘读出的 DTO 跑过一次
        /// ApplyDailyReset;此处对 DTO 再判一次,使「直接构造 DTO 调 ImportMeta」(单测路径)也得到正确的跨天语义。
        /// </summary>
        public void ImportMeta(MergeMetaSave dto, string today = null)
        {
            if (dto == null) return;
            today ??= MergeMetaPersistence.Today();

            // 跨天重置:对 DTO 就地判定,保证单测「直接构造 DTO」路径也走跨天逻辑(LoadAsync 路径已判过,幂等无害)。
            MergeMetaPersistence.ApplyDailyReset(dto, today);

            Soul = dto.soul;
            Piety = dto.piety;
            Exp = dto.exp;
            UnlockedChapter = dto.unlockedChapter;
            BlindBoxCount = dto.blindBoxCount;
            GoddessRating = dto.goddessRating;

            // 女神等级从 1 起:<1(缺省 0 / 篡改)夹到 1(§3.5)。
            GoddessLevel = dto.goddessLevel < 1 ? 1 : dto.goddessLevel;

            // 神庙数组:null 或长度≠HallCount(旧档 / 篡改)重建为全 false(§3.5)。
            TempleRepaired = NormalizeTempleArray(dto.templeRepaired);
            TempleDecorated = NormalizeTempleArray(dto.templeDecorated);

            // NextRepairIndex 夹到 [0, HallCount](§3.5),不越界访问神庙数组。
            int idx = dto.nextRepairIndex;
            if (idx < 0) idx = 0;
            else if (idx > TempleConfig.HallCount) idx = TempleConfig.HallCount;
            NextRepairIndex = idx;

            // 祈愿:DTO 已经 ApplyDailyReset 夹过(跨天则 0),直接用。
            WishUsedToday = dto.wishUsedToday;

            // 体力 + 时基恢复记录时刻(设计 14 §3.7 / 设计 49 §3.2)：
            // lastEnergyRegenTime==0 = 尚无记录(旧档无此字段 → 缺省 0 / 首次)：此时 energy 也是缺省 0,不可信,
            //   夹回起始体力 + 记录时刻保持 0(进窗后 ApplyTimeRegen(now) 以 now 初始化、本次不补)。
            // lastEnergyRegenTime>0 = 真实无尽档：信其 energy(夹 ≥0 防篡改负值),不强塞起始体力(否则离线归零的合法档被刷满)。
            if (dto.lastEnergyRegenTime <= 0)
            {
                Energy = MergeOrderConfig.EnergyStart;
                LastEnergyRegenTime = 0;
            }
            else
            {
                Energy = dto.energy < 0 ? 0 : dto.energy;
                LastEnergyRegenTime = dto.lastEnergyRegenTime;
            }

            // 皮肤态(设计 50 §六):逐字段保底 + 加载校验(缺字段 → 彩色态;单色态非法标识 → 重随机)。
            // 候选池传真实存在标识集 BlockSkinCatalog.MonoIds(设计 50 §四硬约束:非连续区间)。
            Skin.Import(dto, BlockSkinCatalog.MonoIds);
        }

        /// <summary>神庙 bool 数组保底:null 或长度≠HallCount 时重建为全 false;否则原样(深拷贝避免共享引用)。</summary>
        private static bool[] NormalizeTempleArray(bool[] src)
        {
            if (src == null || src.Length != TempleConfig.HallCount)
                return new bool[TempleConfig.HallCount];
            return (bool[])src.Clone();
        }

    }
}
