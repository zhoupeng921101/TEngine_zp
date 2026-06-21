---
name: feedback-probe-testdata-invariant
description: MongoDB 探针直接写测试数据时须满足业务不变量,否则读路径验证会误报 FAIL
metadata:
  type: feedback
---

探针(D:/tmp/.NET 控制台项目)绕过服务端写入校验直接写 MongoDB 集合时,若测试数据不满足业务不变量(如 ledger 的 BalanceAfter == BalanceBefore + Delta),读路径探针在验证不变量时会误报 FAIL——这是探针数据构造问题,不是 handler 代码缺陷。

**Why:** Tier2-45 测试中探针写入第 3 行 Diamond:before=0/delta=-50/after=max(0,-50)=0,不满足 BalanceAfter==BalanceBefore+Delta,导致探针 SV4 余额不变量 FAIL 1 项,经分析确认是探针数据 bug 而非 handler bug。

**How to apply:** 探针写测试数据时,必须按业务约束构造合法数据:
- ledger 行: BalanceAfter = BalanceBefore + Delta(有符号,Delta 可负)
- 余额: BalanceBefore >= 0 且 BalanceAfter >= 0
- 若 Delta 导致余额为负,说明 test case 设计错误(服务端写入路径有 Gte 条件守护)
  - 正确做法: 写 3 行 Diamond 时 before/after 递减应保证余额非负且不变量成立
  - 反例(本次 bug): before=0, delta=-50, after=max(0,-50)=0 → 0 ≠ 0+(-50)
