---
name: project-uiwindow-text-regression-strategy
description: 跑窗口轻量换皮回归测策略:File.ReadAllText+Assert.Contains 关键行,UserData 解析/计算/调用点/回调,无需反射驱动 OnCreate。
metadata:
  type: project
---

在跑窗口做轻量换皮(路 A)的回归测试策略:UIWindow.OnCreate 依赖运行期单例+生命周期、EditMode 反射成本高;回归主测策略=**读源文件文本核对关键行**(`File.ReadAllText` + `Assert.IsTrue(src.Contains(...))`):UserData 解析行/结算计算行/调用点传参/回调目标——这些行只要存在即保证「换皮未越界」,而编译通过 + 全量 EditMode 通过是最强编译自检。测试文件本身即可验证「代码改了关键行就测试失败」,无需反射驱动 OnCreate。

**Why:** UIWindow 在 EditMode 难驱动(无 ServiceLocator/无 RootCanvas),反射造场景成本高、维护贵;关键行文本断言成本极低,改了关键行立即失败,是对「轻量改动」性价比最高的测法(2026-06,settlement-window)。

**How to apply:** 窗口轻量改动(改文案/改回调/改 UserData 解析):① 测里 `File.ReadAllText` 读源 .cs;② 对关键行 `Assert.IsTrue(src.Contains("xxx"))`;③ 不用反射造 OnCreate;④ 编译通过 + 关键行 contains 全过 = 回归;⑤ Play 手验留给视觉/交互层。
