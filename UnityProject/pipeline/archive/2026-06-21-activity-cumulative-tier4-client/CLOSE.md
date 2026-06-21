# 关单:Tier 4 活动系统·客户端段·第 5 子单 · Cumulative GameOver hook + RemoteActivityService

- target:client
- baton:full(plan→dev→test)
- 状态:PASS round 0
- 提交:Unity `715d66ee`(Fantasy 零改)
- 新设计稿:design-docs/48-activity-cumulative-client.md
- 同步重写:design-docs/47 立项框 5 处 + §3/§4/§5/§6/§7/§8/§9 共 13 处 + nav.js 加 48 + index.html ?v=12→?v=13

## 关键决策
- 三处 GameOver 出口共计:Classic GameWindow.TriggerGameOver + MergeOrder TriggerGameOver(双路共用)+ MergeOrder TriggerWin(通关)
  - 通关也算「玩了一局」,否则「玩得越好越没奖」反直觉
- Tarot/Classic 不分别计数(同 GameWindow 复用,沿 26 tarot_mode HUD = Classic GameWindow 再主题)
- 离线/服务不可达:不缓存 pending delta 直接丢弃(守服务端独占,本地缓存违 47 §四不本地放行)
- 无 UI 反馈(沿 EVENT 解锁同范式:服务端达标自动投活动邮件,客户端从邮件领奖,GameOver 焦点不被抢)
- fire-and-forget 不阻塞 GameOver 流程(慢网 await 会让玩家卡 5-30s)
- 错误码客户端处理 5 档分级:Success/Info,InvalidRequest+NotCumulative/Error(客户端 bug),ServiceUnavailable+NetworkDown/Warning(环境抖动);任何返码不弹 UI
- 三层服务:IActivityIncrementSource + RemoteActivityIncrementSource + RemoteActivityService(沿 46/32)
- GameContext.Activity 启动期 GameApp.StartGameLogic 创建(沿 38/46/32)
- ActivityIds.AccumulatePlayCount=5 常量集中(防漂移 + 自文档化)
- 不抽 OnGameFinished 共用方法(三处单行复制粘贴直观,YAGNI)
- 调用点放防重标记 + 关键存档之后(SaveHighScore/FlushSaveIfDirty)、Close/Show UI 之前
- 业务层只调 GameContext.Activity.IncrementAndLogAsync(...).Forget(),不直引 Fantasy.*(沿 38 §五 IRpcGateway 范式)
- 不做「我的活动进度」UI 入口(当前仅 1 套活动信息密度低,留 O5 Tier 4+)
- 不做多套 Cumulative 类活动接入(简报明示首套样例,留 O6 运营后续逐套刀)

## 验收边界
- E2-E6 完整多局真往返(100 局达标 + mongod 直查 + 邮件投递)留运营 QA,不阻塞本环节
  - 沿设计 48 §7.3 + 47 §八 BLOCKED 边界口径;E1 单局 counter 返回 1 已通过
