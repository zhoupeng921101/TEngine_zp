# 状态:开发(dev)

> 开工先读本文件 + roles/dev.md + state/plan.md 交接区。每完成一步就更新这里。

## 当前任务
实现「元素合成+订单+体力 demo 切片(merge-order-energy)」。验收标准见 state/plan.md 交接区 15 条。开发完成,待测试。

## 交接区(给测试)

### 改动摘要
在 08 收集切片之上加法式叠加体力/合成/订单三系统,全部由 `MergeOrderMode` 门控,off 时 Classic+08 行为不变。新系统状态独立成 `MergeOrderState` 类(组合,不塞进 BlockGameState),纯逻辑可单测;窗口 `MergeOrderWindow` 镜像 `CollectDemoWindow` 的棋盘/拖拽/落子流程并插入新系统的扣返补/合成/交付/悔棋。表现复用 `CollectDemo.Glyph/ColorOf`,零美术。数值集中 `MergeOrderConfig` 静态类,不接 Luban。

### 文件清单
**新增**
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/MergeOrderConfig.cs` —— 配置常量 + 循环订单池
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/MergeOrderState.cs` —— `Order` 结构 + 状态机(合成区/订单/体力/保底/悔棋快照)
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWindow.cs` —— 切片主窗口
- `Assets/AssetRaw/UI/Prefabs/MergeOrderWindow.prefab`(+ `.meta`,GUID `a9f3c1d7e84b4f0a8c2d6e5b3f10729c`)—— 窗口根 prefab(Canvas+GraphicRaycaster,内容代码构建,镜像 CollectDemoWindow.prefab)
- `Assets/Editor/Tests/BlockBlast/MergeOrderTests.cs` —— EditMode 单测(19 例)

**修改**
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/BlockGameState.cs` —— 加 `MergeOrderMode`/`MergeState` 字段;`BuildPiece` 加 merge 注入分支;新增 `InjectElementsForMergeOrder`(需求拉动+保底);`PlacePiece` 元素转移门控加 `||MergeOrderMode`;`CollectClearedElements` 加可选 `output` 出参(merge 模式不累加 Collected、改输出被清元素列表);新增 `ResetForMergeOrder`/`ExitMergeOrder`
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MainMenuWindow.cs` —— 加「合成订单 DEMO」入口按钮

### 标注
- **热更程序集**:全部改动在 `GameScripts/HotFix/GameLogic`(热更域),无 Main 域改动。
- **Luban**:不涉及(配置硬编码 `MergeOrderConfig`)。
- **需进 Play 模式手验**:拖拽落子手势、订单卡/合成区/体力条/悔棋按钮的实时刷新与点击、双失败条件弹窗、通关弹窗——逻辑层单测已覆盖,但 UI 交互层(`MergeOrderWindow`)无自动化覆盖,须 Play 模式核对(同 08 切片风险集中点)。
- **资源导入**:新增 `.prefab`/`.cs` 需 Unity 资源刷新导入;`Assets/AssetRaw/UI` 按文件名寻址(AddressByFileName),编辑器 YooAsset 模拟模式刷新后即可加载 location="MergeOrderWindow",无需重建 bundle(打包真机才需重建,demo 范围外)。

### 逐条验证点(对应 plan.md 15 条)
> 单测全在 `MergeOrderTests`(命名空间 `GameLogic.BlockBlast.Tests`),建议 `run_tests mode=EditMode filter=GameLogic.BlockBlast.Tests`。

1. **门控&入口**:`MergeOrderMode` 默认 off→`Default_MergeOrderModeOff_NoStateNoInject`;反复进入初始态→`ResetForMergeOrder_FreshStateEachEntry`;退出零残留→`ExitMergeOrder_ZeroResidue_BackToClassic`。入口按钮在 `MainMenuWindow`(Play 验)。Classic/08 回归靠既有 `CollectDemoTests`+`BlockGameStateTests`+本类 `CollectMode_StillAccumulatesStaticTargets_MergeOff`。
2. **体力模型&扣体力**:`Energy_StartSpendAfford`(起始 20/扣 1/体力 0 不可落子)。体力条 UI + 落子流程在 `MergeOrderWindow.PlaceAndResolve`/`OnPieceEnd`(Play 验体力条显示`当前/上限`)。
3. **消除返还体力**:`RefundEnergy_ClampedToCap_RewardOverflows`(返还=行列数、封顶软上限、奖励溢出)。窗口在消除分支调 `RefundEnergy(lines)`。
4. **自然恢复**:**暂不实装**(boss 拍板 A 档)。常量 `RegenPerTick/RegenIntervalSec` 已留口子在 `MergeOrderConfig`。无单测(未实装)。
5. **体力耗尽兜底**:`MergeOrderState.CanAffordPlace` + 窗口 `TriggerGameOver("精力耗尽")`,复用 `GameOverWindow`,触发后 `_finished` 锁输入。**偏差见下「决策2」**(加了"有单可交付则不死"的公平性判定)。Play 验。
6. **消除元素入合成区**:`CollectClearedElements_OutputsList_NoStaticAccumulation`(输出被清元素列表、merge 模式不累加 Collected、清 overlay)。窗口逐个 `IngestElement`。
7. **合成区自动两两合并升级**:`Ingest_TwoSame_MakesOneLv2`/`Ingest_FourSame_MakesOneLv3`/`Ingest_EightSame_MakesTwoLv3_CapStacks`。
8. **合成区 UI**:`MergeOrderWindow.RefreshSynthesis`(底部 token 行,glyph+等级+数量,随摄入/合并/交付重建)。Play 验。
9. **订单数据&双订单**:`Orders_InitialTwoFromPoolFront`。订单卡 UI=`RefreshOrders`(双卡 glyph+Lv+数量)。
10. **订单交付(消耗+奖励)**:`Deliver_ConsumesInventory_GivesReward_RefreshesSlot`(扣合成物+体力+8 可溢出+分=等级×数量×50+完成数+1+刷新槽;库存不足按钮置灰)。窗口 `OnDeliverClicked`。
11. **订单刷新&锯齿波**:`NextOrder_CyclesPool`。循环池在 `MergeOrderConfig.OrderPool`(手编锯齿波,难单后接易单)。
12. **需求拉动注入+保底**:`NeededTypes_UnionOfActiveOrders`+`Pity_ForcesInjectionAfterThreshold`(达阈值强制注入+计数器归零)。注入逻辑 `BlockGameState.InjectElementsForMergeOrder`。
13. **限次免费悔棋**:全量单步快照回滚→`Undo_RollsBackAllState_ReturnsPiece_RefundsEnergy`(6 项状态+方块退槽+退体力+次数-1);次数耗尽→`Undo_ChargesExhausted_CannotUndo`;交付清栈→`Deliver_ClearsUndoStack`。采用**全量快照(A 档,非降级)**。窗口悔棋按钮 `OnUndoClicked`。
14. **demo 通关**:`IsDemoComplete_AtGoalOrders`(达 5 单)。窗口 `TriggerWin` 复用 `CollectWinWindow` 传摘要,锁输入。**偏差见「决策3」**。
15. **棋盘塞满兜底**:窗口 `PlaceAndResolve` 末 `CanPutAnyOf` 判定→`TriggerGameOver`,与体力耗尽(#5)两个独立失败条件。沿用 08。Play 验。

### 决策/偏差(自行裁断,理由记此)
1. **订单数量受自动配对约束(重要,改了订单池数值而非规则)**:#7 自动配对使每个 (类型,非封顶等级) 库存恒 ≤1(满 2 即升级),故 **Lv1/Lv2 订单数量只能为 1**,唯有封顶 Lv3 可堆积、数量方可 ≥2。文档 §3.2 的示例「★ Lv1 ×3」在自动配对下**不可满足**(永远凑不出 3 个 Lv1)。这是设计示例与合成规则的内在张力,但**微调可救**(订单池是 dev 自编配置):`MergeOrderConfig.OrderPool` 只取可满足组合(Lv1×1/Lv2×1/Lv3×1/Lv3×2),难度靠等级拉开(d=数量×2^(等级-1))。规则(#7 自动配对)未动,仅订单数值据此编排。建议策划知悉:生产环境若要「Lv1 ×N」订单,需改合成规则(如订单可吃 N 个 Lv1 而不强制先合并)或允许低级库存堆积——demo 不展开。
2. **软死亡公平性判定(#5 增强)**:文档原文「体力<PlaceCost 且有手持块→软 GameOver」。直接照此会出现"体力 0 但手上有可交付订单(交付可回 +8 体力)却被判死"的误杀。故加判定:仅当**且当前无任一订单可交付**时才触发软死亡(`AnyDeliverable()`)。输入未锁,玩家可手动交付自救。属体验向增强,不影响"真正无路可走"的兜底。
3. **复用 CollectWinWindow 的小瑕疵(已知,未改共享窗口)**:通关复用 `CollectWinWindow`,其「再来一局」按钮硬编码重开 `CollectDemoWindow`(收集 demo),而非 merge demo。为遵「复用」且不改共享窗口,保留现状。通关面板本身(完成单数/累计得分摘要+锁输入)满足 #14。建议后续做专用通关面板或给 CollectWinWindow 加重玩目标参数。
4. **悔棋为 LIFO 多次单步**:每次落子前压全量快照,悔棋按 LIFO 逐步回退,总次数受 `UndoCharges=3` 限。次数耗尽后停止压栈(限增长)。交付为已提交动作,清空悔棋栈(否则悔棋会倒回已交付的库存/进度)。

### 待测试/编译核验(我无法在 sub-agent 内执行)
- **编译未执行**:本会话(dev sub-agent)无 Unity MCP(`read_console`/`run_tests`/`manage_*` 均不可达),**未运行编译与单测**。已对所有调用做手工 API 核验(逐一对照 `UGuiFactory`/`BlockPieceDragger`/`BurstText`/`BinaryBoard`/`RandomSource`/`BlockShapeMap` 实际签名)。**请测试先 Unity 资源刷新→`read_console` 确认编译 0 error→`run_tests mode=EditMode filter=GameLogic.BlockBlast.Tests` 跑全量单测→Play 模式手验上文标注项**。若编译/单测有错,读报错回退给 dev 修。
