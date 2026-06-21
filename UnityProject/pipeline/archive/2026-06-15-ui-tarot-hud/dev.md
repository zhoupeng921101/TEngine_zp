# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:tarot_mode 主玩法 HUD reskin(塔罗 UI 自治线第 4 屏 · centerpiece)

设计稿:`design-docs/27-tarot-mode-hud-art.html`;交接基线:`pipeline/state/plan.md` 当前任务节。
状态:**已落地 + 编译 0 error + EditMode 全绿(407/407),交测试。** V 组(Play 对位 + 整局实玩)留 boss 桥手验。

---

## 交接区(交测试)

### 1. 改动摘要(做了什么 / 为何这么做 / 关键决策)

按设计稿路 A 轻量换皮,**只换 `GameWindow.BuildStaticUI` 的静态视觉壳,玩法逻辑一行未动**:

- **背景 / 棋盘外框换皮**:`Bg` 贴 `chessboard`(木纹底)、`BoardOuter` 贴 `chess`(九宫格框),color 一律设 `Color.white` 让木纹原色透出(同设计 26 经验)。棋盘外框位置 / 尺寸沿用 `BlockLayout.BoardOriginX/Y + BoardPixels`,绝不动(动了落子对位偏)。
- **格底取方案 A(默认)**:64 个 `cellbg` 保纯色不贴图(外框 + 背景已出木质风,64 格逐贴开销 / 视觉杂);微调为偏暖半透深棕 `(0x3a,0x24,0x14,0x55)` 叠在木纹上更贴效果图浅格。位置 / 尺寸沿用既有 `CellCenterDesign`。
- **新增静态顶栏**(方法 `BuildTopBar`):头像占位(`mask`)+ 3 资源条(底 `resourcebar2` + 图标 `gemstone`/`gemstone2`/`potion` + 数字 + 加号)+ 齿轮(`icon_setting`)+ 退出钮(挪位,回调一字不改)。
- **新增 3 动作按钮**(方法 `BuildActionButtons`):底 `Rectangle` + 文本「更换 / 删除 / 提示」+ 删除配 `hammer` 图标(更换 / 提示无精准图标用纯文本)。
- **分数 / BEST 保留**:`_scoreText` / `_bestText` 重建但行为不变(`OnUpdate` 滚动、`UpdateBest` 变色仍引用它们);分数字号放大到 96 对位效果图大白字,BEST 挪到分数下方小字。
- **齿轮真接线**:`ShowUIAsync<SettingsWindow>()`(`SettingsWindow` 在 `GameLogic.UI` 命名空间,故加 `using GameLogic.UI;`)——叠层弹出,不关本窗、不丢局。
- **占位 / stub 严守不越界**:资源条第 1 条接只读 `_initialHigh`(HighScore),其余静态占位 "0";加号 → `Log.Info` 待建(去变现,不接购买);3 动作按钮 → `Log.Info` 待建 + TODO(新玩法机制本范围外)。**不往数据层加任何字段,不实现任何机制。**

**关键决策**(均按 plan decisions 默认推进):D1 资源条占位、D5 动作按钮 stub、D2 头像 `mask` 占位、D3 广告不投放(`advertisement` 未进打表)、D4 退出钮独立保留、B1 格底保纯色(方案 A)、B2 分数沿用 Text。

### 2. 文件清单(便于 code review / diff)

**本任务改 / 增(只这些):**
- `M` `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/GameWindow.cs`(换皮:+104/-16,全落 BuildStaticUI 视觉区 + 2 新方法;玩法逻辑方法体零增删)
- `??` `Assets/Editor/Tests/BlockBlast/GameWindowReskinRegressionTests.cs`(回归测试,14 个 [Test])
- `??` `Assets/AssetRaw/UIRaw/Atlas/tarot_mode/`(13 张切图 + .meta,ASCII 目录)
- `??` `Assets/AssetRaw/UIRaw/Atlas/Sheet_tarot_mode.png`(+.meta,打表产物,13 子图 Multiple,2048×1024)
- `??` `Assets/AssetArt/Atlas/Atlas_tarot_mode.spriteatlasv2`(工程自动机制连带产物,见下「注意 2」)

