# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区（角色职责在 `.claude/agents/pipeline-test.md`，spawn 时自动注入）。每完成一项验证就更新这里。

## 当前任务:玩法融合代码落地 验证报告

**总判定：PASS**

设计基线：`design-docs/29-gameplay-fusion.html`，4 开关全默认（#1 不接 R1-R3 / #2 信号甲 / #3 每局重置 / #4 主分数沿用合成订单累计分）。

---

## 一、编译验证 — PASS

- `refresh_unity`（force scripts compile request）后 `read_console(types=["error"])` 返回 0 条。
- 桥状态：`UnityProject@02a6dcaa`，编辑器场景 `Assets/Scenes/main.unity` 正常响应。
- 无 CS 编译错误，无警告需关注（域重载桥重连瞬态已知，非编译错）。

---

## 二、单元测试 — PASS

- `run_tests(EditMode, assembly=BlockBlast.Tests)` → **415/415 passed，0 failed，0 skipped**，耗时 3.65s。
- 最后一例：`WeightCfgLubanTests.WeightCfg_Row1_MatchesSourceData`（正常结束）。

### 重点独立核对：dev 改的 2 处断言更新

**R4（SettlementWindowRegressionTests）**

断言：`GameOverWindow` 重试回调目标为 `MergeOrderWindow`，不得再指 `GameWindow`。

对照代码（`GameOverWindow.cs:77`）：
```
GameModule.UI.ShowUIAsync<MergeOrderWindow>();
```
对照设计 29 §3.1：经典纯无尽入口下线，GameOver 仅由融合窗口触发，重试回融合窗口。

判定：断言与设计意图一致，是前提变更后的合理更新，不是放松。

**R5（GameWindowReskinRegressionTests）**

断言：融合主体 `MergeOrderWindow` 承载塔罗皮（`Sheet_tarot_mode` + `chessboard` + `chess`）+ 坐标零回归（`BlockLayout` 既有常量）+ 经济链路保留（`PlacePiece`、`ClearSettlement.Settle`）。

对照代码（`MergeOrderWindow.cs:24`，`122-131`，`230`，`640`）：
- `private const string Atlas = "Sheet_tarot_mode"` ✓
- `bg.SetSubSprite(Atlas, "chessboard")` ✓
- `boardOuter.SetSubSprite(Atlas, "chess")` ✓
- `boardCx = BlockLayout.BoardOriginX + BlockLayout.BoardPixels / 2f` ✓
- `_state.PlacePiece(slotIdx, _board, col, row)` ✓
- `ClearSettlement.Settle(...)` ✓

对照设计 29 §5.2：塔罗皮归属移植到融合主体，零回归红线（`BlockLayout` 坐标常量不动）。

判定：断言是设计 29 融合改变归属前提后的合理翻转，与代码实际一致，不是放松。

---

## 三、Play 手验 — 部分通过（拖拽类遗留人工手验）

### 3.1 可验项（MCP 静态/注入路径）

| 验证点 | 手段 | 结果 |
|---|---|---|
| 主菜单单入口：仅「开始游戏」，无 BtnClassic/BtnMerge 残留 | Play 截图 `fusion_test_mainmenu_ui.png` | PASS |
| 点「开始游戏」→ MergeOrderWindow 打开，MainMenuWindow 关闭 | execute_code 触发 BtnStart.onClick + GameObject.Find 验证 | PASS |
| 隐患 A 不变量：进窗后 Score=0, Combo=0, MergeOrderMode=True | execute_code 反射读字段 | PASS |
| 塔罗皮上屏：木纹背景+棋盘外框渲染正常 | Play 截图 `fusion_test_mergeorder_tarot.png`（UICamera） | PASS，视觉符合预期 |
| 体力/订单/虔诚币/悔棋 HUD 显示正常 | 同上截图 | PASS |
| 隐患 B：Reset() 清 _dynamicWeight/_preDynamicWeight/_refillIndex → 0 | execute_code 注入 -200→Reset→断言 | PASS |
| BeginGame() 不修改 _dynamicWeight | 同上 | PASS |
| 存档 Import(9999)→HighScore=9999 | execute_code 反射注入 | PASS |
| 存档 Export()→dto.highScore=9999 | execute_code 反射断言 | PASS |
| 存档 Import(-1) 负值保底→HighScore=0 | execute_code 反射断言 | PASS |
| 存档 Import(null) 不改现有 HighScore | execute_code 反射断言 | PASS |
| BEST 迁移：取元层/旧键较大值（三场景） | execute_code 模拟三场景（3000<5000→5000; 8000>5000→8000; 0/0→0） | PASS |
| DDA 信号口径甲：`OfferTrio(board, Score)` 未改 | Grep `BlockGameState.cs:181` | PASS |
| 开关 #1 R1-R3 未接：MergeOrderWindow 无 HandGenerationArbiter 引用 | Grep MergeOrderWindow.cs | PASS |
| 已落地系统未被碰：IngestElement/RefundEnergy/ClearSettlement.Settle 在位 | Grep MergeOrderWindow.cs | PASS |

### 3.2 人工 Play 手验遗留（MCP 无法模拟指针拖拽）

以下须人工进入 Play 模式手验，非代码缺陷，不影响本次判定：

1. **三出口实际触发**：
   - 通关（完成 5 单）：手动拖拽落子→消除→交付→完成 5 单→MergeOrderWinWindow 弹出
   - 体力软 GameOver（精力耗尽）：持续落子耗光体力且无单可交→「精力耗尽」GameOver 弹出
   - 硬 GameOver（无处可落）：手牌无合法落点→GAME OVER 弹出

