# plan 状态

## 当前任务:Tier 4 活动系统 · 第 5 子单(Cumulative 客户端 GameOver hook + RemoteActivityService · 客户端段收口)

**类型**:全栈特性 · Tier 4 第 5 子单 · **客户端段(收口 Cumulative 节律)**(协议契约 + handler + service 在 47 已交付,本子单建客户端数据源接缝 + 远程实现 + 服务编排 + 三处 GameOver 出口 fire-and-forget hook + `GameContext.Activity` 挂入;无 UI 投放、无 toast、无离线缓存;服务端零改、47 协议契约零改)。
**设计稿**:[`design-docs/48-activity-cumulative-client.md`](../../design-docs/48-activity-cumulative-client.md)(新增)。
**承接基线**:[47 Tier 4 第 4 子单 PASS](../../design-docs/47-activity-cumulative.md)(`§3.2` 协议契约 + `§3.3` handler 校验链 + `§3.5` service 行为契约 + `§3.6` 跑通示例 + `§3.4` 累计游戏 100 局活动配置 + `§4` 服务异常表声明客户端不可达决策留下一刀;协议生成物已交付 PVC1) + [46 Tier 2 第 4 子单 PASS](../../design-docs/46-player-attr-ledger-client.md)(`§3.2` `IAttrLedgerSource` 接缝 + 生产实现 + `§3.3` `RemoteAttrLedgerService` 编排范式 + `GameContext.AttrLedger` 挂入范式;本子单沿其编排范式) + [32 RemoteMailService](../../design-docs/32-mail-server.md)(`§3.4` 失败兜底范式) + [38 IRpcGateway](../../design-docs/38-player-attr-client.md)(`§五` HotFix 业务层不直引 Fantasy.* 范式) + [40/41 EVENT 同范式无 UI 反馈](../../design-docs/40-event-unlock-relay.md)(服务端达标自动投活动邮件,客户端从邮件领奖,无额外 toast) + Fantasy `be2b3b51`(server handler + service 收口) + 客户端 GameOver 现状(grep:`GameWindow.TriggerGameOver` Classic + `MergeOrderWindow.TriggerGameOver(title)` 双 GameOver 路径共用 + `MergeOrderWindow.TriggerWin(lines)` 通关)。
**目标**:在 47 已交付的协议契约 + handler + service + 累计 100 局样例配置之上,新建客户端三件:① 数据源接缝 `IActivityIncrementSource` + 生产实现 `RemoteActivityIncrementSource`(经 38 `IRpcGateway` 范式调 `C2G_ActivityIncrement`);② 客户端服务 `RemoteActivityService` 编排层(沿 46 / 32 范式,挂入 `GameContext.Activity`,启动期 `GameApp.StartGameLogic` 创建实例);③ 在 Classic + MergeOrder 三处 GameOver 出口(GameWindow.TriggerGameOver / MergeOrderWindow.TriggerGameOver / MergeOrderWindow.TriggerWin)加 fire-and-forget 调用 `IncrementAsync(5, 1)` + POCO `ActivityIncrementResult` + 错误码归一 5 档枚举 `ActivityIncrementCode`(Success / InvalidRequest / NotCumulative / ServiceUnavailable / NetworkDown,沿 46 + 加 NotCumulative)。**收口** Cumulative 节律全栈通路(47 server 段 + 48 客户端段合为 Tier 4 第 4 + 5 子单一刀闭环)。客户端业务接入留 Tier 4+ 运营加多套 Cumulative 活动后逐套刀。

### 交接给 dev 的验收清单(行为级,test 可逐条核对)

> 详细完成定义、POCO 字段集、错误码语义、GameOver hook 落点、fire-and-forget 实现要点、整局走查 / 崩法 / 失败模式 / 诚实边界全在设计稿 [§3](../../design-docs/48-activity-cumulative-client.md)–[§4](../../design-docs/48-activity-cumulative-client.md),本节只列条目骨架,test 据此一一核。

**核心架构(本子单关键)**

