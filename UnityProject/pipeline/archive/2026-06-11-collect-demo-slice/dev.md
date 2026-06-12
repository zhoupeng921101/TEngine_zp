# 状态:开发(dev)

> 开工先读本文件 + roles/dev.md + state/plan.md 交接区。每完成一步就更新这里。

## 当前任务
实现「独立收集 demo 切片(方案 A)」——9 条验收标准全部实现。

## 任务等级
L4(架构级:新模块状态 + 新窗口 + 跨文件;CollectMode 门控回归红线)

## 进度
- [x] 读规范(references:ui-lifecycle/naming/resource/event/modules)+ 设计文档 08 + plan 交接区
- [x] #1 数据模型 CollectDemo.cs(枚举/Key映射/glyph色/静态目标/注入概率)
- [x] #2 PendingPiece.Elements 字段
- [x] #3 BlockGameState collect 门控逻辑(全部 CollectMode 门控)
- [x] #4 CollectDemoWindow + CollectWinWindow + 两个 prefab
- [x] #5 MainMenu 入口按钮
- [x] 编译 0 报错 + EditMode 测试 77/77 通过(含 8 条新增 collect 测试 + 69 条原回归)

## 下一步
转测试。UI 拖拽/渲染/胜利面板需 Play 模式手验(见特殊标注)。

## 自检结果
- 编译:**0 报错**(refresh force + read_console errors=0)
- 单元测试:**BlockBlast.Tests 77/77 通过**(原 69 条 Classic 回归全绿 → 验收#2 单元层成立;新增 8 条覆盖 #2/3/4/5/6/8)
- 核心路径自测:逻辑层经单测覆盖;UI 交互层镜像已跑通的 GameWindow,待 Play 手验

## 交接区(给测试)
> 测试从这里读。

### 改动摘要
按 boss 拍板的 §3.3 决策实现收集 demo 切片(元素来源=A候选块携带;窗口=A新建独立;失败=复用GameOver;表现=glyph/纯色):
- **数据层加法式扩展**:所有 collect 状态(ElementArr/Collected/CollectionTargets)与逻辑(注入/转移/收集/达标)由 `CollectMode` 开关门控。**off 时落子/消除/补块/存档逐字节走原 Classic 路径**——PlacePiece 里 collect 分支仅在 `CollectMode && ElementArr!=null && piece.Elements!=null` 下执行,off 时只多走一个无副作用的本地计数器,SaveArr/board 结果不变。
- **元素链路**(与原版同构):候选块 BuildPiece 按 22% 注入「仍需收集」类型 → PlacePiece 按填充格行优先顺序转移到 ElementArr → 消除时 CollectClearedElements 统计被清格元素并清 overlay → IsCollectionComplete 全达标触发胜利。
- **独立窗口**:CollectDemoWindow 镜像 GameWindow 的渲染/拖拽/ghost/落子流程,叠加元素 overlay 层 + 顶部计数条 + 胜利判定;不动 GameWindow,Classic 零风险。
- **回归保护(关键)**:CollectDemoWindow.OnDestroy 与所有离开路径(退出/胜利/GameOver)都调 `ExitCollectMode()`,确保进入 Classic 前 CollectMode 必为 false。GameOver 路径**不调 Save()**,避免污染 Classic 存档。

### 文件清单
**新增**
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/CollectDemo.cs`(枚举+映射+配置+表现)
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/CollectDemoWindow.cs`(切片主窗口)
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/CollectWinWindow.cs`(胜利面板)
- `Assets/AssetRaw/UI/Prefabs/CollectDemoWindow.prefab`(+.meta)
- `Assets/AssetRaw/UI/Prefabs/CollectWinWindow.prefab`(+.meta)
- `Assets/Editor/Tests/BlockBlast/CollectDemoTests.cs`(8 条新单测)

**修改**
- `Module/BlockBlast/PendingPiece.cs`(+ `CollectElement[] Elements`)
- `Module/BlockBlast/BlockGameState.cs`(+ CollectMode/ElementArr/Collected/CollectionTargets;BuildPiece 注入;PlacePiece 转移;CollectClearedElements/IsCollectionComplete/ResetForCollectDemo/ExitCollectMode)
- `UI/BlockBlastUI/MainMenuWindow.cs`(+「收集 DEMO」入口按钮)

### 验证点(逐条对应 9 条验收)
| # | 验证点 | 怎么验 | 预期结果 |
|---|--------|--------|---------|
| 1 | 元素模型 & demo 目标 | 看 CollectDemo.cs:9 种枚举(Diamond=100…Crown=107,Stone=200)+FromKey(9999→None);DemoTargets=◆×6,★×5。进窗口顶部出 2 个计数器 | 顶部 2 格 `0/6`、`0/5`,glyph ◆/★ |
| 2 | 叠加层 + CollectMode 门控(**回归硬验收**) | EditMode 跑 BlockBlast.Tests(原 69 条);手验 Classic 落子/消除/补块/退出重进存档 | 77/77 全绿;Classic 行为与改前一致 |
| 3 | 候选块携带元素 | 进窗口看候选块上的 glyph;某类型凑满后新块不再出该类型 | 候选块~22%格带元素;达标类型不再注入(单测 BuildPiece_DoesNotInjectSatisfiedTypes) |
| 4 | 落子转移 | 落带元素的块,看棋盘格上 glyph 位置 | 元素与方块格一一对应、无错位(单测 PlacePiece_TransfersElementsInFillOrder) |
| 5 | 消除计数 | 凑一行/列含元素消除 | 计数器按被清元素数增加,被清格 glyph 消失(单测 CollectClearedElements_CountsAndClears;交叉格只计一次) |
| 6 | 达标判定 | 凑齐 ◆×6+★×5 | 瞬间弹胜利面板,之后不可再落子(单测 IsCollectionComplete) |
| 7 | 收集 UI | 顶部计数条实时刷新、达标变绿;棋盘 glyph;胜利面板有达成列表+两按钮 | 见窗口表现;达标项绿色 |
| 8 | demo 入口与重置 | 主菜单点「收集 DEMO」;反复进出 | 每次空棋盘+`0/target`+补满3块,无残留(单测 ResetForCollectDemo) |
| 9 | 失败兜底 | 把 DemoTargets 调大/手动塞满棋盘至无解 | 复用 GameOverWindow,与 Classic 一致 |

### 特殊标注
- **涉及热更程序集**:是。改动全在 `GameScripts/HotFix/GameLogic`(全热更),正式出包需 HybridCLR 重新生成热更 dll;Editor 下直跑无需额外步骤。
- **需 Luban 重生成**:否。切片目标硬编码在 CollectDemo.DemoTargets,不接配置表。
- **需进 Play 模式手验**:**是(重点)**。逻辑层已单测覆盖,但以下交互需 Play 手验:
  1. 拖拽落子 + ghost 高亮(镜像 GameWindow,理应一致)
  2. 棋盘/候选块 glyph 渲染位置正确(◆★ 用内置 Arial 字体可渲染)
  3. 消除后计数器刷新动画、达标变绿
  4. 胜利面板弹出 + 再来一局/返回主菜单
  5. **Classic 回归手验**:主菜单→CLASSIC 完整玩一局,确认与改前无差异
- **已知 UX 取舍**:收集 GameOver 复用的 GameOverWindow「PLAY AGAIN」按钮回到的是 **Classic**(非收集),因复用现有面板;失败面板 SCORE 显示 0(收集模式不计分)。如需「失败也回收集」可后续单独排。

## 测试打回记录
（暂无）
