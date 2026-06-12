# 状态:测试(test)

> 开工先读本文件 + roles/test.md + state/dev.md 交接区。每完成一项验证就更新这里。

## 当前被测任务
merge-order-energy 切片(元素合成+订单+体力 demo)。验收基线:state/plan.md 15 条 + design-docs/09。

## 总判定:PASS(boss 补跑运行门通过,2026-06-12;Play 手验列遗留)

test sub-agent 会话未挂载 Unity MCP,第 1/2/3 类无法在测试会话运行,初判 BLOCKED 并移交 boss 补跑。
boss 于 2026-06-12 经 Unity 6000.4.7f1 命令行 batchmode 补跑(编辑器未占用工程,无 MCP 亦可行):
- **第 1 类 编译**:通过——batchmode 进程退出码 0,日志无 `error CS` / `Compilation failed`
- **第 2 类 单测**:通过——`MergeOrderTests` 19/19 绿;全量 EditMode **96/96 绿**(08/Classic 基线 77 例回归无破坏)
- 运行证据:`TestResults/merge-order-editmode.xml` + `TestResults/merge-order-run.log`(UnityProject 下)
- **第 3 类 Play 手验**:batchmode 无法交互,**列遗留**(归属见 state/boss.md);逻辑层已被单测全覆盖,遗留仅 UI 交互表现
- 第 4 类 code review:测试会话已完整执行,PASS、未发现 FAIL 级缺陷

## 四类逐项结果

### 第 1 类 编译验证 —— 通过(boss batchmode 补跑;测试会话仅静态核验)
代行静态核验:逐一对照被调用 API 的实际签名,未发现类型 / 签名 / 命名空间错误。已核签名:
- `BinaryBoard`:`RowBinary`(public int[])/`IsEmpty()`/`ConvertFromArr`/`PutBlock`/`CanPutBlock`/`CanPutAnyOf(int[])`/`CanClearRowCols(bool)→ClearResult{Rows,Cols:List<int>}`(Core/BinaryBoard.cs、Core/ClearResult.cs)
- `BlockShapeMap.GetCellCount(int)`/`Get(int)`(Core/BlockShapeMap.cs:125)
- `RandomSource`:`SetSeed`/`NextDouble`/`Index`/`Range`(RandomSource.cs)
- `PendingPiece(int,BlockColor)` + `Elements`(public CollectElement[])(PendingPiece.cs:26/28)
- `BlockColor.Blue=0`(BlockColor.cs)
- `CollectElement` 枚举含 Diamond/Star/Leaf/Heart(订单池所用类型全部存在;CollectDemo.cs)
- `SimpleSingleton<T>`:`Instance`/`IsValid`/`Release()`(Release 置 _instance=null → 下次 Instance 重建并 OnInit,单测 SetUp/TearDown 隔离成立;SimpleSingleton.cs)
- `UGuiFactory`:`CreateText(...,TextAnchor=MiddleCenter,FontStyle=Bold)` 重载、`CreateButton(...,out Image,out Text)` 重载、`CreateImage/CreateNode/CreateContentPanel/PlaceByDesignCenter` 均与窗口调用匹配(UGuiFactory.cs)
- 窗口 `BurstText.Spawn` / `ShowUIAsync<CollectWinWindow>(List<string>)` / `ShowUIAsync<GameOverWindow>(int)` / `InitDynamicWeight`+`BeginGame` 与 08 `CollectDemoWindow` 逐行同构(已比对)
- 测试程序集 `BlockBlast.Tests.asmdef` 引用 GameLogic / TEngine.Runtime / nunit,新增 .cs 在 Tests 目录由 asmdef 自动纳入

资产:`MergeOrderWindow.prefab` + `.meta` 存在,meta GUID = `a9f3c1d7e84b4f0a8c2d6e5b3f10729c`(与 dev 交接一致)。
> 静态核验不替代真编译:HybridCLR 热更域、YooAsset 模拟寻址 location="MergeOrderWindow" 的实际导入仍须 Editor 刷新确认。

### 第 2 类 单元测试 —— 通过(boss batchmode 补跑 19/19,全量 96/96;测试会话仅静态推演)
代行静态推演:对 `MergeOrderTests` 全部用例逐条手工执行断言路径,**逻辑上全部通过**。覆盖核对见下表。
- 合成自动配对级联(Two/Four/Eight)逐步手推:8 连摄入 Leaf → Lv3×2 封顶堆积,与断言一致。
- 悔棋全量回滚:快照深拷贝 SaveArr/ElementArr/RowBinary/OperaArr/Combo + 体力/库存/订单/游标/完成数/得分/保底,Restore 全部还原;UndoCharges 不入快照(避免无限悔棋),断言 charges-1 成立。
- 交付清栈、次数耗尽不压栈、订单循环越尾回绕、需求并集、保底强制注入后计数归零 —— 均与实现一致。
- off 回归:`CollectMode_StillAccumulatesStaticTargets_MergeOff` 覆盖 08 累加路径;BuildPiece/PlacePiece/CollectClearedElements 的 off 分支短路逐字节不变(门控 `(CollectMode||MergeOrderMode)`,双 off 即原路径)。

**测试数量出入(观察项)**:dev.md 文件清单记「EditMode 单测(22 例)」、本任务派单亦记「22 例」,
实际文件含 **19 个 `[Test]` 方法**。逐点核对覆盖完整(下表),故判为计数笔误而非覆盖缺口,非 FAIL。

15 验收点 × 单测覆盖:

