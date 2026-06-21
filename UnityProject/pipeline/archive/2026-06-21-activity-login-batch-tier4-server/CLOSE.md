# 关单:Tier 4 活动系统·服务端段·第 3 子单 · Login 批量 2 套 + 钩子按 type 遍历

- target:server
- baton:full(plan→server-dev→server-test)
- 状态:PASS round 0
- 提交:Unity `13a99081` + Fantasy `eff3376b`
- 新设计稿:design-docs/43-activity-login-batch.md
- 同步重写:design-docs/39 §3.5 节律表 Login 行 + §七 O1/O2 + nav.js 加 43

## 关键决策
- 选 2 套:OneShot 7 天大奖(验同 cycle 多活动并存)+ Weekly 5 天周奖(验新 cycle 节律)
- O3 不做「连续登录 N 天」(需扩 activity_progress schema 违守不变量,留后续)
- 登录钩子按 type=Login 遍历(兑现 39 §3.5 规范)
- ResetCounterIfCrossedWeek 放 Increment 之前(避免跨周首登 Counter 累加误判达标)
- 仅 Weekly cycle 调清零(避 OneShot 累计破坏 + Daily 无意义写)
- GiftPool 5003 钻石单项必中 + 5004 多项加权(plan O4 默认)
- AuthoritativeDefs/GiftPoolSeeds 单一权威源(服务端工程无 Luban)

> state 文件已被后续 slice spawn 覆盖丢失;真凭证 = commit + boss.md。
