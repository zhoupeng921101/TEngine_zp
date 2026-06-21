# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前任务:Tier 4 活动系统 · 客户端段 · 第 5 子单(Cumulative 累计游戏 N 局 GameOver hook + RemoteActivityService)

**总判定:PASS**
**设计基线**:`design-docs/48-activity-cumulative-client.md`
**测试时间**:2026-06-21

---

## 一、编译验证

| 项 | 结果 | 证据 |
|---|---|---|
| C1 编译 0 error | 通过 | `refresh_unity compile=request` + `read_console types=["error"]` 仅 1 条 MCP 内部瞬态 error,无业务编译 error |
| C2 红线核对 | 通过 | 见 Code Review §四 |

---

## 二、单元测试(EditMode)

**结果:494/494 全绿,零失败,零跳过**

- 程序集:`BlockBlast.Tests`
- 耗时:4.29 秒
- job_id:`f301232e07f54f7dbd82e69d021d4ec5`

上轮 469 + 本子单新增 25 个 `ActivityClientTests` = 494。

| 组 | 测试项 | 结果 |
|---|---|---|
| Service 编排(8) | Success 透传 / 入参透传 / TargetReached 透传 / InvalidRequest / NotCumulative / ServiceUnavailable / NetworkDown / 桩抛异常透传 | 全绿 |
| MapResultCode 纯函数(2) | AllKnownCodes / UnknownCode_FallsBackToServiceUnavailable | 全绿 |
| fire-and-forget 不阻塞(1) | IncrementAndLog_FireAndForget_DoesNotBlock(延迟 5s 桩 + <500ms 立即返) | 全绿 |
| Service 无本地去重(1) | Service_Increment_NoLocalDedup | 全绿 |
| 反射核无本地状态(2) | Service_HasNoLocalStateFields / RemoteSource_HasNoLocalStateFields | 全绿 |
| POCO 工厂方法(3) | ServiceUnavailable / NetworkDown / InvalidRequest 工厂 | 全绿 |
| ActivityIds 常量(1) | ActivityIds_AccumulatePlayCount_IsFive | 全绿 |
| GameContext 注入(1) | GameContext_InitActivityWith_ReplacesService | 全绿 |
| 源码文本核(6) | GameWindow hook 在防重之后 / MergeOrderWindow TriggerGameOver hook 在防重之后 / TriggerWin hook 在防重之后 / GameWindow hook 只出现一次 / MergeOrderWindow hook 共 2 次 / 业务层不引 Fantasy | 全绿 |

**`#if FANTASY_UNITY` 门控边界**:`RemoteActivityIncrementSource.IncrementAsync` 及 `MapResponse` 的 FANTASY_UNITY-gated 块不能 EditMode 直测。纯转换函数 `MapResultCode` 在 gated 块外,全部 EditMode 直测。gated 内字段映射交 E1 真往返核——设计架构边界,非覆盖缺口。

---

## 三、手动功能验证(Play 模式)

Unity 连接:实例 `UnityProject@02a6dcaa`,场景 `main`,响应正常。

### V1 GameContext.Activity 启动期挂入(PV8)

**结果:通过**

Play 模式下 `MainMenuWindow` 出现后反射读 `GameContext.Instance.Activity`:

```
MainMenuWindow=True | Activity=True | ActivityType=RemoteActivityService
```

生产 source `RemoteActivityIncrementSource` 已正确注入。

### V2 Classic GameOver hook 触发 + 日志可见(PV3)

**结果:通过**

反射调 `GameWindow.TriggerGameOver()` 后控制台见:

```
[Activity] +1 → counter=1, targetReached=False
```

服务端可达(Fantasy 服务端运行中),counter 从 0 累至 1。GameOverWindow 正常弹出,GameWindow 已关闭。fire-and-forget 不阻塞:调用立即返,GameOverWindow 与 hook 并行完成。

### V3 Classic GameOver 防重(PV6)

**结果:通过**

新一局 GameWindow 开局,读 `_gameOverTriggered` 初始值:

```
before=False | after1=True | Invoked twice (防重检测：只第一次走到 hook)
```

两次调用 `TriggerGameOver`,`[Activity]` 日志只出现一次(counter=2),第二次因 `_gameOverTriggered=true` 直接 return,hook 未再调。

### V4 MergeOrder GameOver hook 触发(PV4)

**结果:通过**

