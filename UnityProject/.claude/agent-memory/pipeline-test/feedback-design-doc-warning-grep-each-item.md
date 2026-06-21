---
name: feedback-design-doc-warning-grep-each-item
description: 设计稿顶部 WARNING 块「读前必看」摘要常被 dev 遗漏,test 须显式 grep 块内每条与现状对比
metadata:
  type: feedback
---

conventions §6 交叉检:设计稿顶部 WARNING 块(「读前必看」摘要区)常被 dev 同步清单遗漏,尤其当 §节改写后该块内的对应摘要句过时,test 须显式 grep WARNING 块每个条目与同文件 §节现状对比。

**Why:** 2026-06 player-attr Code Review 实测,WARNING 块本是给读者的快速摘要,§节正文改了但摘要忘改 → 形成稿内自相矛盾的孤儿摘要。

**How to apply:** 与 feedback-design-doc-warning-note-blocks 联用:WARNING 块每条 → 找对应 §节 → 现状一致?不一致 = 文档同步遗漏(非 FAIL)。
