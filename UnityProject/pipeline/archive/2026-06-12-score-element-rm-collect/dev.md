# 状态:开发(dev)

> 开工先读本文件 + roles/dev.md + state/plan.md 交接区。每完成一项就更新这里。

## 当前任务:score-element-rm-collect(dev 已实现,待 test)

需求①把合成订单切片元素生成从「固定概率注入」改为「消除得分驱动数量」;需求②确认收集 Demo 已移除净、未引入回归。设计基线 `design-docs/10-score-element-rm-collect.html`。按用户拍板参数(A 档映射、补牌抽干、Collect* 保留不改名)实现。

### 改动摘要

得分驱动元素生成走「消除得分 → 元素数 k → 压预算队列 → 补牌 FIFO 抽干填候选块」链路:
- 消除时按 `clearScore` 算 `k = ElementsForScore(clearScore)`,把 k 个所需类型元素压入 `MergeState.PendingElements` 队列(类型按 `_needRotor` 轮转均摊、受 `MaxPendingElements` 截断)。
- 补牌 `BuildPiece` 时从队头 FIFO 抽取写入新候选块 `Elements`,队空则不分配(纯方块)。
- 旧的概率注入 + 消除门控 + 保底计数器整套退役(确定性生成 + 轮转下「无消除无元素」「不饿死类型」自动满足)。
- 计分公式抽出 `BlockScoring` 单一信息源,Classic 与 merge-order 共用同一 `ClearScore`。

需求②:全工程 grep 确认收集专属符号与旧注入符号均 0 匹配(详见验证点),无悬空引用、无新增收集残留,无额外删除动作。

### 文件清单

