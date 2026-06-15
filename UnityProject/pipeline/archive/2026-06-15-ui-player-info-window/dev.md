# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:个人信息窗(PlayerInfoWindow)美术换皮 · 表现层(塔罗 UI 自治线第 2 屏,自治·放手默认)

设计稿:`design-docs/25-player-info-window-art.html`。范式复用设计 23 设置窗。已完成:编译 0 error,EditMode 378/378 PASS(含既有 366 零回归),交测试。

### 交接区(交开发 → 测试)

**改动摘要(做了什么 + 关键决策):**

1. **新建 PlayerInfoWindow 窗口(C1):** `[Window(UILayer.Top, "PlayerInfoWindow", false)] : UIWindow`,命名空间 `GameLogic.UI`(同 `SettingsWindow.cs`)。ScriptGenerator 按 §四节点树 `FindChildComponent` 绑定 + `onClick` 接钮;OnCreate 贴静态图(`SetSubSprite("Sheet_settings", box1/box2/icon_x/button)` + 缺图占位);OnRefresh 读 `GameContext.Instance.Player` 刷名 + 头像占位色。改名走就地输入框(D5),委托 `PlayerRenameService.TryRename`(空词表 + `cost=>true`),RenameResult 四拒因分支提示。确定/X/遮罩三种关闭 → 保存 + CloseUI。
2. **新建 PlayerInfoWindow.prefab(C2):** 落 `AssetRaw/UI/Prefabs/`,30 节点,1080×1920 居中布局。根 WorldSpace Canvas + GraphicRaycaster(对齐 MainMenuWindow/SettingsWindow 范式);`m_btn_Mask` 全屏遮罩为首个子节点(渲染在内容之后,即面板盖其上,点面板不穿透)。节点树与窗口 FindChildComponent 路径逐条对齐(已 `get_hierarchy` 核)。location=`PlayerInfoWindow`,被 `Assets/AssetRaw/UI` 组 AddressByFileName 收录(同 SettingsWindow 寻址),模拟清单已重建。
3. **GameContext 扩持有 Player(M1):** 加 `public PlayerInfo Player`;OnInit 同步加载(有档 ImportFromMeta、无档 CreateDefault);加测试注入入口 `InitPlayerFromMeta(dto, rng, avatarValid?)`(仿 InitSettingsWithStore);加 `SavePlayer()` 落盘方法。既有 Settings 不改。
4. **改名 + 确定落盘(H1):** 走 `GameContext.SavePlayer()`——先 `MergeMetaPersistence.Load()` 读回既有 DTO(保留玩法元层字段)→ `ExportToMeta` 覆写玩家字段 → `SaveAsync().Forget()`。接既有存档路径,不另造存储栈。
5. **主菜单入口(M2):** 兑现 `MainMenuWindow.cs` 第 65–67 行 TODO 钩子——照 BtnSettings 加「个人信息」按钮 → `ShowUIAsync<GameLogic.UI.PlayerInfoWindow>()`。
6. **占位分流(§七):** 头像本体=按 CurrentAvatarId 取稳定色的纯色块占位(留 TODO 接 AvatarConfigMgr.GetAvatar(id).Image);头像圆框/编辑铅笔/下拉箭头=占位节点(铅笔显「✎」字符、可点);编辑头像/生日下拉点击 → ShowPlaceholder(Log.Info)。
7. **生日(D2):** 3 下拉纯 UI 占位,固定显「3 ▾」,点击给 Log 反馈,不绑数据/不入存档(数据层无生日字段)。

**关键决策(B1 选路 + 设计偏离):**

- **B1 = 同步加载(路 B 变体):** `GameContext.OnInit` 直接同步 `MergeMetaPersistence.Load()` 加载玩家档,不走路 A 的延迟注入。理由:`MergeMetaPersistence.Load` 本就是同步方法(经 Persistence.Provider 非阻塞内存级读,与 `BlockGameState.cs:347` 同口径,不触「禁阻塞 IO」红线),无与异步加载的时序问题;工程未单独持有内存级 MergeMetaSave 实例(各处都现 Load/SaveAsync),故同步 Load 最直接、无新接缝。落盘对称走异步 `SaveAsync().Forget()` 满足异步红线。
- **avatarValid 防御性兜底:** `GameContext.LoadPlayer` 的头像 id 校验回调包了 try/catch——头像配置经 ConfigSystem/YooAsset 加载,启动时序未就绪 / 表缺失会抛,吞异常退「视作有效」(配置不可达时宁可保留原 id,不卡死启动)。非设计错,是接配置层的工程兜底。

### 文件清单

**新增代码(热更区,GameLogic 程序集):**
- `Assets/GameScripts/HotFix/GameLogic/UI/PlayerInfoWindow.cs` — 窗口(绑定 + 接钮 + 刷态 + 改名委托 + 占位分流)
- `Assets/GameScripts/HotFix/GameLogic/GameContext.cs` — 重写:在既有 Settings 持有上增 Player 成员 + LoadPlayer + SavePlayer + InitPlayerFromMeta 注入入口

