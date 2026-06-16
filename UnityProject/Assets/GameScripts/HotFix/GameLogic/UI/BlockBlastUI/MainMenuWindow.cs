using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.BlockBlast;

namespace GameLogic.BlockBlastUI
{
    /// <summary>
    /// 主菜单（玩法融合单入口，设计 29 §3.1）：标题 + 「开始游戏」按钮 → 融合玩法窗口 + 历史最高分。
    /// 经典纯无尽（GameWindow）入口下线，融合主玩法 = 承载完整经济的 MergeOrderWindow。
    /// </summary>
    [Window(UILayer.UI, location: "MainMenuWindow", fullScreen: true)]
    public sealed class MainMenuWindow : UIWindow
    {
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
            UGuiFactory.CreateText(content, "Title", cx, 320, 700, 110, "BLOCK BLAST", 72,
                new Color32(0xff, 0xe0, 0x66, 0xFF));
            UGuiFactory.CreateText(content, "Sub", cx, 410, 700, 50, "合成订单 · 经典无尽融合", 26,
                new Color32(0x88, 0xaa, 0xcc, 0xFF));

            // 「开始游戏」单入口 → 融合玩法（MergeOrderWindow 承载完整经济，设计 29 §3.1）
            var btn = UGuiFactory.CreateButton(content, "BtnStart", cx, 720, 470, 150, "开始游戏", 52,
                new Color32(0x44, 0x77, 0xff, 0xFF), Color.white, out _, out _);
            UGuiFactory.CreateText(content, "StartSub", cx, 790, 470, 40, "落子·消除·合成·订单 · 完成 5 单通关", 22,
                new Color32(0xdd, 0xee, 0xff, 0xFF));
            btn.onClick.AddListener(() =>
            {
                GameModule.UI.CloseUI<MainMenuWindow>();
                GameModule.UI.ShowUIAsync<MergeOrderWindow>();
            });

            // BEST
            UGuiFactory.CreateText(content, "Best", cx, 1020, 470, 50, $"BEST  {state.HighScore}", 32,
                new Color32(0xaa, 0xbb, 0xdd, 0xFF));

            // 设置入口（设计 23 §八 B5：本轮入口落主菜单；玩法 HUD 齿轮入口后续轮次）
            var btnSettings = UGuiFactory.CreateButton(content, "BtnSettings", cx, 1140, 360, 96, "设置", 36,
                new Color32(0x77, 0x88, 0x99, 0xFF), Color.white, out _, out _);
            btnSettings.onClick.AddListener(() =>
            {
                GameModule.UI.ShowUIAsync<GameLogic.UI.SettingsWindow>();
            });

            // 个人信息入口（设计 25 §八 D6：本轮入口落主菜单，照 BtnSettings 做法；玩法 HUD 顶栏头像入口后续轮）
            var btnPlayerInfo = UGuiFactory.CreateButton(content, "BtnPlayerInfo", cx, 1250, 360, 96, "个人信息", 36,
                new Color32(0x99, 0x77, 0x88, 0xFF), Color.white, out _, out _);
            btnPlayerInfo.onClick.AddListener(() =>
            {
                GameModule.UI.ShowUIAsync<GameLogic.UI.PlayerInfoWindow>();
            });

            // 排行榜入口（设计 28 §八：本轮入口落主菜单，照 BtnPlayerInfo 做法；玩法 HUD / 结算窗入口后续轮）
            var btnRank = UGuiFactory.CreateButton(content, "BtnRank", cx, 1360, 360, 96, "排行榜", 36,
                new Color32(0x88, 0x99, 0x77, 0xFF), Color.white, out _, out _);
            btnRank.onClick.AddListener(() =>
            {
                GameModule.UI.ShowUIAsync<GameLogic.UI.RankWindow>();
            });
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