- **PV1 Unity 编译过**:新增 `IActivityIncrementSource` / `RemoteActivityIncrementSource` / `RemoteActivityService` / `ActivityIncrementResult` / `ActivityIncrementCode` 类编译过;`GameContext.Activity` 字段新增;Classic GameWindow / MergeOrder MergeOrderWindow 三处 hook 调用行加入但编译过(沿 [38 / 46 客户端段范式](../../design-docs/46-player-attr-ledger-client.md))
- **PV2 既有客户端段零回归**:Classic GameWindow / MergeOrder MergeOrderWindow / GameOverWindow / MergeOrderWinWindow / 38 PlayerAttrService / 42 HUD 三属性绑定 / 25 PlayerInfoWindow / 27 主玩法 HUD / 46 我的流水窗 / 32 邮件窗 / 22 排行榜窗 EditMode + PlayMode 测试全 pass;Classic 一局完整玩(开局 → 落子 → GameOver → GameOverWindow 弹出 → 再来一局)行为不变;MergeOrder 一局完整玩(精力耗尽 / GAME OVER / 通关)行为不变;改名扣钻 + HUD 三属性显示正常
- **PV3 Classic GameOver 触发 hook 调用 + 日志可见**:Classic 玩到棋盘无法继续 → GameWindow.TriggerGameOver 触发 → Unity 控制台见 `[Activity] +1 → counter={N}, targetReached={bool}`(若服务端可达)或 `[Activity] ServiceUnavailable activity=5 — 本次累计丢弃` / `[Activity] NetworkDown activity=5 — 本次累计丢弃`(若不可达);GameOverWindow 正常弹出**无额外延迟**(fire-and-forget 不阻塞)
- **PV4 MergeOrder 软 / 硬 GameOver 触发 hook 调用**:MergeOrder 精力耗尽 / GAME OVER 触发 TriggerGameOver(title) → 控制台见 `[Activity] +1 → ...` 日志;GameOverWindow 正常弹出无额外延迟;**两路共用同一函数 hook 加一处覆盖**
- **PV5 MergeOrder 通关触发 hook 调用**:MergeOrder 玩到通关 → TriggerWin 触发 → 控制台见 `[Activity] +1 → ...` 日志;MergeOrderWinWindow 正常弹出无额外延迟
- **PV6 同一局 GameOver hook 只调一次**(防重):Classic GameOver 既有 `_gameOverTriggered` / MergeOrder `_finished` 防重标记守 → hook 在防重之后调,只调一次;EditMode 单测可注入 `FakeActivityIncrementSource` 计数 IncrementAsync 调用次数,验同一局触发两次时 hook 调用次数仍为 1
- **PV7 Tarot / Classic 不分别计数**:Tarot 玩一局 GameOver → 控制台见 `[Activity] +1` 一次;Classic 玩一局 GameOver → 同样 +1 一次;两模式连续玩 → 服务端 counter +2(若可达);沿 [26 tarot_mode HUD = Classic GameWindow 再主题](../../design-docs/26-tarot-mode-hud-art.md) 共用实例
- **PV8 `GameContext.Activity` 启动期挂入 + null-safe**:GameApp.StartGameLogic 完成后 `GameContext.Instance.Activity` 非 null;hook 调用守 `if (GameContext.Instance?.Activity != null)` 不抛
- **PV9 fire-and-forget 错误码归一 + 各级日志**(EditMode 单测):注入 `FakeActivityIncrementSource` 返各档(Success / InvalidRequest / NotCumulative / ServiceUnavailable / NetworkDown / throw NetworkException) → 验:① 各码对位日志级别(Success/Info,InvalidRequest+NotCumulative/Error 代表 bug,ServiceUnavailable+NetworkDown/Warning 是环境抖动);② 抛网络异常 catch 转 NetworkDown 不抛;③ 任何返码都不弹 UI;④ 不重试(单次调用)
- **PV10 fire-and-forget 不阻塞 GameOver 流程**:EditMode 单测:注入延迟 5s 返响应 → TriggerGameOver 触发 hook → GameOver 后续流程(Save / SaveHighScore / CloseUI / ShowGameOverWindow)立即执行不等响应;PlayMode 手验秒级弹窗
- **PV11 ServiceUnavailable 兜底 + 无 UI 反馈**:注入返 ServiceUnavailable → 日志见 Warning「本次累计丢弃」;屏幕**无**错误窗 / toast / 进度条等任何活动相关 UI;玩家完全无感
- **PV12 NetworkDown 兜底 + 无 UI 反馈**:注入抛 NetworkException / 模拟 Session=null → catch 转 NetworkDown;日志见 Warning;屏幕**无**错误窗
- **PV13 离线不缓存 pending delta**(Code Review + 反证):核 `RemoteActivityService` / `RemoteActivityIncrementSource` 无任何持久化字段 / `PlayerPrefs` 调用 / 元层 save 字段加;无 `Queue<PendingIncrement>` / `List<PendingIncrement>` 类似数据结构;无「失败重试」「网络恢复后重发」逻辑
- **PV14 HotFix 业务层不直引 Fantasy.\***:Code Review 核 `using Fantasy` / `using Fantasy.*` 不出现在 `RemoteActivityService.cs` / `IActivityIncrementSource.cs` / `GameWindow.cs` / `MergeOrderWindow.cs` 等业务层文件;`RemoteActivityIncrementSource.cs` 可经 `IRpcGateway` 范式间接调 Fantasy(沿 [38 §五](../../design-docs/38-player-attr-client.md))
- **PV15 Code Review 9 项**:① fire-and-forget 调用用 `.Forget()` 显式标记避免 UniTask warning;② hook 调用在既有防重标记之后(`_gameOverTriggered` / `_finished` 之后);③ hook null-safe(`if (GameContext.Instance?.Activity != null)` 守卫);④ `RemoteActivityIncrementSource` catch 网络异常转 `NetworkDown` 不抛;⑤ `RemoteActivityService` 不持本地状态(无缓存 / 无 pending 队列,守 47 §四 不本地放行);⑥ activityId / delta 在 hook 处硬编码(本子单 5 / 1,不传变量);⑦ `GameContext.Activity` 启动期 GameApp.StartGameLogic 创建实例 + 注入生产 source;⑧ 不动 Classic / MergeOrder 玩法循环核心逻辑(PlaceAndResolve / CollectClearedElements / 落子 / ghost / 拖拽);⑨ 不动 GameOverWindow / MergeOrderWinWindow / PlayerInfoWindow / HUD 等任何 UI 节点

