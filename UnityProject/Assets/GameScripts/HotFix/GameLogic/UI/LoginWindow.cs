using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TEngine;

namespace GameLogic.UI
{
    /// <summary>
    /// 登录窗(强制联网入口的账号输入步)。进入游戏前展示:输入账号 → 点「登录」→ 发起登录,
    /// 成功由 <see cref="GameApp"/> 入口闸放行进游戏。本地无已存账号时启动即开本窗;
    /// 自动登录失败时由 GameApp 回退到本窗并回带失败原因(经 <see cref="UIBaseMono.UserData"/> 传入)。
    /// 纯代码搭 UI(仿 <see cref="ConnectingWindow"/>),prefab 为空壳根节点,无 m_* 绑定。
    ///
    /// 字体时序(WebGL 关键,同 ConnectingWindow):本窗在启动同步路径上摆出、先于字体异步预载完成。
    /// UGuiFactory 文本首次取字体会把进程级字体缓存钉死,若在预载前建文本则 WebGL 下同步取 GBK 失败、
    /// 回退内置字体被钉死 → 全局中文无字形。故 OnCreate 分两段:① 同步建遮屏背景(不依赖字体);
    /// ② 依赖字体的标题/输入框/按钮延后到 <see cref="UIPreloader.PreloadFontsAsync"/> 完成后再建。
    /// </summary>
    [Window(UILayer.Top, location: "LoginWindow", fullScreen: true)]
    public sealed class LoginWindow : UIWindowMono
    {
        // 设计坐标系(1080×1920,左上原点、Y 下正),与 UGuiFactory 同口径。
        private const float Cx = 540f;

        private RectTransform _content;
        private InputField _inputAccount;
        private Text _textError;

        /// <summary>依赖字体的控件是否已建好(字体预载完成后置位)。</summary>
        private bool _textsReady;
        /// <summary>待显示的失败原因(文本未就绪时缓存,建好后应用)。OnRefresh 从 UserData 取。</summary>
        private string _pendingReason = string.Empty;

        private static readonly Color BgColor = new Color32(0x12, 0x16, 0x22, 0xFF);
        private static readonly Color FieldColor = new Color32(0x3a, 0x40, 0x52, 0xFF);
        private static readonly Color ErrorColor = new Color32(0xff, 0x88, 0x88, 0xFF);

        protected override void OnCreate()
        {
            _content = UGuiFactory.CreateContentPanel(rectTransform);

            // ① 立即同步建遮屏背景(不依赖字体),摆上即盖住背后任何画面。
            var bg = UGuiFactory.CreateImage(_content, "Bg", Cx, 960, 1080, 1920, BgColor);
            bg.raycastTarget = true;

            // ② 依赖字体的控件延后到字体预载完成后建。
            BuildAfterFontReady().Forget();
        }

        /// <summary>
        /// await 字体预载完成后建依赖字体的标题/输入框/按钮,再应用累积的失败原因 + 预填上次账号。
        /// race 守卫:await 期间窗可能被关闭/销毁(点登录 → CloseUI&lt;LoginWindow&gt;),
        /// 返回后触碰任何对象前先判窗已否销毁。
        /// </summary>
        private async UniTaskVoid BuildAfterFontReady()
        {
            await GameLogic.UIPreloader.PreloadFontsAsync();
            if (this == null || IsDestroyed)
            {
                return;
            }

            BuildControls();
            _textsReady = true;

            // 应用 await 期间累积的失败原因(OnRefresh 早于文本就绪时只缓存),并预填上次登录账号便于重试。
            _textError.text = _pendingReason;
            _inputAccount.text = LoginAccountStore.Get();
        }

        private void BuildControls()
        {
            UGuiFactory.CreateText(_content, "Title", Cx, 620, 1008, 158, "BLOCK BLAST", 96,
                new Color32(0xff, 0xe0, 0x66, 0xFF));
            UGuiFactory.CreateText(_content, "Subtitle", Cx, 780, 1008, 70, "输入账号登录", 44,
                new Color32(0x88, 0xaa, 0xcc, 0xFF));

            _inputAccount = UGuiFactory.CreateInputField(_content, "AccountInput", Cx, 960, 760, 110,
                "请输入账号", FieldColor, 40);

            _textError = UGuiFactory.CreateText(_content, "Error", Cx, 1080, 1008, 70, string.Empty, 36,
                ErrorColor);

            var btnLogin = UGuiFactory.CreateButton(_content, "BtnLogin", Cx, 1240, 680, 120,
                "登录", 52, new Color32(0x44, 0x77, 0xff, 0xFF), Color.white, out _, out _);
            btnLogin.onClick.AddListener(OnLogin);
        }

        protected override void OnRefresh()
        {
            // 失败原因经 ShowUIAsync<LoginWindow>(reason) 传入,取 UserData。文本未就绪则缓存,就绪后由 BuildAfterFontReady 应用。
            _pendingReason = UserData as string ?? string.Empty;
            if (_textsReady)
            {
                _textError.text = _pendingReason;
                _inputAccount.text = LoginAccountStore.Get();
            }
        }

        private void OnLogin()
        {
            string account = (_inputAccount.text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(account))
            {
                _textError.text = "请输入账号";
                return;
            }
#if FANTASY_UNITY
            _textError.text = string.Empty;
            // 关本窗、由 GameApp 驱动 teardown+reboot 登录并摆连接闸窗;成功放行进游戏,失败回退本窗并回带原因。
            GameModule.UI.CloseUI<LoginWindow>();
            GameApp.BeginLogin(account);
#else
            _textError.text = "网络模块未启用(FANTASY_UNITY 未定义)";
#endif
        }
    }
}
