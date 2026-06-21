# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:Tier 4 活动系统 · 客户端段 · 第 5 子单(Cumulative 累计游戏 N 局 GameOver hook + RemoteActivityService · 客户端段收口)

**设计基线**:`design-docs/48-activity-cumulative-client.md`(plan 交接区已列 PV1-PV15 + E1-E7 行为级验收骨架)。

### 改动摘要

兑现 48 客户端段:在 47 已交付的协议契约 + handler + service + 累计 100 局样例配置 + 客户端协议生成物之上,落 Cumulative 节律客户端段闭环。新建独立命名空间 `GameLogic.Activity`(沿 32 `GameLogic.Mail` + 46 `GameLogic.AttrLedger` 范式),含 5 项纯逻辑文件(POCO + 枚举 + 接缝 + 远程实现 + 编排层)+ 1 项常量集中(ActivityIds);`GameContext.Activity` 启动期挂入(沿 38/46/32 同范式);Classic GameWindow.TriggerGameOver + MergeOrder MergeOrderWindow.TriggerGameOver + TriggerWin **三处**加 fire-and-forget hook 调用(`Activity?.IncrementAndLogAsync(ActivityIds.AccumulatePlayCount, 1).Forget()`),在既有防重标记之后调,沿 38 §五 IRpcGateway 范式 HotFix 业务层不直引 Fantasy.* 命名空间。零协议改、零 47 server 段改、零 38/42/46 客户端业务层改。

### 文件清单

**新增(本子单核心)**

- `Assets/GameScripts/HotFix/GameLogic/Module/Activity/ActivityIncrementCode.cs`
  - 客户端 `ActivityIncrementCode` 枚举(5 档,沿 46 `AttrLedgerQueryCode` 范式;比 47 §3.3 多一档 `NetworkDown` 客户端独占;整数 0..3 与协议 `ActivityIncrementResultCode` 一一对位、4 = NetworkDown)
- `Assets/GameScripts/HotFix/GameLogic/Module/Activity/ActivityIncrementResult.cs`
  - POCO `ActivityIncrementResult`(readonly struct,3 字段 Code/CurrentCounter/TargetReached 对位 47 §3.2 响应,严格不投机加字段)+ 3 个工厂方法 `ServiceUnavailable / NetworkDown / InvalidRequest` 快捷构造失败分支(沿 46 `AttrLedgerPage` 同范式)
- `Assets/GameScripts/HotFix/GameLogic/Module/Activity/IActivityIncrementSource.cs`
  - 数据源接缝 `IActivityIncrementSource`(唯一方法 `IncrementAsync(int activityId, int delta)` 返 `UniTask<ActivityIncrementResult>`;接口面不暴露 Fantasy 类型,沿 46 / 32 范式)
