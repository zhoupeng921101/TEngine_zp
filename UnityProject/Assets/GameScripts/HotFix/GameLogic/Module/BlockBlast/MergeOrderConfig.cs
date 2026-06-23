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
        /// <summary>封顶等级（Lv1→…→Lv5）。到顶不再合并，堆积等待订单消耗。</summary>
        public const int MaxLevel = 5;

        /// <summary>等级折算基础元素数 = 2^(等级-1)（Lv1=1, Lv2=2, Lv3=4, Lv4=8, Lv5=16）。索引 = 等级，[0] 占位。</summary>
        public static readonly int[] LevelBaseCost = { 0, 1, 2, 4, 8, 16 };

        // ── 订单 ──────────────────────────────────────────────
        /// <summary>同时激活的订单数（双订单）。</summary>
        public const int ActiveOrders = 2;

        /// <summary>订单奖励体力（可溢出软上限）。</summary>
        public const int OrderRewardEnergy = 8;

        /// <summary>订单奖励分数系数：分数 = 等级 × 数量 × 此值。</summary>
        public const int OrderScoreFactor = 50;

        // ── 体力 ──────────────────────────────────────────────
        /// <summary>起始体力。核心不变量：≥ 完成首单所需落子数，否则首单前饿死。</summary>
        public const int EnergyStart = 20;

        /// <summary>体力软上限。自然恢复 / 落子返还封顶于此；订单奖励可溢出。</summary>
        public const int EnergyCap = 30;

        /// <summary>每次落子消耗体力。</summary>
        public const int PlaceCost = 1;

        // ── 时基恢复（无尽模型兜底，设计 49 §3.2）──────────────────
        // 无条件、纯时间驱动、含离线累计：每 RegenIntervalSec 真实秒回 RegenPerTick 点，封顶软上限不溢出。
        // 不依赖落子 / 消除 / 交付任何玩法动作（否则卡死时永不恢复，脱困死结，设计 49 §3.3 硬约束 3）。
        /// <summary>时基恢复每 tick 回复量（设计 49 §3.2 默认每 120 秒 +1）。</summary>
        public const int RegenPerTick = 1;
        /// <summary>时基恢复间隔秒数（设计 49 §3.2 默认 120 秒）。</summary>
        public const float RegenIntervalSec = 120f;

        // ── 消除道具（无尽模型脱困兜底，设计 49 §3.1）──────────────
        // 主动清「一整行 + 一整列」让卡死棋盘重新可落；代价体力、无限可用、只 gate 体力（绝不做有限消耗品）。
        /// <summary>
        /// 消除道具代价体力（设计 49 §3.1 默认 25）。硬约束 <c>ClearToolCost ≤ EnergyCap</c>（25 ≤ 30）：
        /// 否则体力封顶后仍不够用一次消除道具，「卡死 + 0 体力」脱困死结重现（设计 49 §3.3 硬约束 1）。
        /// </summary>
        public const int ClearToolCost = 25;

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

        // ── 灵力（单货币）+ 祈愿兑体力（去变现续命）─ 设计 11 §四/§7.1 ─────────
        // 双货币降为单货币：灵力 = 订单/宝箱/女神奖励 + 养成消耗 + 兑体力。无付费/无广告。
        /// <summary>每日祈愿兑体力次数上限（纯灵力消耗，非购买）。</summary>
        public const int WishPerDayLimit = 3;
        /// <summary>一次祈愿消耗的灵力。</summary>
        public const int WishSoulCost = 20;
        /// <summary>一次祈愿补回的体力（回到软上限，不溢出）。</summary>
        public const int WishEnergyGain = 10;

        // ── 消除锤 ─ 设计 11 §7.2（20→8 已拍板）─────────────────
        /// <summary>消除锤代价：摧毁待选区一个方块的体力消耗。= 一单回血，应急但肉疼。</summary>
        public const int HammerCost = 8;

        // ── 连消倍率 ─ 设计 11 §5.3（连续落子链，仅乘显示分）──────
        // 索引 = 连消链长（1 起）。链长 ≥ 数组末项即取末项（封顶 ×2.0）。链断回链长 1（×1.0）。
        // 整数千分比避免浮点不可单测：×1.0=1000、×1.2=1200…，调用方 score*permille/1000。
        /// <summary>连消倍率千分比，索引 0=链长1、1=链长2…末项及以上封顶。</summary>
        public static readonly int[] ComboMultPermille = { 1000, 1200, 1500, 1800, 2000 };

        /// <summary>连消链长 → 倍率千分比（链长 1 起；超数组长取封顶末项）。</summary>
        public static int ComboMultPermilleFor(int comboLen)
        {
            if (comboLen < 1) comboLen = 1;
            int idx = comboLen - 1;
            if (idx >= ComboMultPermille.Length) idx = ComboMultPermille.Length - 1;
            return ComboMultPermille[idx];
        }

        // ── 多消里程碑加码 ─ 设计 11 §5.2（单次落子清行列数，直发跳过合成）─
        // 在「得分驱动基础产出」之上，按单次落子的多消数（lines）额外直发中/高级图案进收集区。
        // 阈值与产物逐档对齐原稿「3 连击=1 中级…6 连击及以上=1 高级」。

        /// <summary>多消里程碑触发的最小行列数（&lt; 此值无加码）。</summary>
        public const int MultiClearMilestoneMinLines = 3;

        /// <summary>
        /// 多消里程碑加码：单次落子清 lines 行列时，额外直发的图案（等级,数量）列表。
        /// 3消→+1 Lv2；4消→+1 Lv2 +1 Lv1；5消→+1 Lv2 +2 Lv1；6+消→+1 Lv3。lines&lt;3 返回空。
        /// 返回 (level, count) 元组数组，level 直接进收集区对应等级（不经合成级联，由调用方 AddDirect）。
        /// </summary>
        public static (int level, int count)[] MultiClearMilestoneBonus(int lines)
        {
            if (lines < MultiClearMilestoneMinLines) return System.Array.Empty<(int, int)>();
            switch (lines)
            {
                case 3:  return new[] { (2, 1) };
                case 4:  return new[] { (2, 1), (1, 1) };
                case 5:  return new[] { (2, 1), (1, 2) };
                default: return new[] { (3, 1) }; // 6+ 直达封顶 Lv3
            }
        }

        /// <summary>多消即时弹字。索引 = 行列数（1 起）；超数组取末项。</summary>
        public static readonly string[] MultiClearLabels =
            { "Good", "Amazing", "Great", "Wonderful", "Excellent" };

        /// <summary>多消行列数 → 弹字（1 起；超数组取封顶末项 Excellent）。</summary>
        public static string MultiClearLabelFor(int lines)
        {
            if (lines < 1) return string.Empty;
            int idx = lines - 1;
            if (idx >= MultiClearLabels.Length) idx = MultiClearLabels.Length - 1;
            return MultiClearLabels[idx];
        }

        // ── 全清奖励 ─ 设计 11 §5.4 ─────────────────────────────
        /// <summary>全清奖励图案等级（高级直发）。</summary>
        public const int AllClearRewardLevel = 3;
        /// <summary>全清奖励图案数量。</summary>
        public const int AllClearRewardCount = 1;

        // ── 女神（全清累计进度）─ 设计 11 §十 ───────────────────
        /// <summary>好评条满档所需全清次数（显示 N/GoddessRatingGoal）。</summary>
        public const int GoddessRatingGoal = 10;

        // ── 智能生成 R1–R3 仲裁阈值 ─ 设计 11 §八 ────────────────
        /// <summary>R2 防卡死：连续未消除落子数阈值（≥ 此值发可消方块）。</summary>
        public const int NoClearThreshold = 5;
        /// <summary>R1 清盘后增难：全清后连发的异形块配额。</summary>
        public const int AntiStreakCount = 3;
        /// <summary>R2 清盘引导：剩余非空格 ≤ 此值时尝试促全清。</summary>
        public const int ClearGuideThreshold = 8;
        /// <summary>R3 高阶引导触发的最小多消数。</summary>
        public const int MultiGuideMinLines = 3;

        // ── 循环订单池（手编锯齿波节奏）─────────────────────────
        // 难度量 d(单) = 数量 × 2^(等级-1) = 折算基础(Lv1)元素数。手工编排成「难单后必出简单单」，
        // 保证每个目标都够得着。NextOrder() 顺序取下一项，到尾循环。
        // 完整程序化锯齿波（依进度动态算 d 并约束相邻一高一低）见文档 §3.2，demo 不实装。
        //
        // 可满足性约束（源于 #7 自动配对）：每个 (类型,非封顶等级) 库存恒 ≤1（满 2 即升级），
        // 故非封顶等级（Lv1..Lv4）订单数量只能为 1；唯有封顶 Lv5 可堆积，数量方可 ≥2。
        // 文档 §3.2 的示例「Lv1 ×3」在自动配对下不可达，本池据此只取可满足组合。
        /// <summary>循环订单池。</summary>
        public static readonly Order[] OrderPool =
        {
            new Order(MergeElement.Butterfly, 1, 1), // d1  易   [初始槽0]
            new Order(MergeElement.Chalice,   2, 1), // d2  易   [初始槽1]
            new Order(MergeElement.Scroll,    3, 1), // d4  中
            new Order(MergeElement.Star,      1, 1), // d1  易
            new Order(MergeElement.Butterfly, 5, 2), // d32 难（唯一封顶堆积单，数量≥2 只能落封顶 Lv5）
            new Order(MergeElement.Chalice,   1, 1), // d1  易（难单后回落）
            new Order(MergeElement.Scroll,    2, 1), // d2  易-中
            new Order(MergeElement.Star,      3, 1), // d4  中
        };
    }
}
