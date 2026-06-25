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
    public partial class SynthTokenWidget : UIWidgetMono
    {
        /// <summary>本 token 当前承载的元素类型（供收集飞行动画按类型匹配落点；None 表示未设置）。</summary>
        public MergeElement ElementType { get; private set; } = MergeElement.None;

        /// <summary>本 token 当前显示的数量（= 库存真实值，SetData 写入）。收集飞行用它算「飞行前数量 = 真实值 - 本次飞入增量」。</summary>
        public int DisplayCount { get; private set; }

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
            DisplayCount = count;
            if (!string.IsNullOrEmpty(glyphSpriteName)) m_img_Glyph.SetSprite(glyphSpriteName, setNativeSize:true);

            // 仅显示数量；等级已由图标分级（{type}_{level}）表现，文字不再重复等级。
            m_text_Info.text = count.ToString();
            // 复用 token 实例时重置为可见，避免上一轮收集飞行的隐藏态残留（池化实例可能上次被隐藏未及恢复）。
            SetContentVisible(true);
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

        /// <summary>
        /// 收集飞行期间临时覆盖显示的数量文字（不改 <see cref="DisplayCount"/> = 库存真实值）。
        /// 用于「飞行前先显示飞入前数量、每个图标飞达再 +1」的渐显：
        /// shownCount &lt;= 0 时隐藏全部内容（飞行前该 token 无存量、纯本次飞入产生，飞达前不显示）；
        /// 否则显示 shownCount 并保持内容可见（保留飞行前已有存量的展示，不被本次飞行隐藏）。
        /// </summary>
        public void SetFlyingShownCount(int shownCount)
        {
            if (shownCount <= 0)
            {
                SetContentVisible(false);
                return;
            }
            if (m_text_Info != null) m_text_Info.text = shownCount.ToString();
            SetContentVisible(true);
        }

        /// <summary>飞行结束后恢复显示库存真实值（<see cref="DisplayCount"/>）并确保可见。</summary>
        public void RestoreDisplayCount()
        {
            if (m_text_Info != null) m_text_Info.text = DisplayCount.ToString();
            SetContentVisible(true);
        }

        /// <summary>
        /// 切换可见内容（图标 + 数量文字）的显隐，仅改 Image/Text 的 enabled、保留 RectTransform 布局占位。
        /// 不 SetActive 整个 GameObject——那会令 HorizontalLayoutGroup 重排、其它 token 位移、飞行落点失准。
        /// 收集飞行用：飞行前隐藏落点 token，待飞向它的全部图标到达后再显示，呈现「元素汇入后该格才点亮」。
        /// </summary>
        public void SetContentVisible(bool visible)
        {
            if (m_img_Glyph != null) m_img_Glyph.enabled = visible;
            if (m_text_Info != null) m_text_Info.enabled = visible;
        }
    }
}
