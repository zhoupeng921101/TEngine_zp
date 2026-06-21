# 关单:Tier 4 活动系统·服务端段·第 4 子单 · Cumulative 节律 + 第 1 套样例(累计 100 局)

- target:server
- baton:full(plan→server-dev→server-test)
- 状态:PASS round 0
- 提交:Unity `ffc88f68` + Fantasy `be2b3b51`
- 新设计稿:design-docs/47-activity-cumulative.md
- 同步重写:design-docs/39 §3.5 Cumulative 行(留 O3 → 已交付) + §3.1 旁注 + §3.5 节律旁注末尾 + §七 O2/O3 + §八 BLOCKED 共 6 处 + nav.js 加 47 + index.html ?v=11→?v=12

## 关键决策
- 新归一 RPC C2G_ActivityIncrement(activityId, delta)(否决复用/扩 C2G_GameEnd 违关注点单一)
- 5 项校验链:身份从会话 / delta>0(fail-fast) / activityId 存在 / type=Cumulative 强制 / delta≤10000 钳制
- 4 档错误码:Success/InvalidRequest/NotCumulative/ServiceUnavailable
  - NotCumulative 单列便客户端差异化提示 + 反作弊日志统计
- type=Cumulative 强制校验(防客户端用此 RPC 推 Login 类活动旁路登录节律,反作弊硬约束)
- 单次 delta 上限 10000 钳制不报错(防恶意推爆 counter,客户端从 response.currentCounter 自查)
- 样例 activity_id=5, target=100 OneShot 累计游戏 100 局
- reward=5005 复用 43 子单 5003 钻石礼包;mail_def 占位 textId 390009 运营后续填 i18n
- handler vs 服务端业务方直调 service 双路 DRY + 单一权威 + service 层兜底 type 校验做纵深防御
- service activityId 不存在返 ServiceUnavailable(语义=配置缓存与 handler 不同步,非客户端参数错;handler 仍返 InvalidRequest 给客户端)
- service 不重复 delta 钳制(handler 是客户端边界守门,service 是内部业务通路)
- 抽 ResetCounterIfCrossedPeriod 通用版(Daily/Weekly 共用 OneShot 跳过)兑现 §3.5 跨周期清零前瞻
- ResetCounterIfCrossedWeek 改内部代理守 Weekly 活动 4 零回归
- counter 累加 MongoDB `$inc` 原子(沿 33 §3.2);抢占 FindOneAndUpdate 原子条件写(沿 39 §3.4 claim-then-act)
- ActivityProgressService.Increment 前瞻支持 Daily/Weekly Cumulative(本子单 OneShot 样例验过,Daily/Weekly 验证留 O4)
- 客户端业务接入(GameOver hook + UI + 离线缓存策略)留下一子单
