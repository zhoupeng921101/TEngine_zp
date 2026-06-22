# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务

**gameplay-no-rounds-infinite 增量① 无尽核心(client,baton=dev-test,本环节=dev 完成)。**
设计基线 = `design-docs/49-infinite-no-rounds.md`(+ 14 §3.7 / 16)。本刀只动「无尽核心」五件 + 持久化两字段,**整盘续存(增量②)不碰**。

---

## 改动摘要(做了什么 / 为何)

### 1. 删通关(IsDemoComplete / DemoGoalOrders / TriggerWin)
- `MergeOrderConfig.DemoGoalOrders` 常量删除。
- `MergeOrderState.IsDemoComplete()` 方法删除。
- `MergeOrderWindow` 删 `TriggerWin()`(原打开 `MergeOrderWinWindow` 通关结算窗)+ 删 `OnDeliverClicked`/`PlaceAndResolve` 里的通关判定调用。订单交付后照常刷新下一单(`Deliver` 内已 `NextOrder`),无胜利终点。

### 2. 删软/硬 GameOver(TriggerGameOver + 软死亡/硬死亡判定块)
- `MergeOrderWindow` 删 `TriggerGameOver(string)`(原打开 `GameOverWindow`)+ 删 `PlaceAndResolve` 尾部「软死亡(精力耗尽)」「硬死亡(无处可放)」两段判定 + 删 `AnyDeliverable()`(仅软死亡用)。
- 删 `_finished` 字段及全部 `!_finished` 门控(无终点 → 窗口永不 finish,卡死/体力归零都保持可交互)。

### 3. 时基恢复(含离线,纯时间驱动)
- `MergeOrderConfig`:`RegenPerTick=1` / `RegenIntervalSec=120` 由「留口子注释」实装为生效常量。
- `MergeOrderState` 新增 `long LastEnergyRegenTime`(Unix 秒,上次结算时刻)+ `ApplyTimeRegen(long nowUnixSec)` 纯方法:
  - 首次无记录(==0)→ 以 now 初始化、本次不补;
  - 负时差(now < 记录)→ 不倒扣/不抛/不更新记录;
  - 应恢复 = `floor(Δ/interval) × RegenPerTick`,封顶 `EnergyCap` 不溢出;体力本就 > 软上限(订单溢出)则不动;
  - 只推进「整除掉的秒数」,**余秒留到下次累计**(防短间隔反复进入吞零头永不恢复)。
- `MergeMetaPersistence.NowUnixSec()` 新增(生产 now 源,单测注入)。
- `BlockGameState.ResetForMergeOrder` 进入时调 `ApplyTimeRegen(NowUnixSec())`(ImportMeta 之后):有档补离线、无档初始化。
- **不接落子/消除/交付触发**——纯时间驱动(卡死时也恢复,脱困保险,设计 49 §3.3 硬约束 3)。

### 4. 消除道具(清一行一列、代价体力、无限可用只 gate 体力)
- **不走 ItemBag 持有计数**(设计 49 §3.1「不要任何持有道具」):做成体力 gate 的棋盘操作,而非 16 的消耗品道具。
- `MergeOrderConfig.ClearToolCost=25`(≤ EnergyCap=30,硬约束)。
- `MergeOrderState`:`CanUseClearTool`(Energy ≥ cost)+ `SpendClearToolCost()`(扣 cost,失败不扣)。
- `BlockGameState.ClearToolRowCol(board,row,col)`:清指定格所在「一整行 + 一整列」全部已占格,同步 SaveArr / ElementArr / BinaryBoard;返回清掉格数;越界返 0 不动;**不调全清结算 → 不触发全清判定**(B13)。
- `MergeOrderWindow` UI:
  - 「消除道具 ⚡25」按钮(y=245),体力 ≥ cost 可用 / < 置灰(`RefreshClearTool`)。
  - 点按钮 → 进「指定格」arming 模式(亮按钮 + 提示条 + 启用棋盘 overlay);再点按钮或点棋盘外取消(toggle)。
  - 新建 `BlockBoardTapper.cs`(IPointerClickHandler):arming 时铺满设计区的透明 overlay 捕获棋盘格点击 → 映射 (col,row)。
  - 点棋盘格 → `CaptureSnapshot`(可悔棋)→ `SpendClearToolCost` + `ClearToolRowCol` → 退 arming + 刷新 + 落盘。

### 5. 持久化范围(只动两字段:体力 + 上次恢复时刻)
- `MergeMetaSave` 加 `int energy` + `long lastEnergyRegenTime`(**CurrentVersion 不升 = 1**,做法同皮肤态平铺;旧档缺 → JsonUtility 缺省 0)。
- `MergeOrderState.ExportMeta` 写这两字段;`ImportMeta` 读:
  - `lastEnergyRegenTime <= 0`(无记录/旧档)→ Energy 夹回 `EnergyStart`(不信缺省 0)、记录保持 0;
  - `> 0`(真实无尽档)→ 信 energy(夹 ≥0 防篡改负值)+ 记录续存。
