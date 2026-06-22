# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前任务

**gameplay-no-rounds-infinite 增量① 无尽核心(client,baton=dev-test,本环节=test 完成)。**

---

## 总判定

**PASS**

---

## 四类验证结果

### 1. 编译验证 — PASS

- `refresh_unity` 触发强制刷新 + compile=request,等待 `ready_for_tools`。
- `read_console` 过滤 Error:仅 1 条 `MCP-FOR-UNITY: Client handler error: Cannot access a disposed object`(域重载桥瞬态,非 CS 编译错误,与 dev 自检记录一致)。
- Warning:0 条。
- **编译 0 error。**

### 2. EditMode 单测 — PASS

实跑结果(job_id `6aed3c91212149bfa91bee6206c50f3f`):

| 指标 | 值 |
|---|---|
| 总用例 | 550 |
| 通过 | 550 |
| 失败 | 0 |
| 跳过 | 0 |
| 耗时 | 13.3 秒 |

**关键用例核验(dev 交接区列出的 B 组单测全部包含在 550 中):**

| 验收点 | 用例名 | 结果 |
|---|---|---|
| B7 cost≤EnergyCap | `ClearTool_CostNotExceedEnergyCap` | PASS |
| B4 gate+扣 cost | `ClearTool_GateByEnergy_SpendCost` | PASS |
| B5 无限可用 | `ClearTool_UnlimitedUse_OnlyGatedByEnergy` | PASS |
| B6 朝可落前进 | `ClearTool_RowCol_OpensLandingSpot` | PASS |
| B13 全清奖不被白嫖 | `ClearTool_RowCol_DoesNotClearWholeBoard` | PASS |
| B8 纯时间驱动+封顶 | `TimeRegen_PureTimeDriven_CapsAtSoftLimit` | PASS |
| B9 离线恢复 | `TimeRegen_Offline_FloorTicksClampedToCap` | PASS |
| B9 余秒累计 | `TimeRegen_RemainderSeconds_AccumulateAcrossCalls` | PASS |
| B10 负时差兜底 | `TimeRegen_NegativeDelta_NoCreditNoThrow` | PASS |
| 溢出不动 | `TimeRegen_AboveSoftLimit_LeftUntouched` | PASS |
| 体力进盘往返+离线补算 | `EnergyPersist_RealSave_RestoresAndAppliesOfflineRegen` | PASS |
| 无记录档夹起始 | `EnergyPersist_NoRecord_ClampsToStartEnergy` | PASS |
| 篡改负值夹 0 | `EnergyPersist_NegativeTampered_ClampsToZero` | PASS |
| 悔棋回滚体力+记录时刻 | `Undo_RestoresEnergyAndRegenTime` | PASS |
| B1 无通关终点(静态核) | `Source_MergeOrderWindow_NoWinOrGameOverTrigger` | PASS |
| B2/B3 无 Win/GameOver 路由 | `R2_MergeOrderWindow_NoGameOverWindowRoute` + `R3b_MergeOrderWindow_NoWinWindowRoute` | PASS |
| Activity hook 翻转 | `Source_MergeOrderWindow_NoAccumulatePlayCountHook` + `Source_MergeOrderWindow_NoActivityHookAtAll` | PASS |
| GameWindow R1/R4 保留 | `R1_GameOverWindow_UserDataParsing_Unchanged` + `R4_*` | PASS |

**测试覆盖说明:**
- `#if FANTASY_UNITY`-gated 代码(如 `RemoteActivityIncrementSource` 的 Fantasy 协议字段映射)无法 EditMode 直测,属设计边界非覆盖缺口,逻辑层已完整覆盖。
- B11(脱困闭环全链路)和 B2/B3 行为级(无 GameOver 面板)属 Play 手验范围,见下节。

### 3. Play 手验 — BLOCKED(环境限制,非代码缺陷)

**阻塞原因:MCP 不支持拖拽操作 + arming 模式的棋盘 overlay 点击。**

下列验证点需人工 Play 模式手验:

| 验收点 | 描述 | 阻塞理由 |
|---|---|---|
| B2/B3 行为级 | 构造卡死/体力归零 → 确认玩法窗保持可交互、不弹任何面板 | 需拖拽落子构造棋盘状态 |
| B4 表现 | 体力<25 按钮置灰 / ≥25 可点 → arming 高亮+提示条 | 需点击 overlay arming |
| B6/B11 闭环 | 卡死棋盘 → 消除道具 → 点棋盘格 → 清行列 → 可落 | 需拖拽+overlay点击 |
| 指定格交互 | arming 时点棋盘外取消 arming 不扣体力 | 需 overlay 点击 |
| 离线恢复 | 改系统时间/等待 → 重进 → 体力按时差补回 | 需真实时间流逝+手动冒烟 |
| 按钮布局 | 消除道具按钮(x150,y245)与提示条无遮挡操作 | 视觉验证 |

