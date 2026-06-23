using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.BlockBlast;

namespace GameLogic
{
    /// <summary>
    /// 结算窗口（游戏结束）：半透明遮罩 + 木质大面板 + 标题木牌 + 分数 + 重试/返回按钮。
    /// userData = previousHigh（本局开始前的历史最高，int；融合窗口路径传 0）。
    /// 视觉：塔罗木质风格（Sheet_settings 子图），对位效果图「游戏结束.png」。
    /// 结算逻辑（UserData 解析 / finalScore 计算）零回归不动；重试回调融合后改指融合窗口（设计 29 §3.1）。
    /// </summary>
    [Window(UILayer.Top, location: "GameOverWindow", fullScreen: true)]
    public sealed class GameOverWindow : UIWindow
    {
        // 子图取自 Sheet_settings.png（Multiple 精灵表，设计 23 已打通，SetSubSprite 可寻址）
        private const string Atlas = "Sheet_settings";

        protected override void OnCreate()
        {
            // ── 结算逻辑：不动（零回归 R1/R2）──────────────────────────────────
            var state = BlockGameState.Instance;
            int previousHigh = UserData is int p ? p : 0;
            int finalScore = state.Score;
            int high = state.HighScore;
            bool isNewBest = finalScore > previousHigh && finalScore > 0;
            // ─────────────────────────────────────────────────────────────────────

            var content = UGuiFactory.CreateContentPanel(rectTransform);
            float cx = BlockLayout.DesignWidth / 2f;
            float cardCy = BlockLayout.DesignHeight * 0.42f;

            // 遮罩（纯色，不挂 Button — 结算无遮罩关窗，强制选择重试/返回）
            UGuiFactory.CreateImage(content, "Mask", cx, BlockLayout.DesignHeight / 2f,
                BlockLayout.DesignWidth, BlockLayout.DesignHeight, new Color(0, 0, 0, 0.6f));

            // 主面板：box1（大木板）；Color.white 让木纹原色透出
            var card = UGuiFactory.CreateImage(content, "Card", cx, cardCy, 806, 893, Color.white);
            card.SetSubSprite(Atlas, "box1");

            // 标题木牌底：box2（小木牌），叠在面板上沿
            var titleBg = UGuiFactory.CreateImage(content, "TitleBg", cx, cardCy - 374, 691, 144, Color.white);
            titleBg.SetSubSprite(Atlas, "box2");

            // 标题文字「游戏结束」（深棕，对位效果图木质风格）
            UGuiFactory.CreateText(content, "Title", cx, cardCy - 374, 634, 115, "游戏结束", 75,
                new Color32(0x5a, 0x2e, 0x10, 0xFF));

            // 新纪录徽章（条件显示，isNewBest 判定逻辑不动）
            if (isNewBest)
            {
                UGuiFactory.CreateText(content, "Badge", cx, cardCy - 245, 691, 72, "★ 新纪录 ★", 43,
                    new Color32(0xff, 0xe0, 0x66, 0xFF));
            }

            // 分数区（从 BlockGameState 取值，逻辑不动 — 零回归 R1/R2）
            UGuiFactory.CreateText(content, "ScoreLabel", cx, cardCy - 144, 576, 58, "得分", 37,
                new Color32(0x8a, 0x5a, 0x30, 0xFF));
            UGuiFactory.CreateText(content, "Score", cx, cardCy - 36, 720, 144, finalScore.ToString(), 127,
                new Color32(0x3a, 0x1a, 0x00, 0xFF));

            // 最高分区（isNewBest 变色逻辑不动）
            UGuiFactory.CreateText(content, "BestLabel", cx, cardCy + 122, 576, 50, "最高分", 35,
                new Color32(0x8a, 0x5a, 0x30, 0xFF));
            UGuiFactory.CreateText(content, "Best", cx, cardCy + 194, 576, 72, high.ToString(), 60,
                isNewBest ? new Color32(0xff, 0xe0, 0x66, 0xFF) : new Color32(0x5a, 0x3a, 0x10, 0xFF));

            // 重试按钮（button 子图）。融合后（设计 29 §3.1）GameOver 仅由融合窗口触发，
            // 重试回融合玩法窗口 MergeOrderWindow（经典纯无尽入口已下线），而非经典 GameWindow。
            var btn = UGuiFactory.CreateButton(content, "BtnAgain", cx, cardCy + 317, 576, 130,
                "重试", 58, Color.white, new Color32(0x5a, 0x2e, 0x10, 0xFF), out var btnBg, out _);
            btnBg.SetSubSprite(Atlas, "button");
            btn.onClick.AddListener(() =>
            {
                GameModule.UI.CloseUI<GameOverWindow>();
                GameModule.UI.ShowUIAsync<MergeOrderWindow>();
            });

            // 返回按钮（button 子图；onClick 回调不动 — 零回归 R4：返回→MainMenuWindow）
            var menu = UGuiFactory.CreateButton(content, "BtnMenu", cx, cardCy + 468, 576, 115,
                "返回", 49, Color.white, new Color32(0x5a, 0x2e, 0x10, 0xFF), out var menuBg, out _);
            menuBg.SetSubSprite(Atlas, "button");
            menu.onClick.AddListener(() =>
            {
                GameModule.UI.CloseUI<GameOverWindow>();
                GameModule.UI.ShowUIAsync<MainMenuWindow>();
            });
        }
    }
}
