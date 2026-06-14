# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:虔诚币 + 神庙修复 长期主线(design 13)

设计稿:`design-docs/13-piety-temple-repair.html`(已挂 index.html,全库 sidebar 文档树同步;doc 11 为旧 topbar 布局无树、不同步)。
下文为交开发的验收标准。设计细节(公式/常量/逐档代入/挂接点)以设计稿为准,本区只列「dev 改什么 + test 核对什么」。

### 基线(开工前已 grep 核实)
- 现有 EditMode 基线 **149 例**(`Assets/Editor/Tests/BlockBlast/`,`[Test]`+`[TestCase]` 计)。新增逻辑须新增单测覆盖,且 149 例零回归。
- 第二货币加法式引入,**不动 灵力(Soul)** 任何字段/公式/奖励路径。
- 现状关键事实:`MergeOrderState.Deliver` / `DeliverSpecial` 当前只发 体力(`OrderRewardEnergy`)+ `TotalScore`,**不调 `AddSoul`**(订单从不发 灵力)。故挂虔诚币是纯新增,不冲突。
- `MergeOrderState` 整体**不做磁盘持久化**,只入悔棋 `Snapshot`;简报「入持久化」在现状下 = 入快照(与 `_soul`/`_blindBoxCount` 一致)。`Persistence` 类是 `BlockGameState` 磁盘层,MergeOrderState 不经它。

### dev 改动清单(符号经 grep 核实,详见设计稿 §四)
1. 新建 `Module/BlockBlast/TempleConfig.cs`:12 厅造价数组 `Halls(name,cost)` + `Cost(i)` + `HallCount=12`;等级曲线 `ExpToNext(level)` / `GuardianLevelFor(totalExp)`;常量 `TempleBaseCost=500`/`TempleCostStep=250`/`LevelExpBase=500`/`LevelExpStep=300`/`TempleRepairEnergy=30`/`LevelUpEnergy=15`/`PietyPerDifficulty=30`/`SpecialPietyMult=2`。仿 `MergeOrderConfig` 静态类风格,不接 Luban。
2. `MergeOrderState.cs` 新字段:`int Piety` / `int Exp` / `int UnlockedChapter` / `int NextRepairIndex` / `bool[] TempleRepaired` / `bool[] TempleDecorated`;派生 `int GuardianLevel => TempleConfig.GuardianLevelFor(Exp)`;`Reset()` 全部清零/置 false。
3. `MergeOrderState.cs` 新方法:`AddPiety(int)`(正数才加,仿 `AddSoul`)、`CanRepairTemple(int)`(三前置:`index==NextRepairIndex` 且未修且 `Piety>=Cost`)、`RepairTemple(int, out result)`(扣币+标记已修+标记装饰+发奖经验/体力+while 跨级升级判定)、`IsTempleAllRepaired => NextRepairIndex>=HallCount`。
4. `Deliver(int slot)`:现有发奖**之后**、`_undoStack.Clear()` 之前,追加 `AddPiety(o.Difficulty * PietyPerDifficulty)`。
5. `DeliverSpecial()`:同上,追加 `AddPiety(o.Difficulty * PietyPerDifficulty * SpecialPietyMult)`(与现有 `AddBlindBox` 附赠同段)。
6. `Snapshot.Capture`/`Restore`:加 `_piety`/`_exp`/`_unlockedChapter`/`_nextRepairIndex`/`_templeRepaired[]`/`_templeDecorated[]`(数组 `.Clone()` 深拷贝)。**修复动作成功后须 `_undoStack.Clear()`**(修复是已提交动作,悔棋不倒回已修厅)。
7. 新建 `UI/BlockBlastUI/TempleWindow.cs`:`[Window(UILayer.UI, location:"TempleWindow", fullScreen:true)]`,glyph/纯色,12 厅三态卡 + 修复按钮(币不足置灰)+ 顶部虔诚币/经验/守护者等级/解锁章节显示。读 `BlockGameState.Instance.MergeState`。
8. `MergeOrderWindow.cs`:顶部加虔诚币显示 + 「神庙」按钮开 `TempleWindow`(叠层不丢局);交付后刷新虔诚币。
9. (可选) `MainMenuWindow.cs` 加神庙入口;时间紧可省。
10. 新建 `Editor/Tests/BlockBlast/TempleRepairTests.cs`:覆盖下方 T1–T12。SetUp 仿 `TarotBlindBoxTests`(InMemory Provider)。

