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

            // BEST
            UGuiFactory.CreateText(content, "Best", cx, 850, 470, 50, $"BEST  {state.HighScore}", 32,
                new Color32(0xaa, 0xbb, 0xdd, 0xFF));
        }
    }
}
