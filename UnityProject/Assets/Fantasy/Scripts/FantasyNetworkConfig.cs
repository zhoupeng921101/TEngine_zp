#if FANTASY_UNITY
using Fantasy.Network;
using UnityEngine;

namespace FantasyClient
{
    /// <summary>
    /// Fantasy 网络配置（运行时可改 + PlayerPrefs 持久化）。
    /// 连接地址 = Host:Port，协议在 KCP / WebSocket 间切换；三者经本地存储跨启动保留。
    /// 局域网联调时通过 <see cref="GameLogic.UI.ServerConfigWindow"/> 修改后重连即生效（连接前由
    /// <see cref="FantasyNetwork.Boot"/> 现读 <see cref="ServerAddress"/> / <see cref="Protocol"/>）。
    /// </summary>
    public static class FantasyNetworkConfig
    {
#if FANTASY_WEBGL
        // WebGL（浏览器）只能用 WebSocket，默认连示例服务器的 WebSocket Gate(20001)。
        public const string DefaultHost = "127.0.0.1";
        public const int DefaultPort = 20001;
        public const NetworkProtocolType DefaultProtocol = NetworkProtocolType.WebSocket;
#else
        // 原生 / 编辑器默认走 KCP，连示例服务器的 KCP Gate(20000)。
        public const string DefaultHost = "127.0.0.1";
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

        /// <summary>服务器主机（局域网 IP，如 192.168.x.x）。set 改内存值，须调 <see cref="Save"/> 才落盘。</summary>
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

        /// <summary>Fantasy 连接接口所需地址，格式 IP:Port（WebSocket 传输层自动拼成 ws://IP:Port）。</summary>
        public static string ServerAddress
        {
            get { EnsureLoaded(); return $"{_host}:{_port}"; }
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

        /// <summary>恢复为本机默认（127.0.0.1 + 平台默认端口 / 协议）并落盘，便于单机调试与联调来回切。</summary>
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
