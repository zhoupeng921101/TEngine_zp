using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.BlockBlast;

namespace GameLogic.BlockBlastUI
{
    /// <summary>
    /// 主菜单（Classic 单模式）：标题 + CLASSIC 开始按钮 + 历史最高分。
    /// </summary>
    [Window(UILayer.UI, location: "MainMenuWindow", fullScreen: true)]
    public sealed class MainMenuWindow : UIWindow
    {
        protected override void OnCreate()
        {
            var state = BlockGameState.Instance;
            state.Load();

            var content = UGuiFactory.CreateContentPanel(rectTransform);
            float cx = BlockLayout.DesignWidth / 2f;

            UGuiFactory.CreateImage(content, "Bg", cx, BlockLayout.DesignHeight / 2f,
                BlockLayout.DesignWidth, BlockLayout.DesignHeight, BlockLayout.BgColor);

            // 标题
            UGuiFactory.CreateText(content, "Title", cx, 320, 700, 110, "BLOCK BLAST", 72,
                new Color32(0xff, 0xe0, 0x66, 0xFF));
            UGuiFactory.CreateText(content, "Sub", cx, 410, 700, 50, "Classic 复刻 v1", 26,
                new Color32(0x88, 0xaa, 0xcc, 0xFF));

            // CLASSIC 按钮
            var btn = UGuiFactory.CreateButton(content, "BtnClassic", cx, 680, 470, 140, "CLASSIC", 48,
                new Color32(0x44, 0x77, 0xff, 0xFF), Color.white, out _, out _);
            UGuiFactory.CreateText(content, "ClassicSub", cx, 740, 470, 40, "无尽模式 · 挑战最高分", 22,
                new Color32(0xdd, 0xee, 0xff, 0xFF));
            btn.onClick.AddListener(() =>
            {
                GameModule.UI.CloseUI<MainMenuWindow>();
                GameModule.UI.ShowUIAsync<GameWindow>();
            });

            // 合成订单 Demo 入口
            var btnMerge = UGuiFactory.CreateButton(content, "BtnMerge", cx, 870, 470, 110, "合成订单 DEMO", 36,
                new Color32(0xcc, 0x77, 0x33, 0xFF), Color.white, out _, out _);
            UGuiFactory.CreateText(content, "MergeSub", cx, 920, 470, 40, "体力·合成·订单切片 · 完成 5 单通关", 22,
                new Color32(0xff, 0xee, 0xdd, 0xFF));
            btnMerge.onClick.AddListener(() =>
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
    }
}
