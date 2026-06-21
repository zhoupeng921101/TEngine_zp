---
name: project-gamemodule-in-gamelogic-assembly
description: GameModule 在 GameLogic 程序集(非 TEngine.Runtime),反射收集须按程序集名筛
metadata:
  type: project
---

GameModule 在 GameLogic 程序集(非 TEngine.Runtime),反射收集时须指定 `GetAssemblies().FirstOrDefault(a => a.GetName().Name == "GameLogic")` 再遍历,不在 TEngine.Runtime/TEngine.Editor 中。

**Why:** 2026-06 ledger-client Play 验证实测,名字带 TEngine 风格会让人误以为在 TEngine.Runtime,实际 GameLogic 程序集是项目业务层,GameModule 这类入口类放在业务侧。

**How to apply:** 反射找 GameModule/业务入口类:`AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "GameLogic").GetType("GameLogic.GameModule")`;别 SelectMany 全程序集扫(慢且不稳)。