**联调段验收(E)— 真往返**

- **E1 真往返:Classic 玩一局后服务端 counter +1**:起服(mongod + Fantasy.Net)+ 客户端登录 → Classic 玩一局到 GameOver → 控制台见 `[Activity] +1 → counter=1, targetReached=false`;mongod 探针验 `activity_progress` 集合 `{account}_5` 文档 `counter=1`
- **E2 真往返:累计 100 局达标 + 邮件可见**:玩 100 局(或 dev 加测试快捷:1 局 +100)→ 第 100 局响应 `targetReached=true` 见日志 `[Activity] 累计达标 activity=5`;mongod `activity_progress` counter=100, lastClaimedCycleKey=1;mongod `mails` 集合该账号收件箱多一封活动邮件挂 reward=5005;客户端 32 邮件窗拉列表见活动邮件 → 领取 → 若含钻石联动 38/42/46 ledger
- **E3 真往返:第 101 局服务端不重发**:E2 之后第 101 局 → counter=101, targetReached=false;mongod `mails` 集合活动邮件仍只 1 封
- **E4 真往返:MergeOrder 三路 hook 服务端均收到**:MergeOrder 精力耗尽 / 通关各玩一局 → counter +2
- **E5 真往返:Tarot 模式与 Classic 共享 counter**:Tarot 一局 + Classic 一局 → counter +2(同账号同 activityId)
- **E6 真往返:断服时 GameOver 不报错 + counter 不动 + 玩家无感**:停 mongod → GameOver → Warning「ServiceUnavailable」/「NetworkDown」;**无**异常 / 错误窗;GameOverWindow 正常弹出
- **E7 真往返:fire-and-forget 不延迟 GameOver UI**:GameOver → GameOverWindow 秒级弹出(典型 <100ms)

### 同步重写清单(本任务内已完成的他篇覆盖式重写,sweep 范围闭合)

