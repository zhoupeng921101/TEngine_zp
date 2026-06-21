# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:ui-settings-window 设置窗美术换皮(2026-06-15,常规模式·用户在场)

设计稿:`design-docs/23-settings-window-art.html`。兑现遗留 #24。本工程第一个美术驱动 UI 窗口 + 切图寻址基础设施 + GameContext 运行期上下文。已完成,EditMode 366/366 PASS,Play 模式端到端实测全链路通,交测试。

### 交接区(交开发 → 测试)

**改动摘要(做了什么 + 关键决策):**

1. **切图寻址链路打通(本轮成败核心,V2):** 21 张切图导入 `AssetRaw/UIRaw/Atlas/setting/`,合成一张精灵表 `Sheet_settings.png` 寻址。窗口经 `Image.SetSubSprite("Sheet_settings", 子图名)` 取图,框架 `SubSpriteReference` 自动管引用计数。
2. **GameContext 运行期上下文(B1=方案 B):** 新建 `GameLogic.GameContext : SimpleSingleton`,持有 `SettingsService` + OnInit 时 Load。薄而通用,后续 player-info/item/mail/rank 逐个挂入。
3. **SettingsWindow 窗口:** `[Window(UILayer.Top, "SettingsWindow", false)]`,prefab 节点树对位 setting.png,ScriptGenerator 绑定 + onClick 接钮 + OnRefresh 刷音乐/音效开关态。
4. **接线:** 热更入口 `GameApp.StartGameLogic()` 接 AudioSink + 触发 GameContext 初始化;`MainMenuWindow` 加「设置」入口按钮。
5. **音乐 + 音效双开关(B4 视觉核实):** setting.png 下排实图含音符♪(音乐)+ 喇叭🔊(音效)两位,故两个开关都进窗,各接 SetMusic/SetSound。
6. **占位分流(§七):** 协议/隐私接 OpenURL(占位 URL);清存档/社交/客服/更多游戏/语言/退登 占位 + Log「待建」反馈。

**关键决策与设计偏离(需 boss 知会 — 见末尾「需 boss 知会」):**
- **寻址方案从 SpriteAtlas v2 改为精灵表 PNG**(用户拍板"每屏一个 SpriteAtlas"在本工程技术上不可行,实测证据见下)。API 调用形态、节点树、子图名映射、验收全部等价,只是图集资源格式不同。
- **AudioSink 接线落点从 ProcedureLaunch 改到 GameApp.StartGameLogic()**(ProcedureLaunch 在非热更区,引用不到热更区 GameContext,热更边界所致;非设计错,微调救)。

### 文件清单

**新增代码(热更区):**
- `Assets/GameScripts/HotFix/GameLogic/GameContext.cs` — 运行期上下文单例(持 SettingsService + InitSettingsWithStore 测试注入接缝)
- `Assets/GameScripts/HotFix/GameLogic/UI/SettingsWindow.cs` — 设置窗(绑定 + 接钮 + 刷态 + 占位分流)