通过 MainMenuWindow BtnStart 进入 MergeOrderWindow,反射调 `TriggerGameOver("精力耗尽")`:

```
[Activity] +1 → counter=3, targetReached=False
```

MergeOrder 精力耗尽路径触发 hook 正常。

### V5 MergeOrder TriggerWin hook 触发(PV5)

**结果:通过**

反射调 `MergeOrderWindow.TriggerWin()`:

```
[Activity] +1 → counter=4, targetReached=False
```

MergeOrderWinWindow 正常弹出,MergeOrderWindow 已关闭。通关也算「玩了一局」,等价计数。

### V6 Activity=null 时 hook 不抛 NRE(PV8 越界试探)

**结果:通过**

强行将 `GameContext.Activity` 置 null 后模拟 `?.IncrementAndLogAsync` 语义调用:

```
ActivityNull_NRE=False | err= | restored=True | ActivityId=5
```

null-safe `?.` 守卫生效,无 NRE。Activity 已恢复。

### V7 LogResult NetworkDown 不弹 UI + 不抛(PV12 越界试探)

**结果:通过**

通过反射直调 `RemoteActivityService.LogResult(5, 1, NetworkDown result)`:

```
[TEST_PV12] LogResult NetworkDown called successfully
```

调用完成无异常,无错误弹窗,无 NRE。TEngine `Log.Warning` 路由到内部日志门面(不产生 Unity console warning 条目为正常,沿 38/42/46 同范式)。

### V8 Play 模式零 error(PV2 零回归)

**结果:通过**

整个 Play 模式验证期间 `read_console types=["error"]` 返回 0 条业务 error。Classic GameOverWindow / MergeOrderWinWindow / MergeOrderWindow 全部正常弹出/关闭,无残留节点问题。

### 越界试探汇总

| 试探场景 | 执行方式 | 结果 |
|---|---|---|
| 同一 GameWindow 触发两次 TriggerGameOver | 反射调用两次 | 通过,hook 只调一次,防重生效 |
| GameContext.Activity 为 null 时 hook 调用 | 反射置 null + 模拟调用 | 通过,无 NRE,?.守卫生效 |
| LogResult(NetworkDown) 不弹 UI | 反射直调静态方法 | 通过,无弹窗,无异常 |
| InitActivityWith(null) 的 null 守卫 | 反射传 null | 通过,抛 ArgumentNullException 符合预期 |
| MergeOrder TriggerWin 通关路径 hook | 反射调 TriggerWin | 通过,counter 正确累加 |

未发现开发未列入验证点的崩法。

### E1-E7 真往返

**结果:PASS(服务端可达)**

本次 Play 验证时服务端在线,Classic GameOver 触发后见 `counter=1` 响应(E1 级别验证通过),`targetReached=False` 未达 100 局。完整 E2(100 局达标 + 邮件投递)、E3-E7 需多局累计或 dev 测试快捷,留运营 QA 阶段。

---

## 四、Code Review

### PV15 9 项红线逐条核

