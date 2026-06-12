# 状态:测试(test)

> 开工先读本文件 + roles/test.md + state/dev.md 交接区。每完成一项验证就更新这里。

## 当前被测任务
收集玩法 collect —— 独立 demo 切片(方案 A)。9 条验收标准。
开发自检:编译 0 报错 + EditMode 77/77。测试独立复验。

## 总判定
**PASS** ✅(四类验证全部通过;无硬失败)

> 附带 2 条「非阻塞」说明,见下方「遗留/观察项」——均不影响收集 demo 验收。

---

## 四类验证结果

### 1. 编译验证 —— PASS
- 操作:`refresh_unity(mode=force, compile=request)` → 轮询 `editor/state` 确认 `is_compiling=false / ready_for_tools=true` → `read_console(types=[error,warning])`
- 结果:**0 error / 0 warning**
- 证据:editor_state `compilation.is_compiling=false`;read_console 返回 `Retrieved 0 log entries`
- Play 全程(两次进出 + Classic):`read_console(error)` = 0 条

### 2. 单元测试 —— PASS
- 操作:`run_tests(EditMode, assembly=BlockBlast.Tests)`,job `a8432812...`
- 结果:**BlockBlast.Tests 77/77 通过(passed=77, failed=0, skipped=0, 1.86s)** —— 独立复现开发自检
  - 含 8 条新增 collect 单测全绿:`Default_CollectModeOff_NoElementArr` / `OffMode_PlacePiece_DoesNotTouchElements` / `ResetForCollectDemo_SetsTargetsAndEmptyBoard` / `PlacePiece_TransfersElementsInFillOrder` / `CollectClearedElements_CountsAndClears`(含行列交叉只计一次)/ `IsCollectionComplete_OnlyWhenAllTargetsMet` / `BuildPiece_DoesNotInjectSatisfiedTypes` / `ExitCollectMode_ClearsState`
  - 含 69 条原 Classic 回归全绿 → **验收 #2 单元层成立**
- 失败用例:无
- 覆盖缺口:逻辑层(注入/转移/收集/达标/重置/off门控)覆盖充分;UI 交互层无自动化单测(属正常,见验证 3)

### 3. 手动功能验证 —— PASS(含 1 项需人工手验,见末行)
进入 Play(Unity 6000.4.7f1),UI 经 UICamera(ScreenSpaceCamera)渲染。逐条核对:

| # | 验证点 | 实际表现 | 符合验收? | 证据 |
|---|--------|---------|-----------|------|
| 1 | 元素模型 & demo 目标 | 进窗口顶部 2 计数器 `◆ 0/6`、`★ 0/5`,glyph 正确 | ✅ | collect_02_window.png;state: targets Diamond=6/Star=5,collected 0 |
| 3 | 候选块携带元素 | 候选块格上预览 glyph(黄块带◆、青块带★);state 显示 4 个携带元素格 | ✅ | collect_02_window.png;`slot element cells=4` |
| 4 | 落子转移(glyph 上板) | 棋盘格上叠加 ◆★◆ 等 glyph,位置与方块格一一居中对齐,无错位 | ✅ | collect_04_board_overlay.png(真实 RenderBoard 渲染);转移顺序由单测 `PlacePiece_TransfersElementsInFillOrder` 保证 |
| 5 | 消除计数 + 达标变绿 | 计数器 `◆ 6/6` 底色变**绿**、`★ 2/5` 白;计数逻辑(行列交叉只计一次)单测保证 | ✅ | collect_04_board_overlay.png;单测 `CollectClearedElements_CountsAndClears` |
| 6 | 达标判定 → 胜利面板 | 弹「收集完成!」面板:达成列表 `◆ 6/6 / ★ 5/5` + 「再来一局」「返回主菜单」两按钮,全屏遮罩锁输入 | ✅ | collect_03_winpanel.png;达标逻辑单测 `IsCollectionComplete` |
| 7 | 收集 UI | 顶部计数条实时刷新 + 达标变绿 + 棋盘 glyph + 胜利面板达成列表/双按钮 | ✅ | collect_02/03/04.png |
| 8 | demo 入口与重置(反复进出无残留) | 主菜单「收集 DEMO」入口在;**真实 UI 路径**进入#1=`0/6,0/5 空棋盘`→×退出→再进入#2 仍=`0/6,0/5 空棋盘`;退出后 `CollectMode=False/ElementArr=null/targets=0` | ✅ | collect_01_mainmenu-1.png;entry#1/#2 state dump;单测 `ResetForCollectDemo_SetsTargetsAndEmptyBoard` |
| 2 | **Classic 回归(红线)** | Classic GameWindow 渲染与改前一致:**无元素层、无计数条、候选块无 glyph**;进 Classic 时 `CollectMode=False/ElementArr=null`,无 collect 污染 | ✅ | collect_05_classic_regression.png;69 条原回归单测全绿 |
| 9 | 失败兜底 | 代码路径:无可落子且未达标 → `TriggerGameOver()` 复用 `GameOverWindow`,且**不调 Save()**(不污染 Classic 存档),先 `ExitCollectMode()` | ✅(代码审查)| CollectDemoWindow.cs:436-445;构造无解局需拖拽,未跑实机 |

