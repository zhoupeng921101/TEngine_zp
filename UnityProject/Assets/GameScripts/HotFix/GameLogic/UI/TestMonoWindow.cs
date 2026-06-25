using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// MonoBehaviour 窗口验证样例：脚本作为组件挂在 prefab 根上，引用 / 数值用 [SerializeField] 暴露到 Inspector。
    /// 验证并行路径跑通（挂 prefab、Inspector 填字段、ShowUI 显示、生命周期、加载去重），非正式功能窗口。
    /// </summary>
    [Window(UILayer.UI, location: "TestMonoWindow")]
    public sealed class TestMonoWindow : UIWindowMono
    {
        [SerializeField] private Button m_btn_Close;
        [SerializeField] private Text m_text_Hello;
        [SerializeField] private int testValue = 42;

        protected override void ScriptGenerator()
        {
            // 引用已由序列化就位；此处仅挂事件（与经典 UIWindow.ScriptGenerator 的 onClick 挂法一致）。
            if (m_btn_Close != null)
            {
                m_btn_Close.onClick.AddListener(Close);
            }
        }

        protected override void OnRefresh()
        {
            // 用 Inspector 填入的 testValue 写文本，验证序列化数值生效。
            if (m_text_Hello != null)
            {
                m_text_Hello.text = $"Hello {testValue}";
            }
        }
    }
}
