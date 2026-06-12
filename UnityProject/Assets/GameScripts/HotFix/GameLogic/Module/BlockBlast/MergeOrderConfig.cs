namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 合成+订单+体力 Demo 切片的静态配置（仿 08 的 <see cref="CollectDemo"/>，不接 Luban）。
    /// 合成 / 订单 / 体力 / 掉率 / 兜底全部硬编码可调常量 + 手编循环订单池。
    /// 改数即调难度。默认值取自设计文档 §五配置表。
    /// 表现（glyph / 纯色）直接复用 <see cref="CollectDemo.Glyph"/> / <see cref="CollectDemo.ColorOf"/>。
    /// </summary>
    public static class MergeOrderConfig
    {
        // ── 合成 ──────────────────────────────────────────────
        /// <summary>封顶等级（Lv1→Lv2→Lv3）。到顶不再合并，堆积等待订单消耗。</summary>
        public const int MaxLevel = 3;

        /// <summary>等级折算基础元素数（Lv1=1, Lv2=2, Lv3=4）。索引 = 等级，[0] 占位。</summary>
        public static readonly int[] LevelBaseCost = { 0, 1, 2, 4 };

        // ── 订单 ──────────────────────────────────────────────
        /// <summary>同时激活的订单数（双订单）。</summary>
        public const int ActiveOrders = 2;

        /// <summary>订单奖励体力（可溢出软上限）。</summary>
        public const int OrderRewardEnergy = 8;

        /// <summary>订单奖励分数系数：分数 = 等级 × 数量 × 此值。</summary>
        public const int OrderScoreFactor = 50;

        /// <summary>demo 通关所需完成单数。</summary>
        public const int DemoGoalOrders = 5;

        // ── 体力 ──────────────────────────────────────────────
        /// <summary>起始体力。核心不变量：≥ 完成首单所需落子数，否则首单前饿死。</summary>
        public const int EnergyStart = 20;

        /// <summary>体力软上限。自然恢复 / 落子返还封顶于此；订单奖励可溢出。</summary>
        public const int EnergyCap = 30;

        /// <summary>每次落子消耗体力。</summary>
        public const int PlaceCost = 1;

        // 自然恢复：demo 暂不实装（单局测试意义小，主补给走订单奖励）。
        // 常量保留留口子：若实装，挂窗口计时按此回到软上限、不溢出。
        /// <summary>自然恢复每 tick 回复量（暂不实装，留口子）。</summary>
        public const int RegenPerTick = 1;
        /// <summary>自然恢复间隔秒数（暂不实装，留口子）。</summary>
        public const float RegenIntervalSec = 120f;

        // ── 掉率 / 保底 ────────────────────────────────────────
        /// <summary>候选块每填充格注入订单所需类型的概率。</summary>
        public const double InjectChance = 0.30;

        /// <summary>
        /// 保底阈值：连续这么多次候选块构建都未注入订单所需类型后，
        /// 下一候选块强制注入一个。绕过概率，保证订单永不被随机饿死。
        /// </summary>
        public const int PityThreshold = 8;

        // ── 兜底 ──────────────────────────────────────────────
        /// <summary>每局免费悔棋次数（无广告 / 无内购）。</summary>
        public const int UndoCharges = 3;

        // ── 循环订单池（手编锯齿波节奏）─────────────────────────
        // 难度量 d(单) = 数量 × 2^(等级-1) = 折算基础(Lv1)元素数。手工编排成「难单后必出简单单」，
        // 保证每个目标都够得着。NextOrder() 顺序取下一项，到尾循环。
        // 完整程序化锯齿波（依进度动态算 d 并约束相邻一高一低）见文档 §3.2，demo 不实装。
        //
        // 可满足性约束（源于 #7 自动配对）：每个 (类型,非封顶等级) 库存恒 ≤1（满 2 即升级），
        // 故 Lv1/Lv2 订单数量只能为 1；唯有封顶 Lv3 可堆积，数量方可 ≥2。文档 §3.2 的
        // 示例「Lv1 ×3」在自动配对下不可达，本池据此只取可满足组合（决策记 state/dev.md）。
        /// <summary>循环订单池。</summary>
        public static readonly Order[] OrderPool =
        {
            new Order(CollectElement.Diamond, 1, 1), // d1 易   [初始槽0]
            new Order(CollectElement.Star,    2, 1), // d2 易   [初始槽1]
            new Order(CollectElement.Diamond, 3, 1), // d4 中
            new Order(CollectElement.Leaf,    1, 1), // d1 易
            new Order(CollectElement.Star,    3, 2), // d8 难
            new Order(CollectElement.Diamond, 1, 1), // d1 易（难单后回落）
            new Order(CollectElement.Heart,   2, 1), // d2 易-中
            new Order(CollectElement.Leaf,    3, 1), // d4 中
        };
    }
}