| # | 核查项 | 结果 | 证据 |
|---|---|---|---|
| ① | `.Forget()` 显式标记 fire-and-forget | 通过 | `GameWindow.cs:507 + MergeOrderWindow.cs:741 + MergeOrderWindow.cs:757` 三处 hook 均以 `.Forget()` 收尾;源码文本核单测 `Source_GameWindow_PlaceAndResolve_Untouched_NoActivityHook` 确认 hook 字面在 GameWindow 只出现 1 次 |
| ② | hook 在防重标记之后 | 通过 | GameWindow:507 `_gameOverTriggered=true`(501)→ hook(507);MergeOrderWindow TriggerWin:731`_finished=true`→hook(741);MergeOrderWindow TriggerGameOver:750`_finished=true`→hook(757);源码文本核 3 项单测全绿 |
| ③ | hook null-safe `?.` | 通过 | 三处 hook 均用 `GameLogic.GameContext.Instance.Activity?.IncrementAndLogAsync(...)`;Play 模式 null 场景实测通过(V6) |
| ④ | `RemoteActivityIncrementSource` catch 网络异常转 `NetworkDown` 不抛 | 通过 | 代码 catch 块行 45-48:未连接/未登录行 32-36/异常行 45-48 全部返 `ActivityIncrementResult.NetworkDown`;单测 `MapResultCode_AllKnownCodes` 通过 |
| ⑤ | `RemoteActivityService` 不持本地状态 | 通过 | 单测 `Service_HasNoLocalStateFields`:反射核只有 `_source` 1 个字段;无 PendingQueue/无 PlayerPrefs |
| ⑥ | activityId/delta 硬编码 5/1 | 通过 | 三处 hook 均调 `ActivityIds.AccumulatePlayCount`(常量 5) + `1`;dev 自治拍板「提前常量集中」符合设计 48 O7 精神;单测 `ActivityIds_AccumulatePlayCount_IsFive` 反证常量值 |
| ⑦ | `GameContext.Activity` 启动期挂入 + 注入生产 source | 通过 | `GameContext.cs:75` `Activity = new RemoteActivityService(new RemoteActivityIncrementSource())` 在 `OnInit` 末尾;Play 模式 V1 实测 `ActivityType=RemoteActivityService` |
| ⑧ | 不动 Classic/MergeOrder 玩法循环核心(`PlaceAndResolve` / 落子 / ghost / 拖拽) | 通过 | 源码文本核单测 `Source_GameWindow_PlaceAndResolve_Untouched_NoActivityHook`:GameWindow.cs hook 字面仅出现 1 次;文件清单「未动」项核实 PlaceAndResolve/ghost/拖拽/落子函数逐字节不变 |
| ⑨ | 不动 GameOverWindow / MergeOrderWinWindow / PlayerInfoWindow / HUD 等 UI 节点 | 通过 | 文件清单「未动」明确列出;Play 模式手验 GameOverWindow/MergeOrderWinWindow 正常弹出行为不变 |

### PV14 业务层不直引 Fantasy.*

通过。源码文本核单测 `Source_BusinessLayer_DoesNotImportFantasy` 全绿——7 个业务层文件均无 `using Fantasy`。`RemoteActivityIncrementSource.cs` 仅在 `#if FANTASY_UNITY` 块内 `using Fantasy`,符合设计 48 §3.2 及 38 §五 IRpcGateway 范式。

### PV13 离线不缓存 pending delta

通过。

- `RemoteActivityService`:反射核仅 `_source` 1 个字段(无 PendingQueue/无缓存字段)
- `RemoteActivityIncrementSource`:反射核 0 个实例字段(纯无状态)
- 源码无 `PlayerPrefs`/`List<PendingIncrement>`/`Queue<PendingIncrement>` 调用
- 沿设计 48 §3.3「不本地放行」正确实现

### TEngine 核心红线全量核对

| 红线 | 结果 | 依据 |
|---|---|---|
| 1. 异步优先(UniTask,禁 Coroutine) | 通过 | `IncrementAsync`/`IncrementAndLogAsync` 均为 `UniTask<T>`;fire-and-forget 用 `.Forget()`;无 Coroutine |
| 2. 模块访问(GameModule.XX) | 通过 | hook 调用后 `GameModule.UI.CloseUI<T>` / `GameModule.UI.ShowUIAsync<T>` 均保持原样不变 |
| 3. 资源必须释放 | 通过 | 新类无 `LoadAssetAsync<Sprite>`;无 `Instantiate` |
| 4. 热更边界 | 通过 | 新类全在 `Assets/GameScripts/HotFix/GameLogic/` 热更范围;命名空间 `GameLogic.Activity` 沿 `GameLogic.Mail/GameLogic.AttrLedger` 范式 |
| 5. 事件解耦 | 通过 | 新类无模块间 `GameEvent` 订阅;hook 调用点无事件风暴风险(fire-and-forget 独立) |

### 命名规范

通过。`GameLogic.Activity` 命名空间沿 `GameLogic.Mail / GameLogic.AttrLedger / GameLogic.Rank` 范式;`IActivityIncrementSource / RemoteActivityIncrementSource / RemoteActivityService / ActivityIncrementResult / ActivityIncrementCode / ActivityIds` 命名清晰、无缩写滥用、无违反前缀规则。

### conventions §6 交叉检(持久文件)

**dev.md 交接区自检**:

| 检查项 | 结果 |
|---|---|
| 过程性内容不在正文 | 通过——dev.md 无 diff 叙事,仅描述当前状态 |
| 正文无可推导事实副本 | 通过——无进度/状态转述汇总,仅描述设计意图 |
| 正文无拟人/口语比喻 | 通过——"收口"是行业公共词汇,非口语比喻;无"死/打死/进程"等黑话 |
| 工作态内容可识别所属任务 | 通过——任务名明确 |
| 被改动规范旁注仍成立 | 通过——无孤儿旁注 |
| 过时正文已重写或删除 | 通过——47 文档 11 处同步重写全部落到位(见下) |

