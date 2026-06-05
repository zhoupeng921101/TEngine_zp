using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.BlockBlast;

namespace GameLogic.BlockBlastUI
{
    /// <summary>
    /// 结算窗口：遮罩 + 卡片（SCORE / BEST / NEW BEST 徽章）+ PLAY AGAIN。
    /// userData[0] = previousHigh（本局开始前的历史最高，用于判定真·破纪录）。
    /// </summary>
    [Window(UILayer.Top, location: "GameOverWindow", fullScreen: true)]
    public sealed class GameOverWindow : UIWindow
    {
        protected override void OnCreate()
        {
            var state = BlockGameState.Instance;
            int previousHigh = UserData is int p ? p : 0;
            int finalScore = state.Score;
            int high = state.HighScore;
            bool isNewBest = finalScore > previousHigh && finalScore > 0;

            var content = UGuiFactory.CreateContentPanel(rectTransform);
            float cx = BlockLayout.DesignWidth / 2f;
            float cardCy = BlockLayout.DesignHeight * 0.42f;

            // 遮罩
            UGuiFactory.CreateImage(content, "Mask", cx, BlockLayout.DesignHeight / 2f,
                BlockLayout.DesignWidth, BlockLayout.DesignHeight, new Color(0, 0, 0.1f, 0.55f));

            // 卡片
            UGuiFactory.CreateImage(content, "Card", cx, cardCy, 560, 600, new Color32(0x22, 0x22, 0x44, 0xF2));

            UGuiFactory.CreateText(content, "Title", cx, cardCy - 230, 500, 70, "GAME OVER", 56,
                new Color32(0xff, 0x88, 0x99, 0xFF));

            if (isNewBest)
            {
                UGuiFactory.CreateText(content, "Badge", cx, cardCy - 160, 500, 50, "★ NEW BEST ★", 32,
                    new Color32(0xff, 0xe0, 0x66, 0xFF));
            }

            UGuiFactory.CreateText(content, "ScoreLabel", cx, cardCy - 90, 400, 40, "SCORE", 26,
                new Color32(0xaa, 0xbb, 0xdd, 0xFF));
            UGuiFactory.CreateText(content, "Score", cx, cardCy - 20, 500, 100, finalScore.ToString(), 92,
                Color.white);

            UGuiFactory.CreateText(content, "BestLabel", cx, cardCy + 90, 400, 35, "BEST", 24,
                new Color32(0xaa, 0xbb, 0xdd, 0xFF));
            UGuiFactory.CreateText(content, "Best", cx, cardCy + 140, 400, 50, high.ToString(), 44,
                isNewBest ? new Color32(0xff, 0xe0, 0x66, 0xFF) : new Color32(0xbb, 0xcc, 0xff, 0xFF));

            // PLAY AGAIN
            var btn = UGuiFactory.CreateButton(content, "BtnAgain", cx, cardCy + 230, 370, 90, "PLAY AGAIN", 38,
                new Color32(0x44, 0x77, 0xff, 0xFF), Color.white, out _, out _);
            btn.onClick.AddListener(() =>
            {
                GameModule.UI.CloseUI<GameOverWindow>();
                GameModule.UI.ShowUIAsync<GameWindow>();
            });

            // 返回菜单
            var menu = UGuiFactory.CreateButton(content, "BtnMenu", cx, cardCy + 330, 300, 60, "Back to Menu", 26,
                new Color(0, 0, 0, 0), new Color32(0x88, 0xaa, 0xcc, 0xFF), out _, out _);
            menu.onClick.AddListener(() =>
            {
                GameModule.UI.CloseUI<GameOverWindow>();
                GameModule.UI.ShowUIAsync<MainMenuWindow>();
            });
        }
    }
}
