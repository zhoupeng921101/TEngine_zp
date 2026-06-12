namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 合成+订单+体力 Demo 切片的静态配置（仿 08 的 <see cref="MergeElementVisual"/>，不接 Luban）。
    /// 合成 / 订单 / 体力 / 得分驱动元素生成 / 兜底全部硬编码可调常量 + 手编循环订单池。
    /// 改数即调难度。默认值取自设计文档 §五配置表。
    /// 表现（glyph / 纯色）直接复用 <see cref="MergeElementVisual.Glyph"/> / <see cref="MergeElementVisual.ColorOf"/>。
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

        // ── 得分驱动元素生成 ──────────────────────────────────
        // 该次消除得分 → 元素数量 k：消得越狠、后续候选块携带的元素越多；无消除→候选块纯方块。
        // 确定性映射（无随机），逐档可单测。

        /// <summary>每多少分折算 1 个元素。「灵活」旋钮：调小更慷慨、调大更吝啬。</summary>
        public const int ScorePerElement = 200;

        /// <summary>单次消除产元素数下限（保底）：任何成功消除至少产 1，小消除不空手。</summary>
        public const int MinElementsPerClear = 1;

        /// <summary>单次消除产元素数上限（封顶）：挡住超高连消刷爆经济。</summary>
        public const int MaxElementsPerClear = 4;

        /// <summary>预算队列总积压上限（≈一组候选块容量）：超出则不再入队，避免元素积压远超候选格承接。</summary>
        public const int MaxPendingElements = 12;

        /// <summary>
        /// 该次消除得分 → 元素数量映射：clearScore≤0 产 0（无消除/开局纯方块）；
        /// 否则 Clamp(CeilDiv(clearScore, ScorePerElement), Min, Max)。单调递增、确定性。
        /// </summary>
        public static int ElementsForScore(int clearScore)
        {
            if (clearScore <= 0) return 0;
            int k = (clearScore + ScorePerElement - 1) / ScorePerElement; // CeilDiv（clearScore>0）
            if (k < MinElementsPerClear) k = MinElementsPerClear;
            if (k > MaxElementsPerClear) k = MaxElementsPerClear;
            return k;
        }

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
            new Order(MergeElement.Diamond, 1, 1), // d1 易   [初始槽0]
            new Order(MergeElement.Star,    2, 1), // d2 易   [初始槽1]
            new Order(MergeElement.Diamond, 3, 1), // d4 中
            new Order(MergeElement.Leaf,    1, 1), // d1 易
            new Order(MergeElement.Star,    3, 2), // d8 难
            new Order(MergeElement.Diamond, 1, 1), // d1 易（难单后回落）
            new Order(MergeElement.Heart,   2, 1), // d2 易-中
            new Order(MergeElement.Leaf,    3, 1), // d4 中
        };
    }
}
