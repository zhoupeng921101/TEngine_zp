# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前被测任务:tarot-blind-box(神秘塔罗盲盒核心系统)

验收判据:`design-docs/12-tarot-blind-box.html` §六 A1–A10(最终判据);功能点 `pipeline/state/plan.md` F1–F15。

### 总判定:PASS

四类验证全部通过。编译 0 报错;EditMode 149/149 全绿(基线 129 + 新增 20);Play 手验 F12/F13 UI 计数与开盒路径符合验收;code review 对照 CLAUDE.md 5 条编码红线 + 命名规范无违规。

环境:MCP 桥连 `UnityProject@02a6dcaa`,自检通过(server up、active scene `main` 响应)。

### 1. 编译验证 — PASS

- `refresh_unity(compile=request, force, wait_for_ready)` 触发刷新 + 编译,返回 ready。
- `read_console(error)`:0 条。
- `read_console(warning, filter=TarotBlindBox)`:0 条。
- 无任何 `CSxxxx` 编译诊断。

### 2. 单元测试 — PASS

- `run_tests(EditMode, assembly=BlockBlast.Tests)`:**149 passed / 0 failed / 0 skipped**(job 54ef62f3,0.47s)。= 基线 129 + 新增 20,零回归(A10、F15)。
- 隔离复跑 `TarotBlindBoxTests`(group):**20 passed / 0 failed**(job 822d0633),逐例对应 A1–A10:
  - A1 `A1_RollReward_DeterministicSequence_AndFrequencyMatchesWeights`(同种子序列复现 + 2 万样本频次 ±25%)
  - A2 `A2_AnySeed_AlwaysValidReward_NoEmpty`(200 seed 无空奖)
  - A3 `A3_NeededHigh_NoGap_DowngradesToPatternHigh` / `A3_NeededHigh_WithGap_GivesHighestGapLevel`
  - A4 `A4_OpenBlindBox_DecrementsCount_AndGrants` / `_ZeroCount_ReturnsFalse_NoChange` / `_EnergyReward_NoOverflowCap`
  - A5 `A5_AllClear_GrantsOneBlindBox` / `_ConsecutiveAllClear_SecondGrantsNone`
  - A6 `A6_ComboThreshold_GrantsOnCrossingHand_Only` / `_ComboBreak_ThenReachThreshold_GrantsAgain` / `_NoComboChain_NeverReachesThreshold`(逐档代入设计 §3.4 三行)
  - A7 `A7_NoClear_GrantsNoBlindBox`
  - A8 `A8_DeliverSpecial_{Express,Story,Golden}_Grants...` / `_NotDeliverable_ReturnsFalse_NoChange`
  - A9 `A9_Undo_RollsBackBlindBoxCount`
  - A10 `A10_NoTrigger_SettlementUnchanged` / `_FreshState_BlindBoxCountZero`
- 测试覆盖缺口:无。A1–A10 全有对应用例,新逻辑(奖池抽样/双重保底/解锁阈值/附赠/快照回滚)均被单测覆盖。

### 3. 手动功能验证(Play) — PASS

Play 模式 + 反射经 `_uiStack` 取 MergeOrderWindow 托管实例驱动(MCP 无法可靠模拟拖拽,连消/全清的落子触发路径以单测 A5/A6 覆盖,UI 计数/开盒以下手验)。截图证据:`Assets/Screenshots/tarot-blindbox-window-uicam.png`(经 UICamera 取景;默认 ScreenCapture 因 UI 走 ScreenSpace-Camera 渲染得空白,改用 UICamera)。

| 验证点 | 操作 | 实际表现 | 判定 |
|--------|------|----------|------|
| F12 计数显示 | 打开窗口,读 `BlindBox` 节点 | `◈ ×0`(glyph 用 ◈ 代 🔮,设计 §五允许) | 符合 |
| F12 按钮置灰 | Count=0 时读 OpenBox 按钮 | `interactable=False` | 符合 |
| F12 计数自增刷新 | 注入 BlindBoxCount=3 调 RefreshBlindBox | 显示 `◈ ×3`,按钮 `interactable=True` | 符合 |
| F13 开盒扣计数+发放+弹字 | 点 OnOpenBoxClicked(seed 777) | count 3→2;Energy +10(BoxEnergyGain,该掷为 Energy 项,27<30 不溢出);BurstText 实例 +1(内联弹字) | 符合 |
| F13 连续开盒 | 再点一次 | count 2→1,UI 刷新为 `◈ ×1` | 符合 |
| 顶部信息行无回归 | 同屏读 Energy/Goal | `⚡ 27/30`、`单 0/5`、订单卡/棋盘/合成区均正常渲染 | 符合 |

