# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:ui-rank-window 排行榜窗表现层(自治·纯 UI 补完·末屏)

兑现遗留 #27(rank 表现层)。设计稿 `design-docs/28-rank-window-art.html`(已注册 nav.js)。塔罗 UI 自治线纯 UI 补完范围最后一屏。
**art 受限**:塔罗素材无排行榜专属切图、无榜行底/名次徽章/头像切图 → 复用 Sheet_settings + 占位(纯色条/块+字符)+ TODO,视觉是结构占位、非高保真(已知限制,不判 FAIL)。

### 范式与数据层(经 grep 核实的真实符号)
- 数据层已就绪(命名空间 `GameLogic.Rank`,归档 `pipeline/archive/2026-06-14-rank-system/`,代码 `Assets/GameScripts/HotFix/GameLogic/Module/Rank/`):
  - `RankService(IRankSource, IRankPersistence, GameLogic.Mail.IMailService, IRankConfigSource cfg=null)`;字段 `NowProvider`/`OpenDate`。
  - 查榜:`RankBoard GetBoard(int rankId)`(榜不存在返 null);`(int rank,long score) GetMyRank(int)`;`(long score,long ticks,int nameTextId) GetMyBest(int)`;`void SubmitScore(int,long)`。
  - 领取:`RankClaimResult ClaimDaily(int)`/`ClaimPraise(int)`;红点 `bool HasClaimable`;结算 `List<SettleResult> CheckAndSettle(DateTime)`。
  - `RankBoard{ int Id; List<RankEntry> Entries; RankEntry Self; int SelfRank; long SelfScore; }`;`RankEntry{ int PlayerNameTextId; long Score; long AchievedTicks; bool IsSelf; int Rank; }`。
  - `RankClaimResult{ RankClaimStatus Status; int TextId; }`;`RankClaimStatus{ Success, NotRanked, NoReward, AlreadyClaimedToday }`。
  - 接缝:`LocalRankSource(Func<int,(long,long,int)> selfProvider, Func<int,IReadOnlyList<RankEntry>> filler=null)`;`RemoteRankSource`(返 `Array.Empty`,不连网);`RankPersistence`(键 `Rank.Progress`)/`InMemoryRankPersistence`;`RankConfigMgrSource`;`GameLogic.Config.RankConfigMgr`(`GetRank`/`All`/`EnsureLoaded`/`InitForTest(IEnumerable<RankDef>)`/`ResetForTest`)。
  - 测试夹具范式(`Assets/Editor/Tests/BlockBlast/RankSystemTests.cs`):`RecordingMailService : IMailService`(`Send` 记 `Sent`);`FixedSource`/`NewService` 注入写法;selfProvider 闭包循环用「先声明 svc、闭包捕获、后赋值」打破(SK1 / 持久化往返用例)。
- 窗口范式复用 `SettingsWindow.cs`/`PlayerInfoWindow.cs`/`GameContext.cs`:`[Window(Top,false)]` + 遮罩 + `FindChildComponent`/`m_` 前缀 + `SetSubSprite("Sheet_settings",子图名)` + `OnRefresh` 刷数据。
- GameContext 现持有 Settings + Player;本轮加 `RankService Rank` 成员(末项预告兑现)+ 测试注入入口 `InitRankWithDeps(...)`(仿 `InitSettingsWithStore`/`InitPlayerFromMeta`)。
- 入口:`MainMenuWindow.cs` 已有 BtnSettings(58-63)/BtnPlayerInfo(65-71),本轮照样加 BtnRank。

---

## 交接区(交开发 → 测试)

> 验收按「逻辑可 EditMode 单测(H/W 组)」与「需 Play / 人眼(V 组)」两档,详见设计稿 §九。占位项验收 = 点击不报错 + 数据真实 + 节点摆齐,不要求高保真图。

### 功能点 + 完成定义(test 可逐条核对)

**F1 · GameContext 扩持有 RankService(设计稿 §四)**
- 完成定义:`GameContext.cs` 加 `RankService Rank { get; private set; }` 成员;`OnInit` 装配生产实现(`LocalRankSource` selfProvider=`svc.GetMyBest`、filler=null;`RankPersistence`;`IMailService` 来源 dev 按工程现状取,见风险/B1);加测试注入入口 `InitRankWithDeps(IRankSource,IRankPersistence,IMailService,IRankConfigSource=null)`。
- 涉及:`Assets/GameScripts/HotFix/GameLogic/GameContext.cs`。
- 验收(EditMode):H2 — `GameContext.Instance.Rank` 非 null;多次 `Instance` 返同一 `RankService`;经 `InitRankWithDeps` 灌 `InMemoryRankPersistence`+`RecordingMailService`+`RankConfigMgr.InitForTest` 后可用、不污染真实 PlayerPrefs / 不连网。

