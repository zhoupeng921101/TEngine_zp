---
name: project-ui-data-singletons-gamecontext-vs-blockgamestate
description: UI 数据层分两套单例:通用服务挂 GameContext,玩法态归 BlockGameState
metadata:
  type: project
---

本项目 UI 数据层与玩法态分两套单例:

- **通用服务**(settings/player-info 等,随会话长存)挂 `GameLogic.GameContext`(SimpleSingleton,有 `InitSettingsWithStore` 式测试注入入口)
- **玩法态**(棋盘/得分/悔棋,随开局 Reset)归 `BlockGameState`

换皮窗取数据走 `GameContext.Instance.Xxx`。注意 player-info 数据层命名空间是 `GameLogic.BlockBlast.Player`(非 GameLogic.Settings),持久化并入既有 `MergeMetaSave`(非另造存储)——设计 18/23 先例。

**Why:** 两类生命周期不同,放一起会让玩法 Reset 误清通用服务、或会话切换误清玩法态。

**How to apply:** 设计新数据服务前先判生命周期:跨局长存 → GameContext;随开局 Reset → BlockGameState。命名空间和持久化容器看相邻系统先例,不另造平行结构。
