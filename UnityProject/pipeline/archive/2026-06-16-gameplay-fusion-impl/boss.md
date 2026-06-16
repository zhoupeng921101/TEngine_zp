# Boss 关单总结:gameplay-fusion-impl 玩法融合代码落地(2026-06-16)

## 任务定义
按 `design-docs/29-gameplay-fusion.html` 把「经典无尽」吸收进「合成订单」,落成单入口一套玩法。源起:用户「现在就开代码融合」(承接同日 gameplay-fusion 设计稿关单)。

## 参与环节
**dev-test**(设计已定 = 29 设计稿,无需 plan)。0 打回。

## 4 开关定调(2026-06-16 用户「按推荐执行」= 全默认基线)
#1 R1-R3 不接 / #2 DDA 信号维持甲 / #3 dynamicWeight 每局重置 / #4 主分数沿用合成订单累计分。#2/#4 手感方向项按默认实现,代码做出来后由用户实玩再定是否切备选。

## 验收结论:PASS(dev-test,test 四类验证)
- **编译**:0 CS error(test 桥 `UnityProject@02a6dcaa` refresh + read_console)。
- **单测**:EditMode `BlockBlast.Tests` 415/415 全绿(dev/test 两次跑均通过)。
- **2 处断言独立核**:R4(GameOver 重试 GameWindow→MergeOrderWindow)、R5(MergeOrderWindow 从「不得引用塔罗皮」翻转为「承载塔罗皮 + 坐标零回归 + 经济链保留」)——均随设计 29 前提变更的合理翻转、且加严(非放松),test 逐条比对代码与 §3.1/§5.2 一致。
- **Play 手验**:可注入/静态项全过(单入口上屏、塔罗皮上屏、隐患 A/B 注入、存档保底夹值、BEST 迁移三场景);拖拽类三出口 + 老存档 BEST 列 P1-P5 人工遗留(MCP 不能模拟指针拖拽,非缺陷)。
- **Code Review**:9 文件核正确性(单入口 wiring 干净 / Reset 先于 BeginGame / HighScore 保底夹值 / 迁移取较大值不抹老高分 / 塔罗皮用 BlockLayout 既有坐标 / 已落地经济系统内部未动)+ conventions 交叉检无违规。截图 `Assets/Screenshots/fusion_test_*.png`。

## 改动(9 文件,131 增)
单入口(`MainMenuWindow`)、经典入口退役(`GameOverWindow` 重试改指 MergeOrderWindow、`GameWindow` 代码保留仅下线)、三隐患(`MergeOrderWindow` Reset+不变量注释、DDA 信号不变)、存档合并(`MergeMetaSave`+`highScore` 平铺 / `BlockGameState` Export·ImportHighScoreFromMeta 保底 / `GameContext` LoadPlayer 加载+SaveHighScore / `GameWindow` TriggerGameOver 追加 SaveHighScore / `MainMenuWindow` BEST 取元层·旧键较大值迁移)、塔罗皮移植到 MergeOrderWindow(背景 chessboard + 外框 chess,经济 HUD 仍纯色+glyph)。改 2 回归断言(R4/R5,随融合)。无新窗口/prefab/Luban/程序集边界变化。

## boss 观察(供用户知悉,非缺陷)
**融合默认基线下「BEST(HighScore)」成为冻结遗产**:经典纯无尽刷分入口退役后,唯一刷 `Score`→长 `HighScore` 的路径消失;融合窗口用 `TotalScore`(经营累计)作可见量、局内 `Score` 恒 0,不再增长 HighScore。结果:主菜单 BEST 保留老玩家历史最高(迁移保底已做),但**此后不再增长**。这是 #4 默认(可见主分数沿用合成订单累计分)的连带结果,非 bug。若希望 BEST 继续增长,与 #4 备选(恢复经典即时大分数)或「BEST 改追 TotalScore 最高」一并定。已记遗留 #32。

## 遗留(转 boss 主文件)
- #31 gameplay-fusion 代码落地 → **本任务完成,标记已解决**。
- 新增 #32:融合后 BEST 冻结的处置(随 #4 手感方向一起定)。
- 新增 #33:融合窗口经济 HUD(体力/订单/合成区/盲盒/虔诚币/神庙)的塔罗子图配套(本轮维持纯色+glyph,配套塔罗皮是后续美术轮)。
- P1-P5 人工 Play 手验(三出口拖拽触发 + 老存档 BEST + 重试/返回)→ 用户人工。
- 工作树未提交(常规模式不自动提交):9 代码文件 + design-docs(29/01/nav.js)+ pipeline state。待用户审阅后提交。