- 体力从「不进盘」改为「进盘」(设计 14 §3.7 反转旧「养体力漏洞」论据:离线恢复=脱困保险)。
- `Snapshot`(悔棋)同步纳入 `LastEnergyRegenTime`(随 `_energy` 一并 Capture/Restore)。
- **不做**:棋盘/手牌/合成区/订单整盘续存(= 增量②,本刀不碰)。CompletedOrders/TotalScore 仍不进盘(分层不变)。
- 落盘时机:落子结算后(体力进盘故每次落子标脏)+ 交付/开盒/悔棋/用消除道具后,均 `MarkAndFlushSave`。

### 6. 47/48/26 触发源善后(编译绿 + 安全停用)
- MergeOrderWindow 删 Win/GameOver 终点后,`ActivityIds.AccumulatePlayCount`(累计游戏 N 局,设计 47/48)的 hook 随之删除(原挂在 TriggerWin + TriggerGameOver 两处)。
- 设计 26 结算窗(GameOverWindow / MergeOrderWinWindow)**代码 + prefab 保留不删**:Classic GameWindow 仍用 GameOverWindow(R1/R4 测试仍绿)。仅 MergeOrderWindow 不再引用这两窗。
- 活动基建(RemoteActivityService / ActivityIds / 服务端配置)完整保留、编译绿、未触发。
- **见下「follow-up 交 boss/plan」**:AccumulatePlayCount 活动在无尽后无可达触发源,语义未定,未自由发挥。

---

## 文件清单

**改:**
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/MergeOrderConfig.cs`(删 DemoGoalOrders;Regen 实装;加 ClearToolCost)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/MergeOrderState.cs`(LastEnergyRegenTime + ApplyTimeRegen + CanUseClearTool/SpendClearToolCost;删 IsDemoComplete;Export/Import 两字段;Snapshot 纳入新字段)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/MergeMetaSave.cs`(加 energy + lastEnergyRegenTime)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/MergeMetaPersistence.cs`(加 NowUnixSec)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/BlockGameState.cs`(ClearToolRowCol + ClearToolCellAt;ResetForMergeOrder 调 ApplyTimeRegen)
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWindow.cs`(删 _finished/TriggerWin/TriggerGameOver/软硬死亡/通关/Activity hook;加消除道具按钮+arming+overlay;goal 文案改累计;落盘体力)

