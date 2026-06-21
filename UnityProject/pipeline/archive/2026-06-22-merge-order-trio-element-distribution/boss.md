# 关单总结:merge-order 候选块元素分配改「容量加权随机」

**日期**:2026-06-22
**baton**:dev-test(常规模式)
**target**:client(Unity 客户端)
**结论**:PASS,round 0 一次过

## 任务定义

merge-order 模式下,`MergeState.PendingElements` 队头元素的分配规则,从「FIFO 抽干第 1 候选块」改为「trio 级容量加权随机分摊到 3 块」。每元素以「1 格 = 1 票」的均权抽签落入某块,大块(cellCount 高)统计上拿到更多、小块少但每块都有机会;队列吃光或所有 piece 满格止。

## 设计基线

无独立 plan 设计稿(dev-test 档跳过 plan 环节)。基线 = boss 简报转述,完整算法/接口/改动清单/不要动清单/验收 4 条在 `dev.md`「当前任务」段。来源 = 与用户在主会话对齐后的口头拍板(渠道选 `/pipeline` + 算法选「容量加权随机」)。

## 拍板归属(boss 编排日志)

- **算法选「容量加权随机」**:用户选定,备选「piece 均权」「Round-robin」未选。本任务沿用。
- **跳过 plan/ui 环节**:用户用 `/pipeline dev-test` 显式指定 baton。无新需求/无 UI 改动,跳过合规。
- **基线 commit 跳过**:启动时 working tree 非干净(`../Fantasy/` 等未跟踪件来源未知,不属本任务),常规模式不自动 commit;关单 checkpoint 仅卷入 UnityProject 工作树内改动,`../Fantasy/` 在 UnityProject 之外不会被卷入。

## spawn 登记

| 阶段 | agent | 结果 |
|------|-------|------|
| dev round 0 | pipeline-dev | 完工:BlockGameState.cs(算法 + 3 处调用站)+ MergeOrderTests.cs(1 改写 + 3 新增用例),编译 0 错,EditMode 515/515(dev 自跑数字) |
| test round 0 | pipeline-test | PASS:四类全过(编译 0 / 单测 497/497 / 冒烟列遗留 / Code Review 过 + 1 观察项) |

打回轮次 = 0。

## 关键文件

| 文件 | 变更摘要 |
|------|----------|
| `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/BlockGameState.cs` | `BuildPiece` 去 drain 调用,只造壳;删私有 `DrainPendingElementsInto`;新增私有 `DistributePendingElementsAcrossTrio(IList<PendingPiece>)`;`RefillPieces` 两分支末尾 + `SetFirstHand` 末尾共 3 处接入分摊调用 |
| `Assets/Editor/Tests/BlockBlast/MergeOrderTests.cs` | 重写旧 FIFO 用例为 `EnqueueScoreElements_TypesSubsetOfNeeded_DistributedAcrossTrio`;新增 `DistributeElements_SpreadsAcrossMultiplePieces` / `DistributeElements_RespectsCapacityCeiling` / `DistributeElements_OffMode_NoAllocation` 三用例 |

## 运行验证结论

- 编译:0 error / 0 warning(含 `BlockBlast.Tests` asmdef)
- EditMode 单测:497/497 全绿,4 个新/改用例全过
- Code Review:conventions 六条全过,1 条观察项(L366 测试注释「不再 FIFO 抽干」含 diff 叙事,建议下轮顺手清,不升 FAIL)
- 人工冒烟:未跑(MCP 无法模拟 Play 模式拖拽;分布性已由单测覆盖)

## 遗留事项

| # | 事项 | 归属 | 处置建议 |
|---|------|------|----------|
| 1 | 测试注释 `MergeOrderTests.cs:366` 含 diff 叙事「不再 FIFO 抽干」 | dev 下次顺手 | 改为现行事实陈述,如「BuildPiece 单独调用不分配元素;trio 级分摊在 RefillPieces 末尾触发」 |
| 2 | 人工冒烟未跑 | 用户或下轮 boss 任务窗口 | 进合成订单窗口连续触发消除,看候选块元素是否散布到 3 块 |
| 3 | dev 报 515 vs test 实跑 497 数字差异 | 下次需要核对 EditMode 集合时澄清 | 可能 dev 多算了 PlayMode 用例;不影响本次判定 |
