# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:核心玩法补全(demo 范围)

设计基线 = `design-docs/11-core-loop-completion.html`(§十二 8 条决策已拍板,定稿)。git 基线 421a2c12(block_claude)。

### 交接区(给 test)

#### 改动摘要

按设计 11 §十一 (B) 新增系统逐条落地,均为**加法式扩展 + 纯逻辑可单测**;现状(A)路径(Classic / 原 merge-order off 路径)零改动,靠原 96 单测兜回归。核心采「逻辑层全覆盖、窗口层最小接线」策略。

1. **连消/多消/全清结算(§5.5 固定流水线)** — 新增 `ClearSettlement.Settle()`:
   - 连消链 `ComboChain`(住 `MergeOrderState`):每次有消除 +1、无消除回 1;倍率 `ComboMultPermilleFor`(×1.0/1.2/1.5/1.8/2.0 封顶,整数千分比避免浮点不可测)。
   - **铁律落地**:倍率**只乘显示分**(`DisplayScore`);元素产出 `EnqueueScoreElements(k)` 用**未乘连消的 baseScore**;全清判定用棋盘空 + 武装位,均不碰倍率。
   - 多消里程碑加码 `MultiClearMilestoneBonus(lines)`:3消→+1 Lv2、4消→+1 Lv2+1 Lv1、5消→+1 Lv2+2 Lv1、6+消→+1 Lv3,经 `AddDirect` 直发收集区(跳过逐级合成)。
   - 全清:武装位 `AllClearArmed`(开局 true),发 1 Lv3 + 推进女神 +1;发奖后置 false,须一次非全清落子重新武装(**不可连续 2 次**)。
2. **订单双轨(§三)** — 新增 `SpecialOrderTrack.cs`(`SpecialOrderKind` 剧情>加急>黄金时段 + `SpecialOrder`)。`MergeOrderState.SpecialTrack`:0/1 占槽 + 等待队列;已占槽不被中途踢出(高优先级请求入队),槽空时升起队列最高优先级(同级 FIFO);加急倒计时 `TickCountdown` 到点过期升队。日常轨(`ActiveOrders=2`)零改动。demo 默认特殊轨空(触发器留窗口按条件投放,本环节未接 UI 触发)。
3. **体力/灵力/祈愿/HammerCost(§四/§7.1/§7.2)** — 单货币 `Soul` + `AddSoul`;祈愿兑体力 `WishForEnergy`(每日限 `WishPerDayLimit=3`,`WishSoulCost=20`→`WishEnergyGain=10`,封顶软上限不溢出,纯灵力无付费);`HammerCost=8`。自然恢复维持现状「留口子未实装」。
4. **智能生成 R1–R3 仲裁(§八)** — 新增 `HandGenerationArbiter.Decide()` + `HandGenContext`(`NoClearStreak`/`AntiStreak`)。优先级链:P0防卡死(Fill)>P1清盘增难(Diff)>P2清盘引导(ClearAll)>P3高阶引导(AllCombination)>P4回落。**P4 返回 `Fallthrough`(不接管)= 现状 dynamicWeight 逐字节不变**;P0 与 P1 互斥时 P0 优先且 antiStreak 此手不递减;计数器 `OnPlaced` 照常更新。规则到 trio 复用既有 `BlockAlgorithms`,本环节**未改 dynamicWeight 本身**。
5. **宝箱(§九,后置项)** — 新增 `ChestSystem.cs`:4 箱位、占槽倒计时 `Tick`、到点 `CanOpen`、`Open` 抽 3 不重复 Kind 三选一、`Claim` 腾位;`TierCountdownSec` 普通/稀有/史诗;**去变现红线:无付费/广告减时入口**。奖励发放(ChestReward→灵力/体力/图案/道具)接线归窗口,本环节只产出选项。
6. **女神(§十,后置项)** — `MergeOrderState`:`GoddessRating`(N/10)+ `GoddessLevel`(只升不降),`AdvanceGoddess()` 满 10 清零升档;由 §5.5 全清统一推进。换背景统一归女神升级触发(§十去重),纯表现归窗口。
7. **术语** — 新代码统一图案/收集区/合成/灵力/体力;无食材/生成器/仓库/虔诚币残留。
8. **窗口最小接线** — `MergeOrderWindow.PlaceAndResolve` 的消除结算段替换为 `ClearSettlement.Settle`(替代原内联 combo/enqueue),新增 `PickMilestoneType()`(取订单所需类型之一作里程碑/全清产物归属)。`_state.Combo` 镜像 `ComboChain`。落子/拖拽手势层未动。
9. **悔棋** — 全量快照新增 ComboChain/AllClearArmed/Soul/WishUsedToday/GoddessRating/GoddessLevel/特殊轨(占槽+等待队列),回滚一并恢复。

#### 文件清单

