using Cysharp.Threading.Tasks;
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
    ///
    /// 字体时序（WebGL 关键）：本窗在 <see cref="GameApp"/> 同步启动路径上立即摆出（先于字体异步预载完成），
    /// 而 <see cref="UGuiFactory"/> 文本创建首次取字体会把进程级静态字体缓存永久钉死。若在字体预载前建文本，
    /// WebGL 下同步取 GBK 失败 → 回退内置字体被钉死 → 全局中文无字形。故 OnCreate 分两段：
    ///   ① 同步建遮屏背景（不依赖字体），保证摆上即盖屏；
    ///   ② 文本/按钮（依赖字体）延后到 <see cref="UIPreloader.PreloadFontsAsync"/> await 完成后再建。
    /// 字体预载是本地 bundle 加载（无网络）、幂等且远早于登录完成，不与登录-CloseUI 竞争。
    /// 窗的「显示」仍走同步路径不动（推迟 show 会破坏登录回调 CloseUI 命中、致闸窗永久盖死）。
    /// </summary>
    [Window(UILayer.Top, location: "ConnectingWindow", fullScreen: true)]
    public sealed class ConnectingWindow : UIWindowMono
    {
        // 设计坐标系（1080×1920，左上原点、Y 下正），与 UGuiFactory 同口径。
        private const float Cx = 540f;

        /// <summary>期望视觉态。文本未建好（字体预载未完成）时先记录，建好后再应用。</summary>
        private enum VisualState { Connecting, Retry }

        private RectTransform _content;
        private RectTransform _retryGroup;   // 重试态控件组（连接中态隐藏；节点同步建，文本/按钮延后）

        private Text _textStatus;
        private Text _textReason;            // 失败原因文案

        /// <summary>依赖字体的文本/按钮是否已建好（字体预载完成后置位）。</summary>
        private bool _textsReady;
        /// <summary>当前期望视觉态（文本未就绪时缓存，就绪后应用）。</summary>
        private VisualState _pendingState = VisualState.Connecting;
        /// <summary>重试态待显示的失败原因（文本未就绪时缓存）。</summary>
        private string _pendingReason = string.Empty;

        private static readonly Color BgColor = new Color32(0x12, 0x16, 0x22, 0xFF);
        private static readonly Color StatusConnectingColor = new Color32(0x88, 0xaa, 0xcc, 0xFF);
        private static readonly Color StatusFailedColor = new Color32(0xff, 0x88, 0x88, 0xFF);

        protected override void OnCreate()
        {
            _content = UGuiFactory.CreateContentPanel(rectTransform);

            // ① 立即同步建遮屏背景（不依赖字体），保证闸窗一摆上就盖住背后任何已存在的画面
            //    （杜绝主菜单/旧数据抢先闪现）。
            var bg = UGuiFactory.CreateImage(_content, "Bg", Cx, 960, 1080, 1920, BgColor);
            bg.raycastTarget = true;

            // 重试态容器节点同步建（空壳、默认隐藏；CreateNode 不依赖字体）。其内的原因文本与按钮在字体就绪后填。
            _retryGroup = UGuiFactory.CreateNode(_content, "RetryGroup");
            _retryGroup.anchorMin = Vector2.zero;
            _retryGroup.anchorMax = Vector2.one;
            _retryGroup.offsetMin = Vector2.zero;
            _retryGroup.offsetMax = Vector2.zero;
            _retryGroup.gameObject.SetActive(false);

#if FANTASY_UNITY
            // 订阅绑定 OnCreate/OnDestroyWindow（各一次），不绑 OnRefresh（同窗重复 ShowUI 会重入 → 重复订阅）。
            // 订阅在文本建好前即生效：登录失败回调若早于字体就绪到达，仅缓存期望态（不触文本），文本建好时再应用。
            FantasyClient.FantasyNetwork.OnLoginFailed += OnLoginFailed;
            FantasyClient.FantasyNetwork.OnConnected += OnReconnected;
#endif

            // ② 文本/按钮延后到字体预载完成后再建（防 WebGL 同步取字体失败钉死回退字体）。
            BuildTextsAfterFontReady().Forget();
        }

        /// <summary>
        /// await 字体预载完成后建依赖字体的文本/按钮，再应用期望视觉态。
        /// race 守卫：await 期间窗可能被关闭/销毁（登录成功 → GameApp.CloseUI&lt;ConnectingWindow&gt;）。
        /// await 返回后、触碰任何 GameObject/组件前，先判窗是否已销毁（<see cref="UIWindowMono.IsDestroyed"/>
        /// + Unity MonoBehaviour 失效判定 this == null），已销毁直接 return，不建节点、不写字段。
        /// </summary>
        private async UniTaskVoid BuildTextsAfterFontReady()
        {
            // 本地 bundle 加载、幂等（已驻留快速返回）。失败逐项记 Error 不抛（见 UIPreloader）。
            await GameLogic.UIPreloader.PreloadFontsAsync();

            // race 守卫：await 期间窗已销毁则不再触碰任何对象。
            // IsDestroyed 守 OnDestroyWindow 已先行；this == null 守 Unity 物体已被 Destroy。
            if (this == null || IsDestroyed)
            {
                return;
            }

            BuildTexts();
            _textsReady = true;

            // 应用 await 期间累积的期望态（OnRefresh/OnLoginFailed/OnReconnected 在文本就绪前只更新缓存态）。
            ApplyState();
        }

        /// <summary>建依赖字体的文本与按钮（字体预载完成后调用一次）。</summary>
        private void BuildTexts()
        {
            UGuiFactory.CreateText(_content, "Title", Cx, 760, 1008, 158, "BLOCK BLAST", 96,
                new Color32(0xff, 0xe0, 0x66, 0xFF));

            // 主状态行（连接中态显示「连接中…」，重试态显示「连接失败」）。
            _textStatus = UGuiFactory.CreateText(_content, "Status", Cx, 960, 1008, 80, "连接中…", 48,
                StatusConnectingColor);

            // ── 重试态控件（原因文案 + 重试/配置入口），挂已同步建好的 _retryGroup 节点下 ──
            _textReason = UGuiFactory.CreateText(_retryGroup, "Reason", Cx, 1080, 1008, 120, "", 36,
                new Color32(0xff, 0x88, 0x88, 0xFF));

            var btnRetry = UGuiFactory.CreateButton(_retryGroup, "BtnRetry", Cx, 1280, 680, 120,
                "重试", 52, new Color32(0x44, 0x77, 0xff, 0xFF), Color.white, out _, out _);
            btnRetry.onClick.AddListener(OnRetry);

            var btnConfig = UGuiFactory.CreateButton(_retryGroup, "BtnConfig", Cx, 1430, 680, 110,
                "服务器配置", 42, new Color32(0x55, 0x5b, 0x6b, 0xFF), Color.white, out _, out _);
            btnConfig.onClick.AddListener(OnOpenServerConfig);
        }

        protected override void OnRefresh()
        {
            // 开窗/刷新时按默认进入「连接中」态（幂等）。失败态由 OnLoginFailed 事件切换。
            SetConnecting();
        }

        protected override void OnDestroyWindow()
        {
#if FANTASY_UNITY
            FantasyClient.FantasyNetwork.OnLoginFailed -= OnLoginFailed;
            FantasyClient.FantasyNetwork.OnConnected -= OnReconnected;
#endif
        }

        /// <summary>切到「连接中」态：记录期望态；文本就绪则即时应用，未就绪则待建好后应用。</summary>
        private void SetConnecting()
        {
            _pendingState = VisualState.Connecting;
            if (_textsReady)
            {
                ApplyState();
            }
        }

        /// <summary>切到「重试」态：记录期望态 + 原因；文本就绪则即时应用，未就绪则待建好后应用。</summary>
        private void SetRetry(string reason)
        {
            _pendingState = VisualState.Retry;
            _pendingReason = reason ?? string.Empty;
            if (_textsReady)
            {
                ApplyState();
            }
        }

        /// <summary>把当前期望视觉态落到已建好的文本/按钮上。仅在文本就绪后调用。</summary>
        private void ApplyState()
        {
            if (_pendingState == VisualState.Retry)
            {
                _textStatus.text = "连接失败";
                _textStatus.color = StatusFailedColor;
                _textReason.text = _pendingReason;
                _retryGroup.gameObject.SetActive(true);
            }
            else
            {
                _textStatus.text = "连接中…";
                _textStatus.color = StatusConnectingColor;
                _retryGroup.gameObject.SetActive(false);
            }
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
