# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务(client,baton=dev-test,本环节=dev)

Bug 修复:`MergeOrderWindow` 第二次及以后进入游戏,放下第一块即弹「通关结束」结算窗。

### 改动摘要

根因复核确认与简报一致:`CompletedOrders` / `TotalScore` 被一物两用——既是 `Reset()` 每局清零的单局瞬态(通关判定 `IsDemoComplete()`、HUD/结算显示、`Deliver` 局内 `+=`),又被 `ExportMeta`/`ImportMeta` 当元层字段进盘。第二局执行链 `ResetForMergeOrder` = `MergeState.Reset()`(置 0)→ `ImportMeta(磁盘档)`(把上一局达标值盖回)。本机历史累计满 5(第一局通关)后,存档 `completedOrders≥5`,新局开局即 `IsDemoComplete()==true` → 放第一块 `PlaceAndResolve` 判通关 → 秒结算。

修复:`CompletedOrders` 与 `TotalScore` 回归纯单局瞬态,从存档(DTO + Export/Import)移除。进盘项 13 → 11。依据设计 29 L139「融合后取定」(单局态含分数,元层不含完成单数)+ 设计 14 O2 降级条款。

保留不动(简报确认):`Reset()` 的清零、`Deliver`/`DeliverSpecial` 的 `+=`、`Snapshot.Capture`/`Restore` 的 `_completed`/`_totalScore`(悔棋是局内回滚)、窗口对局内 `TotalScore`/`CompletedOrders` 的读取(改后自动变本局值,符合预期)。

### 文件清单

代码(HotFix/GameLogic):
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/MergeMetaSave.cs` — 删 `completedOrders`/`totalScore` 字段;注释「进盘 13 项」改 11 项 + 加旁注说明二者为何不进盘。
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/MergeOrderState.cs` — `ExportMeta` 删两行赋值;`ImportMeta` 删两行覆盖;两处 doc 注释「进盘 13 项」→「11 项」。

设计稿:
- `design-docs/14-save-system.md` — §3.1 进盘表(两行从「是」移到「不进盘·局内瞬态」)、§3.2 字段表(删两行)、O2 旁注与待拍板表 O2(改为「已拍板:不进盘」现状陈述,保留设计理由)、A1 验收点(13 项→11 项)、§3.1 表上方小标题措辞同步。
- `design-docs/29-gameplay-fusion.md` — L138 存档行(删「完成单数」、13 项→11 项、局内瞬态侧补「分数 / 完成单数」)、§5.4「现状」描述删「完成单数等」。

测试(Assets/Editor/Tests/BlockBlast):
- `MergeMetaSaveTests.cs` — `NonDefaultState` 删两字段赋值;A1 删两条断言、注释 13→11;新增 `CompletedOrdersAndTotalScore_DoNotCarryAcrossSessions`(核心回归:达标态 Export→序列化往返→新局 Reset+Import→断言 CompletedOrders==0/TotalScore==0/IsDemoComplete()==false/真元层字段保留)与 `LegacyJson_WithRemovedFields_LoadsWithoutError`(旧档兼容:JSON 含已删字段,加载不抛、被忽略)。
- `PlayerInfoTests.cs` — `S2_OldSaveMissingPlayerFieldsFallback` 原用 `completedOrders=4` 作非默认元字段验旧档兜底,字段已删会编译失败;换为 `unlockedChapter=4`(等效:仍是非默认元字段),测试意图不变。**(此处简报未列,是字段移除连带的隐藏编译破坏,已修)**
- `MergeOrderTests.cs` — 顺手清 L366 含 diff 叙事的注释(「新行为…不再 FIFO 抽干」→脱离对话成立的陈述)。悔棋快照 TotalScore 断言(约 L588/605)未动。

### 关键决策

- 旧存档兼容:`MergeMetaPersistence.CurrentVersion` 不升(=1)。JsonUtility `FromJson` 对 JSON 里多余的 `completedOrders`/`totalScore` 键静默忽略;`Migrate` 对 version==CurrentVersion 直接放行;`ImportMeta` 不再读这两键。故旧档加载无异常、新局值归 0。已建专门回归用例 `LegacyJson_WithRemovedFields_LoadsWithoutError` 钉死。
- 设计稿 O2/O3 上方小标题原写「列入待拍板 §七」,O2/O3 现均已拍板,改为「决策汇总见 §七」,避免标题与表内「已拍板」矛盾(conventions 规则6,无孤儿旁注)。

### 自检结论

- 静态编译核验:全仓 grep 确认无任何代码再引用已删的 DTO 成员(`.completedOrders`/`.totalScore`/对其赋值)。剩余 `CompletedOrders`/`TotalScore` 出现点全部是有意保留(字段声明、Reset、Deliver `+=`、Snapshot、IsDemoComplete、窗口显示)。新增/改动测试只用既有可见符号(`MergeOrderConfig.DemoGoalOrders` 等公共 const、`FreshState`/`Serialize`/`Deserialize`/`ImportMeta`),compile-safe。
- **编辑器内编译 + 单测未由 dev 实跑**:本环境无 UnityMCP 工具暴露(unity-check 三步探针的工具均不可用),且本机有多个 Unity.exe 实例打开、工程目录被锁,batchmode 跑测会与开着的编辑器冲突且有未保存态风险,故不强行启动。**此项留给 test 在其 MCP 环境执行**(见下「给 test 的验证点」)。

### 给 test 的可复现验证点

1. **编译 0 报错**:`read_console` 确认 `CSxxxx` 为 0、`editor_state.isCompiling=false`(BlockBlast.Tests EditMode 能跑起来即最强编译自检)。
2. **BlockBlast 单测全绿**(`run_tests` EditMode,需先确保非 Play 模式)。重点核:
   - 新增 `MergeMetaSaveTests.CompletedOrdersAndTotalScore_DoNotCarryAcrossSessions` 通过 = bug 已钉死(等价复现:两局连玩不串味)。
   - 新增 `MergeMetaSaveTests.LegacyJson_WithRemovedFields_LoadsWithoutError` 通过 = 旧档兼容。
   - `MergeMetaSaveTests.A1_RoundTrip_AllMetaFields_Equal_SameDay` 仍绿(现覆盖 11 项)。
   - `PlayerInfoTests.S2_OldSaveMissingPlayerFieldsFallback` 仍绿(字段换 unlockedChapter)。
   - `MergeOrderTests` 悔棋回滚相关 TotalScore 用例仍绿(局内快照行为不变)。
3. **设计一致性交叉检**(conventions 收尾必做):设计 14、设计 29 无「13 项」残留、O2 无「待拍板/默认进盘」旧文、无孤儿旁注。
4. **遗留·Play 模式手验(交 test 判)**:拖拽落子的表现层「第二次进入放第一块不秒结算」属运行期手验。逻辑层已由两局连玩单测等价覆盖;若 test 环境不支持 Play 拖拽,标为遗留。
5. **过异常路径**:旧档缺字段(JsonUtility 给缺省 0)、ImportMeta(null)、跨天重置——均有既有/新增单测覆盖,未发现新崩点。
