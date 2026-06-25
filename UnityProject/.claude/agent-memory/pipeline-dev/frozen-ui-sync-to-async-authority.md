---
name: frozen-ui-sync-to-async-authority
description: 把一个被冻结 UI 同步调用的方法(返回 bool 门控 UI)改为「异步服务端权威」而不碰调用点——用注入钩子发 RPC + 复用既有每秒轮询方法做脏标记重绘
type: rule
---

rule: 当某玩法状态机方法被**冻结的 UI**同步调用、且 UI 用其 bool 返回值门控后续表现(庆祝/刷新),要把它迁成「服务端权威 + 异步 RPC」而不允许改 UI 调用点时,按三招做:
1. **同步乐观提交 + 异步对账**:同步方法内先做本地乐观可逆变更(扣库存 / 置空槽 / 计数),立即 return true 让冻结 UI 的同步重绘正确显示 happy path;真实权威经异步 RPC 回来后对账(成功应用服务端快照;失败调一个**退还方法**回滚乐观变更——失败不丢数据)。把「被消费的负载」作参数传给异步流程,供失败时精确退还。
2. **注入钩子解耦网络**:状态机不直接依赖网络层(保持纯逻辑可 EditMode 测)。加一个 `Action<...> DeliverHook` 实例字段,同步方法乐观提交后 `Hook?.Invoke(...)` 发起异步;钩子由接线层(GameApp `#if FANTASY_UNITY`)注入,内部 `.Forget()` 跑异步 RPC 编排。
3. **复用既有轮询方法当重绘触发器**:冻结 UI 通常有每秒 `OnUpdate` 轮询、且只在某个旧方法返回 true 时才 `RefreshXxx()`。服务端权威模式下把那个旧方法(如本地墙钟刷新 `ApplyOrderRefresh`)**转义**为「自上次绘制以来服务端快照是否更新过」的脏标记信号(返 true 即触发既有重绘入口),异步快照到达时置脏 → 下一拍轮询重绘(≤1s 延迟)。无需给冻结 UI 加任何订阅/回调。

配套:用**实例开关**(非静态)区分「服务端权威 / 本地旧行为」两分支,开关只由生产接线钩子置位、纯逻辑单测不触 → 实例恒旧行为 → 既有单测零回归零污染。**禁用静态开关**:生产 Play 一次会把静态置位、污染同域后续 EditMode 测(测里走的本地分支被翻成服务端分支,断言崩)。

Why: TEngine_block 多个迁移(UI Mono 化、服务端权威化 P0–P3、订单/特殊订单分批搬)都遇到「调用点在冻结文件里、不能改签名」。P1 订单服务端化实测:`MergeOrderState.Deliver(int)` 被冻结的 `MergeOrderWindow.FinishDeliver` 同步调用并用 bool 门控庆祝,既要停本地发币(改服务端 RPC 发 + delta-push 回,避免与 MetaCurrencySync.ReportPending 双计)、又要 normal 订单整批刷新改服务端推送,且不能动 UI。三招落地后:冻结 UI 调用点(Deliver / ApplyOrderRefresh / TryRefreshIfAllDelivered)全不改即编译绿、545 个 EditMode 测里订单/交付/神庙相关全过(仅并行会话挪文件导致的无关 UI 源扫描测失败)。若当初用静态开关,生产 Play 后跑 EditMode 必污染 `ResetForMergeOrder` 路径的本地发币断言。

双计避免的关键洞察:服务端权威分支**根本不本地加** Energy/Piety,则 MetaCurrencySync 的「当前值 - 基线」净 delta 天然为 0、不会重复上报——不需要额外的「排除累加器」(那是体力预测恢复 RegenSinceReport 的场景:本地确实加了、必须显式从基线抬高排除;交付奖励本地不加,无此需要)。两种「不双计」机制别混用。

How to apply: 迁移一个被冻结 UI 同步调用的权威方法时,先 grep 出全部调用点确认哪些在冻结文件;给状态机加实例开关 + 注入钩子 + 退还方法 + 「应用服务端快照」方法 + 脏标记;把 UI 每秒轮询里那个「返 true 才重绘」的旧方法在权威模式下转义为脏标记信号。接线层(GameApp)注入钩子 + 订阅服务端推送 → 应用快照置脏。开关用实例字段、由钩子置位,不用静态。验证:跑覆盖该方法的 EditMode 全套确认本地旧分支零回归(实例开关恒 false)。
