using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 结算窗换皮零回归测试（设计 26 §8.1 R/C 组）。
    ///
    /// 策略：以静态核对 + 编译为主。
    /// 两窗的结算逻辑依赖 BlockGameState.Instance 运行期单例 + UIWindow 生命周期，
    /// EditMode 直接反射驱动 OnCreate 成本高（需运行期注入）。
    /// 此处通过读源文件文本核对关键行是否存在 / 未被删改，
    /// 用编译（能跑起来即 0 error）+ 关键行存在性双重兜底覆盖 R1-R4。
    ///
    /// 关键行定义：换皮前后逻辑不应变化的代码片段，一旦被改则测试报告"越界"。
    /// </summary>
    [TestFixture]
    public class SettlementWindowRegressionTests
    {
        // 源文件相对于 Assets 的路径（通过 Application.dataPath 定位）
        private const string GameOverPath =
            "GameScripts/HotFix/GameLogic/UI/BlockBlastUI/GameOverWindow.cs";
        private const string WinWindowPath =
            "GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWinWindow.cs";
        private const string GameWindowPath =
            "GameScripts/HotFix/GameLogic/UI/BlockBlastUI/GameWindow.cs";
        private const string MergeOrderWindowPath =
            "GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWindow.cs";

        private string _dataPath;

        [SetUp]
        public void SetUp()
        {
            _dataPath = UnityEngine.Application.dataPath;
        }

        private string ReadSource(string relPath)
        {
            var path = Path.Combine(_dataPath, relPath);
            Assert.IsTrue(File.Exists(path), $"源文件不存在: {path}");
            return File.ReadAllText(path);
        }

        // ── C1：文件可读取（程序集能编译才跑到这里，本例本身即编译自检）──────────
        [Test]
        public void C1_SourceFilesExist_AndTestCanRun()
        {
            // 能执行到此处说明 BlockBlast.Tests 程序集已通过编译（0 error 约束）
            Assert.IsNotNull(ReadSource(GameOverPath), "GameOverWindow.cs 可读");
            Assert.IsNotNull(ReadSource(WinWindowPath), "MergeOrderWinWindow.cs 可读");
        }

        // ── R1：GameOverWindow Classic 路径 — UserData 解析行未改 ──────────────────
        [Test]
        public void R1_GameOverWindow_UserDataParsing_Unchanged()
        {
            var src = ReadSource(GameOverPath);

            // 必须保留：UserData as int 解析（Classic 路径 previousHigh）
            Assert.IsTrue(src.Contains("UserData is int p ? p : 0"),
                "R1: UserData is int 解析行必须保留（换皮不得改 previousHigh 来源）");

            // 必须保留：从 BlockGameState.Instance 取 Score / HighScore
            Assert.IsTrue(src.Contains("BlockGameState.Instance"),
                "R1: 必须从 BlockGameState.Instance 取分数（换皮不得改结算数据来源）");
            Assert.IsTrue(src.Contains("state.Score"),
                "R1: finalScore 来源 state.Score 必须保留");
            Assert.IsTrue(src.Contains("state.HighScore"),
                "R1: high 来源 state.HighScore 必须保留");

            // 必须保留：isNewBest 计算逻辑（finalScore > previousHigh && finalScore > 0）
            Assert.IsTrue(src.Contains("finalScore > previousHigh"),
                "R1: isNewBest 计算必须保留（finalScore > previousHigh）");
            Assert.IsTrue(src.Contains("finalScore > 0"),
                "R1: isNewBest 计算必须保留（finalScore > 0）");
        }

        // ── R1b：调用点 GameWindow.cs:336 未改 ────────────────────────────────────
        [Test]
        public void R1b_GameWindow_CallSite336_Unchanged()
        {
            var src = ReadSource(GameWindowPath);

            // GameWindow.cs:336 ShowUIAsync<GameOverWindow>(previousHigh)
            Assert.IsTrue(src.Contains("ShowUIAsync<GameOverWindow>(previousHigh)"),
                "R1: GameWindow.cs 调用点应传 previousHigh（Classic 路径，不得改传参）");
        }

        // ── R2：GameOverWindow 订单结束路径 — MergeOrderWindow.cs:731 传 0 未改 ────
        [Test]
        public void R2_MergeOrderWindow_CallSite731_PassesZero()
        {
            var src = ReadSource(MergeOrderWindowPath);

            // MergeOrderWindow.cs:731 ShowUIAsync<GameOverWindow>(0)
            Assert.IsTrue(src.Contains("ShowUIAsync<GameOverWindow>(0)"),
                "R2: MergeOrderWindow.cs:731 应传 0（订单结束路径，previousHigh=0）");
        }

        // ── R3：MergeOrderWinWindow 通关路径 — UserData 解析 + 渲染循环未改 ─────────
        [Test]
        public void R3_MergeOrderWinWindow_UserDataParsing_AndRenderLoop_Unchanged()
        {
            var src = ReadSource(WinWindowPath);

            // 必须保留：UserData as List<string> 解析
            Assert.IsTrue(src.Contains("UserData as List<string>"),
                "R3: UserData as List<string> 解析行必须保留（通关路径传结算行）");

            // 必须保留：逐行渲染循环（for + lines[i]）
            Assert.IsTrue(src.Contains("lines.Count"),
                "R3: 结算行渲染循环 lines.Count 必须保留");
            Assert.IsTrue(src.Contains("lines[i]"),
                "R3: 结算行取值 lines[i] 必须保留");
        }

        // ── R3b：调用点 MergeOrderWindow.cs:718 传 lines 未改 ───────────────────────
        [Test]
        public void R3b_MergeOrderWindow_CallSite718_PassesLines()
        {
            var src = ReadSource(MergeOrderWindowPath);

            // MergeOrderWindow.cs:718 ShowUIAsync<MergeOrderWinWindow>(lines)
            Assert.IsTrue(src.Contains("ShowUIAsync<MergeOrderWinWindow>(lines)"),
                "R3: MergeOrderWindow.cs:718 应传 lines（通关路径传结算行，不得改传参）");
        }

        // ── R4：四个回调目标未改 ───────────────────────────────────────────────────
        [Test]
        public void R4_GameOverWindow_RetryCallback_TargetsGameWindow()
        {
            var src = ReadSource(GameOverPath);

            // 重试 → GameWindow（R4 零回归）
            Assert.IsTrue(src.Contains("ShowUIAsync<GameWindow>()"),
                "R4: GameOverWindow 重试回调目标必须是 GameWindow");
        }

        [Test]
        public void R4_GameOverWindow_BackCallback_TargetsMainMenuWindow()
        {
            var src = ReadSource(GameOverPath);

            // 返回 → MainMenuWindow（R4 零回归）
            Assert.IsTrue(src.Contains("ShowUIAsync<MainMenuWindow>()"),
                "R4: GameOverWindow 返回回调目标必须是 MainMenuWindow");
        }

        [Test]
        public void R4_MergeOrderWinWindow_AgainCallback_TargetsMergeOrderWindow()
        {
            var src = ReadSource(WinWindowPath);

            // 再来一局 → MergeOrderWindow（R4 零回归）
            Assert.IsTrue(src.Contains("ShowUIAsync<MergeOrderWindow>()"),
                "R4: MergeOrderWinWindow 再来一局回调目标必须是 MergeOrderWindow");
        }

        [Test]
        public void R4_MergeOrderWinWindow_BackCallback_TargetsMainMenuWindow()
        {
            var src = ReadSource(WinWindowPath);

            // 返回 → MainMenuWindow（R4 零回归）
            Assert.IsTrue(src.Contains("ShowUIAsync<MainMenuWindow>()"),
                "R4: MergeOrderWinWindow 返回回调目标必须是 MainMenuWindow");
        }

        // ── C2：换皮红线核对（五条编码红线）─────────────────────────────────────────
        [Test]
        public void C2_GameOverWindow_NoDirectLoadAssetAsync()
        {
            var src = ReadSource(GameOverPath);

            // 不得有裸 LoadAssetAsync<Sprite>（SetSubSprite 自管引用计数）
            Assert.IsFalse(src.Contains("LoadAssetAsync<Sprite>"),
                "C2: GameOverWindow 不得有裸 LoadAssetAsync<Sprite>（应走 SetSubSprite）");

            // 按钮回调用 onClick.AddListener（事件解耦红线：UI 内部用 onClick）
            Assert.IsTrue(src.Contains("onClick.AddListener"),
                "C2: 按钮回调应用 onClick.AddListener（UI 内部事件）");

            // 模块访问用 GameModule.UI
            Assert.IsTrue(src.Contains("GameModule.UI"),
                "C2: 模块访问应用 GameModule.UI");
        }

        [Test]
        public void C2_MergeOrderWinWindow_NoDirectLoadAssetAsync()
        {
            var src = ReadSource(WinWindowPath);

            Assert.IsFalse(src.Contains("LoadAssetAsync<Sprite>"),
                "C2: MergeOrderWinWindow 不得有裸 LoadAssetAsync<Sprite>（应走 SetSubSprite）");
            Assert.IsTrue(src.Contains("onClick.AddListener"),
                "C2: 按钮回调应用 onClick.AddListener");
            Assert.IsTrue(src.Contains("GameModule.UI"),
                "C2: 模块访问应用 GameModule.UI");
        }

        // ── V1 代理：确认 SetSubSprite 链路在两窗中存在（证明换皮代码已写入）────────
        [Test]
        public void V1_GameOverWindow_SetSubSprite_PresentForCardAndButtons()
        {
            var src = ReadSource(GameOverPath);

            // 精灵表常量
            Assert.IsTrue(src.Contains("Sheet_settings"),
                "V1: GameOverWindow 应引用 Sheet_settings 精灵表");

            // 卡片贴 box1
            Assert.IsTrue(src.Contains("\"box1\""),
                "V1: 主面板卡片应贴 box1 子图");

            // 标题木牌贴 box2
            Assert.IsTrue(src.Contains("\"box2\""),
                "V1: 标题木牌应贴 box2 子图");

            // 按钮底贴 button
            Assert.IsTrue(src.Contains("\"button\""),
                "V1: 按钮底应贴 button 子图");
        }

        [Test]
        public void V1_MergeOrderWinWindow_SetSubSprite_PresentForCardAndButtons()
        {
            var src = ReadSource(WinWindowPath);

            Assert.IsTrue(src.Contains("Sheet_settings"),
                "V1: MergeOrderWinWindow 应引用 Sheet_settings 精灵表");
            Assert.IsTrue(src.Contains("\"box1\""),
                "V1: 主面板卡片应贴 box1 子图");
            Assert.IsTrue(src.Contains("\"box2\""),
                "V1: 标题木牌应贴 box2 子图");
            Assert.IsTrue(src.Contains("\"button\""),
                "V1: 按钮底应贴 button 子图");
        }

        // ── 不改清单验证：三处调用点文件存在且行结构完整 ──────────────────────────
        [Test]
        public void NoChange_CallSiteFilesUntouched_ClassAttributePresent()
        {
            // GameOverWindow 属性（location 不改则窗口仍可被寻址）
            var govSrc = ReadSource(GameOverPath);
            Assert.IsTrue(govSrc.Contains("[Window(UILayer.Top, location: \"GameOverWindow\", fullScreen: true)]"),
                "GameOverWindow location 属性不得改（改则运行时找不到 prefab）");

            // MergeOrderWinWindow 属性
            var winSrc = ReadSource(WinWindowPath);
            Assert.IsTrue(winSrc.Contains("[Window(UILayer.Top, location: \"MergeOrderWinWindow\", fullScreen: true)]"),
                "MergeOrderWinWindow location 属性不得改");
        }
    }
}