- **[47 §一立项框「客户端业务接入(GameOver hook)留下一子单」](../../design-docs/47-activity-cumulative.md)** 覆盖式重写为「**48 客户端段已交付**(Classic GameWindow + MergeOrder 三出口 hook + `RemoteActivityService` + 离线丢弃 + 无 UI 反馈沿 EVENT 同范式)」(立项框 5 处:类型 / 方向约束 / 需求降层 a / 范围 / 读前必看)
- **[47 §3.1 节律入口形态对照表](../../design-docs/47-activity-cumulative.md)** mermaid 图 C1 节点「下一刀接」 → 「48 已接 Classic / MergeOrder 三处 GameOver 出口」
- **[47 §3.2 错误码集旁注](../../design-docs/47-activity-cumulative.md)** 「客户端段下一刀的 UI 可据 NotCumulative 显示」覆盖式重写为「客户端 48 据此码落 Error 级日志」
- **[47 §3.6 跑通示例图示「下一刀 GameOver hook 接」](../../design-docs/47-activity-cumulative.md)** 覆盖式重写为「48 已接(Classic + MergeOrder 三出口)」+ 玩家可观测路径「(可选)客户端 UI 弹『累计 100 局达成!』提示(本子单不做、留下一刀)」改为「客户端落日志(48 决策无 UI 反馈,沿 EVENT 同范式)」
- **[47 §四 服务异常表](../../design-docs/47-activity-cumulative.md)** 「客户端不本地放行(累计 delta 丢弃 / 缓存重试由客户端段下一刀决策)」覆盖式重写为「48 决策累计 delta 直接丢弃不缓存,沿 30/32/46 同口径」
- **[47 §五崩法表「服务端不可达 → 客户端本地放行」对策行](../../design-docs/47-activity-cumulative.md)** 覆盖式重写指向「48 §3.3 决策」
- **[47 §六关系表 11/29 行](../../design-docs/47-activity-cumulative.md)** 「(GameOver hook 接入留下一刀)」覆盖式重写为「(在 48 已落)」
- **[47 §七 O2 错误码](../../design-docs/47-activity-cumulative.md)** 「客户端段下一刀按 UI 反馈需要扩」覆盖式重写为「客户端 48 已沿 4 档 + 加 NetworkDown 区分客户端断网」
- **[47 §七 O6 客户端 GameOver hook 接入 + UI 反馈 + 离线缓存策略](../../design-docs/47-activity-cumulative.md)** 整行从「**不在本子单**:留下一子单」覆盖式重写为「**48 已交付**(客户端段)」+ 备选列写明 48 四项决策(三处出口等价计数 / 无 UI 反馈 / 不缓存丢弃 / 不直引 Fantasy.*)
- **[47 §八 8.2 PVC2 客户端业务接入零 diff](../../design-docs/47-activity-cumulative.md)** 覆盖式重写为「客户端业务接入已交付(见 48)」
- **[47 §八 BLOCKED 列表](../../design-docs/47-activity-cumulative.md)** 「客户端业务接入(GameOver hook 调 RPC)+ UI 反馈 + 离线缓存策略」覆盖式重写为「**48 已交付**(客户端段)」+ 「客户端段对 NotCumulative 错误码的差异化提示」覆盖式重写为「48 §3.1 落 Error 级日志」
- **[47 §五诚实边界「本子单不守的」第①②⑤项](../../design-docs/47-activity-cumulative.md)** 客户端业务接入 / 离线缓存 / 客户端 UI 反馈三项覆盖式重写指向 48
- **[47 §九风险表「服务端不可达客户端本地放行 counter」+「范围溢出做客户端 GameOver hook 接入」对策行](../../design-docs/47-activity-cumulative.md)** 覆盖式重写指向 48 §3.3 / 已落
- **[47 §相关文档](../../design-docs/47-activity-cumulative.md)** 加 48 入口
- **[`design-docs/assets/nav.js`](../../design-docs/assets/nav.js)** GROUPS 在 47 之后加 48 入口(`href: '48-activity-cumulative-client.html'`,tag「全栈 · Cumulative 节律客户端段(收口)」,title / desc 详尽度沿 41 / 46 同口径)
- **[`design-docs/index.html`](../../design-docs/index.html)** `?v=12` → `?v=13` 递增防缓存

> [!NOTE]
> **39 / 40 / 41 / 43 / 11 / 29 / 32 / 38 / 42 / 46 设计稿无需改动**
>
> - 39 / 40 / 43:Login 类活动批量 / EVENT 解锁与本子单 Cumulative 节律正交(zero overlap)
> - 41:EVENT 客户端段,与本子单正交(EVENT 是 Login,本子单是 Cumulative)
> - 11 / 29:GameOver 三出口语义层不动,本子单只是在出口加 hook
> - 32:邮件投奖通路完全沿用,签名零改
> - 38:`PlayerAttrService` 核心契约不动,`IRpcGateway` 范式沿用(可能加 `CallActivityIncrementAsync` 方法或直接复用通用 Call 入口,dev 据工程现状取)
> - 42:HUD 三属性绑定不动
> - 46:我的流水通路与本子单正交,服务编排同范式但不耦合

### decisions(本环节自治拍板的取舍)

