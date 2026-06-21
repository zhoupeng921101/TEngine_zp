#if FANTASY_UNITY
using Fantasy.Network;
using UnityEngine;

namespace FantasyClient
{
    /// <summary>
    /// Fantasy 网络配置（集中一处，便于后续接入正式配置或区分 dev / 线上环境）。
    /// 目前为简单静态字段；如需多环境，可改为从 TEngine 配置表 / 环境变量 / 打包参数读取。
    /// </summary>
    public static class FantasyNetworkConfig
    {
#if FANTASY_WEBGL
        // WebGL（浏览器）只能用 WebSocket，连示例服务器的 WebSocket Gate(20001)。
        /// <summary>服务器 Gate 地址，格式 IP:Port（WebSocket 会自动拼成 ws://IP:Port）。</summary>
        public static string ServerAddress = "127.0.0.1:20001";
        /// <summary>连接协议。WebGL 平台固定 WebSocket。</summary>
        public static readonly NetworkProtocolType Protocol = NetworkProtocolType.WebSocket;
#else
        // 原生/编辑器默认走 KCP，连示例服务器的 KCP Gate(20000)。
        /// <summary>服务器 Gate 地址，格式 IP:Port。示例服务器为 KCP 127.0.0.1:20000。</summary>
        public static string ServerAddress = "127.0.0.1:20000";
        /// <summary>连接协议。</summary>
        public static readonly NetworkProtocolType Protocol = NetworkProtocolType.KCP;
#endif

        /// <summary>连接成功后是否自动登录。</summary>
        public static bool AutoLogin = true;

        /// <summary>登录成功后是否自动进入游戏（发送 C2M_InitComplete，触发服务器推送单位）。</summary>
        public static bool AutoEnterGame = true;

        /// <summary>连接失败/断开后是否自动重连（成功后会自动重登并重进游戏）。</summary>
        public static bool AutoReconnect = true;

        /// <summary>最大重连次数；0 表示无限重连。</summary>
        public static int ReconnectMaxAttempts = 3;

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