**F2 · 窗口脚本 RankWindow(设计稿 §六)**
- 完成定义:新建 `RankWindow.cs`(命名空间 `GameLogic.UI`,落 `GameScripts/HotFix/GameLogic/UI/`),`[Window(UILayer.Top, location:"RankWindow", fullScreen:false)]`;`ScriptGenerator` 绑节点 + 接钮(onClick.AddListener,无 RegisterButtonClick);`OnCreate` 贴静态图(box1/box2/icon_x/button);`OnRefresh` 调 `Svc.GetBoard(RankId)` 刷列表 + 我的名次。
- 涉及:`Assets/GameScripts/HotFix/GameLogic/UI/RankWindow.cs`(新)。
- 验收(EditMode):H1 编译 0 error;W5 绑定 path 与节点树(§5.0)逐一对齐。

**F3 · prefab 节点树(设计稿 §5.0)**
- 完成定义:`RankWindow.prefab`(落 `AssetRaw/UI/Prefabs/`)按节点树摆:`m_btn_Mask`(全屏遮罩,树最底)/`Root`/`m_img_TitleBg`/`m_text_Title`/`m_btn_Close`/`m_img_PanelBg`/`m_scroll_List`(或固定槽容器)/`m_text_MyRankLabel`/`m_node_MyRank`(含 `m_text_MyRank`/`m_img_MyAvatar`/`m_text_MyName`/`m_text_MyScore`)/`m_btn_Bottom`;1080×1920 锚点,对位 排行榜.png。
- 涉及:`Assets/AssetRaw/UI/Prefabs/RankWindow.prefab`(新)。
- 验收(Play / 人眼):V2 对位效果图;V3 子图贴对(SetSubSprite 寻址通);V5 遮罩在最底、点面板不穿透。

**F4 · 榜单列表渲染 — 数据贯通(设计稿 §五)**
- 完成定义:`OnRefresh` 读 `RankBoard.Entries` 逐行渲染(名次=`RankEntry.Rank` 真实 / 名=`PlayerNameTextId` 占位查表显占位名或 id / 分=`Score` / `IsSelf` 本人行高亮);行底/名次徽章(金银铜)/头像 = 占位(纯色条/块+字符,名次数字始终真实)。行实现 dev 三选一:A 行 Widget+对象池(推荐,grep 工程真实 `UIWidget`/`RewardItemWidget`/对象池/`ScrollRect` API,别臆造)/ B 代码生成行 / C 固定 N 行槽(art 受限退路)。
- 涉及:`RankWindow.cs` + `RankRowWidget`(如取 A);列表容器 prefab。
- 验收(EditMode + Play):W3 — 行数 == `Entries.Count`、首行名次/名/分 == 数据(反射调渲染或抽纯方法「board→行 VM 列表」单测);若行强依赖 UGUI 实例化难纯测则 W3 退「映射纯方法单测 + V2 人眼核」。W6 — 空榜 / 未入榜不崩。V2/V4 人眼核渲染 + 滚动/固定槽。

**F5 · 我的名次条 — 数据贯通(设计稿 §6 RenderMyRank)**
- 完成定义:读 `board.Self`/`SelfRank`/`SelfScore`;`SelfRank>0` 显名次+头像占位+名+分;`SelfRank==0`(未入榜)显「未上榜」/「--」+ 当前最佳分。
- 涉及:`RankWindow.cs`。
- 验收(EditMode):W3 — 我的名次条文本 == `SelfRank`/`SelfScore`;W6 未入榜不崩。

**F6 · 点赞接 ClaimPraise(设计稿 §6.1,可选/默认省略)**
- 完成定义:效果图无点赞钮 → 默认不加按钮、留接线点(D2,安全默认)。若产品/后续要:加 `m_btn_Praise`,点击 `Svc.ClaimPraise(RankId)` → `RankClaimResult` 分支提示(Success/AlreadyClaimedToday/NoReward),奖进邮箱(排名层经邮件发,不在本窗弹奖),`OnRefresh` 刷态。
- 涉及:`RankWindow.cs`(若做点赞)。
- 验收(EditMode):W4 — 若做点赞:首次 `ClaimPraise`(本人入榜+榜有 PraiseRewardPoolId)→ `Success` 且 `RecordingMailService.Sent` +1;当天再点 → `AlreadyClaimedToday`;`PraiseRewardPoolId==0` 榜 → `NoReward`;验收 = 窗口委托 `Svc.ClaimPraise` 不自写。**不做点赞则 W4 = N/A**。