**未碰(R 组保护对象 + 简报禁区):** `BlockLayout.cs` / `BlockGameState.cs` / `MergeOrderState.cs` / `MergeOrderWindow.cs` / 收集器 / 打表工具 / Fantasy / manifest / ProjectSettings。

> git status 另有 `GameApp.cs`(M)、`GameLogic.asmdef`(M,加 FantasyClient)、`Fantasy/`(??)——**非本任务引入**,文件 mtime(08:43/08:51)早于本会话改动(10:06),是工作树既有的 Fantasy 网络库接入遗留。我未碰它们。

### 3. 验证点(逐条对应 plan 验收标准,告诉测试该验什么 / 怎么验 / 预期)

**已自验通过(编译 + EditMode 静态核对):**
- **C1 编译 0 error + EditMode 全绿** — ✅ `read_console` 0 error;`run_tests BlockBlast.Tests` = **407/407 passed, 0 failed**(含新 14 个换皮回归 + 既有全套零回归)。复验:`run_tests EditMode assembly=BlockBlast.Tests`。
- **C2 五红线** — ✅ 测试 `C2_GameWindow_CodeRedlines`:无裸 `LoadAssetAsync<Sprite>`(走 SetSubSprite 自管引用计数)、`onClick.AddListener`、`GameModule.UI`;热更边界(GameWindow 在 HotFix)。
- **R1 玩法逻辑行未改** — ✅ 测试 `R1`/`R1b`:RenderBoard/RenderSlots/OnPieceBegin·Drag·End/PlaceAndResolve/UpdateGhost/InitGhostPool/ComputeGridPos/TriggerGameOver/UpdateBest/OnUpdate 签名 + 落子/消除/补块/GameOver 调用链关键行全在。git diff 佐证:玩法逻辑方法体零增删行,删除行全是被替换的视觉构建行。
- **R2 棋盘坐标常量未改** — ✅ 测试 `R2`/`R2b`:BlockLayout 的 BoardSize/CellSize/BoardPixels/BoardOriginX·Y/SlotCell/SlotY/SlotSpacing/SlotCenterX/DesignWidth·Height 全未动;换皮节点仍由 BlockLayout 既有常量算位置 / 尺寸。
- **R3 数据层未改** — ✅ 测试 `R3`/`R3b`:BlockGameState 字段集(SaveArr/OperaArr/Score/HighScore/Combo)未增删;反向断言无 Gold/Gem/Diamond/HintCount 等新字段;资源条 / 动作按钮是占位 + Log stub,不写回数据层。
- **R4 退出回调 + 齿轮叠层** — ✅ 测试 `R4`/`R4b`/`R4c`:退出回调仍 `CloseUI<GameWindow>`+`ShowUIAsync<MainMenuWindow>`;齿轮接线行不含 CloseUI<GameWindow>(叠层不关本窗);GameOver 调用点仍传 previousHigh。
- **R5 合成订单窗未受影响** — ✅ 测试 `R5`:MergeOrderWindow.cs 未引用 Sheet_tarot_mode;git diff 仅 GameWindow + 资源(MergeOrderWindow 不在改动清单)。

**打表 + 寻址实测结论(本屏核心——首用打表工具产新表):**
- 打表 `UIAtlasPacker.Pack("Assets/AssetRaw/UIRaw/Atlas/tarot_mode", true)` = Success,13 子图,表 2048×1024,Multiple 模式;三张九宫格图 border 正确继承(resourcebar2=(24,12,24,12)、Rectangle=(24,16,24,16)、chess=(12,12,12,12))。
- 产出表 importer 设置与已验证寻址通的 `Sheet_settings.png` **完全一致**(Sprite/Multiple/ppu100/sRGB/无mip/2048),落点同收录树 `AssetRaw/UIRaw/Atlas/` → 寻址链路同源必通。
- **EditMode 无法实测 `LoadSubAssetsAsync` 运行期寻址**:YooAsset default package 在非 Play 态未初始化(`GetAssetInfo` 抛 "Default package is null")。已改为验证产出表子图元数据完整(AssetDatabase 见 13 个命名子精灵、名字 / 尺寸 / border 正确)——这是寻址成功的必要前提。**运行期 SetSubSprite 取图非 null 留 boss Play 桥验(V1)。**

