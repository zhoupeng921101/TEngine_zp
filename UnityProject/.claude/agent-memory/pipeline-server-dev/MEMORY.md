# pipeline-server-dev 共享经验库索引

> 重型与轻型 server-dev 共用。本角色不被系统自动注入,开工时手动 Read 本索引 + 相关条目。

- [MongoDB 加字段需配套 schema 迁移](mongodb-setoninsert-needs-schema-migration.md) — 已有集合加字段光靠 $setOnInsert + 写 SchemaVersion 不够;老文档永久缺字段、Eq/$bitsAllClear 静默空命中;须登录链路按 SchemaVersion 跑 $ifNull 迁移补字段。强嫌疑根因改代码前必须真库 repro 证实/证伪。
- [批量 RPC 前查频率闸粒度](batch-rpc-check-ratelimit-granularity.md) — 把逐项单发合并成批量时,per-account 单键频率闸下循环调 gated 单发会使第 2 项起被 RateLimited 误拒(单项 batch 单测测不出);须抽 gate-free 核心、整批只检一次闸。per-type 闸则安全。
- [下行快照 repeated 字段须带 loaded 标志](snapshot-repeated-needs-loaded-flag.md) — proto3 repeated 分不出「权威空集」与「降级未取到」,整份覆盖投影缺显式 loaded/valid 布尔时,服务端读库失败的空占位会把客户端本地投影整份清空;服务端可空返回 + 客户端 null=保留/空=清空三态契约。
