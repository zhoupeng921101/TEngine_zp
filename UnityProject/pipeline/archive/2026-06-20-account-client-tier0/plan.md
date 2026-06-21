# plan 状态

当前任务:Tier 0 真实账号体系 · 客户端段 · 第 2 子单(登出接线 + 自动登录链路审计)

## 范围与决策(本子单 = 客户端段收口,自治默认推进)

- **设计稿**:新建 `design-docs/36-account-client.md`(`nav.js` GROUPS 已在「全栈 / 服务端」组 35 之后 33 之前注册 `36-account-client.html` + 详细 desc);同步改写 `design-docs/19-settings-system.md` §一 #10 / §一总结段 / §3.6 末段 / §七 O8 / §八风险表(server 段 35 上线后「离线无账号系统」前提推翻,conventions §6 触发,本任务内同步,不留给后续轮)
- **范围**:严守 boss brief 「客户端登出按钮接线 + LoginUI 接线 + UUID 迁移确认」三件事的现状审计 + 接线;**不做** LoginUI 实做 UI(Tier 1+)、账号切换 / 多账号(Tier 3)、OpenID / 邮箱绑定 / 跨设备恢复(Tier 3)、PlayerPrefs UUID 改造(简报误读,且会改 account 主键值的派生路径致既有四特性 account 字段漂移)、二次确认弹窗(工程无组件,Tier 1+)、踢号 / 封号(Tier 1+ 运营后台)
- **现状审计关键发现**(boss brief 的三项「现状不明」全部已核源码,记录于设计稿 §一):
  - **自动登录链路已无声跑通**:`GameApp.StartGameLogic` 第一行 `FantasyClient.FantasyNetwork.Boot()` → `FantasyNetwork.cs` Boot 内部连服 → `OnConnectComplete` 自动调 `LoginAsync(account)` → `AutoEnterGame=true` 自动 `EnterGame()` 发 `C2M_InitComplete` → 主菜单窗直接打开(`GameModule.UI.ShowUIAsync<MainMenuWindow>()`)。全程无 UI 等待点
  - **LoginUI 是真空壳**:`Assets/GameScripts/HotFix/GameLogic/UI/LoginUI/LoginUI.cs` 11 行无字段无逻辑;`IEvent/ILoginUI.cs` 接口定义;**全工程零** `ShowUIAsync<LoginUI>` 调用、零 LoginUI 实例化点(已 grep 全工程核实)— 是「玩家无感」**设计意图**而非漏接
  - **UUID 来源是 SystemInfo.deviceUniqueIdentifier**(不是 PlayerPrefs):`Assets/Fantasy/Scripts/FantasyNetworkConfig.cs::DefaultAccountName()` 取 SystemInfo.deviceUniqueIdentifier + `"dev_"` 前缀;`unsupportedIdentifier` 回退到字面量 `"editor"`;全工程 `PlayerPrefs` + `deviceUniqueIdentifier` 结合点为零。boss brief「客户端 PlayerPrefs UUID 是否真在用、谁生成」结论 = **不在用 PlayerPrefs**
  - **设置窗登出按钮基础设施已就位**:`SettingsWindow.cs` 第 42/43/44 行字段、76/80/84 行节点查找、117 行 `onClick.AddListener(OnLogout)`、143 行 `base_plate3` 贴底图、147 行 `exit` 贴 Icon;**仅 OnLogout(第 202 行)是 `ShowPlaceholder("离线版无账号系统(设计 19 §七 O8)")` 占位**,这是本子单唯一替换点
  - **客户端门面已暴露的登出能力**:`FantasyNetwork.Shutdown()`(第 211 行,标 `_intentionalClose=true` + `Scene.Dispose` 级联清连接 + 心跳 + 清四态 + 不触发自动重连)+ `FantasyNetwork.Boot()`(第 55 行,重置 `_intentionalClose=false` + 复用 / 重新初始化运行时 + 连服 + 自动登录 + 自动进游戏)— 两者顺次调 = 完整登出语义

