# 状态:测试(test)

> 开工先读本文件 + roles/test.md + state/dev.md 交接区。每完成一项验证就更新这里。

## 当前被测任务:score-element-rm-collect

**总判定:PASS**

验收口径:以代码审查 + git diff 为主(子会话无 Unity MCP)。运行门(编译 0 error、EditMode 96/96)由 boss 经 Unity MCP 跑完,作为既定事实采信。

### 验收逐项结果

**①【最高优先·硬约束】Classic 计分逐数字不变 — PASS**
依据:`git diff HEAD GameWindow.cs` + 读 `BlockScoring.cs`。
- 落子分:抽取前 `int placementScore = CountCells(shape);` → 抽取后 `BlockScoring.PlacementScore(CountCells(shape))`,而 `PlacementScore(cells)=>cells`。逐表达式等价。
- 消除分:抽取前 `int clearScore = clearedCells * 10 + lines * lines * 30;` → 抽取后 `BlockScoring.ClearScore(clearedCells, lines)`,而 `ClearScore(clearedCells,lines)=>clearedCells*10+lines*lines*30`。操作数/系数/顺序逐数字一致。
- 这两处是 GameWindow.cs 仅有的两处改动(diff 仅 2 行),无其它行为变更。判定逐数字等价。

**② dev「需要 test 重点验的点」逐项 — PASS**
- 悔棋回滚队列:代码审查 `MergeOrderState.Snapshot.Capture` 用 `PendingElements.ToArray()` 深拷贝、`Restore` 先 `Clear()` 再 `foreach Enqueue` 重建队列 + 回滚 `_needRotor`;`_pity`/`_clearedSinceRefill` 已从快照移除。单测 `Undo_RestoresPendingElementsQueue`(入队 3 → Undo → 恢复落子前)覆盖,在 96/96 内。
- off 模式零触:`BuildPiece` 仅 `MergeOrderMode && MergeState!=null` 时调 `DrainPendingElementsInto`;`PlacePiece` 转移门控由 `CollectMode||MergeOrderMode` 收窄为 `MergeOrderMode`。单测 `OffMode_PlacePiece_DoesNotTouchElements`(off 下塞 Elements 仍断言 `ElementArr==null`)覆盖。
- 合成订单切片既有用例全绿:EditMode 96/96、0 失败(boss job `fa3867c646fd46b991f983c8fac7283e`),含体力/合成配对/订单交付/通关/悔棋既有用例。
- 需求②保留集存活且被引用(grep 实证,编译 0 error):`CollectClearedElements`←MergeOrderWindow:487;`CollectWinWindow`←MergeOrderWindow:565;`CollectDemo.Glyph/ColorOf`←MergeOrderWindow 8 处 + MergeOrderConfig 注释;`CollectElement` 跨 7 文件引用。

**③ 新增 7 用例审查 — PASS(断言均有实质,无空跑/虚设)**
读 `MergeOrderTests.cs` 确认:
- `ElementsForScore_MapsPerTier`:-50/0→0、1/110/200→1、280→2、500→3、780→4、2000→4(封顶)。覆盖 A 档映射 `Clamp(CeilDiv(score/200),1,4)`、保底、封顶。
- `HigherScore_YieldsMoreElements`:四消 `ClearScore(30,4)=780→4` > 单消 `ClearScore(8,1)=110→1`。
- `EnqueueScoreElements_TypesSubsetOfNeeded_DrainsFifoOnBuild`:入队 2、类型⊆NeededTypes、BuildPiece(13) 行优先 FIFO 抽队头 2 个、余格 None、队列抽干为 0。
- `NoClear_QueueEmpty_PiecesClean`:首手三块全 null、队空再 BuildPiece 仍 null。
- `EnqueueScoreElements_RotatesAcrossNeededTypes`:双订单(needed≥2),入队 4 覆盖 needed[0] 与 needed[1]。
- `EnqueueScoreElements_CapsAtMaxPending`:入队 100 截断至 12,满后再入 5 仍 12。
- `Undo_RestoresPendingElementsQueue`:见②。
另查 `MergeOrderConfig.ElementsForScore` 实现:`clearScore<=0→0`,否则 `CeilDiv=(score+199)/200` 再 Clamp(1,4) — 与逐档断言一致。

**④ 需求②移除净 + 未牵连保留 — PASS**
- `git status` 确认已删:`CollectDemoWindow.prefab(.meta)`、`CollectDemoTests.cs(.meta)`、`CollectDemoWindow.cs(.meta)`。
- 全工程 grep 旧符号(`CollectMode`/`CollectionTargets`/`ResetForCollectDemo`/`CollectDemoWindow`/`DemoTargets`/`CollectTarget`/`FromKey`/`.Collected`/`ScoreKey`/`InjectChance`/`PityThreshold`/`PitySinceNeeded`/`RequireClearForInject`/`ClearedSinceRefill`/`IsCollectionComplete`/`ExitCollectMode`):**0 匹配**,无悬空引用。
- 未牵连:merge-order 元素携带链路(`PendingElements`/`ElementArr`/`PlacePiece` 转移/`BuildPiece` 钩子)与 `CollectWinWindow`(已复用为合成订单通关面板,文案改「通关!」、再来一局回 `MergeOrderWindow`)均保留并正常引用。MainMenuWindow 收集入口按钮已删、合成订单按钮上移补位。
- `BlockScoring.cs.meta` 已生成。

**CONVENTIONS 交叉检(test 对 dev 改过的持久文件):** `state/dev.md` 交接区 lint 无指代词/diff 叙事命中,可识别所属任务。通过。

### 人工遗留(不计入 FAIL)
- Play 模式拖拽手验:狠消一手后下一批候选块目视更多元素、随得分增减 — 子会话无 Unity MCP 做不了,留人工目视确认。逻辑层已由单测覆盖(映射/入队/抽干/轮转/封顶)。

### 结论
四类验证(编译/单测/审查;手验列遗留)全过,Classic 计分硬约束逐数字不变,需求②移除净且保留集完整。**通知 boss:可交付。**