**F7 · 打开入口 + 三种关闭(设计稿 §八)**
- 完成定义:`MainMenuWindow.cs` 加 BtnRank(照 BtnPlayerInfo 做法约 5 行)→ `ShowUIAsync<GameLogic.UI.RankWindow>()`;关闭三路(X `m_btn_Close` / 遮罩 `m_btn_Mask` / 底部按钮 `m_btn_Bottom`)→ `CloseUI<RankWindow>()`(底部「再来一次」默认关窗,语义 D3)。
- 涉及:`MainMenuWindow.cs`。
- 验收(Play):V1 能从入口打开无报错;V5 三种关闭都生效、点面板不穿透。

**R · 零回归(设计稿 §九 R)**
- 完成定义:只加按钮 + GameContext 一个成员,不动玩法;Classic / 合成订单 DEMO 正常进玩;既有 EditMode(设计 22/23/25 测试)零回归、编译 0 error。
- 验收:全量 EditMode 绿(BlockBlast 既有 + 新增 rank window 用例);`git diff` 仅本窗 + GameContext + MainMenuWindow + 新 prefab。

### 本轮实做 vs 占位分流(设计稿 §3.2/§七)
- **接真实数据**:榜单列表(名次/名/分)、我的名次条、(可选)点赞 ClaimPraise。
- **占位 TODO(art 受限)**:榜行底(纯色条,前三名暖金/银/铜区分)/名次徽章(纯色圆+字符)/头像(纯色块)。
- **不做(本屏,效果图无)**:多榜页签(D1 默认单榜,留接线点)/点赞按钮(D2 默认省略)/奖励预览(D5)/结算触发(D4 默认不起)/红点(D6 在入口侧)。

### dev 须 grep 核实(别臆造)
- 行渲染用的 `UIWidget`/`RewardItemWidget`/对象池/`AdjustIconNum`/`ScrollRect` 真实签名(§五推荐方案 A;取不到退方案 C 固定槽)。
- `IMailService` 实例来源(§四 B1):复用未来 GameContext 持有的 MailboxService / 现场 new / no-op stub 三选一,记 dev 交接区;本屏验收不依赖奖真进邮箱(BLK2)。

### BLOCKED(无安全默认 / 依赖未建,结束呈报,非 FAIL)
- BLK1:真实全服榜 + 真实他人成绩 — 无网络模块,`RemoteRankSource` 返空,本屏榜单 = 本机 + 配置/注入陪榜(本轮 filler=null,榜可能只本机一条)。解阻 = 网络模块就绪后实现远程源,服务层零改动切注入。
- BLK2:点赞奖真进邮箱可见 — mail 表现层(遗留 #26)未做,点赞奖经邮件发但无邮件窗看不到。本屏验收只锚 `ClaimPraise` 返码 + 数据层调用(W4)。

### 自治决策(安全默认,记 decisions 不入 blockers;boss 关单复核,要改另开增量)
- D1 多榜页签:默认单榜(对位效果图无页签);备选加页签按 `RankDef.Group` 切换。
- D2 点赞按钮:默认省略(效果图无);留 `ClaimPraise` 接线点。
- D3 底部「再来一次」语义:默认关窗;备选接 `ShowUIAsync<GameWindow>` 再开一局(需定从哪玩法)。
- D4 结算触发:默认不在本屏起 `CheckAndSettle`(避免开窗副作用);交后续登录/tick 流程。
- D5 奖励预览:默认不做(效果图无位);后续屏接 `TierForRank.ShowRewardPoolId` + 17 RewardView(阻塞于奖励图标美术,同遗留 #20)。
- D6 入口红点:本屏内不显;`HasClaimable` 供入口侧 icon 红点(后续轮)。
- D7 行实现方案:推荐 Widget+池(A),art 受限可退固定槽(C),三方案验收等价。

---

## 产出取件
- 设计稿:`design-docs/28-rank-window-art.html`(nav.js 已注册 28;断链/锚点/无BOM/lint 全过,`node --check` 通过)。
- 交接区:本文件「交接区」节(F1-F7 + R + 分流 + decisions/BLOCKED)。
