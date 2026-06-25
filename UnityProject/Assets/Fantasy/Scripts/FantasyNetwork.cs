#if FANTASY_UNITY
using System;
using System.Collections.Generic;
using Fantasy;
using Fantasy.Async;
using Fantasy.Network;

namespace FantasyClient
{
    /// <summary>
    /// 登录玩家信息快照视图(对应协议 G2C_PlayerInfoSnapshot.Info)。
    /// 承载基础档案(账号/昵称/等级/经验) + 七属性余额(coin/diamond/stamina + 四玩法货币 soul/piety/guardianExp/energy) + schema 版本,
    /// 经 <see cref="FantasyNetwork.OnPlayerInfoSnapshot"/> 一次性下发到热更区订阅方。
    /// 不可变值类型:Fantasy 协议对象用完即回池,跨边界须复制为独立快照避免引用悬空。
    /// </summary>
    public readonly struct PlayerInfoView
    {
        public readonly string AccountId;
        public readonly string Nickname;
        public readonly int Level;
        public readonly long Exp;
        public readonly long Coin;
        public readonly long Diamond;
        public readonly long Stamina;
        // 四玩法货币(P2 全栈迁移·客户端段):服务端权威值,登录快照下发覆盖本地视图。
        public readonly long SoulPower;
        public readonly long Piety;
        public readonly long GuardianExp;
        public readonly long Energy;
        public readonly int SchemaVersion;

        public PlayerInfoView(string accountId, string nickname, int level, long exp,
                              long coin, long diamond, long stamina,
                              long soulPower, long piety, long guardianExp, long energy,
                              int schemaVersion)
        {
            AccountId = accountId;
            Nickname = nickname;
            Level = level;
            Exp = exp;
            Coin = coin;
            Diamond = diamond;
            Stamina = stamina;
            SoulPower = soulPower;
            Piety = piety;
            GuardianExp = guardianExp;
            Energy = energy;
            SchemaVersion = schemaVersion;
        }
    }

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
        /// <summary>已登录的账号名。</summary>
        public static string AccountName { get; private set; }

        /// <summary>连接成功后触发。</summary>
        public static event Action OnConnected;
        /// <summary>登录成功后触发。</summary>
        public static event Action OnLoggedIn;
        /// <summary>连接断开后触发。</summary>
        public static event Action OnDisconnected;

        /// <summary>
        /// 入口登录流程失败时触发(与 <see cref="OnLoggedIn"/> 对称)。参数 = 面向用户的失败原因文案。
        /// 触发时机:①连接服务器失败(<see cref="OnConnectFail"/>);②登录 RPC 回包 ErrorCode≠0(<see cref="LoginAsync"/>)。
        /// 仅覆盖「入口尚未登录成功」阶段;已登录后的会话中途断线由 <see cref="OnDisconnected"/> 处理,不经此事件。
        /// 底层已自带自动重连(<see cref="ScheduleReconnect"/>):连接失败后会按退避自动重连、重连成功后 <see cref="OnConnectComplete"/> 自动重登。
        /// 订阅方据此把入口 UI 切到「重试」态(显示原因 + 手动重试入口),在登录成功前阻断进入主菜单。
        /// </summary>
        public static event Action<string> OnLoginFailed;

        /// <summary>
        /// 服务端登录玩家信息快照到达。参数 = 完整 <see cref="PlayerInfoView"/>(档案 + 三属性 + schema 版本)。
        /// 在主线程 Scene 内触发,业务侧可直接刷 UI;<see cref="G2C_PlayerInfoSnapshotHandler"/> 内置薄壳分发。
        /// </summary>
        public static event Action<PlayerInfoView> OnPlayerInfoSnapshot;

        /// <summary>
        /// 服务端属性变更推送到达(设计 38 §五接线)。参数 = (type, newAmount, reason)。
        /// type 整数值与协议 Fantasy.PropertyType 一致(Coin=0/Diamond=1/Stamina=2)。
        /// </summary>
        public static event Action<int, long, string> OnPropertyDeltaPush;

        /// <summary>
        /// 登录上行的本地 playerId 提供者(P0 全栈迁移·客户端段)。
        /// <see cref="LoginAsync"/> 发 C2G_LoginGameRequest 前读取它填 LocalPlayerId,把本地已持久化的
        /// playerId 上交服务端认领;返回 null/空 → 传空串(新装/无本地值)。
        /// 由热更区(GameApp #if FANTASY_UNITY)接入读取 GameContext.Player.Id,FantasyClient 不反向依赖 GameLogic。
        /// </summary>
        public static Func<string> LocalPlayerIdProvider { get; set; }

        /// <summary>
        /// 服务端签发/认领后的权威 playerId 到达(P0 全栈迁移·客户端段)。仅 ErrorCode==0 且 PlayerId 非空时触发。
        /// 订阅方(GameApp #if FANTASY_UNITY)须用此值覆盖本地权威存储(MergeMetaSave.playerId)并持久化,
        /// 此后会话内一律以服务端值为准。在网络主线程 Scene 内触发,可直接落盘。
        /// </summary>
        public static event Action<string> OnPlayerIdIssued;

