# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:tarot-blind-box(神秘塔罗盲盒核心系统)

设计基线:`design-docs/12-tarot-blind-box.html`;功能点/验收映射:`pipeline/state/plan.md` F1–F15 / A1–A10。

### 改动摘要(做了什么、为何、关键决策)

按设计稿与 plan 交接区 F1–F15 加法式扩展现有 merge-order 切片,Classic / 无盲盒触发路径逐字节不变,靠原 129 例回归兜底。

- 新建数据层 `TarotBlindBoxConfig`(静态类,不接 Luban,仿 ChestSystem 风格):奖池权重 Low40/Mid22/Energy20/High10/NeededHigh8、`RollReward(MergeOrderState)` 用 `RandomSource` 加权抽样掷恰好 1 项 + NeededHigh 降级保底。
- `MergeOrderState` 加持有计数 `BlindBoxCount`(Reset 置 0)+ `AddBlindBox`/`CanOpenBlindBox`/`OpenBlindBox(out BlindBoxReward)`;`DeliverSpecial` 在 `OnDelivered()` 之前按 Kind 附赠;计数并入 `Snapshot.Capture/Restore`(与 `_soul`/`_goddessRating` 同体例)。
- `ClearSettlement.Settle` 加两路解锁:连消链 `== BoxComboThreshold(4)` 那一手 +1(用 `==` 不用 `>=`);全清发奖分支 +1(复用全清武装位,连续第 2 次不发)。`SettlementResult` 加 `BlindBoxGained` 字段(构造器/无消除早返回/主路径三处同步)。
- `MergeOrderWindow` 加第二信息行「◈ ×N」计数 + 开盒按钮(Count=0 置灰)、`RefreshBlindBox()`、`OnOpenBoxClicked()`;获得盲盒/开盒结果用 `BurstText.Spawn` 内联弹字;所有刷新批次(落子/交付/悔棋/OnCreate)补 `RefreshBlindBox()`。
- 新建 `Editor/Tests/BlockBlast/TarotBlindBoxTests.cs` 覆盖 A1–A10(20 例)。

关键决策:
1. NeededHigh「当前订单缺口」含特殊槽订单(`SpecialTrack.Occupied`)——设计 §3.1 写「当前订单缺口」,特殊单亦属当前订单,故 `TryPickNeededHigh` 同时扫普通激活订单与特殊槽,取尚未满足项中等级最高者。不影响 A3(无缺口降级)语义。
2. UI glyph 用 `◈`(BMP,LegacyRuntime 字体可渲染)代设计 §五的 `🔮`——补充平面 emoji 在该字体渲不出会显缺字框;设计 §五本就写「🔮 或 ◈」,取可渲染者。逻辑层不涉 glyph,纯表现决策。
3. 体力满时开出 Energy 项走 `Math.Min(EnergyCap, ...)` 不溢出(与 RefundEnergy 同规则)。

### 文件清单(新增/修改)

新增:
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/TarotBlindBoxConfig.cs`
- `Assets/Editor/Tests/BlockBlast/TarotBlindBoxTests.cs`

修改:
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/MergeOrderState.cs`(BlindBoxCount 字段 + Reset + AddBlindBox/CanOpenBlindBox/OpenBlindBox + DeliverSpecial 附赠 + BlindBoxPerSpecial + Snapshot 三处)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/ClearSettlement.cs`(SettlementResult.BlindBoxGained + Settle 连消/全清解锁两路 + 三处构造器同步)
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWindow.cs`(盲盒计数/开盒按钮字段 + BuildStaticUI 第二信息行 + RefreshBlindBox + OnOpenBoxClicked + OpenResultLabel + 各刷新批次/弹字接线)

### 验证点(逐条对应验收标准,告诉测试该验什么)

dev 已自跑 EditMode 全量:**149/149 PASS,0 失败 0 跳过**(基线 129 + 新增 20)。建议测试复跑确认。

