using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.BlockBlast;

namespace GameLogic.BlockBlastUI
{
    /// <summary>
    /// 合成订单通关面板：半透明遮罩 + 木质大面板 + 标题木牌 + 结算行列表 + 再来一局/返回按钮。
    /// UserData = List&lt;string&gt;（结算行，由 MergeOrderWindow.cs:718 传入）。弹出即锁输入。
    /// 视觉：塔罗木质风格（Sheet_settings 子图），对位效果图「恭喜通关.png」。
    /// 逻辑（UserData 解析 / 结算行渲染 / 回调目标）保留不动（零回归约束）。
    /// </summary>
    [Window(UILayer.Top, location: "MergeOrderWinWindow", fullScreen: true)]
    public sealed class MergeOrderWinWindow : UIWindow
    {
        // 子图取自 Sheet_settings.png（Multiple 精灵表，设计 23 已打通，SetSubSprite 可寻址）
        private const string Atlas = "Sheet_settings";

        protected override void OnCreate()
        {
            // ── 结算逻辑：不动（零回归 R3）──────────────────────────────────────
            var lines = UserData as List<string> ?? new List<string>();
            // ─────────────────────────────────────────────────────────────────────

            var content = UGuiFactory.CreateContentPanel(rectTransform);
            float cx = BlockLayout.DesignWidth / 2f;
            float cardCy = BlockLayout.DesignHeight * 0.42f;

            // 遮罩（纯色，不挂 Button — 通关窗无遮罩关窗，强制选择再来一局/返回）
            UGuiFactory.CreateImage(content, "Mask", cx, BlockLayout.DesignHeight / 2f,
                BlockLayout.DesignWidth, BlockLayout.DesignHeight, new Color(0, 0, 0, 0.6f));

            // 主面板：box1（大木板）；Color.white 让木纹原色透出
            var card = UGuiFactory.CreateImage(content, "Card", cx, cardCy, 560, 640, Color.white);
            card.SetSubSprite(Atlas, "box1");

            // 标题木牌底：box2（小木牌），叠在面板上沿
            var titleBg = UGuiFactory.CreateImage(content, "TitleBg", cx, cardCy - 270, 480, 100, Color.white);
            titleBg.SetSubSprite(Atlas, "box2");

            // 标题文字「恭喜通关」（深棕，对位效果图木质风格）
            UGuiFactory.CreateText(content, "Title", cx, cardCy - 270, 440, 80, "恭喜通关", 48,
                new Color32(0x5a, 0x2e, 0x10, 0xFF));

            // 结算行列表（UserData as List<string> 解析 + 逐行渲染，逻辑不动 — 零回归 R3）
            float listY = cardCy - 140;
            for (int i = 0; i < lines.Count; i++)
            {
                UGuiFactory.CreateText(content, $"Line_{i}", cx, listY + i * 72, 500, 60, lines[i], 40,
                    new Color32(0x3a, 0x1a, 0x00, 0xFF));
            }

            // 再来一局按钮（button 子图；onClick 回调不动 — 零回归 R4：再来一局→MergeOrderWindow，重入即 ResetForMergeOrder）
            var again = UGuiFactory.CreateButton(content, "BtnAgain", cx, cardCy + 210, 400, 90,
                "再来一局", 38, Color.white, new Color32(0x5a, 0x2e, 0x10, 0xFF), out var againBg, out _);
            againBg.SetSubSprite(Atlas, "button");
            again.onClick.AddListener(() =>
            {
                GameModule.UI.CloseUI<MergeOrderWinWindow>();
                GameModule.UI.ShowUIAsync<MergeOrderWindow>(); // 重新进入即 ResetForMergeOrder
            });

            // 返回按钮（button 子图；onClick 回调不动 — 零回归 R4：返回→MainMenuWindow）
            var menu = UGuiFactory.CreateButton(content, "BtnMenu", cx, cardCy + 315, 400, 80,
                "返回", 32, Color.white, new Color32(0x5a, 0x2e, 0x10, 0xFF), out var menuBg, out _);
            menuBg.SetSubSprite(Atlas, "button");
            menu.onClick.AddListener(() =>
            {
                GameModule.UI.CloseUI<MergeOrderWinWindow>();
                GameModule.UI.ShowUIAsync<MainMenuWindow>();
            });
        }
    }
}
