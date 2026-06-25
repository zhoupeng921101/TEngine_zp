using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using TEngine;
using GameLogic.BlockBlast;

namespace GameLogic
{
    /// <summary>
    /// 主菜单（玩法融合单入口，设计 29 §3.1）：标题 + 「开始游戏」按钮 → 融合玩法窗口 + 历史最高分。
    /// 经典纯无尽（GameWindow）入口下线，融合主玩法 = 承载完整经济的 MergeOrderWindow。
    /// </summary>
    [Window(UILayer.UI, location: "MainMenuWindow", fullScreen: true)]
    public sealed class MainMenuWindow : UIWindowMono
    {
        /// <summary>进玩法入口等云存档就绪时的看门狗超时(毫秒)。慢网偶发短等;超时按本地兜底放行,绝不卡死。</summary>
        private const int EnterReadyTimeoutMs = 8000;

        /// <summary>进窗防重入。等待就绪期间二次点击「开始游戏」只进窗一次。</summary>
        private bool _entering;

        protected override void OnCreate()
        {
            var state = BlockGameState.Instance;
            state.Load();
            // 最高分权威源 = 元层（设计 29 §5.4）。GameContext.OnInit 启动时已从元层把 HighScore 读进单例；
            // state.Load() 之上的 block_blast_save_v1 键也带 highScore（局内瞬态键的遗产字段），二者取较大值，
            // 迁移期（元层缺省 0 而旧键有历史最高）不抹掉老玩家最高分，新进展由元层落盘接管。
            int metaHigh = LoadMetaHighScore();
            if (metaHigh > state.HighScore) state.HighScore = metaHigh;

            var content = UGuiFactory.CreateContentPanel(rectTransform);
            float cx = BlockLayout.DesignWidth / 2f;

            UGuiFactory.CreateImage(content, "Bg", cx, BlockLayout.DesignHeight / 2f,
                BlockLayout.DesignWidth, BlockLayout.DesignHeight, BlockLayout.BgColor);

            // 标题
            UGuiFactory.CreateText(content, "Title", cx, 461, 1008, 158, "BLOCK BLAST", 104,
                new Color32(0xff, 0xe0, 0x66, 0xFF));
            UGuiFactory.CreateText(content, "Sub", cx, 590, 1008, 72, "合成订单 · 经典无尽融合", 37,
                new Color32(0x88, 0xaa, 0xcc, 0xFF));

            // 「开始游戏」单入口 → 融合玩法（MergeOrderWindow 承载完整经济，设计 29 §3.1）
            var btn = UGuiFactory.CreateButton(content, "BtnStart", cx, 1037, 677, 216, "开始游戏", 75,
                new Color32(0x44, 0x77, 0xff, 0xFF), Color.white, out _, out _);
            UGuiFactory.CreateText(content, "StartSub", cx, 1138, 677, 58, "落子·消除·合成·订单 · 完成 5 单通关", 32,
                new Color32(0xdd, 0xee, 0xff, 0xFF));
            // 进玩法入口闸:进窗前先等云存档下载对齐就绪(常态点按钮时抢跑下载已完成 → 零等待;
            // 慢网偶发才短暂等,期间禁用按钮)。配看门狗超时,超时按本地兜底放行。
            btn.onClick.AddListener(() => EnterMergeOrder(btn).Forget());

            // BEST
            UGuiFactory.CreateText(content, "Best", cx, 1469, 677, 72, $"BEST  {state.HighScore}", 46,
                new Color32(0xaa, 0xbb, 0xdd, 0xFF));

            // 设置入口（设计 23 §八 B5：本轮入口落主菜单；玩法 HUD 齿轮入口后续轮次）
            var btnSettings = UGuiFactory.CreateButton(content, "BtnSettings", cx, 1642, 518, 138, "设置", 52,
                new Color32(0x77, 0x88, 0x99, 0xFF), Color.white, out _, out _);
            btnSettings.onClick.AddListener(() =>
            {
                GameModule.UI.ShowUIAsync<GameLogic.UI.SettingsWindow>();
            });

            // 个人信息入口（设计 25 §八 D6：本轮入口落主菜单，照 BtnSettings 做法；玩法 HUD 顶栏头像入口后续轮）
            var btnPlayerInfo = UGuiFactory.CreateButton(content, "BtnPlayerInfo", cx, 1800, 518, 138, "个人信息", 52,
                new Color32(0x99, 0x77, 0x88, 0xFF), Color.white, out _, out _);
            btnPlayerInfo.onClick.AddListener(() =>
            {
                GameModule.UI.ShowUIAsync<GameLogic.UI.PlayerInfoWindow>();
            });

            // 排行榜入口（设计 28 §八：本轮入口落主菜单，照 BtnPlayerInfo 做法；玩法 HUD / 结算窗入口后续轮）
            var btnRank = UGuiFactory.CreateButton(content, "BtnRank", cx, 1958, 518, 138, "排行榜", 52,
                new Color32(0x88, 0x99, 0x77, 0xFF), Color.white, out _, out _);
            btnRank.onClick.AddListener(() =>
            {
                GameModule.UI.ShowUIAsync<GameLogic.UI.RankWindow>();
            });

            // 服务器配置入口（局域网联调：运行时改 IP / 端口 / 协议并重连，见 ServerConfigWindow）。
            // 置于顶部角落（测试工具），不挤占下方主功能按钮列。
            var btnServer = UGuiFactory.CreateButton(content, "BtnServer", 860, 120, 380, 96, "服务器配置", 40,
                new Color32(0x66, 0x77, 0x99, 0xFF), Color.white, out _, out _);
            btnServer.onClick.AddListener(() =>
            {
                GameModule.UI.ShowUIAsync<GameLogic.UI.ServerConfigWindow>();
            });
        }