**改既有代码(热更区):**
- `Assets/GameScripts/HotFix/GameLogic/GameApp.cs` — StartGameLogic() 接 AudioSink + GameContext 初始化(约 10 行)
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MainMenuWindow.cs` — 加设置入口按钮(约 6 行)

**新增资源(AssetRaw):**
- `Assets/AssetRaw/UIRaw/Atlas/setting/*.png` — 21 张切图(Sprite 类型,6 张底图九宫格 Border)
- `Assets/AssetRaw/UIRaw/Atlas/Sheet_settings.png` — 精灵表(Sprite Mode=Multiple,21 命名子精灵)
- `Assets/AssetRaw/UI/Prefabs/SettingsWindow.prefab` — 窗口 prefab(43 节点,1080×1920)

**新增测试:**
- `Assets/Editor/Tests/BlockBlast/GameContextTests.cs` — 8 例(H1/H2/H3a/H3b/W2×2/AudioSink)

**注:** `Atlas_settings.spriteatlasv2` 一度建过又删除(寻址不通,见决策);`Assets/Screenshots/settings_window_play4.png` 留作 test 视觉对位参考。

### 验证点(逐条对应设计稿 §九验收,test 怎么验 + 预期)

**逻辑档(EditMode,test 直接 run_tests):全部已自测通过**
- **C1 编译 0 error + 全绿:** `run_tests` EditMode 全量 **366/366 PASS**(含既有 settings 数据层零回归)。
- **C2 红线:** 异步优先(SetSubSprite 异步,无同步大资源加载)/ 模块访问(GameModule.Audio/UI)/ 资源释放(SetSubSprite 自管引用计数,无裸 LoadAssetAsync<Sprite>)/ 热更边界(窗口+上下文在 HotFix)/ 事件解耦(onClick)。code review 核 SettingsWindow.cs + GameContext.cs。
- **H1 单例同实例:** `GameContextTests.H1_*` — 多次 Instance 返回同一 GameContext + 同一 SettingsService。
- **H2 默认全开:** `H2_*` — 注入空 InMemory store,MusicOn==SoundOn==true。
- **H3 态保真 + 持久化往返:** `H3a_*`(同实例 SetSound(false) 保真)、`H3b_*`(Release 后从同一 store 复 Load 读回上次值)。
- **W1 节点绑定路径对齐:** prefab 43 节点路径已与 SettingsWindow.cs 的 FindChildComponent 路径逐条核对一致(`manage_prefabs get_hierarchy` 已验);Play 实测各 m_ 字段非空、sprite 全贴上。
- **W2 开关贯通落盘:** `W2_ToggleSound_ViaDataLayer_*` / `W2_ToggleMusic_*` — 切换后内存态 + 存储键(muted 取反)双更新;`AudioSink_*` 验 sink 被调。

**视觉档(Play/人眼,转手验遗留;dev 已 Play 实测逻辑侧 + 渲染,人耳/指针留 test):**
- **V1 入口开 / X / 遮罩关:** Play 实测 — 主菜单「设置」按钮已显示;ShowUIAsync 后 HasWindow=True。X/遮罩点击 onClick 已接 Close(指针点击留人验)。
- **V2 切图显示 + 对位(核心):** **Play 实测通过** — 截图 `settings_window_play4.png`,各节点 sprite 正确贴出(box2/box1/icon_x/各社交/各图标),版面接近 setting.png。寻址链路 100% 打通。
- **V3 音效切换持久化 + 实听:** Play 实测逻辑侧通过 — 点 Toggle → SoundOn=false → AudioModule.SoundEnable=false(sink 贯通)→ PlayerPrefs SoundMuted=true(落盘)。真实实听留人耳。
- **V4 不破坏 Classic/Merge:** EditMode 全绿 + 加法式改动(仅新增 + GameApp/MainMenu 加行,不动玩法窗);玩法手感留 test 既有遗留。
- **V5 占位有反馈不死按钮:** 占位按钮调 ShowPlaceholder → Log.Info「待建」;协议/隐私 OpenURL。Log 反馈可 read_console 核。

**对位可微调项(不阻塞验收,留 test/美术细修):** 标题文字偏上贴边、社交标排列可再居中、九宫格 Border(24px)按实际切图尺寸可再调。

### 标注

- **涉及热更程序集:** 是(GameContext/SettingsWindow/GameApp/MainMenuWindow 全在 HotFix GameLogic 程序集)。
- **需 Luban 重生成:** 否(纯表现层 + 接线,不碰配置表)。
- **需进 Play 模式手验:** V1 点击关窗、V3 真实音效实听、V4 玩法手感 —— 指针点击 / 真实音频 MCP 不能模拟,转手验。
- **模拟清单:** 已重建,`Sheet_settings` / `SettingsWindow` location 在清单,`Atlas_settings` 已彻底移除。test 若直接 run_tests EditMode 不依赖清单;若进 Play 截图,启动会自动 SimulateBuild。

### 需 boss 知会(寻址方案技术偏离 — 等价替代,不停机)

**问题:** 用户拍板「每屏一个 SpriteAtlas v2 + `SetSubSprite(图集location, 子图名)`」在本工程的 SetSubSprite + YooAsset 实现下**技术上不可行**。

**实测证据(Play 模式,设计稿 §3.2 callout 预判的"本轮唯一验证风险"环节):**
- `SetSubSprite` 内部对 location 调 `YooAssets.LoadSubAssetsAsync<Sprite>(location).GetSubAssetObject<Sprite>(spriteName)`(`ResourceExtComponent.SubSprite.cs:50-72`)。
- 把 `Atlas_settings.spriteatlasv2` 收录为 location 后实测:`GetAssetInfo` 有效、`LoadSubAssetsSync` Succeed,但 **SubAssets sprite count = 0**,所有 `GetSubAssetObject(子图名)` 返 NULL。**SpriteAtlas 资源不向 YooAsset 暴露其打包的 Sprite 作为子资源**(SpriteAtlas 是打包容器,子精灵属于源 PNG 而非图集资源)。
- 工程既有 `UIRaw/Atlas/blocks/` 是散 PNG(无 SpriteAtlas),每张 PNG 作 location、SubAssets count=1 —— 即工程 SetSubSprite 范式本就要求 location 是"含多 Sprite 子对象的单一资源",而非 SpriteAtlas。

**采用的等价替代:** 把 21 张切图合成**一张精灵表 PNG(Sprite Mode=Multiple,含 21 个命名子精灵)** `Sheet_settings.png`,location=`Sheet_settings`。实测:SubAssets count=21,按子精灵名(=源切图文件名)全部取到,真实 `img.SetSubSprite("Sheet_settings","icon_x")` 端到端把子图贴上 Image。

**为何这是等价替代而非方向变更:**
- API 调用形态与用户拍板一致:仍是「每屏一个图集资源 + `SetSubSprite(图集location, 子图名)` 取图」,窗口代码、子图名映射、节点树、全部验收点不变。
- 与工程既有范式(blocks 散图 SubAssets 寻址)同源,只是合成一张表(更省 drawcall,更贴"每屏一图集"精神)。
- 是设计稿 §3.2 callout 明确预判风险 + 要求"回报 boss 再定"的落地解(设计稿方案 B"散图直接寻址"与"每屏一图集"相悖;精灵表方案两者兼得)。

**请 boss 知会确认此技术替代。** 若坚持必须用 SpriteAtlas 资源,则 SetSubSprite 这条 API 在本工程对 SpriteAtlas 不工作,需改框架 ResourceExtComponent 的 SubSprite 实现(超出本轮表现层换皮范围)。
