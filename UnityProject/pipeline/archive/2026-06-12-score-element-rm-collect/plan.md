# 状态:策划(plan)

> 开工先读本文件 + roles/plan.md。每完成一步就更新这里。

## 当前任务:score-element-rm-collect(plan 棒产出,待派 dev)

**设计全文**:`design-docs/10-score-element-rm-collect.html`(已挂 index;并在 09 §四风险1 加了「注入机制已更新」指针)。本交接区只列拆解 + 决策 + 给 dev/test 的可落地清单,数值理由与边界推导见 HTML,不在此重复。

### 需求拆解
- **需求①**:合成订单切片的元素生成,由「每填充格固定概率 `InjectChance=0.30` 注入」改为「**该次消除得分驱动元素数量**」。得分越高,后续候选块携带元素越多;无消除→候选块纯方块。
- **需求②**:彻底移除 2026-06-11 收集 Demo 玩法,甄别保留与合成订单共用的元素基础设施。

### 关键核查结论(已 grep 全工程验证)
- 需求② main **已做净**:收集专属代码/预制/测试已删,`CollectDemoWindow`/`CollectMode`/`ResetForCollectDemo`/`CollectionTargets`/`DemoTargets`/`CollectTarget`/`FromKey`/`Collected` 全工程 0 匹配;已删预制 GUID 在 Assets 下 0 引用。**无悬空引用、无孤儿 meta**。
- **无硬冲突**:「彻底移除收集」与「保留合成订单 + 元素携带设施 + CollectWinWindow」不冲突——main 已把两者干净分离。保留集是共享/已转用设施,非收集玩法。可直接推进 dev,无需停手回询。
- 残留仅为**命名误导**(misnomer):`CollectDemo`/`CollectWinWindow`/`CollectClearedElements` 名字带 Collect 但已转用。**本批不改名**(改名零功能收益且 CollectWinWindow 改名要动预制 location 字符串/.prefab/.meta/[Window]/调用点,对已交付切片是风险,与"勿动合成订单"约束冲突)。正名列独立低优先级任务。

### 核心设计决策
1. **得分→元素映射(用户点名要审,§2.3)**:线性系数 + 保底 + 封顶。
   `k = clearScore<=0 ? 0 : Clamp(CeilDiv(clearScore, ScorePerElement), MinPerClear, MaxPerClear)`
   默认 `ScorePerElement=200 / MinPerClear=1 / MaxPerClear=4 / MaxPendingElements=12`。
   得分公式复用 Classic:`clearScore = 被清格数×10 + 被清行列数²×30`。
   逐档:0→0、单消110→1、双消280→2、三消500→3、四消780→4、五消+封顶4。
   理由/边界见 HTML §2.3。`ScorePerElement` 是「灵活」旋钮。
2. **投放时机**:得分算出的 k 个元素压入新增队列 `MergeState.PendingElements`,**补牌(BuildPiece)时 FIFO 抽干填入新候选块**(沿用 09「补牌时注入」节奏,最小改动、可纯逻辑单测)。备选「消除当下即时注入」列后续 UX 增强。
3. **类型选择**:沿用需求拉动——只从 `NeededTypes()` 取,多类型按轮转游标 `_needRotor` 均摊。
4. **旧机制退役**:`InjectChance`/`RequireClearForInject`/`ClearedSinceRefill`/`PityThreshold`/`PitySinceNeeded` 全删(确定性+轮转下,「无消除无元素」「不饿死类型」均自动满足)。
5. **不动**:合成区/订单/体力/通关/悔棋系统;合成订单显示分数(订单交付 TotalScore)不掺 clearScore——clearScore 仅作元素生成内部驱动量。

### 给 dev 的改动点(明确、可落地;完整表见 HTML §四)
- `MergeOrderConfig.cs`:删 `RequireClearForInject`/`InjectChance`/`PityThreshold`;增 `ScorePerElement=200`/`MinElementsPerClear=1`/`MaxElementsPerClear=4`/`MaxPendingElements=12`;增静态 `int ElementsForScore(int clearScore)`(实现 §2.3 公式)。
- `MergeOrderState.cs`:删 `PitySinceNeeded`/`ClearedSinceRefill` 字段;增 `Queue<CollectElement> PendingElements` + `_needRotor`;增 `EnqueueScoreElements(int k)`(从 NeededTypes 轮转取 k 压队,受 MaxPendingElements 截断,NeededTypes 空则跳过);`Reset()` 清队列+游标;`Snapshot.Capture/Restore` 去 `_pity`/`_clearedSinceRefill`、**深拷贝 PendingElements**(悔棋须回滚队列)。
- `BlockGameState.cs`:`InjectElementsForMergeOrder()` 改写为 `DrainPendingElementsInto(piece, shapeId)`(按填充格行优先 FIFO 抽队列填 `piece.Elements`,队空则不分配=纯方块);`BuildPiece` 调用点改调它;删 `ClearRowsAndCols` 里 `ClearedSinceRefill=true` 置位与 `OnHandRefilled` 的复位。元素转移/`CollectClearedElements`/持久化不动。
- 新增 `BlockScoring.cs`(推荐):抽 `ClearScore(clearedCells,lines)`=`clearedCells*10+lines*lines*30`,Classic 与 MergeOrder 共用。**硬约束:Classic 计分逐数字不变**。风险高则退内联同公式+注释同源。
- `MergeOrderWindow.cs`:`PlaceAndResolve` 消除分支捕获 `int clearedCells = _state.ClearRowsAndCols(...)` 返回值 → 算 clearScore → `k=ElementsForScore` → `_merge.EnqueueScoreElements(k)`,**置于 RefillPieces 之前**。其余不动。
- `MergeOrderTests.cs`:删/改写旧注入模型用例(`Inject_GatedUntilClear_FirstHandClean`/`Inject_AllowedAfterClear`/`Inject_GateClosesAgainAfterRefill`/`Undo_RestoresClearGateFlag`/保底注入);新增下列验收对应用例。

### 给 test 的验收点(逐条,完整见 HTML §五)
- 需求①:映射逐档(0→0…780→4…2000封顶4);无消除→队列恒空→候选块纯方块、首手三块全干净;消除即入队 k 个、补牌即 FIFO 投放、类型⊆NeededTypes;四消产出>单消;双订单类型轮转均摊;积压封顶不超 MaxPendingElements;悔棋回滚队列(撤回 k 个、次数-1);off 模式零触(不入队/不分配 Elements/ElementArr 保持 null)。
- 需求②:删收集后编译 0 error;保留集存活且被合成订单引用(CollectElement/CollectDemo.Glyph·ColorOf/CollectWinWindow/CollectClearedElements);合成订单切片既有用例全绿(① 改注入后须重新通过);主菜单无收集入口、无 CollectDemoWindow 引用/孤儿 meta;Classic 计分逐数字不变。
- **运行门**:dev/test 无 Unity MCP,编译+EditMode 全量单测由 boss batchmode 补跑;Play 手验(元素随得分出现在新候选块的目视确认)列人工遗留。

### 待拍板(交 boss/用户;默认采纳建议推进,见 HTML §六)
1. 得分→元素映射数值(核心):`ScorePerElement=200`+保底1+封顶4——慷慨/稀有可调系数。
2. 投放时机:补牌时抽干(推荐) vs 消除即时注入(后续增强)。
3. 共享设施 `Collect*` 命名:保留+标注(推荐) vs 正名(独立低优先级任务)。
