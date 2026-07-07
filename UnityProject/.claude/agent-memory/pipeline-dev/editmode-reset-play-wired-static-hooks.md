---
name: editmode-reset-play-wired-static-hooks
description: GameApp 在 Play 模式装配的生产静态钩子(BlockGameState.OnMergeStateReady 等)跨 EditMode 域存活,污染纯逻辑单测的本地权威基线;须在 assembly 级隔离器 ResetConfigCachesAttribute 每用例前后清零,清单例不够、必须清钩子
type: rule
---

rule: BlockBlast 纯逻辑 EditMode 单测依赖「本地权威」基线(`ServerDeal==null` → 本地发牌;`ServerAuthoritativeOrders==false` → 本地发交付奖励)。但 `GameApp.StartGameLogic` 在 Play 模式装配的**静态**委托 `BlockGameState.OnMergeStateReady` / `OnMergeStateClosed`(钩子体内 `BlockGameState.Instance.ServerDeal = c.ServerDeal` + `OrderSync.OnMergeStateReady(state)` 把活态切服务端权威订单)在整个 Editor domain 内跨测试存活。交互式 Editor 跑过一次 Play 后再跑 EditMode,这些钩子非空 → `ResetForMergeOrder` 末尾 `OnMergeStateReady?.Invoke(MergeState)` 触发 → 重新注入 `ServerDeal`(`RefillPieces` 翻服务端发牌分支 → 候选空 → `OperaArr` 留 null → 访问 `.ShapeId` NRE)并置 `ServerAuthoritativeOrders`(`Deliver` 翻服务端分支 → 本地不发交付体力 → 能量停在扣前值)。表现为「fresh 域绿、Play 后红」的环境/顺序相关 flaky。统一在 assembly 级隔离器 `ResetConfigCachesAttribute`(`Assets/Editor/Tests/BlockBlast/ConfigCacheIsolation.cs`,`ITestAction` 每用例前后跑)里清零这两个静态钩子。

清单例(`BlockGameState.Instance.Release()`,MergeOrderTests.SetUp 已做)**不够**:`Release` 只把 `_instance=null`、下次 `Instance` new 干净实例(`ServerDeal=null`),但存活的静态钩子会在紧接着的 `ResetForMergeOrder` 末尾把 `ServerDeal` 重新注入这个新实例。`ServerDeal` 的唯一注入点即这两个钩子体内(`GameApp` 内两处),故清钩子即从根免疫,无需另清 `ServerDeal`。

Why: TEngine_block「MergeOrderTests 两个用例 Play 后必红」(2026-07)。`RefundEnergy_ClampedToCap_RewardOverflows` 期望交付后 energy 28→36,实际停 28(Deliver 走服务端分支);`EnqueueScoreElements_TypesSubsetOfNeeded_DistributedAcrossTrio` 抛 NRE(RefillPieces 走服务端分支留空候选)。根因是同一静态钩子污染,非被测逻辑错——同域一次域重载(改任意脚本触发)清空静态即全绿。与 [[rebind-baseline-on-session-state-rebuild]] 同源(同一 `OnMergeStateReady` 接线面):那条是「接线层漏调 rebind」,这条是「Play 装配的静态接线泄漏进单测」;服务端权威迁移(M1 去单例化)持续新增此类 Play 装配静态钩子,复发面大。

How to apply: 新增「GameApp / 接线层在 Play 装配的静态生产钩子(`static Action<...>`),且钩子体内会切换某子系统到服务端权威 / 注入权威组件」并有覆盖同子系统的纯逻辑 EditMode 用例时,把该钩子的清零加进 `ResetConfigCachesAttribute.Reset()`(与既有 `*ConfigMgr` 归位并列)。诊断信号:某 EditMode 用例「单独跑 / fresh 域绿,整套 / Play 后红」。UnityMCP `get_test_job` 轮询不可用时的复现验证:`execute_code` 里装污染钩子(`FormatterServices.GetUninitializedObject(typeof(ServerDealSync))` 造非空 ServerDeal)跑测试核心序列得污染值,再调**真实** `new ResetConfigCachesAttribute().BeforeTest(null)` 后重跑得修复值,in-process 对比(本例 energy 28→36、候选 NRE→ACCESS-OK),不依赖测试运行器。相关 [[unitymcp-run-tests-filter]]。
