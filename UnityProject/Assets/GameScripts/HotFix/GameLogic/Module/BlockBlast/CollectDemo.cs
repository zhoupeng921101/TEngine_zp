using System;
using System.Collections.Generic;
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

    /// <summary>收集目标项：元素类型 + 需要数量。对应原版 RequiredCollection{Key,Value}。</summary>
    public readonly struct CollectTarget
    {
        public readonly CollectElement Element;
        public readonly int Count;
        public CollectTarget(CollectElement element, int count)
        {
            Element = element;
            Count = count;
        }
    }

    /// <summary>
    /// 收集 Demo 切片的静态配置 + Key 映射 + 表现（glyph / 纯色）。
    /// 切片不接 Luban 表，目标硬编码在此，改数即可调难度。
    /// </summary>
    public static class CollectDemo
    {
        /// <summary>候选块每个填充格注入元素的概率（复刻原版 ~22%）。可调常量。</summary>
        public const double InjectChance = 0.22;

        /// <summary>分数目标 Key（与收集并存的冲分条件）。收集解析时跳过（切片不做分数目标）。</summary>
        public const int ScoreKey = 9999;

        /// <summary>
        /// 静态 demo 收集目标（已去掉 Key 9999）。改这里即可调难度。
        /// 数值刻意调小，降低纯靠候选块携带「看脸」卡死的概率（见设计文档 §7 风险）。
        /// </summary>
        public static readonly CollectTarget[] DemoTargets =
        {
            new CollectTarget(CollectElement.Diamond, 6),
            new CollectTarget(CollectElement.Star, 5),
        };

        /// <summary>
        /// Key → 元素映射。9999（分数）与未知 Key → None（解析时跳过）。
        /// 枚举值即 Key，故已定义的 Key 直接转换。
        /// </summary>
        public static CollectElement FromKey(int key)
        {
            if (key == ScoreKey) return CollectElement.None;
            if (Enum.IsDefined(typeof(CollectElement), key)) return (CollectElement)key;
            return CollectElement.None;
        }

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
