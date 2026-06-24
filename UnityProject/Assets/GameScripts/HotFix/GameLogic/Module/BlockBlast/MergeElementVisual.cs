using UnityEngine;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 4 种合成元素 + None。每种元素按等级有独立美术（图标寻址 {type}_{level}）。
    /// None=0 表示该格无元素。枚举值连续（1..4），改值会与旧存档的 inventory/ElementArr 失配，需清档重进。
    /// </summary>
    public enum MergeElement
    {
        None = 0,
        Butterfly = 1,
        Chalice = 2,
        Scroll = 3,
        Star = 4,
    }

    /// <summary>合成元素的表现（图标寻址 / glyph / 纯色），供 merge-order 模式渲染。</summary>
    public static class MergeElementVisual
    {
        /// <summary>类型的图标文件名前缀（小写，与 clip 图标库文件名 {type}_{level} 一致）。None 返回空串。</summary>
        public static string TypeName(MergeElement e)
        {
            switch (e)
            {
                case MergeElement.Butterfly: return "butterfly";
                case MergeElement.Chalice: return "chalice";
                case MergeElement.Scroll: return "scroll";
                case MergeElement.Star: return "star";
                default: return string.Empty;
            }
        }

        /// <summary>元素的展示符号（glyph，渲染兜底）。</summary>
        public static string Glyph(MergeElement e)
        {
            switch (e)
            {
                case MergeElement.Butterfly: return "✦";
                case MergeElement.Chalice: return "♆";
                case MergeElement.Scroll: return "✉";
                case MergeElement.Star: return "★";
                default: return string.Empty;
            }
        }

        /// <summary>
        /// 元素图标的寻址名：clip 图标库（Assets/AssetRaw/UI/Atlas/blocks/clip/）中对应 PNG 的文件名（去扩展名），
        /// 按「类型 + 等级」取图（每级独立美术，文件名形如 butterfly_3）。经 SetSprite 按文件名直接定位 Sprite。
        /// <paramref name="level"/> 夹到 [1, MergeOrderConfig.MaxLevel] 防越界。None 返回空串（无图）。
        /// </summary>
        public static string SpriteName(MergeElement type, int level)
        {
            string name = TypeName(type);
            if (string.IsNullOrEmpty(name)) return string.Empty;
            if (level < 1) level = 1;
            else if (level > MergeOrderConfig.MaxLevel) level = MergeOrderConfig.MaxLevel;
            return $"{name}_{level}";
        }

        /// <summary>元素的展示纯色（glyph 渲染失败时仍可区分）。</summary>
        public static Color ColorOf(MergeElement e)
        {
            switch (e)
            {
                case MergeElement.Butterfly: return new Color32(0xa0, 0x88, 0xff, 0xFF); // 紫
                case MergeElement.Chalice: return new Color32(0x66, 0xe0, 0xff, 0xFF);   // 青
                case MergeElement.Scroll: return new Color32(0xff, 0xc8, 0x66, 0xFF);    // 卷轴金棕
                case MergeElement.Star: return new Color32(0xff, 0xe0, 0x55, 0xFF);      // 金
                default: return Color.white;
            }
        }
    }
}
