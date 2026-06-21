---
name: project-cross-namespace-uiwindow-using
description: 跨命名空间引用别的 UIWindow:改前 grep .cs 读 namespace 行,别假设同目录=同命名空间。
metadata:
  type: project
---

跨命名空间引用别的 UIWindow:`SettingsWindow` 在 `GameLogic.UI`、`GameWindow` 在 `GameLogic.BlockBlastUI`,从后者调 `ShowUIAsync<SettingsWindow>` 须加 `using GameLogic.UI;`。改前先确认目标窗类的真实 namespace(Glob 找 .cs 读 namespace 行),别假设同目录=同命名空间。

**Why:** UI 模块演化过程中 namespace 划分不均匀,同目录下 .cs 可能分属不同 namespace;不查直接 ShowUIAsync 报 CS0246 找不到类型(2026-06,tarot-mode)。

**How to apply:** 跨窗口调用前:① Glob `Assets/**/<TargetWindow>.cs` 找文件;② Read 文件读 `namespace` 行;③ 在调用文件加 `using <ns>;`;④ 不依据目录名假设 namespace。
