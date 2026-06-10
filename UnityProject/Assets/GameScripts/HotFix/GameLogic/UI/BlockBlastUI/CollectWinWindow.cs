using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.BlockBlast;

namespace GameLogic.BlockBlastUI
{
    /// <summary>
    /// 收集 Demo 胜利面板：遮罩 + 卡片（「收集完成！」+ 各目标达成列表）+ 再来一局 / 返回主菜单。
    /// UserData = List&lt;string&gt;（各目标达成行，由 CollectDemoWindow 快照传入）。弹出即锁输入。
    /// </summary>
    [Window(UILayer.Top, location: "CollectWinWindow", fullScreen: true)]
    public sealed class CollectWinWindow : UIWindow
    {
        protected override void OnCreate()
        {
            var lines = UserData as List<string> ?? new List<string>();

            var content = UGuiFactory.CreateContentPanel(rectTransform);
            float cx = BlockLayout.DesignWidth / 2f;
            float cardCy = BlockLayout.DesignHeight * 0.42f;

            // 遮罩
            UGuiFactory.CreateImage(content, "Mask", cx, BlockLayout.DesignHeight / 2f,
                BlockLayout.DesignWidth, BlockLayout.DesignHeight, new Color(0, 0.05f, 0.02f, 0.55f));

            // 卡片
            UGuiFactory.CreateImage(content, "Card", cx, cardCy, 560, 640, new Color32(0x1e, 0x3a, 0x2a, 0xF2));

            UGuiFactory.CreateText(content, "Title", cx, cardCy - 250, 520, 80, "收集完成！", 56,
                new Color32(0x66, 0xee, 0x77, 0xFF));

            // 各目标达成列表
            float listY = cardCy - 120;
            for (int i = 0; i < lines.Count; i++)
            {
                UGuiFactory.CreateText(content, $"Line_{i}", cx, listY + i * 70, 500, 60, lines[i], 44,
                    Color.white);
            }

            // 再来一局
            var again = UGuiFactory.CreateButton(content, "BtnAgain", cx, cardCy + 230, 370, 90, "再来一局", 38,
                new Color32(0x33, 0xaa, 0x55, 0xFF), Color.white, out _, out _);
            again.onClick.AddListener(() =>
            {
                GameModule.UI.CloseUI<CollectWinWindow>();
                GameModule.UI.ShowUIAsync<CollectDemoWindow>(); // 重新进入即 ResetForCollectDemo
            });

            // 返回主菜单
            var menu = UGuiFactory.CreateButton(content, "BtnMenu", cx, cardCy + 330, 300, 60, "返回主菜单", 26,
                new Color(0, 0, 0, 0), new Color32(0x88, 0xcc, 0xaa, 0xFF), out _, out _);
            menu.onClick.AddListener(() =>
            {
                GameModule.UI.CloseUI<CollectWinWindow>();
                GameModule.UI.ShowUIAsync<MainMenuWindow>();
            });
        }
    }
}