2. **有老存档时 BEST 显示**：启动前 PlayerPrefs 有 `block_blast_save_v1.highScore=N`，主菜单 BEST 应显示 N（不被元层缺省 0 抹掉）。

3. **GameOver 重试回调**：游戏结束后点「重试」→ 回到 MergeOrderWindow（不回经典 GameWindow）；点「返回」→ 回主菜单。

---

## 四、Code Review — PASS

### 4.1 正确性核对

| 核验项 | 位置 | 结论 |
|---|---|---|
| 单入口 wiring：无残留 BtnClassic/BtnMerge，无 `ShowUIAsync<GameWindow>` 活入口 | 全局 Grep GameLogic *.cs | PASS，仅 MainMenuWindow→MergeOrder、GameOver→MergeOrder、WinWindow→MergeOrder 三条正确路径 |
| Reset 在 BeginGame 之前（隐患 B）| `MergeOrderWindow.cs:78-79` | PASS，`Reset()` 在第 78 行，`BeginGame()` 在第 79 行 |
| HighScore Export/Import 保底夹值（负值夹 0 / null 不改）| `BlockGameState.cs:375-389` | PASS，`dto.highScore > 0 ? dto.highScore : 0` |
| 迁移取较大值不抹老最高分 | `MainMenuWindow.cs:22-23` | PASS，`if (metaHigh > state.HighScore) state.HighScore = metaHigh` |
| 塔罗皮节点用 BlockLayout 既有坐标常量（零回归红线）| `MergeOrderWindow.cs:127-131` | PASS，`BlockLayout.BoardOriginX/BoardPixels` 逐字沿用 |
| 已落地系统内部未被动（合成/订单/体力/结算/盲盒/女神/神庙）| 读 MergeOrderWindow.cs 全文 | PASS，`PlaceAndResolve` 经济链路逻辑一字未动 |
| DDA 信号口径甲（OfferTrio 传 Score）| `BlockGameState.cs:181` | PASS |
| R1-R3 未接入（HandGenerationArbiter 无引用）| MergeOrderWindow.cs Grep | PASS |
| GameWindow 入口下线（TriggerGameOver 追加 SaveHighScore，退出回 MainMenuWindow 不变）| `GameWindow.cs:417-426` | PASS |
| GameOverWindow 重试改指 MergeOrderWindow | `GameOverWindow.cs:77` | PASS |

### 4.2 Conventions 交叉检（dev 改过的持久文件/散文型注释）

对 dev 改动的 9 个文件散文型注释/doc-comment 逐句过「收尾必做」自检：

| 文件 | 过程内容进正文 | 可推导副本 | 拟人/口语比喻 | 旁注孤儿 |
|---|---|---|---|---|
| `MainMenuWindow.cs` 类头 + 注释 | 无 | 无 | 无 | 无 |
| `MergeOrderWindow.cs` 类头 + 注释 | 无 | 无 | 无（`OnAppPause` 注释用平实说明文体） | 无 |
| `GameOverWindow.cs` 类头 | 无 | 无 | 无 | 无 |
| `GameWindow.cs` TriggerGameOver 注释 | 无 | 无 | 无 | 无 |
| `MergeMetaSave.cs` `highScore` 字段注释 | 无 | 无 | 无 | 无 |
| `BlockGameState.cs` 方法头注释 | 无 | 无 | 无 | 无 |
| `GameContext.cs` `LoadPlayer`/`SaveHighScore` doc-comment | 无 | 无 | 无 | 无 |
| `SettlementWindowRegressionTests.cs` 测试头注释 | 无 | 无 | 无 | 无 |
| `GameWindowReskinRegressionTests.cs` 测试头注释 | 无 | 无 | 无 | 无 |

所有注释脱离当下对话成立，无 diff 叙事，无私造圈外词。

`MergeOrderWindow.cs` 内注释 `OnAppPause` 节使用「切后台」系行业通用说法，可接受（非私造词）。
`FlushSaveIfDirty` doc-comment 中「即发即忘」是行业惯用术语，可接受。

---

## 五、待人工 Play 手验清单

| # | 验证点 | 操作步骤 | 预期结果 |
|---|---|---|---|
| P1 | 通关出口 | Play 模式进 MergeOrderWindow，手动拖拽消除交付 5 单 | MergeOrderWinWindow 弹出，显示完成单数 + 累计得分 |
| P2 | 体力软 GameOver | 持续落子耗光体力（20→0），不交付任何单 | GameOverWindow 弹出，标题「游戏结束」，有「重试」→ MergeOrderWindow |
| P3 | 硬 GameOver | 棋盘塞满至手牌三块均无合法落点 | GameOverWindow 弹出，「重试」→ MergeOrderWindow |
| P4 | 有老存档 BEST | 先在 PlayerPrefs 写入 block_blast_save_v1（highScore=3000），重启 Play，进主菜单 | BEST 显示 3000，不被元层缺省 0 抹掉 |
| P5 | GameOver 重试/返回 | 任意触发 GameOver 后，点「重试」；再次 GameOver 点「返回」 | 重试→MergeOrderWindow；返回→MainMenuWindow |

---

## 六、截图存档

- `D:/work/TEngine_block/UnityProject/Assets/Screenshots/fusion_test_mainmenu_ui.png` — 主菜单单入口上屏
- `D:/work/TEngine_block/UnityProject/Assets/Screenshots/fusion_test_mergeorder_tarot.png` — 融合窗口塔罗皮上屏
