# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前被测任务:ui-settings-window 设置窗美术换皮(2026-06-15,常规模式·用户在场)

设计稿 `design-docs/23-settings-window-art.html`;兑现遗留 #24。本工程第一个美术驱动 UI 窗口 + 切图寻址基础设施 + GameContext 运行期上下文,将成为后续约 20 个界面换皮的范式。

### 总判定:PASS

四类验证全过,V2 寻址链路独立复验通过,寻址范式技术结论成立。两个非阻塞观察项(孤儿图集资源待清、dev 交接区两处数字笔误)单列,不影响关单。

---

## 验证一:编译 + 单元测试 — PASS

- **编译**:`refresh_unity` 后 `isCompiling=False`;`read_console`(error/warning,且筛 `CS` 编译诊断)0 命中。编译 0 报错 0 警告。
- **EditMode 全量**:`run_tests` EditMode = **366/366 PASS**,0 failed / 0 skipped(`resultState=Passed`)。既有 settings 数据层零回归(294 例口径含在 366 内,无新增失败)。
- **GameContextTests 真验(非空跑)**:反射枚举该 fixture 实得 **7** 个 `[Test]` 方法(AudioSink / H1 / H2 / H3a / H3b / W2_Music / W2_Sound)。逐例 new fixture → invoke → teardown 实跑,7 例全 PASS。配合对 `SettingsService` 源码核读,确认断言真验到行为:
  - H1 验单例同实例 + 同一 SettingsService;
  - H2 注入空 InMemory store 验默认全开;
  - H3a/H3b 验同实例保真 + Release 后从同一 store 复 Load 读回上次值(持久化往返);
  - W2×2 验切换后内存态 + 存储键 `MusicMuted/SoundMuted` 取反落盘;
  - AudioSink 验 sink lambda 被调且收到当前态。
  - 均非自洽/恒真断言。
- **证据**:job `1866c3b3...` / `517c1e07...`(均 366/366);`execute_code` 反射逐例 invoke 全 PASS。

> 偏差(非缺陷):dev 交接区写「8 例」,实为 7 例(H1/H2/H3a/H3b/W2×2/AudioSink 本就是 7 项,数字笔误)。7 例覆盖设计稿 §9.1 H/W 组全部验收点,无覆盖缺口。

## 验证二:Code Review — PASS

被测:`GameContext.cs` / `SettingsWindow.cs`(并核 `GameApp.cs` / `MainMenuWindow.cs` diff)。

**五条编码红线(对照项目根 CLAUDE.md 正本逐条)**:
1. **异步优先**:新代码无 `LoadAssetAsync` / `Resources.Load` / `.WaitForCompletion` / `StartCoroutine`;`SetSubSprite` 内部 UniTask 异步。PASS。
2. **模块访问**:`GameModule.UI.CloseUI/ShowUIAsync`、`GameModule.Audio.MusicEnable/SoundEnable`;无 `ModuleSystem.GetModule<T>()`。PASS。
3. **资源释放**:核 `ResourceExtComponent.SubSprite.cs` 实现确认——`SetSubSprite` 经 `SubSpriteReference`(挂目标 GameObject)+ `_subSpriteReferences` 计数自管,Image 销毁时 `OnDestroy → DeleteReference` 配平。窗口每 Image 仅 OnCreate 贴一次,无重复计数;无裸 `LoadAssetAsync<Sprite>`。PASS。
4. **热更边界**:4 文件全在 `GameScripts/HotFix/GameLogic`。AudioSink 从 ProcedureLaunch(非热更 Assembly-CSharp)挪到 `GameApp.StartGameLogic()`(热更入口)是对程序集边界的正确修法。PASS。
5. **事件解耦**:UI 内用 `onClick/onValueChanged.AddListener`;本轮无跨模块事件需求。PASS。

**基础设施接口(GameContext,将被 player-info/item/mail/rank 挂入)**:
- 接口薄而通用:仅 `Settings` 属性 + `OnInit` + `InitSettingsWithStore` 测试接缝,无投机性预建成员。符合设计 §五「薄」。
- 单例生命周期:`SimpleSingleton<T>` 懒初始化,`OnInit` 用生产 `PlayerPrefsSettingsStore` + `Load`;`Release` 置空。Load 时机正确(首次 Instance 即加载)。
- 测试注入接缝 `InitSettingsWithStore(ISettingsStore)` 干净,无测试代码泄漏进生产路径。

> 观察(非缺陷):`InitSettingsWithStore` 为 public,生产侧理论可调换 store。属本工程既有测试注入范式,无害;不阻塞。

