using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using GameLogic;
#if ENABLE_OBFUZ
using Obfuz;
#endif
using TEngine;
#pragma warning disable CS0436


/// <summary>
/// 游戏App。
/// </summary>
#if ENABLE_OBFUZ
[ObfuzIgnore(ObfuzScope.TypeName | ObfuzScope.MethodName)]
#endif
public partial class GameApp
{
    private static List<Assembly> _hotfixAssembly;

#if FANTASY_UNITY
    // 入口闸（强制联网入口）：登录成功 + 服务端玩家信息快照应用 + 启动期异步预载完成「三者俱备」后，才放行玩法窗。
    // 三信号到达顺序不保证（OnLoggedIn 来自登录 RPC 回包；OnPlayerInfoSnapshot 来自 push；_preloadDone 由 PreloadThenStart 末尾置位），
    // 故各置一标志位、每个信号到达时检查「三者俱备」。进主游戏订单快照对齐尽力而为、不入闸：登录已成功即服务器可达，
    // 对齐失败有本地兜底，不挡门。_mainMenuOpened 守卫确保玩法窗只开一次。
    //
    // _preloadDone 入闸的根由（修复异步预载引入的时序回归）：闸窗（ConnectingWindow）在 StartGameLogic 同步路径立即摆上、
    // 网络登录与预载并行；若登录 + 快照先于预载完成放行、而玩法窗内 widget 此刻尚未预载驻留，WebGL 上 widget 同步加载会报错。
    // 把预载完成纳入闸条件，保证玩法窗只在 widget 必已驻留后才开。
    private static bool _loginSucceeded;
    private static bool _snapshotApplied;
    private static bool _preloadDone;
    private static bool _mainMenuOpened;
#endif

    /// <summary>
    /// 热更域App主入口。
    /// </summary>
    /// <param name="objects"></param>
    public static void Entrance(object[] objects)
    {
        GameEventHelper.Init();
        _hotfixAssembly = (List<Assembly>)objects[0];
        Log.Warning("======= 看到此条日志代表你成功运行了热更新代码 =======");
        Log.Warning("======= Entrance GameApp =======");
        Utility.Unity.AddDestroyListener(Release);
        Log.Warning("======= StartGameLogic =======");
        StartGameLogic();
    }
    
