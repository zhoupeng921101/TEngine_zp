#if FANTASY_UNITY
using Fantasy.Network;
using UnityEngine;

namespace FantasyClient
{
    /// <summary>
    /// Fantasy 网络配置（运行时可改 + PlayerPrefs 持久化）。
    /// 连接地址 = Host:Port，协议在 KCP / WebSocket 间切换；三者经本地存储跨启动保留。
    /// 默认连接目标按运行平台分流（编译期符号）：编辑器连本机 127.0.0.1:20001(WebSocket)；
    /// WebGL 连主域 tarot-block.lulurob.cn:443(WebSocket，https 页面经 Caddy 反代为 wss)；其余平台（Standalone / Android / iOS）连外网 121.199.24.31:20000(KCP)。
    /// 这三者只是 PlayerPrefs 缺省值——经 <see cref="GameLogic.UI.ServerConfigWindow"/> 手动改并 <see cref="Save"/> 落盘后，
    /// 存盘值优先生效、不被平台默认覆盖；连接前由 <see cref="FantasyNetwork.Boot"/> 现读 <see cref="ServerAddress"/> / <see cref="Protocol"/>。
    /// </summary>
    public static class FantasyNetworkConfig
    {
        // 外网正式服地址；编辑器除外的所有运行平台默认连此。
        private const string RemoteHost = "121.199.24.31";

#if UNITY_EDITOR
        // 编辑器内运行（含 Play Mode，无论当前 BuildTarget）始终连本机 WebSocket Gate(20001)，便于单机联调。
        // 与外网部署版服务端对齐：该部署只开 WebSocket Gate(20001)，未开 KCP Gate(20000)。
        public const string DefaultHost = "127.0.0.1";
        public const int DefaultPort = 20001;
        public const NetworkProtocolType DefaultProtocol = NetworkProtocolType.WebSocket;
#elif FANTASY_WEBGL
        // WebGL（浏览器）只能用 WebSocket，连主域 443；Caddy 终结 TLS 后按 Upgrade 头反代到本机 ws Gate(20001)。
        // https 页面经 UseSsl 自动拼 wss://，与浏览器混合内容策略一致。
        public const string DefaultHost = "tarot-block.lulurob.cn";
        public const int DefaultPort = 443;
        public const NetworkProtocolType DefaultProtocol = NetworkProtocolType.WebSocket;
#else
        // 真机 / 发布包（Standalone / Android / iOS）走 KCP，连外网 KCP Gate(20000)。
        public const string DefaultHost = RemoteHost;
        public const int DefaultPort = 20000;
        public const NetworkProtocolType DefaultProtocol = NetworkProtocolType.KCP;
#endif

        private const string PrefHost = "Fantasy.Net.Host";
        private const string PrefPort = "Fantasy.Net.Port";
        private const string PrefProtocol = "Fantasy.Net.Protocol";

        private static bool _loaded;
        private static string _host;
        private static int _port;
        private static NetworkProtocolType _protocol;

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _host = PlayerPrefs.GetString(PrefHost, DefaultHost);
            _port = PlayerPrefs.GetInt(PrefPort, DefaultPort);
            _protocol = (NetworkProtocolType)PlayerPrefs.GetInt(PrefProtocol, (int)DefaultProtocol);
            _loaded = true;
        }

        /// <summary>服务器主机（IP 或域名，如外网 121.199.24.31 / 本机 127.0.0.1）。set 改内存值，须调 <see cref="Save"/> 才落盘。</summary>
        public static string Host
        {
            get { EnsureLoaded(); return _host; }
            set { EnsureLoaded(); _host = value; }
        }

        /// <summary>服务器 Gate 端口（KCP 默认 20000 / WebSocket 默认 20001）。</summary>
        public static int Port
        {
            get { EnsureLoaded(); return _port; }
            set { EnsureLoaded(); _port = value; }
        }

        /// <summary>连接协议（KCP / WebSocket）。</summary>
        public static NetworkProtocolType Protocol
        {
            get { EnsureLoaded(); return _protocol; }
            set { EnsureLoaded(); _protocol = value; }
        }