- **三处 GameOver 出口等价计数:Classic GameWindow.TriggerGameOver + MergeOrder MergeOrderWindow.TriggerGameOver(双 GameOver 路径共用) + MergeOrder MergeOrderWindow.TriggerWin** → 取此默认。理由:① grep 工程现状确认三处是「玩了一局」的全部出口(精力耗尽 + GAME OVER 共用 `TriggerGameOver(title)` 函数,加 hook 一处两路覆盖);② 通关也算「玩了一局」否则「玩得越好越没奖」反直觉;③ 沿 [29 §三 三出口](../../design-docs/29-gameplay-fusion.md) 语义,三出口对玩家视角等价。可逆(若简报有「只算 GameOver 不算通关」类要求另开)。
- **Tarot / Classic 模式不分别计数** → 取此默认。理由:① Tarot 模式与 Classic 复用同一 GameWindow 实例(沿 [26 tarot_mode HUD = Classic GameWindow 再主题](../../design-docs/26-tarot-mode-hud-art.md));GameWindow.TriggerGameOver 调一次 hook = 1 局,与玩法模式正交;② 累计活动语义本身就是「玩了 N 局游戏」,不区分玩法子模式;③ 若运营未来想分模式(如「累计 50 局 Tarot」)走 [O7 ActivityIds 常量类 + 业务出口判区](../../design-docs/48-activity-cumulative-client.md::open) 新增 activityId,本子单不做(简报无此要求)。可逆。
- **离线 / 服务不可达策略:不缓存 pending delta,直接放弃本次计数** → 取此沿 30/32/46 同口径。理由:① 沿 [30 兑换码](../../design-docs/30-redeem-code-server.md) / [32 邮件领奖](../../design-docs/32-mail-server.md) / [46 ledger query](../../design-docs/46-player-attr-ledger-client.md) 「不本地放行」同范式;② 本地放行 = 服务端复连后丢失推送 = 审计裂缝(沿 47 §四 不本地放行决策);③ 本地缓存需服务端补 idempotency key 字段(违 47 协议不改硬约束)+ 持久化 + 重复防护 + 失败重试退避,复杂度大、收益小(漏一局 / 100 局 = 1% 进度玩家无感);④ Cumulative 类「玩家少一格进度」类窄窗,与 [22 排行榜断服回退本地源](../../design-docs/22-rank-system.md) 「无超发风险可降级」**不同源**——本子单是服务端权威 counter,本地放行致审计裂缝,**不可降级**;⑤ 真做替代留 Tier 4+ 走 O3 PendingIncrements + 需服务端补 idempotency。可逆。
- **UI 反馈:无 toast、无进度条、无窗口** → 取此沿 EVENT 同范式。理由:① 沿 [40 / 41 EVENT 解锁](../../design-docs/40-event-unlock-relay.md) 「服务端达标自动投活动邮件,客户端从邮件领奖,无额外达标 toast」同范式;② GameOver 时玩家关注「再来一局 / 返回主菜单」,toast 抢焦点;③ 累计 100 局是长期成就(13-25 天达 1 次),低频不值新建 UI;④ 邮箱 badge 因新邮件自动刷新,玩家自然能看到;⑤ 「99/100 还差 1 局」类进度条文案是变现诱导(违去变现守不变量);⑥ 离线 fire-and-forget 失败仅落日志玩家无感,加 toast = 离线时「活动累计失败」给玩家造成「我刚玩的不算了」焦虑。可逆(O4 备选最简 toast,留 Tier 4+ 真有运营需求时另开)。
- **fire-and-forget 不阻塞 GameOver 流程** → 取此默认。理由:① hook 调用不 `await`,GameOver 后续流程(Save / SaveHighScore / CloseUI / ShowGameOverWindow)立即执行;② 慢网时 await 会让玩家 GameOver 卡住 5-30s 体验劣化;③ 沿 UniTask 标准 fire-and-forget 范式(`.Forget()` 显式标记避免编译警告);④ 响应回来仅落日志,不弹窗 / 不重试 / 不缓存;⑤ 若玩家在响应未回时关闭 App,本次推送在 OS 层取消(无持久化、无重试,沿不缓存决策)。可逆(违此 = await 慢网卡 GameOver,劣体验)。
- **错误码客户端处理 5 档分级:Success/Info, InvalidRequest+NotCumulative/Error, ServiceUnavailable+NetworkDown/Warning** → 取此默认。理由:① InvalidRequest / NotCumulative 代表「客户端 bug 或配置缺」(理论上正常流量不触发),Error 级让 QA 快速发现异常;② ServiceUnavailable / NetworkDown 是环境抖动非 bug,降到 Warning 避免噪音;③ Success 落 Info(便于 QA 验证达标);④ 任何返码都不弹 UI(沿无 UI 反馈决策);⑤ 沿 [46 AttrLedgerQueryCode](../../design-docs/46-player-attr-ledger-client.md) 范式补 `NotCumulative` 一档。可逆(O10 备选全 Info / 全 Warning,本子单不取)。
- **`IActivityIncrementSource` 接缝 + `RemoteActivityIncrementSource` 生产实现 + `RemoteActivityService` 编排层 三层结构** → 取此沿 46 / 32 范式。理由:① 沿 [46 IAttrLedgerSource + RemoteAttrLedgerSource + RemoteAttrLedgerService](../../design-docs/46-player-attr-ledger-client.md) / [32 IRemoteMailSource + RemoteMailSource + RemoteMailService](../../design-docs/32-mail-server.md) 三层范式;② 接缝层方便 EditMode 单测注入 `FakeActivityIncrementSource` 验编排逻辑;③ 服务层保留便于 Tier 4+ 加交叉编排 / 批量上报 / 重试;④ 业务层(GameWindow / MergeOrderWindow)只调 `GameContext.Activity.IncrementAsync(5, 1)`,不直引 source / Fantasy.*。可逆(违此 = 业务层直 new RPC 调用,违 38 §五 IRpcGateway 范式 + 单测无法注入)。
- **`GameContext.Activity` 启动期挂入**(沿 38 / 46 / 32 范式)→ 取此默认。理由:① 沿 [38 GameContext.PlayerAttr](../../design-docs/38-player-attr-client.md) / [46 GameContext.AttrLedger](../../design-docs/46-player-attr-ledger-client.md) / [32 GameContext.Mail](../../design-docs/32-mail-server.md) 同范式;② GameApp.StartGameLogic 启动时同步创建 `GameContext.Activity = new RemoteActivityService(prodSource)`,启动后立即可用;③ hook null-safe 守 `if (GameContext.Instance?.Activity != null)` 防早期 GameOver 异常触发。可逆。
- **activityId / delta 在 hook 处硬编码(本子单 5 / 1,不传变量)** → 取此默认。理由:① 本子单只接入 1 套 Cumulative 活动(累计 100 局),硬编码可读;② 未来加同类 Cumulative 活动走 [O7 ActivityIds 常量类](../../design-docs/48-activity-cumulative-client.md::open) 集中常量,本子单不前瞻预设;③ delta=1 是「玩了一局」语义,无需变量;④ 沿 [42 HUD 三属性绑定](../../design-docs/42-tarot-hud-player-attr-bind.md) 「单点硬编码 + 后续运营加多套时统一集中」范式。可逆。
- **不做「我的活动进度」UI 入口** → 取此默认。理由:① 当前 Cumulative 类活动只有 1 套(累计 100 局),信息密度极低不值新建窗口;② 玩家发现达标走邮箱 badge 即可;③ 需服务端加「查询 counter」协议(47 无此协议)+ UI 投放设计,工作量大收益小;④ 留 [O5 后续](../../design-docs/48-activity-cumulative-client.md::open) 当运营加多套 Cumulative 后做集中展示窗。可逆。
- **不做多套 Cumulative 类活动接入(累计交付 / 累计消费 / 累计获得 / 累计签到)** → 取此沿简报硬约束。理由:① 简报明示「本子单 = 累计游戏 N 局 GameOver hook」首套样例;② 其它套等运营定具体需求 + 业务出口接;③ 本子单 `RemoteActivityService` 已是归一服务,后续套用 `IncrementAsync(newId, 1)` 即可零代码改;④ 留 [O6 后续](../../design-docs/48-activity-cumulative-client.md::open)。可逆。
- **HotFix 业务层不直引 Fantasy.\***(沿 38 §五 IRpcGateway 范式)→ 取此守 38 PV14 约束。理由:① `RemoteActivityIncrementSource` 经 `IRpcGateway` 间接调 Fantasy(38 §五 范式);② 业务层 GameWindow / MergeOrderWindow 只调 `GameContext.Activity.IncrementAsync(5, 1)`,不引 source / Fantasy.*;③ `IRpcGateway` 是否需扩 `CallActivityIncrementAsync` 方法或直接复用现有「通用 Call」入口,dev 据工程现状取;④ Code Review PV14 拦。不可逆(违此 = HotFix 与 Fantasy 反热更新边界)。
- **既有防重标记(`_gameOverTriggered` / `_finished`)守 hook 每局只调一次,hook 调用在防重之后** → 取此守同账号防重。理由:① 既有 Classic GameWindow.TriggerGameOver 已有 `_gameOverTriggered` 守 GameOver 只处理一次,hook 加在防重之后 = hook 也只调一次;② 既有 MergeOrder MergeOrderWindow.TriggerGameOver / TriggerWin 已有 `_finished` 守,同样;③ 服务端 47 SV8 已验同账号并发原子幂等(`$inc` 文档级原子),即便客户端漏防重双推服务端 counter 也正确,但客户端日志会混乱;④ Code Review PV15 ② 核「hook 在防重之后」。可逆。
- **`_gameOverTriggered` / `_finished` 等防重标记沿用既有不改名 / 不改语义** → 取此沿 conventions §6 「misnomer 不改」类范式守不变量。理由:① 现有玩法循环已用此防重,改名 / 改语义 = 牵连 GameOver 状态机回归;② 本子单只读不写,加 hook 在防重之后;③ Code Review PV15 ⑧ 核「不动 PlaceAndResolve / 落子 / ghost / 拖拽 等核心逻辑」。可逆。
- **不动 Classic / MergeOrder 玩法循环核心**(PlaceAndResolve / CollectClearedElements / 落子 / ghost / 拖拽 / 棋盘判定)→ 取此沿 boss 硬约束 + [设计 27 / 29 玩法循环零回归](../../design-docs/29-gameplay-fusion.md) 同范式。理由:① 本子单只在 TriggerGameOver / TriggerWin 函数内加一行 fire-and-forget;② 不改其它函数;③ Code Review PV15 ⑧ 核;④ PV2 既有玩法循环零回归。不可逆。
- **`RemoteActivityService` 不持本地状态(无缓存 / 无 pending 队列)** → 取此守服务端独占审计完整性。理由:① 沿 47 §四 不本地放行 + 46 §3.3 关键约束「客户端不持本地副本」同口径;② 本地状态 = 客户端可篡改的伪 counter,与服务端 counter 漂移;③ 切账号时不重建实例(沿 GameContext 单例),但下次调时身份从会话取(47 SV12 ① 守),内容自动对位新账号;④ PV13 反证 Code Review 核「无 PendingQueue / 无 PlayerPrefs」。可逆(违此 = 本地缓存致服务端权威漂移)。
- **服务端段 47 / 协议 / handler / service / 配置零改** → 守 boss 硬约束。理由:① 简报明示「不动 47 协议契约 / 服务端 handler」;② 47 协议生成物已在 47 PVC1 验过;③ 本子单纯客户端段;④ 若 dev 落地时发现 47 协议字段需要调整 = 47 协议 bug,联调 E1 拦,plan 不替 server-dev 拍代码改动决策。不可逆。
- **「Plan 不读工程代码」原则下,以 47 / 46 / 32 / 38 规范层 + 行为级 service / source / hook 语义声明为基线** → 取此判据。理由:plan 红线「不读工程源码、不 grep 符号」(本子单作了少量必要的 grep:Classic / MergeOrder GameOver 触发点核实是为「行为级 hook 落点决策」依据,非「指代码定位」);本设计稿以「按规范声明的应有实现」为基线(46 IAttrLedgerSource + RemoteAttrLedgerService 编排 + 32 RemoteMailService + 38 IRpcGateway 范式是规范层声明,实际代码符号 / 类名 / 方法签名由 dev 据工程现状取);dev 落地时若发现现状与规范不一致按 PV15 Code Review 处理。

