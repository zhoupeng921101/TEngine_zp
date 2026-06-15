#if FANTASY_UNITY
using System;
using System.Collections.Generic;
using Fantasy;
using Fantasy.Async;
using Fantasy.Network;

namespace FantasyClient
{
    /// <summary>
    /// Fantasy 客户端网络管理器（静态门面）。
    /// 职责：初始化 Fantasy 运行时 -> 创建客户端 Scene -> 连接服务器 Gate -> 维持心跳 -> 登录。
    /// 由 TEngine 热更入口 GameApp.StartGameLogic() 调用 <see cref="Boot"/> 启动。
    ///
    /// 业务侧用法：
    ///   FantasyNetwork.OnLoggedIn += () => { /* 进入主界面、拉取数据等 */ };
    ///   FantasyNetwork.Boot();                       // 启动（地址/账号取自 FantasyNetworkConfig）
    ///   // 之后通过 FantasyNetwork.Session 发协议：FantasyNetwork.Session.C2X_Xxx(...)
    /// </summary>
    public static class FantasyNetwork
    {
        /// <summary>客户端 Scene，承载所有 Fantasy 功能。</summary>
        public static Scene Scene { get; private set; }
        /// <summary>与服务器的会话，用于收发消息。</summary>
        public static Session Session { get; private set; }
        /// <summary>当前是否已连接。</summary>
        public static bool IsConnected { get; private set; }
        /// <summary>当前是否已登录。</summary>
        public static bool IsLoggedIn { get; private set; }
        /// <summary>是否已进入游戏（已通知服务器 C2M_InitComplete）。</summary>
        public static bool IsInGame { get; private set; }
        /// <summary>已登录的账号名。</summary>
        public static string AccountName { get; private set; }

        /// <summary>连接成功后触发。</summary>
        public static event Action OnConnected;
        /// <summary>登录成功后触发。</summary>
        public static event Action OnLoggedIn;
        /// <summary>已进入游戏后触发（已请求服务器推送单位，推荐业务在此进入主场景）。</summary>
        public static event Action OnEnteredGame;
        /// <summary>连接断开后触发。</summary>
        public static event Action OnDisconnected;

        private static string _address;
        private static string _account;
        private static bool _initialized;
        private static bool _intentionalClose;
        private static int _reconnectAttempt;

        /// <summary>
        /// 启动网络（初始化 + 连接，连接成功后按配置自动登录）。非 async，可直接在启动流程中调用。
        /// </summary>
        /// <param name="address">服务器 Gate 地址；为 null 时取 <see cref="FantasyNetworkConfig.ServerAddress"/>。</param>
        /// <param name="account">登录账号；为 null 时按配置决定是否用默认账号自动登录。</param>
        public static void Boot(string address = null, string account = null)
        {
            _intentionalClose = false;
            _reconnectAttempt = 0;
            _address = address ?? FantasyNetworkConfig.ServerAddress;
            _account = account ?? (FantasyNetworkConfig.AutoLogin ? FantasyNetworkConfig.DefaultAccountName() : null);
            BootAsync().Coroutine();
        }

        private static async FTask BootAsync()
        {
            if (!_initialized)
            {
                // Log 在 Entry.Initialize() 内部才就绪，初始化前不要调用 Log.*。
                await Fantasy.Platform.Unity.Entry.Initialize();
                Scene = await Scene.Create(SceneRuntimeMode.MainThread);
                _initialized = true;
                Log.Info("[Fantasy] 运行时初始化完成");
            }

            Connect();
        }

        private static void Connect()
        {
            Log.Info($"[Fantasy] 连接服务器 {_address} (KCP) ...");
            Session = Scene.Connect(
                _address,
                NetworkProtocolType.KCP,
                OnConnectComplete,
                OnConnectFail,
                OnConnectDisconnect,
                false, 5000);
        }

        private static void OnConnectComplete()
        {
            IsConnected = true;
            _reconnectAttempt = 0;
            // 服务器有心跳检测，需加心跳组件，否则空闲超时会被断开。
            Session.AddComponent<SessionHeartbeatComponent>().Start(2000);
            Log.Info("[Fantasy] ✅ 已连接服务器");
            OnConnected?.Invoke();

            // 按配置自动登录
            if (!string.IsNullOrEmpty(_account))
            {
                LoginAsync(_account).Coroutine();
            }
        }

