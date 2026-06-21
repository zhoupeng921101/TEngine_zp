---
name: project-stale-design-rewrite-or-delete
description: 旧设计稿与现状脱节时按 conventions §6 重写或删除,不加勘误/映射 callout 叠旧
metadata:
  type: project
---

旧设计稿与现状脱节(代码已正名/机制已改/玩法已变)时按 conventions 规则6:正文过时句**重写为现行事实**(现行体系仍在)或**删除整段/整篇**(已下线),**不加勘误/映射 callout 叠旧、不"冻结正文"**,历史归 git。

动手前先 grep 工程现状 + 通读目标篇核实「实际落地名/现状」(简报常停在动手前;警惕原文列过的未采用假想名,如 10 §3.3 列过 `CollectDemo→BlockElementVisual` 实际落 `MergeElementVisual`)。

**Why:** 加勘误注把过时正文留在原地,实际制造没人会再维护的孤儿正文,与旁边现行正文长期矛盾。原「加映射 callout 冻结正文」做法已被规则6取代(2026-06 docs 对齐)。

**How to apply:** 改设计稿前 grep 工程现状,过时句覆盖式重写或整段删除;不写「原 X 改为 Y」勘误注;被删改的设计依据进 commit message。
