# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前被测任务:collect-rename(验收完成)

**总判定:PASS**

纯改名重构(Collect* 共享设施 → merge-order 语义,9 个符号)。四类验证全过,行为零变化得证,改名目标达成。

### 1. 编译验证 — PASS

- force refresh + compile request → editor ready,无 compiling 残留。
- `read_console types=["error"]` = 0 条(剔除 1 条 MCP 自身 "Cannot access a disposed object" 重连噪声,非工程报错)。
- BlockBlast 相关 warning = 0 条。

### 2. 单元测试 — PASS(96/96)

- `run_tests(EditMode, assembly_names=["BlockBlast.Tests"])`:total 96 / passed 96 / failed 0 / skipped 0,durationSeconds≈0.46。
- 含改名后的 `HarvestClearedElements_OutputsList_ClearsOverlay`、`HarvestClearedElements_RowColIntersection_CountedOnce`(均通过)。
- BlockBlast.Tests 隔离跑即覆盖全部 96 例(无其他程序集混入),不必再跑全量。

### 3. Play 模式手验 — PASS(寻址链通畅,核心风险点已验)

证据截图(`Assets/Screenshots/`):
- `collect-rename_01b_mainmenu_uicam.png`:主菜单正常,"合成订单 DEMO" 入口在。
- `collect-rename_02_winwindow.png`:MergeOrderWinWindow 正常渲染——绿色"通关！"标题 + 3 条结算行 + "再来一局"/"返回主菜单"两按钮。

寻址链验证(本任务核心):
- 经 UI 正路径 `GameModule.UI.ShowUIAsync<MergeOrderWinWindow>(userData)` 触发加载 → 场景内实例化出名为 `MergeOrderWinWindow` 的 active GameObject(6 个 Text、2 个 Button 齐全),无 LoadGameObjectAsync 加载报错。证明 `[Window(location:"MergeOrderWinWindow")]` ↔ prefab 文件名 ↔ prefab m_Name 三处一致、寻址未断。
- `CloseUI<MergeOrderWinWindow>` 经"返回主菜单"按钮触发:WinWindow 销毁、MainMenuWindow 重新激活,无报错。

手验边界(如实记录):
- 未经真实拖拽落子打到自然通关——MCP 无法可靠模拟 BlockPieceDragger 指针拖拽(经验已知)。改用「同一 UI 加载路径直接触发胜利面板」覆盖了 rename 引入的全部寻址风险(WinWindow 加载 + CloseUI 泛型解析)。
- "再来一局"按钮指向 `ShowUIAsync<MergeOrderWindow>`(该窗口非本任务改名对象,寻址早已工作),非 rename 风险,未单独验。

UI 经 UICamera(ScreenSpaceCamera)渲染,默认 Main Camera/ScreenCapture 截图为白屏;须指定 `camera=UICamera` 取景(经验已记 memory)。

### 4. Code Review — PASS

逐文件 git diff 核对「纯改名」声明,全部为符号替换、无逻辑/控制流/数值变化:

- `BlockGameState.cs`:`CollectElement`→`MergeElement`、`CollectClearedElements`/`CollectAt`→`HarvestClearedElements`/`HarvestAt`;循环界、索引、None 判定全等。
- `MergeOrderState.cs`:Order/Inventory/PendingElements/Snapshot 全部 `CollectElement`→`MergeElement`;难度公式、字典键结构、快照克隆逻辑全等。
- `MergeOrderConfig.cs`:OrderPool 8 条 `new Order(MergeElement.*, n, m)` 参数值逐条不变;cref 指向 `MergeElementVisual`。
- `PendingPiece.cs`:`Elements` 字段类型改名 + 注释由"收集模式"改述为"merge-order 模式"(仅文档)。
- `MergeOrderWindow.cs`:`CollectDemo.Glyph/ColorOf`→`MergeElementVisual.*`、`CollectClearedElements`→`HarvestClearedElements`、`ShowUIAsync<CollectWinWindow>`→`<MergeOrderWinWindow>`;消除/入库/返还体力调用顺序与参数全等。
- `MergeElementVisual.cs`(原 CollectDemo.cs):enum 值 100–200、9 个 glyph、9 个 Color32 与旧版逐字节一致(diff 仅名字行)。
- `MergeOrderWinWindow.cs`(原 CollectWinWindow.cs):location 串、类名、CloseUI 泛型参;面板布局/按钮/数值不变。
- `MergeOrderTests.cs`:枚举引用 + 2 个测试方法名;断言与期望值不变。
- prefab:与旧版逐行 diff 仅 `m_Name`(CollectWinWindow→MergeOrderWinWindow)一处。

编码红线(项目根 CLAUDE.md)逐条核:
1. 异步优先 — 未引入新同步 IO,沿用 ShowUIAsync。✓
2. 模块访问 — 仍 `GameModule.UI.*`。✓
3. 资源释放 — 未引入新 Load*,窗口走标准 UIWindow/CloseUI 生命周期。✓
4. 热更边界 — 改动全在 `GameScripts/HotFix/`。✓
5. 事件解耦 — 无事件改动。✓
naming-rules:`MergeOrderWinWindow` 保留 XxxWindow 后缀、`MergeElement` enum、`MergeElementVisual` 静态表现类,均合规。✓

改名零遗漏:`Assets/**/*.cs` 与 `Assets/**/*.prefab` grep `CollectElement|CollectDemo|CollectWinWindow|CollectClearedElements|CollectAt` = 0 匹配。残留 `Collect` 仅:`System.Collections`(框架)、`BlockAlgorithms.CollectClearKeys`(无关动词,dev 已标注)、`v1 不含 ...Collection` 注释——均为 dev 刻意保留项,核对无误。

GUID 保留(无引用断裂):三对 .meta 均 git rename(R),guid 字段与 HEAD 一致——
- prefab.meta `2f31f8db6e174940a8a651dc761b5eb4`
- MergeElementVisual.cs.meta `e5d0a0cd131d004409514a9e63a9edfd`
- MergeOrderWinWindow.cs.meta `bcaa6cf4f238e5d488a28a65cdefeb34`

持久文件交叉检(dev.md):lint 无指代词/diff 叙事(改名清单表的「旧名→新名」属交接区工作态、可识别归属 collect-rename、任务关闭时由 boss 清,正文未污染),通过。

### 范围外观察(不影响本判定)

- 工作树同时含 CLAUDE.md / `.claude/skills/pipeline/SKILL.md` / pipeline-auto.js / boss.md 等改动,非本 rename 任务范围,未纳入本次验收。