以上逻辑层均由 EditMode 单测覆盖（B4/B5/B6/B7/B8/B9/B10/B13);Play 手验仅补表现/交互层。判 BLOCKED 不升 FAIL,代码未见缺陷。

**越界试探(静态核+单测已覆盖的部分):**
- 越界点击(`row<0`/`col>=8`):单测 `ClearTool_RowCol_OutOfBounds_NoOp` — 覆盖 ✅
- arming 期间体力被耗低:代码二次 gate 已防(`OnBoardTapForClearTool` 再检 `CanUseClearTool`) — 静态核 ✅
- 首次无存档/DTO=null:`ImportMeta(null)` 直接 return — 静态核 ✅
- 旧档含已删字段:`MergeMetaSaveTests.LegacyJson_WithRemovedFields_LoadsWithoutError` — 单测 ✅
- 负时差:单测 B10 ✅；篡改负体力:单测 `EnergyPersist_NegativeTampered` ✅

### 4. Code Review — PASS

**设计49 硬约束逐条核:**

| 约束 | 代码证据 | 判定 |
|---|---|---|
| `ClearToolCost≤EnergyCap`(25≤30) | `MergeOrderConfig.cs` L52 `ClearToolCost=25`, L33 `EnergyCap=30`;单测直接断言 | PASS |
| 消除道具无限可用、只 gate 体力 | `MergeOrderState.CanUseClearTool`/`SpendClearToolCost` 无 ItemBag、无持有计数;`MergeOrderWindow` 无持有字段 | PASS |
| 时基恢复无条件纯时间驱动含离线 | `ApplyTimeRegen` 注入 now 参数;`PlaceAndResolve`/`OnDeliverClicked` 无 `ApplyTimeRegen` 调用;仅在 `ResetForMergeOrder` 进窗时调一次 | PASS |
| B13 全清奖不被白嫖 | `ClearToolRowCol` 不调 `ClearSettlement`;注释明确标注;单测 `DoesNotClearWholeBoard` | PASS |

**删通关/软硬 GameOver 无悬空崩点:**
- `MergeOrderWindow` 无 `TriggerWin`/`TriggerGameOver`/`_finished` 字段/`AnyDeliverable`。
- `PlaceAndResolve` 末尾无结束判定,comment 明确说明无 GameOver。
- 47/48/26 窗口代码保留,编译绿,Classic GameWindow 的 `GameOverWindow` 路由未受影响(R1/R4 单测仍绿)。

**TEngine 核心红线:**

| 红线 | 核查 | 判定 |
|---|---|---|
| 异步 IO 用 UniTask | `SaveAsync(dto).Forget()`;`Load()` 同步读 PlayerPrefs(内存级非阻塞,与 BlockGameState.Load 同口径) | PASS |
| 模块访问 `GameModule.XXX` | `GameModule.UI.ShowUIAsync/CloseUI` | PASS |
| 资源释放 | 无裸 `LoadAssetAsync<Sprite>`,全走 `SetSprite`/`SetSubSprite` | PASS |
| 热更边界 | 所有改动在 `GameScripts/HotFix/` | PASS |
| 事件解耦 | `onClick.AddListener`;无跨模块 GameEvent 滥用 | PASS |

**命名/节点/事件泄漏:**
- `BlockBoardTapper` 是运行时 AddComponent 无 prefab 节点命名问题。
- `OnAppPause` 在 `OnDestroy` 解注订阅,无泄漏。

**conventions 交叉检(dev 动过的持久文件):**
- 代码注释:无 diff 叙事,无过时「通关/GameOver」残留,脱离对话成立。
- `pipeline/state/dev.md`:工作状态含任务标识,格式规范。
- 所有规范层内容(config 注释/doc-comment)与无尽模型描述一致。

---

## 遗留/观察项(非 FAIL)

1. **`MergeOrderTests.cs:366` 注释含 diff 叙事**(`// 不再 FIFO 抽干`):boss.md 已记录"dev 下次顺手清",非本刀新引入。

2. **`ActivityClientTests.cs:228-231` 注释提到 `_finished`**:该注释说明"防重靠 `_gameOverTriggered/_finished`",而 `_finished` 已在本刀删除。注释在编译态无影响,属文档同步遗漏。按 `feedback-doc-sync-gap-not-code-fail.md` 先例不升 FAIL,记录供 dev 下轮清理。

3. **Play 手验列遗留**:B2/B3 行为级 + 消除道具 arming 交互 + 离线恢复真机 — 上述因 MCP 拖拽限制不可达,代码逻辑层已单测覆盖,表现/交互层留人工冒烟。

4. **dev follow-up 认可**:
   - AccumulatePlayCount 活动无可达触发源 → 语义重定交 boss/plan,本刀合规。
   - CompletedOrders/TotalScore 未进盘 → 增量② 整盘续存时一并定,本刀范围外。
   - GameOverWindow/MergeOrderWinWindow 「重试→MergeOrderWindow」在无尽下语义模糊 → 代码保留未动,follow-up 清理。
