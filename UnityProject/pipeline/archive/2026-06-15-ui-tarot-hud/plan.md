# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:tarot_mode 主玩法 HUD reskin(塔罗 UI 自治线第 4 屏 · centerpiece · 风险最高)

设计稿:`design-docs/27-tarot-mode-hud-art.html`(已注册 nav.js GROUPS「表现层 / 换皮」组,断链/anchor/BOM/lint 全过)。

### 调查结论(关键)

- **tarot_mode = Classic `GameWindow`(435 行)的再主题,非合成订单 `MergeOrderWindow`(799 行)。** 证据:效果图有「大居中分数」、无「订单卡/合成区/体力条」——正是 GameWindow 特征(合成订单窗顶部是体力/单数/标题、无大居中分数)。本轮只改 `GameWindow.cs` 的 `BuildStaticUI` 视觉部分,回归面 = Classic 整局玩法。
- **顶栏 3 资源条多数无数据源。** Classic 数据层 `BlockGameState` 经 grep 只有 `Score`/`HighScore`/`Combo`,无体力/货币/钻石/心/提示次数字段。→ 资源条占位(第 1 条可接 HighScore,其余数字占位,加号去变现不接购买)。
- **更换/删除/提示 = 三个新玩法机制,Classic + 数据层均无。** → 视觉占位 + stub(点击 Log 待建),真机制本范围外。
- reskin 方式取**路 A 轻量换皮**(复用设计 26 范式:对在跑 code-built 窗的 `UGuiFactory` 返回 Image / `out bgImage` 链 `.SetSubSprite`),不取 prefab 重构。
- **本屏是首个用设计 24 打表工具产新精灵表的真实换皮屏**(前三屏复用 `Sheet_settings`):16 张切图 → 导入 ASCII 目录 `AssetRaw/UIRaw/Atlas/tarot_mode/` → `UIAtlasPacker.Pack(...)` 或菜单 `Tools/UI/打表(散切图 -> Multiple 精灵表)` → 产 `Sheet_tarot_mode.png` → `SetSubSprite("Sheet_tarot_mode", 子图名)`。

---

## 交接区(交开发)

### 范围(严格,纯 UI 补完——只换静态视觉壳)

- **改**:`Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/GameWindow.cs` 的 `BuildStaticUI`(:86–132)——背景/棋盘外框/格底(可选)/分数面板贴 `Sheet_tarot_mode` 子图 + 底色白;新增静态顶栏(头像+3 资源条+齿轮)+ 3 动作按钮(更换/删除/提示)+ stub 钩子;齿轮接 `ShowUIAsync<SettingsWindow>`;退出入口保留。
- **新增资源**:切图 16 张(ASCII 目录 `tarot_mode/`)+ 打表产 `Sheet_tarot_mode.png`+`.meta`(落 `AssetRaw/UIRaw/Atlas/`,收集器自动收)。
- **绝不碰**:`RenderBoard`/`RenderSlots`/`OnPieceBegin/Drag/End`/`PlaceAndResolve`/`UpdateGhost`/`InitGhostPool`/`ComputeGridPos`/`TriggerGameOver`/`UpdateBest`/`OnUpdate` 玩法逻辑;`BlockLayout` 棋盘坐标映射常量(动了落子对位偏);`BlockGameState`/`MergeOrderState` 数据层(不加字段);`MergeOrderWindow`/合成订单玩法;收集器配置;打表工具本身。

### 静态壳换皮精确清单(改哪个文件哪几处 / 每处换哪张子图 / 哪些占位)