        /// <summary>由 <see cref="G2C_PlayerInfoSnapshotHandler"/> 调,把分发交给热更区订阅方(避免 FantasyClient 反向依赖 GameLogic)。</summary>
        internal static void RaisePlayerInfoSnapshot(PlayerInfoView view)
            => OnPlayerInfoSnapshot?.Invoke(view);

        /// <summary>由 <see cref="G2C_PropertyDeltaPushHandler"/> 调,把分发交给热更区订阅方。</summary>
        internal static void RaisePropertyDeltaPush(int type, long newAmount, string reason)
            => OnPropertyDeltaPush?.Invoke(type, newAmount, reason);

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
            Log.Info($"[Fantasy] 连接服务器 {_address} ({FantasyNetworkConfig.Protocol}) ...");
            Session = Scene.Connect(
                _address,
                FantasyNetworkConfig.Protocol,
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
            Log.Error($"[Fantasy] ❌ 连接服务器失败：请确认服务器已启动且 Gate({FantasyNetworkConfig.Protocol} {_address}) 可达。");
            // 入口失败通知:让入口 UI 切到重试态并阻断进入(底层 ScheduleReconnect 同时驱动自动重连)。
            OnLoginFailed?.Invoke($"连接服务器失败\n（{FantasyNetworkConfig.Protocol} {_address}）");
            ScheduleReconnect();
        }

        private static void OnConnectDisconnect()
        {
            IsConnected = false;
            IsLoggedIn = false;
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

            // 复用已有 Scene 重新建立连接；成功后 OnConnectComplete 会自动重登。
            Connect();
        }

        /// <summary>
        /// 登录：发送 C2G_LoginGameRequest（带本地 playerId 上交认领），返回服务器错误码（0 表示成功）。
        /// 上行 LocalPlayerId 取自 <see cref="LocalPlayerIdProvider"/>（本地占位值，无则空串）；
        /// 成功后置位 <see cref="IsLoggedIn"/>、用服务端 PlayerId 触发 <see cref="OnPlayerIdIssued"/>（订阅方覆盖本地落地），再触发 <see cref="OnLoggedIn"/>。
        /// </summary>
        public static async FTask<uint> LoginAsync(string accountName)
        {
            if (!IsConnected)
            {
                Log.Error("[Fantasy] 未连接，无法登录");
                return uint.MaxValue;
            }

            // 本地已持久化的 playerId 作为认领候选上交；无 provider / 无本地值 → 空串（新装首登）。
            string localPlayerId = LocalPlayerIdProvider?.Invoke() ?? string.Empty;
            Log.Info($"[Fantasy] 登录中 account={accountName} localPlayerId={(string.IsNullOrEmpty(localPlayerId) ? "(空)" : localPlayerId)} ...");
            var response = await Session.C2G_LoginGameRequest(accountName, localPlayerId);
            if (response.ErrorCode != 0)
            {
                Log.Error($"[Fantasy] ❌ 登录失败 ErrorCode={response.ErrorCode}");
                // 入口失败通知:登录被服务端拒绝(连接仍在),让入口 UI 切到重试态、阻断进入。
                // 此路径无自动重连(自动重连只覆盖连接断开),需用户经 UI 手动 RetryLogin。
                OnLoginFailed?.Invoke($"登录被拒绝（错误码 {response.ErrorCode}）");
                return response.ErrorCode;
            }

            IsLoggedIn = true;
            AccountName = accountName;
            // 服务端签发/认领的权威 playerId：ErrorCode==0 时保证非空，落地交订阅方覆盖本地（设计契约）。
            // 非空守卫兜底极端协议异常，不拿空值覆盖本地占位值。
            if (!string.IsNullOrEmpty(response.PlayerId))
            {
                Log.Info($"[Fantasy] 服务端权威 playerId={response.PlayerId}");
                OnPlayerIdIssued?.Invoke(response.PlayerId);
            }
            else
            {
                Log.Warning("[Fantasy] 登录成功但服务端 PlayerId 为空，保留本地占位值不覆盖。");
            }
            Log.Info($"[Fantasy] ✅ 登录成功 account={accountName}");
            OnLoggedIn?.Invoke();
            return 0;
        }

        /// <summary>
        /// 手动重试入口登录(供入口重试 UI 调用)。按当前连接态选路:
        /// 未连接 → 重新发起连接(<see cref="Connect"/>),连上后 <see cref="OnConnectComplete"/> 自动重登;
        /// 已连接但未登录 → 直接重发登录 RPC。已登录则忽略(入口已通过)。
        /// 与底层自动重连不冲突:_intentionalClose 仍为 false,二者最终都汇入同一登录流程。
        /// </summary>
        public static void RetryLogin()
        {
            if (IsLoggedIn)
            {
                return;
            }
            if (IsConnected)
            {
                if (!string.IsNullOrEmpty(_account))
                {
                    LoginAsync(_account).Coroutine();
                }
                return;
            }
            // 未连接:重置重连计数后立即重连(connectFail 兜底仍会按退避自动重连)。
            _reconnectAttempt = 0;
            Connect();
        }

        /// <summary>关闭网络：销毁 Scene 会级联清理连接与 Fantasy 功能（不会触发重连）。</summary>
        public static void Shutdown()
        {
            _intentionalClose = true;
            Scene?.Dispose();
            Scene = null;
            Session = null;
            IsConnected = false;
            IsLoggedIn = false;
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
