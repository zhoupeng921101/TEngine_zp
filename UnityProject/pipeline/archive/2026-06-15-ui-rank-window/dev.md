# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:ui-rank-window 排行榜窗表现层(设计 28,兑现遗留 #27)

状态:**dev 自检通过,交测试**。编译 0 error;EditMode 全量 407 passed / 0 failed / 0 skipped(含新增 8 例 RankWindowTests + 既有零回归)。V 组(Play/人眼)留 boss 桥跑兜底。

---

## 交接区(交开发 → 测试)

### 改动摘要

按设计稿 28 + plan F1-F7 落地排行榜窗表现层,复用设置/个人信息窗范式(`[Window(Top,false)]` + 遮罩 + GameContext 持服务 + SetSubSprite 取图 + OnRefresh 刷数据),不改数据层 / 玩法 / Settings / Player。

- **F1**:GameContext 扩持有 `RankService Rank`,OnInit 装配生产实现,加测试注入入口 `InitRankWithDeps`。
- **F2/F4/F5**:新建 RankWindow(查榜 → 渲染榜单列表 + 我的名次条,占位行底/徽章/头像,名次/名/分真实)。
- **F3**:RankWindow.prefab 节点树(对位效果图,1080×1920,根 Canvas WorldSpace + stretch)。
- **F6**:点赞默认省略(效果图无,D2),留接线点注释。
- **F7**:MainMenuWindow 加 BtnRank → ShowUIAsync<RankWindow>。

### 关键决策(自治默认,boss 关单复核)

