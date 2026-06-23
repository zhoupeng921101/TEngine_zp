using UnityEngine;
using UnityEngine.UI;
using TEngine;

namespace GameLogic.UI
{
    /// <summary>
    /// 服务器地址配置窗（运行时可改 IP / 端口 / 协议，PlayerPrefs 持久化）。
    /// 局域网联调用：输入服务器主机的局域网 IP（形如 192.168.x.x，非 127.0.0.1/localhost）+ 端口 + 协议，
    /// 「连接并保存」落盘后断开重连，连接前由 <see cref="FantasyClient.FantasyNetwork.Boot"/> 现读配置生效；
    /// 「恢复默认」回到本机 127.0.0.1 便于单机调试。数据层见 <see cref="FantasyClient.FantasyNetworkConfig"/>。
    /// 纯代码搭 UI（仿 <see cref="GameLogic.MainMenuWindow"/>），prefab 为空壳根节点，无 m_* 绑定。
    /// </summary>
    [Window(UILayer.Top, location: "ServerConfigWindow", fullScreen: false)]
    public sealed class ServerConfigWindow : UIWindow
    {
        // 设计坐标系（1080×1920，左上原点、Y 下正），与 UGuiFactory 同口径。
        private const float Cx = 540f;

        private InputField _inputHost;
        private InputField _inputPort;
        private Button _btnKcp;
        private Button _btnWs;
        private Image _imgKcpBg;
        private Image _imgWsBg;
        private Text _textStatus;

        // 选中协议：0=KCP，1=WebSocket（编辑态工作副本，「连接并保存」时才写入配置）。
        private int _protocolSel;

        private static readonly Color PanelColor = new Color32(0x22, 0x26, 0x33, 0xF2);
        private static readonly Color FieldColor = new Color32(0x3a, 0x40, 0x52, 0xFF);
        private static readonly Color TabOnColor = new Color32(0x44, 0x77, 0xff, 0xFF);
        private static readonly Color TabOffColor = new Color32(0x3a, 0x40, 0x52, 0xFF);

