# pipeline-server-dev 共享经验库索引

> 重型与轻型 server-dev 共用。本角色不被系统自动注入,开工时手动 Read 本索引 + 相关条目。

- [MongoDB 加字段需配套 schema 迁移](mongodb-setoninsert-needs-schema-migration.md) — 已有集合加字段光靠 $setOnInsert + 写 SchemaVersion 不够;老文档永久缺字段、Eq/$bitsAllClear 静默空命中;须登录链路按 SchemaVersion 跑 $ifNull 迁移补字段。强嫌疑根因改代码前必须真库 repro 证实/证伪。