        private static void OnConnectFail()
        {
            IsConnected = false;
            Log.Error("[Fantasy] ❌ 连接服务器失败：请确认服务器已启动且 Gate(KCP 20000) 在监听。");
            ScheduleReconnect();
        }

        private static void OnConnectDisconnect()
        {
            IsConnected = false;
            IsLoggedIn = false;
            IsInGame = false;
            UnitViewManager.Clear();
            Log.Warning("[Fantasy] 与服务器断开连接");
            OnDisconnected?.Invoke();
            ScheduleReconnect();
        }

        private static void ScheduleReconnect()
        {
            if (_intentionalClose || !FantasyNetworkConfig.AutoReconnect)
            {
                return;
            }

            var max = FantasyNetworkConfig.ReconnectMaxAttempts;
            if (max > 0 && _reconnectAttempt >= max)
            {
                Log.Error($"[Fantasy] 重连次数已达上限({max})，停止重连。");
                return;
            }

            _reconnectAttempt++;
            ReconnectAfterDelayAsync().Coroutine();
        }

        private static async FTask ReconnectAfterDelayAsync()
        {
            var delay = Math.Min(
                FantasyNetworkConfig.ReconnectBaseDelayMs * _reconnectAttempt,
                FantasyNetworkConfig.ReconnectMaxDelayMs);
            Log.Info($"[Fantasy] {delay}ms 后第 {_reconnectAttempt} 次重连 ...");
            await Scene.TimerComponent.Net.WaitAsync(delay);

            if (_intentionalClose || IsConnected)
            {
                return;
            }

            // 复用已有 Scene 重新建立连接；成功后 OnConnectComplete 会自动重登并重进游戏。
            Connect();
        }

        /// <summary>
        /// 登录：发送 C2G_LoginGameRequest，返回服务器错误码（0 表示成功）。
        /// 成功后置位 <see cref="IsLoggedIn"/> 并触发 <see cref="OnLoggedIn"/>。
        /// </summary>
        public static async FTask<uint> LoginAsync(string accountName)
        {
            if (!IsConnected)
            {
                Log.Error("[Fantasy] 未连接，无法登录");
                return uint.MaxValue;
            }

            Log.Info($"[Fantasy] 登录中 account={accountName} ...");
            var response = await Session.C2G_LoginGameRequest(accountName);
            if (response.ErrorCode != 0)
            {
                Log.Error($"[Fantasy] ❌ 登录失败 ErrorCode={response.ErrorCode}");
                return response.ErrorCode;
            }

            IsLoggedIn = true;
            AccountName = accountName;
            Log.Info($"[Fantasy] ✅ 登录成功 account={accountName}");
            OnLoggedIn?.Invoke();

            // 按配置进入游戏：通知服务器准备就绪，开始接收单位等推送。
            if (FantasyNetworkConfig.AutoEnterGame)
            {
                EnterGame();
            }
            return 0;
        }

        /// <summary>
        /// 进入游戏：发送 C2M_InitComplete（漫游消息，经 Gate 路由到 Map）。
        /// 服务器随后会推送自己的单位(M2C_UnitCreate, isSelf)并同步场内其他玩家。
        /// </summary>
        public static void EnterGame()
        {
            if (!IsLoggedIn)
            {
                Log.Error("[Fantasy] 未登录，无法进入游戏");
                return;
            }

            Log.Info("[Fantasy] 进入游戏：发送 C2M_InitComplete，等待服务器推送单位 ...");
            Session.C2M_InitComplete();
            IsInGame = true;
            OnEnteredGame?.Invoke();
        }

        /// <summary>关闭网络：销毁 Scene 会级联清理连接与 Fantasy 功能（不会触发重连）。</summary>
        public static void Shutdown()
        {
            _intentionalClose = true;
            UnitViewManager.Clear();
            Scene?.Dispose();
            Scene = null;
            Session = null;
            IsConnected = false;
            IsLoggedIn = false;
            IsInGame = false;
            _initialized = false;
        }

        /// <summary>
        /// 连通性自检：发送一次 C2G_TestRequest RPC，返回服务器回包的 Tag。仅用于验证链路。
        /// </summary>
        public static async FTask<string> SendTestRpcAsync(string tag = "Hello From TEngine")
        {
            if (!IsConnected)
            {
                return "(未连接)";
            }

            var response = await Session.C2G_TestRequest(tag, new List<byte>());
            return response.Tag;
        }
    }
}
#endif
