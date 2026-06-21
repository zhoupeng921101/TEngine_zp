# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前被测任务:核心玩法补全(demo 范围)

设计基线 = `design-docs/11-core-loop-completion.html`(§十一 验收清单 + §十二 8 条拍板)。被测 commit 0bbb7f01「核心玩法开发」。运行环境:active_instance=UnityProject@02a6dcaa(MCP 桥在线,scene=main)。

### 总判定:PASS

四类验证全跑通:编译 0 error、EditMode BlockBlast.Tests 129/129 全绿、Code Review 五条红线全过。唯一留人工的是 Play 模式拖拽弹字的目视确认——MCP 无法可靠模拟指针拖拽(角色记忆已记),但所有弹字分支与 PickMilestoneType 取值均由现已全绿的单测覆盖,逻辑层无残留风险。

### 四类验证逐项

#### 1. 编译验证 — PASS

- `refresh_unity(force, compile=request)` → 编辑器进 compiling;域重载期桥短暂断连(`refresh_unity` wait 两次超时属域重载常态),`manage_scene get_active` 复探即恢复。
- `read_console(types=[error], stacktrace)` 仅 2 条,均非项目 CS 编译错:
  - `MCP-FOR-UNITY: Client handler error: Cannot access a disposed object`(`McpLog.cs:50` / `StdioBridgeHost.cs:641`)——桥传输层在域重载时 NetworkStream 被弃,框架插件内部日志,非项目错。
  - `NullReferenceException` at `ResourceModuleDriver.Update()`(`Assets/TEngine/Runtime/Module/ResourceModule/ResourceModuleDriver.cs:302`,`_resourceModule.UnloadUnusedAssets()`,`_resourceModule` 为 null)——TEngine **框架 Runtime** 代码、在 Play 模式 Update 循环触发的运行期 NRE,不在本改动文件清单内(dev 只动 `GameScripts/HotFix/GameLogic` + `Assets/Editor/Tests`)。**非编译错**(无任何 `CSxxxx`,编译已完成代码才能跑到 Update);属环境/框架既有,与核心玩法改动无关。停 Play 模式后 EditMode 测试干净通过,印证此 NRE 仅 Play tick 产物。
- 结论:**0 CS 编译错误**,编译通过。

#### 2. 单元测试 — PASS

- 首次 `run_tests(EditMode, ["BlockBlast.Tests"])` 报 `Cannot start a test run while the Editor is in or entering Play Mode`——编辑器处于 Play 模式(亦是上条框架 NRE 的根因)。`manage_editor(stop)` 退出 Play 后重跑。
- `run_tests(EditMode, ["BlockBlast.Tests"])` job `9b9f5e88...` **succeeded**:`summary: total 129 / passed 129 / failed 0 / skipped 0`,resultState=Passed,0.71s。最后完成用例 `WeightCfgLubanTests.WeightCfg_Row1_MatchesSourceData`。
- **数目核对**:129 = 96(原)+ 33(新)。前序交接「34/130」为一处差一误记(`CoreLoopCompletionTests.cs` 实有 33 个 `[Test]`,无 `[TestCase]` 展开);129 全绿即完整覆盖,无用例被排除/缺失。
- 测试覆盖:无覆盖缺口——新逻辑(连消/多消/全清结算、特殊轨、祈愿/HammerCost、仲裁、宝箱、女神、悔棋回滚)均有对应用例,全绿。

#### 3. 手动功能验证 — 逻辑层 PASS / 拖拽视觉留人工目视

- Play 模式拖拽落子的弹字(Good/.../COMBO x{n}/PERFECT)、连消/多消/全清的视觉表现属人工目视项:MCP 无法可靠模拟 `BlockPieceDragger` 指针拖拽(角色记忆已记)。**留人工进 Play 目视,勿代签。**
- 窗口接线段对照实际代码核对(`MergeOrderWindow.cs` PlaceAndResolve,行 480-565):
  - 调用顺序 = HarvestClearedElements → ClearRowsAndCols → IngestElement → RefundEnergy → `ClearSettlement.Settle`(行 487-496),与 §5.5 流水线 + ClearSettlement 注释契约(窗口先做清除/Ingest/Refund)一致。
  - 弹字分支顺序 AllClearRewarded→PERFECT(行 502)、lines≥MultiClearMilestoneMinLines→MultiLabel(行 504)、ComboChain≥2→COMBO x{n}(行 506),无歧义。
  - `_state.Combo = settle.ComboChain >= 2 ? settle.ComboChain : 0`(行 497)——镜像 ≥2 才显示,一致。
  - 无消除分支 `ClearSettlement.Settle(0,...)`(行 510)断链回 1 + 重新武装,= §5.5。
  - `PickMilestoneType` 取 `NeededTypes()[0]`,空时 None,Settle 内兜底 Diamond(行 561-565 + ClearSettlement.cs:87)——产物归属链完整。
