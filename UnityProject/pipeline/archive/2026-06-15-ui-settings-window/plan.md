# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:ui-settings-window 设置窗美术换皮(2026-06-15,常规模式·用户在场)

设计稿:`design-docs/23-settings-window-art.html`(已落,nav.js 已加入「表现层 / 换皮」组,侧边栏 + 首页卡片自动同步)。

兑现遗留 #24(settings 表现层)。本工程第一个美术驱动 UI 窗口,目的是打通整条换皮链路并成为后续界面模板。数据逻辑层(设计 19 `GameLogic.Settings`)已交付,本轮只做表现层 + 接线,不重写数据层。

### 交接区(交开发 → 测试)

**做什么(六块,详见设计稿对应章节):**
1. 切图寻址基础设施:22 张切图导入 `AssetRaw/UIRaw/Atlas/setting/` → 打成 `Atlas_settings.spriteatlasv2` → 收集器收录使可寻址 → `Image.SetSubSprite("Atlas_settings", 子图名)` 取图(§三)。
2. prefab 节点树:对位 `setting.png` 摆节点 + `m_` 前缀命名 + 1080×1920 锚点 + 每节点用哪张子图(§四)。
3. SettingsService 运行期持有者:新建 `GameLogic.GameContext` 单例统一持有无主数据(§五,推荐方案 B)。
4. 窗口脚本 `SettingsWindow : UIWindow` + `[Window(Top,"SettingsWindow",false)]`:ScriptGenerator 绑定 + onClick 接钮 + OnRefresh 读 SettingsService 刷开关态(§六)。
5. 每个按钮 / 开关的实做 vs 占位分流(§七):音效 / 清存档 / 协议隐私 OpenURL / 兑换码入口实做接线;社交 / 客服 / 更多游戏 / 语言 / 退登占位 + Toast「待建」+ TODO。
6. 打开入口(主菜单加按钮 → ShowUIAsync)+ 关闭(X + 遮罩点击 → CloseUI)(§八)。

**dev 改动清单:** 设计稿 §十(9 项新建 / 改 + 1 项不改)。代码全落 `GameScripts/HotFix`,资源落 `AssetRaw/`。

**勘察已核实的关键现状(写进设计稿,供 dev 免重复勘察):**
- 取子图 API `Image.SetSubSprite(location, spriteName)` 真实存在(`SetSpriteExtensions.cs:43`),内部 `YooAssets.GetAssetInfo(location)` + `LoadSubAssetsAsync<Sprite>(location)` + `GetSubAssetObject<Sprite>(spriteName)`;**location 必须是被收集器收录、可作 SubAssets 加载的资源**。
- 工程**从未实际调用过 SetSubSprite**,现有图集在 `AssetArt/Atlas/`(不在收集器),收集器只收 `AssetRaw/` 树。→ 图集寻址是本轮唯一带验证风险的环节(设计稿 §3.2 callout 给方案 A 推荐 + B 兜底,要求 dev 落地第一步先单点验证寻址跑通)。
- CanvasScaler 参考分辨率经场景 `main.unity:366` 覆写为 **1080×1920**(与美术基准一致),prefab 直接用真实锚点,不套 `BlockLayout`(750×1334)/ `UGuiFactory`。
- 既有窗口全是 code-built(UGuiFactory),无一个 prefab 绑定窗口——本轮是首个;prefab 根范式参 `TempleWindow.prefab`(RectTransform + Canvas + GraphicRaycaster)。
- **references vs 工程出入(写进设计稿 §六 warn,dev 别踩)**:`RegisterButtonClick` 在 tengine-dev references 示例里有,**本工程 UIModule 未实现**(grep 零命中),既有窗口一律用 `btn.onClick.AddListener`。本稿按工程实际写。
- SettingsService 当前无运行期持有者(只在模块文件 / 单测 new);AudioSink 须启动时接一次(设计稿 §五给 ProcedureLaunch 接线)。

**验收标准(设计稿 §九,test 逐条核对,拆两档):**
- 逻辑可单测(EditMode):C1-C2 编译 + 红线;H1-H3 GameContext 单例 + 持久化往返;W1-W2 窗口绑定路径 + 开关切换贯通数据层落盘。
- 需 Play / 人眼(手验遗留,boss 授权):V1 入口开 / X / 遮罩关;**V2 切图经 SetSubSprite 显示、对位接近 setting.png(本轮成败核心、链路打通标志)**;V3 音效切换持久化 + 真实实听;V4 不破坏 Classic/Merge 主玩法;V5 占位按钮有反馈不死按钮。

### decisions(有安全默认,已按默认在设计稿铺开,交 boss 关单复核)

- **B1(基础设施,提请 boss 复核)**:SettingsService 运行期持有者取**方案 B = 新建 GameContext 统一上下文单例**(薄、可逆,分层于玩法态单例 BlockGameState)。这是会被后续 player-info/item/mail/rank 依赖的基础设施决定;备选方案 A(只给 SettingsService 套单例,试验做最小)。两者对本窗验收等价。
- B2 清除存档默认实做带二次确认(dev 不便建确认窗则降级占位 + TODO)。
- B3 音效开关默认用 Toggle(切图缺静音态图则 color 区分 + TODO)。
- B4 音乐开关默认进本窗(效果图无音乐独立位则只做音效、音乐留 TODO,dev 读图核实)。
- B5 打开入口默认落主菜单 MainMenuWindow 加按钮(玩法 HUD 入口后续轮次)。
- B6 切图「子图名→功能位」最终映射由 dev 读图核实(设计稿 §二 callout 给推断,命名歧义以视觉为准)。
- 社交外链 / 协议 / 隐私 / 客服真实 URL 无来源 → 安全默认 = 占位 URL + Toast「待建」+ TODO(可逆、不抵触去变现方向),不停机;真实地址由产品后续提供替换常量。

### blockers(令流水线停机的方向问题):无

所有范围开关均有安全默认可走,无抵触 spec / 不可逆的方向问题,不入 blockers。
