---
name: project-mongodb-isolated-probe
description: 服务端裁决逻辑可在不起整服的前提下,对活 MongoDB 实跑验证 — 独立 console 探针复刻 helper 调用,断言 SV 行为
metadata:
  type: project
---

服务端裁决逻辑可在不起整服的前提下**对活 MongoDB 实跑验证**:独立 console 探针(`MongoDB.Driver` 版本对齐 Fantasy.Net 引用的 3.8.0)复刻 helper 的 MongoDB 调用,在隔离集合上断言 SV 行为(尤其并发取最优),用后 DropCollection 清理。

**Why:** 起整服跑 RPC harness 慢(几分钟启动 + 客户端模拟器),且 RPC 链路若有问题(Handler 未注册等)会掩盖 helper 真实行为。探针直接绕过整服跑 MongoDB 操作,验 helper 算法本身。

**How to apply:** 比"仅编译 + 起服看 init 日志"强,且不需客户端 RPC harness。dev 写完 helper 后(典型:rank 结算并发取最优 / ledger 写入 / 活动达标判)用探针验:① 启动本机 mongod(参 server-test 同名 memory)→ ② 在 D:\tmp 建 console 项目 + add MongoDB.Driver 3.8.0 → ③ 复刻 helper 调用喂构造数据 → ④ 断言行为 → ⑤ DropCollection 清。