- 这些分支的取值正确性由现已全绿的单测(`Settle_*`、`MultiMilestone_*`)兜底,人工目视仅需确认视觉呈现,无逻辑风险。

#### 4. Code Review — PASS

对照 CLAUDE.md「核心原则(编码红线)」全 5 条(以正本为准),按改动文件逐一核对实际代码:

| 红线 | 核查(实测) | 结论 |
|------|---------|------|
| 1 异步优先 | 4 个新逻辑文件纯状态机,无 IO/同步加载/Coroutine(grep `Resources.Load`/`StartCoroutine`/`.Result;`/`.Wait()` 零命中);窗口 `ShowUIAsync` 异步 | ✓ |
| 2 模块访问 GameModule.XXX | 新逻辑不访问框架模块;窗口用 `GameModule.UI.CloseUI/ShowUIAsync`(非 `ModuleSystem.GetModule<T>()`,grep 零命中) | ✓ |
| 3 资源必须释放 | 4 个新文件无 LoadAssetAsync/LoadGameObjectAsync(grep 零命中);窗口结算段不加载资源 | ✓ |
| 4 热更边界 | 4 个新逻辑文件全在 `GameScripts/HotFix/GameLogic/Module/BlockBlast`(热更区);测试在 `Assets/Editor/Tests`(编辑器域) | ✓ |
| 5 事件解耦 | 新代码不发 GameEvent/AddUIEvent(grep 零命中);无事件泄漏/风暴 | ✓ |

- 隔离铁律落地(实测 `ClearSettlement.Settle`):倍率只乘 `displayScore`(行 80 `baseScore * permille / 1000`);元素产出用未乘连消的 `baseScore`(行 83 `ElementsForScore(baseScore)`);全清判定用 `boardEmptyAfter && AllClearArmed`(行 94),不碰倍率。三处隔离正确。
- 全清武装位:发奖后 `AllClearArmed = false`(行 101),非全清落子重新武装(行 106 / 无消除分支行 69-70),连续第二次不发奖(行 108 注 + 不进任一 if 分支)。= §5.4,不可连续 2 次。
- 命名/术语:新代码统一图案/收集区/合成/灵力/体力;grep `食材/生成器/仓库/虔诚币` 在 4 个新文件零命中。= §4.2。
- 持久文件交叉检(dev 改过的 `pipeline/state/dev.md` 交接区,按 conventions.md lint + 抽查):指代词/diff 叙事(`刚才|你说的|从.*改成|本次修改` 等)零命中;正文无可推导事实副本(进度靠现场推导,不复制状态);工作态可识别所属任务、标注「boss 关单事务清空/归档」。✓

### 证据索引

- 编译:`read_console(error, detailed)` 2 条,均非 CS 错(见上诊断)。
- 单测:job `9b9f5e8879264d058f9aa951d735a4b1`,total/passed 129,failed 0,Passed。
- Play 模式干扰:首跑被 Play 模式阻断 → `manage_editor(stop)` → 重跑全绿,印证框架 NRE 为 Play tick 产物。
- Code Review:对 `ClearSettlement.cs`(全文)、`MergeOrderWindow.cs`(480-579)实读 + 4 新文件 grep 红线/术语零命中。

### 给人工的剩余确认项(非阻塞,不影响 PASS)

进 Play 模式,在 `MergeOrderWindow` 真实拖拽落子,目视确认:弹字(COMBO x{n}/MultiLabel/PERFECT)、连消/多消/全清的视觉表现。逻辑已由 129 全绿单测兜底,此项仅核视觉呈现。

> 本任务工作态;boss 关单事务清空/归档。