**留 boss Play 桥手验(V 组,MCP 不能模拟拖拽 / 点击):**
- V1 Play 进 GameWindow,13 子图经 SetSubSprite 正常显示(背景 / 外框 / 资源条 / 齿轮 / 动作按钮贴上木质子图,无 NULL)。
- V2 对位 tarot_mode.png:顶栏(头像 + 3 资源条 + 齿轮)+ 大分数 + 木质棋盘外框 + 8×8 + 3 候选块 + 底部 3 动作按钮。
- V3 棋盘对位不偏:换皮后落子 ghost / 已落方块仍精确贴 8×8 格。
- V4 **Classic 整局可进可玩(零回归实玩)**:摆块 / 落子 / 消除 / 连击弹字 / 补块 / GameOver / 分数滚动 / 最高分全正常。
- V5 齿轮→开设置窗(不丢局);退出→回主菜单;加号 / 更换 / 删除 / 提示→Log 待建(不报错、不动玩法)。
- V6 资源条 / 动作按钮不挡棋盘 / 候选块 / 落子区(动作按钮 Y=1288,候选槽 hit 区底 1225,留 ~18px 间隙)。
- V7 合成订单窗从主菜单进正常(本轮未碰)。

### 4. 标注

- **涉及热更程序集**:是。GameWindow.cs 在 `GameScripts/HotFix/`(热更),改动须随热更包发布。
- **需 Luban 重生成**:否。
- **需进 Play 模式手验的功能点**:V1-V7(尤其 V4 整局实玩 + V1 运行期寻址)。
- **注意 1(打表态)**:我跑测试需 EditMode,发现 Unity 当时在 **Play 模式**(疑 boss/用户在手验),已 `manage_editor stop` 退出以跑测;**跑完未重新进 Play**,当前为 Edit 态。boss 若要继续手验需重新进 Play。
- **注意 2(spriteatlasv2 连带产物)**:工程有自动机制按 UIRaw/Atlas 子目录生成 `Atlas_<dir>.spriteatlasv2`(既有 Atlas_setting / Atlas_blocks_* 同此),我新建 tarot_mode 目录连带生成了 `Atlas_tarot_mode.spriteatlasv2`。**它不参与寻址**(设计稿明确 v2 不向 YooAsset 暴露子精灵,我们用 Sheet PNG),删了下次刷新还会再生成。留着不影响功能,boss 知悉即可。
- **注意 3(打表工具边界缺口,值得给工具维护者)**:首跑 16 张时,源含 `image.png`(2.7MB)+ `tarot_mode.png`(2.3MB)两张超大图,打表报 Success=16 但产出表实际只切出 14 子图(这两张被静默丢弃)——`PackTextures` 把超 MaxAtlasSize 的图排进去却越界,工具的 `uvRects.Length != count` 校验未拦住。处置:这两张 + `advertisement`(去变现不投放)本就不参与换皮,已移出打表目录(源在 Downloads 原件仍在),重打得 13 张全部正确切出。**本任务无影响**,但打表工具对「单图超表尺寸」缺有效校验,记给工具维护(设计 24 §四的 2048 校验只防总面积、未防单图越界丢图)。

---

## decisions(沿用 plan,无新增方向偏离)

全部按 plan §decisions 默认推进(D1/D5 占位 stub、D2 头像占位、D3 广告不投、D4 退出独立、B1 格底纯色、B2 文本数字、B3 子图读图选)。子图最终映射(读效果图 + 产出表尺寸核实)已写进 GameWindow.cs 类头注释。

## blockers(空)

无阻塞。编译 + EditMode 自验全过,运行期 V 组按设计可正常落地(寻址链路与已验证的 Sheet_settings 同源),交 boss Play 桥兜验。
