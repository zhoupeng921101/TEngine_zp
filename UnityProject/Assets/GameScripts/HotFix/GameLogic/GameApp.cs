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
#if FANTASY_UNITY
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
                attr.ApplyProfile(view.AccountId, view.Nickname, view.Level, view.Exp, view.SchemaVersion);
                attr.ApplySnapshotFull(view.Coin, view.Diamond, view.Stamina,
                    view.SoulPower, view.Piety, view.GuardianExp, view.Energy);
            }
            // 四货币(P2 客户端段):用服务端快照权威值覆盖本地缓存 + 对账器基线 + 已开的玩法态。
            ctx.ApplyServerCurrencySnapshot(view.SoulPower, view.Piety, view.GuardianExp, view.Energy);

            // 云存档下载(P3 客户端段):在 P0 身份确立(OnPlayerIdIssued 已在 LoginAsync 内先触发)、
            // P2 货币快照已应用(上一行)之后再做,避免次序冲突。下载只覆盖局内/标志/权重,保留本地货币 + playerId。
            // 即发即忘:DownloadAndResolve 完成后置 IsReady,此后存档边界上传才放行。
            ctx.CloudSave?.DownloadAndResolve().Forget();
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

            // 云存档节流上传(P3):元层落盘 = 一次「有意义的存档边界」,据此节流批量上传(只搬非货币非身份切片)。
            // 下载未完成(IsReady=false)时 TryUploadThrottled 内部直接 return,不会拿未对齐 version 覆盖云端。
            ctx.CloudSave?.TryUploadThrottled().Forget();
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

        // Block Blast：预热动态权重表（ConfigSystem 懒加载，失败则退化随机），打开主菜单
        try
        {
            GameLogic.Config.WeightCfgConfigMgr.InitDynamicWeight();
        }
        catch (System.Exception e)
        {
            Log.Warning($"[GameApp] 权重表初始化失败，动态难度退化为随机：{e.Message}");
        }
        GameModule.UI.ShowUIAsync<GameLogic.MainMenuWindow>();
    }
    
    private static void Release()
    {
        SingletonSystem.Release();
        Log.Warning("======= Release GameApp =======");
    }
}