- **安全默认采纳**:
  - **LoginUI 不上线,保留真空壳作 Tier 1+ 接口余量**(D1):自动登录链路已无声跑通,boss brief 的「显式状态 + 进入游戏按钮」默认会动 GameApp 启动序列(玩法主入口路径回归面)或 LoginUI 一闪而过(无 UX 价值);Tier 0 GDD「玩家无感」明示不需要;现有 `LoginUI.cs` + `ILoginUI.cs` 真空壳保留作 Tier 1+ 真做账号 UI 时的接口余量
  - **设置窗登出按钮 = 实做**(D2):按钮 + 子图 + 节点 + 监听全已就位,占位文案在 35 上线后过时,这是本子单唯一可观测 UX 改动
  - **登出语义 = 断当前会话 + 立即重新走自动登录**(D3):顺次调 `Shutdown() → Boot()`;玩家「登出」的自然意图 = 「重新登录到同一账号」,UUID 不变(SystemInfo 派生,玩家无法清);GameApp 启动「一次性」无「回登录前态」UX 设施新建超本子单范围;账号切换 / 与服务端断开后停在登录前 / 多账号管理是 Tier 1+ / Tier 3 范围
  - **不引入 PlayerPrefs UUID,沿用现状 SystemInfo 派生**(D4):简报误读;引入 = 改 account 主键值派生路径 = 既有 30 / 31 / 32 / 33 业务集合的 account 字段值漂移 = 违 35 守不变量
  - **登出反馈走 ShowPlaceholder 兜底,文案改现状语义**(D5):工程暂无确认弹窗组件(同 19 §六 V5 OnClearSave 也走 ShowPlaceholder Log.Info),不加二次确认;文案如「已断开连接,正在重新登录…」,不再写「离线版无账号系统」
  - **19 设计稿同步改写**(D6):conventions §6 「现行体系仍有活引用 → 覆盖式重写 + 改一处即同步被它过时的他篇」;本任务内已改 §一 #10 / §一总结段 / §3.6 末段 / §七 O8 / §八风险表

## 守不变量(给 dev / test 的硬边界)

- **客户端工程 `Assets/` git diff 只命中一文件**:`Assets/GameScripts/HotFix/GameLogic/UI/SettingsWindow.cs`;**其它任何路径零改动**:
  - `Assets/GameScripts/HotFix/GameLogic/GameApp.cs`(热更入口 StartGameLogic 启动序列不动)
  - `Assets/Fantasy/Scripts/FantasyNetwork.cs`(网络门面 Shutdown / Boot 等 API 不动,无新增方法)
  - `Assets/Fantasy/Scripts/FantasyNetworkConfig.cs`(UUID 派生路径 + 服务器地址 + 自动登录开关全不动)
  - `Assets/GameScripts/HotFix/GameLogic/UI/LoginUI/LoginUI.cs`(11 行真空壳保留作 Tier 1+ 接口余量)
  - `Assets/GameScripts/HotFix/GameLogic/IEvent/ILoginUI.cs`(接口定义保留)
  - `Assets/GameProto/`(无新协议生成物)
- **不动 server 段协议契约**:无新 proto、无新客户端可见 RPC、无新字段;LoginGameRequest / LoginGameResponse 协议字段集 1:1 沿用
- **既有四个全栈特性 E1 真往返不被破坏**:本子单不动 account 派生路径(D4) + 不动登录链路(GameApp / FantasyNetwork / FantasyNetworkConfig 全不动);兑换码 30 / 排行榜 31 / 邮件 32 / 结算 33 的 E1 真往返各自已 PASS 验收点须仍 PASS(零回归)
- **不动 Tier 1+ / Tier 3 范围**:不实做 LoginUI UI / 不做账号切换 / 不做 OpenID / 不做邮箱绑定 / 不做跨设备恢复 / 不投机性预留 Tier 1+ 接口余量(真空壳 LoginUI.cs + ILoginUI.cs 保留即足够)