### 验收标准(test 逐条核对,对应设计稿 §六 T1–T12)
- **T1 普通订单发虔诚币**:`Deliver` 成功 → `Piety += o.Difficulty*PietyPerDifficulty`;现有 体力/`TotalScore` 仍按原值增。逐档:Lv1×1=30、Lv2×1=60、Lv3×1=120、Lv3×2=240。
- **T2 特殊订单发币(×倍率)**:`DeliverSpecial` 成功 → `Piety += o.Difficulty*PietyPerDifficulty*SpecialPietyMult`;现有盲盒附赠仍生效。
- **T3 不可交付不发币**:库存不足 → 返回 false 且 `Piety` 不变。
- **T4 修复三前置门控**:`CanRepairTemple` 仅 `index==NextRepairIndex` 且未修且币足为 true;跳修/回修已修/币不足均 false;`RepairTemple` 不满足时返回 false 且状态全不变(币不扣、不标记)。
- **T5 修复扣币+标记+推进**:币足修第 0 厅 → `Piety -= Cost(0)`、`TempleRepaired[0]==true`、`TempleDecorated[0]==true`、`NextRepairIndex==1`;再修第 1 厅推进到 2。
- **T6 修复发经验+体力**:修第 i 厅 → `Exp += Cost(i)`;`Energy += TempleRepairEnergy` 但不超 `EnergyCap`(满血不溢出)。
- **T7 经验→等级曲线**:`GuardianLevelFor(0)==1`;恰好门槛(500)→ 2 级(`>=`);逐档 1300→3、2400→4…(设计稿 §3.4 表);等级是经验纯函数。
- **T8 修复触发升级(含跨级)**:`Exp` 接近门槛 → 一次修复使 `GuardianLevel` 增 ≥1;经验巨大一次跨多级 → `UnlockedChapter` 增跨级数、升级体力按跨级累加(受软上限)。
- **T9 全厅修完标记**:依次修完 12 厅 → `NextRepairIndex==12`、`IsTempleAllRepaired==true`;此后 `RepairTemple` 返回 false。
- **T10 虔诚币累积+只增**:`AddPiety` 负数/0 不增;多次交付累加正确;无上限(大值不截断)。
- **T11 悔棋快照回滚主线字段**:`CaptureSnapshot` → 落子交付使虔诚币变 → `Undo` → `Piety`/`Exp`/`NextRepairIndex`/修复位全回滚(同 Soul/盲盒快照测试)。另:修复后清悔棋栈,修复后 `Undo` 不倒回已修厅。
- **T12 旧路径零回归**:基线 149 例全绿;不调虔诚币/神庙方法时 `Deliver`/`DeliverSpecial` 现有断言(体力/分数/盲盒)与基线一致;Classic 零影响。

### 涉及模块/事件/配置(给 dev 定位)
- 数据层:`Module/BlockBlast/MergeOrderState.cs`、新建 `TempleConfig.cs`、`Order.Difficulty`(现有)、`MergeOrderConfig.EnergyCap`/`RefundEnergy`(现有,复用)。
- UI:新建 `UI/BlockBlastUI/TempleWindow.cs`、改 `MergeOrderWindow.cs`(顶部信息行 + 入口按钮)、可选 `MainMenuWindow.cs`;复用 `UGuiFactory`/`BurstText`/`MergeElementVisual`。
- 测试:新建 `Editor/Tests/BlockBlast/TempleRepairTests.cs`;asmdef `BlockBlast.Tests`。
- 无 Luban、无 GameEvent、无资源加载新增(纯逻辑 + glyph/纯色 UI)。

### 环境约束
dev/test 子会话带 unityMCP,自行编译/跑测;MCP 桥不可达时 test 判 **BLOCKED**(不判 FAIL),环境恢复后补测。
