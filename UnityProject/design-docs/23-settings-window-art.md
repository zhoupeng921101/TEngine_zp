<style>
  /* 本篇专用：schema / 字段表 / 节点树 / 分档代入小样式（沿用 15–22 口径） */
  pre.code { margin:8px 0; padding:12px; background:#0d1622; border:1px solid var(--border); border-radius:8px; overflow:auto; font-size:13px; line-height:1.55; }
  table.tight td, table.tight th { padding:6px 10px; }
  .pill-new { display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(91,214,160,.16); color:#5bd6a0; margin-left:6px; }
  .pill-cur { display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(108,140,255,.16); color:#6c8cff; margin-left:6px; }
  .pill-no  { display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(255,122,138,.16); color:#ff7a8a; margin-left:6px; }
  .yes { color:#5bd6a0; font-weight:bold; }
  .no  { color:#ff7a8a; font-weight:bold; }
  td.mono, code.mono { font-family:ui-monospace,Consolas,monospace; }
  .tree { font-family:ui-monospace,Consolas,monospace; font-size:12.5px; line-height:1.6; white-space:pre; background:#0d1622; border:1px solid var(--border); border-radius:8px; padding:12px; overflow:auto; }
</style>

# 设置窗美术换皮 · 表现层

本工程**第一个美术驱动的 UI 窗口**:把效果图 `setting.png` 换皮成可运行的 `SettingsWindow`。目的有二——① 兑现[设计 19 设置系统](#19-settings-system)遗留的表现层(boss 遗留 #24);② <mark>打通「切图 → 每屏一个 SpriteAtlas → prefab 摆节点 → FindChildComponent 绑定 → \[Window\] 加载 → SetSubSprite 取图 → 热更」整条链路,成为后续所有界面换皮的模板</mark>。数据逻辑层(音频开关 + 持久化 + 信息 getter)[设计 19](#19-settings-system) 已交付并经单测,本次换皮**只做表现层 + 接线**,不重写数据层。

> [!WARNING]
> **读前必看 · 与工程现状的关系(单一事实源 = 代码)**
>
> 五条边界先钉死,防把「换皮一个窗」做歪成「重写设置系统 / 改框架 / 自造一套寻址」:
>
> - **数据层不动,只接线。**`GameLogic.Settings`(`SettingsService` / `AudioSettings` / `SettingsInfo` / `SettingsText` / `SettingsLinks`)已实装并经 294 例 EditMode 单测覆盖([设计 19](#19-settings-system),归档 `pipeline/archive/2026-06-14-settings-system/`)。本次换皮**不**改这些类的逻辑;窗口只**调用**它们(读开关态、切换、读版本号 / 用户 ID)。
> - **音频开关已能持久化,缺的只是「运行期持有者」。**框架键 `Setting.MusicMuted` / `SoundMuted` 经 `SettingsService` 落盘、启动流程 `ProcedureLaunch.InitSoundSettings` 加载([设计 19 §2.2](#19-settings-system::additive)),但 `SettingsService` 当前**没有运行期单例持有者**(只在模块文件 / 单测里 `new`)。本次换皮要给它一个持有者([§五](#23-settings-window-art::holder)),否则窗口每次开都 `new` 一个、改的内存态无人持有。
> - **切图寻址 = 每屏一个 SpriteAtlas v2 + `Image.SetSubSprite(图集 location, 子图名)`。**(用户拍板)**不**用 `AddressByFileName` 平铺单图——这套塔罗素材跨界面有重名(`x` / `icon_x` / `setting` 等),平铺会被导入器去重并报错。图集落点 + 建法 + 收集器配置见 [§三](#23-settings-window-art::atlas)。
> - **绑定 = 传统 FindChildComponent + prefab 节点 `m_` 前缀;窗口继承 `UIWindow` + `[Window]`。**(用户拍板)**不**用 UIBindComponent。前缀规则见 tengine-dev 的 `naming-rules` 参考文档(本篇 [§四节点树](#23-settings-window-art::tree) 逐节点给名)。弹窗 `fullScreen:false`、层级 `Top`、prefab 根挂 `Canvas`([§六](#23-settings-window-art::window))。
> - **UI 业务代码在热更区,资源走 YooAsset。**窗口脚本落 `GameScripts/HotFix/GameLogic/UI/`(热更);切图 / 图集 / prefab 是资源,落 `AssetRaw/` 下被收集器收。改动全在加法侧,不破坏 Classic / Merge 主玩法([§九 验收 R](#23-settings-window-art::accept))。

> [!NOTE]
> **立项信息**
>
> | 项 | 内容 |
> | --- | --- |
> | **类型** | 表现层换皮 · 首个美术驱动 UI 窗口 + 寻址基础设施 出设计稿 + 验收标准,交开发落地。兑现遗留 #24。 |
> | **设计基线(经勘察核实的真实符号 / 现状)** | **数据层(已实装,只调用)**:`GameLogic.Settings.SettingsService`(`Load`/`SetMusic(bool)`/`SetSound(bool)`/`ToggleMusic()`/`ToggleSound()`/`Audio.MusicOn`/`Audio.SoundOn`/`AudioSink`/`static ToggleTipTextId`);`SettingsInfo.Version()`/`UserId(PlayerInfo)`;`SettingsLinks.UserAgreementUrl`/`PrivacyPolicyUrl`/`ContactSupport`;`SettingsText.MusicOn/Off/SoundOn/Off`(190001–4)。 **UI 框架**:`UIWindow` + `[Window(UILayer, location, fullScreen, hideTimeToClose)]`(`WindowAttribute.cs:53`);生命周期 `ScriptGenerator → RegisterEvent → OnCreate → OnRefresh`(`UIBase.cs`);绑定 `FindChildComponent<T>(path)` / `FindChild(path)`(`UIBase.cs:253/243`);事件 `AddUIEvent`(`UIBase.cs:299`);打开 `GameModule.UI.ShowUIAsync<T>()` / 关闭 `CloseUI<T>()`。 **取子图 API**:`Image.SetSubSprite(string location, string spriteName, bool setNativeSize=false)`(`SetSpriteExtensions.cs:43`);内部 `YooAssets.GetAssetInfo(location)` + `LoadSubAssetsAsync<Sprite>(location)` + `GetSubAssetObject<Sprite>(spriteName)`(`ResourceExtComponent.SubSprite.cs:50`),**location 必须指向一个被收集器收录、可作 SubAssets 加载的 SpriteAtlas 资源**。引用计数由框架 `SubSpriteReference` 自动管,无需手动释放。 **收集器**:`AssetBundleCollectorSetting.asset` 现有组 `UI`(`Assets/AssetRaw/UI`,`AddressByFileName`,`PackSeparately`)放 prefab;`UIRaw/Atlas`(`Assets/AssetRaw/UIRaw/Atlas`,`AddressByFileName`,`PackDirectory`)放图集源。 **分辨率**:UIRoot CanvasScaler 参考分辨率经场景 `main.unity:366` 覆写为 **1080×1920**(与美术基准一致);prefab 直接用 1080×1920 锚点坐标,**不**套用 `BlockLayout` 那套 750×1334 私有坐标系 / `UGuiFactory`。 |
> | **方向约束** | 离线还原 · **去变现**:窗口不含充值 / 内购 / 快捷登录(效果图也无)。社交外链 / 客服 / 协议 URL 按 [设计 19](#19-settings-system) 的「实做 / 占位 / 不做」分档([§七](#23-settings-window-art::dispatch))。加法式:新建窗口 + 图集 + prefab + 持有者,不改框架、不改数据层逻辑、不动既有玩法窗口。 |
> | **影响范围** | **新增资源**:切图 22 张(`AssetRaw/UIRaw/Atlas/setting/`)+ `Atlas_settings.spriteatlasv2` + `SettingsWindow.prefab`(`AssetRaw/UI/Prefabs/`); **新增代码(热更区)**:`SettingsWindow.cs`(窗口脚本)+ `GameContext`(运行期上下文单例,持有 `SettingsService` 等无主数据,[§五](#23-settings-window-art::holder)); **改既有(最小)**:收集器加一个 `Atlas` 子收集路径(或复用 UIRaw/Atlas);主界面 / HUD 留设置入口按钮接 `ShowUIAsync<SettingsWindow>`(入口位置 [§八](#23-settings-window-art::entry));`ProcedureLaunch` 启动时把 `SettingsService.AudioSink` 接到音频模块([§五](#23-settings-window-art::holder),一处约 4 行)。 **不改**:`GameLogic.Settings` 各类逻辑、框架 UI / 资源代码、Classic / Merge 玩法窗口、数据层单测。 |
> | **关键约束(继承现状)** | 窗口逻辑可被反射驱动单测(EditMode 编译 + 反射调 `ScriptGenerator`/字段),但**真实视觉对位 / 指针点击 / 真实音频实听**须 Play 模式人眼 + 手验(MCP 不能模拟指针拖拽 / 点击,但能 `ShowUIAsync` + 截图核渲染)。验收按「逻辑可单测」与「需 Play / 人眼」两档拆开([§九](#23-settings-window-art::accept))。 |

## 一、做什么与为什么 {#what}

现状:游戏所有 UI 窗口都是<mark>纯代码搭建(<code>UGuiFactory</code> + glyph + 纯色)</mark>,零美术——主菜单 / 玩法窗 / 神庙窗都是占位方块。设置数据层([设计 19](#19-settings-system))已交付但**没有任何窗口能让玩家触达**:玩家无处关音乐 / 看版本号。同时整个工程<mark>从未走通「美术切图 → prefab → 运行期换皮」的链路</mark>——没有一个 prefab 绑定的窗口、没有一次 `SetSubSprite` 调用、没有一个为 UI 投放建的 SpriteAtlas。

本次换皮以「设置窗」为第一块试验田,一次性把这条链路打通并固化成模板。之所以挑设置窗当第一个:① 它的数据层已就绪(只缺壳),改动面可控;② 效果图结构清晰(标题 + 关闭 + 一行图标 + 4 长条按钮 + 一排图标按钮),节点类型齐全(`Image` / `Button` / `Toggle` / 文本),足以覆盖大多数后续界面会用到的节点种类;③ 它是模态弹窗,能顺带验证「非全屏弹窗 + 遮罩 + Top 层」这套后续到处要用的范式。资源加载红线见 tengine-dev 的 `resource-api` 参考文档(Sprite 用 `SetSprite` / `SetSubSprite`,禁 `LoadAssetAsync<Sprite>`)。

| # | 本次换皮要打通 / 交付的 | 落法 | 性质 |
| --- | --- | --- | --- |
| 1 | 切图寻址基础设施(每屏一图集 + SetSubSprite) | 22 张切图导入 → 打成 `Atlas_settings` → 收集器收录使可寻址 → 窗口 `SetSubSprite("Atlas_settings", "button")` 取图([§三](#23-settings-window-art::atlas)) | <span class="pill-new">新基础设施 · 模板</span> |
| 2 | prefab 节点树(对位 setting.png) | 按效果图层级摆节点 + `m_` 前缀命名 + 1080×1920 锚点 + 标注每节点用哪张子图([§四](#23-settings-window-art::tree)) | <span class="pill-new">新 prefab</span> |
| 3 | SettingsService 运行期持有者 | 新建 `GameContext` 单例统一持有无主数据([§五](#23-settings-window-art::holder)) | <span class="pill-new">新基础设施</span> |
| 4 | 窗口脚本(绑定 / 接钮 / 刷开关态) | `SettingsWindow : UIWindow` + `[Window]`,`ScriptGenerator` 绑定 + 接 `SettingsService`([§六](#23-settings-window-art::window)) | <span class="pill-new">新窗口</span> |
| 5 | 每个按钮 / 开关的实做 vs 占位 | 音效 / 兑换码实做接服务;社交 / 客服 / 协议 / 语言 / 退登按分档占位([§七](#23-settings-window-art::dispatch)) | <span class="pill-cur">接线 + 占位</span> |
| 6 | 打开入口 + 关闭(X + 遮罩) | 主界面设置图标 → `ShowUIAsync`;X 按钮 + 遮罩点击 → `CloseUI`([§八](#23-settings-window-art::entry)) | <span class="pill-new">新接线</span> |

**不做(本次换皮明确排除):**<span class="pill-no">改数据层逻辑</span>(只调用);<span class="pill-no">把其它已建窗口也换皮</span>(本次换皮只设置窗,模板沉淀后另开轮次);<span class="pill-no">真实社交 / 客服 / 协议 URL</span>(无真实地址,占位);<span class="pill-no">语言切换真实多语言表</span>(无多语言系统,占位);<span class="pill-no">退出登录真实账号系统</span>(离线无账号,占位);<span class="pill-no">多语言文本表真实查表</span>(提示文案沿用 textId 占位,同 19);<span class="pill-no">音量滑条</span>(数据层只给开 / 关,效果图也无)。

## 二、效果图拆解(对位基准) {#effigy}

美术基准 `setting.png`(1080×1920 竖屏,扁平 PNG)。盖在游戏 HUD 上的**模态弹窗**:半透明深色遮罩 + 居中木牌面板。自上而下五块:

| 区块 | 效果图内容 | 切图(`设置\` 文件夹) | 节点类型 |
| --- | --- | --- | --- |
| ① 遮罩 | 盖住整屏的半透明深色背景(透出底层 HUD) | 无(纯色 `Image`,alpha≈0.6) | `Image` + `Button`(点击关窗) |
| ② 标题木牌 | 顶部木牌,牌面写 "SETTING" | `setting`(木牌底)+ 文字(美术已烤进图 / 或单独文本) | `Image` |
| ③ 关闭按钮 | 右上角圆形 × 按钮 | `icon_x`(圆底)+ `x` 或 `close`(× 号) | `Button` |
| ④ 上面板(社交 + 4 长条) | 大木板:左上「关注我们」+ 5 个社交圆标一行;下方 4 个长条按钮(联系我们 / 更多游戏 / 语言切换 / 退出登录) | 底板 `base_plate` / `base_plate2`;社交 `facebook` `twitter` `x` `youtube` `instagram`;长条底 `button`;条内图标 `chat`(联系)`game`(更多游戏)`language`(语言)`exit`(退出) | `Image` ×N + `Button` ×9 |
| ⑤ 下面板(5 图标按钮) | 小木板:一排 5 个图标按钮(清除存档 / 音效设置 / 通知设置 / 帮助设置 / 隐私设置) | 底板 `base_plate3` / `box1` / `box2`;图标 `clear`(清存档)`Player_music` 或 `Volume_up`(音效)`printer` 或 `chat`(通知)`help`(帮助)`setting`(隐私 / 兜底) | `Button` ×5(其中音效用 `Toggle` 或双态 `Image`) |

> [!NOTE]
> **切图与功能的对应须 dev 读图二次核实**
>
> 22 张切图的命名(`chat` / `game` / `printer` / `Volume_up` 等)与效果图上 9 个功能位的对应,上表是<mark>按图名推断</mark>。dev 落地时**对照 `setting.png` 与切图缩略图逐一核实**(哪张是哪个按钮的面),把最终「子图名 → 功能位」映射写进 prefab 节点 + 窗口脚本注释。子图名取**切图文件名(不含扩展名)**——`SetSubSprite` 的 `spriteName` 即图集内每张子图的精灵名,等于源 PNG 文件名。

## 三、美术资产接入(每屏一图集 + SetSubSprite) {#atlas}

### 3.1 切图导入落点 {#atlas-import}

把 `C:\Users\pc\Downloads\塔罗\塔罗\设置\` 整个文件夹的 22 张 PNG 导入工程,落点**与现有 UIRaw 图集源体例一致**:

```text
Assets/AssetRaw/UIRaw/Atlas/setting/        ← 新建子目录，22 张切图放这里
    base_plate.png  base_plate2.png  base_plate3.png  box1.png  box2.png
    button.png  chat.png  clear.png  exit.png  facebook.png  game.png
    help.png  icon_x.png  instagram.png  language.png  Player_music.png
    printer.png  setting.png  twitter.png  Volume_up.png  x.png  youtube.png
```

**为什么放这里**:现有图集源(如 `Atlas_misc` 打的 `misc/` PNG)就在 `Assets/AssetRaw/UIRaw/Atlas/` 下,该路径已被收集器组 `UIRaw/Atlas` 收录(`AssetBundleCollectorSetting.asset`,`AddressByFileName` + `PackDirectory`)。每张切图导入设置:`Texture Type = Sprite (2D and UI)`、`Sprite Mode = Single`、`Mesh Type = Full Rect`(UI 用)、九宫格按钮底图(`button` / `base_plate*`)设 `Border` 便于拉伸。

### 3.2 SpriteAtlas v2 建法 {#atlas-build}

建一个图集 `Atlas_settings.spriteatlasv2`,落点**与现有图集一致**(`Assets/AssetArt/Atlas/`),把上面整个 `setting/` 目录作为 `Packable` 加进去(整目录加,不逐张加):

```text
Assets/AssetArt/Atlas/Atlas_settings.spriteatlasv2
  Packables: [ Assets/AssetRaw/UIRaw/Atlas/setting (folder) ]
  关键导入设置（对齐现有 Atlas_misc.spriteatlasv2.meta）：
    Include in Build (bindAsDefault) = true   ← 使运行期可解析子图
    Allow Rotation / Tight Packing = 按现有图集口径
    Max Texture Size = 2048, 压缩按现有平台设置
```

> [!WARNING]
> **关键:图集本身必须可被 YooAsset 当 SubAssets 加载(这是 SetSubSprite 能跑通的前提)**
>
> `SetSubSprite(location, spriteName)` 内部对 `location` 调 `YooAssets.GetAssetInfo(location)` + `LoadSubAssetsAsync<Sprite>(location)`(`ResourceExtComponent.SubSprite.cs:52,62`)。所以 `location` 必须是一个**被收集器收录、可按子资源枚举出各精灵的资源**。现有 `AssetArt/Atlas/` 目录**不在**收集器任何组里(已勘察:收集器只收 `AssetRaw/` 树),工程也**从未实际调用过 `SetSubSprite`**——所以「图集如何被寻址」是本次换皮要打通并验证的核心未知点。dev 须二选一并**在 Play 模式实测 `GetAssetInfo` 不返回 invalid、子图能取到**:
>
> - **(方案 A，推荐先试)** 把 `Atlas_settings.spriteatlasv2` 放进 `Assets/AssetRaw/UIRaw/Atlas/`(与切图源同树),使其被现有 `UIRaw/Atlas` 组按 `AddressByFileName` 收录,`location = "Atlas_settings"`。切图源 PNG 仍在子目录、由图集打包,运行期对图集资源做 `LoadSubAssetsAsync` 取各精灵。
> - **(方案 B，A 不通时)** 不依赖 SpriteAtlas 资源本身,而是**对收录了散图的目录 / 散图直接做子资源寻址**——但这与用户拍板「每屏一个图集」相悖,仅作 A 不通时的兜底,**须回报 boss 再定**。
>
> 这是本次换皮唯一带验证风险的环节(其余链路均有同类先例)。**dev 落地第一步就先验证寻址跑通(取到任意一张子图显示出来)**,再铺满整窗——避免摆完整个 prefab 才发现寻址不通。

### 3.3 运行期取图写法 {#atlas-use}

窗口里给每个 `Image` 贴图集子图。`spriteName` = 切图文件名(不含扩展名):

```text
// 窗口字段（ScriptGenerator 绑定）
private Image _imgClose;
private Image _imgPanelTop;
// ...
// 贴图（OnCreate 里一次性贴静态图）。SetSubSprite 内置引用计数 + SubSpriteReference 自动释放，无需手动 Unload。
private const string Atlas = "Atlas_settings";   // 图集 location（§3.2 方案 A）
_imgClose.SetSubSprite(Atlas, "icon_x");
_imgPanelTop.SetSubSprite(Atlas, "base_plate");
_imgBtnContact.SetSubSprite(Atlas, "button");
_imgIconContact.SetSubSprite(Atlas, "chat");
// ...逐节点贴，子图名以 dev 读图核实后的「子图名→功能位」映射为准（§二 callout）
```

**不写法**:<span class="no">不</span>用 `LoadAssetAsync<Sprite>` 加载图(无缓存池 + 需手动释放,违 `resource-api` 红线);<span class="no">不</span>用 `AddressByFileName` 平铺单图 + `SetSprite(单图location)`(跨界面重名会被导入器删,用户拍板排除)。整窗静态图在 `OnCreate` 贴一次即可(弹窗内容不随数据变,开关态用文本 / 切双态图刷,见 [§六](#23-settings-window-art::window))。

### 3.4 收集器配置确认 {#atlas-collector}

prefab 与图集分别被两个现有组收录,dev 落地后在收集器界面确认(或补一条收集路径):

| 资源 | 落点 | 收集组 | location |
| --- | --- | --- | --- |
| `SettingsWindow.prefab` | `AssetRaw/UI/Prefabs/` | `UI`(`AddressByFileName` / `PackSeparately`,现有) | `SettingsWindow` |
| `Atlas_settings.spriteatlasv2` | `AssetRaw/UIRaw/Atlas/`(§3.2 方案 A) | `UIRaw/Atlas`(`AddressByFileName` / `PackDirectory`,现有) | `Atlas_settings` |
| 切图 22 张 | `AssetRaw/UIRaw/Atlas/setting/` | 同上(由图集打包,运行期不单独寻址) | —(经图集子资源取) |

即:`[Window(..., location:"SettingsWindow")]` 对应 prefab location,`SetSubSprite("Atlas_settings", ...)` 对应图集 location,两者都靠 `AddressByFileName` = 文件名。

## 四、prefab 节点树(对位 setting.png + m_ 前缀命名) {#tree}

根节点照既有窗口 prefab 范式(`TempleWindow.prefab`):根挂 `RectTransform`(stretch 锚点 0,0→1,1)+ `Canvas`(`RenderMode` 同 UIRoot)+ `GraphicRaycaster`。坐标系 = 1080×1920 参考分辨率(场景 CanvasScaler 已设此值,<mark>直接用真实锚点 / 像素,不套 750 私有系</mark>)。下方坐标为对位描述(精确像素 dev 摆图时对着 `setting.png` 微调)。`m_` 前缀决定 `FindChildComponent` 绑定类型(前缀表见 tengine-dev 的 `naming-rules` 参考文档)。

```text
SettingsWindow                         (根: RectTransform 全屏 stretch + Canvas + GraphicRaycaster)
├─ m_btn_Mask                          Button  全屏遮罩(Image alpha≈0.6 深色; 点击关窗); 锚点全屏 stretch
└─ Root                                RectTransform 居中容器(锚点中心, 承载所有可见内容)
   ├─ m_btn_Close                      Button  右上角圆形关闭; 子图 icon_x(+ x); 锚点(顶部右)
   ├─ PanelTop                         RectTransform 上面板
   │  ├─ m_img_PanelTopBg              Image   底板; 子图 base_plate
   │  ├─ m_text_Title                  Text    "SETTING"(若木牌已烤字则此节点删, 用 m_img_Title 贴 setting 木牌)
   │  ├─ SocialRow                     RectTransform 关注我们一行
   │  │  ├─ m_text_FollowUs            Text    "关注我们"
   │  │  ├─ m_btn_Facebook             Button  子图 facebook
   │  │  ├─ m_btn_Twitter              Button  子图 twitter
   │  │  ├─ m_btn_X                    Button  子图 x
   │  │  ├─ m_btn_Youtube             Button  子图 youtube
   │  │  └─ m_btn_Instagram           Button  子图 instagram
   │  ├─ m_btn_Contact                 Button  长条"联系我们"; 底 button + 图标 chat
   │  ├─ m_btn_MoreGames               Button  长条"更多游戏"; 底 button + 图标 game
   │  ├─ m_btn_Language                Button  长条"语言切换"; 底 button + 图标 language
   │  └─ m_btn_Logout                  Button  长条"退出登录"; 底 button + 图标 exit
   └─ PanelBottom                      RectTransform 下面板
      ├─ m_img_PanelBottomBg           Image   底板; 子图 base_plate3(或 box1/box2)
      ├─ m_btn_ClearSave               Button  图标按钮"清除存档"; 子图 clear
      ├─ m_toggle_Sound                Toggle  音效开关; 子图 Volume_up / Player_music(双态)
      ├─ m_btn_Notify                  Button  图标按钮"通知设置"; 子图 printer(占位)
      ├─ m_btn_Help                    Button  图标按钮"帮助设置"; 子图 help
      └─ m_btn_Privacy                 Button  图标按钮"隐私设置"; 子图 setting(兜底)
```

> [!NOTE]
> **静态节点 vs 动态节点**
>
> 本窗**全是静态节点**(prefab 直接摆好,无列表 / 无运行期增删)——这是它适合当「第一个换皮试验」的原因之一,不涉及 `AdjustIconNum` / Widget 列表复用。唯一会运行期变的是**音效开关态**(`m_toggle_Sound` 的勾选 / 图标),由 `OnRefresh` 读 `SettingsService.Audio.SoundOn` 刷新(<a href="#23-settings-window-art::window">§六</a>)。

> [!NOTE]
> **音效开关用 Toggle 还是双态 Image?**
>
> 效果图下排「音效设置」是一个图标按钮,不是标准 Toggle 外观。两种实现:**(默认推荐)** 用 `m_toggle_Sound`(`Toggle`),`onValueChanged` 接 `SettingsService.SetSound`,勾选态用图标颜色 / 替换子图体现(开 `Volume_up`、关一个带斜杠的静音图——切图若无静音版则降级为开图 + 半透明)。**(备选)** 用 `m_btn_Sound`(`Button`)+ 点击 `ToggleSound()` + 自己刷图标。两者验收等价(都经 `SettingsService` 持久化),Toggle 更贴框架范式,取 Toggle 为默认。dev 落地时若切图缺静音态图,在脚本里以 `color` 区分开 / 关并留 TODO 待美术补静音图。

## 五、SettingsService 运行期持有者(基础设施决定) {#holder}

这是本次换皮影响最深远的一处设计——它不只服务设置窗,而是给<mark>所有「已建数据层但无运行期持有者」的无主系统</mark>(settings / player-info / item / redeem / mail / rank)定一个统一归宿。慎重对待。

### 5.1 问题 {#holder-problem}

[设计 19](#19-settings-system) 的 `SettingsService` 是个普通类(非单例):构造要传 `ISettingsStore`,改的开关态存在实例字段里。当前它只在单测里 `new`。若设置窗每次 `OnCreate` 都 `new SettingsService(...)`,则:① 每次开窗都重新 `Load`(可接受,但浪费);② <mark>窗口关掉后实例被回收,运行期没有任何地方持有「当前会话的设置态」</mark>——别处(如 HUD 静音图标)想读当前音效开关只能再 `new + Load`,逻辑分散且易漂移。同样的「数据层已建、运行期无人持有」问题,[player-info](#18-player-info) / item / mail / rank 都有(见各自遗留 #22/#19/#26/#27)。

### 5.2 三个候选 {#holder-options}

| 方案 | 做法 | 利 | 弊 |
| --- | --- | --- | --- |
| **A. 各系统各自单例** | 给 `SettingsService` 套 `SimpleSingleton`,player-info / item 等也各套各的 | 改动小、就近 | 单例散落、各系统初始化时机 / 接存档各写一遍;<mark>无统一接缝,N 个系统 N 套样板</mark> |
| **B. 统一运行期上下文单例 `GameContext`(推荐)** | 新建一个 `GameContext : SimpleSingleton<GameContext>`,作为所有无主数据层服务的运行期持有者 + 统一初始化 + 统一接存档接缝。本次换皮先只持有 `SettingsService`,后续系统逐个挂进来 | 无主系统有统一归宿;初始化 / 存档接缝写一次;窗口只 `GameContext.Instance.Settings` 取;与既有 `BlockGameState`(玩法态单例)分层清晰(玩法态 vs 通用服务) | 引入一个新基础设施类(但很薄) |
| **C. 并入 `BlockGameState`** | 把 `SettingsService` 等挂到既有玩法态单例 `BlockGameState` 上 | 不新增类 | <mark>职责混淆</mark>:`BlockGameState` 是 Classic/Merge 玩法态(棋盘 / 得分 / 悔棋),设置 / 玩家信息是**通用层**,塞进去会让玩法单例越来越胖、与玩法无关的东西也随它 Reset |

### 5.3 推荐:方案 B(GameContext)+ 理由 {#holder-rec}

取 **B**。理由:① 这批无主系统(settings / player-info / item / redeem / mail / rank)是<mark>同一类问题</mark>——「通用数据层服务,需一个运行期持有者 + 一次初始化 + 一处接存档」。按 CLAUDE.md「同类摩擦第二次出现就在源头修」,与其每个系统各打一个单例补丁(方案 A),不如一次性给它们一个统一归宿;② 与既有 `BlockGameState` 自然分层:玩法态归 `BlockGameState`(随开局 Reset),通用服务归 `GameContext`(随会话长存),职责不混(避开方案 C 的弊);③ 薄——本次换皮 `GameContext` 只需持有 `SettingsService` 一个成员 + 一个 `Init()`,后续系统挂进来时才长大,不投机性预建。

```text
namespace GameLogic   // 通用层，与 BlockBlast 玩法解耦
{
    /// <summary>运行期通用服务上下文(单例)。持有「数据层已建、需运行期持有者」的无主系统服务，
    /// 统一初始化 + 统一接存档接缝。玩法态(棋盘/得分/悔棋)仍归 BlockGameState，二者分层。
    /// 先只持有 SettingsService；player-info/item/mail/rank 后续逐个挂入。</summary>
    public sealed class GameContext : GameLogic.BlockBlast.SimpleSingleton<GameContext>
    {
        public GameLogic.Settings.SettingsService Settings { get; private set; }
        protected override void OnInit()
        {
            // 生产用 PlayerPrefs 存储；测试可经另一构造/注入换 InMemory。
            Settings = new GameLogic.Settings.SettingsService(
                new GameLogic.Settings.PlayerPrefsSettingsStore());
            Settings.Load();   // 启动即从框架键读已保存的开关态
        }
    }
}
```

**AudioSink 接线(启动时一处)**:`SettingsService.AudioSink` 是把开关推给真实音频模块的副作用接缝([设计 19 §3.3](#19-settings-system::apply)),须在启动流程接一次。落点 `ProcedureLaunch`(启动加载音频设置那段附近):

```text
// ProcedureLaunch 启动时（音频模块就绪后）接一次：
GameContext.Instance.Settings.AudioSink = (musicOn, soundOn) =>
{
    // dev 须 grep 核实 GameModule.Audio 在 HotFix 可达性（设计 19 §3.3）；
    // 不可达则改 ModuleSystem.GetModule<IAudioModule>()（同 ProcedureLaunch 现有用法）。
    GameModule.Audio.MusicEnable = musicOn;
    GameModule.Audio.SoundEnable = soundOn;
};
GameContext.Instance.Settings.Apply();   // 把已加载的态立即应用一次（可选；§六 OnRefresh 也会应用）
```

> [!WARNING]
> **待拍板(B1):GameContext 是否本次换皮就引入,还是先用方案 A 最小化?**
>
> 引入 `GameContext` 是个会被后续多个系统依赖的基础设施决定。**安全默认 = 引入 B**(它很薄、可逆——后续不想要可把成员摊回各单例),已按默认在本稿铺开。但这是「影响后续多系统的基础设施」,<mark>boss / 用户若倾向先用方案 A(只给 SettingsService 套单例)把试验做最小,再观察是否值得抽 GameContext</mark>,可改 —— 列入待拍板交 boss 复核(<a href="#23-settings-window-art::open">§十一 B1</a>)。两者对本窗验收等价(窗口都能拿到一个持久的 `SettingsService`)。

## 六、窗口脚本设计(SettingsWindow) {#window}

继承 `UIWindow` + `[Window]`,弹窗参数:层级 `Top`(盖在玩法窗上)、`fullScreen:false`(不优化隐藏底层、保留底层可见)。生命周期分工:`ScriptGenerator` 绑节点 → `OnCreate` 贴静态图 + 接钮一次性 → `OnRefresh` 每次开窗刷开关态。

```text
using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.Settings;
namespace GameLogic.UI    // 通用 UI 命名空间，与 BlockBlastUI（玩法窗）分开
{
    /// <summary>设置窗(美术换皮，设计 23)。模态弹窗：音频开关 + 信息 + 各跳转按钮。
    /// 数据走 GameContext.Instance.Settings(设计 19 数据层)，切图经 Atlas_settings 图集子图。</summary>
    [Window(UILayer.Top, location: "SettingsWindow", fullScreen: false)]
    public sealed class SettingsWindow : UIWindow
    {
        private const string Atlas = "Atlas_settings";
        private Button _btnMask, _btnClose;
        private Toggle _toggleSound;
        private Button _btnContact, _btnMoreGames, _btnLanguage, _btnLogout;
        private Button _btnClearSave, _btnNotify, _btnHelp, _btnPrivacy;
        private Image  _imgClose, _imgPanelTop, _imgPanelBottom; // …各贴图 Image
        private SettingsService Svc => GameContext.Instance.Settings;
        protected override void ScriptGenerator()
        {
            _btnMask      = FindChildComponent<Button>("m_btn_Mask");
            _btnClose     = FindChildComponent<Button>("Root/m_btn_Close");
            _toggleSound  = FindChildComponent<Toggle>("Root/PanelBottom/m_toggle_Sound");
            _btnContact   = FindChildComponent<Button>("Root/PanelTop/m_btn_Contact");
            // …其余按钮按 §四 路径绑定
            _imgClose      = FindChildComponent<Image>("Root/m_btn_Close");
            _imgPanelTop   = FindChildComponent<Image>("Root/PanelTop/m_img_PanelTopBg");
            // …其余贴图 Image
            // 接钮：本项目无 RegisterButtonClick（references 文档有、本工程未实现），用 onClick.AddListener（同既有窗口）。
            _btnMask.onClick.AddListener(Close);
            _btnClose.onClick.AddListener(Close);
            _toggleSound.onValueChanged.AddListener(OnSoundToggled);
            _btnClearSave.onClick.AddListener(OnClearSave);
            _btnContact.onClick.AddListener(OnContact);
            // …各按钮接对应处置（§七）
        }
        protected override void OnCreate()
        {
            // 一次性贴静态图（SetSubSprite 自动管引用计数，无需 OnDestroy 释放）
            _imgClose.SetSubSprite(Atlas, "icon_x");
            _imgPanelTop.SetSubSprite(Atlas, "base_plate");
            // …逐节点贴，子图名以 dev 读图核实的映射为准（§二 callout）
        }
        protected override void OnRefresh()
        {
            // 每次开窗刷开关态（用户上次的选择）
            _toggleSound.SetIsOnWithoutNotify(Svc.Audio.SoundOn);  // 不触发回调，仅刷 UI
            // 若有音乐开关同理；版本号/用户ID 文本同此处刷（§七）
        }
        private void OnSoundToggled(bool on)
        {
            Svc.SetSound(on);          // 数据层：改模型 + 落盘 + 应用音频（设计 19 §3.4）
            // 可选：弹提示「音效已打开/已关闭」(Svc.ToggleTipTextId → textId 占位，多语言延后)
        }
        private void Close() => GameModule.UI.CloseUI<SettingsWindow>();
        // OnClearSave / OnContact / … 见 §七
    }
}
```

> [!WARNING]
> **dev 注意:references 与工程实际的两处出入(已勘察)**
>
> - `RegisterButtonClick(btn, handler)` 在 tengine-dev references 的 ui-patterns 示例里出现,但**本工程 UIModule 未实现该方法**(grep 零命中)。既有窗口(`TempleWindow` / `MainMenuWindow`)一律用 `btn.onClick.AddListener(...)`。本稿按工程实际写 `onClick.AddListener`。
> - 窗口销毁回调是 `OnDestroy()`(无 `OnClose`)。`onClick` 监听随 GameObject 销毁自动清,无需手动 `RemoveAllListeners`;`AddUIEvent` 注册的跨模块事件才随窗口自动清(本窗暂无 `AddUIEvent` 需求)。

## 七、每个按钮 / 开关的处置分流表 {#dispatch}

按「实做(接服务 / API)/ 占位(留接线点 + TODO)/ 不做」三档。占位项不阻塞验收——只要点击不报错、留清晰接线点即可。

| 功能位 | 本次换皮处置 | 接什么 / 留什么 |
| --- | --- | --- |
| **音效开关**(`m_toggle_Sound`) | 实做 | `Svc.SetSound(on)`(设计 19 数据层,落盘 + 应用音频);`OnRefresh` 读 `Svc.Audio.SoundOn` 刷态。**核心验收项**。 |
| **关闭 X** + **遮罩** | 实做 | `CloseUI<SettingsWindow>()`。 |
| **清除存档**(`m_btn_ClearSave`) | 实做(带二次确认) | 调既有存档清除(`MergeMetaPersistence` 清键 / `BlockGameState` 重置)。<mark>须二次确认弹窗</mark>(防误触清档)。dev 若本次换皮不便建确认弹窗,降级为占位 + TODO(列 §十一 B2)。 |
| **版本号**(若效果图 / 面板有显示位) | 实做 | `SettingsInfo.Version()` → 文本节点。效果图未明显见版本号位,若 dev 读图发现有则接,无则不强加。 |
| **用户 ID**(同上) | 实做 | `SettingsInfo.UserId(playerInfo)` + 复制按钮调 `ClipboardUtil.Copy`(设计 18)。同版本号:有位才接。 |
| **兑换码入口**(若面板有) | 实做接线点 | 兑换码系统([设计 20](#20-redeem-code-system))数据层已建(#25)。入口按钮接 `GameLogic.Redeem.RedeemService`(服务入口见 `SettingsLinks.cs` 注释)。<mark>但兑换码**输入窗**本身无美术、本次换皮不建</mark>——入口按钮留点击 → 现阶段 `Log` / Toast「兑换码界面待建」+ TODO 指向 RedeemService。效果图下排 5 图标未明确含兑换码,dev 读图核实;无位则纯留服务入口注释。 |
| **帮助设置**(`m_btn_Help`) | 占位 | 无帮助 / FAQ 内容系统。点击 → Toast「帮助界面待建」+ TODO。 |
| **通知设置**(`m_btn_Notify`) | 占位 | 无推送 / 通知系统(离线)。点击 → Toast「通知设置待建」+ TODO。 |
| **隐私设置 / 用户协议**(`m_btn_Privacy`) | 占位(URL 常量已备) | `SettingsLinks.PrivacyPolicyUrl` / `UserAgreementUrl` 是占位 URL(`example.com`)。接 `Application.OpenURL(SettingsLinks.PrivacyPolicyUrl)` 即可点开占位页,真实 URL 替换常量即生效(设计 19 §七 O3)。 |
| **联系我们 / 客服**(`m_btn_Contact`) | 占位 | `SettingsLinks.ContactSupport` 是空 stub(spec 自身标待定,设计 19 §七 O2)。点击 → Toast「客服待定」+ TODO。 |
| **更多游戏**(`m_btn_MoreGames`) | 占位 | 无导流页 / 厂商列表。点击 → Toast「更多游戏待建」+ TODO(去变现方向,不做真实导流)。 |
| **语言切换**(`m_btn_Language`) | 占位 | 无多语言系统(全局文案均 textId 占位、未接真实文本表)。点击 → Toast「多语言待建」+ TODO。 |
| **退出登录**(`m_btn_Logout`) | 不做 / 占位 | 离线无账号系统(设计 19 §七 O8「快捷登录不做」对称)。按钮保留(美术对位需要),点击 → Toast「离线版无账号」+ TODO。 |
| **社交外链**(facebook/twitter/x/youtube/instagram) | 占位(URL 待来源) | 无真实社交账号 URL。点击 → `Application.OpenURL(占位URL)` 或 Toast「外链待配」+ TODO。<mark>真实 URL 来源须问产品(列 §十一)</mark>。 |

**占位统一做法**:占位按钮点击调一个统一的 `ShowPlaceholderToast(string textId)`(用 `GameModule` 既有 Toast / 飘字,若无则临时 `Log.Info`),并在代码处留 `// TODO(设计 23 §七):接 XXX`。<mark>占位不等于无反馈</mark>——点了要有「待建」提示,不能死按钮。

## 八、打开入口 + 关闭 {#entry}

| 动作 | 触发 | 实现 |
| --- | --- | --- |
| 打开 | 主界面 / HUD 的设置图标(效果图顶栏右上有齿轮 icon = `setting` 子图) | `GameModule.UI.ShowUIAsync<SettingsWindow>()`。本次换皮入口落点:<mark>主菜单 <code>MainMenuWindow</code> 加一个设置按钮</mark>(它已是 code-built 窗,加一个 `UGuiFactory.CreateButton` + onClick 即可,最小改动);玩法内 HUD 入口可后续轮次接。 |
| 关闭(X) | 右上角 `m_btn_Close` | `CloseUI<SettingsWindow>()` |
| 关闭(遮罩) | `m_btn_Mask` 全屏遮罩点击 | 同上。<mark>遮罩 Button 须在节点树最底(z 序最先),面板内容盖在其上</mark>,点面板不穿透关窗。 |

> [!NOTE]
> **为什么入口先落主菜单而非玩法 HUD**
>
> 主菜单 `MainMenuWindow` 改动面最小(加一个按钮),先打通「能开 → 能关 → 能切音效」的闭环;玩法内顶栏 HUD 入口(效果图顶栏齿轮)涉及 HUD 容器,留后续轮次或本次换皮 dev 行有余力时一并接。验收只要求**能从某个入口打开**(<a href="#23-settings-window-art::accept">§九 V1</a>)。

## 九、验收点 {#accept}

拆两档:**逻辑可单测(EditMode,test 直接跑)**vs **需 Play / 人眼(MCP 能 ShowUI + 截图核渲染,但不能模拟指针点击 / 拖拽 → 转手验遗留)**。

### 9.1 逻辑 / 编译可单测(EditMode) {#accept-logic}

| 组 | # | 验收点(完成定义) |
| --- | --- | --- |
| 编译 C | C1 | `SettingsWindow.cs` + `GameContext.cs` 编译 0 error;现有 EditMode 全绿(零回归,settings 数据层 294 例不动) |
| 编译 C | C2 | Code Review 5 红线:异步优先(无同步大资源加载)/ 模块访问 `GameModule`(音频 / UI 经 GameModule 或 ModuleSystem 正路径)/ 资源释放(`SetSubSprite` 自管引用计数,无裸 `LoadAssetAsync<Sprite>`)/ 热更边界(窗口代码在 HotFix)/ 事件解耦(按钮用 onClick / 跨模块用 GameEvent) |
| 持有者 H | H1 | `GameContext.Instance.Settings` 非空,且跨多次 `Instance` 访问返回**同一** `SettingsService` 实例(单例持有,不每次新建) |
| 持有者 H | H2 | `GameContext` 首次 `Instance` 触发 `OnInit` 调 `Settings.Load()`:无键时 `Audio.SoundOn==true && Audio.MusicOn==true`(默认全开,经数据层默认) |
| 持有者 H | H3 | 经 `GameContext.Instance.Settings.SetSound(false)` 后,再次 `GameContext.Instance.Settings.Audio.SoundOn==false`(同实例态保真);`Release()` 后重取 `Instance` 复 `Load` 仍能从存储读回上次值(持久化往返,复用数据层 P3 同源逻辑) |
| 窗口逻辑 W | W1 | 反射驱动 `ScriptGenerator`(对 prefab 实例或在测试桩上)后,各 `m_` 字段非空(节点路径 / 前缀与脚本绑定一致)——若 test 反射不便覆盖 prefab,降级为「`ScriptGenerator` 内各 `FindChildComponent` 路径与 §四节点树逐条对齐」的静态核对(test 读脚本 + prefab 对路径) |
| 窗口逻辑 W | W2 | `OnSoundToggled(false)` 调用后 `GameContext.Instance.Settings.Audio.SoundOn==false` 且存储中 `Setting.SoundMuted==true`(窗口确把开关切换贯通到数据层落盘——可经注入 InMemory store 的 GameContext 测试变体断言) |

### 9.2 需 Play / 人眼(手验遗留,boss 授权) {#accept-play}

| # | 验收点 | 能否 MCP 截图 |
| --- | --- | --- |
| V1 | 从主菜单设置入口点开 → `SettingsWindow` 显示;点 X / 点遮罩 → 关闭 | 开窗可 ShowUIAsync + 截图;点击关闭须真机 / 手验(MCP 不模拟点击) |
| V2 | <mark>切图经 SetSubSprite 正常显示</mark>:窗口各节点贴上对应子图,版面接近 `setting.png`(木牌 / 关闭 / 社交行 / 4 长条 / 5 图标各就各位) | 可 ShowUIAsync + 截图比对(核心:证明寻址链路通) |
| V3 | 音效开关切换 → 真听到游戏音效停 / 起;关窗重开,开关态保持上次选择(持久化生效) | 开关态可截图核;真实音频实听须真机手验 |
| V4 | 不破坏 Classic / Merge 主玩法:开 / 关设置窗前后,玩法窗正常,棋盘 / 落子 / 得分无异常 | 玩法渲染可截图;拖拽落子手感沿用既有手验遗留 |
| V5 | 占位按钮点击有「待建」反馈、不报错 / 不死按钮;协议 / 隐私按钮能 `OpenURL` 打开占位页 | 反馈 Toast 可截图;OpenURL 真机验 |

> [!WARNING]
> **本次换皮的「链路打通」标志(给 boss 关单判据)**
>
> V2 是本次换皮成败的核心:只要 <mark>SetSubSprite 能从 Atlas_settings 取到子图并显示在窗口上</mark>(哪怕只截图证明一两张图正确贴出),就证明「切图 → 图集 → 收集器寻址 → SetSubSprite → 显示」整条链路通,模板成立。其余按钮接线 / 占位是模板的填充内容,链路通了即可复制到后续界面。

## 十、dev 改动清单 {#hook}

符号名经勘察核实(真实存在标「✓」,新建标「新建」)。代码全落热更区 `GameScripts/HotFix`;资源落 `AssetRaw/` 被收集器收。

| # | 文件 / 资源 | 动作 | 说明 |
| --- | --- | --- | --- |
| 1 | `AssetRaw/UIRaw/Atlas/setting/*.png`(22 张切图) | 新建(导入) | §3.1;Sprite 导入设置 + 按钮底九宫格 Border |
| 2 | `AssetRaw/UIRaw/Atlas/Atlas_settings.spriteatlasv2`(§3.2 方案 A 落点) | 新建 | 整 `setting/` 目录作 Packable;Include in Build;<mark>第一步先验证寻址跑通</mark>(§3.2 callout) |
| 3 | `AssetRaw/UI/Prefabs/SettingsWindow.prefab` | 新建 | §四节点树;根 Canvas+Raycaster(仿 ✓ `TempleWindow.prefab`);1080×1920 锚点 |
| 4 | `GameScripts/HotFix/GameLogic/UI/SettingsWindow.cs` · `GameLogic.UI.SettingsWindow` | 新建 | §六;`UIWindow`+`[Window(Top, "SettingsWindow", false)]`;绑定 + 接钮 + 刷态 |
| 5 | `GameScripts/HotFix/GameLogic/GameContext.cs` · `GameLogic.GameContext` | 新建 | §五;`SimpleSingleton`(✓ `SimpleSingleton.cs`)持 `SettingsService`(✓ 设计 19)+ `Load` |
| 6 | `ProcedureLaunch`(启动流程,接 AudioSink 处) | 改(约 4 行) | §五;`GameContext.Instance.Settings.AudioSink = ...`;<mark>dev grep 核实 <code>GameModule.Audio</code> 可达性</mark>(设计 19 §3.3) |
| 7 | `MainMenuWindow.cs`(✓ 现有) | 改(加 1 按钮) | §八;加设置入口按钮 → `ShowUIAsync<SettingsWindow>` |
| 8 | `AssetBundleCollectorSetting.asset`(✓ 现有) | 确认 / 可能补路径 | §3.4;确认 `UIRaw/Atlas` 收录到 `.spriteatlasv2`;若 PackDirectory 不含图集资源本身,补一条收集路径 |
| 9 | 测试:`Assets/Editor/Tests/...` Settings/Context | 新建测试 | §9.1 的 H/W 组;`GameContext` 单例 + `SettingsService` 往返(经 InMemory store 变体) |
| — | `GameLogic.Settings` 各类 / 框架 UI / 资源代码 / 既有玩法窗口 | **不改** | 只调用数据层,不改其逻辑;不动框架;不动 Classic/Merge 玩法窗 |

## 十一、待拍板清单(范围开关,交 boss / 用户) {#open}

常规模式、用户在场。有安全默认的按默认推进(列此备查);<mark>无安全默认 / 抵触 spec / 不可逆</mark>的另在返回 blockers 报 boss。

| # | 开关 | 本次换皮默认(安全默认) | 备选 / 改动触发 |
| --- | --- | --- | --- |
| **B1** | SettingsService 运行期持有者(基础设施,影响后续多系统) | **引入 `GameContext` 统一上下文**(方案 B,§五)——薄、可逆 | 若 boss 倾向先把试验做最小:用方案 A(只给 `SettingsService` 套单例),后续再抽 GameContext。<mark>这是会被后续 player-info/item/mail/rank 依赖的决定,提请 boss 复核</mark> |
| B2 | 清除存档二次确认弹窗 | **实做带确认**(防误触清档);dev 不便建确认窗则降级占位 + TODO | 有通用确认弹窗组件则复用;无则本次换皮占位、后续补 |
| B3 | 音效开关控件类型 | **Toggle**(`m_toggle_Sound`,贴框架范式) | 切图缺静音态图时以 color 区分 + TODO;或降级双态 Button |
| B4 | 音乐开关是否进本窗 | **进**(数据层有 `SetMusic`;效果图下排若只有「音效」一项,则音乐开关并入音效位或暂不投放) | dev 读图核实下排图标含义;效果图无音乐独立位则只做音效,音乐留 TODO |
| B5 | 打开入口落点 | **主菜单 `MainMenuWindow` 加按钮**(最小改动) | 玩法 HUD 顶栏齿轮入口后续轮次 / 行有余力时一并接 |
| B6 | 切图「子图名 → 功能位」最终映射 | dev 读图核实后定(§二 callout 给推断) | 命名歧义大的(`printer` / `chat` 复用)以效果图视觉为准 |

> [!WARNING]
> **需问用户 / 产品(无安全默认 → 入返回 blockers 或 decisions)**
>
> - **社交外链真实 URL 来源**:5 个社交平台(facebook/twitter/x/youtube/instagram)的真实账号链接无来源。**安全默认 = 占位 URL / Toast「外链待配」+ TODO**(可逆、不抵触方向),按默认推进、不停机;真实 URL 由产品提供后替换常量。
> - 协议 / 隐私 / 客服真实 URL 同此:占位(数据层已备占位常量),不停机。
>
> 以上均有安全默认(占位 + TODO),按 plan 红线**不入 blockers**(不停机),记 decisions 供 boss 关单复核;真实地址是后续数据,本次换皮不阻塞。

## 十二、风险表 {#risk}

| 风险 | 应对 |
| --- | --- |
| **图集寻址不通**:`SetSubSprite` 的 `location` 取不到资源(`GetAssetInfo` 返 invalid / `LoadSubAssetsAsync` 取不到子图)——工程从未实际用过 SetSubSprite,这是真未知 | §3.2 callout:dev <mark>落地第一步先单点验证寻址</mark>(取一张子图显示出来)再铺满;方案 A 不通则试 B 并回报 boss。这是本次换皮唯一带验证风险的环节,前置验证不要等摆完整窗 |
| **分辨率坐标错位**:误套 `BlockLayout` 的 750×1334 私有坐标系 / `UGuiFactory`,与 1080×1920 参考分辨率冲突 | §四:prefab 直接用 1080×1920 锚点,不引 `BlockLayout`/`UGuiFactory`(那套是 code-built 玩法窗专用)。CanvasScaler 已是 1080×1920(✓ `main.unity:366`) |
| **遮罩穿透**:点面板内容误穿透到遮罩 Button 关窗;或遮罩没盖住底层 HUD 交互 | §八:遮罩 Button 置节点树最底(最先绘制),面板盖其上;`fullScreen:false` 但遮罩 Image 自身全屏挡住底层射线 |
| **把数据层重写一遍**:dev 为「接通」在窗口里重新实现开关 / 落盘逻辑,绕过 `SettingsService` | §读前必看第 1 条 + §六:窗口只**调用** `GameContext.Instance.Settings`,落盘 / 取反 / 应用全在数据层,窗口不碰 PlayerPrefs / 框架键 |
| **占位做成死按钮**:占位按钮点了无反应,玩家以为卡死 | §七:占位统一调 `ShowPlaceholderToast` 给「待建」反馈 + 留 TODO;验收 V5 专核 |
| **持有者越界**:把玩法态塞进 `GameContext` 或把通用服务塞进 `BlockGameState` | §五:二者分层——玩法态(棋盘 / 得分 / 悔棋)归 `BlockGameState`,通用服务归 `GameContext`;本次换皮 `GameContext` 只持 `SettingsService`,不投机性塞别的 |