**增:**
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/BlockBoardTapper.cs`(棋盘格点击捕获,消除道具指定格用)

**测试改/增:**
- `Assets/Editor/Tests/BlockBlast/MergeOrderTests.cs`(删 IsDemoComplete_AtGoalOrders;加 14 个无尽测:时基恢复 B8/B9/B10 + 余秒累计 + 溢出不动、消除道具 gate B4/B5/B7、清一行一列 B6/B13 + 元素/位掩码同步 + 越界、体力进盘往返 + 离线补算 + 篡改夹值、悔棋回滚体力/记录时刻)
- `Assets/Editor/Tests/BlockBlast/MergeMetaSaveTests.cs`(去 IsDemoComplete/DemoGoalOrders 依赖;A12 体力断言改注「无记录档夹回起始」)
- `Assets/Editor/Tests/BlockBlast/ActivityClientTests.cs`(翻转 3 个 MergeOrderWindow hook 断言 → 断言「不再存在 Win/GameOver/hook」;GameWindow 的 Classic hook 断言保留)
- `Assets/Editor/Tests/BlockBlast/SettlementWindowRegressionTests.cs`(翻转 R2/R3b → 断言 MergeOrderWindow 不再路由 GameOverWindow/MergeOrderWinWindow;R1/R3/R4/V1/C2 保留)

---

## 验证点(给 test,锚设计 49 §六 B1–B11/B13;B12 本刀不核)

**EditMode 单测(已跑全绿 550/550):**
- **B7** cost ≤ EnergyCap → `ClearTool_CostNotExceedEnergyCap`(配置层断言 25 ≤ 30)。
- **B4** 消除道具 gate 体力 + 扣 cost → `ClearTool_GateByEnergy_SpendCost`。
- **B5** 无限可用、只 gate 体力 → `ClearTool_UnlimitedUse_OnlyGatedByEnergy`(体力够 3 次就能用 3 次,无持有计数)。
- **B6** 朝可落前进 → `ClearTool_RowCol_OpensLandingSpot`(满盘卡死 → 清一行一列 → CanPut(1) 为真;清 15 格=8+8-1)。
- **B13** 全清奖不被白嫖 → `ClearTool_RowCol_DoesNotClearWholeBoard`(清后棋盘非空 → 不触发全清判定)。
- **B8** 时基纯时间驱动 + 封顶不溢出 → `TimeRegen_PureTimeDriven_CapsAtSoftLimit`(注入 now,无落子/消除;首次不补;按速率恢复;巨量时间封顶 30)。
- **B9** 离线恢复 = floor(N/interval)×perTick 夹软上限 → `TimeRegen_Offline_FloorTicksClampedToCap` + 余秒累计 `TimeRegen_RemainderSeconds_AccumulateAcrossCalls`。
- **B10** 负时差兜底 → `TimeRegen_NegativeDelta_NoCreditNoThrow`(不倒扣/不抛/不更新记录)。
- 体力进盘往返 + 离线补算 → `EnergyPersist_RealSave_RestoresAndAppliesOfflineRegen`;无记录档夹起始 → `EnergyPersist_NoRecord_ClampsToStartEnergy`;篡改负值夹 0 → `EnergyPersist_NegativeTampered_ClampsToZero`;悔棋回滚 → `Undo_RestoresEnergyAndRegenTime`。
- 时基不动溢出体力 → `TimeRegen_AboveSoftLimit_LeftUntouched`。

**源码文本核(已绿):**
- **B1** 无通关终点 → `ActivityClientTests.Source_MergeOrderWindow_NoWinOrGameOverTrigger`(MergeOrderWindow 无 TriggerWin)+ `SettlementWindowRegressionTests.R3b_MergeOrderWindow_NoWinWindowRoute`。
- **B2/B3** 无硬/软 GameOver → 同上 `NoWinOrGameOverTrigger`(无 TriggerGameOver)+ `R2_MergeOrderWindow_NoGameOverWindowRoute`(不路由 GameOverWindow)。

**需进 Play 模式手验(MCP 不支持拖拽 + 点击 overlay,逻辑层已单测覆盖,表现/交互层列手验):**
- **B2/B3 行为级**:构造卡死 / 体力归零,确认玩法窗保持可交互、不弹任何面板。
- **B4 表现**:体力 < 25 时消除道具按钮置灰;≥ 25 可点 → arming 高亮 + 提示条。
- **B6/B11 闭环**:卡死棋盘 → 点消除道具 → 点棋盘任一格 → 清一行一列、棋盘重新可落、体力 -25,全程无 GameOver 面板。
- **指定格交互**:arming 时点棋盘外(含按钮区,y<300 → row 越界)取消 arming 不扣体力;再点按钮 toggle 取消。
- **离线恢复真机**:存档体力低 → 改系统时间 / 等待 → 重进玩法窗,体力按时差补回(封顶 30)。
- **布局**:消除道具按钮(x150,y245)与提示条(x375,y245)x 向有小重叠(195–270),确认不挡操作;按钮位于棋盘上方留白(board y≥300),不压棋盘。

**异常路径自检(已过,写明供 test 复核):**
- 空/无记录档:energy 缺省 0 + lastEnergyRegenTime=0 → ImportMeta 夹回 EnergyStart(测 `EnergyPersist_NoRecord`)。
- 负时差:ApplyTimeRegen(now<记录)不倒扣不抛(测 B10)。
- 篡改负体力:ImportMeta 夹 0(测 `EnergyPersist_NegativeTampered`)。
- 满盘卡死 + 越界点击:ClearToolRowCol 越界返 0 不动(测 `OutOfBounds_NoOp`);arming 越界点击取消不扣体力(代码 OnBoardTapForClearTool 已防)。
- arming 期间体力被耗低:OnBoardTapForClearTool 二次 gate,不够则取消 + 提示(代码已防,无单测——表现层,列手验)。
- 旧档含已删字段(completedOrders/totalScore)JSON:`MergeMetaSaveTests.LegacyJson_WithRemovedFields_LoadsWithoutError` 仍绿。

---

## follow-up 交 boss/plan(本刀不自由发挥,单列)

1. **「累计游戏 N 局」活动(AccumulatePlayCount=5,设计 47/48)无可达触发源**:无尽模型删「局」概念后,该活动唯一玩法内触发(MergeOrderWindow 的 Win/GameOver 终点)已删;仅剩 Classic GameWindow:507 的触发,而 Classic 入口已下线(设计 29 §4.2)。活动基建/服务端配置完整保留、编译绿,但实际不再被触发。**语义重定交 boss/plan**:无尽下「玩了一局」对应什么(如「每完成 N 单 +1」/「每用一次消除道具」/直接废弃该活动)——本刀按指示未自由发挥新语义。

2. **CompletedOrders/TotalScore 定位**:设计 49 §五 O4 + 14 §3.7 表把「历史完成单数/交付分」列为生涯累计可进盘(增强项);本刀按 boss 范围「只动体力 + 上次恢复时刻」未实装其进盘,仍每会话归 0。增量② 整盘续存时一并定。

3. **GameOverWindow / MergeOrderWinWindow「再来一局/重试 → MergeOrderWindow」回调**(设计 29 R4):这两窗现仅 Classic GameWindow 可达;其「重试回 MergeOrderWindow」在无尽下语义模糊(无尽无「重试」)。代码保留未动(R4 测试仍绿),如需清理交后续。

---

## 自检结论
- 编译 0 error(read_console 仅 1 条域重载瞬态 `disposed object`,非 CSxxxx);EditMode 全量 **550/550 PASS**(含本刀 14 新增 + 5 翻转用例)。
- 不做 git 提交(boss 关单时 checkpoint;`Fantasy/` 未跟踪来源不明,勿 `git add -A`)。