## 客户端段交付清单(给 dev)

> dev 开工第一步 = **核 36 §一现状审计的接线点是否仍是「`SettingsWindow.cs::OnLogout` 第 202 行」**(若设置窗已被他人改动,接线点行号可能变,但仍是 `OnLogout` 方法体),其它都不动。

### 客户端落地清单(按 36 §三登出语义 + §七 CV 表)

| 落点 | 行为级完成定义 |
| --- | --- |
| `SettingsWindow.cs::OnLogout` 方法体改写 | 由 1 行 `ShowPlaceholder("离线版无账号系统(设计 19 §七 O8)")` 改为 3 行**顺次**调用:① `ShowPlaceholder("已断开连接,正在重新登录…");`(或等价现状语义中文短文案,不再含「离线版无账号系统」类过时表述);② `FantasyClient.FantasyNetwork.Shutdown();` ③ `FantasyClient.FantasyNetwork.Boot();`(无参,沿默认走 `DefaultAccountName()` 派生)|
| 不夹任何分支 / 等待 / 异步 await | 三步顺次 = 三个调用,中间不加 if / 不加 await / 不加新方法定义 / 不加新字段;UI 主线程不卡 |
| 文案常量现状化 | 文案改为现状语义(示例「已断开连接,正在重新登录…」),不再写「离线版无账号系统(设计 19 §七 O8)」类过时表述 |
| 设置窗自身不关闭 | 沿用现状,OnLogout 不调 `Close()` 设置窗(玩家可继续看面板,网络重连在后台进行)|
| `LoginUI.cs` + `ILoginUI.cs` 真空壳保留 | 不删,不改,作 Tier 1+ 真做账号 UI 时的接口余量 |
| `FantasyNetwork.cs` / `FantasyNetworkConfig.cs` / `GameApp.cs` 不动 | 自动登录链路、UUID 派生路径、热更入口启动序列全零改动 |
| 协议生成物 `Assets/GameProto/` 不动 | 本子单无新协议 |

### dev 须自行据工程现状定的实现选择

- `OnLogout` 方法是 expression-bodied(`=> ShowPlaceholder(...)`)单行 → 改写后会变成有方法体的形态(`{ ShowPlaceholder(...); Shutdown(); Boot(); }`),C# 语法层是显然改写、dev 据现状定具体写法(单一表达式不能塞三句,必须改方法体)
- `ShowPlaceholder` 调用文案常量是否抽 const / 是否多语言 textId 化:沿用同文件其它 OnXxx 占位的字面量做法(`ShowPlaceholder("…");`),不为登出单独抽常量(同 19 §六 O6 「文案多语言延后」做法)
- 引用 `FantasyClient.FantasyNetwork`:`SettingsWindow.cs` 当前命名空间是 `GameLogic.UI`,引用 `FantasyClient` 命名空间下的静态门面需加 `using FantasyClient;` 或全限定名;dev 据 C# 风格定(全限定名更显式、避免污染 using 列表;`using` 与文件已有 using 风格一致)
- 是否包 `#if FANTASY_UNITY`:`FantasyNetwork.cs` 类自身在 `#if FANTASY_UNITY` 内,调用方需对称处理;dev 据工程编译配置定(其它处的 `FantasyNetwork.Boot` 调用如 `GameApp.cs::StartGameLogic` 已在 `#if FANTASY_UNITY` 内调用,SettingsWindow 的 OnLogout 同样需要保护)

## 验收标准(test 逐条核;完成定义 = 行为可观测)

参见 36 §七 全部验收点(CV1-CV8 + E1-E3)。主验:
- **CV1 编译**、**CV2 改动面控制(git diff 仅 SettingsWindow.cs 一文件)**、**CV3 OnLogout 三步顺次结构**、**CV5 LoginUI 真空壳保留**、**CV6 UUID 派生路径不变**、**CV8 Code Review**、**E1 Tier 0 全栈登录链路真往返**、**E3 既有四特性 E1 真往返零回归**

