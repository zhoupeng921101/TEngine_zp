---
name: feedback-doc-sync-gap-not-code-fail
description: 设计稿增量验收段措辞过时但代码行为正确时,判为文档同步遗漏而非代码 FAIL,记录供 dev 下轮精化
metadata:
  type: feedback
---

conventions §6 交叉检判定精度:设计稿「增量自身验收」段(如 SK1/SK2)措辞若有部分仍成立(Fetch 行为)、有部分随后续增量失真(「全系统无真实网络调用」),判为文档同步遗漏而非代码 FAIL;代码行为正确时不升 FAIL,记录供 dev 下轮精化。

**Why:** 2026-06 rank client 实测,代码已演进出真实网络调用,设计稿老段措辞「无真实网络调用」过时,但当前增量自身实现正确——这是文档腐烂,不是代码缺陷。

**How to apply:** 交叉检发现稿/码不一致时分两类:①代码行为不符合设计意图 = FAIL ②代码符合现行设计、稿措辞被后续增量改写遗漏 = 记录项(非 FAIL),报告里写「文档同步遗漏,dev 下轮精化」。
