---
name: project-server-internal-trigger-verify
description: 服务端内部触发特性的真往返验证策略(无客户端 RPC 时仍可 PASS)
metadata:
  type: project
---

结算类特性(进程内定时器触发,非客户端 RPC)可实现真往返 PASS:起服 + 活 MongoDB + 等待节律间隔(60s)→ 节律首次触发 → 观测服务端日志中的结算完成条目 → MongoDB 查询 rank_settle 文档确认已结标记写入 + rank_def 结算字段同源。

**Why:** 设计 33 结算不依赖客户端 RPC,服务端自己触发,无需同步客户端才能观测结算行为。上一轮邮件(设计 32)因缺客户端 RPC 触发器判 BLOCKED,本轮结算因触发方式是服务端内部,故力争真往返 PASS。

**How to apply:** 判定运行验证能否 PASS 时,先看「触发方是客户端还是服务端内部」:内部触发(定时器/登录钩子/进程内调度)→ 直接起服等节律触发,用 MongoDB 查询 + 日志观测;客户端 RPC 触发 → 需同步客户端,缺触发器则 BLOCKED。

**实测验证要素:**
- 起服后等待节律间隔(本项目 60s),日志见 `RankSettleHelper: 榜 X 本周期结算完成`。
- 临时 MongoDB 查询探针:在 `D:\tmp` 建 .NET 10 控制台项目 + 引用 MongoDB.Driver 3.0.0,用 `MongoClient("mongodb://127.0.0.1:27017")` 直接查集合,无需 mongosh。
- 第二次节律触发后日志中无新结算条目 = 幂等生效(SV6)。
- 榜 _id 已有上轮探针留存的 LastSettledPeriodMs,本轮起服后同周期正确跳过 = SV7(重启幂等)生效。