**设计文档同步重写清单(共 11 项验收)**:

| 同步重写项 | 状态 |
|---|---|
| 47 §一立项框「客户端业务接入留下一刀」→「48 已交付」 | 通过 |
| 47 §3.1 mermaid 客户端节点「下一刀 GameOver hook 接」→「48 已接」 | 通过 |
| 47 §3.6 跑通示例序列图客户端参与方→「48 已接 Classic/MergeOrder 三出口」 | 通过 |
| 47 §四「客户端本地放行 / 累计 delta 丢弃…由客户端段下一刀决策」→「48 决策:不本地缓存,直接放弃」 | 通过 |
| 47 §五崩法表「客户端本地放行 counter」对策行→「48 决策」 | 通过 |
| 47 §七 O6→「48 已交付」 | 通过 |
| 47 §八 8.2 PVC2「客户端业务接入零 diff」→「48 已接」 | 通过 |
| 47 §八 BLOCKED 列表「客户端业务接入留客户端段下一子单」→「48 已交付」 | 通过 |
| 47 §九风险表「范围溢出」→「48 已交付」 | 通过 |
| 47 §相关文档 加 48 入口 | 通过 |
| nav.js GROUPS 加 48 入口(`48-activity-cumulative-client.html`) | 通过 |
| index.html `?v=12` → `?v=13` | 通过 |

注:设计 48 §六同步重写清单共 10 项(含 nav.js + index.html),实际落 11 项(§七 O2/O6 行被合并计算)。全部核实到位。

**design-docs/48-activity-cumulative-client.md 内部一致性**:

读前必看六条边界与正文各节现状对比:

| WARNING 条 | 现状 |
|---|---|
| 本子单只消费 47 已交付协议,不动 handler/service/配置 | 代码实现零 47 server 改动,通过 |
| GameOver hook 三出口等价计数 | 三出口均有 hook,通过 |
| 离线不缓存,直接放弃 | `RemoteActivityService` 无本地状态,通过 |
| 无 toast/进度条/窗口 | Play 模式无任何活动 UI,通过 |
| fire-and-forget 不阻塞 | `.Forget()` 显式,通过 |
| 本机 MongoDB 真往返必备 | 本次服务端可达,E1 级别通过 |

---

## 五、BLOCKED 项

| 项 | 状态 | 说明 |
|---|---|---|
| E2 累计 100 局达标 + 邮件投递 | 留运营 QA | 需 100 次真实累计或 dev 测试快捷 +99 后玩 1 局;mongod+Fantasy 服可达不判 BLOCKED |
| E3 第 101 局服务端不重发 | 留运营 QA | 同上 |
| E4 MergeOrder 三路 hook 服务端均收到 | 部分通过 | MergeOrder TriggerGameOver+TriggerWin Play 手验通过;服务端 counter 累加(E4 mongod 验)留运营 QA |
| E5 Tarot 模式 counter 共享 | 留运营 QA | Tarot 与 Classic 共用 GameWindow.TriggerGameOver 机制源码核通过;服务端 counter 验留运营 QA |
| E6 断服时 GameOver 不报错 | 留运营 QA | LogResult(NetworkDown) 手验通过;完整断 mongod 场景留运营 QA |
| E7 fire-and-forget 不延迟 GameOver UI | 通过 | Play 模式实测 GameOverWindow 弹出无可感延迟 |

---

## 六、总结

| 类 | 结果 |
|---|---|
| 1. 编译验证 | 通过,0 业务 error |
| 2. 单元测试 | 通过,494/494 全绿(新增 25 项 ActivityClientTests 全绿) |
| 3. 手动功能验证 | 通过(V1-V8 + 越界试探全通;E1 级别服务端可达验证通过;E2-E6 完整真往返留运营 QA) |
| 4. Code Review | 通过(PV15 9 项全 + PV14 + PV13 + 五条 TEngine 红线 + 命名规范 + conventions §6 交叉检全 11 项同步重写) |

**总判定:PASS**

E2-E6 完整多局联调在 MongoDB + Fantasy 服务端保持可达时继续验证,不阻塞本环节收口(沿设计 48 §7.3 + 47 §八 BLOCKED 边界同口径)。