**改既有代码(热更区):**
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MainMenuWindow.cs` — 兑现 TODO,加「个人信息」入口按钮(约 6 行)

**新增资源(AssetRaw):**
- `Assets/AssetRaw/UI/Prefabs/PlayerInfoWindow.prefab`(+ .meta) — 窗口 prefab(30 节点,1080×1920)。无新切图(复用 Sheet_settings)。

**新增测试(EditMode):**
- `Assets/Editor/Tests/BlockBlast/PlayerInfoWindowTests.cs` — 12 例(H2×3 / W3×5 / W5×3 / StableColorFor×1)

### 验证点(逐条对应设计稿 §九 / plan H/W/V,test 怎么验 + 预期)

**H/W 组(EditMode,test 直接 run_tests):全部已自测通过**
- **H1 编译 0 error:** read_console error 类 0 条;EditMode 全量 **378/378 PASS**(既有 366 零回归 + 本轮新增 12)。
- **H2 GameContext 持有 PlayerInfo 往返:** `PlayerInfoWindowTests.H2_InjectKnownDto_FieldsEqualDto`(注入已知 DTO → 6 字段 == DTO 值)、`H2_NoDto_CreatesDefault`(无 DTO → 头像 1/框 101/RenameCount 0/有系统名)、`H2_Player_Persists_AcrossInstanceAccess`(多次访问同一实例)。注入经 `InitPlayerFromMeta`,不碰真实 PlayerPrefs(SetUp 换 InMemoryPersistenceProvider)。
- **W3 改名贯通数据层 4 分支:** `W3_LegalName_RenamesAndCountsUp`(合法名 Success + Name 改 + RenameCount+1)、`W3_EmptyName_Rejected_NoChange`(空名拒、不变)、`W3_TooLongName_Rejected_NoChange`(超 16 拒)、`W3_EmptyWordList_DoesNotTriggerProfanity`(空词表不触发 Profanity)、`W3_NonFirstRename_DiamondSpendDefaultTrue_Succeeds`(非首次 + 默认 trySpendDiamond=true → 成功)。**验法 = 反射调窗口私有 OnRenameSubmit,观察 GameContext.Player 按数据层规则变化**,证窗口确实委托 `PlayerRenameService.TryRename`、不自写改名逻辑(数据层规则本身由 PlayerInfoTests 覆盖)。
- **W5 占位项不崩:** `W5_EditAvatarPlaceholder_DoesNotThrow` / `W5_BirthdayPlaceholder_DoesNotThrow` / `W5_EnterRename_NoInputField_DoesNotThrow`(bare 窗口 UI 字段全 null,空守卫挡住、不抛)。console 见 `[个人信息窗] 头像选择网格待建` / `生日待接数据层` 两条 Log.Info,印证占位方法真执行过。
- **StableColorFor:** 同 id 同色、不同 id 异色(RefreshAvatar 占位取色逻辑)。

**W2 / W4(节点绑定 + OnRefresh 文本刷新):code review + Play 实测**
- **W2:** OnRefresh 读 `P.Name` 写 `_textName.text`——bare 窗口无真实 UI 组件不能在 EditMode 断言文本,数据侧由 H2 的 Player.Name 往返兜;真实文本刷新留 V 组 Play 实测。code review 核 `PlayerInfoWindow.OnRefresh`。
- **W4 绑定路径对齐:** 已 `manage_prefabs get_hierarchy` 核 30 节点路径与 ScriptGenerator 各 FindChildComponent 路径逐条一致;Play 打开无 null 留 V 组实测。

**V 组(需 Play/人眼,boss 主会话桥跑):**
- **V1 入口开窗:** 主菜单「个人信息」按钮已加;ShowUIAsync 后窗口出现(指针点击留人验)。
- **V2 视觉对位 个人信息.png:** 标题/头像+框/名+铅笔/生日 3 下拉/确定/关闭提示按 1080×1920 锚点摆位;占位图(头像色块/铅笔字符/下拉箭头字符)允许外观不同,位置须对。
- **V3 切图贴对:** box1(面板)/box2(标题)/icon_x(关闭)/button(确定+下拉底)经 SetSubSprite 显示(寻址链路同设计 23 已打通;prefab 也已烤入静态预览 sprite)。
- **V4 改名指针闭环:** 点铅笔→输入框显→输新名→确认→刷新;重开窗保留(SavePlayer 落盘);非法名见 Log 拒绝提示。
- **V5 三种关闭:** X / 确定 / 点遮罩空白 → 关窗;点面板内容不穿透(遮罩为首子节点、内容盖其上)。
- **R 零回归:** EditMode 378/378;加法式改动(仅新增 + GameContext 增成员 + MainMenu 加行),不动玩法窗;CLASSIC / 合成订单 DEMO 进玩留 V 组手验。

### 标注

- **涉及热更程序集:** 是(PlayerInfoWindow / GameContext / MainMenuWindow 全在 HotFix GameLogic 程序集)。
- **需 Luban 重生成:** 否(纯表现层 + 接线,不碰配置表)。
- **需进 Play 模式手验:** V1 点击开窗、V4 改名指针闭环 + 重开保留、V5 三种关闭、V2 视觉对位 —— 指针点击 / 输入法改名 MCP 不能模拟,转 boss 桥跑。
- **模拟清单:** `PlayerInfoWindow` location 已被 `AssetRaw/UI` 组 AddressByFileName 收录、SimulateBuild 已重建;test 若 run_tests EditMode 不依赖清单(测试 bare new 窗口、不加载 prefab);若进 Play 截图,启动会自动 SimulateBuild。

### 需 boss 知会(范围开关,均按本轮默认推进,非阻塞,关单复核)

- **D2 生日是否真做 + 入存档**(本轮 UI 占位)——最值得一看:效果图有、数据层无。要真做须数据层加字段(PlayerInfo + MergeMetaSave + 保底 + 单测),属本轮范围外另开增量。
- D3 头像三态选择网格(本屏占位)、D4 等级槽/id 复制(本屏不补)——属设计 18 后续屏。
- 缺图占位(圆头像框/铅笔/下拉箭头/头像 Sprite)= 预期占位,美术补图后替子图/Sprite 即生效、节点与脚本不返工。

(上一任务 ui-atlas-packer 打表工具已于 2026-06-15 关单,归档:`pipeline/archive/2026-06-15-ui-atlas-packer/`)