辅验:**CV4 文案现状化**、**CV7 19 设计稿同步改写已落**、**E2 登出按钮 + 自动重连真往返**

### BLOCKED 边界

- 本机 MongoDB(`D:\mongodb-portable`)不可达 / Fantasy 服务端起不来 → E1 / E2 / E3 真往返判 **BLOCKED 非 FAIL**(memory `local-mongodb-for-server-roundtrip` + server-test memory `feedback-blocked-vs-fail`)
- 编译 / Code Review(CV1 / CV8)、改动面 / 文案 / 现状审计核(CV2 / CV3 / CV4 / CV5 / CV6 / CV7)照常验

## 已拍板决策(decisions,自治默认推进,要改另开增量)

- **设计稿 = 新建 36-account-client.md**(`nav.js` GROUPS 已在「全栈 / 服务端」组 35 之后 33 之前注册 + 详细 desc);设计 18 / 20 / 21 / 22 / 23 / 30 / 31 / 32 / 33 / 35 **零改写**(本子单与之均正交或沿用既有契约);**19 设计稿同步改写**(D6,conventions §6 触发,本任务内已落)
- **D1 LoginUI 不上线,保留 11 行真空壳作 Tier 1+ 接口余量**:自动登录链路已无声跑通,GameApp 启动到主菜单是自动链路,插入 LoginUI 阻断点要么动玩法主入口路径(回归面)、要么 LoginUI 一闪而过(无 UX 价值);Tier 0 GDD「玩家无感」明示;现有 `LoginUI.cs` + `ILoginUI.cs` 保留作 Tier 1+ 真做账号 UI 时接口余量
- **D2 设置窗登出按钮 = 实做**:基础设施全已就位(按钮 + 子图 + 节点 + 监听),OnLogout 占位文案「离线版无账号系统」在 35 上线后过时,这是本子单唯一可观测 UX 改动,boss brief 明列
- **D3 登出语义 = 断当前会话 + 立即重新走自动登录**:顺次调 `FantasyNetwork.Shutdown() → FantasyNetwork.Boot()`;玩家自然意图 = 「重新登录到同一账号」,UUID 不变(SystemInfo 派生玩家无法清)→ server 段 35 走 update 分支(末次登录时间刷新、首次注册时间不变)= 玩家观感「我重连了一下,服务端记我又活跃了」;账号切换 / 多账号 / 回登录前态 UX 留 Tier 1+ / Tier 3
- **D4 不引入 PlayerPrefs UUID,沿用现状 SystemInfo.deviceUniqueIdentifier 派生**:boss brief「PlayerPrefs UUID」表述与现状不符(已核源码);引入会改 account 主键值派生路径 → 既有 30/31/32/33 业务集合的 account 字段值漂移 → 违 35 守不变量「既有四特性零迁移、零数据兼容性问题」
- **D5 登出反馈走 ShowPlaceholder 兜底文案改现状语义,不做二次确认弹窗**:工程暂无确认弹窗组件(同 19 §六 V5 OnClearSave 也走 ShowPlaceholder Log.Info);Tier 0 登出不损坏玩家数据(服务端账号账本 + 客户端 MergeMetaSave 都不清),无需二次确认守护;文案改为现状语义(如「已断开连接,正在重新登录…」)
- **D6 19 设计稿同步改写**(conventions §6):§一 #10 / §一总结段 / §3.6 末段 / §七 O8 / §八风险表均按 36 §六同步改写清单已落;改写口径 = 「快捷登录」语义收窄为 Tier 3 OpenID/邮箱绑定才属此范畴(仍 = 不做)+「设置窗登出按钮 = 实做」标记 +「无账号系统」表述清除
- **设置窗自身不关闭**(O6):玩家可继续看面板,网络重连在后台进行;Tier 1+ 上 LoginUI 实做配合时再改
- **登出连点防抖 = 不做**(O7):沿用既有架构幂等性(Shutdown 容忍 null、Boot `_initialized` 标记防重复初始化);实测连点卡 UI 时 Tier 1+ 加防抖
- **登出反馈非异步等待**:不做 `await FantasyNetwork.LoginAsync` 等待登录成功才反馈(异步 await 会让 OnLogout 回调挂死、UI 主线程卡顿);ShowPlaceholder 在 Shutdown + Boot **之前**即时反馈,玩家观感 = 「点了就有响应」
- **不投机性预留 Tier 3 接口**:沿用 19 settings / 35 §六 「不投机性建未来用不上的接口」做法,LoginUI / FantasyNetwork / FantasyNetworkConfig 真空壳 / 沿用现状自然就是 Tier 1+ / Tier 3 接口余量,无需另加预留