- Play 期间 `read_console(error)`:4 条,全为基础设施噪声,无游戏逻辑报错——2× `MCP-FOR-UNITY: Cannot access a disposed object`(桥域重载瞬态,dev 已记录);2× `CaptureScreenshot region exceeds...`(本环节首次空白截图尝试所致)。无 NullReferenceException 等盲盒代码报错。
- 未手验项:特殊订单附赠(F10)无 UI 投放路径(设计 §3.5 明示属设计 11 独立未接项,本轮只数据层 + 单测 A8 覆盖);连消/全清的落子触发(需拖拽)以单测 A5/A6 覆盖。两项均按设计与角色记忆既定边界处理,非缺口。

### 4. Code Review — PASS

对照 CLAUDE.md「核心原则(编码红线)」逐条核对改动文件(`TarotBlindBoxConfig.cs` 新建、`MergeOrderState.cs`、`ClearSettlement.cs`、`MergeOrderWindow.cs`、`TarotBlindBoxTests.cs` 新建):

1. 异步优先:盲盒新增均为纯同步 UI/逻辑(BurstText.Spawn/RefreshBlindBox/OnOpenBoxClicked),无 IO/资源加载/Coroutine;窗口导航沿用既有 `ShowUIAsync`。无违规。
2. 模块访问:仅 `GameModule.UI.*`(全为既有导航),无 `ModuleSystem.GetModule<T>()`。无违规。
3. 资源释放:新代码不加载任何 asset,无需配对释放;UI 节点由 UGuiFactory 代码构建。无违规。
4. 热更边界:改动全在 `GameScripts/HotFix/GameLogic`(全热更)+ `Editor/Tests`(EditMode 不打包),无 `GameScripts/Main` 改动。无违规。
5. 事件解耦:数据层不发事件,结果经 `SettlementResult.BlindBoxGained` 返回值暴露(既有体例);UI 按钮用 `onClick.AddListener`(与既有 _undoBtn/_deliver 同款)。无事件泄漏/风暴。无违规。

- 命名:私有字段 `_小驼峰`、公开成员大驼峰、`On` 回调前缀、`BlindBoxRewardKind`/`BlindBoxReward`/`TarotBlindBoxConfig` 类型名均合规。UI 节点名(`BlindBox`/`OpenBox`/`BoxBg`)为运行时代码构建查找名,非 prefab codegen 绑定,前缀规则不适用,与全窗既有体例(`Energy`/`Goal`/`Undo`)一致。`const` 旋钮用 PascalCase 与 sibling `MergeOrderConfig` 既有约定一致。
- 关键逻辑核对(对照设计风险表):连消阈值用 `m.ComboChain == BoxComboThreshold`(`==` 非 `>=`,ClearSettlement.cs:90),防通胀;全清复用武装位(连续第 2 次不发,:108/:118);盲盒计数 `_blindBoxCount` 进 Snapshot.Capture/Restore 对称(MergeOrderState.cs:448/475/506),防悔棋错乱;DeliverSpecial 在 `OnDelivered()` 之前读 Kind 附赠(:299→:301)。四项设计明列风险点均已正确实现并被对应单测验证。
- 静态 API 签名核验:`RandomSource.Range/SetSeed`、`UGuiFactory.CreateButton`(out Image/out Text 顺序)、`SpecialOrderTrack.HasOccupied/Occupied.Req/Kind/SnapshotWaiting/RestoreWaiting`、`SpecialOrderKind.{Express,Story,GoldenHour}` 均与实际签名一致。

- 持久文件交叉检(对 dev 改过的 `pipeline/state/dev.md`):lint 无指代词/diff 叙事/拟人比喻命中;工作态可识别所属任务、内容自包含。合规。

### 给 boss 的备注(非缺陷,知会用途)

- glyph 用 `◈` 代设计 §五的 `🔮`:dev 关键决策 2 已说明(补充平面 emoji 在 LegacyRuntime 字体渲不出,设计 §五本写「🔮 或 ◈」),取可渲染者,纯表现决策,不影响验收。
- 第二信息行(y=170)与订单卡 glyph(y≈203)在截图中视觉略有靠近,计数与按钮均完整可见、功能正常,属轻微布局观察项,非功能缺陷。