**新增**
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/BlockScoring.cs` — 计分公式单一信息源。`PlacementScore(cells)=>cells`、`ClearScore(clearedCells,lines)=>clearedCells*10+lines*lines*30`。Classic 与 merge-order 共用。
  - **无 .meta**:dev 子会话无 Unity MCP,.meta 由 boss batchmode 刷新时自动生成。

**修改**
- `Module/BlockBlast/MergeOrderConfig.cs` — 删 `RequireClearForInject`/`InjectChance`/`PityThreshold`;增 `ScorePerElement=200`/`MinElementsPerClear=1`/`MaxElementsPerClear=4`/`MaxPendingElements=12` 及静态 `ElementsForScore(int)`(clearScore≤0→0,否则 Clamp(CeilDiv,Min,Max))。类头注释「掉率」改「得分驱动元素生成」。
- `Module/BlockBlast/MergeOrderState.cs` — 删 `PitySinceNeeded`/`ClearedSinceRefill` 字段;增 `Queue<CollectElement> PendingElements` + `_needRotor` + `EnqueueScoreElements(int k)`。`Reset()` 清队列+游标。`Snapshot` 去 `_pity`/`_clearedSinceRefill`、改存 `_pendingElements`(`ToArray()` 深拷贝)+`_needRotor`,`Restore` 重建队列。
- `Module/BlockBlast/BlockGameState.cs` — `InjectElementsForMergeOrder` 改写为 `DrainPendingElementsInto(piece,shapeId)`(队头 FIFO 抽取填 `piece.Elements`,队空不分配);`BuildPiece` 调用点改调它;删 `OnHandRefilled()` 方法 + `RefillPieces` 两处调用;删 `ClearRowsAndCols` 内 `ClearedSinceRefill=true` 置位(方法签名/返回值不变);`ClearRowsAndCols`、`ResetForMergeOrder` 文档注释更新为当前事实。元素转移/`CollectClearedElements`/持久化未动。
- `UI/BlockBlastUI/GameWindow.cs`(Classic) — 落子分与消除分改调 `BlockScoring.PlacementScore`/`ClearScore`,**纯等价抽取,输出逐数字不变**。
- `UI/BlockBlastUI/MergeOrderWindow.cs` — `PlaceAndResolve` 消除分支捕获 `ClearRowsAndCols` 返回的 `clearedCells` → `BlockScoring.ClearScore` → `ElementsForScore` → `EnqueueScoreElements`,置于补牌 `RefillPieces` 之前。其余未动。
- `Editor/Tests/BlockBlast/MergeOrderTests.cs` — 删旧注入模型用例(`Pity_ForcesInjectionAfterThreshold` 及 `Inject_GatedUntilClear_FirstHandClean`/`Inject_AllowedAfterClear`/`Inject_GateClosesAgainAfterRefill`/`Undo_RestoresClearGateFlag`);`Undo_RollsBackAllState` 去 `PitySinceNeeded` 断言;新增见下。

### 新增单测(MergeOrderTests.cs)

- `ElementsForScore_MapsPerTier` — 映射逐档:-50/0→0、1→1(保底)、110/200→1、280→2、500→3、780→4、2000→4(封顶)。
- `HigherScore_YieldsMoreElements` — 四消(780→4)产元素 > 单消(110→1)。
- `EnqueueScoreElements_TypesSubsetOfNeeded_DrainsFifoOnBuild` — 入队 k 个、类型⊆NeededTypes;补牌按填充格行优先 FIFO 抽干,余格 None,队列清空。
- `NoClear_QueueEmpty_PiecesClean` — 开局首手三块全干净;队空继续 BuildPiece 仍纯方块。
- `EnqueueScoreElements_RotatesAcrossNeededTypes` — 双订单不同类型时轮转覆盖两类。
- `EnqueueScoreElements_CapsAtMaxPending` — 入队超上限被截断至 `MaxPendingElements`,满后不增。
- `Undo_RestoresPendingElementsQueue` — 悔棋回滚预算队列(撤回入队的 k 个)。

### 与 plan 改动点清单的对应/偏差

逐条对上 plan 交接区与 HTML §四改动表。**一处轻微偏差**:
- plan/HTML 建议 `BlockScoring` 抽 `ClearScore` 与 `PlacementScore` 二者。已**两者都抽**并接入 Classic `GameWindow`(`PlacementScore(cells)=>cells` 为恒等,接入零行为变化),取设计推荐的「单一信息源」路径而非退化内联。Classic 计分输出逐数字不变(纯等价抽取),仍属硬约束验收范围。

无设计层错误需停手;A 档映射参数、补牌抽干时机、Collect* 不改名均按用户拍板落地,未改设计。

### 需要 test 重点验的点

- **Classic 计分逐数字不变**(硬约束):`BlockScoring` 抽取后 Classic 落子分(每格+1)、消除分(格数×10+行列数²×30)与抽取前完全一致。这是接入 Classic 的唯一风险点,优先验。
- **悔棋回滚队列**:落子引发消除→入队 k 个,悔棋后 `PendingElements` 恢复落子前、次数-1(深拷贝 `ToArray()`,非共享引用)。
- **off 模式零触**:`MergeOrderMode=false` 时 `BuildPiece` 不分配 `Elements`、`ElementArr` 保持 null(既有 `Default_MergeOrderModeOff_NoStateNoInject` 覆盖)。
- **合成订单切片既有用例全绿**:改注入后体力/合成配对/订单交付/通关/悔棋既有用例须重新通过。
- **需求②保留集存活**:`CollectElement`/`CollectDemo.Glyph·ColorOf`/`CollectWinWindow`/`CollectClearedElements` 仍被引用、编译 0 error。

### 待 boss 补跑的运行门(dev/test 无 Unity MCP)

- **编译**:Unity 6000.4.7f1 batchmode 编译全工程,确认 0 error(并生成 `BlockScoring.cs.meta`)。
- **EditMode 全量单测**:跑 `MergeOrderTests` 全部用例 + Classic 计分回归用例。
- **Play 手验(人工遗留)**:拖拽落子时元素随得分出现在新候选块的目视确认(狠消一手→下一批候选块明显更多元素)。
