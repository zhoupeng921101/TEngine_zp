# 状态:策划(plan)

> 开工先读本文件 + roles/plan.md。每完成一步就更新这里。

## 当前任务
设计「独立收集 demo 切片（方案 A）」——在现有 Classic 无尽玩法之上，加元素生成 + 收集计数 + collect 达标判定 + 简单 UI，做成不依赖完整关卡框架的单关 demo。

## 进度
- [x] 扒原版 collect 机制（参考工程 template-vite-ts-zp）：元素类型/Key 映射、RequiredCollections[]、elementArr 叠加层、buildPiece 22% 注入、placePiece 转移、消除计数、isCollectionComplete 判定
- [x] 读现有设计文档 03 / 05 的 collect 背景
- [x] 看 Unity 现状（Module/BlockBlast + UI/BlockBlastUI/GameWindow），确认挂接点
- [x] 产出设计文档 `design-docs/08-collect-demo-slice.html`（互链 + 入 index 导航）
- [x] 写验收标准到本文件「交接区」（9 条）
- [x] boss 已拍 §3.3 的 4 个范围开关（2026-06-10，全取策划建议项）

## 下一步
已派开发。开发从下方交接区逐条实现。

## §3.3 决策(boss 已拍板 2026-06-10)
1. 元素来源 → **A 纯候选块携带**(不在初始棋盘预置)
2. 窗口落地 → **A 新建独立 `CollectDemoWindow`**(Classic 零风险)
3. 失败兜底 → **复用现有 GameOver 流程**
4. 元素表现 → **glyph/纯色叠加**(零美术)

## 交接区(给开发)——验收标准
> 开发从这里读。每条都要可被测试核对。设计细节见 `design-docs/08-collect-demo-slice.html`。
> 总原则:所有 collect 逻辑由 `CollectMode` 门控;off 时 Classic 行为零变化(回归硬验收)。

| # | 功能点 | 完成定义(测试可核对) | 涉及模块/UI/事件/配置 |
|---|--------|----------------------|----------------------|
| 1 | 元素数据模型 & demo 目标 | 定义 9 种元素枚举 `CollectElement`(Diamond=100…Crown=107, Stone=200) + Key→元素映射(含 9999=分数,收集解析时跳过)。一份静态 demo 收集目标(如 ◆×10、★×8)。进入 demo 时正确解析出 N 种目标,顶部按 N 种类型显示计数器,初值 `0/target`。 | 新增 `CollectElement` 枚举 + 静态 demo 配置常量;`BlockGameState.CollectionTargets` |
| 2 | 元素叠加层 + CollectMode 门控 | 新增与 `SaveArr` 平行的 `ElementArr[8][8]`(默认 None) + `Collected`/`CollectionTargets` 字典 + `CollectMode` 开关。**CollectMode=off 时**:落子/消除/补块/存档(Save/Load)行为与现状逐字节一致(Classic 回归测试通过)。 | `BlockGameState.cs`(新增字段);Classic 回归 |
| 3 | 候选块携带元素(生成) | CollectMode 下 `BuildPiece` 对每个填充格按 ~22% 概率注入一个「仍需收集」类型(从 `Collected[t] < Target[t]` 集合随机抽);某类型 `Collected≥Target` 后,新候选块**不再**出该类型。概率/类型集合为可调常量。 | `BlockGameState.BuildPiece`;`PendingPiece.Elements`(新增可选数组) |
| 4 | 落子转移 | 落子时按形状填充格的行优先顺序,把 `piece.Elements[cellIdx]` 写入 `ElementArr[posRow+r][posCol+c]`。落子后棋盘对应格显示元素图标,位置与方块格一一对应(无错位)。 | `BlockGameState.PlacePiece`;`CollectDemoWindow` 渲染 |
| 5 | 消除计数 | 消行/列时遍历被清格,凡 `ElementArr[r][c]` 有元素 → `Collected[type]++` 并清该格 overlay。触发一次含元素的消除后,顶部计数器按被清元素数量正确增加,且被清格元素图标消失。 | 新增 `BlockGameState.CollectClearedElements(rows,cols)`,配合 `ClearRowsAndCols` + `BinaryBoard.CanClearRowCols` |
| 6 | collect 达标判定 | 所有活跃类型 `Collected[t] ≥ Target[t]` 即达标。凑齐全部目标的瞬间触发胜利(弹胜利面板),之后锁输入不可再落子。未达标不触发。 | `BlockGameState.IsCollectionComplete()`;`CollectDemoWindow` 胜利事件 |
| 7 | 收集 UI | 顶部计数条:每活跃类型一格(图标 + `got/target`),实时刷新,达标项变绿。棋盘格上渲染元素图标(落子显示、被消移除;飞向计数器动画为可选)。胜利面板:标题 +各目标达成 + 返回主菜单/再来一局按钮。 | 新增 `CollectDemoWindow.cs` + 胜利面板(新增或复用结算窗);glyph/纯色,无新美术 |
| 8 | demo 入口与重置 | 主菜单新增「收集 Demo」入口按钮 → 打开 `CollectDemoWindow`。进入即重置:空棋盘 + 清 `Collected`(全 0)+ 重设 `Target` + 补满 3 块。反复进入,每次都从 `0/target`、空棋盘开始,无上一局残留。 | `MainMenuWindow`(加按钮);`BlockGameState.ResetForCollectDemo()` |
| 9 | 失败兜底(复用) | 剩余手持块无处可放且未达标 → 触发 GameOver(复用现有 `GameOverWindow` 流程,与 Classic 一致)。 | 复用 `BinaryBoard.CanPutAnyOf` + `GameOverWindow` |

## 已知问题 / 待澄清
> 设计文档 §3.3,需 boss 拍板后再派开发:
1. **元素来源**:A 纯候选块携带 / B 也在初始棋盘预置少量元素。策划建议 A 为主(默认),B 仅在「想更快出镜」时加。
2. **窗口落地**:A 新建独立 `CollectDemoWindow`(建议,Classic 零风险)/ B 在 `GameWindow` 加 collect 分支。
3. **失败兜底**:复用 GameOver(建议)/ 无失败一直补块到达标。
4. **元素表现**:glyph/纯色叠加(建议,零美术)/ 等美术出图集。
> 另:切片**砍掉**星级评定(步数效率)、score_to_win(Key 9999)、collect 进度存档、关卡 Map/LevelLoader——均属完整关卡框架,不在切片范围。