- **B1 邮件服务来源**:取路②真实发奖出口 `new MailboxService(new MailPersistence())`(键 `Mail.Inbox`,设计 21),非 no-op stub。理由:MailboxService 类头已声明「下轮排行榜接此真实服务而非再 stub」;点赞/结算奖经它真实下发进收件箱存档,邮件表现层(#26)落地后零改动复用同一持久化键即在邮件窗可见。本屏验收不依赖奖真进邮箱(BLK2)。
- **行渲染方案**:取**方案 B 代码生成行**(plan D7 等价退路,art 受限)。理由:Widget+池路线(方案 A)需「配同名行 prefab + 收集器寻址 + CreateWidget 实例化」,工程内连既有 RewardItemWidget 都尚未接 prefab(美术/投放是独立后续),铺通成本高且零运行先例;代码生成行 prefab 只需空 Content 容器,用原生 UGUI 在 1080×1920 坐标系建行(不套 BlockLayout 750 私有坐标系)。「board → 行 VM 列表」抽成纯静态方法 `RankWindow.BuildRowModels` 便于单测(W3 锚点);我的名次条文本同样抽 `MyRankText/MyNameText/MyScoreText` 纯方法。
- **D3 底部「再来一次」**:默认关窗(安全默认)。
- **D1/D4/D5/D6**:多榜页签/结算触发/奖励预览/红点 本屏不做,按 plan 默认。

### 文件清单

新增:
- `Assets/GameScripts/HotFix/GameLogic/UI/RankWindow.cs`(+.meta) — 窗口脚本(热更区)
- `Assets/AssetRaw/UI/Prefabs/RankWindow.prefab`(+.meta) — 节点树(AssetRaw/UI 按文件名寻址)
- `Assets/Editor/Tests/BlockBlast/RankWindowTests.cs`(+.meta) — EditMode 测试(asmdef BlockBlast.Tests)

修改:
- `Assets/GameScripts/HotFix/GameLogic/GameContext.cs` — 加 Rank 成员 + InitRank() + InitRankWithDeps()(Settings/Player 一行不改)
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MainMenuWindow.cs` — 加 BtnRank 入口(照 BtnPlayerInfo,约 7 行)

### 验证点(逐条对应验收标准)

**已 dev 自检通过(EditMode,test 复跑核):**
- H1 编译 0 error:`read_console` 过滤 `error CS` 0 条;EditMode 能跑起来即程序集编译通过。
- H2 GameContext 持 RankService:`RankWindowTests.H2_GameContext_HoldsRankService_SameInstance`(非 null + 多次 Instance 同一服务)、`H2_InitRankWithDeps_InjectsTestSeams`(InMemoryRankPersistence + RecordingMailService 注入后查榜可用、不发邮件、不依赖真实 PlayerPrefs)。
- H3 查榜数据贯通:`H3_GetBoard_ViaContext_SortsAndFillsRank`(分降序 + 名次 1 起回填 + 本机 IsSelf + SelfRank/SelfScore 一致)。
- W3 board→行 VM + 我的名次条文本映射:`W3_BuildRowModels_MapsEntries`(行数==Entries.Count、首行名次/名/分==数据、本机行 IsSelf)、`W3_MyRankText_InBoard`(MyRankText==名次、MyScoreText==★分、MyNameText==占位名)。
- W6 空榜/未入榜/null 不崩:`W6_NotRanked_FriendlyState`(未入榜显「--」/「未上榜」+ 最佳分)、`W6_EmptyAndNullBoard_NoThrow`(null/Entries null/空 Entries 三态)、`W6_NonexistentBoard_GetBoardNull_NoThrow`(GetBoard 返 null)。
- W4 点赞:**N/A**(本屏默认省略点赞按钮,D2)。
- W5 绑定 path 对齐 prefab:ScriptGenerator 各 FindChildComponent path 与 prefab 节点树逐一核对一致(test code review 核;Play 打开无 FindChild null 报错并入 V 组)。已 dev 自核:
  - `m_btn_Mask` / `Root/m_btn_Close` / `Root/m_btn_Bottom` / `Root/m_img_PanelBg` / `Root/m_img_TitleBg`
  - `Root/m_scroll_List/Viewport/Content`(_listRoot)
  - `Root/m_node_MyRank` + `/m_text_MyRank` / `/m_text_MyName` / `/m_text_MyScore` / `/m_img_MyAvatar`
- R 零回归:全量 EditMode 407 passed / 0 failed(BlockBlast.Tests 既有 + 新增 rank window 用例);git diff 仅本窗 + GameContext + MainMenuWindow + 新 prefab + 新测试。

**须 Play / 人眼核(V 组,boss 桥跑):**
- V1 从主菜单 BtnRank 打开窗口无报错。
- V2 对位 排行榜.png:标题木牌/关闭/榜单列表(前三名徽章占位色区分)/「我的排名」分隔/我的名次条/底部按钮 位置大致一致;榜行名次/名/分逐行显示对。占位图朴素允许、位置+数据须对(art 受限)。
- V3 切图贴对:box1(主面板)/box2(标题板)/icon_x(关闭)/button(底部条)经 SetSubSprite 寻址正确显示。
- V4 滚动:列表套 ScrollRect(垂直)+ Content 顶锚,条目多于可见区可滚动。
- V5 三种关闭(X / 遮罩空白 / 底部按钮)都关窗;点面板内容不穿透(遮罩 m_btn_Mask 在节点树最底)。
- R(Play 部分):CLASSIC / 合成订单 DEMO 正常进玩(只加按钮 + GameContext 一个成员,不动玩法)。

### 标注

- **涉及热更程序集**:是。RankWindow.cs / GameContext.cs / MainMenuWindow.cs 在 `GameScripts/HotFix/`(全热更)。
- **需 Luban 重生成**:否(无新表 / 无配置改动,复用既有 RankConfigMgr)。
- **需进 Play 手验的功能点**:V1-V5 + R 的 Play 部分(见上)。
- **运行期寻址前置**:新增 RankWindow.prefab 落 `AssetRaw/UI/Prefabs/`(收集器按文件名收录)。EditMode 不验运行期取图(YooAsset default package 未 SetDefaultPackage,SetSubSprite 在 EditMode 抛「Default package is null」是预期,见 dev memory)。Play 启动会自动重建模拟清单 location;若 boss 不重启编辑器直接 Play,框架启动流程会建包。
- **prefab 静态文本字体**:m_text_Title / m_text_MyRankLabel / m_text_*  等静态文本节点 prefab 内未绑 Font(MCP 不便赋内置字体引用)→ Play 下可能不显字(代码生成的榜行文本在运行期由 `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` 赋字,正常显)。属 art 受限占位的已知限制,补美术 / 在编辑器给静态文本赋字即生效,不影响结构与数据贯通。V2 人眼核时若静态标题不显字,非逻辑 FAIL。

### BLOCKED / 已知限制(非 FAIL,预期占位)

- BLK1 真实全服榜:无网络模块,RemoteRankSource 返空,filler=null → 离线榜可能只本机一条 + 注入陪榜。本屏渲染以本机条目正确为准,不要求满榜。
- BLK2 点赞奖真进邮箱可见:mail 表现层(#26)未做,点赞奖经 IMailService.Send 真实下发但无邮件窗看不到。本屏点赞默认省略(D2),验收只锚数据层调用(W4=N/A)。
- 行底/名次徽章(金银铜)/头像 = 占位纯色 + 字符 + TODO(塔罗素材无排行榜专属切图,设计 28 §3.2),名次/名/分真实。

### 给 boss 的范围提示(非阻塞,报备)

git working tree 含若干**非本任务**改动:`ProjectSettings/ProjectSettings.asset`、`UserSettings/EditorUserSettings.asset`、`Assets/PSD2UGUI/.../TranslationCache.json` 是 Unity 编辑器在 MCP 操作期间自动写的(我未手编);`CLAUDE.md` / `pipeline/*` / `design-docs/*` 是流水线前序环节产物。本任务的代码/资产改动仅:GameContext.cs / MainMenuWindow.cs / RankWindow.cs(+meta) / RankWindow.prefab(+meta) / RankWindowTests.cs(+meta)。关单打包时建议**不要把 ProjectSettings / EditorUserSettings / TranslationCache 的自动改动并入本变更**。

---

## 产出取件
- 设计稿:`design-docs/28-rank-window-art.html`
- 交接区:本文件「交接区」节
- 测试范式参:`Assets/Editor/Tests/BlockBlast/RankWindowTests.cs`(InitRankWithDeps 注入口径)
