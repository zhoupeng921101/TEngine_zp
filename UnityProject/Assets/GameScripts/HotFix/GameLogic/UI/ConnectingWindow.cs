using UnityEngine;
using UnityEngine.UI;
using TEngine;

namespace GameLogic.UI
{
    /// <summary>
    /// 入口连接闸窗（强制联网入口）。启动时立即打开，遮住背后，
    /// 在「登录成功 + 服务端数据就绪」之前阻断进入主菜单（由 <see cref="GameApp"/> 控制开/关）。
    /// 两态：
    ///   连接中 — 登录在途时显示「连接中…」，不放过任何点击；
    ///   重试   — 登录失败（连不上/超时/ErrorCode≠0）时显示原因 + 「重试」按钮 + 「服务器配置」入口，
    ///            登录成功前进不去主菜单。
    /// 自订阅 <see cref="FantasyClient.FantasyNetwork.OnLoginFailed"/> / <c>OnConnected</c> 切自身视觉态；
    /// 关窗与开主菜单的 gate 判定在 GameApp（本窗不感知快照态，只负责展示与重试触发）。
    /// 纯代码搭 UI（仿 <see cref="ServerConfigWindow"/>），prefab 为空壳根节点，无 m_* 绑定。
    /// </summary>
    [Window(UILayer.Top, location: "ConnectingWindow", fullScreen: true)]
    public sealed class ConnectingWindow : UIWindow
    {
        // 设计坐标系（1080×1920，左上原点、Y 下正），与 UGuiFactory 同口径。
        private const float Cx = 540f;

        private Text _textStatus;
        private RectTransform _retryGroup;   // 重试态控件组（连接中态隐藏）
        private Text _textReason;            // 失败原因文案

        private static readonly Color BgColor = new Color32(0x12, 0x16, 0x22, 0xFF);

        protected override void OnCreate()
        {
            var content = UGuiFactory.CreateContentPanel(rectTransform);

            // 全屏不透明底，遮住背后任何已存在的画面（杜绝主菜单/旧数据抢先闪现）。
            var bg = UGuiFactory.CreateImage(content, "Bg", Cx, 960, 1080, 1920, BgColor);
            bg.raycastTarget = true;

            UGuiFactory.CreateText(content, "Title", Cx, 760, 1008, 158, "BLOCK BLAST", 96,
                new Color32(0xff, 0xe0, 0x66, 0xFF));

            // 主状态行（连接中态显示「连接中…」，重试态显示「连接失败」）。
            _textStatus = UGuiFactory.CreateText(content, "Status", Cx, 960, 1008, 80, "连接中…", 48,
                new Color32(0x88, 0xaa, 0xcc, 0xFF));

            // ── 重试态控件组（默认隐藏）──
            _retryGroup = UGuiFactory.CreateNode(content, "RetryGroup");
            _retryGroup.anchorMin = Vector2.zero;
            _retryGroup.anchorMax = Vector2.one;
            _retryGroup.offsetMin = Vector2.zero;
            _retryGroup.offsetMax = Vector2.zero;

            _textReason = UGuiFactory.CreateText(_retryGroup, "Reason", Cx, 1080, 1008, 120, "", 36,
                new Color32(0xff, 0x88, 0x88, 0xFF));

            var btnRetry = UGuiFactory.CreateButton(_retryGroup, "BtnRetry", Cx, 1280, 680, 120,
                "重试", 52, new Color32(0x44, 0x77, 0xff, 0xFF), Color.white, out _, out _);
            btnRetry.onClick.AddListener(OnRetry);

            var btnConfig = UGuiFactory.CreateButton(_retryGroup, "BtnConfig", Cx, 1430, 680, 110,
                "服务器配置", 42, new Color32(0x55, 0x5b, 0x6b, 0xFF), Color.white, out _, out _);
            btnConfig.onClick.AddListener(OnOpenServerConfig);

            _retryGroup.gameObject.SetActive(false);

#if FANTASY_UNITY
            // 订阅绑定 OnCreate/OnDestroy（各一次），不绑 OnRefresh（同窗重复 ShowUI 会重入 → 重复订阅）。
            FantasyClient.FantasyNetwork.OnLoginFailed += OnLoginFailed;
            FantasyClient.FantasyNetwork.OnConnected += OnReconnected;
#endif
        }

        protected override void OnRefresh()
        {
            // 开窗/刷新时按默认进入「连接中」态（幂等）。失败态由 OnLoginFailed 事件切换。
            SetConnecting();
        }

        protected override void OnDestroy()
        {
#if FANTASY_UNITY
            FantasyClient.FantasyNetwork.OnLoginFailed -= OnLoginFailed;
            FantasyClient.FantasyNetwork.OnConnected -= OnReconnected;
#endif
        }

        /// <summary>切到「连接中」态：隐藏重试组，状态文案转为等待。</summary>
        private void SetConnecting()
        {
            _textStatus.text = "连接中…";
            _textStatus.color = new Color32(0x88, 0xaa, 0xcc, 0xFF);
            if (_retryGroup != null) _retryGroup.gameObject.SetActive(false);
        }

        /// <summary>切到「重试」态：显示原因 + 重试/配置入口。</summary>
        private void SetRetry(string reason)
        {
            _textStatus.text = "连接失败";
            _textStatus.color = new Color32(0xff, 0x88, 0x88, 0xFF);
            if (_textReason != null) _textReason.text = reason ?? string.Empty;
            if (_retryGroup != null) _retryGroup.gameObject.SetActive(true);
        }

        // 登录失败（连不上/超时/ErrorCode≠0）→ 切重试态。事件在网络主线程触发，UI 主线程内安全刷。
        private void OnLoginFailed(string reason) => SetRetry(reason);

        // 连接（重新）建立 → 切回连接中态（自动重连成功后会自动重登，等待结果）。
        private void OnReconnected() => SetConnecting();

        private void OnRetry()
        {
#if FANTASY_UNITY
            SetConnecting();
            FantasyClient.FantasyNetwork.RetryLogin();
#endif
        }

        private void OnOpenServerConfig()
        {
            GameModule.UI.ShowUIAsync<ServerConfigWindow>();
        }
    }
}
