using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TEngine;

namespace GameLogic
{
    /// <summary>
    /// 通用倒计时显示组件。仅负责显示剩余时间文本，业务数据由父窗口每秒喂入。
    /// 绑定字段由 CountdownWidget_Gen.g.cs 的 ScriptGenerator() 负责。
    /// </summary>
    public partial class CountdownWidget : UIWidget
    {
        /// <summary>
        /// 设置剩余秒数，刷新时间显示文本。自适应格式：
        /// &lt;60 秒显秒数（如 "8s"）；≥60 秒显 "mm:ss"（如 "04:32"）。seconds&lt;=0 夹 0 显 "0s"（不显负数）。
        /// 组件业务无关：只接收剩余秒数，不读体力/订单数据、不自计时（计时与喂数由宿主窗口负责）。
        /// </summary>
        /// <param name="seconds">剩余秒数（&lt;=0 按 0 处理）。</param>
        public void SetRemainingSeconds(int seconds)
        {
            if (m_text_Time == null) return;
            if (seconds < 0) seconds = 0;

            if (seconds < 60)
            {
                m_text_Time.text = $"{seconds}s";
            }
            else
            {
                int mm = seconds / 60;
                int ss = seconds % 60;
                m_text_Time.text = $"{mm:00}:{ss:00}";
            }
        }

        /// <summary>
        /// 控制整个倒计时组件根的显隐。
        /// </summary>
        /// <param name="visible">true 显示，false 隐藏。</param>
        public void SetVisible(bool visible)
        {
            if (gameObject != null) gameObject.SetActive(visible);
        }

        #region 事件

        #endregion
    }
}
