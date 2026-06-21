---
name: feedback-fix-scope-vs-diff-full-check
description: dev 交接区限定复检范围时只跑指定项,但 diff 须全量核查(可能超出列点的合规改动)
metadata:
  type: feedback
---

返修复检范围精确识别:dev 交接区注明「仅需 grep 复核 + 通读 X 段,编译/单测/真往返不重跑」时,test 只跑指定复检项即可;但 diff 须全量核查,dev 返修可能超出交接区列点(同源过时口径被一并清理),应确认额外改动合规而非报新问题。

**Why:** 2026-06 player-attr FAIL-1 返修实测,dev 限定改动范围以省时间,但顺手清理了同源问题(同口径过时措辞在他处也出现),test 若不看 diff 全貌会误把这些合规改动当新问题报。

**How to apply:** 返修轮 test 两步:①按 dev 限定的复检项跑(不扩散)②git diff 全量看,识别「列点外的改动」→ 评估合规性(通常是同源清理,合规)→ 不报新问题。