### blockers

(无)

### 自检(plan 收尾,conventions + 设计自检)

- 需求降层 a/b/c 三行齐全且 b 答得出(立项框 a.表层 = 接 47 客户端业务 + GameOver hook + 离线 / UI plan 拍板;b.底层 = 让 47 已交付的 Cumulative 节律真实跑起来 + 为后续 4 套 Cumulative 类活动铺接入范式 + 验证 47 反作弊三层防御真客户端流量下不破 + 完成 Tier 4 第 4 + 5 子单合刀闭环;c.列方案 A/B/C/D 对比,3 个备选否的理由具体到「方案 B 违 38 §五 IRpcGateway 范式 + 单测无法注入 + DRY」「方案 C 本地累计需持久化 + 重复防护 + 失败重试复杂度大」「方案 D 服务端无玩法上下文判区 + 反层级」),c 未发现更直达做法 ✔
- 整局走查每个机制附一行崩法(§4 表覆盖 13 类崩法 + 各对策:同一局 GameOver 触发两次 / Tarot 与 Classic 双推 / 通关被漏接 / MergeOrder 双 GameOver 路径漏接一路 / GameContext.Activity 启动期未挂 NRE / 网络断 Session 未建 / 服务端 MongoDB 抖 / 玩家关 App 响应未回 / 玩家连开两局并发 / 长时间离线大量游戏 / 客户端传错 activityId / delta 异常 / 协议字段缺 / `.Forget()` 漏标 warning)✔
- 向上对体验:① 玩家(无感后台累计 + 邮箱 badge 自然通知 + GameOver 焦点不被抢);② 运营(后续 4-5 套 Cumulative 类活动可零代码改加 = 沿本子单范式 + 47 service 已建);③ 工程(Tier 4 第 4 + 5 合刀闭环 + Cumulative 节律全栈通路真实可用 + 反作弊三层防御真客户端流量下验证);④ 反作弊(运营 / QA 可观察 NotCumulative 拒数 = 0 = 正常无攻击;客户端 hook 加防重守同账号每局只推一次 + 客户端不本地放行守审计完整性) — 四向对齐 ✔
- 范围闸:核心 / 增强 / 可砍三档已标(§八 待拍板 11 档);砍掉「后续」档(O4 UI 反馈 / O5 我的活动进度 UI / O6 多套 Cumulative 接入 / O7 activityId 常量集中)后核心循环「客户端玩一局 → fire-and-forget RPC → 服务端 counter +1 → 累 100 局达标 → 投邮件 → 玩家邮箱领奖」仍成立 ✔
- 语体扫描 `grep -nE '钉死|焊死|绑死|打死|死在' design-docs/48-activity-cumulative-client.md` 零命中 ✔
- 同步重写清单 sweep 闭合(本节已列 15 处覆盖 47 立项框 / §3.1 / §3.2 / §3.6 / §四 / §五 / §六 / §七 / §八 / §九 / §相关文档 + nav.js + index.html 缓存版本);设计稿 conventions §6「重写不加勘误」遵守:47 全是覆盖式重写,无叠勘误注 ✔
- 设计稿正文与代码解耦:PV / E 验收点是行为级(三处 GameOver 出口 + fire-and-forget + 错误码归一 + 不缓存等),代码符号 / 文件 / 行号 / 类名 / 方法签名是行为级符号设计(`IActivityIncrementSource` / `RemoteActivityIncrementSource` / `RemoteActivityService` / `GameContext.Activity` / `IncrementAsync` 是行为级符号,具体命名空间 / 方法签名由 dev 据工程现状取,沿 46 / 38 / 32 同范式);**plan 必要的少量代码 grep** 是为 hook 落点决策(GameWindow.TriggerGameOver / MergeOrderWindow.TriggerGameOver / TriggerWin 三处),不指 dev 实现 ✔
- UI 投放边界声明:本子单**纯客户端业务 hook 接入**,**无 UI 投放**(沿无 UI 反馈决策);唯一可观测路径 = GameOver → fire RPC + 日志可见 + 真往返达标后邮件可见;设计稿 + 交接区显式声明 ✔
- 全栈服务端化三段切分覆盖(plan memory §1):本子单 = **客户端段(收口)** + **联调段**(E1-E7 真往返);**服务端段** + 客户端协议生成物在 47 已落 ✔
- 设计自检:fire-and-forget 不阻塞 GameOver 流程(claim-then-act 反向:GameOver 先关键流程后 hook)、防重(`_gameOverTriggered` / `_finished` 沿用)、null-safe(`GameContext.Activity` 启动期挂入 + hook 守卫)、不持本地状态(沿服务端独占)、HotFix 业务层不直引 Fantasy.*(沿 38 §五)、UI 反馈无(沿 EVENT 同范式)、错误码归一(5 档分级日志)✔
- carry-forward 不积新:无新增 carry-forward;46 § 二 NOTE 提到的 38 设计稿 Gold / Diamond / Energy 与代码 Coin / Diamond / Stamina 命名漂移**不在本子单范围**(本子单不动属性枚举),仍属未来 dev / plan 后续清理 ✔
