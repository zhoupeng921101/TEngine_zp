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
    // _preloadDone 入闸的根由（修复异步预载引入的时序回归）：闸窗（UIConnectingPanel）在 StartGameLogic 同步路径立即摆上、
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

        // Fantasy 客户端网络的启动(Boot)不在此处无条件发起:登录改为账号驱动,由启动尾段按「本地是否已存账号」
        // 决定自动登录还是先出登录窗(见下方入口分流)。事件订阅须先于任一登录发起完成,故全部集中在 Boot 之前挂好。

        // 玩家元层属性接线(设计 38 §五):把 Fantasy 推送/快照分发到热更区 PlayerAttrService。
        // 先于任何登录发起挂好,保登录快照不丢;事件已在网络主线程触发,可直安全刷视图。
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

            // 背包(背包系统·客户端段):用服务端快照两轨全量覆盖投影(loaded 门控三态)——堆叠轨→Items、批次轨→Inventory,
            // 服务端时刻锚定批次倒计时基准。降级空占位(loaded=false)保留本地投影不清空。
            ApplyServerInventorySnapshot(ctx, view.Inventory);

            // 进主游戏订单快照对齐不在登录侧发起:改由进主游戏请求(EnterMainGame)同包回带驱动(决策②每次进入重新对齐)。
            // 接线在入口闸放行处(TryOpenMainMenu → EnterMergeOrder) → ctx.EnterMainGame.EnterAsync()。

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

        // 背包推送(背包系统·客户端段):获得 / 使用 / 过期后服务端起推整份背包,整份覆盖两轨投影(loaded 门控三态)。
        FantasyClient.FantasyNetwork.OnInventoryDeltaPush += inv =>
        {
            ApplyServerInventorySnapshot(GameLogic.GameContext.Instance, inv);
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
            // 实际建局 RPC(C2G_GameStart)由 UIMergeOrderPanel.OnCreate 发起,本步只接好引用;开窗即清旧局标志,避免读到上局。
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
        //    登录侧不再单独推订单快照(G2C_MergeOrderSnapshotPush 已退役)。接线在入口闸放行处(EnterMergeOrder)。

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
            // 保存登录账号供下次启动自动登录(账号名为可丢便捷缓存、非权威;权威 playerId 由服务端签发)。
            GameLogic.LoginAccountStore.Save(FantasyClient.FantasyNetwork.AccountName);
            _loginSucceeded = true;
            TryOpenMainMenu();
        };

        // 入口登录失败(连不上/超时/账号被服务端拒):回退到登录窗让用户重输账号重试。
        // 仅入口阶段处理(_mainMenuOpened==false,尚未放行进游戏);已进游戏后的中途登录失败不打断,
        // 维持底层自动重连语义。先 Shutdown 停后台自动重连——否则用户停留登录窗期间后台重连+重登可能悄悄成功、
        // 绕过登录窗直接放行。再关闸窗、开登录窗并回带失败原因。
        FantasyClient.FantasyNetwork.OnLoginFailed += reason =>
        {
            if (_mainMenuOpened)
            {
                return;
            }
            FantasyClient.FantasyNetwork.Shutdown();
            GameModule.UI.CloseUI<GameLogic.UI.UIConnectingPanel>();
            GameModule.UI.ShowUIAsync<GameLogic.UILoginPanel>(reason);
        };
