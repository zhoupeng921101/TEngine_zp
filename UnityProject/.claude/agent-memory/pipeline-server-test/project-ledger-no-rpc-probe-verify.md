---
name: project-ledger-no-rpc-probe-verify
description: 服务端 ledger 类特性(无客户端 RPC 触发器)用探针模拟 + 代码静态分析双路验证
metadata:
  type: project
---

服务端 ledger 类特性(ChangeProperty 旁路写,非定时触发)无法由客户端 RPC 触发。改由探针直接模拟 `FindOneAndUpdate + InsertOne` 路径验证 MongoDB 真往返;同时对代码关键路径做静态分析补充(AppendAsync 调用点位置 / 失败路径 return 位置)。两者结合可达到 PASS 置信度而无需客户端 RPC。

**Why:** ledger 是「写库后旁路追加」非定时器触发,没有外部入口可触发;但仍需验证 mongodb 真往返。

**How to apply:** 与 [[project-server-internal-trigger-verify]](定时器触发类)正交:那条用「起服+等节律+日志观测」,这条用「探针直接写库读路径」。两类特性互斥(一个特性要么定时器要么旁路),按特性选用。