**※ 需人工手验(MCP 无法可靠模拟指针拖拽 `BlockPieceDragger`)**:
- 真实拖拽落子 + ghost 落点高亮(Classic 与 Collect 两窗)。
- 落子的**逻辑**(转移/消除/收集/达标/补块/GameOver)已由 77 单测 + state 注入覆盖;**渲染**(glyph/计数条/绿/胜利面板/Classic 正常)已由 5 张截图覆盖。仅「手指拖动→吸附落点」这一交互手势未实机点验,建议人工补一次拖拽手感确认。

### 4. Code Review —— PASS
对照文件清单 diff review + CLAUDE.md 编码红线:

- **热更边界(红线4)**:✅ 全部新增/改动在 `GameScripts/HotFix/GameLogic`(全热更);prefab 在 AssetRaw。无越界。
- **模块访问(红线2)**:✅ 一律 `GameModule.UI.xxx`,无 `ModuleSystem.GetModule<T>()`。
- **资源释放(红线3)**:✅ 未用 `LoadAssetAsync`/YooAsset 句柄(UI 全走 UGuiFactory 基元 + 内置字体 `LegacyRuntime.ttf`,内置资源无需 Unload);ghost池/格子/glyph 均为窗口子物体,随窗口销毁;`ElementArr` 在 `ExitCollectMode`/`OnDestroy` 释放。无泄漏。
- **事件解耦(红线5)**:✅ 窗口未订阅 GameEvent(无残留订阅风险);无事件风暴。
- **异步优先(红线1)**:✅ 无同步 IO/Coroutine。(注:slot glyph 用 `Resources.GetBuiltinResource<Font>` 同步取内置字体——非 YooAsset 资源、与现有 UI 一致,可接受。)
- **命名/前缀**:✅ `CollectDemoWindow`/`CollectWinWindow` 遵循 `XxxWindow`;UI 节点命名镜像 GameWindow(`Bg`/`BoardLayer`/`cell_r_c` 等)。
- **CollectMode 门控对 off 路径零副作用(红线·验收#2 核心)**:✅ 已逐行核验:
  - `BuildPiece`:off 时直接 `new PendingPiece(...)`,**不调 InjectElements**,Classic RNG 序列不变。
  - `PlacePiece`:off 时 `transferElements=false`,内层元素写入分支永不进入,仅多一个本地 `cellIdx++`,**SaveArr 写入逐字节不变**。
  - `CollectClearedElements`/`IsCollectionComplete`/`CollectAt`:开头 `if(!CollectMode...) return 0/false`,off 直接短路。
  - GameWindow(Classic)调用链 `PlacePiece→CanClearRowCols→ClearRowsAndCols` 未改;CollectDemoWindow 在两者间插入的 `CollectClearedElements` 受 CollectMode 门控,Classic 不触达。
  - 离开路径(×退出/胜利/GameOver/OnDestroy)全部调 `ExitCollectMode()`,实机已验退出后 `CollectMode=False`。

---

## 遗留 / 观察项(非阻塞,不影响本任务验收)

1. **全量 EditMode 含 2 条无关失败**:`run_tests(EditMode)` 全量为 79 个,其中 `Xxhq.Htmltougui.Editor.Tests.EditorExampleTest`(SimplePasses/WithEnumerator)抛 `NullReferenceException`。**属 HtmlToUGUI 工具的示例测试,独立程序集,与收集 demo 无关**(来自 `html-to-ugui管线` 提交,非本任务文件清单)。隔离跑 `assembly=BlockBlast.Tests` 即 77/77 全绿。→ 不计入本任务判定,但建议 boss 知会相应负责人清理。
2. **快速双开异步窗口的瞬态读数**:测试中用 execute_code 连续异步 Close/Show 窗口时,曾读到一次 `Diamond 9/6` 的异常瞬态;改用**正常 UI 路径**(×退出→再进入)无法复现,两次进入均为干净 `0/6,0/5`。判为我方异常驱动产生的 race 假象,**非产品缺陷**;正常玩家路径无此操作。

## 证据路径
- 截图:`Assets/Screenshots/collect_01_mainmenu-1.png`(主菜单入口)、`collect_02_window.png`(收集窗 0/6/0/5 + 候选 glyph)、`collect_03_winpanel.png`(胜利面板)、`collect_04_board_overlay.png`(棋盘 glyph + ◆6/6 达标变绿)、`collect_05_classic_regression.png`(Classic 正常)
- 单测:job `a8432812e0884e4fafe87cc385e3f30a`,BlockBlast.Tests 77/77
- 控制台:0 error / 0 warning(编译期 + Play 全程)

## FAIL 时:给开发的可复现清单
(无 —— 总判定 PASS)