        private static Font _font;
        private static Font UIFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                            ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return _font;
            }
        }

        protected override void OnCreate()
        {
            var content = UGuiFactory.CreateContentPanel(rectTransform);

            // 半透明遮罩铺满全屏，拦截背后点击（不自动关闭，避免误触丢失正在输入的地址）。
            var dim = UGuiFactory.CreateImage(content, "Dim", Cx, 960, 1080, 1920, new Color(0, 0, 0, 0.6f));
            dim.raycastTarget = true;

            // 面板底板（居中），承载所有控件。
            UGuiFactory.CreateImage(content, "Panel", Cx, 920, 920, 1180, PanelColor);

            UGuiFactory.CreateText(content, "Title", Cx, 440, 880, 100, "服务器配置", 64,
                new Color32(0xff, 0xe0, 0x66, 0xFF));

            // ── IP 行 ──
            UGuiFactory.CreateText(content, "HostLabel", 300, 600, 280, 64, "服务器 IP", 40,
                Color.white, TextAnchor.MiddleLeft);
            _inputHost = CreateInputField(content, "HostInput", 700, 600, 460, 96, "192.168.x.x");

            // ── 端口行 ──
            UGuiFactory.CreateText(content, "PortLabel", 300, 740, 280, 64, "端口", 40,
                Color.white, TextAnchor.MiddleLeft);
            _inputPort = CreateInputField(content, "PortInput", 700, 740, 460, 96, "20000");
            _inputPort.contentType = InputField.ContentType.IntegerNumber;

            // ── 协议行（两段式 KCP / WebSocket）──
            UGuiFactory.CreateText(content, "ProtoLabel", 300, 880, 280, 64, "协议", 40,
                Color.white, TextAnchor.MiddleLeft);
            _btnKcp = UGuiFactory.CreateButton(content, "BtnKcp", 600, 880, 200, 96, "KCP", 38,
                TabOffColor, Color.white, out _imgKcpBg, out _);
            _btnWs = UGuiFactory.CreateButton(content, "BtnWs", 820, 880, 200, 96, "WebSocket", 30,
                TabOffColor, Color.white, out _imgWsBg, out _);
            _btnKcp.onClick.AddListener(() => SelectProtocol(0));
            _btnWs.onClick.AddListener(() => SelectProtocol(1));

            // ── 当前连接状态 ──
            _textStatus = UGuiFactory.CreateText(content, "Status", Cx, 1010, 840, 60, "", 30,
                new Color32(0x88, 0xaa, 0xcc, 0xFF));

            // ── 操作按钮 ──
            var btnConnect = UGuiFactory.CreateButton(content, "BtnConnect", Cx, 1160, 680, 110,
                "连接并保存", 46, new Color32(0x44, 0x77, 0xff, 0xFF), Color.white, out _, out _);
            btnConnect.onClick.AddListener(OnConnect);

            var btnReset = UGuiFactory.CreateButton(content, "BtnReset", Cx, 1290, 680, 100,
                "恢复默认 (127.0.0.1)", 38, new Color32(0x77, 0x88, 0x99, 0xFF), Color.white, out _, out _);
            btnReset.onClick.AddListener(OnResetDefault);

            var btnClose = UGuiFactory.CreateButton(content, "BtnClose", Cx, 1410, 680, 100,
                "关闭", 42, new Color32(0x55, 0x5b, 0x6b, 0xFF), Color.white, out _, out _);
            btnClose.onClick.AddListener(Close);
        }

        protected override void OnRefresh()
        {
            // 每次开窗从持久化配置带出当前值（编辑态工作副本）。
#if FANTASY_UNITY
            _inputHost.text = FantasyClient.FantasyNetworkConfig.Host;
            _inputPort.text = FantasyClient.FantasyNetworkConfig.Port.ToString();
            _protocolSel = FantasyClient.FantasyNetworkConfig.Protocol == Fantasy.Network.NetworkProtocolType.WebSocket ? 1 : 0;
#else
            _inputHost.text = "127.0.0.1";
            _inputPort.text = "20000";
            _protocolSel = 0;
#endif
            RefreshProtocolTabs();
            RefreshStatus();
        }

        private void SelectProtocol(int sel)
        {
            _protocolSel = sel;
            RefreshProtocolTabs();
        }

        private void RefreshProtocolTabs()
        {
            _imgKcpBg.color = _protocolSel == 0 ? TabOnColor : TabOffColor;
            _imgWsBg.color = _protocolSel == 1 ? TabOnColor : TabOffColor;
        }

        private void RefreshStatus()
        {
#if FANTASY_UNITY
            bool connected = FantasyClient.FantasyNetwork.IsConnected;
            bool loggedIn = FantasyClient.FantasyNetwork.IsLoggedIn;
            string state = loggedIn ? "已登录" : (connected ? "已连接（未登录）" : "未连接");
            _textStatus.text = $"当前：{FantasyClient.FantasyNetworkConfig.Protocol} {FantasyClient.FantasyNetworkConfig.ServerAddress}  ·  {state}";
#else
            _textStatus.text = "网络模块未启用（FANTASY_UNITY 未定义）";
#endif
        }

        /// <summary>保存配置 → 断开重连。连接前 Boot 现读配置，新地址 / 协议即时生效。</summary>
        private void OnConnect()
        {
            string host = (_inputHost.text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(host))
            {
                _textStatus.text = "请输入服务器 IP";
                return;
            }
            if (!int.TryParse((_inputPort.text ?? string.Empty).Trim(), out int port) || port <= 0 || port > 65535)
            {
                _textStatus.text = "端口非法（应为 1~65535）";
                return;
            }

#if FANTASY_UNITY
            FantasyClient.FantasyNetworkConfig.Host = host;
            FantasyClient.FantasyNetworkConfig.Port = port;
            FantasyClient.FantasyNetworkConfig.Protocol = _protocolSel == 1
                ? Fantasy.Network.NetworkProtocolType.WebSocket
                : Fantasy.Network.NetworkProtocolType.KCP;
            FantasyClient.FantasyNetworkConfig.Save();

            // 重连：Shutdown 清连接 + 心跳 + 四态并把 _initialized=false，Boot 才会走完整初始化用新地址。
            FantasyClient.FantasyNetwork.Shutdown();
            FantasyClient.FantasyNetwork.Boot();
            _textStatus.text = $"正在连接 {FantasyClient.FantasyNetworkConfig.Protocol} {FantasyClient.FantasyNetworkConfig.ServerAddress} …";
            Log.Info($"[ServerConfig] 切换服务器并重连：{FantasyClient.FantasyNetworkConfig.Protocol} {FantasyClient.FantasyNetworkConfig.ServerAddress}");
#else
            _textStatus.text = "网络模块未启用，无法连接";
#endif
        }

        /// <summary>恢复本机默认（127.0.0.1 + 平台默认端口 / 协议）并刷新输入框。</summary>
        private void OnResetDefault()
        {
#if FANTASY_UNITY
            FantasyClient.FantasyNetworkConfig.ResetToDefault();
#endif
            OnRefresh();
        }

        private void Close() => GameModule.UI.CloseUI<ServerConfigWindow>();

        /// <summary>
        /// 创建一个 legacy InputField（含文本 + 占位文本）。UGuiFactory 无输入框工厂，故就地构建。
        /// 中文走内置 LegacyRuntime 字体（项目无 CJK TMP 字体），与 UGuiFactory 文本同口径。
        /// </summary>
        private InputField CreateInputField(Transform parent, string name, float designCx, float designCy,
            float w, float h, string placeholder)
        {
            var img = UGuiFactory.CreateImage(parent, name, designCx, designCy, w, h, FieldColor);
            img.raycastTarget = true;
            var rt = img.rectTransform;

            var input = img.gameObject.AddComponent<InputField>();
            input.targetGraphic = img;
            input.lineType = InputField.LineType.SingleLine;

            // 占位文本（无输入时显示）。
            var ph = NewChildText(rt, "Placeholder", placeholder, 36, new Color(1, 1, 1, 0.35f));
            input.placeholder = ph;

            // 实际文本。
            var txt = NewChildText(rt, "Text", string.Empty, 36, Color.white);
            input.textComponent = txt;

            return input;
        }

        // 创建充满父矩形的子文本（左对齐留内边距），供输入框文本 / 占位复用。
        private static Text NewChildText(RectTransform parent, string name, string content, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(16, 6);
            rt.offsetMax = new Vector2(-16, -6);
            var t = go.GetComponent<Text>();
            t.font = UIFont;
            t.text = content;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }
    }
}
