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
    /// 纯逻辑、可单测：合成区自动配对升级、订单队列交付/刷新、体力扣/返/补、保底计数器、悔棋快照栈。
    /// 棋盘 / 元素层 / 候选槽仍住在 BlockGameState；悔棋整体回滚由 <see cref="CaptureSnapshot"/> /
    /// <see cref="Undo"/> 协调（需传入 BlockGameState + BinaryBoard）。
    /// </summary>
    public sealed class MergeOrderState
    {
        /// <summary>当前体力。</summary>
        public int Energy;

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

        /// <summary>剩余悔棋次数。</summary>
        public int UndoCharges;

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
        /// 持有式、无倒计时、无硬上限（设计 12 §3.3）。入悔棋快照（单局内回滚正确），不单独磁盘存盘。
        /// </summary>
        public int BlindBoxCount;

        /// <summary>特殊订单轨（设计 11 §三：0/1 占槽 + 等待队列，剧情&gt;加急&gt;黄金时段）。</summary>
        public readonly SpecialOrderTrack SpecialTrack = new SpecialOrderTrack();

        // ── 长期主线：虔诚币 + 神庙修复 + 经验·守护者等级（设计 13）──
        /// <summary>
        /// 虔诚币（长期主线货币，设计 13）。唯一来源 = 订单交付的主要奖励；唯一出口 = 修复神庙大厅。
        /// 只增不减地累积（<see cref="AddPiety"/> 仅接受正数），仅在 <see cref="RepairTemple"/> 处扣减。
        /// 入悔棋快照（单局内回滚正确），不单独磁盘存盘——与 灵力/盲盒计数 同口径。
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

        // 悔棋快照栈：每次落子前压入一份全量状态；Undo 弹出并回滚。
        // 只在 UndoCharges>0 时压栈，避免次数耗尽后无意义增长。
        private readonly Stack<Snapshot> _undoStack = new Stack<Snapshot>();

        /// <summary>开新一局：起始体力 + 空合成区 + 初始订单 + 清计数器 + 满悔棋次数。</summary>
        public void Reset()
        {
            Energy = MergeOrderConfig.EnergyStart;
            Inventory.Clear();
            OrderCursor = 0;
            CompletedOrders = 0;
            TotalScore = 0;
            PendingElements.Clear(); // 开局队空 → 首手纯方块
            _needRotor = 0;
            UndoCharges = MergeOrderConfig.UndoCharges;
            _undoStack.Clear();

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
        /// 交付为已提交的经济动作：清空悔棋栈（落子后再交付，悔棋不应倒回已交付的库存/进度）。
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

            _undoStack.Clear();
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
            // 附赠随交付一起固化，不被悔棋倒回（交付清空悔棋栈，符合「已交付」语义）。
            AddBlindBox(BlindBoxPerSpecial(SpecialTrack.Occupied.Kind));

            // 长期主线（设计 13 §3.1）：特殊订单虔诚币 = 难度 × 旋钮 × 特殊溢价倍率。纯追加。
            AddPiety(o.Difficulty * TempleConfig.PietyPerDifficulty * TempleConfig.SpecialPietyMult);

            SpecialTrack.OnDelivered();
            _undoStack.Clear(); // 已提交动作，悔棋不倒回特殊单交付
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
        /// 修复是已提交的经济动作：成功后清空悔棋栈（悔棋不倒回已修的厅/已扣的币，与交付同语义）。
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

            _undoStack.Clear(); // 已提交动作，悔棋不倒回已修厅/已扣币
            return true;
        }

        // ── 跨会话磁盘存档:导出/导入元层(设计 14 §3.3-§3.6)─────────
        // ExportMeta/ImportMeta 是纯方法:无 IO、无 UniTask,可在纯 C# 单测里同步调用。
        // 磁盘外壳(SaveAsync/LoadAsync)在 MergeMetaPersistence,本类不 using UniTask、不碰磁盘。
        // 与悔棋快照 Snapshot 是两条独立轨:ImportMeta 不触 _undoStack,只覆盖元字段。

        /// <summary>
        /// 落盘脏位:元变更后由窗口侧 <see cref="RequestSave"/> 标脏,合并一串连带变更成一次落盘(§3.4)。
        /// 不进悔棋快照(脏位是落盘调度状态,非玩法状态)、不进 Export/Import(纯调度,不持久)。
        /// </summary>
        private bool _saveDirty;

        /// <summary>标脏:有意义元变更后由窗口侧调用,延迟到合适节点合并落盘(§3.4)。不立即写盘。</summary>
        public void RequestSave() => _saveDirty = true;

        /// <summary>当前是否有未落盘的元变更(窗口侧据此决定是否 SaveAsync)。</summary>
        public bool IsSaveDirty => _saveDirty;

        /// <summary>清脏(落盘成功后由窗口侧调用)。</summary>
        public void ClearSaveDirty() => _saveDirty = false;

        /// <summary>
        /// 导出元层进度到 DTO(设计 14 §3.1 进盘 13 项 + version + 当前祈愿重置日期)。纯方法、无 IO。
        /// version 置 <see cref="MergeMetaPersistence.CurrentVersion"/>;lastWishResetDate 取 <paramref name="today"/>
        /// (默认本地日期),保证落盘的日期与「今日祈愿」语义一致(§3.6)。
        /// </summary>
        public MergeMetaSave ExportMeta(string today = null)
        {
            return new MergeMetaSave
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
                completedOrders = CompletedOrders,
                totalScore = TotalScore,                            // O2:进盘当累计总分
                wishUsedToday = WishUsedToday,
                lastWishResetDate = today ?? MergeMetaPersistence.Today(),
            };
        }

        /// <summary>
        /// 用 DTO 覆盖元层进度(设计 14 §3.5 逐字段保底 + §3.6 跨天重置)。纯方法、无 IO、不触 _undoStack。
        /// 仅覆盖元字段(§3.1 进盘 13 项),不动局内瞬态(棋盘/手牌/订单/合成区/悔棋栈)——调用前须先 <see cref="Reset"/>
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
            CompletedOrders = dto.completedOrders;
            TotalScore = dto.totalScore;
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
        }

        /// <summary>神庙 bool 数组保底:null 或长度≠HallCount 时重建为全 false;否则原样(深拷贝避免共享引用)。</summary>
        private static bool[] NormalizeTempleArray(bool[] src)
        {
            if (src == null || src.Length != TempleConfig.HallCount)
                return new bool[TempleConfig.HallCount];
            return (bool[])src.Clone();
        }

        // ── demo 终点 ──────────────────────────────────────────

        /// <summary>完成单数达标即通关。</summary>
        public bool IsDemoComplete() => CompletedOrders >= MergeOrderConfig.DemoGoalOrders;

        // ── 悔棋（全量单步快照回滚）────────────────────────────

        /// <summary>
        /// 落子前压入全量快照：棋盘 / 元素层 / 候选槽 / 连击 / 体力 / 合成区 / 订单进度 / 元素预算队列 / 轮转游标。
        /// 仅在尚有悔棋次数时压栈。次数本身不进快照（否则悔棋后又满次数 → 无限悔棋）。
        /// </summary>
        public void CaptureSnapshot(BlockGameState s, BinaryBoard board)
        {
            if (UndoCharges <= 0) return;
            _undoStack.Push(Snapshot.Capture(s, board, this));
        }

        /// <summary>有快照且尚有次数即可悔棋。</summary>
        public bool CanUndo => UndoCharges > 0 && _undoStack.Count > 0;

        /// <summary>
        /// 悔棋：整体回滚到最近一次落子前的快照（方块退回待选槽、退回该次扣的体力一并随快照恢复），
        /// 消耗一次次数。返回 false 表示无可悔棋。
        /// </summary>
        public bool Undo(BlockGameState s, BinaryBoard board)
        {
            if (!CanUndo) return false;
            var snap = _undoStack.Pop();
            snap.Restore(s, board, this);
            UndoCharges -= 1;
            return true;
        }

        // 全量状态快照。深拷贝所有可变结构，避免与现场共享引用。
        private sealed class Snapshot
        {
            private int[][] _saveArr;
            private MergeElement[][] _elementArr;
            private int[] _rowBinary;
            private PendingPiece[] _opera;
            private int _combo;
            private int _energy;
            private Dictionary<(MergeElement, int), int> _inventory;
            private Order[] _orders;
            private int _orderCursor;
            private int _completed;
            private int _totalScore;
            private MergeElement[] _pendingElements;
            private int _needRotor;
            private int _comboChain;
            private bool _allClearArmed;
            private int _soul;
            private int _wishUsedToday;
            private int _goddessRating;
            private int _goddessLevel;
            private int _blindBoxCount;
            private SpecialOrder _special;
            private SpecialOrder[] _specialWaiting;
            // 长期主线（设计 13）：随 灵力/盲盒 同体例入快照，悔棋须回滚。
            private int _piety;
            private int _exp;
            private int _unlockedChapter;
            private int _nextRepairIndex;
            private bool[] _templeRepaired;
            private bool[] _templeDecorated;

            public static Snapshot Capture(BlockGameState s, BinaryBoard board, MergeOrderState m)
            {
                var snap = new Snapshot
                {
                    _saveArr = CloneIntGrid(s.SaveArr),
                    _elementArr = CloneElementGrid(s.ElementArr),
                    _rowBinary = (int[])board.RowBinary.Clone(),
                    _opera = (PendingPiece[])s.OperaArr.Clone(), // piece 对象落子时不被改写，浅拷贝引用即可退回槽位
                    _combo = s.Combo,
                    _energy = m.Energy,
                    _inventory = new Dictionary<(MergeElement, int), int>(m.Inventory),
                    _orders = (Order[])m.ActiveOrders.Clone(),
                    _orderCursor = m.OrderCursor,
                    _completed = m.CompletedOrders,
                    _totalScore = m.TotalScore,
                    _pendingElements = m.PendingElements.ToArray(), // 深拷贝队列：悔棋须回滚消除引发的元素入队
                    _needRotor = m._needRotor,
                    _comboChain = m.ComboChain,
                    _allClearArmed = m.AllClearArmed,
                    _soul = m.Soul,
                    _wishUsedToday = m.WishUsedToday,
                    _goddessRating = m.GoddessRating,
                    _goddessLevel = m.GoddessLevel,
                    _blindBoxCount = m.BlindBoxCount,
                    _special = m.SpecialTrack.Occupied,           // SpecialOrder 为不可变值类型，浅拷贝即可
                    _specialWaiting = m.SpecialTrack.SnapshotWaiting(),
                    _piety = m.Piety,
                    _exp = m.Exp,
                    _unlockedChapter = m.UnlockedChapter,
                    _nextRepairIndex = m.NextRepairIndex,
                    _templeRepaired = (bool[])m.TempleRepaired?.Clone(),   // 数组深拷贝，避免与现场共享
                    _templeDecorated = (bool[])m.TempleDecorated?.Clone(),
                };
                return snap;
            }

            public void Restore(BlockGameState s, BinaryBoard board, MergeOrderState m)
            {
                s.SaveArr = CloneIntGrid(_saveArr);
                s.ElementArr = CloneElementGrid(_elementArr);
                for (int r = 0; r < _rowBinary.Length; r++) board.RowBinary[r] = _rowBinary[r];
                for (int i = 0; i < s.OperaArr.Length && i < _opera.Length; i++) s.OperaArr[i] = _opera[i];
                s.Combo = _combo;

                m.Energy = _energy;
                m.Inventory.Clear();
                foreach (var kv in _inventory) m.Inventory[kv.Key] = kv.Value;
                m.ActiveOrders = (Order[])_orders.Clone();
                m.OrderCursor = _orderCursor;
                m.CompletedOrders = _completed;
                m.TotalScore = _totalScore;
                m.PendingElements.Clear();
                foreach (var e in _pendingElements) m.PendingElements.Enqueue(e);
                m._needRotor = _needRotor;
                m.ComboChain = _comboChain;
                m.AllClearArmed = _allClearArmed;
                m.Soul = _soul;
                m.WishUsedToday = _wishUsedToday;
                m.GoddessRating = _goddessRating;
                m.GoddessLevel = _goddessLevel;
                m.BlindBoxCount = _blindBoxCount;
                m.SpecialTrack.Occupied = _special;
                m.SpecialTrack.RestoreWaiting(_specialWaiting);
                m.Piety = _piety;
                m.Exp = _exp;
                m.UnlockedChapter = _unlockedChapter;
                m.NextRepairIndex = _nextRepairIndex;
                m.TempleRepaired = (bool[])_templeRepaired?.Clone();   // 深拷贝复原，避免快照与现场共享
                m.TempleDecorated = (bool[])_templeDecorated?.Clone();
            }

            private static int[][] CloneIntGrid(int[][] src)
            {
                if (src == null) return null;
                var dst = new int[src.Length][];
                for (int r = 0; r < src.Length; r++) dst[r] = (int[])src[r].Clone();
                return dst;
            }

            private static MergeElement[][] CloneElementGrid(MergeElement[][] src)
            {
                if (src == null) return null;
                var dst = new MergeElement[src.Length][];
                for (int r = 0; r < src.Length; r++) dst[r] = (MergeElement[])src[r].Clone();
                return dst;
            }
        }
    }
}
