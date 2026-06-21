<style>
  /* 本篇专用：节点树 / 字段表 / 分流小样式（沿用 18 / 23 口径） */
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

# 个人信息窗美术换皮 · 表现层

塔罗 UI 换皮自治线**第二个屏**:把效果图 `个人信息.png` 换皮成可运行的 `PlayerInfoWindow`。目的:兑现[设计 18 玩家信息系统](#18-player-info)遗留的表现层(boss 遗留 #22)。<mark>本次换皮 = 纯 UI 补完</mark>——数据逻辑层([设计 18](#18-player-info))已交付并经 30 例 EditMode 单测,本屏只**调用**它 + 接线,不重写数据层。基础设施**全部复用**[设计 23 设置窗](#23-settings-window-art)已确立的范式:`[Window(Top, false)]` 弹窗 + 半透明遮罩 + `GameContext` 持有数据服务 + `FindChildComponent` 绑定 + `m_` 前缀 + `Image.SetSubSprite(精灵表, 子图名)` 取图 + `OnRefresh` 读数据刷态。**不重新发明任何基础设施。**

> [!WARNING]
> **读前必看 · 五条边界(界定范围,防把「换皮一个窗」扩成重写)**
>
> - **数据层不动,只调用。**`GameLogic.BlockBlast.Player`(`PlayerInfo` / `PlayerRenameService` / `RenameResult` / `AvatarUnlockService` / `PlayerLevelConfig` / `PlayerNameGenerator` / `ClipboardUtil`)+ 配置 `AvatarConfigMgr` 已实装并经 EditMode 单测([设计 18](#18-player-info),归档 `pipeline/archive/2026-06-14-player-info/`)。本次换皮**不**改这些类的逻辑;窗口只**调用**(读名/头像、调改名)。
> - **基础设施全部复用设计 23,不重造。**切图寻址(`SetSubSprite(精灵表 location, 子图名)`)、绑定(`FindChildComponent` + `m_` 前缀)、窗口范式(`UIWindow` + `[Window]` + 遮罩 + Top 层)、运行期上下文(`GameContext` 单例)全部已在设置窗打通([设计 23](#23-settings-window-art) §三/§四/§五/§六)。本屏照搬,**无任何新基础设施**。
> - **本屏无专属切图,复用 `Sheet_settings` 精灵表。**塔罗素材里**没有「个人信息」专属切图文件夹**(只有 设置/占卜/…/塔罗模式 等 12 个)。本屏的面板/标题板/输入框底/确定钮/关闭钮全部用设置窗已导入的 `Sheet_settings` 里的通用木板/框/按钮子图拼([§三](#25-player-info-window-art::atlas))。缺的专属图(圆形头像框、编辑铅笔、下拉箭头)→ 占位 + TODO,不阻塞([§3.2](#25-player-info-window-art::placeholder))。
> - **「生日」无数据层字段 → UI 占位。**效果图有「生日 + 3 个下拉框」,但**设计 18 数据层完全无生日概念**(`PlayerInfo` 字段:Id/Name/RenameCount/Exp/CurrentAvatarId/CurrentFrameId/UnlockedAvatarIds/UnlockedFrameIds,无生日)。本次换皮安全默认 = **UI 占位**(摆 3 个下拉框对位,不绑数据、不入存档),不擅自往持久化 DTO 加字段([§5.3](#25-player-info-window-art::birthday),决策 D2)。
> - **UI 业务代码在热更区,资源走 YooAsset,加法式不破坏 Classic。**窗口脚本落 `GameScripts/HotFix/GameLogic/UI/`(热更,与 `SettingsWindow.cs` 同目录);prefab 落 `AssetRaw/UI/Prefabs/`。改动全在加法侧——主菜单加一个入口按钮(同设置窗),不动 Classic / Merge 主玩法([§九 验收 R](#25-player-info-window-art::accept))。

> [!NOTE]
> **立项信息**
>
> | 项 | 内容 |
> | --- | --- |
> | **类型** | 表现层换皮 · 塔罗 UI 自治线第 2 屏 · 纯 UI 补完 复用设计 23 范式,兑现遗留 #22(player-info 表现层)。出设计稿 + 验收标准,交开发落地。 |
> | **设计基线(经 grep 核实的真实符号)** | **数据层(已实装,只调用,命名空间 `GameLogic.BlockBlast.Player`)**:`PlayerInfo`(字段 `Id`/`Name`/`RenameCount`/`Exp`/`CurrentAvatarId`/`CurrentFrameId`/`UnlockedAvatarIds[]`/`UnlockedFrameIds[]`;只读 `Level`;静态 `CreateDefault(rng)`/`ExportToMeta(dto)`/`ImportFromMeta(dto,rng,…)`;常量 `DefaultAvatarId=1`/`DefaultFrameId=101`);`PlayerRenameService.TryRename(p, newName, IReadOnlyCollection<string> wordList, Func<int,bool> trySpendDiamond)` → `RenameResult`(`Success`/`Reason`/`Cost`;`RenameReject{None,Empty,TooLong,Profanity,NotEnoughDiamond}`;常量 `MinLen=1`/`MaxLen=16`);`RenamePriceConfig.RENAME_PRICE=100`/`PriceFor(count)`;`AvatarUnlockService.StateOf/IsUnlocked/TryEquip/SyncLevelUnlocks/GrantUnlock`;`PlayerLevelConfig.LevelFor/ExpIntoLevel/ExpToNext`;`ClipboardUtil.Copy(text)`(可注入 `Sink`)。配置 `GameLogic.Config.AvatarConfigMgr`(`GetAvatar/GetByType/All/EnsureLoaded`)。 **UI 框架 + 取图 API(与设计 23 同源,已打通)**:`UIWindow` + `[Window(UILayer, location, fullScreen, hideTimeToClose)]`;生命周期 `ScriptGenerator → OnCreate → OnRefresh`;绑定 `FindChildComponent<T>(path)`;打开 `GameModule.UI.ShowUIAsync<T>()` / 关闭 `CloseUI<T>()`;取子图 `Image.SetSubSprite(string location, string spriteName)`([设计 23 §三](#23-settings-window-art::atlas)实测:精灵表用单张 `Sheet_settings.png`「Sprite Mode=Multiple + 命名子精灵」,`location="Sheet_settings"`,子图名 = 源切图文件名)。 **运行期上下文(已建,本次换皮扩持有)**:`GameLogic.GameContext : SimpleSingleton<GameContext>`(`OnInit` 里 `new SettingsService(...)` + `Load`;现有成员 `Settings`;现有 `InitSettingsWithStore(store)` 测试注入入口);启动接线在热更入口 `GameApp.StartGameLogic()`([§五](#25-player-info-window-art::holder))。 **入口现状**:`GameLogic.BlockBlastUI.MainMenuWindow`(code-built,`OnCreate` 里 `UGuiFactory.CreateButton`)已加过 `BtnSettings` 入口(第 58–63 行),并留有 `// TODO(player-info UI 轮): 左上角入口 → 打开 PlayerInfoWindow` 钩子(第 65–67 行)——本次换皮兑现该钩子。 **分辨率**:UIRoot CanvasScaler 参考分辨率 **1080×1920**(场景 `main.unity` 已覆写,与美术基准一致);prefab 直接用 1080×1920 锚点,**不**套 `BlockLayout` 那套 750×1334 私有坐标系。 |
> | **方向约束** | 离线还原 · **去变现**:窗口不含充值 / 内购 / 快捷登录(效果图也无)。改名扣钻经设计 38 `PlayerAttrService` 同步等服务端响应(数据层 `trySpendDiamond` 接缝形态保留;首次改名免费不发 RPC;非首次按服务端钻石账本扣减,[设计 18](#18-player-info) O8 已兑现)。加法式:新建窗口 + prefab + GameContext 扩一个成员,不改框架、不改数据层逻辑、不动既有玩法窗口。 |
> | **影响范围** | **新增资源**:`PlayerInfoWindow.prefab`(`AssetRaw/UI/Prefabs/`);**无新切图**(复用 `Sheet_settings`)。 **新增代码(热更区)**:`PlayerInfoWindow.cs`(窗口脚本,落 `GameScripts/HotFix/GameLogic/UI/`,同 `SettingsWindow.cs`)。 **改既有(最小)**:`GameContext.cs` 加一个 `Player` 成员 + `OnInit` 里 Load([§五](#25-player-info-window-art::holder));`MainMenuWindow.cs` 兑现 TODO 钩子接入口按钮(一处约 5 行,同 `BtnSettings` 做法)。 **不改**:`GameLogic.BlockBlast.Player` 各类逻辑、`AvatarConfigMgr`、`SettingsService`、框架 UI / 资源代码、Classic / Merge 玩法窗口、数据层单测、`MergeMetaSave` 任一字段(生日不入盘,[§5.3](#25-player-info-window-art::birthday))。 |
> | **关键约束(继承设计 23)** | 窗口逻辑可被反射 / 直调驱动单测(EditMode 编译 + `GameContext` 往返 + 改名贯通),但**真实视觉对位 / 指针点击 / 输入法改名**须 Play 模式人眼 + 手验。验收按「逻辑可单测(EditMode)」与「需 Play / 人眼」两档拆开([§九](#25-player-info-window-art::accept))。 |

## 一、做什么与为什么 {#what}

现状:[设计 18](#18-player-info) 的玩家信息**数据层已交付但无任何窗口能触达**——玩家无处看自己的名字 / 头像、无处改名。[设计 23](#23-settings-window-art) 已把「切图 → 精灵表 → prefab → `SetSubSprite` → 热更」整条换皮链路打通并固化成模板,并建好 `GameContext` 运行期上下文。本次换皮是<mark>模板的第二次应用</mark>:照搬设置窗的全套范式,把个人信息窗补出来,顺带把 `GameContext` 从「只持有 Settings」扩成「也持有 Player」(兑现设计 23 §五「player-info / item / mail / rank 后续逐个挂入」的预告)。

**本屏比设计 18 的完整 spec 简**:设计 18 的玩家信息界面 spec 含「改名 / id 复制 / 等级经验槽 / 头像框三态网格页签」;<mark>效果图 <code>个人信息.png</code> 是简化版</mark>——只有「头像 + 编辑铅笔 / 玩家名 + 编辑铅笔 / 生日 + 3 下拉 / 确定按钮 / 点击任意处关闭」,无等级槽、无头像网格页签、无 id 复制位,却多了一个数据层没有的「生日」。**本次换皮以效果图为对位基准**(简报指定),按效果图的元素摆节点;设计 18 spec 里效果图未出现的元素(等级槽 / 头像网格 / id 复制)属后续屏 / 后续轮,本次换皮不强加([§七](#25-player-info-window-art::dispatch))。

| # | 本次换皮交付的 | 落法 | 性质 |
| --- | --- | --- | --- |
| 1 | prefab 节点树(对位 个人信息.png) | 按效果图层级摆节点 + `m_` 前缀 + 1080×1920 锚点 + 标注每节点用 `Sheet_settings` 哪张子图 / 占位([§四](#25-player-info-window-art::tree)) | <span class="pill-new">新 prefab</span> |
| 2 | 窗口脚本(绑定 / 接钮 / 刷玩家信息) | `PlayerInfoWindow : UIWindow` + `[Window(Top, false)]`,`ScriptGenerator` 绑定 + 接 `GameContext.Instance.Player`([§六](#25-player-info-window-art::window)) | <span class="pill-new">新窗口</span> |
| 3 | GameContext 扩持有 PlayerInfo | 加 `Player` 成员 + `OnInit` Load(延续设计 23 §五统一上下文,[§五](#25-player-info-window-art::holder)) | <span class="pill-cur">扩既有(加成员)</span> |
| 4 | 改名贯通数据层 | 编辑名字 → `PlayerRenameService.TryRename` → `RenameResult` 分支提示([§七](#25-player-info-window-art::dispatch)) | <span class="pill-cur">接线</span> |
| 5 | 打开入口 + 关闭 | 主菜单 `MainMenuWindow` 兑现 TODO 钩子加入口 → `ShowUIAsync`;X / 遮罩 / 确定 → `CloseUI`([§八](#25-player-info-window-art::entry)) | <span class="pill-new">新接线</span> |

**不做(本次换皮明确排除):**<span class="pill-no">改数据层逻辑</span>(只调用);<span class="pill-no">生日入存档</span>(数据层无字段,UI 占位,[§5.3](#25-player-info-window-art::birthday));<span class="pill-no">头像三态解锁网格页签</span>(效果图只显当前头像,完整网格属设计 18 后续屏,本次换皮占位/后续,[§七](#25-player-info-window-art::dispatch));<span class="pill-no">等级经验槽</span>(效果图无该位,数据层有 `Level` 可显但效果图未画,本次换皮不强加);<span class="pill-no">新切图 / 新图集 / 打表</span>(复用 `Sheet_settings`,无新切图目录则不动打表工具)。

## 二、效果图拆解(对位基准) {#effigy}

美术基准 `个人信息.png`(1080×1920 竖屏,扁平 PNG)。盖在游戏 HUD 上的**模态弹窗**:半透明深色遮罩 + 居中木牌面板。自上而下:

| 区块 | 效果图内容 | 取图(`Sheet_settings` 子图 / 占位) | 节点类型 | 处置 |
|---|---|---|---|---|
| ① 遮罩 | 盖住整屏的半透明深色背景(透出底层 HUD) | 无(纯色 `Image`,alpha≈0.6) | `Button`(点击关窗) | 实做 |
| ② 标题木牌 | 顶部木牌「个人信息」 | `box2`(木牌底,设置窗上面板同用)+ 文本节点「个人信息」 | `Image` + `Text` | 实做 |
| ③ 关闭按钮 | 右上角圆形 × 按钮 | `icon_x`(圆 + X 一体图,设置窗同用) | `Button` | 实做 |
| ④ 主面板底板 | 居中大木板 | `box1` 或 `box2`(木板底,设置窗面板同用) | `Image` | 实做 |
| ⑤ 头像 | 圆形头像 + 圆形描边框,右下角编辑铅笔 | 头像本体:当前佩戴头像 Sprite(<mark>无美术,占位纯色圆 / 通用图</mark>);圆框 + 铅笔:<mark>Sheet_settings 无圆头像框 / 铅笔图 → 占位</mark>(<a href="#25-player-info-window-art::placeholder">§3.2</a>) | `Image` + `Button`(铅笔=编辑头像) | 显当前头像实做<br><span class="pill-no">框/铅笔图占位</span> |
| ⑥ 玩家名 | 居中「玩家123」+ 右侧编辑铅笔 | 名字:文本节点(读 `PlayerInfo.Name`);铅笔:占位 | `Text` + `InputField`(改名)+ `Button`(铅笔) | 实做改名 |
| ⑦ 生日 + 3 下拉 | 「🎂 生日」标题 + 一行 3 个下拉框(各显「3 ▾」) | 下拉底:`button`(条底,设置窗同用);下拉箭头:<mark>占位</mark>;标题:文本节点 | `Text` + `Dropdown`×3(或 `Image`+`Text` 占位) | 占位(数据层无生日,<a href="#25-player-info-window-art::birthday">§5.3</a>) |
| ⑧ 确定按钮 | 底部黄色「确定」长条 | `button`(长条底,设置窗同用)+ 文本「确定」 | `Button` | 实做(保存并关) |
| ⑨ 关闭提示 | 面板下方小字「点击任意位置置关闭」 | 无(文本节点) | `Text` | 实做(纯文本) |

> [!NOTE]
> **取图须 dev 读图二次核实**
>
> 上表「取哪张子图」是<mark>按设置窗已用子图推断</mark>的最省方案。dev 落地时对照 `个人信息.png` 与 `Sheet_settings` 各子图缩略图核实哪张木板 / 框最贴效果图(`box1`/`box2`/`base_plate`/`base_plate2`/`base_plate3` 五种木板任选最贴的),把最终「子图名 → 节点」映射写进窗口脚本注释。**子图名集合 = 设置窗那 22 张切图文件名**(`base_plate`/`base_plate2`/`base_plate3`/`box1`/`box2`/`button`/`icon_x`/`x`/`chat`/`clear`/`exit`/`facebook`/`game`/`help`/`instagram`/`language`/`Player_music`/`printer`/`setting`/`twitter`/`Volume_up`/`youtube`),<mark>这些就是本屏可用的全部图</mark>——圆头像框 / 铅笔 / 下拉箭头都不在其中,故占位(<a href="#25-player-info-window-art::placeholder">§3.2</a>)。

## 三、美术资产接入(复用 Sheet_settings,无新切图) {#atlas}

### 3.1 复用设置窗精灵表 {#atlas-reuse}

本屏<mark>不导入任何新切图、不建新图集、不动打表工具</mark>。设置窗已把 `Sheet_settings.png`(单张 Multiple 模式精灵表,含 22 个命名子精灵)导入并被收集器收录、运行期可经 `SetSubSprite` 寻址([设计 23 §三](#23-settings-window-art::atlas)实测打通)。本屏的所有木板 / 框 / 长条 / 关闭按钮直接复用其子图:

```text
private const string Atlas = "Sheet_settings";   // 复用设置窗精灵表 location（设计 23 §三）
// OnCreate 一次性贴静态图（SetSubSprite 内置引用计数 + SubSpriteReference 自动释放，无需手动 Unload）
_imgPanelBg.SetSubSprite(Atlas, "box1");     // 主面板底板
_imgClose.SetSubSprite(Atlas, "icon_x");     // 关闭圆按钮
_imgConfirmBg.SetSubSprite(Atlas, "button"); // 确定长条底
// …各节点贴，子图名以 §二 dev 读图核实后的映射为准
```

**不写法**(同设计 23):<span class="no">不</span>用 `LoadAssetAsync<Sprite>`(违 `resource-api` 红线);<span class="no">不</span>用 `AddressByFileName` 平铺单图。静态图在 `OnCreate` 贴一次,会运行期变的只有头像 / 玩家名,由 `OnRefresh` 刷([§六](#25-player-info-window-art::window))。

### 3.2 三处缺图的占位策略 {#placeholder}

效果图有三类图 `Sheet_settings` 没有,本次换皮占位 + 留 TODO,不阻塞验收(同设计 23 占位口径——占位不等于无反馈 / 不等于摆不出节点):

| 缺的图 | 占位做法 | TODO(待美术补) |
| --- | --- | --- |
| **圆形头像 + 描边框** | 头像本体:`Image` 节点贴当前头像(<mark>头像 Sprite 也无美术</mark>,设计 18 O2)→ 退一层占位:纯色圆 `Image`(`color` 由 `CurrentAvatarId` 取一个稳定色)或显一张通用图(如 `setting` 子图临时代替)。圆框:用一张半透明描边色块 / 设置窗 `box*` 缩成圆角代替,或省略(留节点占位)。 | 待美术补「圆头像框」切图 + 各头像 Sprite,接 `AvatarConfigMgr.GetAvatar(id).Image` 资源名加载 |
| **编辑铅笔 icon**(头像 / 名字各一) | 用现成图标代替(如 `setting` 齿轮 / `language` 等近形子图),或纯文本「✎」/「编辑」按钮。<mark>关键是按钮可点、点了能触发编辑</mark>,图标外观次要。 | 待美术补「编辑铅笔」切图,替子图即生效 |
| **下拉箭头「▾」** | 用文本字符「▾」/「∨」直接当箭头(`Text` 节点),或省略(下拉框本就占位)。 | 待美术补「下拉箭头」切图(生日整体占位,优先级最低) |

> [!NOTE]
> **占位的统一原则(同设计 23)**
>
> 占位 = **节点摆齐 + 可交互 + 留清晰 TODO**,不是「不摆」。头像 / 铅笔 / 下拉箭头视觉是占位,但<mark>头像节点要显出当前头像区分度、铅笔按钮要可点触发编辑、生日下拉要摆出 3 个框对位</mark>——美术切图到位后替子图 / 替 Sprite 即生效,节点结构与脚本逻辑不返工。

## 四、prefab 节点树(对位 个人信息.png + m_ 前缀命名) {#tree}

根节点照设置窗 prefab 范式:根挂 `RectTransform`(stretch 0,0→1,1)+ `Canvas` + `GraphicRaycaster`。坐标系 = 1080×1920 参考分辨率,直接用真实锚点。`m_` 前缀决定 `FindChildComponent` 绑定类型(前缀表见 tengine-dev `naming-rules`)。下方坐标为对位描述,精确像素 dev 摆图时对着 `个人信息.png` 微调。

```text
PlayerInfoWindow                       (根: RectTransform 全屏 stretch + Canvas + GraphicRaycaster)
├─ m_btn_Mask                          Button  全屏遮罩(Image alpha≈0.6 深色; 点击关窗); 锚点全屏 stretch
└─ Root                                RectTransform 居中容器(承载所有可见内容)
   ├─ m_img_TitleBg                    Image   标题木牌底; 子图 box2
   ├─ m_text_Title                     Text    "个人信息"(若美术木牌已烤字则删此节点)
   ├─ m_btn_Close                      Button  右上角圆形关闭; 子图 icon_x; 锚点(顶部右)
   ├─ m_img_PanelBg                    Image   主面板底板; 子图 box1
   ├─ AvatarBlock                      RectTransform 头像区
   │  ├─ m_img_Avatar                  Image   圆形头像本体(占位: 当前头像 Sprite, 无美术→纯色圆/通用图)
   │  ├─ m_img_AvatarFrame             Image   圆形描边框(占位, §3.2)
   │  └─ m_btn_EditAvatar             Button  右下角编辑铅笔(占位图 + 可点); 点击→编辑头像(本次换皮占位 Toast)
   ├─ NameBlock                        RectTransform 玩家名区
   │  ├─ m_text_Name                   Text    显示当前昵称(读 PlayerInfo.Name)
   │  ├─ m_input_Name                  InputField 改名输入框(默认隐藏, 点铅笔显出; 见 §七)
   │  ├─ m_btn_EditName               Button  右侧编辑铅笔(占位图 + 可点); 点击→进入改名态
   │  ├─ m_text_DiamondBalance         Text    钻石余额(设计 38 §五, 显当前 PlayerAttrService.Diamond 支撑「钻石不足」分支可观测)
   │  └─ m_btn_LedgerEntry            Button  「我的流水」入口按钮(设计 46 §4.2, 点击→OpenWindow PlayerAttrLedgerWindow; 美术补图前节点占位 + null-safe 跳过, 沿 Sheet_settings.button 长条底图)
   ├─ BirthdayBlock                    RectTransform 生日区(整块 UI 占位, 不绑数据 §5.3)
   │  ├─ m_text_BirthdayLabel          Text    "🎂 生日"
   │  ├─ m_drop_Year                   Dropdown 年(占位; 或 Image+Text 摆位)
   │  ├─ m_drop_Month                  Dropdown 月(占位)
   │  └─ m_drop_Day                    Dropdown 日(占位)
   ├─ m_btn_Confirm                    Button  确定长条; 子图 button + 文本"确定"; 点击→保存并关
   └─ m_text_CloseHint                 Text    "点击任意位置置关闭"(纯文本)
```

> [!NOTE]
> **静态节点 vs 动态节点**
>
> 本窗**几乎全静态**(prefab 直接摆好,无列表 / 无运行期增删),适合照搬设置窗范式。运行期会变的只有:**头像**(`m_img_Avatar`,`OnRefresh` 读 `CurrentAvatarId` 刷)与**玩家名**(`m_text_Name`,改名后刷)。改名输入框 `m_input_Name` 默认隐藏(`SetActive(false)`),点编辑铅笔切到改名态显出(<a href="#25-player-info-window-art::dispatch">§七</a>)。<mark>不涉及头像三态网格</mark>(那是设计 18 完整界面的元素,效果图本屏未画,本次换皮不建,<a href="#25-player-info-window-art::dispatch">§七</a>)。

> [!NOTE]
> **改名交互:就地输入框 vs 独立改名弹窗?**
>
> 效果图只见「玩家名 + 铅笔」,未画独立改名窗。两种实现:**(默认推荐)就地输入框**——`m_text_Name`(只读显示)+ `m_input_Name`(`InputField`,默认隐藏)叠在同位,点铅笔时 `Name` 隐藏 / `Input` 显出并聚焦,确认(回车 / 点确定)调 `TryRename`,成功后切回只读。**(备选)**独立 `RenameWindow` 弹窗——但效果图无该窗、且会多一个 prefab,本次换皮不引入。取就地输入框为默认,验收等价(都贯通 `TryRename` + `RenameResult` 分支,<a href="#25-player-info-window-art::accept">§九 W3</a>)。

## 五、GameContext 扩持有 PlayerInfo(延续统一上下文) {#holder}

### 5.1 为什么纳入 GameContext {#holder-why}

[设计 23 §五](#23-settings-window-art::holder)已确立:`GameContext` 是「数据层已建、需运行期持有者」的无主系统的统一归宿,并明示「本次换皮先只持有 `SettingsService`;player-info / item / mail / rank 后续逐个挂入」。<mark>本次换皮兑现 player-info 那一项</mark>——把 `PlayerInfo` 的运行期持有 + 一次加载收进 `GameContext`,而非给 `PlayerInfo` 另套一个单例(避开设计 23 §5.2 方案 A 的「N 个系统 N 套样板」)。窗口只 `GameContext.Instance.Player` 取,与设置窗 `GameContext.Instance.Settings` 同源。

### 5.2 加载来源:并入既有 MergeMetaSave {#holder-load}

玩家信息持久化<mark>不另造存储栈</mark>:[设计 18 §3.8](#18-player-info::persist)已把玩家字段平铺进既有 `MergeMetaSave` DTO,经 `MergeMetaPersistence.Load` 落盘 / 读取,`PlayerInfo.ExportToMeta(dto)` / `ImportFromMeta(dto, rng, …)` 是纯方法在 DTO 与模型间转换。`GameContext.OnInit` 里的加载分两路(dev 落地时按工程现状二选一,见下 callout):

```text
// GameContext.cs（增量：加 Player 成员 + OnInit 里 Load）
public PlayerInfo Player { get; private set; }   // 新增成员（设计 18 数据层模型）
protected override void OnInit()
{
    // ── 既有：Settings（设计 23，一行不改）──
    Settings = new SettingsService(new PlayerPrefsSettingsStore());
    Settings.Load();
    // ── 新增：Player ──
    var rng = new System.Random();
    var dto = LoadMergeMetaDto();                 // 取既有存档 DTO（路 A/B 见下）
    Player = (dto != null)
        ? PlayerInfo.ImportFromMeta(dto, rng,     // 有档：从 DTO 重建（逐字段保底夹值，设计 18 §3.8）
              avatarValid: id => GameLogic.Config.AvatarConfigMgr.GetAvatar(id) != null)
        : PlayerInfo.CreateDefault(rng);          // 无档：新建（新 id + 系统名 + 默认头像框）
}
```

> [!WARNING]
> **待 dev 核实(B1):玩家信息从哪取存档 DTO + 何时落盘**
>
> `MergeMetaPersistence.Load` 是异步外壳(<a href="#14-save-system">设计 14</a>:序列化层同步 + 磁盘 IO 异步 UniTask)。`GameContext.OnInit`(`SimpleSingleton` 同步)里直接同步取 DTO 可能与异步加载时序不一致。dev 落地时按工程现状定 **取档时机**(二选一):**(路 A)**`GameContext` 不在 `OnInit` 同步加载玩家档,而由 `GameApp.StartGameLogic()` 在 `MergeMetaPersistence` 加载完成后(同设置窗 AudioSink 接线那段附近)调一个 `GameContext.Instance.InitPlayerFromMeta(dto, rng)`(仿既有 `InitSettingsWithStore` 注入入口);**(路 B)**若工程已有同步可达的「当前 `MergeMetaSave` 实例 / 当前 `PlayerInfo`」(如 `BlockGameState` 或 `MergeOrderState` 已在内存持有),直接引用、不重复读盘。<mark>两路对本窗验收等价</mark>(窗口都拿到一个持久的 `PlayerInfo`);取哪路 dev 按 `MergeMetaPersistence` / `MergeOrderState` 的真实加载时序定,并把**改名后落盘**接到同一存档路径(改名改了 `PlayerInfo.Name`/`RenameCount`,须 `ExportToMeta` + 存档,否则重启丢失,<a href="#25-player-info-window-art::dispatch">§七 W3</a>)。这是本次换皮唯一需 dev 对齐工程加载时序的环节,非方向问题(决策 D1 已定「纳入 GameContext」,只是接线落点 dev 定)。

### 5.3 生日:数据层无字段 → UI 占位(决策 D2) {#birthday}

效果图有「生日 + 3 下拉框」,但[设计 18](#18-player-info) 数据层<mark>完全无生日概念</mark>——`PlayerInfo` 8 个字段无一与生日相关,spec(`1001玩家信息系统.xlsx`)也未列生日。处置二选一:

| 方案 | 做法 | 代价 | 本次换皮取舍 |
| --- | --- | --- | --- |
| **UI 占位(默认)** | 摆 3 个下拉框对位效果图,<mark>不绑数据、不入存档</mark>,值固定显「3」(同效果图),点击 → Log / Toast「生日待接数据层」+ TODO | 零数据层改动,可逆,与 spec 无抵触 | 取此 |
| 加生日字段进盘 | 给 `PlayerInfo` 加 birthYear/Month/Day 字段 + `MergeMetaSave` 加 3 字段 + `ExportToMeta`/`ImportFromMeta` 加拷贝 + 保底夹值 + 单测 | <mark>改动数据层 + 持久化 DTO + 保底逻辑 + 数据层单测</mark>——超出「纯 UI 补完」范围,且 spec 未要求 | 不取(本次换皮) |

**取 UI 占位**(决策 D2,安全默认)。理由:① 本次换皮任务是「纯 UI 补完」,加生日字段会动数据层 + 持久化 + 保底,溢出范围;② spec 未要求生日,擅自入盘是替产品定需求;③ 占位可逆——日后若产品要真生日,在数据层加字段是局部增量(同设计 18 加字段做法),UI 节点已摆好、替占位为绑定即可,不返工。<mark>生日是否要真做、是否入存档,列待裁决交 boss / 产品</mark>([§十一 D2](#25-player-info-window-art::open)),本次换皮按占位推进不阻塞。

## 六、窗口脚本设计(PlayerInfoWindow) {#window}

继承 `UIWindow` + `[Window]`,弹窗参数同设置窗:层级 `Top`、`fullScreen:false`。生命周期分工:`ScriptGenerator` 绑节点 + 接钮 → `OnCreate` 贴静态图 → `OnRefresh` 每次开窗刷头像 + 名。命名空间 `GameLogic.UI`(同 `SettingsWindow`)。

```text
using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.BlockBlast.Player;
namespace GameLogic.UI
{
    /// <summary>个人信息窗(美术换皮，设计 25)。模态弹窗：头像 + 玩家名(可改名) + 生日(占位) + 确定。
    /// 数据走 GameContext.Instance.Player(设计 18 数据层)，切图复用 Sheet_settings 精灵表(设计 23)。</summary>
    [Window(UILayer.Top, location: "PlayerInfoWindow", fullScreen: false)]
    public sealed class PlayerInfoWindow : UIWindow
    {
        private const string Atlas = "Sheet_settings";   // 复用设置窗精灵表（设计 23 §三）
        private Button _btnMask, _btnClose, _btnConfirm, _btnEditAvatar, _btnEditName;
        private Text   _textName;
        private InputField _inputName;
        private Image  _imgAvatar, _imgAvatarFrame, _imgPanelBg, _imgTitleBg, _imgClose, _imgConfirmBg;
        // 生日 3 下拉占位（Dropdown 或 Image+Text，§5.3）
        private PlayerInfo P => GameContext.Instance.Player;
        protected override void ScriptGenerator()
        {
            _btnMask       = FindChildComponent<Button>("m_btn_Mask");
            _btnClose      = FindChildComponent<Button>("Root/m_btn_Close");
            _btnConfirm    = FindChildComponent<Button>("Root/m_btn_Confirm");
            _btnEditAvatar = FindChildComponent<Button>("Root/AvatarBlock/m_btn_EditAvatar");
            _btnEditName   = FindChildComponent<Button>("Root/NameBlock/m_btn_EditName");
            _textName      = FindChildComponent<Text>("Root/NameBlock/m_text_Name");
            _inputName     = FindChildComponent<InputField>("Root/NameBlock/m_input_Name");
            _imgAvatar     = FindChildComponent<Image>("Root/AvatarBlock/m_img_Avatar");
            _imgPanelBg    = FindChildComponent<Image>("Root/m_img_PanelBg");
            _imgClose      = FindChildComponent<Image>("Root/m_btn_Close");
            _imgConfirmBg  = FindChildComponent<Image>("Root/m_btn_Confirm");
            // …其余按 §四 路径绑定
            // 接钮（onClick；监听随 GameObject 销毁自动清，无需手动 Remove——同设计 23）
            _btnMask.onClick.AddListener(OnConfirmAndClose);   // 遮罩 = 点任意处关闭（效果图「点击任意位置关闭」）
            _btnClose.onClick.AddListener(OnConfirmAndClose);
            _btnConfirm.onClick.AddListener(OnConfirmAndClose);
            _btnEditAvatar.onClick.AddListener(OnEditAvatar);
            _btnEditName.onClick.AddListener(OnEnterRename);
            if (_inputName != null) _inputName.onEndEdit.AddListener(OnRenameSubmit);
        }
        protected override void OnCreate()
        {
            // 一次性贴静态图（SetSubSprite 自动管引用计数）
            _imgTitleBg?.SetSubSprite(Atlas, "box2");
            _imgPanelBg.SetSubSprite(Atlas, "box1");
            _imgClose.SetSubSprite(Atlas, "icon_x");
            _imgConfirmBg.SetSubSprite(Atlas, "button");
            // …其余静态底图；头像框 / 铅笔 / 下拉箭头无图 → 占位（§3.2）
            if (_inputName != null) _inputName.gameObject.SetActive(false);  // 改名态默认隐藏
        }
        protected override void OnRefresh()
        {
            // 每次开窗刷玩家信息
            if (_textName != null) _textName.text = P.Name;
            RefreshAvatar();
        }
        /// <summary>刷头像（占位：无头像 Sprite → 按 CurrentAvatarId 取稳定占位色，§3.2）。</summary>
        private void RefreshAvatar()
        {
            // TODO(设计 25 §3.2): 接 AvatarConfigMgr.GetAvatar(P.CurrentAvatarId).Image 加载真实头像 Sprite。
            // 占位：纯色圆 / 通用图，区分度由 id 取色。
        }
        // ── 改名（实做，贯通数据层 §七 W3）──
        private void OnEnterRename()
        {
            if (_inputName == null) return;
            _inputName.text = P.Name;
            _inputName.gameObject.SetActive(true);
            if (_textName != null) _textName.gameObject.SetActive(false);
            _inputName.ActivateInputField();
        }
        private void OnRenameSubmit(string newName)
        {
            // 屏蔽字词表可注入空表（去变现/不阻塞，设计 18 O6）；扣钻经服务端 PlayerAttrService.TryChangeAsync（设计 38）。
            // 同步等响应（OnRenameSubmit 改 async UniTask；细节见设计 38 §六）。
            bool spent = await GameContext.Instance.PlayerAttr.TryChangeAsync(
                AttrType.Diamond, -cost, "player_rename").ContinueWith(r => r.Success);
            var result = PlayerRenameService.TryRename(
                P, newName,
                wordList: System.Array.Empty<string>(),
                trySpendDiamond: _ => spent);   // 经设计 38 兑现 18 §3.4 O8
            if (result.Success)
            {
                if (_textName != null) _textName.text = P.Name;
                SavePlayerMeta();                  // 改名落盘（§5.2 B1：接既有存档路径）
            }
            else
            {
                ShowRenameReject(result.Reason);   // 分支提示：空 / 超长 / 屏蔽字 / 钻石不足
            }
            _inputName.gameObject.SetActive(false);
            if (_textName != null) _textName.gameObject.SetActive(true);
        }
        private void ShowRenameReject(RenameReject reason)
        {
            string msg = reason switch
            {
                RenameReject.Empty            => "名字不能为空",
                RenameReject.TooLong          => "名字过长（上限 16）",
                RenameReject.Profanity        => "名字含敏感词",
                RenameReject.NotEnoughDiamond => "钻石不足",
                _                             => "改名失败",
            };
            ShowPlaceholder(msg);
        }
        private void OnEditAvatar()
            => ShowPlaceholder("头像选择网格待建（设计 18 三态网格属后续屏 §七）");
        /// <summary>确定 / 关闭：保存玩家信息并关窗（效果图「确定」「点击任意位置关闭」同此）。</summary>
        private void OnConfirmAndClose()
        {
            SavePlayerMeta();
            GameModule.UI.CloseUI<PlayerInfoWindow>();
        }
        /// <summary>玩家信息落盘（§5.2 B1：ExportToMeta + 接既有 MergeMetaPersistence 存档路径）。</summary>
        private void SavePlayerMeta()
        {
            // TODO(设计 25 §5.2 B1): P.ExportToMeta(dto) + 调既有 MergeMetaPersistence.SaveAsync(dto)。
        }
        /// <summary>占位统一反馈（同设计 23：工程暂无 Toast → 临时 Log.Info；待建后替换）。</summary>
        private void ShowPlaceholder(string msg) => Log.Info($"[个人信息窗] {msg}");
    }
}
```

> [!WARNING]
> **dev 注意(与设计 23 一致的两处工程实际)**
>
> - 接钮一律 `onClick.AddListener`(本工程 UIModule **无 `RegisterButtonClick`**,设计 23 已验证)。`onClick` 监听随 GameObject 销毁自动清,无需手动 `RemoveAllListeners`。
> - 占位反馈 `ShowPlaceholder` 临时走 `Log.Info`(工程暂无 Toast / 飘字,同设计 23);改名拒绝提示亦同——待 Toast 系统建后替换为真实弹字。**占位不等于死按钮**:点了要有 Log / 提示。
> - `InputField` vs `TMP_InputField`:dev 按工程既有 UI 用的输入框类型选(grep 现有 prefab / 框架默认),`FindChildComponent<T>` 的 T 与 prefab 节点组件类型须一致。

## 七、各控件处置分流表 {#dispatch}

按「实做 / 占位 / 不做(本屏)」三档。占位项不阻塞验收——只要点击不报错、留清晰接线点即可。

| 功能位 | 本次换皮处置 | 接什么 / 留什么 |
|---|---|---|
| **玩家名显示**(`m_text_Name`) | 实做 | `OnRefresh` 读 `GameContext.Instance.Player.Name` → 文本。**核心验收项**(W2)。 |
| **改名**(铅笔 → `m_input_Name`) | 实做(贯通数据层 + 接服务端) | 点铅笔进改名态 → 输入 → 同步等 `PlayerAttrService.TryChangeAsync(Diamond, -cost, "player_rename")` 服务端响应(设计 38)→ 把响应成败封装为 `trySpendDiamond` 传 `PlayerRenameService.TryRename` → `RenameResult`:成功刷名 + 落盘;失败按 `Reason` 分支提示(空 / 超长 / 屏蔽字 / 钻石不足)。**核心验收项**(W3)。屏蔽字词表本次换皮注空表(去变现 / 不阻塞,设计 18 O6);首次改名(`RenameCount = 0`)免费,不发 RPC(设计 38 §六)。 |
| **头像显示**(`m_img_Avatar`) | 实做(显当前)<br><span class="pill-no">真图占位</span> | `OnRefresh` 读 `CurrentAvatarId`。<mark>头像 Sprite 无美术</mark>(设计 18 O2)→ 占位纯色圆 / 通用图,留 TODO 接 `AvatarConfigMgr.GetAvatar(id).Image`。 |
| **编辑头像**(`m_btn_EditAvatar` 铅笔) | 占位 | 头像三态选择网格是设计 18 完整界面元素,<mark>效果图本屏未画</mark>(本屏只显当前头像)。点击 → Toast「头像选择待建」+ TODO。完整网格(`AvatarUnlockService.StateOf` + 换装 `TryEquip`)属设计 18 后续屏 / 后续轮(<a href="#25-player-info-window-art::open">§十一 D3</a>)。 |
| **生日 + 3 下拉**(`BirthdayBlock`) | 占位(整块) | 数据层无生日字段(<a href="#25-player-info-window-art::birthday">§5.3</a> D2)。摆 3 个下拉框对位,值固定「3」,不绑数据 / 不入盘。点击 → Toast「生日待接数据层」+ TODO。是否真做交 boss / 产品。 |
| **确定**(`m_btn_Confirm`) | 实做 | 保存玩家信息(`ExportToMeta` + 落既有存档,<a href="#25-player-info-window-art::holder">§5.2 B1</a>)+ `CloseUI`。本次换皮玩家信息的运行期改动主要是改名(已在改名时落盘),确定再保险存一次 + 关。 |
| **关闭 X** + **遮罩(点任意处)** | 实做 | 同确定:保存 + `CloseUI<PlayerInfoWindow>()`。效果图「点击任意位置置关闭」= 遮罩 `m_btn_Mask` 点击关窗(遮罩须在节点树最底,面板内容盖其上,点面板不穿透)。 |
| **关闭提示文本**(`m_text_CloseHint`) | 实做 | 纯文本「点击任意位置置关闭」,无逻辑。 |
| **等级 / 经验槽** | 不做(本屏) | <mark>效果图本屏无等级 / 经验槽位</mark>。数据层有 `PlayerInfo.Level` / `PlayerLevelConfig` 可显,但效果图未画 → 本次换皮不强加(设计 18 完整界面有此位,属后续屏)。 |
| **id 复制** | 不做(本屏) | 效果图本屏无 id 显示 / 复制位。数据层有 `ClipboardUtil.Copy` + `PlayerInfo.Id`,设计 18 完整界面有此位,属后续屏。 |

## 八、打开入口 + 关闭 {#entry}

| 动作 | 触发 | 实现 |
| --- | --- | --- |
| 打开 | 主菜单入口(效果图顶栏左上角有玩家头像 / 信息入口) | 兑现 `MainMenuWindow.cs` 第 65–67 行已留的 `// TODO(player-info UI 轮): 左上角入口 → 打开 PlayerInfoWindow` 钩子:照 `BtnSettings` 做法(第 58–63 行)加一个 `UGuiFactory.CreateButton` + `onClick` → `GameModule.UI.ShowUIAsync<GameLogic.UI.PlayerInfoWindow>()`。最小改动。 |
| 关闭(X) | 右上角 `m_btn_Close` | 保存 + `CloseUI<PlayerInfoWindow>()` |
| 关闭(确定) | `m_btn_Confirm` | 同上(效果图底部「确定」) |
| 关闭(点任意处) | `m_btn_Mask` 全屏遮罩 | 同上(效果图「点击任意位置置关闭」)。<mark>遮罩 Button 须在节点树最底,面板内容盖其上</mark>,点面板不穿透关窗。 |

> [!NOTE]
> **入口落主菜单(同设计 23)**
>
> 入口与设置窗一致先落主菜单 `MainMenuWindow`(改动面最小,加一个按钮),打通「能开 → 能改名 → 能关」闭环;玩法内 HUD 顶栏的玩家信息入口(效果图顶栏左上头像)涉及 HUD 容器,留后续轮次。验收只要求**能从某个入口打开**(<a href="#25-player-info-window-art::accept">§九 V1</a>)。

## 九、验收标准 {#accept}

拆两档:**H/W 组 = 逻辑可 EditMode 单测**(编译 + `GameContext` 往返 + 改名贯通,不依赖 Play / 真实视觉);**V 组 = 需 Play / 人眼**(对位 + 指针 + 开关闭)。占位项验收 = 「点击不报错 + 有 Log / 提示 + 节点摆齐」,不要求真功能。

### 9.1 H/W 组 — EditMode 可单测(test 直调断言) {#accept-hw}

| # | 验收点 | 怎么核 |
| --- | --- | --- |
| H1 | 编译通过 | 新增 `PlayerInfoWindow.cs` + 改 `GameContext.cs` / `MainMenuWindow.cs` 后,热更程序集编译 0 error。 |
| H2 | GameContext 持有 PlayerInfo 往返 | EditMode:`GameContext.Instance.Player` 非 null;经测试注入入口(仿 `InitSettingsWithStore` 加一个 `InitPlayerFromMeta(dto, rng)` 或直读)灌入已知 DTO → `Player.Name`/`CurrentAvatarId` 等字段 == DTO 值;无 DTO 时 == `CreateDefault` 缺省(默认头像 1 / 框 101 / 系统名)。 |
| W2 | 玩家名显示读数据层 | EditMode:构造 `PlayerInfo` 设 `Name="测试名"` → 窗口 `OnRefresh` 后 `m_text_Name.text == "测试名"`(经反射调 `OnRefresh` + 读字段,同设计 23 反射驱动口径)。 |
| W3 | 改名贯通数据层 + 接服务端(4 分支) | EditMode 经 `GameContext.InitPlayerAttrWith(桩 IRpcGateway)` 注入桩响应,直调 `PlayerInfoWindow.OnRenameSubmit`(或 await 等价的纯逻辑入口):合法名 + 桩返成功 → `Success` 且 `Player.Name` 改、`RenameCount+1`;空 → `Reason==Empty`;超 16 → `TooLong`;空词表 → 不拦(`Profanity` 不触发);桩返失败(余额不足)且非首次 → `NotEnoughDiamond`(同设计 38 CV5 + W4 范式);首次改名(`RenameCount == 0`)→ 桩 `IRpcGateway` 调用次数 = 0(免费不走 RPC,W6)。<mark>这层数据层已有单测(设计 18 R1–R5),本次换皮验收 = 窗口确实委托它 + 接 PlayerAttrService 同步等响应、不自己另写改名逻辑、不 fire-and-forget</mark>。 |
| W4 | 窗口绑定路径对齐 prefab | `ScriptGenerator` 里每个 `FindChildComponent<T>(path)` 的 path 与 §四节点树逐一对齐(dev 自查 + test code review 核);Play 模式打开窗口无「FindChild 返回 null」报错(并入 V 组实测)。 |
| W5 | 占位项不崩 | 编辑头像 / 生日下拉 / 各占位点击 → 走 `ShowPlaceholder`(`Log.Info`),不抛异常、不空引用。EditMode 可调对应方法断言不抛。 |

### 9.2 V 组 — 需 Play / 人眼(MCP 截图 + 手验) {#accept-v}

| # | 验收点 | 怎么核 |
| --- | --- | --- |
| V1 | 能从入口打开 | 主菜单点个人信息入口 → 窗口出现(MCP `ShowUIAsync` + 截图),无报错。 |
| V2 | 视觉对位 个人信息.png | 人眼比对:标题木牌 / 圆头像 + 框 / 玩家名 + 铅笔 / 生日 + 3 下拉 / 确定 / 关闭提示 位置与效果图大致一致(占位图允许外观不同,位置须对)。 |
| V3 | 切图贴对(复用 Sheet\_settings) | 截图核 `box1`/`box2`/`icon_x`/`button` 等子图正确显示(`SetSubSprite` 寻址通,同设计 23 已打通的链路)。 |
| V4 | 改名指针闭环 | 点铅笔 → 输入框显出 → 输入新名 → 确认 → 名字刷新;重开窗口名字保留(落盘生效);输非法名 → 见拒绝提示(Log / 文本)。 |
| V5 | 三种关闭都生效 | 点 X / 点确定 / 点遮罩空白处 → 窗口关闭;点面板内容不穿透关窗。 |
| R | 不破坏 Classic / Merge(零回归) | 主菜单 → CLASSIC / 合成订单 DEMO 正常进、正常玩(本次换皮只加按钮 + 加 GameContext 一个成员,不动玩法);既有 EditMode 单测零回归(编译 0 error,设计 18 / 23 测试照过)。 |

> [!NOTE]
> **BLOCKED 条件**
>
> (1) unityMCP 桥不可达致 EditMode / Play 跑不起 → V 组判 BLOCKED 不判 FAIL(可备选 batchmode 跑 EditMode);(2) 头像 Sprite / 圆框 / 铅笔 / 下拉箭头切图缺失 = **预期占位**,不判 FAIL(本就占位,见 <a href="#25-player-info-window-art::placeholder">§3.2</a>)。

## 十、待拍板清单(范围开关,交 boss / 产品) {#open}

均有安全默认、本次换皮按默认推进、不阻塞;列出供 boss / 产品复核,要改另开增量。

| # | 开关 | 本次换皮默认 | 备选(要改另开) |
| --- | --- | --- | --- |
| D1 | 玩家信息纳入 GameContext 持有(vs 另套单例) | √ 纳入 `GameContext.Player`(延续设计 23 §五统一上下文预告) | 给 `PlayerInfo` 另套单例——与设计 23 既定方向相悖,不推荐 |
| D2 | 生日是否真做 + 入存档 | UI 占位(3 下拉对位,不绑数据 / 不入盘,[§5.3](#25-player-info-window-art::birthday)) | 真做:加 `PlayerInfo` 生日字段 + `MergeMetaSave` 字段 + 保底 + 单测(数据层增量,本次换皮范围外) |
| D3 | 头像三态选择网格是否本屏做 | 不做:本屏只显当前头像,编辑头像点击占位 | 本屏 / 后续屏接 `AvatarUnlockService.StateOf` + 换装 `TryEquip` 网格(设计 18 完整界面元素,效果图本屏未画) |
| D4 | 等级槽 / id 复制是否本屏补 | 不补:效果图本屏无该两位 | 后续屏接 `PlayerLevelConfig` 经验槽 + `ClipboardUtil` id 复制(设计 18 完整界面元素) |
| D5 | 改名交互形态 | 就地输入框(`m_text_Name`/`m_input_Name` 同位切换) | 独立 `RenameWindow` 弹窗(效果图无该窗,本次换皮不引入) |
| D6 | 入口落点 | 主菜单 `MainMenuWindow`(同设计 23,兑现既有 TODO 钩子) | 玩法内 HUD 顶栏左上头像入口(涉及 HUD 容器,后续轮) |

## 十一、风险表 {#risk}

| 风险 | 应对 |
| --- | --- |
| `GameContext.OnInit` 同步取玩家档与 `MergeMetaPersistence` 异步加载时序不一致([§5.2 B1](#25-player-info-window-art::holder)) | dev 二选一:路 A 由 `GameApp.StartGameLogic()` 在存档加载完成后注入(仿 `InitSettingsWithStore`);路 B 引用工程已同步持有的 DTO / PlayerInfo。两路验收等价。非方向问题,接线落点 dev 定。 |
| 改名后未落盘 → 重启丢失 | 改名成功 + 确定 / 关闭都调 `SavePlayerMeta`(`ExportToMeta` + 既有存档路径,[§5.2 B1](#25-player-info-window-art::holder))。验收 V4 实测重开保留。 |
| 缺图占位被误判为做错 | 圆头像框 / 铅笔 / 下拉箭头 / 头像 Sprite 缺失 = 预期占位([§3.2](#25-player-info-window-art::placeholder)),验收 BLOCKED 条款已声明不判 FAIL;美术补图后替子图 / Sprite 即生效,不返工。 |
| 生日擅自入盘溢出范围 | 本次换皮 UI 占位、零数据层改动([§5.3](#25-player-info-window-art::birthday) D2)。是否真做交 boss / 产品。 |
| `InputField` 类型与工程不一致(`UnityEngine.UI` vs TMP) | dev 按工程既有输入框类型选,`FindChildComponent<T>` 的 T 与 prefab 组件一致([§六](#25-player-info-window-art::window) warn)。 |
| 遮罩穿透 / z 序错致点面板也关窗 | 遮罩 `m_btn_Mask` 在节点树最底,`Root` 内容盖其上([§四](#25-player-info-window-art::tree) / [§八](#25-player-info-window-art::entry));验收 V5 实测点面板不穿透。 |
