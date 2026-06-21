---
name: project-arbiter-overlay-no-change-baseline
description: 智能生成上层仲裁套现状 dynamicWeight:仲裁器返二态(接管/不接管),不接管=保持现状逐字节不变,「现状零改变」做成可测断言。
metadata:
  type: project
---

智能生成上层仲裁套现状 dynamicWeight:仲裁器返回「接管/不接管」二态,不接管=保持现状发牌逐字节不变——把「现状零改变」做成可测断言(四规则都不触发→`Override==false`)。规则到 trio 复用既有 `BlockAlgorithms`,别重写启发式。

**Why:** 仲裁层叠在现有发牌逻辑上,默认路径必须不被新代码污染;把「不接管=零改变」做成显式断言,可在每次仲裁规则改动时锁回归(2026-06,core-loop)。

**How to apply:** 写智能仲裁器:① 仲裁器返二态 struct(Override:bool + 数据);② 不接管时不修改任何外部状态;③ 单测「构造所有规则都不命中的场景→断言 Override==false」;④ 复用既有启发式函数,不另写副本。
