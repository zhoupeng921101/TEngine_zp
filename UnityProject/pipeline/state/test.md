# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前被测任务:核心玩法补全(demo 范围)

设计基线 = `design-docs/11-core-loop-completion.html`(§十一 验收清单 + §十二 8 条拍板)。git 基线 421a2c12。

### 总判定:BLOCKED(运行门未达成 — Unity MCP 不可达,连续第四轮同一阻塞)

非 PASS、非代码 FAIL。第 4 类(code review + 静态 API 核验 + 单测逐例手推)已全做且全部通过,未发现编译/逻辑/红线缺陷;但第 1-3 类(编译、EditMode、Play 手验)因环境阻塞跑不了,不可代签 PASS。判据见角色记忆「Unity MCP 不可达」条。

本轮(第四轮)实测:`mcpforunity://instances` `instance_count:0`;`read_console` 返回 `no_unity_session`;`refresh_unity(force, compile=request, wait_for_ready=true)` 60s 超时未 ready。进程级诊断:Unity 编辑器(PID 11724,~3.6GB)+ 3 个 `mcp-for-unity` 桥进程全部存活且 `Responding=True`,但编辑器未向桥注册会话(`instances` 空)——「桥握手 / 会话注册链路断」,非进程挂死。**新增观察**:`Unity.ILPP.Runner` 进程在场(IL 后处理器,仅编译/域重载期运行),编辑器疑似卡在内部编译/域重载未完成,故始终到不了 `ready_for_tools`。6 个改动源文件 + 新测试文件经 Glob 复核全部在位。阻塞与本改动无关,dev 侧亦无可修;须由用户在 Unity 端恢复 MCP-for-Unity 桥(确认插件加载、会话注册、编辑器域重载完成)后,由 dev/test 补跑下方清单方可下 PASS。复跑同一未注册实例徒劳(记忆已记),本轮不再重复逐例静态手推(代码自上轮零改动,前轮静态核验仍有效)。

### 四类验证逐项

#### 1. 编译验证 — BLOCKED(无法执行)

- `mcpforunity://instances` 历轮读取均 `instance_count: 0`;`refresh_unity(force, compile=request)` 等 60s 超时未 ready。第四轮进程诊断:编辑器 + 桥进程全活且 `Responding=True`,会话未注册;`Unity.ILPP.Runner` 在场提示编辑器或卡在内部编译/域重载。
- 替代:静态 API 签名核验全部通过(下表),可预判无编译错,但 HybridCLR 热更编译 + 真导入仍须 Editor 确认。

静态核验的外部符号(新代码引用 → 实际定义,逐一 grep 命中一致):

| 引用 | 定义位置 | 结论 |
|------|---------|------|
| `AlgorithmKind.Fill/Diff/ClearAll/AllCombination` | `Algorithms/AlgorithmKind.cs` | ✓ 枚举值齐 |
| `BinaryBoard.RowBinary/RowCount/ColCount/IsEmpty()` | `Core/BinaryBoard.cs` | ✓ |
| `BlockScoring.ClearScore(cells,lines)` | `BlockScoring.cs:14` | ✓ 签名一致 |
| `BlockGameState.SaveArr/ElementArr/OperaArr/Combo/Instance/ResetForMergeOrder/MergeState/Release` | `BlockGameState.cs` / `SimpleSingleton` | ✓ |
| `RandomSource.Index(n)/SetSeed(seed)` | `RandomSource.cs` | ✓ |
| `Persistence.Provider` / `InMemoryPersistenceProvider` | `Persistence.cs` | ✓ |
| `MergeElement.Crown/Star/Diamond/Leaf/Heart/None` | `MergeElementVisual.cs` | ✓ |
| `(int,int)[]` / `System.Array.Empty<(int,int)>()` 元组语法 | 项目既有字典在用同语法 | ✓ |
| 测试 asmdef 引用链 `BlockBlast.Tests → GameLogic` | `BlockBlast.Tests.asmdef`(refs: GameLogic/GameProto/TEngine.Runtime/TestRunner) | ✓ 可达 GameLogic 全部新类型 |

#### 2. 单元测试 — BLOCKED(无法执行)

- `run_tests(EditMode, ["BlockBlast.Tests"])` 跑不了(Editor 不可达)。
- 替代:逐例手推 34 个新用例的断言路径,全部通过(无矛盾、无越界、无 NRE 路径)。重点核:
  - `MultiMilestone_PerTier`:`MultiClearMilestoneBonus(1..9)` 逐档 = §5.2 表(1/2 空、3→{(2,1)}、4→{(2,1),(1,1)}、5→{(2,1),(1,2)}、6+→{(3,1)})。✓
  - `Settle_ComboMultOnlyAffectsDisplayScore`:ClearScore(8,1)=110;display=110×2000/1000=220;k=ElementsForScore(110)=1,用未乘连消的基础分;PendingElements 入 1(NeededTypes 含 Diamond/Star 非空)。隔离铁律可测点成立。✓
  - `Settle_ConsecutiveAllClear`:第 2 次 armed=false 且 boardEmptyAfter=true → 两个 if 分支均不进,不发奖、armed 保持 false。= §5.5 步 6 注。✓
  - `SpecialTrack_*`:已占槽不踢(Request 入队返 false)、PromoteFromWaiting 严格大于取最高优先级 + 同级 FIFO、TickCountdown 到点 OnExpired 升队。= §三。✓
  - `Wish_*` / `HammerCost_IsEight` / `Goddess_TenAllClears` / `Arbiter_*`(P0>P1、回落不接管)/ `Chest_*`(满 4 拒入、倒计时门、三选一去重、领取腾位)/ `Undo_RollsBackSettlementFields`(快照 Capture/Restore 覆盖 ComboChain/AllClearArmed/Soul/WishUsedToday/GoddessRating/GoddessLevel/特殊轨 Occupied+Waiting)。逐例核对 Snapshot 字段与 Restore 一一对应,无遗漏。✓
