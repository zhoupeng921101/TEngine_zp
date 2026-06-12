# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:核心玩法补全(demo 范围)

设计基线 = `design-docs/11-core-loop-completion.html`(§十二 8 条决策已拍板,定稿)。git 基线 421a2c12(block_claude)。

### 交接区(给 test)

#### 改动摘要

按设计 11 §十一 (B) 新增系统逐条落地,均为**加法式扩展 + 纯逻辑可单测**;现状(A)路径(Classic / 原 merge-order off 路径)零改动,靠原 96 单测兜回归。核心采「逻辑层全覆盖、窗口层最小接线」策略。

1. **连消/多消/全清结算(§5.5 固定流水线)** — 新增 `ClearSettlement.Settle()`:
   - 连消链 `ComboChain`(住 `MergeOrderState`):每次有消除 +1、无消除回 1;倍率 `ComboMultPermilleFor`(×1.0/1.2/1.5/1.8/2.0 封顶,整数千分比避免浮点不可测)。
   - **铁律落地**:倍率**只乘显示分**(`DisplayScore`);元素产出 `EnqueueScoreElements(k)` 用**未乘连消的 baseScore**;全清判定用棋盘空 + 武装位,均不碰倍率。
   - 多消里程碑加码 `MultiClearMilestoneBonus(lines)`:3消→+1 Lv2、4消→+1 Lv2+1 Lv1、5消→+1 Lv2+2 Lv1、6+消→+1 Lv3,经 `AddDirect` 直发收集区(跳过逐级合成)。
   - 全清:武装位 `AllClearArmed`(开局 true),发 1 Lv3 + 推进女神 +1;发奖后置 false,须一次非全清落子重新武装(**不可连续 2 次**)。
2. **订单双轨(§三)** — 新增 `SpecialOrderTrack.cs`(`SpecialOrderKind` 剧情>加急>黄金时段 + `SpecialOrder`)。`MergeOrderState.SpecialTrack`:0/1 占槽 + 等待队列;已占槽不被中途踢出(高优先级请求入队),槽空时升起队列最高优先级(同级 FIFO);加急倒计时 `TickCountdown` 到点过期升队。日常轨(`ActiveOrders=2`)零改动。demo 默认特殊轨空(触发器留窗口按条件投放,本环节未接 UI 触发)。
3. **体力/灵力/祈愿/HammerCost(§四/§7.1/§7.2)** — 单货币 `Soul` + `AddSoul`;祈愿兑体力 `WishForEnergy`(每日限 `WishPerDayLimit=3`,`WishSoulCost=20`→`WishEnergyGain=10`,封顶软上限不溢出,纯灵力无付费);`HammerCost=8`。自然恢复维持现状「留口子未实装」。
4. **智能生成 R1–R3 仲裁(§八)** — 新增 `HandGenerationArbiter.Decide()` + `HandGenContext`(`NoClearStreak`/`AntiStreak`)。优先级链:P0防卡死(Fill)>P1清盘增难(Diff)>P2清盘引导(ClearAll)>P3高阶引导(AllCombination)>P4回落。**P4 返回 `Fallthrough`(不接管)= 现状 dynamicWeight 逐字节不变**;P0 与 P1 互斥时 P0 优先且 antiStreak 此手不递减;计数器 `OnPlaced` 照常更新。规则到 trio 复用既有 `BlockAlgorithms`,本环节**未改 dynamicWeight 本身**。
5. **宝箱(§九,后置项)** — 新增 `ChestSystem.cs`:4 箱位、占槽倒计时 `Tick`、到点 `CanOpen`、`Open` 抽 3 不重复 Kind 三选一、`Claim` 腾位;`TierCountdownSec` 普通/稀有/史诗;**去变现红线:无付费/广告减时入口**。奖励发放(ChestReward→灵力/体力/图案/道具)接线归窗口,本环节只产出选项。
6. **女神(§十,后置项)** — `MergeOrderState`:`GoddessRating`(N/10)+ `GoddessLevel`(只升不降),`AdvanceGoddess()` 满 10 清零升档;由 §5.5 全清统一推进。换背景统一归女神升级触发(§十去重),纯表现归窗口。
7. **术语** — 新代码统一图案/收集区/合成/灵力/体力;无食材/生成器/仓库/虔诚币残留。
8. **窗口最小接线** — `MergeOrderWindow.PlaceAndResolve` 的消除结算段替换为 `ClearSettlement.Settle`(替代原内联 combo/enqueue),新增 `PickMilestoneType()`(取订单所需类型之一作里程碑/全清产物归属)。`_state.Combo` 镜像 `ComboChain`。落子/拖拽手势层未动。
9. **悔棋** — 全量快照新增 ComboChain/AllClearArmed/Soul/WishUsedToday/GoddessRating/GoddessLevel/特殊轨(占槽+等待队列),回滚一并恢复。

