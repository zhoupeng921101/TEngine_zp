---
name: exclude-optimistic-spend-baseline-inline
description: 把「服务端权威扣费 + 客户端乐观扣显示」的单笔同步乐观扣从基线-diff 上报器中排除时,须在扣减发生的同步瞬间就抬平上报基线,而非等 RPC 响应到达才校正——否则落盘边界抢在响应前跑会把 -cost 当净产出上报、服务端双扣
type: rule
---

rule: 当一笔消耗改为「服务端权威扣(serverAuthoritative,固定额、不接受客户端上报)+ 客户端本地乐观扣(仅即时 HUD)」,而客户端体力/货币走的是长生命周期对账器(如 `MetaCurrencySync`)的「当前值 - 基线」净 delta 上报模式时:必须在**乐观扣减发生的同一同步边界**把该笔一并抬进上报基线(`_base += signedDelta`,扣为负),使这笔乐观扣从下次 `ReportPending` 的待上报 delta 净算为 0。**不能**只依赖「RPC 响应回带的权威余额到达后再 `ApplyDeltaPush` 校正」——因为落盘边界(`OnSaved→ReportPending`)与 RPC 响应、`G2C_PropertyDeltaPush` 三者异步交错,若 ReportPending 抢在响应前跑,就会把 -cost 当客户端净产出发 `C2G_PropertyChange`,服务端 CAS 忠实执行 → 与它自己已扣的那一次叠成双扣。

配套:
- 真实权威值随后仍由 `ApplyDeltaPush(state, type, NewEnergy)`(响应回带的绝对余额)或晚到的 `G2C_PropertyDeltaPush`(同值)对齐——两者都是**绝对值 set 而非增量**,故重复 set 幂等、不叠加。
- 服务端拒绝(如 NotEnoughEnergy)回滚时,须以**相反符号再调一次**抬基线(`_base += cost`)撤销排除,再用服务端回带的当前余额 `ApplyDeltaPush` 对齐;不撤则基线滞留低 cost,下次 ReportPending 反把 +cost 当虚假产出上报。
- 未 Ready(登录快照未到、无权威基线可锚)时抬基线入口内部直接跳过,交随后快照接管。

Why: TEngine_block 消除道具(ClearTool)客户端权威化(2026-07)。服务端 handler `serverAuthoritative:true` 扣体力一次 + `SendDeltaPushTo` 推 Energy delta,响应回带 `NewEnergy`=扣后余额。客户端保留 `SpendClearToolCost()` 乐观扣仅为即时 HUD。若照搬订单交付的「不本地加 = 无 delta」范式失败——交付是**服务端发奖、客户端根本不本地加**,而消除道具**客户端确实本地扣了**(手感要求)。若只在响应到达后 `ApplyDeltaPush` 校正,`MarkAndFlushSave→OnSaved→ReportPending` 完全可能在 ClearTool RPC 往返之前跑完,此刻 `state.Energy=old-cost`、`_baseEnergy=old` → 上报 delta=-cost → 服务端双扣。解法是新增 `MetaCurrencySync.ExcludeEnergySpend(signedDelta)` 在 `OnBoardTapForClearTool` 里紧挨 `SpendClearToolCost()` 同步调用(抬 `_baseEnergy-=cost`),无论 ReportPending 何时跑该笔恒净 0;单测 `MetaCurrencySyncTests.T13`(落盘边界抢在响应前跑仍不双扣)锁死此时序。这是既有三种双计避免机制之外的第三种,与它们的分工:①服务端发奖-客户端不本地加(交付);②本地加了-用累加器字段于 ReportPending 内并入基线排除(体力预测恢复 `RegenSinceReport`);③本地单笔同步扣-扣减瞬间即时抬基线(本条,消除道具)。

How to apply: 把一笔消耗迁成「服务端权威扣 + 客户端乐观扣显示」时,先确认服务端是否 `serverAuthoritative` 扣(是则客户端绝不能再自报这笔)。给对账器加一个 `ExcludeXxxSpend(signedDelta)` 入口(带 `IsReady` 守卫,`_base += signedDelta`),在 UI 乐观扣的**同一同步方法**里紧挨扣减调用(扣传负);响应/推送到达后用绝对余额 `ApplyDeltaPush` 对齐(幂等);服务端拒绝分支以相反符号撤销排除 + `ApplyDeltaPush` 回滚。单测必覆盖「落盘边界在响应之前触发」的时序(断言该货币零上报)与「拒绝回滚后无残留虚假 delta」。判据别混:若客户端根本不本地加走范式①、若是按时间累积的预测量走范式②、只有单笔同步乐观扣才用本条范式③。
