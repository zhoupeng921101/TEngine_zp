using UnityEngine;
using UnityEngine.UI;
using TEngine;

namespace GameLogic
{
    /// <summary>
    /// 棋盘元素图标 Widget（MergeOrderWindow 棋盘每个含元素的格一个实例，叠在格底块之上，由窗口池化复用）。
    /// 棋盘元素恒为 Lv1 原料（无等级层），图标 sprite 由窗口按 MergeElementVisual.SpriteName(el,1) 解析后传入。
    /// 绑定字段 m_img_Icon 由 ElementWidget_Gen.g.cs 的 ScriptGenerator() 负责，本文件不重复定义。
    /// </summary>
    public partial class ElementWidget
    {
        /// <summary>
        /// 设置元素图标：贴图并按 sprite 原生尺寸自适应（setNativeSize，与原棋盘元素图标口径一致）。
        /// </summary>
        /// <param name="spriteLocation">元素图标 sprite 定位名（散 PNG 按文件名 location）。</param>
        public void SetIcon(string spriteLocation)
        {
            if (m_eximg_Icon != null) m_eximg_Icon.SpriteName = spriteLocation;
        }
    }
}
