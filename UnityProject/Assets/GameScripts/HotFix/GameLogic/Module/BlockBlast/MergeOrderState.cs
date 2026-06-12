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

        /// <summary>特殊订单轨（设计 11 §三：0/1 占槽 + 等待队列，剧情&gt;加急&gt;黄金时段）。</summary>
        public readonly SpecialOrderTrack SpecialTrack = new SpecialOrderTrack();

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

            SpecialTrack.OnDelivered();
            _undoStack.Clear(); // 已提交动作，悔棋不倒回特殊单交付
            return true;
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
            private SpecialOrder _special;
            private SpecialOrder[] _specialWaiting;

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
                    _special = m.SpecialTrack.Occupied,           // SpecialOrder 为不可变值类型，浅拷贝即可
                    _specialWaiting = m.SpecialTrack.SnapshotWaiting(),
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
                m.SpecialTrack.Occupied = _special;
                m.SpecialTrack.RestoreWaiting(_specialWaiting);
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