- 回归(A):原 96 用例零改动(现状 Classic / merge-order off 路径未被本棒触碰),但**必须 Editor 实跑确认** 96 仍绿——静态不能替代回归判定。

#### 3. 手动功能验证 — BLOCKED(无法执行)

- Play 手验需 Editor。窗口接线段(`MergeOrderWindow.PlaceAndResolve`)静态核对通过:
  - 调用顺序 = 扣体力 → HarvestClearedElements → IngestElement → RefundEnergy → `ClearSettlement.Settle`,与 §5.5 流水线 + ClearSettlement 注释契约(窗口先做扣体力/Ingest/Refund)一致。
  - 弹字分支:AllClearRewarded→PERFECT、lines≥3→MultiLabel、ComboChain≥2→COMBO x{n};`_state.Combo` 镜像(≥2 才显示)。逻辑分支无歧义。
  - `PickMilestoneType` 取 NeededTypes[0],空时 None,ClearSettlement 内兜底 Diamond——产物归属链完整。
- 拖拽/手势层未动(记忆:MCP 不可靠模拟指针拖拽),弹字与真实连消/多消/全清的视觉表现仍须人工 Play 确认。

#### 4. Code Review — PASS

对照 CLAUDE.md「核心原则(编码红线)」全 5 条 + naming-rules + 事件 antipattern + conventions 交叉检:

| 红线 | 核查 | 结论 |
|------|------|------|
| 1 异步优先 | 新代码全纯逻辑,无 IO/同步加载/Coroutine | ✓ 不涉及 |
| 2 模块访问 GameModule.XXX | 新代码不访问框架模块(纯状态机) | ✓ 不涉及 |
| 3 资源必须释放 | 无 LoadAssetAsync/GameObject 加载;窗口段 Object.Destroy 落子槽容器(既有路径) | ✓ |
| 4 热更边界 | 新文件全在 `GameScripts/HotFix/GameLogic`,属热更区,正确 | ✓ |
| 5 事件解耦 | 新代码不发 GameEvent/AddUIEvent;无事件泄漏/风暴 | ✓ 不涉及 |

- 命名:新类型/字段符合工程既有风格(PascalCase 公开、_camel 私有);术语统一图案/收集区/合成/灵力/体力,**无食材/生成器/仓库/虔诚币残留**(grep 新文件确认)。= §4.2。
- 设计符合度(§十一 逐条):
  - 问题 1 并发:日常轨 ActiveOrders=2 零改动;特殊轨 0/1 占槽 + 优先级 Story>Express>GoldenHour(枚举值 3>2>1)+ 已占不踢 + 队列升起。✓
  - 问题 2 连消/多消/全清:倍率千分比 {1000,1200,1500,1800,2000} 封顶 ×2.0;只乘 DisplayScore;元素用 baseScore;里程碑逐档对齐;全清不可连续 2 次(武装位)。✓ 隔离铁律落地正确。
  - 问题 3 体力:档位常量对齐(20/30/1/+8);WishForEnergy 每日限 3、纯灵力、封顶不溢出;HammerCost=8。✓(自然恢复维持现状「留口子未实装」,与设计 §七一致。)
  - 问题 5 经济:MaxLevel=3;OrderPool 注释钉死自动配对约束(Lv1/Lv2 数量恒 1,仅 Lv3 可堆积),池内组合合法。✓
  - 问题 6 智能生成:优先级链 P0>P1>P2>P3>P4;P0 与 P1 同时则 P0 优先且 antiStreak 此手不递减;P4 返回 Fallthrough(不接管)= 现状 dynamicWeight 零改变(未触碰 dynamicWeight 本身)。✓
  - 问题 7 宝箱:4 箱位、满拒入、倒计时门、三选一按 Kind 去重、领取腾位;无付费/广告减时入口。✓
  - 问题 8 女神:GoddessRating N/10、满 10 清零升档、GoddessLevel 只升不降;换背景统一归升级(窗口表现层)。✓
- 持久文件交叉检(dev 改过的 `pipeline/state/dev.md` 交接区):lint(指代词/diff 叙事)零命中;正文无可推导事实副本;工作态可识别所属任务、标注「boss 关单清空/归档」。✓

### 给 boss / dev 的补跑清单(解阻塞后必跑,缺一不可下 PASS)

环境恢复(Unity Editor 开启且 MCP-for-Unity 桥加载,`mcpforunity://instances` 非空)后执行:

1. `refresh_unity(force, compile=request, wait_for_ready=true)` → `read_console(types=[error,warning])` 确认编译 0 error。
2. `run_tests(EditMode, ["BlockBlast.Tests"])` → 期望 96 + 34 = 130 全绿;任一红即回退本判定为 FAIL 并贴用例名 + 断言。
3. Play 手验:进 MergeOrderWindow,真实落子核 弹字(Good/.../COMBO x{n}/PERFECT)、连消/多消/全清在拖拽下的表现;截图存 `Assets/Screenshots`。

> 静态核验已挡住编译/逻辑/红线缺陷(逐例手推 + API 签名 grep),但 HybridCLR 热更编译 + YooAsset 模拟寻址的真导入与回归 96 须 Editor 实证,不可代签。

> 本任务工作态;boss 关单事务清空/归档。
