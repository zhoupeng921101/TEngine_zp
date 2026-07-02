using UnityEngine;
using UnityEngine.UI;
using TEngine;
using Cysharp.Threading.Tasks;
using Log = TEngine.Log;
#if FANTASY_UNITY
using Fantasy; // C2G_ClearPlayerDataRequest 扩展方法 + ClearPlayerDataResultCode 所在命名空间
#endif

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
    public sealed class ServerConfigWindow : UIWindowMono
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

        // 诊断面板：懒建的全窗覆盖层（点「诊断连接」首次构建，之后复用显隐）。
        // 结构：Dim 遮罩 + 滚动结果区（多行报告文本）+ 复制 / 关闭按钮。
        private GameObject _diagPanel;
        private Text _diagText;
        private bool _diagRunning;
        // 探测目标外网正式服（与 FantasyNetworkConfig.RemoteHost 同址；诊断主动测外网，不只测当前生效地址）。
        private const string DiagRemoteHost = "121.199.24.31";
        private const int DiagWsPort = 20001;   // WS/TCP Gate
        private const int DiagKcpPort = 20000;  // KCP/UDP Gate
        private const int DiagTimeoutMs = 3000; // 短超时，避免点一下卡死

        // 清空玩家数据按钮 + 二次确认态：首点 _clearArmed 置 true 并改文字提示，再点才真发请求；
        // 期间禁重入（_clearing）。开窗 OnRefresh 复位 _clearArmed，避免上次开窗的「已确认」态残留。
        private Button _btnClearData;
        private Text _btnClearDataLabel;
        private bool _clearArmed;
        private bool _clearing;

        private static readonly Color DangerColor = new Color32(0xcc, 0x33, 0x33, 0xFF);
        private static readonly Color DangerArmedColor = new Color32(0xff, 0x55, 0x33, 0xFF);

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

            // 诊断入口（排障工具）：置于标题下方角落，点开全窗诊断报告覆盖层。连不上的玩家也能在此自查链路。
            var btnDiag = UGuiFactory.CreateButton(content, "BtnDiag", 820, 530, 280, 80,
                "诊断连接", 36, new Color32(0x33, 0x88, 0x66, 0xFF), Color.white, out _, out _);
            btnDiag.onClick.AddListener(OpenDiagnostics);

            // ── IP 行 ──
            UGuiFactory.CreateText(content, "HostLabel", 300, 600, 280, 64, "服务器 IP", 40,
                Color.white, TextAnchor.MiddleLeft);
            _inputHost = UGuiFactory.CreateInputField(content, "HostInput", 700, 600, 460, 96, "192.168.x.x", FieldColor);

            // ── 端口行 ──
            UGuiFactory.CreateText(content, "PortLabel", 300, 740, 280, 64, "端口", 40,
                Color.white, TextAnchor.MiddleLeft);
            _inputPort = UGuiFactory.CreateInputField(content, "PortInput", 700, 740, 460, 96, "20000", FieldColor);
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

            // ── 操作按钮（紧凑排布，腾出清档按钮位，不压窗）──
            var btnConnect = UGuiFactory.CreateButton(content, "BtnConnect", Cx, 1120, 680, 96,
                "连接并保存", 46, new Color32(0x44, 0x77, 0xff, 0xFF), Color.white, out _, out _);
            btnConnect.onClick.AddListener(OnConnect);

            var btnReset = UGuiFactory.CreateButton(content, "BtnReset", Cx, 1230, 680, 90,
                "恢复默认 (127.0.0.1)", 38, new Color32(0x77, 0x88, 0x99, 0xFF), Color.white, out _, out _);
            btnReset.onClick.AddListener(OnResetDefault);

            // 危险操作：清空玩家数据（始终可见，调试窗任何包都显示）。红系底色，二次确认才发。
            _btnClearData = UGuiFactory.CreateButton(content, "BtnClearData", Cx, 1340, 680, 90,
                ClearLabelIdle, 40, DangerColor, Color.white, out _, out _btnClearDataLabel);
            _btnClearData.onClick.AddListener(OnClearPlayerData);

            var btnClose = UGuiFactory.CreateButton(content, "BtnClose", Cx, 1450, 680, 90,
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
            DisarmClear(); // 复位二次确认态：上次开窗若停在「已确认」态，本次开窗回到初始未确认。
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
            TEngine.Log.Info($"[ServerConfig] 切换服务器并重连：{FantasyClient.FantasyNetworkConfig.Protocol} {FantasyClient.FantasyNetworkConfig.ServerAddress}");
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

        // ── 清空玩家数据（清档·客户端段）────────────────────────────────

        private const string ClearLabelIdle = "清空玩家数据";
        private const string ClearLabelArmed = "再点一次确认清空";

        /// <summary>清档按钮点击：两段式。首点仅武装确认；再点（已武装）才真正发请求。</summary>
        private void OnClearPlayerData()
        {
            if (_clearing) return; // 在途防重入

            if (!_clearArmed)
            {
                _clearArmed = true;
                if (_btnClearDataLabel != null) _btnClearDataLabel.text = ClearLabelArmed;
                if (_btnClearData != null) _btnClearData.image.color = DangerArmedColor;
                _textStatus.text = "清档不可恢复：再点一次确认，或点其它按钮取消。";
                return;
            }

            // 已武装：执行。立即解除武装态并进入在途态，防连点重发。
            DisarmClear();
            _clearing = true;
            ClearPlayerDataFlow().Forget();
        }

        /// <summary>解除二次确认态：按钮文字 / 颜色回到初始。不动在途态 <see cref="_clearing"/>。</summary>
        private void DisarmClear()
        {
            _clearArmed = false;
            if (_btnClearDataLabel != null) _btnClearDataLabel.text = ClearLabelIdle;
            if (_btnClearData != null && _btnClearData.image != null) _btnClearData.image.color = DangerColor;
        }

        /// <summary>
        /// 清档主流程（仿 <see cref="GameLogic.BlockBlast.Player.EnterMainGameGatewayProd"/> 范式）：
        /// guard 连接+登录 → 发 <c>C2G_ClearPlayerDataRequest</c>（无参，身份从会话取）→ 按结果码处理。
        /// Success：清本地玩法投影缓存 → 断开网络 → 全量重启（重载场景 0，重走登录/快照/进主游戏），
        /// 使客户端从已重置的服务端快照重建为新手态，而非沿用旧内存/本地视图。
        /// 任何往返失败/超时不抛，走状态文字提示重试，不误清本地。
        /// </summary>
        private async UniTaskVoid ClearPlayerDataFlow()
        {
#if FANTASY_UNITY
            try
            {
                var session = FantasyClient.FantasyNetwork.Session;
                if (session == null
                    || !FantasyClient.FantasyNetwork.IsConnected
                    || !FantasyClient.FantasyNetwork.IsLoggedIn)
                {
                    _textStatus.text = "请先连接并登录后再清空（未登录无法清服务端数据）。";
                    return;
                }

                _textStatus.text = "正在清空玩家数据…";

                G2C_ClearPlayerDataResponse response;
                try
                {
                    response = await session.C2G_ClearPlayerDataRequest();
                }
                catch
                {
                    _textStatus.text = "清档请求失败，请重试。";
                    return;
                }
                if (response == null)
                {
                    _textStatus.text = "清档无响应，请重试。";
                    return;
                }

                switch (response.ResultCode)
                {
                    case ClearPlayerDataResultCode.Success:
                        // ① 清本地玩法投影缓存（务必在重连重登前，否则重登从本地缓存复活旧数据，尤其棋盘 blob）。
                        GameLogic.BlockBlast.Player.PlayerDataLocalReset.ClearAll();
                        _textStatus.text = "已清空，正在重连…";
                        TEngine.Log.Info("[ServerConfig] 玩家数据已清空，本地缓存已清，软重启重登。");
                        // ② 软重启重登：复位入口闸 + 关所有窗 + 释放内存单例 + Shutdown/Boot 重连。
                        //    使客户端从已重置的服务端快照重建为新手态（详见 GameApp.RestartAfterDataReset）。
                        //    本窗将随 CloseAll 销毁，故此后不再触本窗 UI。
                        GameApp.RestartAfterDataReset();
                        break;
                    case ClearPlayerDataResultCode.NotLoggedIn:
                        _textStatus.text = "未登录，请先连接。";
                        break;
                    case ClearPlayerDataResultCode.ServiceUnavailable:
                        _textStatus.text = "服务繁忙，请重试。";
                        break;
                    default:
                        _textStatus.text = "清档返回未知结果，请重试。";
                        break;
                }
            }
            finally
            {
                _clearing = false;
            }
#else
            _textStatus.text = "网络模块未启用（FANTASY_UNITY 未定义）。";
            _clearing = false;
            await UniTask.CompletedTask;
#endif
        }

        private void Close() => GameModule.UI.CloseUI<ServerConfigWindow>();

        // ── 连接诊断（运行时排障，发布包内可用）────────────────────────────
        //
        // 点「诊断连接」打开全窗覆盖层，报告运行环境 / 平台默认 / 实际生效目标 / 双协议外网可达性 + 人话结论。
        // 探测走 FantasyNetworkDiagnostics：每次创建独立临时 Scene 连接尝试、用后即弃，不碰主连接与自动重连。
        // KCP（UDP）可达性走真实 KCP 握手判定，不用 TCP 测 UDP 端口（TCP 连 UDP 必失败 = 假阴性）。

        /// <summary>打开诊断覆盖层并立即跑一轮探测。</summary>
        private void OpenDiagnostics()
        {
            if (_diagPanel == null)
            {
                BuildDiagPanel();
            }
            _diagPanel.SetActive(true);
            RunDiagnostics().Forget();
        }

        private void CloseDiagnostics()
        {
            if (_diagPanel != null)
            {
                _diagPanel.SetActive(false);
            }
        }

        /// <summary>懒建诊断覆盖层：Dim 遮罩 + 可滚动结果文本 + 重测 / 关闭按钮。</summary>
        private void BuildDiagPanel()
        {
            var content = transform.Find("Content") ?? transform;

            _diagPanel = UGuiFactory.CreateNode(content, "DiagPanel").gameObject;
            var panelRt = (RectTransform)_diagPanel.transform;
            panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(1080, 1920);
            panelRt.anchoredPosition = Vector2.zero;

            // 不透明遮罩盖住背后配置窗，拦截穿透点击。
            var dim = UGuiFactory.CreateImage(panelRt, "DiagDim", Cx, 960, 1080, 1920, new Color(0.06f, 0.07f, 0.10f, 0.98f));
            dim.raycastTarget = true;

            UGuiFactory.CreateText(panelRt, "DiagTitle", Cx, 200, 880, 100, "连接诊断报告", 56,
                new Color32(0xff, 0xe0, 0x66, 0xFF));

            // 可滚动结果区：ScrollRect + RectMask2D 视口 + ContentSizeFitter 自适应高度的文本。
            var frame = UGuiFactory.CreateImage(panelRt, "DiagScrollFrame", Cx, 940, 960, 1180, new Color(0.13f, 0.15f, 0.20f, 1f));
            var frameRt = frame.rectTransform;
            var scroll = frame.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            var vpRt = viewport.GetComponent<RectTransform>();
            vpRt.SetParent(frameRt, false);
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = new Vector2(20, 20);
            vpRt.offsetMax = new Vector2(-20, -20);
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0.001f); // 近乎透明，仅作 Mask 的 graphic
            scroll.viewport = vpRt;

            var contentGo = new GameObject("DiagContent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text),
                typeof(ContentSizeFitter));
            var cRt = contentGo.GetComponent<RectTransform>();
            cRt.SetParent(vpRt, false);
            cRt.anchorMin = new Vector2(0, 1);
            cRt.anchorMax = new Vector2(1, 1);
            cRt.pivot = new Vector2(0.5f, 1f);
            cRt.anchoredPosition = Vector2.zero;
            cRt.offsetMin = new Vector2(0, cRt.offsetMin.y);
            cRt.offsetMax = new Vector2(0, cRt.offsetMax.y);
            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _diagText = contentGo.GetComponent<Text>();
            _diagText.font = UIFont;
            _diagText.fontSize = 32;
            _diagText.color = new Color32(0xdd, 0xee, 0xff, 0xFF);
            _diagText.alignment = TextAnchor.UpperLeft;
            _diagText.supportRichText = false;
            _diagText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _diagText.verticalOverflow = VerticalWrapMode.Overflow;
            _diagText.text = "";
            scroll.content = cRt;

            var btnRetest = UGuiFactory.CreateButton(panelRt, "DiagRetest", 360, 1620, 460, 110,
                "重新诊断", 44, new Color32(0x33, 0x88, 0x66, 0xFF), Color.white, out _, out _);
            btnRetest.onClick.AddListener(() => RunDiagnostics().Forget());

            var btnBack = UGuiFactory.CreateButton(panelRt, "DiagBack", 720, 1620, 460, 110,
                "返回", 44, new Color32(0x55, 0x5b, 0x6b, 0xFF), Color.white, out _, out _);
            btnBack.onClick.AddListener(CloseDiagnostics);
        }

        /// <summary>跑一轮诊断：收集环境 / 配置信息 + 双协议对外网（及当前生效地址）探测，汇总成报告文本。</summary>
        private async UniTaskVoid RunDiagnostics()
        {
            if (_diagRunning) return;
            _diagRunning = true;
            try
            {
#if FANTASY_UNITY
                var sb = new System.Text.StringBuilder(1024);

                // ① 运行环境
                sb.AppendLine("【运行环境】");
                sb.AppendLine($"  编辑器内运行：{(Application.isEditor ? "是" : "否")}");
                sb.AppendLine($"  运行平台：{Application.platform}");
                sb.AppendLine($"  本编译分支平台默认：{FantasyClient.FantasyNetworkConfig.DefaultProtocol} " +
                              $"{FantasyClient.FantasyNetworkConfig.DefaultHost}:{FantasyClient.FantasyNetworkConfig.DefaultPort}");
                sb.AppendLine();

                // ② 实际生效目标（PlayerPrefs 解析后）+ 是否等于平台默认
                string effHost = FantasyClient.FantasyNetworkConfig.Host;
                int effPort = FantasyClient.FantasyNetworkConfig.Port;
                var effProto = FantasyClient.FantasyNetworkConfig.Protocol;
                bool sameAsDefault = effHost == FantasyClient.FantasyNetworkConfig.DefaultHost
                                     && effPort == FantasyClient.FantasyNetworkConfig.DefaultPort
                                     && effProto == FantasyClient.FantasyNetworkConfig.DefaultProtocol;
                sb.AppendLine("【实际生效目标】");
                sb.AppendLine($"  当前会连：{effProto} {effHost}:{effPort}");
                if (sameAsDefault)
                {
                    sb.AppendLine("  与平台默认一致。");
                }
                else
                {
                    sb.AppendLine("  ⚠ 当前用的是存盘地址，非平台默认！");
                    sb.AppendLine("    （若连不上，多半是旧存盘地址顶掉了平台默认，下方点『返回』再『恢复默认』。）");
                }
                sb.AppendLine();

                _diagText.text = sb.ToString() + "正在探测，请稍候…";

                // ③ 双协议对外网可达性探测（主动测外网，不只测当前生效地址）
                sb.AppendLine($"【外网可达性探测（{DiagRemoteHost}）】");
                var wsRemote = await ProbeAsync(DiagRemoteHost, DiagWsPort, Fantasy.Network.NetworkProtocolType.WebSocket);
                sb.AppendLine($"  WS  (TCP {DiagWsPort})：{DescribeProbe(wsRemote)}");
                var kcpRemote = await ProbeAsync(DiagRemoteHost, DiagKcpPort, Fantasy.Network.NetworkProtocolType.KCP);
                sb.AppendLine($"  KCP (UDP {DiagKcpPort})：{DescribeProbe(kcpRemote)}");
                sb.AppendLine();

                // 也测当前生效地址（若与外网不同，便于对比）
                FantasyClient.DiagResult effProbe = default;
                bool effProbed = false;
                if (effHost != DiagRemoteHost)
                {
                    sb.AppendLine($"【当前生效地址探测（{effHost}:{effPort}）】");
                    effProbe = await ProbeAsync(effHost, effPort, effProto);
                    effProbed = true;
                    sb.AppendLine($"  {effProto}：{DescribeProbe(effProbe)}");
                    sb.AppendLine();
                }

                // ④ 结论文本
                sb.AppendLine("【结论】");
                foreach (var line in BuildVerdict(wsRemote, kcpRemote, effHost, effPort, effProto, sameAsDefault, effProbed, effProbe))
                {
                    sb.AppendLine("  " + line);
                }

                _diagText.text = sb.ToString();
#else
                _diagText.text = "网络模块未启用（FANTASY_UNITY 未定义），无法诊断。";
                await UniTask.CompletedTask;
#endif
            }
            finally
            {
                _diagRunning = false;
            }
        }

