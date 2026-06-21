# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:个人信息窗(PlayerInfoWindow)美术换皮 · 表现层(塔罗 UI 自治线第 2 屏,自治·放手默认)

设计稿:`design-docs/25-player-info-window-art.html`(已挂 nav.js GROUPS 表现层组 + 全库 sidebar/index 卡片自动同步;断链检查 0 处:本篇锚点全解析、跨篇链接全在、nav.js 各 href 落实际文件;node --check nav.js OK;无 BOM)。
范式来源:复用设计 23 设置窗(`design-docs/23-settings-window-art.html`)全套基础设施,**不重造**。本轮 = 纯 UI 补完,兑现设计 18 玩家信息系统表现层(遗留 #22)。

### 设计基线(经 grep 核实的真实符号,dev 据此定位)
- **数据层(已实装,只调用,命名空间 `GameLogic.BlockBlast.Player`)**:`PlayerInfo`(字段 Id/Name/RenameCount/Exp/CurrentAvatarId/CurrentFrameId/UnlockedAvatarIds[]/UnlockedFrameIds[];只读 `Level`;静态 `CreateDefault(rng)`/`ExportToMeta(dto)`/`ImportFromMeta(dto,rng,avatarValid,frameValid)`;常量 `DefaultAvatarId=1`/`DefaultFrameId=101`);`PlayerRenameService.TryRename(p, newName, IReadOnlyCollection<string> wordList, Func<int,bool> trySpendDiamond)` → `RenameResult`(`Success`/`Reason`/`Cost`;`RenameReject{None,Empty,TooLong,Profanity,NotEnoughDiamond}`;常量 `MinLen=1`/`MaxLen=16`);`RenamePriceConfig.RENAME_PRICE=100`;`AvatarUnlockService.StateOf/IsUnlocked/TryEquip`;`PlayerLevelConfig.LevelFor/ExpIntoLevel/ExpToNext`;`PlayerNameGenerator.Generate(rng)`;`ClipboardUtil.Copy(text)`。配置 `GameLogic.Config.AvatarConfigMgr`(`GetAvatar/GetByType/All/EnsureLoaded`)。
- **持久化(复用,已建)**:`GameLogic.BlockBlast.MergeMetaSave`(DTO,设计 18 已平铺玩家字段 playerId/playerName/playerRenameCount/playerExp/curAvatarId/curFrameId/unlockedAvatarIds[]/unlockedFrameIds[]) + `MergeMetaPersistence`(`Load`/`SaveAsync` 等)。`PlayerInfo.ExportToMeta`/`ImportFromMeta` 是纯方法,模型↔DTO 转换 + 逐字段保底夹值。
- **运行期上下文(已建,本轮扩持有)**:`GameLogic.GameContext : SimpleSingleton<GameContext>`(`Module/BlockBlast/SimpleSingleton.cs`;现有成员 `Settings`;现有 `OnInit` new SettingsService + Load;现有测试注入入口 `InitSettingsWithStore(store)`)。设计 23 §五已明示「player-info/item/mail/rank 后续逐个挂入」——本轮兑现 player-info 项。
- **取图 API(已打通)**:`Image.SetSubSprite("Sheet_settings", 子图名)`。设计 23 实测:精灵表 = 单张 `Sheet_settings.png`(Sprite Mode=Multiple + 命名子精灵),location=`Sheet_settings`,子图名=源切图文件名。本屏复用,无新切图/无新图集/不动打表工具。
- **可用子图全集(= 设置窗 22 张切图文件名)**:base_plate/base_plate2/base_plate3/box1/box2/button/icon_x/x/chat/clear/exit/facebook/game/help/instagram/language/Player_music/printer/setting/twitter/Volume_up/youtube。**圆头像框/编辑铅笔/下拉箭头/头像 Sprite 都不在其中 → 占位**。
- **UI 框架/窗口范式(同设计 23)**:`UIWindow` + `[Window(UILayer.Top, location:"PlayerInfoWindow", fullScreen:false)]`;生命周期 `ScriptGenerator → OnCreate → OnRefresh`;绑定 `FindChildComponent<T>(path)`;接钮 `onClick.AddListener`(工程无 RegisterButtonClick);打开 `GameModule.UI.ShowUIAsync<T>()`/关闭 `CloseUI<T>()`。命名空间 `GameLogic.UI`(同 `SettingsWindow.cs`,落同目录)。
- **入口现状**:`GameLogic.BlockBlastUI.MainMenuWindow`(code-built)第 58–63 行已加过 `BtnSettings` 入口(范式参照);第 65–67 行留有 `// TODO(player-info UI 轮): 左上角入口 → 打开 PlayerInfoWindow` 钩子 → 本轮兑现。
- **分辨率**:1080×1920(场景 main.unity 已覆写 CanvasScaler),prefab 直接用真实锚点,不套 BlockLayout 750×1334 私有系。
- **关键缺口**:数据层 `PlayerInfo` **无生日字段**;效果图有「生日 + 3 下拉」→ UI 占位不入盘(决策 D2)。

---

## 交接区(交开发)

### dev 改动清单(详见设计稿 §四/§五/§六/§七;符号名经 grep 核实。C=新建 M=改既有增量 H=钩子兑现 T=测试 X=不碰)
- **C1** `PlayerInfoWindow.cs`(窗口脚本):落 `GameScripts/HotFix/GameLogic/UI/`(同 `SettingsWindow.cs`),命名空间 `GameLogic.UI`。`[Window(UILayer.Top, location:"PlayerInfoWindow", fullScreen:false)] : UIWindow`。`ScriptGenerator` 按 §四节点树绑定 + `onClick` 接钮;`OnCreate` 贴静态图(`SetSubSprite("Sheet_settings", box1/box2/icon_x/button…)` + 缺图占位);`OnRefresh` 读 `GameContext.Instance.Player` 刷名 + 头像。改名委托 `PlayerRenameService.TryRename`(空词表 + `cost=>true`),`RenameResult` 四拒因分支提示;确定/X/遮罩 → 保存 + `CloseUI`。骨架见设计稿 §六代码块。
- **C2** `PlayerInfoWindow.prefab`:落 `AssetRaw/UI/Prefabs/`(同 `SettingsWindow.prefab`)。节点树见 §四:根 RectTransform+Canvas+GraphicRaycaster;`m_btn_Mask`(全屏遮罩,树最底)+ `Root`{`m_img_TitleBg`/`m_text_Title`/`m_btn_Close`/`m_img_PanelBg`/`AvatarBlock`{`m_img_Avatar`/`m_img_AvatarFrame`/`m_btn_EditAvatar`}/`NameBlock`{`m_text_Name`/`m_input_Name`/`m_btn_EditName`}/`BirthdayBlock`{`m_text_BirthdayLabel`/`m_drop_Year`/`m_drop_Month`/`m_drop_Day`}/`m_btn_Confirm`/`m_text_CloseHint`}。1080×1920 锚点对位 `个人信息.png`。被收集器 `UI` 组(AddressByFileName)收录,location=`PlayerInfoWindow`。
- **M1** `GameContext.cs`:加 `public PlayerInfo Player { get; private set; }` 成员;`OnInit` 里 Load 玩家信息(无档 `CreateDefault(rng)`,有档 `ImportFromMeta(dto,rng,avatarValid:id=>AvatarConfigMgr.GetAvatar(id)!=null)`)。**B1 取档时机 dev 二选一**(见下);加测试注入入口 `InitPlayerFromMeta(dto,rng)`(仿既有 `InitSettingsWithStore`)。既有 `Settings` 一行不改。
- **M2** `MainMenuWindow.cs`:兑现第 65–67 行 TODO 钩子——照 `BtnSettings`(58–63 行)加一个 `UGuiFactory.CreateButton` + `onClick` → `GameModule.UI.ShowUIAsync<GameLogic.UI.PlayerInfoWindow>()`。约 5 行。
- **H1**(改名落盘,与 B1 同接缝):改名成功 + 确定/关闭 → `P.ExportToMeta(dto)` + 调既有 `MergeMetaPersistence.SaveAsync(dto)`(落点 dev 按工程存档时序定,见 B1)。
- **T1** 新建 `Assets/Editor/Tests/BlockBlast/PlayerInfoWindowTests.cs`(或并入既有 GameContextTests.cs;asmdef 已引用 GameLogic,新 .cs 自动纳入):H/W 组验收(GameContext 往返 / OnRefresh 反射驱动刷名 / 改名贯通 4 分支 / 占位不抛)。
- **X 不碰(零回归)**:`GameLogic.BlockBlast.Player` 各类逻辑、`AvatarConfigMgr`、`SettingsService`、`MergeMetaSave` 任一字段(生日不入盘)、框架 UI/资源代码、Classic/Merge 玩法窗口、设计 18/23 数据层单测。

### B1(需 dev 对齐工程加载时序,非方向问题——决策已定纳入 GameContext,只是接线落点)
`GameContext.OnInit`(SimpleSingleton 同步)取玩家存档 DTO 可能与 `MergeMetaPersistence.Load`(异步 UniTask 外壳,设计 14)时序不一致。dev 二选一:
- **路 A**:`GameContext.OnInit` 不同步加载玩家档,由 `GameApp.StartGameLogic()` 在存档加载完成后(同设置窗 AudioSink 接线那段附近)调 `GameContext.Instance.InitPlayerFromMeta(dto, rng)`(仿既有 `InitSettingsWithStore` 注入入口)。
- **路 B**:若工程已有同步可达的当前 `MergeMetaSave`/`PlayerInfo`(如 `MergeOrderState`/`BlockGameState` 已在内存持有),直接引用、不重复读盘。
两路对本窗验收等价。改名后落盘(H1)接同一存档路径。

### 验收标准(test 逐条核;详见设计稿 §九。两档:H/W=EditMode 可单测,V=需 Play/人眼)
**H/W 组(EditMode 可单测)**
- **H1** 编译过:新增 PlayerInfoWindow.cs + 改 GameContext.cs/MainMenuWindow.cs 后热更程序集 0 error。
- **H2** GameContext 持有 PlayerInfo 往返:`Instance.Player` 非 null;注入已知 DTO → 字段==DTO 值;无 DTO → ==CreateDefault 缺省(头像 1/框 101/系统名)。
- **W2** 玩家名显示读数据层:设 `Name="测试名"` → 反射调 `OnRefresh` 后 `m_text_Name.text=="测试名"`。
- **W3** 改名贯通数据层 4 分支:合法名 Success 且 Name 改+RenameCount+1;空 →Empty;超 16 →TooLong;空词表不拦(Profanity 不触发);`trySpendDiamond=>false` 且非首次 →NotEnoughDiamond。**验收 = 窗口确实委托 `PlayerRenameService.TryRename`,不自写改名逻辑**(数据层 R1–R5 已有单测)。
- **W4** 窗口绑定路径对齐:`FindChildComponent` 各 path 与 §四节点树逐一对齐(code review + Play 实测无 null)。
- **W5** 占位项不崩:编辑头像/生日下拉/各占位点击 → 走 `ShowPlaceholder`(Log.Info),不抛异常/空引用。

**V 组(需 Play/人眼,MCP 截图+手验)**
- **V1** 能从主菜单入口打开,无报错。
- **V2** 视觉对位 `个人信息.png`(标题/头像+框/名+铅笔/生日 3 下拉/确定/关闭提示位置一致,占位图允许外观不同、位置须对)。
- **V3** 切图贴对:box1/box2/icon_x/button 等 `SetSubSprite` 正确显示。
- **V4** 改名指针闭环:点铅笔→输入框显→输新名→确认→刷新;重开窗保留(落盘生效);非法名见拒绝提示。
- **V5** 三种关闭(X/确定/遮罩空白)都生效;点面板内容不穿透关窗。
- **R** 零回归:CLASSIC/合成订单 DEMO 正常进玩;既有 EditMode 单测照过、编译 0 error。

- **BLOCKED 条件**:(1) unityMCP 桥不可达致 EditMode/Play 跑不起 → V 组 BLOCKED 不判 FAIL(备选 batchmode 跑 EditMode);(2) 头像 Sprite/圆框/铅笔/下拉箭头切图缺失 = **预期占位**,不判 FAIL(本就占位)。

### 关键自治决策(放手默认推进;详见设计稿 §十待拍板表)
- **D1** 玩家信息纳入 `GameContext.Player`(延续设计 23 §五统一上下文预告)。安全默认 √,可逆。
- **D2** 生日 UI 占位、**不入存档**(数据层无字段、spec 未要求、加字段溢出「纯 UI 补完」范围)。安全默认 √,可逆——日后产品要真生日,数据层加字段是局部增量、UI 节点已摆好、替占位即可不返工。
- **D3** 头像三态选择网格本屏不做(效果图本屏只显当前头像,完整网格属设计 18 后续屏)。编辑头像点击占位。
- **D4** 等级槽/id 复制本屏不补(效果图本屏无该两位;数据层有 `Level`/`PlayerLevelConfig`/`ClipboardUtil`/`PlayerInfo.Id` 可显,属设计 18 后续屏)。
- **D5** 改名交互 = 就地输入框(`m_text_Name`/`m_input_Name` 同位切换),不引入独立 RenameWindow(效果图无该窗)。
- **D6** 入口落主菜单(同设计 23,兑现既有 TODO 钩子);玩法 HUD 顶栏入口后续轮。

### 待 boss/用户裁决(范围开关,不阻塞,均按本轮默认推进;关单复核)
- D2 生日是否真做 + 入存档(默认 UI 占位)——**最值得 boss 一看**:效果图有、数据层无、本轮按占位推进。要真做另开数据层增量。
- D3 头像三态网格是否后续屏做(默认本屏占位)
- D4 等级槽/id 复制是否后续屏补(默认本屏不补)

### blockers(令自治流水线中止的方向问题):无
(D1–D6 均有安全默认、与 spec/GDD 不抵触、可逆 → 入 decisions 不入 blockers。B1 是接线落点选择、两路等价,亦非方向问题。)

上一单 ui-atlas-packer 打表工具已于 2026-06-15 关单,归档 `pipeline/archive/2026-06-15-ui-atlas-packer/`。