    private static void StartGameLogic()
    {
        // 注册本地持久化默认 provider 工厂(PlayerPrefs):Persistence 入口自身 0 Unity 依赖
        // (PlayerPrefsProvider 隔离在独立文件),Unity 依赖由此处注册侧持有。须早于任何
        // Persistence.Provider 访问(BlockSkinState / DynamicWeightDiff 等),故置 StartGameLogic 起始。
        GameLogic.BlockBlast.Persistence.DefaultProviderFactory =
            () => new GameLogic.BlockBlast.PlayerPrefsProvider();

#if FANTASY_UNITY
        // 重置入口闸标志位:静态字段跨「编辑器内反复 Play」不归零,不重置会让二次进入直接卡住或跳过闸。
        _loginSucceeded = false;
        _snapshotApplied = false;
        _mainMenuOpened = false;

        // 启动 Fantasy 客户端网络：初始化运行时 -> 连接服务器 Gate -> 自动登录。
        // 地址/账号取自 FantasyClient.FantasyNetworkConfig；业务可订阅 FantasyNetwork.OnLoggedIn 进主流程。
        FantasyClient.FantasyNetwork.Boot();

        // 玩家元层属性接线(设计 38 §五):把 Fantasy 推送/快照分发到热更区 PlayerAttrService。
        // 早挂(在 Boot 之后、登录前)保登录快照不丢;事件已在网络主线程触发,可直安全刷视图。
        FantasyClient.FantasyNetwork.OnPlayerInfoSnapshot += view =>
        {
            var ctx = GameLogic.GameContext.Instance;
            var attr = ctx.PlayerAttr;
            if (attr != null)
            {
                // 先档案后七属性:ApplySnapshotFull 末尾置 IsReady=true 并触发 All 事件,
                // 让订阅方在收到事件时档案字段已就绪。
                attr.ApplyProfile(view.AccountId, view.Nickname, view.Level, view.Exp, view.RenameCount, view.SchemaVersion);
                attr.ApplySnapshotFull(view.Coin, view.Diamond, view.Stamina,
                    view.SoulPower, view.Piety, view.GuardianExp, view.Energy);
            }
            // 四货币 + 六计数器(客户端段):用服务端快照权威值覆盖本地缓存 + 对账器基线 + 已开的玩法态。
            ctx.ApplyServerCurrencySnapshot(view.SoulPower, view.Piety, view.GuardianExp, view.Energy,
                view.GoddessLevel, view.GoddessRating, view.UnlockedChapter,
                view.BlindBoxCount, view.TempleRepaired, view.NextRepairIndex);

            // 头像/框(客户端段):用服务端快照当前佩戴 + 解锁集覆盖本地投影 Player;随后 bootstrap 把客户端按等级算出的
            // 应解锁集与服务端集做差、对缺的 id 上报补齐(幂等,只报差集)。bootstrap 即发即忘,不阻塞入口闸;
            // 晚到时头像网格短暂少几个解锁,补报响应回带集合刷新。
            ctx.ApplyServerCosmeticSnapshot(view.CurrentAvatarId, view.CurrentFrameId,
                view.UnlockedAvatarIds, view.UnlockedFrameIds);
            BootstrapCosmeticUnlocks(ctx);

            // 祈愿每日态(客户端段):用服务端快照今日已用次数(懒重置后当日值)覆盖本地投影(缓存 + 已开活态);
            // 客户端 WishUsedToday 降为投影,不再本地跨天重置。今日剩余 = 每日上限 - 已用。
            ctx.ApplyServerWishSnapshot(view.WishUsedToday);

            // 皮肤态 + 神庙装饰(客户端段 3b):用服务端快照三态(是否单色 + 当前单色 id + 已装饰厅数标量)覆盖本地投影
            // (缓存 + 已开活态)。SkinMonoId=-1 表未选(彩色态);TempleDecorated 标量 → 前缀布尔数组。
            // 登录快照是唯一初值源。变更(全清换皮 / 神庙装饰)由 ProfileState 全量 SET 上报,fire-and-forget。
            ctx.ApplyServerProfileStateSnapshot(view.SkinMono, view.SkinMonoId, view.TempleDecorated);

            // 进主游戏订单快照对齐不在登录侧发起:改由进主游戏请求(EnterMainGame)同包回带驱动(决策②每次进入重新对齐)。
            // 接线在 MainMenuWindow「开始游戏」入口闸 → ctx.EnterMainGame.EnterAsync()。

            // 入口闸信号①:服务端玩家信息快照已应用。
            _snapshotApplied = true;
            TryOpenMainMenu();
        };
        FantasyClient.FantasyNetwork.OnPropertyDeltaPush += (type, newAmount, reason) =>
        {
            // PropertyType 整数值与 AttrType 一一映射(Coin=0/Diamond=1/Stamina=2/SoulPower=3/Piety=4/GuardianExp=5/Energy=6)
            var attrType = (GameLogic.BlockBlast.Player.AttrType)type;
            var ctx = GameLogic.GameContext.Instance;
            // Coin/Diamond/Stamina 由 PlayerAttrService 处理;四货币由 MetaCurrencySync 处理(各自 set 对应字段,互不干扰)。
            ctx.PlayerAttr?.ApplyDeltaPush(attrType, newAmount, reason);
            var live = GameLogic.BlockBlast.BlockGameState.Instance;
            var state = (live != null && live.MergeOrderMode) ? live.MergeState : null;
            ctx.MetaCurrency?.ApplyDeltaPush(state, attrType, newAmount);
        };

        // normal 订单服务端权威投影(P1 客户端段):
        // ① 活态就绪/退出钩子 → 交 OrderSync 切权威(置 ServerAuthoritativeOrders)+ 接交付 RPC + 应用已缓存快照
        //    (覆盖 blob 旧 normal 订单)/ 解绑旧活态。开关在 OrderSync.OnMergeStateReady 内置位,无静态、不污染单测。
        // 同钩子内重对齐四货币对账基线(P2):开窗新建 state 后 ImportMeta 从本地缓存读出四货币,而对账基线仍停在
        //   登录快照值(state=null 时只记基线、未随活态对齐)。二者不一致(缓存被清/旧档/离线漂移)时,首次
        //   ReportPending 会把(缓存值-基线)当玩法变更上报、服务端据此冲穿余额(体力重进归零的根因)。把基线钉到
        //   开窗后的活态值,使首刀只上报「本次开窗后的真实增量」。未 Ready(登录快照未到)由 RebindBaseline 内部跳过。
        GameLogic.BlockBlast.BlockGameState.OnMergeStateReady = state =>
        {
            var c = GameLogic.GameContext.Instance;
            c.OrderSync?.OnMergeStateReady(state);
            c.MetaCurrency?.RebindBaseline(state);
            // 服务端权威发牌(M3):把 ServerDealSync 注入玩法态,使发牌入口(开局/落子/补牌)切服务端权威。
            // 实际建局 RPC(C2G_GameStart)由 MergeOrderWindow.OnCreate 发起,本步只接好引用;开窗即清旧局标志,避免读到上局。
            GameLogic.BlockBlast.BlockGameState.Instance.ServerDeal = c.ServerDeal;
            c.ServerDeal?.Close();
        };
        GameLogic.BlockBlast.BlockGameState.OnMergeStateClosed = state =>
        {
            var c = GameLogic.GameContext.Instance;
            c.OrderSync?.OnMergeStateClosed(state);
            c.ServerDeal?.Close();
            GameLogic.BlockBlast.BlockGameState.Instance.ServerDeal = null;
        };
        // ③ 服务端订单快照来源:改由进主游戏请求(EnterMainGame)同包回带 → ctx.EnterMainGame 内部喂 OrderSync.OnSnapshotPush。
        //    登录侧不再单独推订单快照(G2C_MergeOrderSnapshotPush 已退役)。接线在 MainMenuWindow「开始游戏」入口闸。

        // 货币聚合上报钩子(P2 客户端段):每次元层落盘(MergeMetaPersistence.SaveAsync,= 一次玩法事件边界)后,
        // 把四货币本地净变化聚合成一笔上报服务端。钩子注册在接线层(本类),使 MergeMetaPersistence 对货币同步无知。
        GameLogic.BlockBlast.MergeMetaPersistence.OnSaved = () =>
        {
            var ctx = GameLogic.GameContext.Instance;
            var live = GameLogic.BlockBlast.BlockGameState.Instance;

            // 四货币聚合上报(P2):仅 merge-order 现场有效时(MergeState 即四货币活态权威源);窗未开时落盘的是缓存兜底,无活态可对账。
            var sync = ctx.MetaCurrency;
            if (sync != null && live != null && live.MergeOrderMode && live.MergeState != null)
                sync.ReportPending(live.MergeState, "merge_event").Forget();

            // 局内 cosmetic + 合成经济叠加层不再经此存档边界上传:改作不透明切片经落子 / 消除道具搭车上行
            // (C2G_Place/ClearTool 的 SliceJson)由服务端存档、续局回带,故 OnSaved 只保留货币聚合上报。
        };

        // 玩家身份接线(P0 全栈迁移·客户端段):playerId 改以服务端登录签发为权威。
        // 上行:登录前读本地已加载的占位 playerId(GameContext.Player.Id,LoadPlayer 已从存档/新生成填好)上交认领。
        //   provider 是延迟读的 Func,登录回调远晚于此处接线 + GameContext OnInit,Player 必已就绪;空值兜底退空串。
        FantasyClient.FantasyNetwork.LocalPlayerIdProvider = () => GameLogic.GameContext.Instance.Player?.Id;
        // 下行:服务端权威 playerId 覆盖本地内存 + 落盘(仅 ErrorCode==0 且非空触发,见 FantasyNetwork.LoginAsync 契约)。
        FantasyClient.FantasyNetwork.OnPlayerIdIssued += serverPlayerId =>
        {
            GameLogic.GameContext.Instance.ApplyServerPlayerId(serverPlayerId);
        };

        // 入口闸信号②:登录成功(OnLoggedIn 在 OnPlayerIdIssued 之后由登录 RPC 回包触发,ErrorCode==0)。
        FantasyClient.FantasyNetwork.OnLoggedIn += () =>
        {
            _loginSucceeded = true;
            TryOpenMainMenu();
        };
#endif
        // 运行期通用服务上下文：首次 Instance 触发 OnInit（new SettingsService + Load）。
        // 接 AudioSink，把设置开关推到真实音频模块（设计 23 §五；落点在热更入口而非
        // 非热更区 ProcedureLaunch——后者引用不到热更区 GameContext，热更边界所致）。
        var settings = GameContext.Instance.Settings;
        settings.AudioSink = (musicOn, soundOn) =>
        {
            GameModule.Audio.MusicEnable = musicOn;
            GameModule.Audio.SoundEnable = soundOn;
        };
        // 把已加载的态立即应用一次（Apply 为私有，经 SetMusic/SetSound 同值重设触发，等价且不改语义）。
        settings.SetMusic(settings.Audio.MusicOn);
        settings.SetSound(settings.Audio.SoundOn);

        // 闸窗立即摆上（同步路径，不等预载）：网络登录与异步预载并行，登录回调回来时闸窗必已在栈，
        // CloseUI 才能命中（修复回归：闸窗曾被推到 await 之后才 show，导致登录先到时 CloseUI 落空、闸窗后摆且无人关）。
#if FANTASY_UNITY
        ShowConnectingGate();
#else
        // 网络模块未启用(无 Fantasy 栈,无登录流程):退回旧行为直接开主菜单,避免闸永不满足而卡死。
        GameModule.UI.ShowUIAsync<GameLogic.MainMenuWindow>();
#endif

        // 配置 / UI 预制依赖配置的启动尾段移到异步：WebGL 禁止同步加载未驻留 bundle，故先 await 预载
        // 全部配置二进制 + 玩法 UI 预制到内存 / 资源池，并置预载完成信号入闸。
        PreloadThenStart().Forget();
    }