#if FANTASY_UNITY
        /// <summary>把 FantasyNetworkDiagnostics 的回调式探测包成可 await 的 UniTask（单次完成）。</summary>
        private UniTask<FantasyClient.DiagResult> ProbeAsync(string host, int port, Fantasy.Network.NetworkProtocolType protocol)
        {
            var tcs = new UniTaskCompletionSource<FantasyClient.DiagResult>();
            FantasyClient.FantasyNetworkDiagnostics.TestConnect(host, port, protocol, DiagTimeoutMs,
                result => tcs.TrySetResult(result));
            return tcs.Task;
        }

        /// <summary>把探测结果翻成人话（含耗时）。</summary>
        private static string DescribeProbe(FantasyClient.DiagResult r)
        {
            switch (r.Outcome)
            {
                case FantasyClient.DiagOutcome.Success:
                    return $"可达（握手成功，{r.ElapsedMs}ms）";
                case FantasyClient.DiagOutcome.Refused:
                    return $"连接被拒绝（端口快速拒收，{r.ElapsedMs}ms）";
                case FantasyClient.DiagOutcome.Timeout:
                    return $"超时不可达（{r.ElapsedMs}ms，端口被丢弃/未监听/网络不通）";
                default:
                    return $"探测异常（{r.Error}）";
            }
        }

        /// <summary>据探测结果给人话结论与下一步建议。</summary>
        private static System.Collections.Generic.List<string> BuildVerdict(
            FantasyClient.DiagResult wsRemote, FantasyClient.DiagResult kcpRemote,
            string effHost, int effPort, Fantasy.Network.NetworkProtocolType effProto,
            bool sameAsDefault, bool effProbed, FantasyClient.DiagResult effProbe)
        {
            var list = new System.Collections.Generic.List<string>();

            if (Application.isEditor)
            {
                list.Add("编辑器默认连本机 127.0.0.1，属预期（本机需自起服务端）。");
            }

            bool wsOk = wsRemote.Outcome == FantasyClient.DiagOutcome.Success;
            bool kcpOk = kcpRemote.Outcome == FantasyClient.DiagOutcome.Success;

            if (wsOk && !kcpOk)
            {
                list.Add("外网 WS 通、KCP 不通：疑似服务端 UDP 20000 未放行。");
                list.Add("→ 检查云安全组 UDP 入方向规则（放行 UDP 20000）；或改用 WebSocket 协议连 20001。");
            }
            else if (wsOk && kcpOk)
            {
                list.Add("外网 WS 与 KCP 均可达：服务端两路 Gate 都正常。");
            }
            else if (!wsOk && !kcpOk)
            {
                list.Add("外网 WS 与 KCP 均不可达：服务端可能未启动，或本机网络/出口被限。");
            }
            else // !wsOk && kcpOk
            {
                list.Add("外网 KCP 通、WS 不通：检查服务端 TCP 20001 是否监听 / 放行。");
            }

            // 存盘地址陷阱（嫌疑 2）
            if (!sameAsDefault)
            {
                if (effHost == "127.0.0.1" || effHost == "localhost")
                {
                    list.Add("当前连的是存盘的本机地址 127.0.0.1（非平台默认外网）：");
                    list.Add("→ 点『返回』再点『恢复默认』，改用平台默认外网地址。");
                }
                else
                {
                    list.Add($"当前生效地址（{effHost}:{effPort}）是存盘值、非平台默认：");
                    list.Add("→ 若连不上，点『返回』再点『恢复默认』回到平台默认。");
                }
            }

            // 当前生效地址自身探测结论
            if (effProbed)
            {
                if (effProbe.Outcome == FantasyClient.DiagOutcome.Success)
                {
                    list.Add($"当前生效地址 {effProto} {effHost}:{effPort} 探测可达。");
                }
                else
                {
                    list.Add($"当前生效地址 {effProto} {effHost}:{effPort} 探测不可达（{DescribeProbe(effProbe)}）。");
                }
            }

            return list;
        }
#endif

    }
}