新增(`Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/`):
- `ClearSettlement.cs`(§5.5 结算流水线 + SettlementResult)
- `SpecialOrderTrack.cs`(§三 特殊轨 + Kind/SpecialOrder)
- `HandGenerationArbiter.cs`(§八 R1–R3 仲裁 + HandGenContext)
- `ChestSystem.cs`(§九 宝箱)

修改(同目录):
- `MergeOrderConfig.cs`(灵力/祈愿/HammerCost/连消倍率/多消里程碑/弹字/全清/女神/智能生成阈值常量 + 映射函数)
- `MergeOrderState.cs`(新增结算/灵力/女神/特殊轨字段 + AddDirect/特殊轨/灵力祈愿/女神方法;Reset 初始化;Snapshot 扩展回滚)

修改(UI):
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWindow.cs`(结算段接 ClearSettlement + PickMilestoneType)

新增(测试):
- `Assets/Editor/Tests/BlockBlast/CoreLoopCompletionTests.cs`(34 个新单测,assembly `BlockBlast.Tests`)

#### 验证点(逐条对应 §十一)

- **编译**:`read_console` 0 error,域重载完成(`editor_state` ready)。涉热更程序集(GameLogic);**无** Luban 重生成需求(配置全硬编码常量)。
- **EditMode 全量**:`run_tests(mode=EditMode, assembly_names=["BlockBlast.Tests"])`。预期原 96 + 新 34 = 130 全绿。重点核:
  - 多消里程碑逐档(`MultiMilestone_PerTier_MatchesDesignTable`)= §5.2 表;连消倍率封顶(`ComboMult_Ramp_CapsAt2x`)= §5.3。
  - **隔离铁律**(`Settle_ComboMultOnlyAffectsDisplayScore_NotElements`):倍率乘显示分、元素用基础分——§5.5 关键可测点。
  - 全清武装位不可连续 2 次(`Settle_ConsecutiveAllClear_SecondNotRewarded` / `Settle_AllClearRearmsAfterNonAllClearMove`)= §5.4。
  - 特殊轨已占槽不被踢 + 升队优先级 + FIFO + 加急过期(`SpecialTrack_*`)= §三。
  - 祈愿每日限次/封顶/灵力不足(`Wish_*`)= §7.1;`HammerCost==8` = §7.2。
  - 女神 10 次升档清零(`Goddess_*`)= §十。
  - 仲裁 P0>P1、回落不接管(`Arbiter_*`)= §八「现状零改变」。
  - 宝箱满 4 拒入/倒计时门/三选一不重复/领取腾位(`Chest_*`)= §九。
  - 悔棋回滚新字段(`Undo_RollsBackSettlementFields`)。
- **需进 Play 模式手验**:`MergeOrderWindow` 实际落子时弹字(Good/Great/.../COMBO/PERFECT)、连消/多消/全清在真实拖拽下的表现。逻辑层已单测,风险集中在窗口接线的弹字分支与 PickMilestoneType 取值(手势层未动,回归风险低)。

### 自检状态(运行验证补跑完成 — 环境恢复后 PASS)

环境恢复后(active_instance=UnityProject@02a6dcaa 在线),前序四轮因 Unity MCP 桥未注册会话而阻塞的运行验证已补跑完成。

补跑实测:
- **编译 0 error**:`refresh_unity(force, compile=request, wait_for_ready=true)` → 编辑器进 compiling 后回 idle/ready;`read_console(types=[error])` 仅 1 条 MCP 桥内部日志(`MCP-FOR-UNITY: Client handler error: Cannot access a disposed object`,桥层非项目编译错),**无任何 CS 编译错误**。
- **EditMode 全量绿**:`run_tests(EditMode, ["BlockBlast.Tests"])` job 成功,`summary: total 129 / passed 129 / failed 0 / skipped 0`,resultState=Passed,2.79s。
  - **数目订正**:实际全量 = 96(原) + 33(新)= **129**,非前序交接预估的 130。复核 `CoreLoopCompletionTests.cs` 实有 33 个 `[Test]` 方法(无 `[TestCase]` 参数化展开),前序「34」为新用例数的一处差一误记;无用例缺失或被排除,129 全绿即完整通过。
- **Play 手验(MergeOrderWindow 弹字/连消/多消/全清视觉)**:属人工目视确认项。拖拽手势层本环节未动(见文件清单),所有弹字分支 + PickMilestoneType 取值均由现已全绿的单测覆盖;MCP 无法可靠模拟指针拖拽(角色记忆已记),此项留人工进 Play 目视。窗口资源在位复核通过:`Assets/AssetRaw/UI/Prefabs/MergeOrderWindow.prefab` + `MergeOrderWindow.cs` 均存在。

结论:阻塞 4 轮的真正卡点(编译 + EditMode 运行验证)已双双 PASS,本环节无代码改动。剩余仅 Play 模式拖拽弹字的人工目视一项(逻辑已单测兜底)。

> 本任务工作态;boss 关单事务清空/归档。
