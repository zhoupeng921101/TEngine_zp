# 关单:Tier 2 玩家属性权威·客户端段·第 4 子单 · 我的流水 UI + RemoteAttrLedgerService

- target:client
- baton:full(plan→dev→test)
- 状态:PASS round 0
- 提交:Unity `b1fdec0c` + Fantasy `1f18d35b`(carry-forward 跨仓清)
- 新设计稿:design-docs/46-player-attr-ledger-client.md
- 同步重写:design-docs/25 §3.5 入口按钮挂位 + 44/45 §6.2/§一 Tier 2+ 接口余量 + nav.js 加 46 + index.html ?v=10→?v=11

## 关键决策
- UI 位置:独立窗 PlayerAttrLedgerWindow + PlayerInfoWindow 改名面板下方入口按钮
  - 否决 Tab(破模态语义)、HUD 长按(发现性差)
- 默认拉 50 条(沿 32 邮件常见 + 信息密度适中)
- 时间格式:本地时区 + 相对时间混合(<1min/h/24h/7d/≥7d)
- 属性图标复用 42 HUD 占位(gemstone/gemstone2/potion)
- source 文案硬编码 switch 中文 10 档(去变现 i18n 暂无,Tier 2+ 上 Luban i18n 再迁)
- 加载失败兜底:区分 ServiceUnavailable / NetworkDown 两档文案
- 离线/服务不可达:返空列表 + 不本地放行(守服务端独占审计完整性)
- 翻旧页本子单不做:按钮置灰 +「更多功能即将推出」诚实文案(禁「Tier 2+」字眼)
- 全代码生成 UI(沿 28 RankWindow BuildRowGo 范式推到整窗,新沉淀 dev memory)
- PlayerInfoWindow 入口按钮动态生成(NameBlock 父缺失返 null 等价 null-safe,不动 25 prefab)
- `_disposed + _fetchToken` 双保险防响应到达时窗已 dispose / 高频切 tab 旧响应覆盖新列表
- SafeSource 整数→枚举映射只接受 0..9,超出 Unknown 兜底

## carry-forward
- Fantasy `PlayerAttrLedgerDoc.Kind` doc comment 误差跨仓清(`1f18d35b`)

> state 文件已被后续 slice spawn 覆盖丢失;真凭证 = commit + boss.md。