## 影响半径(本次改动 + 同步落点)

- **设计稿**:新建 `design-docs/36-account-client.md`(`nav.js` GROUPS 已注册;首页卡片 / 侧边栏自动同步)
- **既有设计稿同步**:**19 settings**(本任务内已改 §一 #10 / §一总结段 / §3.6 末段 / §七 O8 / §八风险表;原「离线无账号系统」「快捷登录 = 不做(账号系统未建)」类表述已清,改为「Tier 3 快捷登录 = 不做」+「设置窗登出按钮 = 实做」分档);**18 / 20 / 21 / 22 / 23 / 30 / 31 / 32 / 33 / 35 零改写**(均正交或沿用既有契约,见 36 §五关系表)
- **代码**:dev 改一文件 `Assets/GameScripts/HotFix/GameLogic/UI/SettingsWindow.cs` 的 `OnLogout` 方法体(由 1 行 expression-bodied 改为 3 行方法体);其它任何客户端文件零 diff(SV-equivalent CV2 硬验)
- **不同步**(明令不改):18 player-info 数据层(正交)、19 §二 / §3.1-3.5 / §四 / §五 / §六验收(本子单不影响 19 数据层)、20 / 21 / 22 / 30 / 31 / 32 / 33 业务契约(account = UUID 字符串语义不变)、35 server 段(本子单是其客户端段配套,不动 server)
- **server 段联动**:server 段 35 已 PASS,本子单不动 server 段;Tier 0 全栈联调由 E1 / E2 / E3 真往返收口

## 自检(plan 收尾必做,conventions §「收尾必做」)

- [x] 过程性内容不在正文(本设计稿无 diff 叙事 / 无「按你说的改成」/ 无对话痕迹;§一现状审计是事实陈述非过程叙事)
- [x] 正文无可推导事实的副本(本子单是新建稿,旁注「fantasy-net 现状以工程为准」沿用 35 范式;§一现状审计是新写稿对工程现状的一次性事实陈述,与「正文不复述代码」张力检视:本子单是 plan 不读代码红线的**例外**——是「现状审计 + 单点接线」类任务,接线点位置 / 占位现状 / 自动登录链路通否,必须用工程现状作单一事实源 → §一显式标「仅本节含文件路径与方法名作锚」,其余章节回到 code-free 行为契约,沿 35 「§读前必看 + §一切分表 + §3.2 表」给 server-dev 锚的口径;同 35 设计稿对「复用既有 demo Account 实体 + GateAccountFlagComponent」必给路径的特殊性)
- [x] 正文无拟人 / 口语比喻(grep 「死/打死/收口/钉死」:§读前必看末「Tier 0 全栈联调对外即告完成」「收口」用了一次 — 检视:「收口」是工程常用名词非比喻,且 35 / 33 也用过同表述,留;无「死/打死/钉死/焊死」)
- [x] 工作态内容可识别所属任务(本 state 文件头部明标「Tier 0 第 2 子单(登出接线 + 自动登录链路审计)」)
- [x] 被改动规范的旁注仍成立(19 改写后旁注仍成立:旧文「O8 快捷登录 = 不做(离线无账号,不留钩子)」旁注「不投机性建未来用不上的接口」在 19 §3.6 末段;改写后 §3.6 末段「Tier 3 快捷登录 = 不做不留钩子(不投机性建未来用不上的接口)」旁注同义保留,O8a 行未叠新旁注 — 仍成立)
- [x] 过时正文已重写或删除(19 §一 #10 / §一总结段 / §3.6 末段 / §七 O8 / §八风险表 5 处过时表述已**覆盖式重写**为现状,**不加勘误注 / 不加状态注叠旧文**;O8 行由「快捷登录 = 不做」改为「设置窗登出按钮 = 实做」+ 新增 O8a「Tier 3 快捷登录 = 不做」分档,呼应 conventions §6「现行体系仍有活引用 → 覆盖式重写」)
- [x] design-docs 正文 code-free(grep `[A-Z][a-zA-Z]+\.[A-Z]` 类符号:§一现状审计含文件路径 + 方法名 + 行号是本子单 plan 不读代码红线的**例外**——是「现状审计 + 单点接线」类任务、接线点必须明确;§一已显式标「仅本节含文件路径与方法名作锚」,其它章节(§二-§九)回到行为契约 code-free;同 35 设计稿「§读前必看第 5 条 + §一切分表 + §3.2 表」三处例外做法、同 25/27 等换皮稿对节点路径 / Atlas / Sprite 名给出具体落点的做法,沿用本库已确立的「现状审计 + 接线点」类设计稿例外条款)

## 自治审计(本环节自主拍板的取舍,简记)

- **boss brief 三项「现状不明」全部已核源码**,审计结论与简报「现状已自动登录跑通且 UI 不在路径上」「LoginUI 接线本身可能不存在『接线工作』」「现状审计 + 零 diff」三项判断**完全吻合**;**仅一项简报误读已纠正**:boss brief 「客户端 PlayerPrefs UUID」表述与现状不符(UUID 取自 SystemInfo.deviceUniqueIdentifier 不入 PlayerPrefs),据现状已调整 D4(不引入 PlayerPrefs UUID),决策依据进 36 §一 / §二 D4 / §四 4.5;不入 blockers(本子单守不变量「不引入 PlayerPrefs UUID」与简报「不清 PlayerPrefs UUID,下次仍同 UUID 自动登录」**结果等价**——简报想要「下次同 UUID」,现状 SystemInfo 派生本就是「下次同 UUID」,无需 PlayerPrefs)
- **设置窗登出按钮 = 实做**(D2):虽然 boss brief 描述「本质可能 = LoginUI/登出按钮 UX 上线 + UUID 迁移现状确认」,但实际 LoginUI 不上线、UUID 迁移不做,登出按钮接线是本子单**唯一**实质 UX——故本子单非零 diff,有一处必须改的接线;Tier 0 客户端段不空走
- **整局走查崩法 5 类 + 诚实边界齐**:§四 4.1-4.6 已落,每类均给对策;§4.6 诚实边界明示「不守回登录前态 UI / 账号切换 / 二次确认 / 登出后清玩法存档(登出 ≠ 切号 ≠ 清进度)」防 test/boss 误判范围
- **范围闸**:核心档 = 登出按钮接线(D2,本子单唯一可观测 UX);砍掉 = 无可砍(本子单已是 Tier 0 客户端段最小可观测改动 — LoginUI 不上线、UUID 不迁移、二次确认不做,均已是「不做」档);所有「可砍 / 增强」档均归 Tier 1+ / Tier 3,本子单只留核心档,范围已收敛到最小
- **整局走查向上对体验**:登出按钮接线让玩家「能主动重连一次」的体验在「服务端可能挂了 / 网络抖了 / 我换设备登的怀疑」三类场景下成立(玩家点登出 → 几秒内自动重登 → 知道服务还在 / 账号还在);LoginUI 不上线让「玩家无感登录」的体验在 GDD Tier 0 「玩家不必看见账号体系」前提下成立 — 两个决策都能答上「让玩家的什么体验更好」,过「向上对体验」校
