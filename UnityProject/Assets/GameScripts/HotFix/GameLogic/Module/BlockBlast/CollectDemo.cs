using UnityEngine;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 9 种收集元素。枚举值 = 原版配置 Key（省一层映射）。
    /// None=0 表示该格无收集元素。
    /// 源：参考工程 GameState.ts 的 ElementType + COLLECT_KEY_MAP。
    /// </summary>
    public enum CollectElement
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

    /// <summary>收集元素的表现（glyph / 纯色，零美术），供 merge-order 模式渲染。</summary>
    public static class CollectDemo
    {
        /// <summary>元素的展示符号（glyph，零美术）。</summary>
        public static string Glyph(CollectElement e)
        {
            switch (e)
            {
                case CollectElement.Diamond: return "◆";
                case CollectElement.Pentagon: return "⬟";
                case CollectElement.Star: return "★";
                case CollectElement.Heart: return "♥";
                case CollectElement.Sun: return "☀";
                case CollectElement.Moon: return "☾";
                case CollectElement.Leaf: return "✿";
                case CollectElement.Crown: return "♕";
                case CollectElement.Stone: return "⬢";
                default: return string.Empty;
            }
        }

        /// <summary>元素的展示纯色（glyph 渲染失败时仍可区分）。</summary>
        public static Color ColorOf(CollectElement e)
        {
            switch (e)
            {
                case CollectElement.Diamond: return new Color32(0x66, 0xe0, 0xff, 0xFF); // 青
                case CollectElement.Pentagon: return new Color32(0xa0, 0x88, 0xff, 0xFF); // 紫
                case CollectElement.Star: return new Color32(0xff, 0xe0, 0x55, 0xFF);    // 金
                case CollectElement.Heart: return new Color32(0xff, 0x66, 0x88, 0xFF);   // 粉
                case CollectElement.Sun: return new Color32(0xff, 0xaa, 0x33, 0xFF);     // 橙
                case CollectElement.Moon: return new Color32(0xcc, 0xdd, 0xff, 0xFF);    // 银蓝
                case CollectElement.Leaf: return new Color32(0x77, 0xdd, 0x77, 0xFF);    // 绿
                case CollectElement.Crown: return new Color32(0xff, 0xd7, 0x00, 0xFF);   // 皇金
                case CollectElement.Stone: return new Color32(0x99, 0x99, 0x99, 0xFF);   // 灰
                default: return Color.white;
            }
        }
    }
}