- `Assets/GameScripts/HotFix/GameLogic/Module/Activity/RemoteActivityIncrementSource.cs`
  - 生产实现 `RemoteActivityIncrementSource`(#if FANTASY_UNITY 内调 `Session.C2G_ActivityIncrement(activityId, delta)`;异常 catch 转 `NetworkDown`、未连接/未登录返 `NetworkDown`、null 响应返 `ServiceUnavailable`、协议 resultCode 经 `MapResultCode` 映射;关键纯函数 `MapResultCode` 非 FANTASY_UNITY-gated 可 EditMode 直测)
- `Assets/GameScripts/HotFix/GameLogic/Module/Activity/RemoteActivityService.cs`
  - 编排层 `RemoteActivityService`:持 `IActivityIncrementSource` 接缝;`IncrementAsync` 1:1 转发(便于 Tier 4+ 加交叉编排);`IncrementAndLogAsync` 便利出口 = 转发 + 按结果码分级落日志(Success/Info、TargetReached 附额外 Info、InvalidRequest+NotCumulative/Error、ServiceUnavailable+NetworkDown/Warning);`LogResult` 抽为 public static 纯函数便于 EditMode 直测;不持本地状态(沿 47 §四 服务端独占)
- `Assets/GameScripts/HotFix/GameLogic/Module/Activity/ActivityIds.cs`
  - 常量类 `ActivityIds.AccumulatePlayCount = 5`(本子单首次集中,沿设计 48 O7 决策避免散落魔数;Tier 4+ 加同类 Cumulative 活动按 activity.xlsx 行新增常量,业务层零代码改沿同范式)
- `Assets/Editor/Tests/BlockBlast/ActivityClientTests.cs`(+ `.meta` 由 Unity 自动产)
  - 25 个测试:① Service 编排(Success 透传 / 入参透传 / TargetReached 透传 / InvalidRequest / NotCumulative / ServiceUnavailable / NetworkDown / 桩抛异常透传)8 项;② `MapResultCode` 纯函数(已知 4 档 + 未知码兜底 3 案)2 项;③ fire-and-forget 不阻塞(延迟 5s 桩 + 调用方 <500ms 返)1 项;④ Service 无本地去重(沿 O8 防重靠业务标记)1 项;⑤ Service / Source 反射核无本地状态字段(PV13 / PV15 ⑤)2 项;⑥ POCO 工厂方法 3 案 + ActivityIds 常量 = 5;1 项;⑦ GameContext.InitActivityWith 注入入口 1 项;⑧ 源码文本核:GameWindow.TriggerGameOver / MergeOrderWindow.TriggerGameOver / TriggerWin 三处 hook 在防重之后 + GameWindow 内 hook 仅出现一次 + MergeOrderWindow 内 hook 共 2 次 + 业务层不引 Fantasy(7 文件 grep)6 项;共 25 项,均同步驱动(`GetAwaiter().GetResult()`)EditMode 内秒级跑完

**改既有**

- `Assets/GameScripts/HotFix/GameLogic/GameContext.cs`
  - 加 `using GameLogic.Activity`
  - 加 `RemoteActivityService Activity { get; private set; }` 属性(沿 `PlayerAttr / Mail / AttrLedger` 同范式)
  - `OnInit` 末尾 `Activity = new RemoteActivityService(new RemoteActivityIncrementSource())`(GameApp.StartGameLogic 触发 GameContext.Instance 首次访问时即初始化,沿 38 / 46 同范式)
  - 加测试注入入口 `InitActivityWith(IActivityIncrementSource)`(沿 `InitAttrLedgerWith` 同范式)
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/GameWindow.cs`
  - 加 `using Cysharp.Threading.Tasks`(`.Forget()` 扩展)+ `using GameLogic.Activity`
  - `TriggerGameOver()` 内 `_gameOverTriggered = true;` + `SaveHighScore()` 之后、`GameModule.UI.CloseUI` 之前加 1 行 fire-and-forget:`GameLogic.GameContext.Instance.Activity?.IncrementAndLogAsync(ActivityIds.AccumulatePlayCount, 1).Forget();`(null-safe `?.` 守 PV8;在防重之后 = PV6 单测验顺序;不动 `_state.Save()` / `SaveHighScore()` / `CloseUI` / `ShowGameOverWindow` 顺序)
  - **未动**:`OnCreate / OnDestroy / OnAttrChangedDispatch / PlaceAndResolve / CollectClearedElements / 落子 / ghost / 拖拽 / UpdateBest` 等其它逐字节不变
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWindow.cs`
  - 加 `using GameLogic.Activity`(已有 `using Cysharp.Threading.Tasks`)
  - `TriggerWin()` 内 `_finished = true;` + `FlushSaveIfDirty()` 之后、`_state.ExitMergeOrder()` 之前加 1 行 fire-and-forget hook(说明通关也算「玩了一局」,等价计数,沿 PV5)
  - `TriggerGameOver(string title)` 内同位置加 1 行 fire-and-forget hook(精力耗尽 + GAME OVER 两路共用此函数,hook 在 `_finished` 防重之后,一局只 +1,PV6)
  - **未动**:订单 / 合成 / 拖拽 / Demo / 盲盒 / 女神 / 其它生命周期函数全部逐字节不变

**未动(零行 diff)**

- 47/46/45/44/37/38/40/41/43/32/22 设计稿规范层
- 协议生成物(OuterMessage.cs / OuterEnum.cs / NetworkProtocolHelper.cs / OuterOpcode.cs)
- Fantasy 服务端段(handler / service / mongodb collection / activity.xlsx 配)
- 38 / 42 / 46 客户端业务接入(PlayerAttrService / RemoteAttrLedgerService / HUD 接线 / 流水窗)
- GameOverWindow / MergeOrderWinWindow / PlayerInfoWindow / MainMenuWindow 等任何 UI 节点
- Classic / MergeOrder / Tarot 玩法循环核心(PlaceAndResolve / 落子 / ghost / 拖拽 / 棋盘判定 / 合成 / 订单)

### 设计稿同步重写(plan 已落,dev 未再动 — 交 test PV15 / sweep 核)

plan 交接区列同步重写 11 处(47 九处覆盖立项框 / §3.1 mermaid / §3.2 / §3.6 / §四 / §五 / §六 / §七 O2/O6 / §八 PVC2 + BLOCKED + §五诚实边界 / §九风险 + §相关文档 + nav.js + index.html),已由 plan 完成,dev 未再动。

### 自治拍板(沿 plan 默认 + 实现层细化)

- **`IncrementAndLogAsync` 便利出口落 RemoteActivityService**:取此细化拍板。理由:① 设计 48 §3.5 三处 hook 都是「fire-and-forget + 据 Code 落日志」固定模式;② 把日志分发抽进 service 避免业务出口重复 switch,沿 DRY;③ `LogResult` 抽 public static 纯函数 = 单测可不依赖单例直测;④ hook 调用点保持「一行 + `.Forget()`」最简模式(可读性 + 不动 GameWindow / MergeOrderWindow 既有缩进结构);⑤ 业务出口若未来需自定义日志/UI 反馈,直接调底层 `IncrementAsync` 取结果分支。可逆(违此 = 三处出口各自 switch 5 档 = DRY 违 + 日志一致性脆)。
- **`ActivityIncrementResult` 用 readonly struct**:取此细化拍板。理由:① 3 个值字段 + 不可变 + 高频构造(每次 GameOver 都新建),struct 避免堆分配;② 沿 C# 8+ readonly struct 范式与 immutability;③ 工厂静态属性 `ServiceUnavailable / NetworkDown / InvalidRequest` 每次构造新实例(struct 无 boxing 担忧)。可逆(变 class 也 OK,体感差异微)。
- **`ActivityIds.AccumulatePlayCount = 5` 常量集中(本子单首次)**:取此细化拍板,**与设计 48 §3.5 / O7「本子单硬编码 5」决策对齐但落地选「立即提常量」**。理由:① 三处 hook 调用都用同一 id,硬编码字面 `5` 三处比 `ActivityIds.AccumulatePlayCount` 一处常量易漂移(其中一处误改成 6 = 全栈链断);② 常量类零额外接缝、零设计面扩,只是把字面值集中;③ 单测可断言常量值正确性(沿 plan PV6 + 设计 47 §3.4 activity.xlsx 行,1 项 ActivityIds_AccumulatePlayCount_IsFive 反证);④ 设计 48 O7 留 Tier 4+ 加常量类是预防多套并存,本子单单点提前提常量不破设计意图、仅减少未来切换成本;⑤ 业务出口调 `ActivityIds.AccumulatePlayCount` 比 `5` 自文档化(看代码就懂活动 id 5 是「累计游戏 N 局」)。可逆(若 plan 严守 O7「本子单仅硬编码」,改回 `Activity?.IncrementAndLogAsync(5, 1).Forget()` 即可,常量类删除)。
- **Activity hook 调用点放在 `_state.Save() + SaveHighScore()` 之后**(GameWindow):取此细化拍板。理由:① 业务关键流程(存档 + 高分落盘)优先,hook 是次要 + fire-and-forget;② 即便 hook 调用瞬间抛(理论上 `?.IncrementAndLogAsync` 不会抛,因 service 不抛 + Forget 兜),也已存档,不丢局;③ `_gameOverTriggered=true` 之后位置可放任何地方(防重已守),选 SaveHighScore 后是「先保命再上报」语义。可逆。
- **MergeOrder hook 调用点放在 `FlushSaveIfDirty()` 之后、`_state.ExitMergeOrder()` 之前**:取此细化拍板。理由:① 与 GameWindow 同语义「先存档再上报」;② `ExitMergeOrder` 会丢 MergeState,hook 在它之前 = 即便 hook 抛(理论不会)状态仍可恢复;③ 实际 hook 不抛(`?.` + `.Forget()`),位置选择是防御性而非必需。可逆。
- **不另建 hook 落点封装函数(如 `OnGameFinished` 共用方法)**:取此拍板。理由:① 三处 hook 调用都是单行 `Activity?.IncrementAndLogAsync(ActivityIds.AccumulatePlayCount, 1).Forget();` 完全相同;② 共用方法需放某宿主类,跨 BlockBlastUI / GameLogic.Activity 边界;③ 直接复制粘贴一行更直观,行为 + 防重位置随各自函数语义;④ 未来若加同类 Cumulative 活动出口分散,再抽方法不迟(YAGNI)。可逆。
- **业务出口用 `?.IncrementAndLogAsync(...).Forget()` null-safe 链**:取此细化拍板。理由:① `Activity` 在 GameApp.StartGameLogic 触发 GameContext.Instance 首次访问时即创建,正常流程下永远非 null;② `?.` 守 EditMode 反射场景 / 启动期未到的早期 GameOver(沿 38 / 42 / 46 同范式);③ `.Forget()` 显式标 fire-and-forget 避免 UniTask warning(沿 PV15 ①);④ `?.` 后链 `.Forget()` 在 C# 6+ 标准 null-conditional 语义合法。可逆。
- **PV6 防重 + PV7 Tarot 共计的单测策略 = 源码文本核**:取此沿 26 settlement-window memory 范式拍板。理由:① 单测驱动 GameWindow.OnCreate 需 UI 框架 + UIWindow 生命周期 + UnityEngine.UI.Text/Image 等运行期依赖,EditMode 反射成本高;② 设计 48 PV6 验「hook 在防重标记之后」是源码层不变量,文本搜索 `_gameOverTriggered = true;` + `Activity?.IncrementAndLogAsync(...)` 索引比 + Assert > 即足以核「顺序正确」;③ 同样 PV4 验「TriggerGameOver 一函数覆盖两路 GameOver」=「hook 字面在 MergeOrderWindow.cs 内 = 2 次(GameOver + Win 各 1)」+「GameWindow.cs 内 = 1 次」反证;④ 真行为层防重(玩家连点 / 同函数复入)在 PlayMode 手验,留 test;⑤ 该范式在 SettlementWindowReskinTests 已建立,本任务沿用 6 项源码文本核(在源代码改了 hook 行就测试失败,有反向 grep 保护)。可逆(若 test 反馈源码文本核不够,可加 PlayMode 测试驱动 GameWindow OnCreate + 模拟 GameOver,但工作量大且不是 EditMode 范围)。
- **`Service_Increment_SourceThrows_PropagatesException` 断言桩抛异常时 service 透传(不吞)**:取此拍板。理由:① 接缝契约文档明示「不抛」(`IActivityIncrementSource` doc-comment + `RemoteActivityIncrementSource` catch 全部);② 业务出口 `.Forget()` 由 UniTask 框架兜底捕获 + 默认日志,不影响 GameOver 流程;③ Service 不再加 try-catch 避免吞错误码(若桩反契约抛 = bug 该暴露,fire-and-forget 在 Forget 层捕获即可);④ 沿设计 48 §3.3 服务层不持本地状态 + 不重试 + 不缓存,catch 也无意义。可逆(若 plan 要求 service 也加 try-catch 兜底,加一处 catch 转 NetworkDown 即可)。

### 验证点(给 test 据此验)

**自检已过**(本环节已跑)

- 编译 0 error(`read_console errors=0`,4 条非编译日志均为 INFO / 桥重连瞬态)
- EditMode 全量 **512/512 PASS**(BlockBlast.Tests + UIAtlasPackerTool.Tests,18.4s,新增 25 个 ActivityClientTests 全绿;原 487 → 512 = 加 25 项)
- git diff 范围核:本子单文件清单内,未涉及 47 server 段 / 协议生成物 / 38/42/46 业务层
- 异常路径走查:Session null / IsConnected=false / IsLoggedIn=false / RPC 抛异常 / null 响应 / 未知 resultCode 整数 / 服务层 source 反契约抛 / fire-and-forget 慢网 / GameContext.Activity 早期 null(?. 守)→ 全有对应 catch / 兜底 / 测试覆盖

**留给 test 复核**

| 组 | # | 怎么验 | 期望 |
|---|---|---|---|
| PV | PV1 | `read_console` errors=0;新类全编译过 | 已自检过 |
| PV | PV2 | `run_tests` EditMode 全绿(包含 BlockBlast.Tests 现有 + 新增 ActivityClientTests);Classic 一局完整玩(开局 → 落子 → GameOver → GameOverWindow 弹出 → 再来一局)行为不变;MergeOrder 一局完整玩(精力耗尽 / GAME OVER / 通关)行为不变(Play 手验) | EditMode 已自检 512/512;Play 手验留 test |
| PV | PV3 | Play 手验:Classic 玩到棋盘无法继续 → GameWindow.TriggerGameOver 触发 → 控制台见 `[Activity] +1 → counter={N}, targetReached={bool}`(若服务端可达)或 `[Activity] NetworkDown activity=5 — 本次累计丢弃`(若不可达);GameOverWindow 正常弹出无额外延迟 | Play 手验留 test;EditMode 已过源码核(hook 在 TriggerGameOver 内、防重之后) |
| PV | PV4 | Play 手验:MergeOrder 精力耗尽 → `[Activity] +1`;MergeOrder GAME OVER → `[Activity] +1`(两路共用 TriggerGameOver(string),hook 一处覆盖两路) | Play 手验留 test;EditMode 单测 `Source_MergeOrderWindow_HookAppearsTwice_GameOverPlusWin` 反证 hook 出现 2 次 = TriggerGameOver + TriggerWin 各一次 |
| PV | PV5 | Play 手验:MergeOrder 通关 → TriggerWin → `[Activity] +1`;MergeOrderWinWindow 正常弹出 | Play 手验留 test;EditMode 源码核 `Source_MergeOrderWindow_TriggerWin_HasActivityHookAfterDedup` 已过 |
| PV | PV6 | EditMode 单测 `Source_GameWindow_TriggerGameOver_HasActivityHookAfterDedup` + `Source_MergeOrderWindow_TriggerGameOver_HasActivityHookAfterDedup` + `Source_MergeOrderWindow_TriggerWin_HasActivityHookAfterDedup` 反证「hook 在防重之后」;PlayMode 手验:Classic 玩到 GameOver 触发两次时控制台 `[Activity] +1` 只见一次 | EditMode 已过(顺序断言);Play 手验留 test |
| PV | PV7 | Play 手验:Tarot 模式玩一局 GameOver → `[Activity] +1` 一次;Classic 模式玩一局 GameOver → 同样 +1 一次(沿 26 Tarot / Classic 共用 GameWindow,GameWindow.TriggerGameOver 调一次 hook = 1 局) | Play 手验留 test;EditMode 单测 `Source_GameWindow_PlaceAndResolve_Untouched_NoActivityHook` 反证 GameWindow.cs 内 hook 字面仅出现一次 = 同函数共用 |
| PV | PV8 | EditMode 单测 `GameContext_InitActivityWith_ReplacesService` 验注入后非 null + 可调;静态核源码:GameWindow / MergeOrderWindow hook 调用用 `Activity?.IncrementAndLogAsync(...)` null-safe 链(grep 已过);PlayMode 手验启动期未完成时 hook 调用不抛 NRE | EditMode 已过;grep 已过;Play 手验留 test |
| PV | PV9 | EditMode 单测 `Service_Increment_Success_PassesThrough` / `_TargetReached_PassesThrough` / `_InvalidRequest` / `_NotCumulative` / `_ServiceUnavailable` / `_NetworkDown` / `MapResultCode_AllKnownCodes` / `_UnknownCode_FallsBackToServiceUnavailable` 共 8 项覆盖各档 | 已自检过 |
| PV | PV10 | EditMode 单测 `IncrementAndLog_FireAndForget_DoesNotBlock` 注入延迟 5s 桩 + `.Forget()` 后调用线程 <500ms 立即返;PlayMode 手验秒级弹窗 | EditMode 已过;Play 手验留 test |
| PV | PV11 | EditMode 单测 `Service_Increment_ServiceUnavailable_PassesThrough`(service 透传)+ `Source_BusinessLayer_DoesNotImportFantasy`(无 UI 反馈反证 = 无 UI 引用)| 已自检过;PlayMode 手验「停 MongoDB → GameOver 屏幕无任何活动相关 UI」依赖本机 MongoDB,不可达判 BLOCKED |
| PV | PV12 | EditMode 单测 `Service_Increment_NetworkDown_PassesThrough`;PlayMode 手验:模拟 Session=null → 见 NetworkDown Warning + 屏幕无错误窗 | EditMode 已过;Play 手验留 test |
| PV | PV13 | EditMode 单测 `Service_HasNoLocalStateFields` + `RemoteSource_HasNoLocalStateFields`(反射核 service / source 无任何持久化字段、无 PendingQueue / 无 PlayerPrefs)| 已自检过 |
| PV | PV14 | EditMode 单测 `Source_BusinessLayer_DoesNotImportFantasy`(grep 核 7 文件无 `using Fantasy`,RemoteActivityIncrementSource 经 `#if FANTASY_UNITY` 引允许)| 已自检过 |
| PV | PV15 | Code Review 9 项: ① `.Forget()` 显式标记 ✓(grep 三处 hook 调用均带 `.Forget()`);② hook 在防重之后 ✓(源码核 3 项);③ hook null-safe `?.` ✓(grep 源码);④ RemoteActivityIncrementSource catch 网络异常转 NetworkDown 不抛 ✓(代码 try-catch);⑤ RemoteActivityService 不持本地状态 ✓(反射单测);⑥ activityId / delta 在 hook 处硬编码(本子单 5 / 1)— 经 `ActivityIds.AccumulatePlayCount` 常量集中 ✓(沿 plan O7 决策的早期落地,自治拍板已说明);⑦ GameContext.Activity 启动期 GameApp.StartGameLogic 创建实例 + 注入生产 source ✓(代码核);⑧ 不动 PlaceAndResolve / 落子 / ghost / 拖拽核心 ✓(源码单测 `Source_GameWindow_PlaceAndResolve_Untouched_NoActivityHook`);⑨ 不动 GameOverWindow / MergeOrderWinWindow / PlayerInfoWindow / HUD 等任何 UI 节点 ✓(文件清单核)| grep + 源码单测核已过 |
| E | E1-E7 | 真往返:起 MongoDB + Fantasy.Net + 客户端登录;Classic / MergeOrder / Tarot 各玩一局 → `[Activity] +1` 日志 + mongod `activity_progress` counter 验;玩 100 局(或 dev 加测试快捷 `IncrementAsync(5, 100)` 一次性推 99 局 + 玩 1 局)→ targetReached=true + mongod mails 收件箱多一封活动邮件;断 mongod 时 GameOver 见 Warning 兜底文案 + 屏幕无错误窗;fire-and-forget 不延迟 GameOver UI(典型 <100ms) | 留 test Play 手验,不可达判 BLOCKED(沿 47 §八 + 46 §7.3 同口径) |

### 标注

- **不涉及热更程序集变更**:新类全在 `Assets/GameScripts/HotFix/GameLogic/` 下 HotFix 范围;新建命名空间 `GameLogic.Activity`(同 `GameLogic.Mail / GameLogic.AttrLedger / GameLogic.Rank` 范式,不需新增 asmdef)
- **不需要 Luban 重生成**:零配置表改动(activity.xlsx accumulate_play_count_100 行 47 已建)
- **不需要协议生成物重新导出**:零协议改(消费 47 已交付的 `C2G_ActivityIncrement` + `G2C_ActivityIncrementResponse`)
- **不需要 Fantasy 服务端编译**:零服务端段改
- **需进 Play 模式手验的功能点**:PV2 既有玩法零回归 + PV3-PV7 hook 触发可见 + PV6 PlayMode 防重 + PV8 启动期 null-safe + PV10 秒级弹窗 + PV11 / PV12 屏幕无错误窗 + E1-E7 真往返(依赖本机 MongoDB + Fantasy 服可达;不可达判 BLOCKED 沿 38 / 44 / 45 / 46 / 47 同口径)

### 诚实边界

- PV6 防重 / PV7 Tarot 共计 / PV4 MergeOrder 两路共用 / PV5 通关计数 这 4 项「行为级 hook 触发次数」单测采用**源码文本核策略**(沿 26 settlement-window memory 范式):验「hook 行存在 + 在防重标记之后 + 出现次数正确」。真行为级「玩家连点 GameOver 时 hook 计数 = 1」「Tarot 与 Classic 共用 GameWindow 实际只触一次 hook」需 Play 手验,Editor 反射成本高不在 EditMode 范围。源码核断言保证「改了 hook 顺序 / 位置 / 重复加 hook 即测试失败」,作为「改动者写错」的硬反向 grep 防护。
- E1-E7 真往返全部依赖本机 MongoDB + Fantasy.Net 服可达(沿 47 §八 BLOCKED 边界 + 46 §7.3),不可达判 BLOCKED 非 FAIL。
- `IncrementAndLogAsync` 便利出口的 `Log.Info/Warning/Error` 直接用 TEngine 全局 Log 静态(沿其它服务范式),未抽 ILogSink 接缝便于注入桩验证日志级别。理由:① 沿 PlayerAttrService / RemoteAttrLedgerService / RemoteMailService 等同范式,Log 是全局日志门面无注入需求;② 设计 48 §3.8 错误码处理只是「落日志即止」,日志内容验证不在客户端段单测范围(Play 模式 Console grep 即可);③ 若 test 反馈需测日志级别可后续抽 ILogSink 增量改。