**遮罩 z 序穿透核查**:prefab `m_btn_Mask` 是 root 下首个子节点(最先绘制=最底),`Root`(承载面板内容)在其后绘制,面板盖在遮罩上。设计 §十二风险「遮罩穿透」已规避。

## 验证三:Play 手验 — PASS(MCP 可做项全过,人耳/真机点击留既有遗留)

进 Play 模式(bootstrap 完成、MainMenuWindow 已加载),走真实 UI 路径 `ShowUIAsync<SettingsWindow>` 开窗。

- **V1 入口/开窗**:`GameObject.Find("SettingsWindow")` 命中,窗口实例化成功。主菜单设置入口按钮已加(MainMenuWindow diff 核实)。点击关窗 onClick 已接 `CloseUI<SettingsWindow>`(指针点击留人验)。PASS(可验部分)。
- **V2 切图寻址(本轮成败核心,独立复验)**:
  - 源层:Play 中 `LoadSubAssetsSync<Sprite>("Sheet_settings")` Succeed,**SubAssets sprite count=21**,21 个子图按名(base_plate/box1/box2/button/chat/clear/exit/facebook/game/help/icon_x/instagram/language/Player_music/printer/twitter/Volume_up/x/youtube...)全取得到。
  - 端到端:开窗后窗内 27 个 Image,**26 个 sprite 非空**,唯一无 sprite 的是 `m_btn_Mask`(设计即纯色遮罩、无切图)→ 26/26 应贴图节点全贴上。逐节点映射与 `SettingsWindow.cs` OnCreate 意图逐条一致(Close→icon_x、面板→box1/box2、社交→5 平台、长条→base_plate3+chat/game/language/exit、下排→button+clear/Player_music/Volume_up/help/printer)。
  - 视觉对位:截图 `Assets/Screenshots/screenshot-20260615-024019.png` 与基准 `setting.png` 比对——标题木牌 / 关注我们+5 社交 / 4 长条 / 下排图标按钮版面、比例、木纹美术faithful 匹配。
  - 寻址链路「切图 → 精灵表 PNG → 收集器 → SetSubSprite → 显示」100% 打通,模板成立。PASS。
- **V3 音效切换持久化**:走生产真实栈(非 InMemory)验——`GameContext.Instance.Settings.SetSound(false)` → SoundOn=False + PlayerPrefs `SoundMuted`=True(落盘,on→muted 取反正确);`SetSound(true)` → SoundOn=True + `SoundMuted`=False(往返)。AudioSink 已接(生产侧 GameApp 接的),全链 Apply→sink→GameModule.Audio 无异常。真实实听留人耳遗留。PASS(数据侧)。
- **V4 不破坏 Classic/Merge**:EditMode 366 全绿 + 加法式改动(仅新增 + GameApp/MainMenu 加行,不动玩法窗);Play 全程无游戏运行期报错(唯一 console error 是 MCP-FOR-UNITY 退 Play 时的 disposed-object 基础设施消息,非游戏码)。玩法手感留既有遗留。PASS(可验部分)。
- **V5 占位有反馈不死按钮**:反射调 7 个占位 handler(OnContact/OnLanguage/OnMoreGames/OnLogout/OnHelp/OnSocial/OnClearSave)全无异常,`read_console` 见 7 条「[设置窗·待建] ...」Log 反馈。协议/隐私接 OpenURL(占位 URL)。PASS。

## 验证四:寻址范式审查(影响后续约 20 屏)— 结论:**采纳精灵表 PNG 为范式**

详见末尾「寻址范式审查结论」专节。

---

## 额外核查

- **孤儿图集资源(观察项,建议清理,不阻塞)**:`Assets/AssetArt/Atlas/Atlas_setting.spriteatlasv2`(+ .meta,单数 setting)仍在磁盘。其 GUID `f11d40e65453d144ba58c6fa5b4ced07` 全工程仅被自身 .meta 引用 = 真孤儿;落在 `AssetArt/Atlas/`(不被 YooAsset 收集器收录),运行期无害,但属误导后续 + 无用残留,**建议删除**(连同 dev 称已删的 `Atlas_settings` 复数版,核实复数版已不在 `UIRaw/Atlas/`,仅余此单数孤儿)。dev 交接区只提「Atlas_settings 一度建过又删除」,未提此单数 `Atlas_setting` 孤儿。
- **切图数量笔误(观察项)**:dev 交接区/plan 写「22 张切图」,实际 `setting/` 下 21 张 PNG + `Sheet_settings.png` 精灵表 21 子图。数字笔误,不影响功能(21 子图全寻址通)。
- **持久文件交叉检(conventions「交叉检」)**:对 dev 改过的 `pipeline/state/dev.md`、`pipeline/memory/dev.md` 跑 lint(指代词/diff 叙事/拟人比喻)+ 人工通读——均 0 命中、语体平实、新增 memory 3 条(SpriteAtlas 不工作 / WorldSpace Canvas / ProcedureLaunch 程序集边界)脱离对话成立、术语合法、跨任务可复用,准入合格。`design-docs/23`(plan 产物,untracked)同跑 lint 0 命中。

