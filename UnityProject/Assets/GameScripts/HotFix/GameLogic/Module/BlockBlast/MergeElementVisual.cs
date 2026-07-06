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
        /// 元素图标的寻址名：card_element 图标库（Assets/AssetRaw/UI/Atlas/game/card_element/）中对应 PNG 的文件名（去扩展名），
        /// 按「花色 + 品质色」取图（等级以卡面品质色区分，文件名形如 sword_blue）。经 SetSprite 按文件名直接定位 Sprite。
        /// <paramref name="level"/> 夹到 [1, 品质色数]（与 MergeOrderConfig.MaxLevel 同为 5）防越界。None 返回空串（无图）。
        /// </summary>
        public static string SpriteName(MergeElement type, int level)
        {
            string name = TypeName(type);
            if (string.IsNullOrEmpty(name)) return string.Empty;
            if (level < 1) level = 1;
            else if (level > LevelColorNames.Length) level = LevelColorNames.Length; // 与 MaxLevel 同为 5；MaxLevel 扩容而美术未扩时回落最高色
            return $"{name}_{LevelColorNames[level - 1]}";
        }
    }
}
