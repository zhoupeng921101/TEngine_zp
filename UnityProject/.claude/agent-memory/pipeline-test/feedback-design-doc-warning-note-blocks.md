---
name: feedback-design-doc-warning-note-blocks
description: 设计稿顶部 WARNING/NOTE 块易被 dev 同步遗漏,test 交叉检须显式核验这两块每个表述
metadata:
  type: feedback
---

conventions §6 交叉检漏点:设计稿内 `[!WARNING]`(读前必看)和 `[!NOTE]`(立项信息)两类标注块常被同步改写清单遗漏——这两块不在正文正文中、位于文档顶部,dev 按「§节号」列改写点时容易跳过;test 交叉检须显式对这两块的每个表述做现状核验。

**Why:** 2026-06 account-client 实测,dev 按设计稿 §节号列同步清单,跳过位于文档顶部的 WARNING/NOTE 块,导致这两块措辞与现状脱节。

**How to apply:** 交叉检流程加一步:文档顶部 grep `[!WARNING]`/`[!NOTE]` 块 → 逐句对照现状(代码或后续 §节)→ 不一致计文档同步遗漏(非 FAIL)。
