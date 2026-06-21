---
name: project-mode-toggle-additive-extension
description: 给既有玩法加模式开关:加法式扩展+单开关门控,所有离开路径调 Exit 清态,off 路径逐字节不变靠原回归单测兜底。
metadata:
  type: project
---

给既有玩法加模式开关的安全做法:加法式扩展 + 单开关门控 + 所有离开路径调 Exit 清态;off 路径逐字节不变,靠原回归单测兜底。

**Why:** 修改既有玩法极易引入隐藏回归;通过新增模式开关 + 离开时清态,保持默认路径行为完全等价,既能扩展新模式又不破坏旧路径(2026-06,CollectMode 实例)。

**How to apply:** 新增玩法模式/玩法变体时:① 新增独立开关字段(Mode/Enabled);② 新代码全在 if(Mode)分支内;③ 任何离开路径(返回主菜单/重开/异常)都显式调 ExitMode 清空新状态;④ off 路径代码逐字节不动;⑤ 原回归单测全数跑通即证零回归。