        /// <summary>
        /// 「开始游戏」进玩法编排:等云存档就绪(配看门狗超时)→ 关主菜单 + 开融合玩法窗。
        /// 由 Button.onClick 经 .Forget() 调用(等价 async void),故全程 try/catch 兜底、异常不外逃;
        /// _entering 防重入保证等待期二次点击只进窗一次。就绪/超时后再 Close+Show,MergeOrderWindow.OnCreate 读到的本地键已是服务端对齐后投影。
        /// </summary>
        private async UniTaskVoid EnterMergeOrder(Button btn)
        {
            if (_entering) return;
            _entering = true;
            if (btn != null) btn.interactable = false; // 等待期禁用,慢网时给轻量「不可再点」反馈

            try
            {
                var cloud = GameContext.Instance?.CloudSave;
                if (cloud != null && !cloud.IsReady)
                {
                    // 看门狗:就绪与超时谁先到都放行。超时→按本地兜底进入(ResetForMergeOrder 回落本地),绝不卡死。
                    await UniTask.WhenAny(cloud.WhenReady(), UniTask.Delay(EnterReadyTimeoutMs, ignoreTimeScale: true));
                }

                GameModule.UI.CloseUI<MainMenuWindow>();
                GameModule.UI.ShowUIAsync<MergeOrderWindow>();
            }
            catch (System.Exception e)
            {
                // 任何异常都不得让 async void 逃逸崩主菜单:本地兜底放行。
                Log.Warning($"[MainMenuWindow] 进玩法等待云存档就绪异常,按本地兜底放行:{e.Message}");
                GameModule.UI.CloseUI<MainMenuWindow>();
                GameModule.UI.ShowUIAsync<MergeOrderWindow>();
            }
            // 不重置 _entering / 按钮 interactable:成功路径下本窗已 Close 销毁,无需还原。
        }

        /// <summary>
        /// 从元层存档读经典最高分（设计 29 §5.4）。同步经 Persistence.Provider 读（PlayerPrefs 非阻塞内存级，
        /// 不触「禁阻塞 IO」红线，与 GameContext.LoadPlayer / BlockGameState.Load 同口径）。无档 → 0。
        /// </summary>
        private static int LoadMetaHighScore()
        {
            var dto = MergeMetaPersistence.Load();
            return dto != null && dto.highScore > 0 ? dto.highScore : 0;
        }
    }
}
