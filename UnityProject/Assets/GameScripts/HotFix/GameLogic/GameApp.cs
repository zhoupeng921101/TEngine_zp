using System.Collections.Generic;
using System.Reflection;
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
        GameModule.UI.ShowUIAsync<GameLogic.BlockBlastUI.MainMenuWindow>();
    }
    
    private static void Release()
    {
        SingletonSystem.Release();
        Log.Warning("======= Release GameApp =======");
    }
}