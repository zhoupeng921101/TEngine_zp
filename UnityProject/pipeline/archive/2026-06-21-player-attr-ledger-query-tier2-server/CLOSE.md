# 关单:Tier 2 玩家属性权威·服务端段·第 3 子单 · ledger 查询 RPC + handler + 协议生成物

- target:server
- baton:full(plan→server-dev→server-test)
- 状态:PASS round 0
- 提交:Unity `3d2e966c` + Fantasy `29597b26`
- 新设计稿:design-docs/45-player-attr-ledger-query.md
- 同步重写:design-docs/44 §6.2/§一/§5.4/§7.2 + nav.js 加 45 + index.html ?v=9→?v=10

## 关键决策
- 新协议 C2G_QueryAttrLedgerRequest + G2C_QueryAttrLedgerResponse + AttrLedgerEntry
- kind 可选(0=不过滤)、sinceTs 可选(默认 0)、limit 必填且服务端钳制上限 100
- 响应 entry 7 字段白名单(不暴露 ObjectId/SchemaVersion/account)
- 分页 sinceTs 滑动窗口(增量同步),不做翻旧页 cursor/offset(留 Tier 2+)
- source 整数枚举码(客户端段下一刀映射文本)
- 3 错误码:Success/InvalidRequest/ServiceUnavailable
- 身份从会话取,handler 强制覆盖 account(防客户端伪造拉他人)
- Kind 整数偏移:0=sentinel/1-3=Coin/Diamond/Stamina(避与 PropertyType.Coin=0 冲突)
- handler 只读 ledger(无 Insert/Update/Delete,沿 44 §5.4 追加式 invariant)
- limit=0 short-circuit 不走 Mongo(避 MongoDB Limit(0) 语义『不限』致全表扫)
- 顺手改 ExporterSettings.json 本机绝对路径(沿 memory proto-exporter-quirks #2)

## carry-forward(已清)
- Fantasy `PlayerAttrLedgerDoc.Kind` doc comment 误差 → 客户端段下一刀跨仓清(`1f18d35b`)

> state 文件已被后续 slice spawn 覆盖丢失;真凭证 = commit + boss.md。
