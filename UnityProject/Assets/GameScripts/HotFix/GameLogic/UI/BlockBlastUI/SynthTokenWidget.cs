using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.BlockBlast;

namespace GameLogic
{
    /// <summary>
    /// 合成 Token Widget（MergeOrderWindow 合成区可变数量池，由 AdjustIconNum 管理实例数）。
    /// 每个实例对应一种合成元素（类型×等级），显示图标和数量文字。
    /// 绑定字段由 SynthTokenWidget_Gen.g.cs 的 ScriptGenerator() 负责，本文件不重复定义。
    /// </summary>
    public partial class SynthTokenWidget : UIWidget
    {
        /// <summary>本 token 当前承载的元素类型（供收集飞行动画按类型匹配落点；None 表示未设置）。</summary>
        public MergeElement ElementType { get; private set; } = MergeElement.None;

        /// <summary>
        /// 刷新 token 显示数据。
        /// </summary>
        /// <param name="type">元素类型（飞行落点按类型匹配）。</param>
        /// <param name="glyphSpriteName">元素图标 sprite 名称。</param>
        /// <param name="level">元素等级。</param>
        /// <param name="count">数量。</param>
        public void SetData(MergeElement type, string glyphSpriteName, int level, int count)
        {
            ElementType = type;
            if (!string.IsNullOrEmpty(glyphSpriteName)) m_img_Glyph.SetSprite(glyphSpriteName);
            // 仅显示数量；等级已由图标分级（{type}_{level}）表现，文字不再重复等级。
            m_text_Info.text = $"×{count}";
        }

        /// <summary>
        /// 图标落点 RectTransform（合成区元素图标），供收集飞行动画取终点世界坐标。
        /// 只读暴露既有绑定节点，不改 prefab 结构。
        /// </summary>
        public RectTransform GlyphRect => m_img_Glyph != null ? m_img_Glyph.rectTransform : null;

        /// <summary>对图标做一次轻微放大反馈（收集飞行图标到达时调用）。ScalePunch 仅销毁组件、不动 GameObject，可重复调用。</summary>
        public void PunchGlyph()
        {
            if (m_img_Glyph != null) m_img_Glyph.gameObject.AddComponent<ScalePunch>();
        }
    }
}
