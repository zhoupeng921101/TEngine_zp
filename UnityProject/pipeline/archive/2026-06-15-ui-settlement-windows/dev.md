# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区（角色职责在 `.claude/agents/pipeline-dev.md`，spawn 时自动注入）。每完成一项就更新这里。

## 当前任务:结算窗 reskin（GameOverWindow + MergeOrderWinWindow）· 塔罗 UI 自治线第 3 屏

---

## 交接区（交测试）

### 改动摘要

对两个已在运行的 code-built 结算窗做轻量换皮（路 A）：把 `UGuiFactory.CreateImage`/`CreateButton` 返回的 `Image` 对象链 `.SetSubSprite("Sheet_settings", 子图名)` 换成木质贴图，底色改 `Color.white` 让木纹原色透出。结算逻辑、UserData 解析、finalScore/high/isNewBest 计算、三处调用点传参、四个按钮回调目标**一行未改**。

装饰层决策：取方案 B（省略三图标/太阳格），原因是 Sheet_settings 无星/心/草/太阳切图，摆纯色方块美观度不如直接保留分数+按钮核心。方案 B 符合设计稿 §4.3 指引，可逆。

### 文件清单

**改动（视觉部分）**
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/GameOverWindow.cs`
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWinWindow.cs`

**新增（测试）**
- `Assets/Editor/Tests/BlockBlast/SettlementWindowRegressionTests.cs`

**未改（零回归保护）**
- `GameWindow.cs`（调用点 :336）
- `MergeOrderWindow.cs`（调用点 :731、:718）
- `UGuiFactory`、`BlockLayout`、`BlockGameState`、`MergeOrderState`、`GameContext`
- `Sheet_settings.png` + 收集器

### 子图映射（B4 实施结果）

| 节点 | 子图 | 说明 |
|---|---|---|
| 主面板卡片（Card） | `box1` | 最大木板（1002×435 px），九宫格可拉伸 |
| 标题木牌底（TitleBg） | `box2` | 较高木板（1002×864 px），叠在卡片上沿 |
| 所有按钮底（BtnAgain/BtnMenu） | `button` | 方形九宫格（118×118 px）拉成长条 |

### 验证点（对应 R1–R4 / C1–C2 / V 组）

**C1（硬）** 编译 0 error：Unity 控制台 `read_console types=["error"]` 返回 0 条。

**C2（硬）** 编码红线核对：
- 无裸 `LoadAssetAsync<Sprite>`，取图走 `SetSubSprite`（自管引用计数）
- 按钮回调用 `onClick.AddListener`（UI 内部事件解耦）
- 模块访问用 `GameModule.UI`
- 两窗在热更区 `GameScripts/HotFix/`

**R1（硬）** GameOverWindow Classic 路径：`UserData is int p ? p : 0` 解析行在；`state.Score`/`state.HighScore` 取值行在；`finalScore > previousHigh && finalScore > 0` 计算行在；`GameWindow.cs:336` 传 `previousHigh` 行在。单测 `R1_GameOverWindow_UserDataParsing_Unchanged` + `R1b_GameWindow_CallSite336_Unchanged` 均通过。

**R2（硬）** 订单结束路径：`MergeOrderWindow.cs:731` 仍传 `0`。单测 `R2_MergeOrderWindow_CallSite731_PassesZero` 通过。

**R3（硬）** 通关路径：`UserData as List<string>` 解析行在；`lines.Count`/`lines[i]` 渲染循环在；`MergeOrderWindow.cs:718` 仍传 `lines`。单测 `R3_MergeOrderWinWindow_UserDataParsing_AndRenderLoop_Unchanged` + `R3b_MergeOrderWindow_CallSite718_PassesLines` 均通过。

**R4（硬）** 四回调目标：重试→`GameWindow`，GameOver 返回→`MainMenuWindow`，再来一局→`MergeOrderWindow`，通关返回→`MainMenuWindow`。单测 R4 四条全通过。

**EditMode 全量跑 BlockBlast.Tests**：378 条全绿，0 失败（含本次新增约 14 条回归测试）。

**V 组（需 Play / 人眼，留 boss 主会话桥跑）**
- V1：ShowUIAsync 弹出两窗后截图，核木牌+木板+按钮有木质贴图
- V2：GameOverWindow 对位「游戏结束.png」—— 标题「游戏结束」+ box2 木牌 + box1 大木板 + 分数 + 最高分 + 重试/返回按钮
- V3：MergeOrderWinWindow 对位「恭喜通关.png」—— 标题「恭喜通关」+ box1 大木板 + 结算行列表 + 再来一局/返回按钮
- V4：三条触发路径真机弹窗正常（Classic 死局/订单结束/通关）
- V5：按钮真机点击跳转正确
- V6：装饰层 = 方案 B（省略三图标/太阳格），无多余节点、无报错

### 标注

- **热更程序集**：是，两窗在 `GameScripts/HotFix/`；新测试在 Editor-only `BlockBlast.Tests` 程序集
- **Luban 重生成**：不需要（无配置改动）
- **需 Play 模式手验**：V1–V6 组，由 boss/test 跑

### decisions 记录

- **D1（装饰层）**：取设计稿方案 B（省略三图标/太阳格），无切图+无数据源，方案 A 占位图美观度不达标
- **B3（文案）**：通关窗「再来一局」，回调目标 MergeOrderWindow 不改；文案 boss 可拍「下一关」（只改显示文字）
- **B4（子图选择）**：box1=主面板，box2=标题木牌底，button=所有按钮底

---

## 完成状态

编译 0 error + EditMode 378 条全绿，交接测试。