#### 文件清单

新增(`Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/`):
- `ClearSettlement.cs`(§5.5 结算流水线 + SettlementResult)
- `SpecialOrderTrack.cs`(§三 特殊轨 + Kind/SpecialOrder)
- `HandGenerationArbiter.cs`(§八 R1–R3 仲裁 + HandGenContext)
- `ChestSystem.cs`(§九 宝箱)

修改(同目录):
- `MergeOrderConfig.cs`(灵力/祈愿/HammerCost/连消倍率/多消里程碑/弹字/全清/女神/智能生成阈值常量 + 映射函数)
- `MergeOrderState.cs`(新增结算/灵力/女神/特殊轨字段 + AddDirect/特殊轨/灵力祈愿/女神方法;Reset 初始化;Snapshot 扩展回滚)

修改(UI):
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWindow.cs`(结算段接 ClearSettlement + PickMilestoneType)

新增(测试):
- `Assets/Editor/Tests/BlockBlast/CoreLoopCompletionTests.cs`(34 个新单测,assembly `BlockBlast.Tests`)

#### 验证点(逐条对应 §十一)

- **编译**:`read_console` 0 error,域重载完成(`editor_state` ready)。涉热更程序集(GameLogic);**无** Luban 重生成需求(配置全硬编码常量)。
- **EditMode 全量**:`run_tests(mode=EditMode, assembly_names=["BlockBlast.Tests"])`。预期原 96 + 新 34 = 130 全绿。重点核:
  - 多消里程碑逐档(`MultiMilestone_PerTier_MatchesDesignTable`)= §5.2 表;连消倍率封顶(`ComboMult_Ramp_CapsAt2x`)= §5.3。
  - **隔离铁律**(`Settle_ComboMultOnlyAffectsDisplayScore_NotElements`):倍率乘显示分、元素用基础分——§5.5 关键可测点。
  - 全清武装位不可连续 2 次(`Settle_ConsecutiveAllClear_SecondNotRewarded` / `Settle_AllClearRearmsAfterNonAllClearMove`)= §5.4。
  - 特殊轨已占槽不被踢 + 升队优先级 + FIFO + 加急过期(`SpecialTrack_*`)= §三。
  - 祈愿每日限次/封顶/灵力不足(`Wish_*`)= §7.1;`HammerCost==8` = §7.2。
  - 女神 10 次升档清零(`Goddess_*`)= §十。
  - 仲裁 P0>P1、回落不接管(`Arbiter_*`)= §八「现状零改变」。
  - 宝箱满 4 拒入/倒计时门/三选一不重复/领取腾位(`Chest_*`)= §九。
  - 悔棋回滚新字段(`Undo_RollsBackSettlementFields`)。
- **需进 Play 模式手验**:`MergeOrderWindow` 实际落子时弹字(Good/Great/.../COMBO/PERFECT)、连消/多消/全清在真实拖拽下的表现。逻辑层已单测,风险集中在窗口接线的弹字分支与 PickMilestoneType 取值(手势层未动,回归风险低)。

### 自检状态(⚠ 运行验证仍阻塞 — Unity MCP 桥握手失败,环境问题持续第三轮)

运行验证(编译 0 error + EditMode 全量)**三轮均未能自跑**,根因是 Unity MCP 桥未与编辑器建立会话,非代码缺陷。

环境实测(本轮 = 第三轮,新增更具体的诊断):
- Unity 编辑器进程存活(PID 11724,自 2026-06-12 20:19 起,~3.5GB),且 `Responding=True`——进程**未挂死**(进程级响应正常)。
- MCP 桥进程(`mcp-for-unity` ×3)存活且 `Responding=True`;另有 `Unity.ILPP.Runner` / `UnityShaderCompiler` / `UnityPackageManager` 存活。
- 但 `mcpforunity://instances` 仍 `instance_count: 0`;`read_console` 仍 `no_unity_session`;`refresh_unity(force, compile=request, wait_for_ready)` 仍 60s 超时未 ready。
- **精化诊断**:进程都活且响应,但编辑器**未向桥注册会话**(instances 空)。这是「桥握手失败 / 编辑器内 MCP 监听未连上桥」,而非前两轮假设的「编辑器进程卡死」——编辑器进程响应正常,问题在编辑器与桥之间的会话注册链路。无论哪种,dev 都无法驱动该实例。
- 判据(代码无关):若是本环节编译错误,编辑器会进「带错就绪」态、`read_console` 可读出 error 列表;此处全程 `no_session`(桥连不上),与本改动无关。
- dev 侧无法安全解此阻塞:不擅自重启/强杀用户编辑器(有未保存编辑器态风险),桥会话注册在代码控制范围外。**复跑同一卡死实例徒劳**(memory 已记),不再空转重试。

