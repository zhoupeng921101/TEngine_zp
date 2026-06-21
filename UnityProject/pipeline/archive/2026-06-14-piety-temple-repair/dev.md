# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:虔诚币 + 神庙修复 长期主线(design 13)

设计稿 `design-docs/13-piety-temple-repair.html`,验收标准在 `pipeline/state/plan.md`。已实现 + 编译自检 + 自跑 EditMode 全绿,交测试。

### 改动摘要(做了什么 / 为何这么做 / 关键决策)

加法式引入第二货币「虔诚币」+ 神庙修复 + 经验·守护者等级长期主线,完全不动现有 灵力(Soul) 经济与核心循环。逻辑全部叠在既有 `MergeOrderState` 状态机上,新字段同体例入悔棋快照,旧路径零行为变化。

- **数据配置**:新建 `TempleConfig` 静态类(仿 `MergeOrderConfig` 风格,不接 Luban),含 12 厅手编造价数组(大阿尔卡那命名,公式 `500+i*250` 逐项写死)+ 等级曲线(`ExpToNext`/`GuardianLevelFor`,线性递增 `500+(L-1)*300`)+ 虔诚币旋钮(`PietyPerDifficulty=30`/`SpecialPietyMult=2`)+ 发奖常量。虔诚币旋钮放 TempleConfig 与主线同源(设计 §四 #1 推荐)。
- **状态机**:`MergeOrderState` 加 `Piety`/`Exp`/`UnlockedChapter`/`NextRepairIndex`/`bool[] TempleRepaired`/`bool[] TempleDecorated` 字段 + 派生 `GuardianLevel`(经验纯函数,不存等级值)/`IsTempleAllRepaired`;新方法 `AddPiety`(正数才加,仿 `AddSoul`)/`CanRepairTemple`(三前置)/`RepairTemple`(扣币+标记+发奖+跨级升级+清悔棋栈)。新增 `TempleRepairResult` 只读结构供 UI 弹字。
- **发币挂接**:`Deliver` 现有发奖后追加 `AddPiety(o.Difficulty*PietyPerDifficulty)`;`DeliverSpecial` 在盲盒附赠同段追加 `×SpecialPietyMult`。均为纯追加,在 `_undoStack.Clear()` 之前(随交付固化)。
- **快照**:`Snapshot.Capture/Restore` 加 6 个主线字段,两数组用 `.Clone()` 深拷贝(`?.` 空安全)。修复动作成功后 `_undoStack.Clear()`(已提交动作,悔棋不倒回已修厅)。
- **UI**:新建 `TempleWindow`(全屏,glyph/纯色,3×4 厅卡四态:已修/可修/币不足/未解锁 + 修复按钮币不足置灰 + 顶部虔诚币/守护者等级/本级经验进度/解锁章节 + BurstText 修复弹字)。`MergeOrderWindow` 顶部第二信息行加虔诚币计数 + 「神庙」按钮(叠层开 TempleWindow,不丢局),交付/悔棋后刷新虔诚币。TempleWindow 关窗回调刷新 MergeOrderWindow 虔诚币(经 UserData 传 Action)。
- **持久化口径**:`MergeOrderState` 整体不存盘,只入悔棋快照——简报「入持久化」在现状下 = 入 Snapshot,与 `_soul`/`_blindBoxCount` 一致。这是「长期主线」语义与现状的落差(单局尺度),设计稿 §七 O3 已标注,**请 boss 知会用户**(非缺陷,跨会话存盘是独立大改不在本轮)。

### 文件清单

新增:
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/TempleConfig.cs`(+ .meta,Unity 已生成)
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/TempleWindow.cs`(+ .meta)
- `Assets/AssetRaw/UI/Prefabs/TempleWindow.prefab`(+ .meta,新 GUID `6db15220feb043ab99c2207556c617ea`;照抄 MergeOrderWindow.prefab 仅改 m_Name)
- `Assets/Editor/Tests/BlockBlast/TempleRepairTests.cs`(+ .meta,19 例覆盖 T1–T12 + 配置自洽)

修改:
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/MergeOrderState.cs`(新字段/方法/快照/Deliver·DeliverSpecial 发币挂接 + `TempleRepairResult` 结构)
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWindow.cs`(虔诚币显示 + 神庙按钮 + RefreshPiety)

### 验证点(test 逐条核对)

热更程序集:是(GameLogic.HotFix,TempleConfig/MergeOrderState/两窗口均在 HotFix 树)。Luban:无。Play 模式手验点见末。

dev 自跑结果:**EditMode `BlockBlast.Tests` 168/168 全绿**(基线 149 + 新增 19,零回归),编译 0 报错(refresh 后 idle、console 无 CS 诊断、测试能跑起来=程序集编译通过)。test 子会话复跑确认即可。

- **T1 普通订单发币**:`T1_Deliver_GrantsPiety_ByDifficulty_OldRewardsIntact`。逐档 Lv1×1=30/Lv2×1=60/Lv3×1=120/Lv3×2=240;同时断言体力/分数/完成数旧行为不变。
- **T2 特殊订单发币(×倍率)**:`T2_DeliverSpecial_GrantsPiety_WithMult_BlindBoxIntact`。Star Lv3×2 d=8 → 8×30×2=480;盲盒附赠仍生效。
- **T3 不可交付不发币**:`T3_NotDeliverable_NoPiety`。普通+特殊两轨库存不足返 false 且币不变。
- **T4 三前置门控**:`T4_CanRepair_ThreeGates` + `T4_CanRepair_AlreadyRepaired_False`。跳修/越界/币不足/回修已修均 false;失败时状态全不变。
- **T5 扣币+标记+推进**:`T5_Repair_SpendMark_Advance`。修第 0 厅扣 Cost(0)、Repaired/Decorated[0]=true、NextRepairIndex=1;再修第 1 厅推进到 2。
- **T6 发经验+体力**:`T6_Repair_GrantsExp_AndEnergyCapped`。Exp += Cost(i);满血修复体力不溢出 EnergyCap。
- **T7 等级曲线**:`T7_ExpToLevelCurve`。GuardianLevelFor(0)=1、恰好 500→2(>=)、逐档 1300→3/2400→4/3800→5/5500→6/7500→7;GuardianLevel 与配置函数一致。
- **T8 升级(含跨级)**:`T8_Repair_TriggersLevelUp_SingleAndMulti`(单级 + 真跨多级:前 11 厅标记已修后修第 11 厅 Cost3250 使 Exp0→3250 跨 Lv1→Lv4 共 3 级,UnlockedChapter+=3)+ `T8_Repair_MultiLevelJump_ChapterAndEnergyAccumulate`(顺序修 12 厅累计章节=累计跨级数,修完等级=累计造价对应等级)。
- **T9 全厅修完**:`T9_AllRepaired_Marked_NoMoreRepair`。修完 12 厅 NextRepairIndex=12、IsTempleAllRepaired=true,此后 RepairTemple 返 false。
- **T10 累积只增**:`T10_AddPiety_OnlyPositive_NoCapNoOverflowGuard` + `T10_MultiDeliver_PietyAccumulates`。负/0 不增、多次交付累加、大值不截断。
- **T11 悔棋回滚 + 修复清栈**:`T11_Undo_RollsBackMainlineFields`(币/经验/进度回滚)+ `T11_Undo_RollsBackTempleRepairBits`(快照机制对修复位回滚能力,手动改位后 Undo 复原——隔离测,因真实 RepairTemple 会清栈)+ `T11_RepairClearsUndoStack_NoRollbackOfRepairedHall`(修复后 CanUndo=false、已修厅不倒回)。
- **T12 零回归**:`T12_FreshState_MainlineZeroed` + `T12_Deliver_WithoutTouchingMainlineMethods_OldBehaviorStable` + 基线 149 全绿。
- **配置自洽**:`Config_CostMatchesFormula_AndHallCount`(厅数=数组长、Cost(0)=500、逐项=公式默认、越界=0)。

**需进 Play 模式手验的功能点**(逻辑层单测已覆盖,UI 接线层须人验):
1. MergeOrderWindow 顶部虔诚币计数显示 + 交付后实时刷新;「神庙」按钮叠层开 TempleWindow 不丢当前局,关闭返回后虔诚币刷新。
2. TempleWindow 12 厅四态渲染正确(已修绿卡+◈装饰、可修金价、币不足红价+还差 N、未解锁🔒);修复按钮币不足置灰;点修复后弹字 + 卡片态/顶部信息行刷新 + 后一厅转可修/币不足。
3. TempleWindow.prefab 能被 `LoadGameObjectAsync("TempleWindow")` 找到(AssetRaw/UI 按文件名寻址,prefab+meta 已建)。

### 自主拍板(decisions)
见结构化返回 decisions 字段。

### 阻塞项
无方向问题。唯一需 boss 知会(非阻塞):持久化口径为单局尺度(设计稿 §七 O3),与「长期主线」字面有落差,设计已显式标注、本轮按现状只入快照。
