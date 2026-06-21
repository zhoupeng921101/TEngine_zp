---
name: project-ui-vs-data-field-mismatch-split
description: UI 换皮效果图与数据层 spec 字段不一致时的分流默认:多出占位记 decisions,少了归后续屏
metadata:
  type: project
---

UI 换皮设计「效果图元素集 ≠ 数据层 spec 元素集」时的分流(自治放手默认):

① 效果图**多出**数据层没有的字段(本例「生日」)→ UI 占位(摆节点对位、不绑数据、不入存档),决策记 decisions 交 boss/产品复核,**不擅自往持久化 DTO 加字段**(加字段=动数据层+保底+单测,溢出「纯 UI 补完」范围、且替产品定需求);可逆——日后真做是局部增量、节点已摆好替占位即可不返工。

② 效果图**少了** spec 有的元素(本例等级槽/头像三态网格/id 复制)→ 归后续屏,本轮不强加(效果图是本任务对位基准)。

两者都属安全默认、入 decisions 不入 blockers。

**Why:** 2026-06 player-info 窗:个人信息.png 有生日但 PlayerInfo 无字段→占位 D2;效果图无等级槽/网格→后续屏先例。

**How to apply:** UI 换皮稿对效果图与数据层字段集合做集合差:多出 → UI 占位 + decisions;少了 → 后续屏;均不入 blockers,不擅自动数据层。
