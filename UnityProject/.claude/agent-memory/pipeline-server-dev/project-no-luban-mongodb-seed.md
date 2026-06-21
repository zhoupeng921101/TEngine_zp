---
name: project-no-luban-mongodb-seed
description: 服务端工程无 Luban 配置集成;与客户端同源的配置按 redeem/rank 先例播种 MongoDB 文档,不建服务端 Luban→导出路径
metadata:
  type: project
---

服务端工程**无 Luban 配置集成**(无 TbXxx / .bytes 加载链)。需要「与客户端配置同源」的服务端权威配置时,按 redeem/rank 先例**播种 MongoDB 文档**(值手抄客户端 xlsx 口径)。

**Why:** Fantasy.Net 工程没集成 Luban,新建一条 Luban→服务端导出路径是大工程(改 Luban 配置 + 加生成目标 + 协议管理);服务端只读少量权威配置(兑换码/排行榜/邮件等)时,直接在 Service Awake 内 `AuthoritativeDefs` / `GiftPoolSeeds` upsert 到 MongoDB 集合更直达。

**How to apply:** 服务端新增「与客户端配置同源」类需求(典型:活动 reward 礼包 id / 兑换码池 / 邮件模板)→ 找一个对应的 Service 组件,在 Awake 内 `ReconcileDefs` 批量 upsert(沿 30/31/32/33/39/40/43/47 已建范式)。值手抄客户端 xlsx 口径(代码里硬编码常量),运营热改需求出现时才上数据库集合管理(那时需要 GM 后台,本期不做)。
