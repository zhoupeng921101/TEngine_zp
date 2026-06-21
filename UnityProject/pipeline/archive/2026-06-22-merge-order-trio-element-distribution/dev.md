# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务

**merge-order 候选块元素分配改「容量加权随机」**(dev-test 档,无 plan 在环,设计基线 = boss 简报转述)。

### 任务一句话

把 `MergeState.PendingElements` 的元素分配从「FIFO 抽干第 1 候选块」改成「trio 级容量加权随机分摊到 3 块」。

### 目标算法(用户已拍板)

每从队头取 1 个元素,就在「3 个候选块剩余空格」里按空格数加权随机抽 1 格落入(每空格中签概率均等)。结果:大块(cellCount 高)拿到更多、小块少但都有机会;队列吃光或所有 piece 满格止。

### 改动清单

#### `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/BlockGameState.cs`

1. **`BuildPiece(int shapeId)`(约 108-113 行)**:移除 `DrainPendingElementsInto(piece, shapeId)` 调用,只造壳返回。
2. **新增 `DistributePendingElementsAcrossTrio(IList<PendingPiece> trio)`**:
   - 短路条件:`!MergeOrderMode || MergeState==null || queue.Count==0 || totalCap==0`。
   - 用 `int[] capacity` 跟踪每块剩余空格(初值 = `BlockShapeMap.GetCellCount(piece.ShapeId)`),`totalCap` 是 capacity 总和。
   - `while (queue.Count>0 && totalCap>0)`:`pick = RandomSource.Index(totalCap)` → 按累计区间定位中签 piece(跳过 capacity==0 的)→ 弹队 1 元素入该 piece 的 bucket → 对应 `capacity[i]-- / totalCap--`。
   - 写回 piece.Elements:每 piece 的 bucket 按入桶序顺次填到 `new MergeElement[cellCount]` 的前若干格(与 `PlacePiece` 中 `cellIdx` 行优先填入同序——早入桶元素落到更早的填充格);余格保持 `MergeElement.None`。
   - **随机源**:统一走 `RandomSource.Index(...)`,不要混用 `System.Random`(与现有代码一致,且 RandomSource 是测试可控的)。
3. **三处 trio 构造站调用新方法**:
   - `RefillPieces` 动态调度分支(约 178-189 行):3 次 `BuildPiece` 之后、`return` 之前。
   - `RefillPieces` 随机无死分支(约 191-211 行):兜底 3×1×1 / 正常 `chosen` 两路落到 `OperaArr` 之后,共调一次(放分支末尾 `for (int i = 0; i < 3; i++) OperaArr[i] = chosen[i];` 之后)。
   - `SetFirstHand`(约 214-218 行):3 次 `BuildPiece` 之后调一次(经典窗口已下线但保一致性,且后续可能被 ResetForMergeOrder 或测试代码调到)。
   - **不要在 `RandomDistinctTrio` 里也调**:它被 `RefillPieces` 包裹,会双重处理。

#### `Assets/Editor/Tests/BlockBlast/MergeOrderTests.cs`

旧的 FIFO 抽干断言(约 366-373 行,用 `s.BuildPiece(13)` 后断言 `Elements[0]==expected[0]` / `Elements[1]==expected[1]` / `Elements[2]==None` / 队列被抽干)需重写:
- 删旧断言——`BuildPiece` 单独调用不再吃队列。
- 改为:`s.RefillPieces(board)` 之后断言:
  - 队列剩余 = `max(0, originalQueueCount - sum(cellCount of 3 pieces))`(队列被吃光或剩部分均合法)。
  - 3 个 `OperaArr[i].Elements` 加起来非 `None` 的元素数 = 原始入队数 - 剩余。

**新增至少 2 个用例**(用 `BlockBlast.Tests` asmdef 现有命名风格):
- **分布性**:入队 12 个元素(顶到 `MaxPendingElements`)+ 让 trio 三块 cellCount 都 ≥4(可由 RefillPieces 自然产生或临时塞 `OperaArr`)→ 3 个 piece 的 Elements 非 None 计数都 > 0(不再全 0;统计意义上需多次跑或者绑定 `RandomSource` 种子,见下)。
- **容量上限**:trio 总 cellCount > 12 时 → 3 piece bucket 总和 = 12,队列剩 0;总 cellCount < 12 时 → bucket 总和 = totalCap,队列剩 `12 - totalCap`。
- (可选第 3)**off 模式不分配**:`MergeOrderMode=false` 时 `RefillPieces` 后 `OperaArr[i].Elements` 全 null(保经典零回归)。

> **RandomSource 种子**:先看 `RandomSource.cs` 是否提供 `Seed(int)` / `Push(seed)` 类接口可在测试里固定。若没有,「分布性」用例改为弱断言:**至少 2 个 piece 拿到了 ≥1 元素**(单跑分布性不强但能挡「全 0/全集中」回归)。

### 不要动

- `MergeState.PendingElements` 入队/容量规则(`EnqueueScoreElements` / `MaxPendingElements=12`)。
- 元素 → 订单类型的产出映射(`NeededTypes` / 轮转游标)。
- `PlacePiece` 中 `piece.Elements[cellIdx]` 转移到 `ElementArr` 的行优先填充顺序(保此顺序,bucket 写回时按入桶序填前若干格即可同序)。
- 经典模式 / `MergeOrderMode=false` 分支:分摊方法必须短路、`Elements==null`,零回归。

