using UnityEngine;
using UnityEngine.UI;
using TEngine;

namespace GameLogic
{
    /// <summary>
    /// 棋盘元素图标 Widget（MergeOrderWindow 棋盘每个含元素的格一个实例，叠在格底块之上，由窗口池化复用）。
    /// 棋盘元素恒为 Lv1 原料（无等级层），图标 sprite 由窗口按 MergeElementVisual.SpriteName(el,1) 解析后传入。
    /// 同一 widget 也用作合成/交付飞行图标的视觉本体（FlyToTargetFx 创建并驱动运动）。
    /// 绑定字段 m_eximg_Icon 由 ElementWidget_Gen.g.cs 声明、prefab 序列化引用，本文件不重复定义。
    /// 图标子节点拉伸填满 widget 根（anchor 0–1），故图标尺寸 = 根尺寸（由各调用方设根 sizeDelta 控制）。
    /// </summary>
    public partial class ElementWidget
    {
        /// <summary>
        /// 设置元素图标：按名从图集换贴图。图标子节点拉伸填满根，尺寸随根（不走 setNativeSize）。
        /// </summary>
        /// <param name="spriteLocation">元素图标 sprite 定位名（散 PNG 按文件名 location）。</param>
        public void SetIcon(string spriteLocation)
        {
            if (m_eximg_Icon != null) m_eximg_Icon.SpriteName = spriteLocation;
        }

        /// <summary>
        /// 设置图标渲染显隐（仅开关图标组件，不动 GameObject 激活态）：
        /// 供飞行图标 stagger 延迟期隐藏视觉而保持根节点 active，使运动驱动组件的 Update 仍计时。
        /// </summary>
        public void SetIconVisible(bool visible)
        {
            if (m_eximg_Icon != null) m_eximg_Icon.enabled = visible;
        }
    }
}