#endif
        // 启动尾段(GameContext 初始化 → 音频设置 → 入口分流登录/闸窗 → UI 预制/字体预载)整体移入异步:
        // 首个配置消费者 GameContext.OnInit→LoadPlayer 读 avatar 配置,须先 await 配置字节预载再触发 GameContext.Instance;
        // 配置 await 在发起登录之前完成,登录回调必晚于闸窗摆上(无 CloseUI 落空)。见 PreloadConfigThenStart。
        PreloadConfigThenStart().Forget();
    }

    /// <summary>
    /// 异步启动尾段(时序:配置 → GameContext → 登录 → UI 预载)：
    /// ① 先 await 全部配置表字节预载(WebGL 禁运行时同步加载未驻留 bundle),使最早的配置消费者
    ///    (GameContext.OnInit→LoadPlayer 读 avatar、登录快照回调 BootstrapCosmeticUnlocks 读 avatar)命中预载字节;
    /// ② 配置就绪后首次访问 GameContext 触发 OnInit,LoadPlayer 的头像校验真正生效;接 AudioSink 推音频设置;
    /// ③ 入口分流发起登录 / 摆闸窗——配置 await 已在此之前完成,登录回调必晚于闸窗摆上(无 CloseUI 落空);
    /// ④ 预载玩法 UI/特效预制与字体(与登录并行),末尾置入口闸第三信号 _preloadDone 并触发放行检查。
    /// 各步失败不阻断启动(逐项记 Error,尽力放行)。
    /// </summary>
    private static async UniTaskVoid PreloadConfigThenStart()
    {
        // ① 配置字节预载:提到首个配置消费者(GameContext.OnInit 读 avatar)与登录发起之前。
        // WebGL 禁运行时同步加载未驻留 bundle,各表首次访问须命中预载字节。
        try
        {
            ConfigSystem.Instance.Load();
            await ConfigSystem.Instance.PreloadAsync();
        }
        catch (System.Exception e)
        {
            Log.Error($"[GameApp] 配置构建异常：{e}");
        }

        // ② 运行期通用服务上下文：首次 Instance 触发 OnInit（new SettingsService + Load + LoadPlayer；
        // 此刻 avatar 配置已就绪,LoadPlayer 的头像 id 校验真正生效）。接 AudioSink，把设置开关推到真实音频模块
        //（设计 23 §五；落点在热更入口而非非热更区 ProcedureLaunch——后者引用不到热更区 GameContext，热更边界所致）。
        var settings = GameContext.Instance.Settings;
        settings.AudioSink = (musicOn, soundOn) =>
        {
            GameModule.Audio.MusicEnable = musicOn;
            GameModule.Audio.SoundEnable = soundOn;
        };
        // 把已加载的态立即应用一次（Apply 为私有，经 SetMusic/SetSound 同值重设触发，等价且不改语义）。
        settings.SetMusic(settings.Audio.MusicOn);
        settings.SetSound(settings.Audio.SoundOn);

        // ③ 入口分流(账号驱动登录):本地已存账号 → 直接用它自动登录 + 立即摆闸窗;无 → 先出登录窗由用户输入账号,
        // 点登录才发起(见 BeginLogin)。Boot 与 ShowConnectingGate 同步紧邻(其间无 await),登录回调回来时闸窗必已在栈,
        // CloseUI 才能命中(配置预载已在 ① await 完,登录发起晚于闸窗摆上,无 CloseUI 落空)。
#if FANTASY_UNITY
        string savedAccount = GameLogic.LoginAccountStore.Get();
        if (!string.IsNullOrEmpty(savedAccount))
        {
            // 已存账号 → 自动登录:闸窗等待,登录成功放行;失败由 OnLoginFailed 回退登录窗(预填该账号供重试)。
            FantasyClient.FantasyNetwork.Boot(null, savedAccount);
            ShowConnectingGate();
        }
        else
        {
            // 无已存账号(首次)→ 先出登录窗,不发起 Boot;由用户输入账号点登录经 BeginLogin 发起。
            GameModule.UI.ShowUIAsync<GameLogic.UILoginPanel>();
        }
#else
        // 网络模块未启用(无 Fantasy 栈,无登录流程):直接开玩法窗,避免闸永不满足而卡死。
        GameModule.UI.ShowUIAsync<GameLogic.UIMergeOrderPanel>();
#endif

        // ④ 玩法 UI 预制 + 字体预载 + 置放行信号:复用 PreloadThenStart(与清档软重启 RestartAfterDataReset 同款尾段;
        // 其内配置预载对本路径为幂等缓存命中,不重复下载)。与登录并行。
        PreloadThenStart().Forget();
    }

    /// <summary>
    /// 预载尾段(配置幂等 + 玩法 UI/特效预制 + 字体)：让玩法窗内同步实例化的 widget 命中资源池、
    /// UGuiFactory 文本命中已驻留字体。各步失败不阻断启动（逐项记 Error，尽力放行）。末尾置入口闸第三信号
    /// _preloadDone 并触发放行检查：保证玩法窗只在预制必已驻留后才开。
    /// 主入口尾段(<see cref="PreloadConfigThenStart"/> ④)与清档软重启(<see cref="RestartAfterDataReset"/>)共用:
    /// 主入口已在更前面单独 await 过配置(供 GameContext.OnInit 读 avatar 命中),软重启时配置缓存跨重启存活,
    /// 故此处配置预载对两路径均为幂等缓存命中。
    /// </summary>
    private static async UniTaskVoid PreloadThenStart()
    {
        try
        {
            ConfigSystem.Instance.Load();
            await ConfigSystem.Instance.PreloadAsync();
        }
        catch (System.Exception e)
        {
            Log.Error($"[GameApp] 配置构建异常：{e}");
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

        try
        {
            await GameLogic.UIPreloader.PreloadShadersAsync();
        }
        catch (System.Exception e)
        {
            Log.Error($"[GameApp] UI shader 预载异常，玩法窗建材质取不到 shader（WebGL 下 Shader.Find 失效、去色/辉光将退化）：{e}");
        }

#if FANTASY_UNITY
        // 入口闸第三信号：预载完成。置位后触发放行检查——若登录 + 快照已先到，此刻补齐预载即放行开玩法窗（widget 已驻留）。
        _preloadDone = true;
        TryOpenMainMenu();
#endif
    }

#if FANTASY_UNITY
    /// <summary>
    /// 登录窗提交入口:用输入账号发起一次入口登录。teardown+reboot(Shutdown → Boot,与 <see cref="RestartAfterDataReset"/>
    /// 同款、已验证的安全范式)清掉任何半开连接 / 后台重连再从头连,规避重复连接叠加。复位入口闸的登录 / 快照 / 放行标志
    /// (不动 _preloadDone:预载在启动尾段已完成且与网络无关,重置会让闸第三信号永缺、永不放行),摆连接闸窗等待。
    /// 成功由 TryOpenMainMenu 放行进游戏,失败由 OnLoginFailed 回退登录窗。
    /// </summary>
    public static void BeginLogin(string account)
    {
        _loginSucceeded = false;
        _snapshotApplied = false;
        _mainMenuOpened = false;
        FantasyClient.FantasyNetwork.Shutdown();
        FantasyClient.FantasyNetwork.Boot(null, account);
        ShowConnectingGate();
    }

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
        GameModule.UI.ShowUIAsync<GameLogic.UI.UIConnectingPanel>();
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
        GameModule.UI.CloseUI<GameLogic.UI.UIConnectingPanel>();
        EnterMergeOrder().Forget();
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

            GameModule.UI.ShowUIAsync<UIMainMenuPanel>();
        }
        catch (System.Exception e)
        {
            // 任何异常都不得让 async void 逃逸崩启动:本地兜底放行进玩法。
            Log.Warning($"[GameApp] 进玩法发进主游戏请求异常,按本地兜底放行:{e.Message}");
            GameModule.UI.ShowUIAsync<UIMainMenuPanel>();
        }
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

    /// <summary>
    /// 应用服务端背包整份快照 / 推送到两轨投影(背包系统·客户端段)。
    /// loaded=false(服务端读库降级的空占位)→ 保留本地投影不清空(三态防误清);loaded=true → 整份覆盖:
    /// 堆叠轨 Holdings→<see cref="GameLogic.BlockBlast.Item.ItemBag"/>(服务端未含 id 被清)、批次轨 Lots→<see cref="GameLogic.BlockBlast.Player.InventoryService"/>,
    /// ServerNowMs 锚定批次倒计时基准。跨边界视图已是独立值类型,直接读用。
    /// </summary>
    private static void ApplyServerInventorySnapshot(GameLogic.GameContext ctx, FantasyClient.InventorySnapshotView inv)
    {
        if (ctx == null || !inv.Loaded)
        {
            return; // 三态:降级空占位不覆盖、不清空。
        }

        // 堆叠轨 → ItemBag(整份权威覆盖)。
        var items = ctx.Items;
        if (items != null)
        {
            var holdings = new System.Collections.Generic.List<(int, long)>(inv.Holdings?.Length ?? 0);
            if (inv.Holdings != null)
            {
                foreach (var h in inv.Holdings) holdings.Add((h.ItemId, h.Count));
            }
            items.ApplyAuthoritativeSnapshot(holdings);
        }

        // 批次轨 → InventoryService(整份覆盖 + 锚定服务端时间基准)。
        var inventory = ctx.Inventory;
        if (inventory != null)
        {
            var lots = new System.Collections.Generic.List<GameLogic.BlockBlast.Player.InventoryLot>(inv.Lots?.Length ?? 0);
            if (inv.Lots != null)
            {
                foreach (var l in inv.Lots)
                {
                    lots.Add(new GameLogic.BlockBlast.Player.InventoryLot(l.LotId, l.ItemId, l.Count, l.AcquireMs, l.ExpireMs));
                }
            }
            inventory.ApplyLotsSnapshot(lots, inv.ServerNowMs, true);
            // 登录 seed 使用幂等锚底(推送 LastUseReqSeq=0 时 no-op):保重登后首个使用序号 > 服务端已处理值。
            inventory.SeedReqSeq(inv.LastUseReqSeq);
        }
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
        //    用当前账号重登(而非配置的设备默认账号):优先内存已登录账号,回退本地已存账号——清档只重置玩家数据、
        //    不改账号身份,故仍登同一账号。闸窗立即摆上(同步,与网络登录 + 预载并行);三信号俱备前不放行(沿强制联网入口语义)。
        string reloginAccount = FantasyClient.FantasyNetwork.AccountName;
        if (string.IsNullOrEmpty(reloginAccount))
        {
            reloginAccount = GameLogic.LoginAccountStore.Get();
        }
        FantasyClient.FantasyNetwork.Shutdown();
        FantasyClient.FantasyNetwork.Boot(null, reloginAccount);
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