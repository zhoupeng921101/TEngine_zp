namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 4 种合成元素 + None。美术为塔罗花色卡（元素↔花色、等级↔品质色，图标寻址 {花色}_{品质色}）。
    /// None=0 表示该格无元素。枚举值连续（1..4），改值会与旧存档的 inventory/ElementArr 失配，需清档重进。
    /// 枚举成员名保留旧元素命名（服务端协议注释以此为准），与美术花色的对应见 <see cref="MergeElementVisual.TypeName"/>。
    /// </summary>
    public enum MergeElement
    {
        None = 0,
        Butterfly = 1,
        Chalice = 2,
        Scroll = 3,
        Star = 4,
    }

    /// <summary>合成元素的表现（图标寻址 / glyph），供 merge-order 模式渲染。</summary>
    public static class MergeElementVisual
    {
        /// <summary>元素对应的塔罗花色名（小写，即卡面图标文件名前缀）。None 返回空串。</summary>
        public static string TypeName(MergeElement e)
        {
            switch (e)
            {
                case MergeElement.Butterfly: return "sword";
                case MergeElement.Chalice: return "cup";
                case MergeElement.Scroll: return "wand";
                case MergeElement.Star: return "pentacle";
                default: return string.Empty;
            }
        }

        /// <summary>元素的展示符号（glyph，渲染兜底）。取塔罗花色↔扑克花色的通行对应（剑↔黑桃/杯↔红桃/杖↔梅花/星币↔方块）。</summary>
        public static string Glyph(MergeElement e)
        {
            switch (e)
            {
                case MergeElement.Butterfly: return "♠";
                case MergeElement.Chalice: return "♥";
                case MergeElement.Scroll: return "♣";
                case MergeElement.Star: return "♦";
                default: return string.Empty;
            }
        }

        /// <summary>等级 → 品质色名（卡面图标文件名后缀），下标 = level-1。</summary>
        private static readonly string[] LevelColorNames = { "white", "green", "blue", "purple", "gold" };

        /// <summary>
        /// 「花色_品质色」基名（如 sword_blue）：花色取 <see cref="TypeName"/>、品质色取等级对应色阶。
        /// 两套元素图集（card_element 大卡面 / samll_icon2 小图标）共用此基名，各自寻址方法在其上加前缀区分，避免按文件名全局定位时撞名。
        /// <paramref name="level"/> 夹到 [1, 品质色数]（与 MergeOrderConfig.MaxLevel 同为 5）防越界；MaxLevel 扩容而美术未扩时回落最高色。None 返回空串。
        /// </summary>
        private static string SuitColor(MergeElement type, int level)
        {
            string name = TypeName(type);
            if (string.IsNullOrEmpty(name)) return string.Empty;
            if (level < 1) level = 1;
            else if (level > LevelColorNames.Length) level = LevelColorNames.Length;
            return $"{name}_{LevelColorNames[level - 1]}";
        }

        /// <summary>
        /// 订单卡元素图标的寻址名：card_element 图标库（Assets/AssetRaw/UI/Atlas/game/card_element/）中对应 PNG 文件名（去扩展名），
        /// 即「花色_品质色」基名（如 sword_blue）。仅订单卡（大塔罗卡面）取此库；棋盘 / 候选 / 合成 / 飞行的小图标见 <see cref="IconSpriteName"/>。
        /// 经 SetSprite 按文件名直接定位 Sprite。None 返回空串（无图）。
        /// </summary>
        public static string SpriteName(MergeElement type, int level) => SuitColor(type, level);

        /// <summary>
        /// 棋盘 / 候选 / 合成 / 飞行元素图标的寻址名：samll_icon2_sliced 小图标库（Assets/AssetRaw/UI/Atlas/samll_icon2_sliced/）中对应 PNG 文件名，
        /// 即「icon2_花色_品质色」（如 icon2_sword_blue）。前缀避免与 card_element 的同基名在按文件名全局定位时撞名。
        /// 订单卡另取大卡面（见 <see cref="SpriteName"/>）。None 返回空串（无图）。
        /// </summary>
        public static string IconSpriteName(MergeElement type, int level)
        {
            string suitColor = SuitColor(type, level);
            return string.IsNullOrEmpty(suitColor) ? string.Empty : $"icon2_{suitColor}";
        }

        /// <summary>订单卡底框(等级品质框)张数：card_underframe_0..4 共 5 张。等级独立于花色，故与 LevelColorNames 同长但各自成规。</summary>
        private const int FrameCount = 5;

        /// <summary>
        /// 等级 → 订单卡底框(card_underframe 图集)寻址名：card_underframe_{level-1}（Atlas_game_card_underframe 内按文件名定位）。
        /// 等级越高框品质色越高。<paramref name="level"/> 夹到 [1, <see cref="FrameCount"/>] 防越界（MaxLevel 扩容而底框美术未扩时回落最高框）。
        /// </summary>
        public static string FrameSpriteName(int level)
        {
            if (level < 1) level = 1;
            else if (level > FrameCount) level = FrameCount;
            return $"card_underframe_{level - 1}";
        }
    }
}
