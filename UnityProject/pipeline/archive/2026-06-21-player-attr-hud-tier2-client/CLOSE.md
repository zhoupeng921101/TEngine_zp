# 关单:Tier 2 客户端段第 3 子单 · tarot HUD 三属性接 PlayerAttrService

- target:client
- baton:full(plan→dev→test)
- 状态:PASS round 0
- 提交:Unity `c861d610`
- 新设计稿:design-docs/42-tarot-hud-player-attr-bind.md
- 同步重写:design-docs/27 §三 D1/§5.3/§十 D1 + 38 §一/§7.7/§9.4 + nav.js 加 42

## 关键决策
- 三属性图标沿用 gemstone/gemstone2/potion 占位(专属图另开 UI 抛光刀)
- IsReady=false 占位文本「—」(0 是合法余额值)
- 顺序 Coin/Diamond/Stamina(协议枚举顺序)
- HUD「+」点击保留 Log 待建(去变现)

## carry-forward
- 27 第 49/51/93/250 行与新设计内部矛盾在 plan 同任务内重写

> state 文件已被后续 slice spawn 覆盖丢失;真凭证 = commit + boss.md。