    /// <summary>
    /// 异步启动尾段：先预载配置二进制（ConfigSystem）+ 玩法 UI/特效预制（UIPreloader），
    /// 使玩法窗内同步实例化 widget 全部命中内存缓存 / 资源池，
    /// 在 WebGL 下不触发任何 bundle 同步加载。预载失败不阻断启动（逐项记 Error，尽力放行）。
    /// 末尾置入口闸第三信号 _preloadDone 并触发放行检查：保证玩法窗只在 widget 必已预载驻留后才开。
    /// </summary>
    private static async UniTaskVoid PreloadThenStart()
    {
        try
        {
            await ConfigSystem.Instance.PreloadAsync();
        }
        catch (System.Exception e)
        {
            Log.Error($"[GameApp] 配置预载异常，部分表可能落回同步加载（WebGL 将报错）：{e}");
        }

        try
        {
            await GameLogic.UIPreloader.PreloadGameplayWidgetsAsync();
        }
        catch (System.Exception e)
        {
            Log.Error($"[GameApp] UI 预制预载异常，玩法窗内 widget 可能落回同步加载（WebGL 将报错）：{e}");
        }

        try
        {
            await GameLogic.UIPreloader.PreloadFontsAsync();
        }
        catch (System.Exception e)
        {
            Log.Error($"[GameApp] UI 字体预载异常，UGuiFactory 文本可能落回同步加载（WebGL 将报错、回退内置字体）：{e}");
        }

#if FANTASY_UNITY
        // 入口闸第三信号：预载完成。置位后触发放行检查——若登录 + 快照已先到，此刻补齐预载即放行开玩法窗（widget 已驻留）。
        _preloadDone = true;
        TryOpenMainMenu();
#endif
    }

#if FANTASY_UNITY
    /// <summary>
    /// 摆上连接闸窗（守卫：闸已放行 _mainMenuOpened==true 时不再摆，避免重复闸窗盖死已开的玩法窗且无人关）。
    /// 冷启动与清档软重启都经此摆窗；两处调用前均已复位 _mainMenuOpened=false，正常路径守卫不触发，仅防意外重入。
    /// </summary>
    private static void ShowConnectingGate()
    {
        if (_mainMenuOpened)
        {
            Log.Warning("[GameApp] 闸已放行(_mainMenuOpened),跳过重复摆连接闸窗,避免盖死玩法窗。");
            return;
        }
        GameModule.UI.ShowUIAsync<GameLogic.UI.ConnectingWindow>();
    }