| 验收 | 怎么验 | 预期 |
|------|--------|------|
| A1 抽样确定性 | `A1_RollReward_DeterministicSequence_AndFrequencyMatchesWeights` | 同种子序列逐项一致;2 万样本各 Kind 频次与权重比 ±25% 内 |
| A2 下限保底 | `A2_AnySeed_AlwaysValidReward_NoEmpty` | 200 seed 必产有效图案或体力,无空奖 |
| A3 NeededHigh 降级 | `A3_NeededHigh_NoGap_DowngradesToPatternHigh` / `A3_NeededHigh_WithGap_GivesHighestGapLevel` | 无缺口不返 NeededHigh(降级 PatternHigh Lv3);有缺口取最高缺口项 |
| A4 开盒扣计数+发放 | `A4_OpenBlindBox_DecrementsCount_AndGrants` / `_ZeroCount_ReturnsFalse_NoChange` / `_EnergyReward_NoOverflowCap` | 开后计数-1、图案进区、体力增不溢出;计数 0 返 false 无变化 |
| A5 全清解锁 | `A5_AllClear_GrantsOneBlindBox` / `_ConsecutiveAllClear_SecondGrantsNone` | 全清 +1 且 BlindBoxGained==1;连续第 2 次 0 |
| A6 连消阈值 | `A6_ComboThreshold_GrantsOnCrossingHand_Only` / `_ComboBreak_ThenReachThreshold_GrantsAgain` / `_NoComboChain_NeverReachesThreshold` | 逐档代入 §3.4 三行:6 连仅 +1、断链再达再 +1(共 +2)、孤立消除永不发 |
| A7 无消除不发 | `A7_NoClear_GrantsNoBlindBox` | Settle(lines=0) 不增 |
| A8 特殊附赠 | `A8_DeliverSpecial_{Express,Story,Golden}_Grants...` / `_NotDeliverable_ReturnsFalse_NoChange` | Express+1/Story+2/Golden+1;不可交付返 false 不变 |
| A9 悔棋回滚 | `A9_Undo_RollsBackBlindBoxCount` | 快照后全清 +1 → Undo → 计数回 c |
| A10 零回归 | `A10_NoTrigger_SettlementUnchanged` / `_FreshState_BlindBoxCountZero` + 基线 129 例 | 未触发不发盲盒、里程碑产出不受影响;Reset 后计数 0;基线全绿 |

### 标注

- **热更程序集**:涉及。改动均在 `GameScripts/HotFix/GameLogic`(全热更)+ `Editor/Tests`(EditMode,不打包)。
- **Luban**:不涉及。盲盒配置硬编码进静态类 `TarotBlindBoxConfig`,不接 Luban,无需重生成。
- **进 Play 模式手验的功能点**(F12/F13 UI,无单测覆盖):打开 `MergeOrderWindow`(合成订单 DEMO)→ 第二信息行显示「◈ ×N」+ 开盒按钮(N=0 时按钮置灰);连消到第 4 连/全清时弹「+N ◈」且计数自增;点开盒弹「开出:◆ Lv3 ×1」或「开出:⚡ +10」、计数 -1、合成区/体力相应刷新。本轮特殊订单 UI 未接(设计 11 遗留项),F10 附赠仅数据层 + 单测覆盖,无 UI 手验路径。

### 环境备注(给测试)

- MCP 桥:本会话连 `UnityProject@02a6dcaa`,自检通过。dev 自跑期间编辑器一度处于 Play Mode(导致首次 run_tests 报「Cannot start... in Play Mode」),已 `manage_editor stop` 退出后复跑通过。测试若遇同问题,先 stop play mode 再跑。
- 控制台有 `MCP-FOR-UNITY: Cannot access a disposed object` 与一条无堆栈 `NullReferenceException`:前者是域重载期桥重连的瞬态;后者发生在编辑器 Play Mode 期间,与本改动无关(盲盒逻辑此时未被触达)。无任何 `CSxxxx` 编译诊断。
- MCP 桥不可达时按硬约束:test 判 BLOCKED 不判 FAIL。
