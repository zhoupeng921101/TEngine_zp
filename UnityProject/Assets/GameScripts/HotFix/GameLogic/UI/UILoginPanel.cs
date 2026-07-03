using TEngine;

namespace GameLogic
{
    /// <summary>
    /// 登录窗(强制联网入口的账号输入步)。输入账号 → 点「登录」→ 发起登录,成功由 <see cref="GameApp"/> 入口闸放行进游戏。
    /// 本地无已存账号时启动即开本窗;自动登录失败时由 GameApp 回退到本窗并回带失败原因(经 <see cref="UIBaseMono.UserData"/> 传入)。
    /// UI 由 UILoginPanel.prefab 搭建、控件经 [SerializeField] 绑定;文本字体作为 prefab 序列化依赖随窗异步加载,
    /// 故无需运行时字体预载时序处理。
    /// </summary>
    [Window(UILayer.Top, location: "UILoginPanel", fullScreen: true)]
    public sealed partial class UILoginPanel : UIPanelMono
    {
        protected override void OnRefresh()
        {
            // 失败原因经 ShowUIAsync<UILoginPanel>(reason) 传入 UserData;并预填上次登录账号便于重试。
            m_text_Error.text = UserData as string ?? string.Empty;
            m_input_Account.text = LoginAccountStore.Get();
        }

        private partial void OnClick_LoginBtn()
        {
            string account = (m_input_Account.text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(account))
            {
                m_text_Error.text = "请输入账号";
                return;
            }
#if FANTASY_UNITY
            m_text_Error.text = string.Empty;
            // 关本窗、由 GameApp 驱动 teardown+reboot 登录并摆连接闸窗;成功放行进游戏,失败回退本窗并回带原因。
            GameModule.UI.CloseUI<UILoginPanel>();
            GameApp.BeginLogin(account);
#else
            m_text_Error.text = "网络模块未启用(FANTASY_UNITY 未定义)";
#endif
        }
    }
}
