using GameLogic.Config;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 合成+订单+体力 Demo 切片的静态配置（仿 08 的 <see cref="MergeElementVisual"/>）。
    /// 合成 / 订单 / 体力 / 多消元素产出(纯 N 函数) / 兜底多为硬编码可调常量 + 手编循环订单池。
    /// 其中订单数 / 订单刷新间隔 / 体力恢复间隔 / 体力上限四项运行时读 Luban global 键值表（经 <see cref="GlobalConfigMgr"/>，缺键回退默认）。
    /// 改数即调难度。常量默认值取自设计文档 §五配置表；走配置的四项默认值与 global 表初值一致。
    /// 表现（glyph）直接复用 <see cref="MergeElementVisual.Glyph"/>。
    /// </summary>
    public static class MergeOrderConfig
    {
        // ── 合成 ──────────────────────────────────────────────
        /// <summary>封顶等级（Lv1→…→Lv5）。到顶不再合并，堆积等待订单消耗。</summary>
        public const int MaxLevel = 5;

        /// <summary>
        /// 合成比：满 MergeCount 个同 (类型,等级) 合成 1 个上一级。
        /// 折算因子（等级折算成多少个基础 Lv1 元素，虔诚币/难度计量用）= MergeCount^(等级-1)。
        /// </summary>
        public const int MergeCount = 4;

        /// <summary>整数幂 b^e（e≥0）。避免 Math.Pow 浮点；折算因子统一走此函数。</summary>
        public static int Pow(int b, int e)
        {
            int r = 1;
            for (int i = 0; i < e; i++) r *= b;
            return r;
        }

        /// <summary>等级折算基础元素数 = MergeCount^(等级-1)（Lv1=1, Lv2=4, Lv3=16, Lv4=64, Lv5=256）。索引 = 等级，[0] 占位。</summary>
        public static readonly int[] LevelBaseCost = { 0, 1, 4, 16, 64, 256 };

        // ── 订单 ──────────────────────────────────────────────
        /// <summary>
        /// 同时激活的订单数（横滑订单列表，可交付的卡排在前）。运行时读 global 表（id=1），缺键回退默认。
        /// 原为编译期 const，改运行时属性后：数组定长改为运行时分配；老存档按旧值（如 5）存的订单数组长度
        /// 与本值不符时，<see cref="MergeOrderState.ImportIngame"/> 走「保持 Reset 建好的订单」兜底路径，不崩。
        /// </summary>
        public static int ActiveOrders => GlobalConfigMgr.OrderCountValue;

        /// <summary>订单按时整批刷新间隔（真实秒，id=2）。每经过此秒数，激活订单整批替换为新一批。</summary>
        public static int OrderRefreshIntervalSec => GlobalConfigMgr.OrderRefreshSecondsValue;

        /// <summary>订单奖励体力（可溢出软上限）。</summary>
        public const int OrderRewardEnergy = 8;

        /// <summary>订单奖励分数系数：分数 = 等级 × 数量 × 此值。</summary>
        public const int OrderScoreFactor = 50;

        // ── 体力 ──────────────────────────────────────────────
        /// <summary>起始体力。核心不变量：≥ 完成首单所需落子数，否则首单前饿死。</summary>
        public const int EnergyStart = 20;

        /// <summary>
        /// 体力软上限。自然恢复 / 落子返还封顶于此；订单奖励可溢出。运行时读 global 表（id=4），缺键回退默认。
        /// 不变量 <c>ClearToolCost ≤ EnergyCap</c> 由编译期成立转为运行时依赖：两项均读 global 表，当前表值满足，
        /// 配置错时不崩（仅可能让脱困道具体力 gate 失衡），不在此处强校验。
        /// </summary>
        public static int EnergyCap => GlobalConfigMgr.EnergyRecoverCapValue;

        /// <summary>每次落子消耗体力。</summary>
        public const int PlaceCost = 1;

        // ── 时基恢复（无尽模型兜底，设计 49 §3.2）──────────────────
        // 无条件、纯时间驱动、含离线累计：每 RegenIntervalSec 真实秒回 RegenPerTick 点，封顶软上限不溢出。
        // 不依赖落子 / 消除 / 交付任何玩法动作（否则卡死时永不恢复，脱困死结，设计 49 §3.3 硬约束 3）。
        // 量与间隔均读 global 表（id=3 "点数#间隔秒"，经 GlobalConfigMgr 解析；旧 bare int 兼容为 量=1、间隔=该值，缺键回退默认）。
        /// <summary>时基恢复每 tick 回复量（每满间隔回此点数）。运行时读 global 表（id=3 的点数段），缺键 / 旧格式回退 1。</summary>
        public static int RegenPerTick => GlobalConfigMgr.EnergyRecoverAmount;
        /// <summary>时基恢复间隔秒数（每满此秒数回一次）。运行时读 global 表（id=3 的间隔段），缺键回退默认。</summary>
        public static float RegenIntervalSec => GlobalConfigMgr.EnergyRecoverIntervalSeconds;

        // ── 消除道具（无尽模型脱困兜底，设计 49 §3.1）──────────────
        // 主动清「一整行 + 一整列」让卡死棋盘重新可落；代价体力、无限可用、只 gate 体力（绝不做有限消耗品）。
        /// <summary>
        /// 消除道具代价体力。运行时读 global 表（id=5），缺键回退默认 5。硬约束 <c>ClearToolCost ≤ EnergyCap</c>（5 ≤ 30）：
        /// 否则体力封顶后仍不够用一次消除道具，「卡死 + 0 体力」脱困死结重现（设计 49 §3.3 硬约束 1）。
        /// </summary>
        public static int ClearToolCost => GlobalConfigMgr.ClearToolEnergyCostValue;

        // ── 多消元素产出（纯 N 函数）──────────────────────────
        // 单次落子清的行列总数 N → 本次多消的全部元素产出 (Lv1, Lv2, Lv3)。纯 N 函数、无随机、与得分无关，
        // 逐档可单测。落点：Lv1 入候选块预算队列（EnqueueScoreElements），Lv2/Lv3 直发收集区（AddDirect）。
        // 得分（ClearScore / 显示分 / 连消倍率）独立保留，不再驱动元素数量。

        /// <summary>预算队列总积压上限（≈一组候选块容量）：超出则不再入队，避免元素积压远超候选格承接。</summary>
        public const int MaxPendingElements = 12;

        /// <summary>
        /// N（本次落子清的行列总数）→ 本次多消的全部元素产出 (lv1, lv2, lv3)。单一信息源、逐档可单测：
        /// N=1→(1,0,0)；N=2→(3,0,0)；N=3→(1,1,0)；N=4→(3,1,0)；N=5→(2,2,0)；N≥6→(0,0,1)；N≤0→全 0。
        /// 全清额外 1 Lv3 不在此表（由 Settle 在此基础上叠加，见 AllClearRewardLevel/Count）。
        /// </summary>
        public static (int lv1, int lv2, int lv3) ElementsForLines(int lines)
        {
            switch (lines)
            {
                case 1:  return (1, 0, 0);
                case 2:  return (3, 0, 0);
                case 3:  return (1, 1, 0);
                case 4:  return (3, 1, 0);
                case 5:  return (2, 2, 0);
                default: return lines >= 6 ? (0, 0, 1) : (0, 0, 0); // ≥6 封顶 Lv3；≤0 无产出
            }
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

        // ── 多消弹字阈值 ─ 设计 11 §5.2 ─────────────────────────
        /// <summary>多消弹字（"Great" 等）展示的最小行列数（&lt; 此值不显多消弹字，走连消弹字）。</summary>
        public const int MultiClearMilestoneMinLines = 3;

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
        /// <summary>
        /// 满档所需全清次数（显示 N/GoddessRatingGoal，攒满可领取一次奖励，可循环）。
        /// 运行时读 global 表（id=7），缺键回退默认 10；与服务端 GoddessRatingUpperBound 同源（服务端读同一 id）。
        /// </summary>
        public static int GoddessRatingGoal => GlobalConfigMgr.GoddessMaxCountValue;

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
        // 难度量 d(单) = 数量 × MergeCount^(等级-1) = 折算基础(Lv1)元素数。手工编排成「难单后必出简单单」，
        // 保证每个目标都够得着。NextOrder() 顺序取下一项，到尾循环。
        // 完整程序化锯齿波（依进度动态算 d 并约束相邻一高一低）见文档 §3.2，demo 不实装。
        //
        // 可满足性约束（源于 #7 自动配对）：每个 (类型,非封顶等级) 库存恒 ≤ MergeCount-1（满 MergeCount 即升级），
        // 故非封顶等级订单数量只要 ≤ MergeCount-1 即可堆积满足；封顶 Lv5 不再升级、可无限堆积。
        // 本池保守只取数量小的组合，远在可满足范围内。
        /// <summary>循环订单池。</summary>
        public static readonly Order[] OrderPool =
        {
            new Order(MergeElement.Butterfly, 1, 1), // d1   易   [初始槽0]
            new Order(MergeElement.Chalice,   2, 1), // d4   易   [初始槽1]
            new Order(MergeElement.Scroll,    3, 1), // d16  中
            new Order(MergeElement.Star,      1, 1), // d1   易
            new Order(MergeElement.Butterfly, 5, 2), // d512 难（唯一封顶堆积单，数量≥2 落封顶 Lv5）
            new Order(MergeElement.Chalice,   1, 1), // d1   易（难单后回落）
            new Order(MergeElement.Scroll,    2, 1), // d4   易-中
            new Order(MergeElement.Star,      3, 1), // d16  中
        };
    }
}
