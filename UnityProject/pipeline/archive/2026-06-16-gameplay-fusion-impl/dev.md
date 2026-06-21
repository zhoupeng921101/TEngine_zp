# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:玩法融合代码落地(dev-test 的 dev 环节)

设计基线 `design-docs/29-gameplay-fusion.html`,4 开关全默认基线(#1 不接 R1-R3 / #2 DDA 信号维持甲 / #3 dynamicWeight 每局重置 / #4 主分数沿用合成订单累计分)。

### 交接给测试 — 改动摘要

**做了什么 / 为何:** 把经典无尽吸收进合成订单,单入口化 + 三隐患不变量 + 存档合并 + 塔罗皮归属。融合主玩法窗口 = MergeOrderWindow(完整经济超集),经典 GameWindow 代码保留不删、仅下线入口。

1. **主菜单单入口化**(§3.1 / §5.1):`MainMenuWindow.OnCreate` 去掉「CLASSIC / 合成订单 DEMO」二选一,改单个「开始游戏」按钮 → `ShowUIAsync<MergeOrderWindow>`。BEST 显示保留(权威源改为元层,见存档合并);设置/个人信息/排行榜入口一字未动。只动 OnCreate UI 构建,未碰数据层。

2. **经典纯无尽入口退役**(§4.2):`GameWindow.cs` 代码不删(保留类名/location/[Window]/prefab,避免连带风险)。退场路径:① 主菜单不再有进 GameWindow 的入口;② `GameOverWindow` 重试回调改 `ShowUIAsync<GameWindow>` → `ShowUIAsync<MergeOrderWindow>`(融合后 GameOver 仅由融合窗口触发,重试回融合窗口)。

3. **三隐患归一**(§5.3):
   - **隐患 A**(局内瞬态清零显式化):`MergeOrderWindow.OnCreate` 进入路径注释显式声明不变量——`ResetForMergeOrder` 已把 `BlockGameState.Score/Combo` 清零(局内瞬态、不进盘),未改清零行为,只把它写成融合窗口的进入不变量。
   - **隐患 B**(#3 dynamicWeight 每局重置):`MergeOrderWindow.OnCreate` 把 `DynamicWeightDiff.Instance.BeginGame()` 改为先 `Reset()` 再 `BeginGame()`。`Reset()` 清 `_dynamicWeight/_preDynamicWeight/_refillIndex` 并 `Save()` 落盘中位值——消除「跨局/跨模式/跨重启累积」。**连带满足 §5.4③**:每局重置后落盘恒中位值,`block_blast_dynamic_v1` 跨会话存储语义自然退化为无害(不必删该键)。
   - **隐患 C**(#2 DDA 信号维持甲):未引入任何 DDA 信号源改动,口径默认甲(以 `BlockGameState.Score` 为信号)。融合窗口局内不刷 `Score`,DDA 继续休眠在清屏窗口分支——零回归,无配平。

4. **存档合并**(§5.4):把经典 `HighScore` 并入元层时机,与 `MergeMetaSave` 同一加载/落盘节点(范本=PlayerInfo 平铺并入)。
   - `MergeMetaSave` 加平铺字段 `highScore`(int,旧档缺省 0,CurrentVersion 不升)。
   - `BlockGameState` 加纯方法 `ExportHighScoreToMeta(dto)` / `ImportHighScoreFromMeta(dto)`(逐字段保底夹值,负值夹 0;不塞进 MergeOrderState.ExportMeta/ImportMeta,与 PlayerInfo 同体例独立)。
   - `GameContext.LoadPlayer` 同一份 DTO 追加 `ImportHighScoreFromMeta`(与玩家信息同时机加载);新增 `SaveHighScore()` 与 `SavePlayer()` 对称(读回 DTO→只覆写 highScore→`SaveAsync().Forget()`)。
   - `GameWindow.TriggerGameOver` 在 `_state.Save()` 后追加 `GameContext.Instance.SaveHighScore()`(经典遗产可靠并入元层)。
   - `MainMenuWindow.OnCreate` BEST 取「`state.Load()` 旧键 highScore 与元层 highScore 较大值」——迁移期(元层缺省 0、旧键有历史最高)不抹老玩家最高分。
   - 分层不变:`block_blast_save_v1`(棋盘/手牌/分数=局内瞬态,每局重开不进元层);`ImportMeta` 仍只覆盖元字段、不触局内瞬态与悔棋栈(未碰)。

5. **美术换皮归属**(§5.2,实现可解,未上报):塔罗木质皮原在 GameWindow,完整经济原在 MergeOrderWindow——正交分布。判定为实现可解(`SetSubSprite` 寻址已验证、坐标常量两窗共用),直接以 MergeOrderWindow 为主体把塔罗皮移植到核心区:背景 `chessboard`(木纹大图)+ 棋盘外框 `chess`(九宫格框)贴 `Sheet_tarot_mode` 子图,格底改半透深棕(照 GameWindow 范式)。**取舍**:经济 HUD(体力/订单/合成区/盲盒/虔诚币/神庙)在塔罗精灵表无对应子图,维持现状纯色+glyph(与 GameWindow 里资源条/动作按钮占位 stub 镜像同理);给经济 HUD 配套塔罗子图是另一轮美术工作,不在本融合范围。

### 文件清单(新增/修改)

修改:
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MainMenuWindow.cs` — 单入口化 + BEST 元层权威源 + `LoadMetaHighScore` 辅助方法
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWindow.cs` — 隐患 B(Reset)+ 隐患 A 不变量注释 + 塔罗皮(背景/外框/格底)
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/GameOverWindow.cs` — 重试回调改指 MergeOrderWindow + 类头注释同步
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/GameWindow.cs` — TriggerGameOver 追加 SaveHighScore(经典遗产并入元层)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/MergeMetaSave.cs` — 加 `highScore` 平铺字段
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/BlockGameState.cs` — 加 Export/ImportHighScoreFromMeta 纯方法
- `Assets/GameScripts/HotFix/GameLogic/GameContext.cs` — LoadPlayer 加载 highScore + 新增 SaveHighScore
- `Assets/Editor/Tests/BlockBlast/SettlementWindowRegressionTests.cs` — R4 重试断言更新为 MergeOrderWindow(随融合)
- `Assets/Editor/Tests/BlockBlast/GameWindowReskinRegressionTests.cs` — R5 断言更新为「融合主体承载塔罗皮 + 坐标零回归」(随融合)

无新增文件(无新窗口→无新 prefab/location)。无 Luban 重生成。无新热更程序集边界变化(全在既有 GameLogic 热更区)。

### 验证点(逐条对应 §6.2 落地核验项,告诉测试该验什么/怎么验/预期)

- **编译 0 error + 单入口**:已验。`refresh_unity` 后 `read_console` 无 CS 诊断;415/415 EditMode 全绿。主菜单只剩「开始游戏」一个开始按钮,无残留二选一。怎么验:静态读 `MainMenuWindow.cs` 确认无 `BtnClassic`/`BtnMerge`、无 `ShowUIAsync<GameWindow>`;Play 启动主菜单点「开始游戏」进融合窗口。
- **三出口可达**(Play 手验):通关(完成 5 单)/ 体力软 GameOver(精力耗尽)/ 无处可落硬 GameOver。融合窗口沿用 MergeOrderWindow 既有三出口判定逻辑(`PlaceAndResolve` 内未改),预期三路各能触发。**需进 Play 模式手验**。
- **连消倍率隔离**:沿用 `ClearSettlement` 既有单测,本任务未碰结算逻辑,既有连消隔离单测应继续全绿(已全绿)。
- **三隐患按选定策略落地**:
  - 隐患 A:静态读 `MergeOrderWindow.OnCreate` 确认进入路径走 `ResetForMergeOrder`(内清 Score/Combo);可单测断言进窗后 `BlockGameState.Score==0 && Combo==0`。
  - 隐患 B:静态读确认 `DynamicWeightDiff.Instance.Reset()` 在 `BeginGame()` 之前被调;可单测:先 `InternalSetWeight(非0)` → 进融合窗口入口路径 → 断言 `DynamicWeight==0`。预期每局公平起步。
  - 隐患 C:静态确认 `OfferTrio` 调用仍传 `Score`(未改信号源),DDA 信号口径甲。
- **存档合并**(元层往返):
  - 怎么验:`GameContext.InitPlayerFromMeta` 路径可注入 DTO;新增 `highScore` 字段往返——构造 `MergeMetaSave{highScore=N}` → `BlockGameState.Instance.ImportHighScoreFromMeta(dto)` → 断言 `HighScore==N`;反向 `ExportHighScoreToMeta(dto)` 后 `dto.highScore==HighScore`。保底:负值/缺省 dto 夹 0 / null 不改。
  - 分层不变:元层 DTO 不含棋盘/手牌(仍只局内瞬态键);`ImportMeta` 未触 highScore 以外的局内瞬态(highScore 走独立 Export/Import,不进 MergeOrderState 悔棋快照)。
  - 迁移保底:旧玩家 `block_blast_save_v1` 有历史最高、元层 highScore=0 时,主菜单 BEST 取较大值不丢老最高分。**建议 Play 验**:有老存档时启动看 BEST 是否保留。
- **范围开关按 §七默认落地**:#1 不接 R1-R3(发牌只走 DynamicWeightDiff,`RefillPieces` 未插仲裁层)/#2 信号甲 / #3 每局重置 / #4 主分数沿用 TotalScore——四项默认均已落地,静态可核。

### 零回归红线守住情况

- `BlockLayout` 坐标常量(格尺寸/原点/槽位)逐字未动;塔罗皮节点全用 `BlockLayout` 既有值算位置/尺寸(R5 断言已核)。
- 拖拽/ghost 落点/落子合法性判定:`MergeOrderWindow` 的 `OnPieceEnd`/`ComputeGridPos`/`CanPutBlock`/`UpdateGhost` 一字未动。
- 已落地系统(合成/订单/体力/`ClearSettlement` 结算/盲盒/女神/神庙)内部数值与逻辑未碰——本任务只动入口与窗口组织 + 三隐患不变量 + 存档。415/415 既有单测全绿印证。

### 自检结论

- **编译**:`refresh_unity`(force scripts)→ `read_console` 无 CSxxxx;3 条 `MCP-FOR-UNITY disposed object` 是域重载桥重连瞬态,非编译错。
- **EditMode**:`BlockBlast.Tests` 415/415 passed,0 failed(两次跑均全绿,含我更新断言的 R4/R5)。这是最强编译自检 + 逻辑零回归证明。
- **桥状态**:本会话桥可用,编译已实证。
- **需 Play 手验**(逻辑层测不到):三出口实际触发、融合窗口塔罗皮上屏观感、有老存档时 BEST 迁移保留。

### 给测试的注意

- 我同步改了 2 个回归测试断言(R4 重试目标、R5 换皮归属),因它们是设计 26/27 换皮的零回归约束,与设计 29 融合意图冲突——融合改变了前提,断言随之更新(否则测试报假阳越界)。code review 时请核对这 2 处断言更新与代码一致(已确认全绿)。
- GameWindow 代码保留但入口下线:若 review 认为「保留可达的死入口」需进一步处理(如 GameWindow 自身退出仍回主菜单是合理的),属设计 §4.2 命名不连带改动范围,非本任务越界点。