        /// <summary>Fantasy 连接接口所需地址，格式 IP:Port（传输层据此拼成 ws://IP:Port 或 wss://IP:Port，scheme 由 <see cref="UseSsl"/> 决定）。</summary>
        public static string ServerAddress
        {
            get { EnsureLoaded(); return $"{_host}:{_port}"; }
        }

        /// <summary>
        /// WebSocket 是否启用 TLS(wss)。WebGL 下默认 true(加密)，仅宿主页面显式 http:// 时才降级明文 ws；其余平台默认 false(明文 ws)。
        /// 由 <see cref="FantasyNetwork.Connect"/> 作 isHttps 入参传入传输层，决定连接 scheme(ws:// / wss://)。
        /// Application.absoluteURL 在部分浏览器(Chrome/Firefox 等)返回空字符串，不能据其降级明文——空 URL 时默认 wss，
        /// 与正式部署恒在 https 域名一致；浏览器禁止 https 页内连明文 ws(混合内容策略)，降级 ws 会被拦。
        /// wss 还要求服务端在域名上配好 TLS(证书不能绑裸 IP)。
        /// </summary>
        public static bool UseSsl
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                var url = Application.absoluteURL;
                // 默认加密：空/取不到 URL 时走 wss；仅页面显式 http:// 时才用明文 ws(本地 http 调试场景)。
                return string.IsNullOrEmpty(url) || url.StartsWith("https");
#else
                return false;
#endif
            }
        }

        /// <summary>把当前 Host / Port / Protocol 写入本地存储，下次启动自动带出。</summary>
        public static void Save()
        {
            EnsureLoaded();
            PlayerPrefs.SetString(PrefHost, _host);
            PlayerPrefs.SetInt(PrefPort, _port);
            PlayerPrefs.SetInt(PrefProtocol, (int)_protocol);
            PlayerPrefs.Save();
        }

        /// <summary>恢复为当前平台默认 Host / 端口 / 协议并落盘（编辑器为 127.0.0.1，其余平台为外网），便于改过地址后一键还原。</summary>
        public static void ResetToDefault()
        {
            EnsureLoaded();
            _host = DefaultHost;
            _port = DefaultPort;
            _protocol = DefaultProtocol;
            Save();
        }

        /// <summary>
        /// 是否开启框架中央收发包 JSON 日志（默认关）。
        /// 开启后 <see cref="FantasyNetwork.Connect"/> 把该值作 enableReceiveMessageJsonLog 尾参传入 Scene.Connect，
        /// 框架改用 DebugClientSession（打印所有发包）+ DebugClientMessageScheduler（打印所有收包，含 RPC 响应），整包 JSON、Log.Debug 级。
        /// 噪声大且为 Debug 级，平时关、调试链路时开（须确保日志级别含 Debug 才可见）。
        /// </summary>
        public static bool EnableNetworkJsonLog = true;

        /// <summary>连接成功后是否自动登录。</summary>
        public static bool AutoLogin = true;

        /// <summary>连接失败/断开后是否自动重连（成功后会自动重登）。</summary>
        public static bool AutoReconnect = true;

        /// <summary>最大重连次数；0 表示无限重连。</summary>
        public static int ReconnectMaxAttempts = 10;

        /// <summary>重连退避基数（毫秒），实际延时 = min(base * 次数, max)。</summary>
        public static int ReconnectBaseDelayMs = 1000;

        /// <summary>重连退避上限（毫秒）。</summary>
        public static int ReconnectMaxDelayMs = 8000;

        /// <summary>
        /// 默认账号名：基于设备唯一标识，保证同一设备每次登录同一账号（便于测试）。
        /// 正式项目应换成真实账号体系（如登录服下发的 token / 第三方登录）。
        /// </summary>
        public static string DefaultAccountName()
        {
            var id = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrEmpty(id) || id == SystemInfo.unsupportedIdentifier)
            {
                id = "editor";
            }
            return "dev_" + id;
        }
    }
}
#endif