### 验收(test 负责,dev 自检覆盖前 2 条)

1. 编译 0 error / 0 warning(含 `Editor` asmdef)。
2. EditMode 单测全绿(基线 444/444 — 详 boss.md「最近关单」第 2026-06-21 EVENT 解锁客户端段;新加用例补进去后整体仍全绿)。
3. Code Review:对照 `.claude/rules/conventions.md` 走收尾自检(新增方法注释、避免过程性叙事、避免可推导事实副本)。
4. (可选)人工冒烟:进入合成订单窗口,触发 1-2 次消除后看下一批候选块——元素不再集中在 #1,3 块上都能见到。

### 交接给 test 写什么

`state/dev.md`「交接」段填:改动文件清单 + 每文件改动摘要(不写 diff) + EditMode 跑通的 N/N + 已自查的不变项(经典零回归、PlacePiece 顺序保留)+ 是否走了 RandomSource 种子(若没有,说明分布性用例采用了弱断言)。

## 交接(dev → test)

### 改动文件清单

| 文件 | 变更 |
|------|------|
| `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/BlockGameState.cs` | `BuildPiece` 去 Drain 调用,只造壳;删私有方法 `DrainPendingElementsInto`;新增私有方法 `DistributePendingElementsAcrossTrio(IList<PendingPiece>)`;`RefillPieces` 动态分支末尾 `return` 前调一次;`RefillPieces` 随机无死分支末尾 `OperaArr` 赋值后调一次;`SetFirstHand` 末尾调一次。 |
| `Assets/Editor/Tests/BlockBlast/MergeOrderTests.cs` | 重写旧 FIFO 抽干用例为 `EnqueueScoreElements_TypesSubsetOfNeeded_DistributedAcrossTrio`(断言 BuildPiece 单独调用不再吃队列 + RefillPieces 触发分摊 + 总数守恒);新增 3 个用例:`DistributeElements_SpreadsAcrossMultiplePieces`(分布性:种子固定+totalCap=27>队列 12,至少 2 块拿到元素)、`DistributeElements_RespectsCapacityCeiling`(双 case:totalCap>队列全分摊队列空;totalCap<队列 bucket 总和=totalCap,队列剩 `队列-totalCap`)、`DistributeElements_OffMode_NoAllocation`(off 模式 RefillPieces 后 `Elements==null` 零回归)。 |

### 改动摘要

**算法**:从「FIFO 抽干第 1 块」改为「trio 级容量加权随机分摊到 3 块」。每元素 `pick = RandomSource.Index(totalCap)` → 按累计区间定位中签 piece → 弹队入桶 → `capacity[i]--/totalCap--`,直至队空或 totalCap=0。每 piece 的桶按入桶序写到 `new MergeElement[cellCount]` 前若干格,余格保持 `None`(与 `PlacePiece` 行优先转移顺序同源,不需要改 PlacePiece)。

**短路条件**:`!MergeOrderMode || MergeState==null || queue.Count==0 || totalCap==0` 任一命中即返回,piece.Elements 全保持 null,与经典模式逐字节一致。

**随机源**:统一走 `RandomSource.Index(...)`(测试可控,`SetSeed(int)` 已存在)。

**未命中块的 Elements**:命中过的块在写回阶段挂上 `Elements`(可能含尾部 None),未命中的块 `Elements` 保持 `null`(更贴近原行为「无元素 = 纯方块」)。

### 自检结果

- 编译:`read_console errors` 仅返 3 条 PlayMode 期 NRE(无堆栈),无 CSxxxx,0 编译错。
- EditMode:`run_tests editmode` → 515/515 全绿(基线 444 偏低系本会话前已涨,关键是 0 failed;新加 3 个 + 改写 1 个用例都在 `BlockBlast.Tests` asmdef,集合内全过)。
- 不变项自查:
  - `MergeState.PendingElements` 入队规则未动(`EnqueueScoreElements` / `MaxPendingElements=12`)
  - `NeededTypes` / 轮转游标未动
  - `PlacePiece` 行优先转移逻辑未动(新算法只改 piece 内桶填充,转移顺序源不变)
  - 经典模式短路:off 测试 `DistributeElements_OffMode_NoAllocation` 显式断言 `Elements==null` 零回归
- RandomSource 种子:走通(已有 `SetSeed(int)`,SetUp 固定 20260612)。分布性用例「至少 2 块拿到 ≥1 元素」+ totalCap=27 显著大于队列 12,在固定种子下稳定为强断言;未退化到弱断言。

### 给 test 的提示

- 重点核 4 个新/改用例的断言强度是否符合「分布性 + 容量上限 + off 零回归」三层覆盖。
- 反射调私有 `DistributePendingElementsAcrossTrio` 是为绕开 `RefillPieces` 内的 board.CheckPutAllBlocks 重试干扰(测试需手工指定三块都 9 格 / 都 1 格)。如不想用反射,可改为通过 `RefillPieces` + 控制 board 状态间接验证——但断言会模糊。
- 人工冒烟可选:进 MergeOrder 窗口,连续触发几次消除堆队列后看候选块,元素应散布到 3 块,不应总堆在第 1 块。
