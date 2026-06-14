namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 长期主线（虔诚币 + 神庙修复 + 经验·守护者等级，设计 13）的静态配置。
    /// 仿 <see cref="MergeOrderConfig"/> 风格：全部硬编码可调常量 + 手编 12 厅造价数组，不接 Luban。
    /// 三条单向链：订单交付产虔诚币 → 攒够修 12 神庙大厅 → 修复产经验抬守护者等级 → 升级解锁剧情标记。
    /// 全确定性、无随机，逐档可单测。表现（厅名/glyph/纯色）由窗口侧组合。
    /// </summary>
    public static class TempleConfig
    {
        // ── 虔诚币产出旋钮（订单交付奖励，设计 13 §3.1）────────────
        /// <summary>虔诚币 = 订单难度 × 此值。调全局虔诚币慷慨度。</summary>
        public const int PietyPerDifficulty = 30;

        /// <summary>特殊订单虔诚币溢价倍率（特殊单更值钱）。</summary>
        public const int SpecialPietyMult = 2;

        // ── 12 神庙造价 + 顺序解锁（设计 13 §3.2）──────────────────
        /// <summary>神庙大厅总数（GDD：12 厅）。</summary>
        public const int HallCount = 12;

        /// <summary>第 0 厅基础造价（GDD 例「愚者大厅 500」）。</summary>
        public const int TempleBaseCost = 500;

        /// <summary>每厅造价线性递增步长：Cost(i) = TempleBaseCost + i*TempleCostStep。</summary>
        public const int TempleCostStep = 250;

        /// <summary>
        /// 12 厅手编数组（大阿尔卡那前 12 张命名，贴「塔罗」主题）。
        /// 造价以 §3.2 公式默认值逐项写死（允许将来对个别厅手工调价，不被公式绑死），
        /// 与 <see cref="MergeOrderConfig.OrderPool"/> 同款「公式定基调、数组可手调」风格。
        /// 改名/改价/增删厅只动这一处。
        /// </summary>
        public static readonly (string name, int cost)[] Halls =
        {
            ("愚者大厅",     500),  // The Fool       [0]
            ("魔术师大厅",   750),  // The Magician
            ("女祭司大厅",  1000),  // The High Priestess
            ("女皇大厅",    1250),  // The Empress
            ("皇帝大厅",    1500),  // The Emperor
            ("教皇大厅",    1750),  // The Hierophant
            ("恋人大厅",    2000),  // The Lovers
            ("战车大厅",    2250),  // The Chariot
            ("力量大厅",    2500),  // Strength
            ("隐士大厅",    2750),  // The Hermit
            ("命运之轮大厅", 3000), // Wheel of Fortune
            ("正义大厅",    3250),  // Justice         [11]
        };

        /// <summary>第 i 座（0-indexed）神庙造价。越界返回 0。</summary>
        public static int Cost(int index)
        {
            if (index < 0 || index >= Halls.Length) return 0;
            return Halls[index].cost;
        }

        /// <summary>第 i 座神庙名。越界返回空串。</summary>
        public static string HallName(int index)
        {
            if (index < 0 || index >= Halls.Length) return string.Empty;
            return Halls[index].name;
        }

        // ── 修复 / 升级发奖常量（设计 13 §3.3 / §3.5）───────────────
        /// <summary>修复一厅返还的体力（默认 30 = 回满软上限；受 EnergyCap 约束不溢出）。</summary>
        public const int TempleRepairEnergy = 30;

        /// <summary>每升一级返还的体力（受软上限）。</summary>
        public const int LevelUpEnergy = 15;

        // ── 经验 → 守护者等级曲线（设计 13 §3.4）────────────────────
        /// <summary>1→2 级所需经验（线性递增基数）。</summary>
        public const int LevelExpBase = 500;

        /// <summary>每级升级所需经验的递增步长。</summary>
        public const int LevelExpStep = 300;

        /// <summary>
        /// 从第 L 级升到第 L+1 级所需经验（L 从 1 起）：LevelExpBase + (L-1)*LevelExpStep。
        /// L1→2=500、L2→3=800、L3→4=1100…逐档线性递增。L&lt;1 视作 1。
        /// </summary>
        public static int ExpToNext(int level)
        {
            if (level < 1) level = 1;
            return LevelExpBase + (level - 1) * LevelExpStep;
        }

        /// <summary>
        /// 守护者等级 = 累积经验能买到的最高等级（从 1 起，等级是经验的纯函数，不单独存等级值）。
        /// totalExp=0 → 1 级；恰好达门槛升级（用 &gt;=）；只增不减。
        /// </summary>
        public static int GuardianLevelFor(int totalExp)
        {
            int level = 1;
            int remaining = totalExp;
            int need = ExpToNext(level);
            while (remaining >= need)
            {
                remaining -= need;
                level++;
                need = ExpToNext(level);
            }
            return level;
        }
    }
}