| 节点(BuildStaticUI) | 处置 | 子图(dev 读图核实)/ 占位 |
|---|---|---|
| `Bg` 全屏背景 | 换皮 | `chessboard`/`image`/`blue`(木纹底)+ 底色白;无整屏图则保纯色微调 |
| `BoardOuter` 棋盘外框 | 换皮 | `chess`(九宫格);**位置/尺寸不动** |
| 64 个 `cellbg_r_c` 格底 | 可选换皮(默认保纯色) | `Rectangle`/`blue`;默认方案 A 保既有纯色(外框+背景已出木质风,64 格逐贴开销/视觉杂)**位置/尺寸不动** |
| `Score` 大分数 | 保留/微调 | 沿用 Text;字号/色/位对位效果图。位图数字为可选增强 B2 |
| `BestLabel`+`Best` 最高分 | 保留/改位 | 文本逻辑不动;可挪进顶栏资源条之一 |
| `Exit` 退出钮 | 改/挪位 | 效果图右上是齿轮不是 ×;**退出入口须保留**(回调不改),挪位或并入(D4) |
| (新增)头像 | 视觉占位 | `mask`/占位圆(无数据源 D2) |
| (新增)3 资源条 | 视觉占位 | 条底 `resourcebar2` + 图标 `gemstone`/`gemstone2`/`potion` + 数字(第 1 条接 `HighScore`、其余占位)+ 加号 → Log 待建(D1) |
| (新增)齿轮 | 真接线 | `icon_setting` → `ShowUIAsync<SettingsWindow>`(设计 23 已建) |
| (新增)3 动作按钮 | 视觉占位+stub | 条底 `Rectangle` + 图标(`magic_book`/`hammer` 等读图核实)+ 文本「更换/删除/提示」;点击 → Log 待建 + TODO(D5) |

> 设计稿给了顶栏(§5.3)与动作按钮(§六)的示意代码骨架(SetSubSprite + onClick stub),dev 对着 tarot_mode.png 微调坐标。

### 资产流程(dev 要走的)

