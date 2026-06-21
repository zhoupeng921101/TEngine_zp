---
name: project-reskin-core-gameplay-window-triple-zone-split
description: 换皮核心玩法窗拆「换皮区/占位区/绝不碰区」三类,坐标常量单列硬验收
metadata:
  type: project
---

换皮在跑**核心玩法窗**(非弹窗/结算)时,零回归约束比换弹窗重得多:玩法窗每局全程在跑,改一处玩法逻辑或坐标常量即整局回归。

设计稿须把窗口拆「换皮区(静态视觉 BuildStaticUI)/ 占位区(无数据源·无机制)/ 绝不碰区(Render*/拖拽/落子/ghost/GameOver/OnUpdate + 坐标映射常量 + 数据层)」三类,画结构图明分,R 组硬验收逐条静态核对「玩法逻辑行未改 + 坐标常量未改 + git diff 仅该窗视觉」。

坐标映射常量(BlockLayout 的格尺寸/原点/槽位)单列一条 R:动了落子对位偏移=玩法回归,换皮节点 position/size 沿用既有值即可对位。

**Why:** 2026-06 tarot_mode HUD = Classic GameWindow 再主题先例。

**How to apply:** 玩法窗换皮稿必含三区分类结构图 + 坐标常量单列 R + git diff 范围硬验收。
