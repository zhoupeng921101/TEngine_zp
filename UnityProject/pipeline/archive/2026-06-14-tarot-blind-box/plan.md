# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:tarot-blind-box(神秘塔罗盲盒核心系统)

设计稿:`design-docs/12-tarot-blind-box.html`(已挂 index + 全库 sidebar 文档树;同步补回了此前漏挂的 11 行)。
本交接区与设计稿配套:设计稿是完整正文(公式/数值/挂接点/风险),本区是 dev/test 直接核对的功能点 + 完成定义清单。冲突以设计稿为准。

### 范围边界(钉死)
- 产物图案上限收敛到 Lv3(工程 `MaxLevel=3`,设计 11 已 5→3 级收敛);不引入 Lv4。
- 不做付费购买入口(去变现红线)。本轮获取仅两条:消除挑战解锁 + 特殊订单附赠。
- 盲盒计数随 MergeOrderState 现状:入悔棋快照,不单独做磁盘存盘(MergeOrderState 整体当前不存盘)。
- 不做:命运之轮包装、集卡、占卜屋、神谕降临双倍、开盒仪式动画。

### 功能点 + 完成定义(test 可逐条核对)

| # | 功能点 | 完成定义(可核对) | 涉及模块/符号(dev 定位) |
|---|--------|------------------|------------------------|
| F1 | 盲盒持有计数 | `MergeOrderState` 新增 `int BlindBoxCount`,`Reset()` 置 0 | `MergeOrderState.cs` |
| F2 | 开盒 API | `CanOpenBlindBox`(Count>0)、`OpenBlindBox(out BlindBoxReward)` 扣 1 + 掷奖 + 发放;Count=0 时返 false 且计数不变 | `MergeOrderState.cs` + 新 `TarotBlindBoxConfig.cs` |
| F3 | 奖池加权抽样 | `TarotBlindBoxConfig.RollReward` 用 `RandomSource` 加权掷恰好 1 项;权重表见设计 §3.1(Low40/Mid22/Energy20/High10/NeededHigh8) | 新 `TarotBlindBoxConfig.cs`(仿 `ChestSystem.RollRewards`) |
| F4 | 双重保底 | (a) 任意 seed 必产有效奖(无空奖);(b) 抽中 NeededHigh 但无订单缺口 → 降级 PatternHigh(Lv3) | `TarotBlindBoxConfig.RollReward` |
| F5 | 产物落点 | 图案走 `AddDirect(type,level,1)`(类型取 `NeededTypes()` 轮转,空回退 Diamond;级联合并是预期);体力走 `RefundEnergy(BoxEnergyGain)`(受 EnergyCap,不溢出) | `MergeOrderState.AddDirect` / `RefundEnergy` |
| F6 | 全清解锁 | `ClearSettlement.Settle` 全清发奖分支 +1 盲盒;连续第 2 次全清(武装位已消)不发 | `ClearSettlement.cs` · `Settle()` |
| F7 | 连消阈值解锁 | `ComboChain == BoxComboThreshold`(默认 4)那一手 +1;同链更长不重复发(用 `==` 不用 `>=`);链断回 1 后再达阈值重新发 | `ClearSettlement.cs` · `Settle()` |
| F8 | 无消除不发 | `Settle(lines=0)` 不增 BlindBoxCount | `ClearSettlement.cs` |
| F9 | 结算结果暴露 | `SettlementResult` 新增 `int BlindBoxGained`(本手获得数)供窗口表现 | `ClearSettlement.cs` · `SettlementResult` |
| F10 | 特殊订单附赠 | `DeliverSpecial()` 成功时按 Kind 附赠:Express=`BoxPerExpress`(1)/Story=`BoxPerStory`(2)/Golden=`BoxPerGolden`(1);须在 `SpecialTrack.OnDelivered()` 之前读 Kind;不可交付返 false 计数不变 | `MergeOrderState.cs` · `DeliverSpecial()` |
| F11 | 悔棋快照回滚 | `Snapshot.Capture/Restore` 加 `_blindBoxCount`;落子触发全清使计数 +1 后 `Undo` 计数回滚(与 `_soul`/`_goddessRating` 同体例) | `MergeOrderState.cs` · `Snapshot` |
| F12 | UI 计数 + 开盒 | `MergeOrderWindow` 顶部信息行显示「🔮 ×N」+ 开盒按钮(Count=0 置灰);开盒/获得后调 `RefreshSynthesis`+`RefreshEnergy`+ 新 `RefreshBlindBox` | `UI/BlockBlastUI/MergeOrderWindow.cs` |
| F13 | UI 结果表现 | 开盒结果用 `BurstText.Spawn` 内联弹字(「开出:◆ Lv3 ×1」);获得盲盒(连消/全清/交付)弹「+1 🔮」 | `MergeOrderWindow.cs` + 现有 `BurstText` |
| F14 | 单测覆盖 | 新建 `Editor/Tests/BlockBlast/TarotBlindBoxTests.cs` 覆盖设计 §六 A1–A10;`SetUp` 用 InMemory Provider + `RandomSource.SetSeed`(仿 `CoreLoopCompletionTests`) | 新测试文件 |
| F15 | 零回归 | core-loop 基线 129 例全绿;无盲盒触发时所有现有结算结果与基线一致 | `BlockBlast.Tests` 全量 |

### 验收测试映射(设计 §六 A1–A10)
A1 抽样确定性 / A2 下限保底 / A3 NeededHigh 降级 / A4 开盒扣计数+发放 / A5 全清解锁(含连续第2次不发) / A6 连消阈值(逐档代入逐行) / A7 无消除不发 / A8 特殊订单附赠各 Kind / A9 悔棋回滚计数 / A10 旧路径零回归。

### 默认拍板的数值(dev 直接用,无须再问)
- 奖池权重 `BoxRewardWeights`:Low=40 / Mid=22 / Energy=20 / High=10 / NeededHigh=8(和=100)。
- `BoxEnergyGain`=10、`BoxComboThreshold`=4、`BoxPerExpress`=1 / `BoxPerStory`=2 / `BoxPerGolden`=1。
- 持有计数本轮不设硬上限;`BoxHoldCap` 旋钮默认 99 备用。

### 待 boss/用户拍板(设计 §七)
- O1 是否支持 Lv4 产物(默认收敛 Lv3)。
- O2 是否设持有软上限(默认不设)。
- O3 MergeOrderState 是否做全量跨会话存盘(默认不做,本轮只入快照)。
- O4 开盒是否要独立仪式感动画(默认内联弹字)。

### 环境约束(test)
dev/test 带 unityMCP 可编译/跑测;MCP 桥不可达时 test 判 BLOCKED 不判 FAIL。