    /// <summary>
    /// 入口闸放行检查:登录成功 + 服务端快照应用 + 启动期预载完成「三者俱备」时,关闭连接闸窗、打开玩法窗(只开一次)。
    /// 由三个独立信号回调各自调用一次;先到者不满足条件直接返回,后到者补齐时放行。
    /// 预载入闸保证玩法窗内 widget 必已预载驻留(WebGL 同步实例化不报错)。
    /// </summary>
    private static void TryOpenMainMenu()
    {
        if (_mainMenuOpened || !_loginSucceeded || !_snapshotApplied || !_preloadDone)
        {
            return;
        }
        _mainMenuOpened = true;
        GameModule.UI.CloseUI<GameLogic.UI.ConnectingWindow>();
        EnterMergeOrder().Forget();
        // GameModule.UI.ShowUIAsync<GameLogic.MainMenuWindow>();
        Log.Info("[GameApp] 入口闸放行:登录成功 + 快照就绪 + 预载完成,打开玩法窗。");
    }
    
    private static async UniTaskVoid EnterMergeOrder()
    {
        try
        {
            var enter = GameContext.Instance?.EnterMainGame;
            if (enter != null)
            {
                // 看门狗:进主游戏响应应用完成与超时谁先到都放行。超时→按本地兜底进入(ResetForMergeOrder 回落本地),绝不卡死。
                // EnterAsync 自带防重入 + 失败降级(请求失败用本地兜底、不清空本地订单),故此处只需配超时。
                await UniTask.WhenAny(enter.EnterAsync(), UniTask.Delay(8000, ignoreTimeScale: true));
            }

            GameModule.UI.CloseUI<MainMenuWindow>();
            GameModule.UI.ShowUIAsync<MergeOrderWindow>();
        }
        catch (System.Exception e)
        {
            // 任何异常都不得让 async void 逃逸崩主菜单:本地兜底放行。
            Log.Warning($"[MainMenuWindow] 进玩法发进主游戏请求异常,按本地兜底放行:{e.Message}");
            GameModule.UI.CloseUI<MainMenuWindow>();
            GameModule.UI.ShowUIAsync<MergeOrderWindow>();
        }
        // 不重置 _entering / 按钮 interactable:成功路径下本窗已 Close 销毁,无需还原。
    }

