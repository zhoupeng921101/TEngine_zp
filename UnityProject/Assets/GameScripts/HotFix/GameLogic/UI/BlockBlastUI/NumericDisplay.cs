using UnityEngine;
using GameLogic.Config;
using GameLogic.BlockBlast.Numeric;

namespace GameLogic.BlockBlastUI
{
    /// <summary>
    /// 可复用数值显示 helper（设计 15 §3.5）。站在注册表 + 格式化之上：
    /// 给 num_id + 数量，产出显示文本 / 图标资源名 / 品质色。
    /// 本轮交付到「文本 + 图标资源名」，真实 Sprite 加载列后续（O4）。
    /// </summary>
    public static class NumericDisplay
    {
        /// <summary>核心:数量 → 缩写文本（最常用，纯逻辑，可单测）。</summary>
        public static string Format(long amount)
        {
            return NumericFormat.Abbreviate(amount);
        }

        /// <summary>
        /// 带货币语义:num_id + 数量 → "标签 999.9K"。
        /// 标签暂用 name 文本 id 兜底（func_name 是服务器字段不导出客户端；文本表本轮未接）。
        /// </summary>
        public static string FormatWith(int numId, long amount)
        {
            var e = NumericConfigMgr.Get(numId);
            string label = e != null ? e.NameTextId.ToString() : numId.ToString();
            return label + " " + NumericFormat.Abbreviate(amount);
        }

        /// <summary>取图标资源名（真实 Sprite 加载交调用方 / 后续轮，helper 只给名字）。</summary>
        public static string IconName(int numId)
        {
            var e = NumericConfigMgr.Get(numId);
            return e != null ? e.IconName : null;
        }

        /// <summary>
        /// 取品质色（quality → Color），helper 给映射，调用方上色。
        /// 白(1)/蓝(2)/紫(3)/红(4)，越界返白。
        /// </summary>
        public static Color QualityColor(int quality)
        {
            switch (quality)
            {
                case 2: return new Color(0.36f, 0.55f, 1f);   // 蓝
                case 3: return new Color(0.68f, 0.42f, 0.96f); // 紫
                case 4: return new Color(1f, 0.36f, 0.36f);    // 红
                default: return Color.white;                   // 白(1) / 越界
            }
        }
    }
}