本轮做的事(全量静态再验,非"信上轮"):
- 逐文件重读 6 个改动文件(`ClearSettlement` / `SpecialOrderTrack` / `HandGenerationArbiter` / `ChestSystem` / `MergeOrderConfig` / `MergeOrderState`)+ 测试文件 `CoreLoopCompletionTests`。
- **逐符号交叉核**:测试引用的每个外部成员均在源码核到匹配签名——
  `MergeOrderState`{Reset/InventoryCount/IngestElement/AddSoul/CanWishForEnergy/WishForEnergy/AdvanceGoddess/CanDeliverSpecial/DeliverSpecial/CaptureSnapshot/Undo/EnqueueScoreElements/PendingElements/ComboChain/AllClearArmed/GoddessRating/GoddessLevel/SpecialTrack}、
  `ClearSettlement.Settle(m,lines,cells,bool,MergeElement)→SettlementResult`{Lines/BaseScore/DisplayScore/BaseElementsK/AllClearRewarded}、
  `SpecialOrderTrack`{Reset/Request/HasOccupied/Occupied(.Kind/.Req.Type)/WaitingCount/OnDelivered/TickCountdown/SnapshotWaiting/RestoreWaiting}、
  `HandGenContext`{NoClearStreak/AntiStreak/OnPlaced}+`HandGenerationArbiter.Decide→Decision(.Override/.Algo/.Rule)`、
  `ChestSystem`{SlotCount/PickCount/TierCountdownSec/Reset/TryAcquire/IsFull/OccupiedCount/CanOpen/Tick/Open/Claim}。全部命中,无缺签名/无签名漂移。
- 关键断言可满足性复核:`Settle_ComboMultOnlyAffectsDisplayScore` 依赖 Reset() 后 NeededTypes 非空(OrderPool[0]=Diamond,[1]=Star → {Diamond,Star} 非空)→ EnqueueScoreElements(k) 实际入队 k → `PendingElements.Count==k` 成立;`Chest_OpenThreePick_DistinctKinds` 用 Epic(池 5 项 ≥ PickCount 3,Kind 各异)→ 三选一去重可满足。
- 重核最高风险隔离铁律 `ClearSettlement.Settle`:连消倍率(`permille`)只乘 `displayScore`(行 80);元素产出 `k`(行 83 用 baseScore)+ 里程碑 `AddDirect`(固定档位)不碰倍率;全清判定 `boardEmptyAfter && AllClearArmed`(行 94);连续第二次全清(`boardEmptyAfter && !Armed`)落空两 if 分支、不发奖、武装位保持 false(行 108 注)。与 §5.5 一致,无缺陷。
- 结论:本轮**无代码改动**——代码经全量静态再验仍干净,无可修处;阻塞纯属桥会话注册失败,须人工恢复。

**给 boss / test 的解阻塞清单(缺一不可下 PASS)**:
1. 人工恢复 MCP 桥会话:编辑器与桥进程都活且响应,但编辑器未向桥注册(`instances` 空)。由用户在 Unity 端重连桥(菜单触发 MCP-for-Unity 重连 / 必要时重启编辑器),直到 `mcpforunity://instances` 非空。dev/test agent 不擅自强杀编辑器(未保存态风险),亦不再对同一未注册实例空转重试。
2. `refresh_unity(force, compile=request, wait_for_ready=true)` → `read_console([error])` 确认编译 0 error。
3. `run_tests(EditMode, ["BlockBlast.Tests"])` → 期望 96 + 34 = 130 全绿。
4. Play 手验 MergeOrderWindow 弹字 / 连消多消全清表现。

> 本任务工作态;boss 关单事务清空/归档。
