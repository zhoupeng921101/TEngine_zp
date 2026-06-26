---
name: mongodb-setoninsert-needs-schema-migration
description: 给已有 MongoDB 集合加字段时,光靠 $setOnInsert + 写 SchemaVersion 不够 —— 老文档永远缺新字段,所有 Eq(field,默认值)/$bitsAllClear 类 filter 静默空命中;须在登录链路按 SchemaVersion 跑 $ifNull 迁移补字段
type: rule
---

给一个**已有数据的** MongoDB 集合(如 players)新增字段时,只用 `$setOnInsert` 写初值 + 写一个 `SchemaVersion` 字段,**不足以**让老文档拿到新字段。`$setOnInsert` 只在 insert 触发;老玩家重登走 update 路径完全不写新字段,文档里新字段永久 absent。而 MongoDB 中「字段缺失(absent)」≠「字段 = 默认值」:`Eq(field, 0)` / `Eq(field, 0L)` / `$bitsAllClear(field, mask)` 对 absent 字段**永不匹配**,直接让任何依赖「该字段 present」的原子 CAS / bootstrap 静默空命中(`FindOneAndUpdate` 返 null),业务表现为「功能对新号 OK、对老号永久失败」。仅当集合里只可能有「全是首登后插入」的文档时才可省略迁移。

根治模式:在登录加载文档的那一处(单一入口,先于一切玩法读写),判 `doc.SchemaVersion < CurrentSchemaVersion` 时跑一次原子迁移 —— 聚合管线 update `[{ $set: { 字段: { $ifNull: ["$字段", 默认值] }, ..., SchemaVersion: 当前版本 } }]`,filter 带 `Lt(SchemaVersion, 当前版本)`。`$ifNull` 保证 present 字段保留既有值、absent 字段填默认值(不覆盖玩家已有数据);filter 的版本下界保证幂等(已迁移文档不命中、并发多登录只一路补成)。默认值口径与 `$setOnInsert` 完全一致。

**Why:** 这是「字段缺失≠默认值」运行时陷阱(见用户记忆 fantasy-mongodb-runtime-gotchas)在「集合演进」维度的复发形态,且**每加一个新持久字段都会重犯一次**:`SchemaVersion` 字段写了但没有任何代码读它来驱动迁移 = 死字段,给人「有版本管理」的错觉。2026-06 订单交付「永远 AlreadyDelivered」即此根因:OrderCursor/LastOrderRefreshMs/OrderDeliveredMask 三字段在 SchemaVersion=1 老档里全 absent,交付 CAS 的 `Eq(cursor,0)`+`Eq(lastMs,0L)`+`$bitsAllClear(mask,bit)` 三条件同时空命中,连 bootstrap 的 `Eq(LastOrderRefreshMs,0L)` 也空命中故无法自愈。当时的强嫌疑根因(`$bitsAllClear` 用 (long)bit 查 Int32 字段的位宽不匹配)经真库 repro **证伪** —— `$bitsAllClear` 对 present 字段无论存储类型都正常命中,真凶是字段 absent。

**How to apply:** ① 给已有集合加持久字段时,除了 `$setOnInsert`,必须配套一段「登录时按 SchemaVersion 跑 $ifNull 迁移」逻辑,否则视为未完成。② 排查「新功能对部分账号永久失败、对新注册账号正常」类 bug,先连真库 dump 该账号文档的**实际字段集与 BSON 类型**(`col.Find().FirstOrDefault()` 遍历 `Elements`,看字段是 present 还是 absent),不要假设字段存在且为默认值。③ 强嫌疑根因(尤其 MongoDB 类型/算子层)在改代码前必须真库 repro 证实或证伪 —— 隔离每个 filter 条件单独 `CountDocuments`,定位到确切空命中的那一条,别按猜测改。