---

## 寻址范式审查结论(单列,影响后续约 20 屏)

### 1. SpriteAtlas v2 取不到子图——dev 技术结论成立(独立核实)

dev 称「SpriteAtlas v2 子精灵不向 YooAsset SubAssets 暴露(count=0)」。独立核实 **成立,非配置疏漏**:

- YooAsset 在 Editor SimulateBuild 路径(dev 与本轮测试都走此路径)的 SubAssets 加载实现是 `AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)`(`VirtualBundleLoadSubAssetsOperation.cs:62/66`)。
- 对 **Multiple 模式 Texture**(精灵表 PNG):该 API 返回 21 个命名 Sprite 子表示(本轮实测 count=21)。
- 对 **`.spriteatlasv2`**:该 API 对图集资源路径返回的是图集自身表示,**不含被打包的各 Sprite**——打包的 Sprite 在 AssetDatabase 里仍是其**源 PNG** 路径的子表示,不是图集文件的子表示。SpriteAtlas 是按 GUID 引用 Sprite 的打包容器,不「拥有」这些 Sprite 作为自身子对象。故 `GetSubAssetObjects<Sprite>(图集location)` = 0。
- 有无被忽略的配置能救?无。`Include in Build`/bindAsDefault 只影响运行期图集纹理是否加载以在渲染时替换 Sprite 引用,不改变 AssetDatabase 子表示枚举结果。「图集作单一 location + 子图名寻址」与 YooAsset 的 `LoadSubAssetsAsync` 枚举机制根本不兼容。
- 唯一让 SpriteAtlas「相关」的路子是对每张**源 PNG** 各自 `LoadSubAssetsAsync`——但那正是「散图直接寻址」,不是「每屏一图集单 location」。

**结论:dev 从 SpriteAtlas v2 改精灵表 PNG 的技术判断正确,API 调用形态(每屏一图集资源 + `SetSubSprite(location, 子图名)`)与用户拍板一致,属等价替代。**

### 2. 精灵表 PNG 作项目级范式——可靠,但须补一项工具化

- **与工程既有范式同源**:工程 `UIRaw/Atlas/` 既有 653 张 Multiple 模式 PNG(blocks_main_* 等,各 1 子图);收集器组 `UIRaw/Atlas`(AddressByFileName/PackDirectory)已收录该树。精灵表 PNG 落同处即可寻址,无新增收集器配置。且全工程此前从未调过 `SetSubSprite`(仅本窗调),本轮是范式从零落地。
- **优于「散图直接寻址」**:散图各自作 location 会重蹈塔罗素材跨界面**全局重名**问题(`x`/`icon_x`/`setting` 等被导入器去重/覆盖,设计 §读前必看第 3 条),且 drawcall 不合批。精灵表把命名空间收进单张图(子图名只需**屏内**唯一),合批省 drawcall,更贴「每屏一图集」精神。范式选型正确。
- **唯一隐患——制作流程未工具化(建议补,非阻塞本轮)**:`Sheet_settings.png` 是把 21 张切图打包成单张 PNG + Multiple 模式切 21 命名子精灵(子图名=源切图文件名)。工程内**无任何 Editor 打表工具**(搜 PackTextures/SpriteMetaData/Packer 在 `Assets/Editor` 零命中),dev 交接区/memory 描述的是一次性脚本式生成。20 屏每屏手工打一张表 = 重复手工成本 + 易错(子图名漏改、rect/pivot/border 手设)。
  - **建议**:在范式复制到下一屏前,把「散切图目录 → 合成 Multiple 精灵表 PNG(子图名=文件名、自动切 rect、九宫格 border 可配)」做成一个 Editor 菜单工具沉淀下来。本轮单屏可接受手工,但作为「后续 20 屏范式」,工具化应在第二屏前补齐,否则手工成本会随屏数线性累积。此项交 boss/plan 排期,不阻塞本轮关单。

---

## 返回 boss

- **总判定:PASS**
- 报告路径:`pipeline/state/test.md`(本文件)
- 关单建议:可关单。两个观察项请 boss 知会/排期:① 清理孤儿 `Assets/AssetArt/Atlas/Atlas_setting.spriteatlasv2`(+.meta);② 范式工具化(散图→精灵表 Editor 工具)在第二屏换皮前补齐。dev 寻址技术偏离(SpriteAtlas→精灵表 PNG)经独立复核为正确等价替代,予以确认。
