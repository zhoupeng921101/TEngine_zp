# 关单:Tier 2 玩家属性权威·服务端段·第 2 子单 · PlayerAttr 变动 ledger 审计日志

- target:server
- baton:full(plan→server-dev→server-test)
- 状态:PASS round 0
- 提交:Unity `3825e1d1` + Fantasy `750ec980`
- 新设计稿:design-docs/44-player-attr-ledger.md
- 同步重写:design-docs/37 §六 ledger 句柄/挂钩声明 + nav.js 加 44 + index.html ?v=9→?v=10

## 关键决策
- 独立集合 `player_attr_ledger`(否决内嵌 players 数组:16MB 上限/读放大/查询模式不匹配)
- source 10 档枚举(5 核心 + 4 增强 + Unknown 兜底)
- 写入时机:写库后/推送前/校验失败分支不写
- 旁路追加不进 37 FindOneAndUpdate 原子边界(InsertOne 失败仅 Warning 不回滚)
- 协议字段仍是字符串 reason(守 PropertyChangeRequest 签名,source 由 MapReasonToSource 服务端映射)
- MapReasonToSource:StartsWith Ordinal 前缀匹配 + player_rename Equals 精确
- 索引最小集:(account,timestamp DESC) 复合 + (timestamp DESC) 单字段
- 追加式 invariant:永不 update/delete,Code Review grep 全工程拦
- 客户端 UI + 拉流水 RPC 留 Tier 2+
- Tier 2+ 增强枚举前缀映射已登记(接入零额外改动)

> state 文件已被后续 slice spawn 覆盖丢失;真凭证 = commit + boss.md。