| # | 点 | 单测覆盖 | 备注 |
|---|----|---------|------|
| 1 | 门控&入口&重置 | Default_MergeOrderModeOff / ResetForMergeOrder_FreshState / ExitMergeOrder_ZeroResidue / CollectMode_StillAccumulates(回归) | 入口按钮 Play 验 |
| 2 | 体力扣减 | Energy_StartSpendAfford | 体力条 UI Play 验 |
| 3 | 消除返还 | RefundEnergy_ClampedToCap_RewardOverflows | |
| 4 | 自然恢复 | 无(A 档未实装,boss 认可) | 常量留口子 |
| 5 | 体力耗尽兜底 | 无单测(窗口 AnyDeliverable+TriggerGameOver) | **Play 验**;决策2 公平性判定 |
| 6 | 元素入合成区 | CollectClearedElements_OutputsList_NoStaticAccumulation | |
| 7 | 自动两两合并 | Ingest_TwoSame / FourSame / EightSame_CapStacks | |
| 8 | 合成区 UI | 无单测(RefreshSynthesis) | **Play 验** |
| 9 | 双订单 | Orders_InitialTwoFromPoolFront | 订单卡 UI Play 验 |
| 10 | 交付消耗+奖励 | Deliver_ConsumesInventory_GivesReward_RefreshesSlot | |
| 11 | 刷新&锯齿波 | NextOrder_CyclesPool | 锯齿波节奏=手编池,数值见决策1 |
| 12 | 需求拉动+保底 | NeededTypes_UnionOfActiveOrders / Pity_ForcesInjectionAfterThreshold | |
| 13 | 限次免费悔棋 | Undo_RollsBackAllState / Undo_ChargesExhausted / Deliver_ClearsUndoStack | |
| 14 | demo 通关 | IsDemoComplete_AtGoalOrders | 通关面板复用 CollectWinWindow,决策3 |
| 15 | 棋盘塞满兜底 | 无单测(窗口 CanPutAnyOf+TriggerGameOver) | **Play 验** |

### 第 3 类 手动功能验证(Play) —— 遗留(batchmode 无法交互,逻辑层已由单测覆盖)
全部 Play 验项遗留待补:体力条扣/返/补显示、订单卡点亮/置灰/交付、合成区 token 实时重建、
悔棋按钮态与回滚手感、双失败弹窗(精力耗尽软死亡 / 棋盘塞满硬死亡)、通关弹窗、拖拽落子手感。
> 拖拽手感按角色记忆列「需人工 Play 手验」;其余逻辑表现可在 Play 内用代码驱动落子复核。截图须经 UICamera 取景存 Assets/Screenshots/merge_0*.png。

### 第 4 类 Code Review —— 已执行,PASS
diff review 对照 dev.md 文件清单(5 新增 + 2 修改)逐文件读完。

**编码红线(CLAUDE.md 核心原则)逐条**:
1. 异步优先:窗口 IO 走 `ShowUIAsync`,无同步加载 / Coroutine;`InitDynamicWeight` 与 08 同构。✓
2. 模块访问:经 `GameModule.UI`,未见 `ModuleSystem.GetModule<T>()`。✓
3. 资源释放:窗口内 UI 由 UGuiFactory 运行时构建、随窗口销毁;无裸 `LoadAssetAsync`;`Resources.GetBuiltinResource<Font>` 为内置字体无需卸载;prefab 由 UIWindow 框架托管。✓
4. 热更边界:全部改动在 `GameScripts/HotFix/GameLogic`(热更域),无 Main 域改动。✓
5. 事件解耦:本切片无跨模块事件,窗口内用按钮 onClick;无 GameEvent 滥用 / 泄漏风险。✓

**门控隔离(回归硬验收)**:`MergeOrderMode` off 时三处分支(BuildPiece 注入 / PlacePiece 元素转移 / CollectClearedElements)全部短路,Classic+08 路径不变;`ResetForMergeOrder` 进入时显式置 `CollectMode=false`、`ExitMergeOrder` + 窗口 OnDestroy 双保险关门控释放 ElementArr/MergeState,零残留。✓

**逻辑正确性**:合成级联、悔棋深拷贝、订单循环、保底归零、软上限钳制 / 奖励溢出 —— 全部静态推演自洽(见第 2 类)。未发现缺陷。

**dev 4 条自裁决策**:订单池数值受自动配对约束(决策1)/ 软死亡公平性判定(决策2)/ 复用 CollectWinWindow 重开按钮指向 collect(决策3)/ 悔棋 LIFO 多步(决策4)—— 均已 boss 认可,按实际行为核验,与设计文档差异不判 FAIL,记此为观察。

**CONVENTIONS 交叉检(dev 改过的持久文件 state/dev.md)**:正文为任务态交接、归属清晰;决策/偏差段为 handoff 合法过程留痕。一处可推导事实漂移:文件清单「单测(22 例)」与实际 19 例不符(应改为 19,或补足缺的 3 例)。建议 dev 收尾时校正计数。

## 补跑执行记录(boss,2026-06-12)
原清单按 Unity MCP 写;实际 boss 会话亦无 MCP,改用等效命令行 batchmode 一次覆盖前 4 步:
`Unity.exe -batchmode -projectPath <UnityProject> -runTests -testPlatform EditMode -testResults TestResults/merge-order-editmode.xml -logFile TestResults/merge-order-run.log`
1-2. 编译:退出码 0、日志无 error CS ✓
3. `MergeOrderTests` 19/19 绿 ✓
4. 全量 EditMode 96/96 绿(77 基线回归 + 19 新增)✓
5. Play 手验:batchmode 不可交互,未执行 → 列遗留(归属 state/boss.md)