1. 16 张切图 `C:\Users\pc\Downloads\塔罗\塔罗\塔罗模式\` → 导入 `Assets/AssetRaw/UIRaw/Atlas/tarot_mode/`(**ASCII 目录**,避中文 location);每张 Sprite/Single/FullRect,九宫格图(resourcebar2/Rectangle/chess)源 PNG 设 Border。
2. 跑打表:菜单 `Tools/UI/打表(散切图 -> Multiple 精灵表)`(Project 选中 tarot_mode 目录)或 `UIAtlasPackerTool.UIAtlasPacker.Pack("Assets/AssetRaw/UIRaw/Atlas/tarot_mode")` → 产 `Sheet_tarot_mode.png`(16 子图,不覆盖已存在文件——重跑先删旧表)。
3. **dev 落地第一步先验寻址跑通**(取一张子图显示出来,确认 `SetSubSprite("Sheet_tarot_mode","chess")` 非 null)再铺满整窗。

### 验收标准(逐条,test 可核对)

**回归 R(硬:不破坏对应玩法 + Classic + Merge,编译零回归)——主验收 = 编译 0 error + 静态核对 + git diff:**
- C1 `GameWindow.cs` 编译 0 error;现有 EditMode 全绿。
- C2 Code Review 5 红线(资源释放/热更边界/事件解耦/模块访问/异步优先)。
- R1 玩法逻辑行未改:静态核对 `RenderBoard`/`RenderSlots`/`OnPieceBegin/Drag/End`/`PlaceAndResolve`/`UpdateGhost`/`InitGhostPool`/`ComputeGridPos`/`TriggerGameOver`/`UpdateBest`/`OnUpdate` 与换皮前逐行一致(只 BuildStaticUI 视觉变)。
- R2 棋盘坐标映射未改:`BlockLayout` 的 `BoardOriginX/Y`/`BoardPixels`/`CellSize`/`CellCenterDesign`/`SlotCenterX`/`SlotY`/`SlotSpacing` 等常量未动;换皮节点 position/size 沿用既有值;落子 grid 计算路径不变。
- R3 数据层未改:`BlockGameState` 无新增字段;`OperaArr`/`SaveArr`/`Score`/`HighScore`/`Combo` 读写路径不变;资源条/动作按钮未往数据层加状态。
- R4 退出回调目标未改:退出入口仍 `CloseUI<GameWindow>`+`ShowUIAsync<MainMenuWindow>`;齿轮新增的 `ShowUIAsync<SettingsWindow>` 是叠层、不关本窗、不丢局。
- R5 合成订单玩法不受影响:`MergeOrderWindow.cs` 本轮未改(git diff 仅 `GameWindow.cs`+资源);`MergeOrderMode` 门控独立。

**视觉 V(Play 对位 + 切图贴对 + 玩法仍可进可玩,需 Play/人眼手验):**
- V1 打表 + 切图经 SetSubSprite 正常显示(`Sheet_tarot_mode` 16 子图运行期取得到)——本屏核心(首用打表工具产新表)。
- V2 对位 tarot_mode.png:顶栏(头像+3资源条+齿轮)+大分数+木质棋盘外框+8×8+3候选块+底部3动作按钮。
- V3 棋盘对位不偏:换皮后落子 ghost/已落方块仍精确贴 8×8 格。
- V4 Classic 整局可进可玩(零回归实玩):摆块/落子/消除/连击弹字/补块/GameOver/分数滚动/最高分全正常。
- V5 齿轮→开设置窗(不丢局);退出→回主菜单;加号/更换/删除/提示→Log 待建(不报错、不动玩法)。
- V6 资源条占位/动作按钮 stub 不报错、不挡棋盘/候选块/落子区、不穿帮。
- V7 合成订单窗不受影响(从主菜单进合成订单正常)。

> R 组主验收以编译 + 静态核对 + git diff 为主(GameWindow 玩法依赖运行期单例 + UIWindow 生命周期 + 拖拽指针,EditMode 反射驱动成本高);整局可玩 + 真机点击拖拽归 V4/V5 手验。R1–R5 任一玩法逻辑被改即不通过(换皮越界)。

---

## decisions(自治默认推进,boss 关单复核;要改另开增量)

- **D1 顶栏 3 资源条语义/数据源/加号**:无资源字段(BlockGameState 只 Score/HighScore/Combo)→ 视觉占位(第 1 条接 HighScore、其余数字占位、图标 gemstone/potion 占位、加号 Log 待建,去变现不接购买)。真做需产品定义 3 资源各是什么+数据源+加号行为,后续轮。**最值得 boss/产品复核。**
- **D5 3 动作按钮(更换/删除/提示)机制**:Classic+数据层均无 → stub(Log 待建+TODO),真机制本范围外。需产品定义(花什么资源/几次/规则)后另开轮。**最值得 boss/产品复核。**
- D2 头像数据源:Classic 无 PlayerInfo 接入 → 占位圆图(不为头像把 PlayerInfo 接进 Classic GameWindow);真头像可后续接设计 18 默认头像。
- D3 切图 `advertisement`(广告):去变现方向 → **不投放**。
- D4 退出入口归属:**保留独立退出钮**(挪位但回调不改,不依赖设置窗有退出项);产品若定「只留齿轮、退出走设置窗」可并入。
- B1 棋盘格底是否换皮:默认外框+背景换皮、格底保纯色(方案 A);dev 视效果可连格底换。
- B2 分数/资源条数字是否用位图:默认沿用 Text;要位图须先确认 `image` 切图是数字条并逐位拼,后续增强。
- B3 背景/外框/各底用哪张子图:dev 读图选最贴者。

## blockers(空)

本轮全部范围开关有安全默认、可逆、不抵触 GDD 主线(去变现·离线还原)→ 取默认推进、记 decisions、不入 blockers(不停机)。无「无安全默认 / 抵触 spec / 不可逆」的方向问题。

## taskFlaw(无)

任务定义无硬伤:需求自洽、设计基线(GameWindow/设计 23/24/26)真实存在且未归档、与工程现状一致、范围可行。
