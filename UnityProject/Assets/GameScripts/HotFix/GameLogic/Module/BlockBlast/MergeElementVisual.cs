using UnityEngine;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 9 种合成元素。枚举值 = 原版配置 Key（省一层映射）。
    /// None=0 表示该格无元素。
    /// 源：参考工程 GameState.ts 的 ElementType + COLLECT_KEY_MAP。
    /// </summary>
    public enum MergeElement
    {
        None = 0,
        Diamond = 100,  // ◆
        Pentagon = 101, // ⬟
        Star = 102,     // ★
        Heart = 103,    // ♥
        Sun = 104,      // ☀
        Moon = 105,     // ☾
        Leaf = 106,     // ✿
        Crown = 107,    // ♕
        Stone = 200,    // ⬢
    }

    /// <summary>合成元素的表现（glyph / 纯色，零美术），供 merge-order 模式渲染。</summary>
    public static class MergeElementVisual
    {
        /// <summary>元素的展示符号（glyph，零美术）。</summary>
        public static string Glyph(MergeElement e)
        {
            switch (e)
            {
                case MergeElement.Diamond: return "◆";
                case MergeElement.Pentagon: return "⬟";
                case MergeElement.Star: return "★";
                case MergeElement.Heart: return "♥";
                case MergeElement.Sun: return "☀";
                case MergeElement.Moon: return "☾";
                case MergeElement.Leaf: return "✿";
                case MergeElement.Crown: return "♕";
                case MergeElement.Stone: return "⬢";
                default: return string.Empty;
            }
        }

        /// <summary>
        /// 元素图标的寻址名：clip 图标库（Assets/AssetRaw/UIRaw/Atlas/blocks/clip/）中对应 PNG 的文件名（去扩展名）。
        /// 经 SetSprite 按文件名直接定位 Sprite（与棋盘方块 BlockSkinCatalog 同寻址口径）。None 返回空串（无图）。
        /// </summary>
        public static string SpriteName(MergeElement e)
        {
            switch (e)
            {
                case MergeElement.Diamond: return "gem_drop_blue";
                case MergeElement.Pentagon: return "scroll_purple_gold";
                case MergeElement.Star: return "star_gold";
                case MergeElement.Heart: return "star_pink_gem";
                case MergeElement.Sun: return "lightning";
                case MergeElement.Moon: return "chalice_greek";
                case MergeElement.Leaf: return "stone_green";
                case MergeElement.Crown: return "book_magic";
                case MergeElement.Stone: return "stone_gray";
                default: return string.Empty;
            }
        }

        /// <summary>元素的展示纯色（glyph 渲染失败时仍可区分）。</summary>
        public static Color ColorOf(MergeElement e)
        {
            switch (e)
            {
                case MergeElement.Diamond: return new Color32(0x66, 0xe0, 0xff, 0xFF); // 青
                case MergeElement.Pentagon: return new Color32(0xa0, 0x88, 0xff, 0xFF); // 紫
                case MergeElement.Star: return new Color32(0xff, 0xe0, 0x55, 0xFF);    // 金
                case MergeElement.Heart: return new Color32(0xff, 0x66, 0x88, 0xFF);   // 粉
                case MergeElement.Sun: return new Color32(0xff, 0xaa, 0x33, 0xFF);     // 橙
                case MergeElement.Moon: return new Color32(0xcc, 0xdd, 0xff, 0xFF);    // 银蓝
                case MergeElement.Leaf: return new Color32(0x77, 0xdd, 0x77, 0xFF);    // 绿
                case MergeElement.Crown: return new Color32(0xff, 0xd7, 0x00, 0xFF);   // 皇金
                case MergeElement.Stone: return new Color32(0x99, 0x99, 0x99, 0xFF);   // 灰
                default: return Color.white;
            }
        }
    }
}
