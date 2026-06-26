---
name: rebind-baseline-on-session-state-rebuild
description: 服务端权威投影的「基线-diff 上报」模式里,长生命周期对账器持有的基线必须在每次重建短生命周期会话 state(开窗/重进)后重对齐到该 state,否则首次 diff 把(缓存值-旧基线)当成玩法变更上报、被服务端真扣
type: rule
---

rule: 当一个**长生命周期**对账器(单例,如 `MetaCurrencySync`)用「当前值 - 基线」算 delta 上报服务端裁定,而它对账的**值住在一个每次会话/开窗都重建的短生命周期 state 对象**(如 `MergeOrderState`,`ResetForMergeOrder` 每次 new 一个并 `ImportMeta` 从本地缓存填值)里时:每次重建该 state 后,必须在「state 就绪钩子」里把对账器基线**重对齐到新 state 的当前值**(rebind),否则基线仍停在上一次对齐点(登录快照值),与新 state 从缓存读出的值不一致 → 首次上报算出虚假 delta(可能为负)→ 服务端按真 delta 改库,把资源冲穿。

配套:rebind 时若 state 含「已并入当前值、待从下次上报排除的累加器」(如体力预测恢复 `RegenSinceReport`),须**一并清零**——基线已含该增量,不清零则首次上报再 `base += 累加器` 使基线超出活态值,又算出等额负 delta。

rebind 须带「权威已就绪」守卫(`IsReady`):权威快照(`ApplySnapshot`)未到时活态值只是本地缓存、无权威可锚,此刻 rebind 会建错误基线;未就绪一律跳过,交由随后到达的快照接管(快照同时设基线 + 覆盖活态)。

Why: TEngine_block「体力重进归零」bug(2026-06)。登录时玩法窗未开 → `ApplySnapshot(state=null)` 只记基线(=服务端 energy);开窗 `ResetForMergeOrder` 新建 `MergeOrderState` 并 `ImportMeta` 从本地缓存读 energy。设计本有 `RebindBaseline` 契约(开窗 ImportMeta 后重对齐),但**接线层从未调用**——开窗钩子 `OnMergeStateReady` 只接了订单同步 `OrderSync`,漏了货币基线重对齐。当缓存 energy 与登录基线不一致(缓存被清/旧档/离线漂移)时,首次落盘边界 `ReportPending` 把(缓存值 - 基线)当玩法 delta(实测 -15)发 `C2G_PropertyChange`,服务端 CAS 忠实执行 → 体力归 0 → 下次快照即 0。服务端无错(它只faithfully应用客户端发来的 delta),根因 100% 在客户端漏接 rebind。EditMode 复现:`ApplySnapshot(null, energy=15)` + 新 state(Energy=0)+ `ReportPending` → 发出虚假 Energy delta=-15;加 `RebindBaseline(state)` 后 delta=0。

How to apply: 改/查这类「单例对账器 + 每会话重建 state」时,grep 对账器的 rebind/重对齐方法是否真有调用点(`RebindBaseline` 当初零调用是漏接信号)。在 state 重建钩子(本例 `GameApp` 的 `BlockGameState.OnMergeStateReady`,在 `ResetForMergeOrder` 末尾、ImportMeta+ApplyTimeRegen 之后触发)里调 rebind,与既有 `OrderSync.OnMergeStateReady` 并列。rebind 内清零已并入基线的预测累加器、带 `IsReady` 守卫。验证:写复现单测(基线来自快照、state 来自缓存、二者不等 → 断言首次上报 Energy delta 应为 0),修前红、修后绿;跑 `MetaCurrencySyncTests` 全套确认零回归。