    /// <summary>
    /// 登录快照到达后触发头像/框解锁 bootstrap(头像服务端权威·客户端段):把客户端按等级算出的应解锁集与服务端
    /// 快照集做差、对缺的 id 上报补齐(<see cref="GameLogic.BlockBlast.Player.CosmeticService.BootstrapUnlocksAsync"/>)。
    /// 即发即忘、不阻塞入口闸;配置未就绪(AvatarConfigMgr.All 抛)时退「只兜默认 id」不崩启动。
    /// </summary>
    private static void BootstrapCosmeticUnlocks(GameLogic.GameContext ctx)
    {
        var player = ctx?.Player;
        var cosmetic = ctx?.Cosmetic;
        if (player == null || cosmetic == null) return;

        System.Collections.Generic.IReadOnlyCollection<GameLogic.BlockBlast.Player.AvatarEntry> all = null;
        try { all = GameLogic.Config.AvatarConfigMgr.All(); }
        catch { all = null; } // 配置未就绪:退 null,BootstrapUnlocksAsync 仍会补默认 id

        cosmetic.BootstrapUnlocksAsync(player, all).Forget();
    }

#endif

#if FANTASY_UNITY
    /// <summary>
    /// 清档后重连重登(清档·客户端段)。服务端玩家数据已清 + 本地玩法投影缓存已清后调用,使客户端从已重置的
    /// 服务端快照重建为新手态,而非沿用旧的内存视图 / 已开窗口。
    ///
    /// 不走整场景重载(SceneManager.LoadScene):本类的网络事件订阅(OnLoggedIn/OnPlayerInfoSnapshot 等)在
    /// StartGameLogic 内以 += 挂载且无解绑,重载会让 StartGameLogic 再跑一遍、订阅翻倍。改为「原地软重启」:
    ///   ① 复位入口闸标志位,使重登后 TryOpenMainMenu 能再次放行、重走进主游戏流程;
    ///   ② 关掉所有已开窗口(含本配置窗 / 玩法窗),由重登后的入口流程重新开;
    ///   ③ 释放持有内存态的轻量单例(GameContext / BlockGameState;DynamicWeightDiff 作为 BlockGameState
    ///      的逐局实例字段随之释放),下次 .Instance 访问时 OnInit 从已清缓存 + 服务端快照重建;
    ///   ④ Shutdown + Boot 重连:复用既有网络事件订阅(不重复挂),重登触发快照覆盖货币 + 入口流程重进。
    /// </summary>
    public static void RestartAfterDataReset()
    {
        Log.Info("[GameApp] 清档后软重启:复位入口闸 + 释放内存单例 + 重连重登。");

        // ① 复位入口闸:静态标志位不归零则重登后 TryOpenMainMenu 永远早返回、不再放行。
        //    _preloadDone 也复位:软重启重跑预载、由 PreloadThenStart 末尾重新置位入闸(否则闸第三信号永缺、永不放行)。
        _loginSucceeded = false;
        _snapshotApplied = false;
        _preloadDone = false;
        _mainMenuOpened = false;

        // ② 关所有窗口:本配置窗 + 玩法窗等由重登后入口流程重开;先关再重启,避免旧窗叠新窗。
        GameModule.UI.CloseAll();

        // ③ 释放持内存态的轻量单例(SimpleSingleton 的静态 _instance 跨场景 / 重连存活,须显式释放才会重建)。
        //    DynamicWeightDiff 现为 BlockGameState 的逐局实例字段,随 BlockGameState 释放一并丢弃,无独立单例可释。
        if (GameLogic.GameContext.IsValid) GameLogic.GameContext.Instance.Release();
        if (GameLogic.BlockBlast.BlockGameState.IsValid) GameLogic.BlockBlast.BlockGameState.Instance.Release();

        // ④ 重连重登:Shutdown 复位静态网络态(_initialized/Scene),Boot 走完整初始化 → 登录 → 快照 → 入口流程。
        //    闸窗立即摆上(同步,与网络登录 + 预载并行);登录 + 快照 + 预载三者俱备前不放行(沿强制联网入口语义)。
        FantasyClient.FantasyNetwork.Shutdown();
        FantasyClient.FantasyNetwork.Boot();
        ShowConnectingGate();

        // ⑤ 重跑预载并重置闸第三信号:配置已预载进字节缓存(幂等去重,直接命中)、widget 预制重新确保驻留资源池
        //    (CloseAll 销毁的是 widget 实例,预制模板由预载的 spawned 注册独立持有;重跑以保证软重启后驻留不依赖该假设),
        //    末尾置 _preloadDone=true + TryOpenMainMenu 放行。
        PreloadThenStart().Forget();
    }
#endif

    private static void Release()
    {
        SingletonSystem.Release();
        Log.Warning("======= Release GameApp =======");
    }
}