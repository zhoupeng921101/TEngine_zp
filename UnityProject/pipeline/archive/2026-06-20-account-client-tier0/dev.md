# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:Tier 0 真实账号体系·客户端段·第 2 子单(登出接线 + 自动登录链路审计)

状态(第 1 轮返修后):F1 已修——19-settings-system.md 第 25 行(WARNING 块第 4 条「离线 + 去变现适配」)与第 33 行(NOTE 块「方向约束」列)两处「离线无账号系统/离线无账号」过时文案按 conventions §6 覆盖式重写,口径与已 PASS 的 §一 #10 / §3.6 / §七 O8 对齐(「Tier 3 OpenID/邮箱绑定的快捷登录 = 不做」+「设置窗登出按钮 = 实做」+「账号体系已上服务端 35」)。代码侧零改动(SettingsWindow.cs 本轮未触)。

### 第 1 轮返修产出(给 test 复检 CV7)

**修复点 1:第 25 行 `[!WARNING]` 读前必看第 4 条**
- 原文(过时):`**离线 + 去变现适配(项目方向)。**① 快捷登录 = **不做**(离线无账号系统,同 18 player-info 账号绑定排除);...`
- 改后:`**去变现适配(项目方向)。**① Tier 3 快捷登录(OpenID/邮箱绑定后免输入登)= **不做**(同 18 player-info 账号绑定排除,留 Tier 3);账号体系已上服务端(见 35,设备 UUID 自动注册式),设置窗登出按钮 = **实做**(见 [36 §三登出语义](#36-account-client::logout));② 联系客服 ...`
- 处置:① 标题「离线 + 去变现」收窄为「去变现」(账号体系已上线,不再是「离线」前提);②「快捷登录 = 不做(离线无账号系统)」收窄为「Tier 3 OpenID/邮箱绑定的快捷登录 = 不做」(对齐 §一 #10 / §七 O8);③ 补「账号体系已上服务端(35,设备 UUID 自动注册式)」+「设置窗登出按钮 = 实做」现状声明;④ 不叠勘误注、覆盖式重写(conventions §6)。

**修复点 2:第 33 行 `[!NOTE]` 立项信息「方向约束」列**
- 原文(过时):`离线还原 · **去变现**:本系统不含充值 / 内购 / 快捷登录(离线无账号);...`
- 改后:`**去变现**:本系统不含充值 / 内购 / Tier 3 快捷登录(OpenID/邮箱绑定,留 Tier 3);账号体系已上服务端(见 35),设置窗登出按钮 = 实做(见 [36 §三登出语义](#36-account-client::logout));...`
- 处置:① 标题「离线还原 · **去变现**」收窄为「**去变现**」;②「快捷登录(离线无账号)」收窄为「Tier 3 快捷登录(OpenID/邮箱绑定,留 Tier 3)」;③ 补「账号体系已上服务端(35)」+「设置窗登出按钮 = 实做」现状声明。

**修复点 3(自检顺带净化):§一 #10 行「本节不再写『离线无账号』」过程叙事净化**
- test 报告未提及,但属 conventions §1 隐患——「不再写」是改写过程的留痕、违正文「不掺过程」红线(规则1)。本轮把元陈述句换为现状陈述句「账号体系由服务端 35 承载(设备 UUID 自动注册式)」,与上两处口径一致。

### 终检(本轮第 2 次扫文)

- `grep "离线无账号|离线版无账号|无账号系统|无账号" 19-settings-system.md` 零命中。
- `grep "离线" 19-settings-system.md` 零命中(三个标题前缀「离线 + 去变现 / 离线还原 · 去变现 / 不再写离线无账号」全清)。
- 文件清单:仅 `design-docs/19-settings-system.md` 一文件本轮新 diff;Assets/ 工作树本轮零新增 diff(SettingsWindow.cs 沿用上轮已 PASS 改动)。
- conventions §6 守则:覆盖式重写、不叠勘误/状态注、改一处即同步被它过时的他篇(本篇内 §一 #10 + §3.6 + §七 O8 在上轮已同步,本轮把同篇内剩余两处 WARNING/NOTE 块补齐 + 净化 §一 #10 元陈述句)。
- 收尾自检(conventions §「收尾必做」):① 过程性内容不在正文:已净化 §一 #10「本节不再写」元陈述;② 正文无可推导事实副本:三处改写均是行为级声明;③ 无拟人/口语比喻;④ 工作态可识别所属任务(本 state 头部);⑤ 旁注仍成立(本次改写未触及任何旁注);⑥ 过时正文已重写、无勘误叠旧;同篇所有过时表述本轮内同步完成。

### 验证点对照(给 test 复检)

| CV | 怎么验 | 预期 | 自跑状态 |
|---|---|---|---|
| CV7·19 设计稿同步改写已落 | `grep "离线无账号\|离线版无账号\|无账号系统\|无账号\|离线" design-docs/19-settings-system.md` | 零命中 | PASS(本轮终检) |
| 其它 CV1-CV6 / CV8 / E1 / E2 / E3 | 上轮已 PASS | 不重复验 | 沿用上轮结论(本轮 Assets/ 零改动)|

### 标注

- 仅文档修复,代码零改动(Assets/ 工作树无本轮新 diff)。
- 不需 Unity 编译复检(脚本未触)。
- 不需重跑 E1/E2/E3 真往返(Assets/ 零改动 + UUID 派生路径不动)。
- 不需重跑 EditMode 单测(代码零改动)。

### 第 0 轮原始交接(保留,test 已 PASS,留作上下文)

## 交接区(交测试)

### 1. 改动摘要(做了什么 / 为何 / 关键决策)

把设置窗登出按钮回调由占位文案改为「断当前会话 + 立即重新走自动登录」,与 server 段 35(2026-06-19 已 PASS)对齐:server 段已落 accounts upsert + LoginGameHandler 钩子,客户端段 36 的 plan 调查证实自动登录链路无声跑通(`GameApp.StartGameLogic → FantasyClient.FantasyNetwork.Boot → 自动连服 → LoginAsync → 自动 EnterGame → MainMenuWindow`),LoginUI 工程内零 ShowUIAsync 调用是设计意图(玩家无感),UUID 来源是 `SystemInfo.deviceUniqueIdentifier`(不入 PlayerPrefs)——这三项均**不接线、不动**。**唯一接线点**是 `SettingsWindow.cs::OnLogout`(plan 标第 202 行,核对一致)。

- **`OnLogout` 由 expression-bodied 单行改为方法体**:① `ShowPlaceholder("已断开连接，正在重新登录…");` 即时反馈;② `FantasyClient.FantasyNetwork.Shutdown();` 清连接 + 心跳 + 单位视图缓存 + 四态(`IsConnected/IsLoggedIn/IsInGame/_initialized`)+ 标 `_intentionalClose=true` 防自动重连;③ `FantasyClient.FantasyNetwork.Boot();` 无参重连(账号走 `DefaultAccountName()` 派生 = 同一设备 UUID = 走 server 段 35 update 分支,末次登录时间刷新、首次注册时间不变)。三步顺次、无 if 分支、无 await、无新方法、无新字段。
- **`#if FANTASY_UNITY` 块包裹** Shutdown/Boot 两步:与 `GameApp.cs::StartGameLogic` 现有 Boot 调用范式(第 38-42 行 `#if FANTASY_UNITY ... FantasyClient.FantasyNetwork.Boot(); ... #endif`)对齐;`FantasyNetwork` 类自身在 `#if FANTASY_UNITY` 内、`FantasyClient` 程序集带 `defineConstraints:["FANTASY_UNITY"]`,跨 define 直调会编译失败。`ShowPlaceholder` 在 `#if` 外:即使该 define 关闭,玩家点登出仍有反馈,不至于「点了无任何响应」。
- **采用全限定名 `FantasyClient.FantasyNetwork.Shutdown/Boot`**:与 `GameApp.cs` 现有范式一致(亦全限定、未 `using FantasyClient;`),不污染 `SettingsWindow.cs` 的 using 列表,显式标出跨程序集调用。
- **方法上方加 3 行注释**:① 标明出处「设计 36 §三」;② 简述三步顺次语义;③ 显式标顺序锁(Shutdown→Boot,调换则 Boot 内 `_initialized` 早返复用旧 Scene 不重连)——给后续维护者留接缝。

关键决策(plan 已拍板,本环节零额外取舍):
- **改动面控制在 SettingsWindow.cs 单文件**:plan §3.1 + CV2 硬验「`git diff Assets/` 仅命中一文件」,工作树自查 `git diff --stat Assets/` 输出 = `SettingsWindow.cs | 13 ++++++++++++-`(单文件、12 增 1 删)。LoginUI / FantasyNetwork / FantasyNetworkConfig / GameApp / Assets/GameProto 零改动。
- **沿 plan D3 取「Shutdown → Boot」语义**:玩家自然意图「重新登录到同一账号」;UUID 不变(SystemInfo 派生,玩家无法清);server 段 35 走 update 分支记一次新活跃。
- **沿 plan D5 文案改现状语义**:删「离线版无账号系统(设计 19 §七 O8)」,改「已断开连接，正在重新登录…」(中文短文案,与既有占位回调如 `OnContact`/`OnMoreGames` 字面量做法同口径)。

### 2. 文件清单

新增:无。

修改:
- `Assets/GameScripts/HotFix/GameLogic/UI/SettingsWindow.cs`(`OnLogout` 由单行 expression-bodied 改为带 3 行注释的方法体:3 步顺次调用 + `#if FANTASY_UNITY` 包裹 Shutdown/Boot;第 202 行原文 1 行,改后 202-213 行共 12 行)

未触:
- `Assets/GameScripts/HotFix/GameLogic/UI/LoginUI/LoginUI.cs`(11 行真空壳保留作 Tier 1+ 接口余量,CV5)
- `Assets/GameScripts/HotFix/GameLogic/IEvent/ILoginUI.cs`(接口定义保留)
- `Assets/Fantasy/Scripts/FantasyNetwork.cs`(门面 Shutdown / Boot 等 API 不动,无新增方法,CV2)
- `Assets/Fantasy/Scripts/FantasyNetworkConfig.cs`(`DefaultAccountName` + 服务器地址 + 自动登录开关全不动,CV6;注:本文件在本会话开始前 git index 已 staged 一处非本任务改动 `ReconnectMaxAttempts 0→3`,与本任务正交,未触 UUID 派生路径)
- `Assets/GameScripts/HotFix/GameLogic/GameApp.cs`(热更入口 StartGameLogic 启动序列不动)
- `Assets/GameProto/`(无新协议生成物)
- `BlockBlast.Tests`(本任务无新增 EditMode 测试,理由见 §4 验证点档差)

### 3. 验证点(逐条对应验收 / 该验什么 / 怎么验 / 预期)

| CV | 怎么验 | 预期 | 自跑状态 |
| --- | --- | --- | --- |
| CV1·编译通过 | `refresh_unity compile=request scope=scripts` + `read_console types=error` | 0 error;域重载完成、`SettingsWindow` 类型可解析 | PASS |
| CV2·改动面控制 | `git diff --stat Assets/` | 仅 `SettingsWindow.cs` 一文件;其它路径(GameApp.cs / FantasyNetwork.cs / FantasyNetworkConfig.cs / LoginUI.cs / ILoginUI.cs / Assets/GameProto/)零工作树改动 | PASS |
| CV3·OnLogout 三步顺次结构 | 读 `SettingsWindow.cs` 第 198-213 行,核 OnLogout 方法体 | 三步:① `ShowPlaceholder("已断开连接，正在重新登录…");` ② `FantasyClient.FantasyNetwork.Shutdown();` ③ `FantasyClient.FantasyNetwork.Boot();`;② ③ 在 `#if FANTASY_UNITY` 块内;无 if / 无 await / 无新方法 / 无新字段 | PASS |
| CV4·文案现状化 | grep `SettingsWindow.cs` 内 "离线版无账号系统" 应零命中 | OnLogout 内 ShowPlaceholder 文案 = 「已断开连接，正在重新登录…」 | PASS |
| CV5·LoginUI 真空壳保留 | grep `LoginUI.cs` + `ILoginUI.cs` 与本任务工作树 diff;grep 工程内 `ShowUIAsync<LoginUI>` | `LoginUI.cs` 11 行内容不动;`ILoginUI.cs` 不动;`ShowUIAsync<LoginUI>` 零命中 | PASS(`git status --short` 未列两文件) |
| CV6·UUID 派生路径不变 | 读 `FantasyNetworkConfig.cs::DefaultAccountName()`;grep `PlayerPrefs` + `deviceUniqueIdentifier` | `DefaultAccountName` 仍取 `SystemInfo.deviceUniqueIdentifier` + `"dev_"` 前缀;无新增 PlayerPrefs UUID 写入键 | PASS(本任务零改动该文件相关行;参 §2「未触」说明) |
| CV7·19 设计稿同步改写已落 | 读 `design-docs/19-settings-system.md` §一 #10 / §一总结段 / §3.6 末段 / §七 O8 / §八风险表 | 「离线版无账号系统」「快捷登录 = 不做(账号系统未建)」类表述已清;OnLogout 占位标记改为「实做(本子单 36 接线)」;Tier 3 快捷登录新增分档 | plan 阶段已落(`pipeline/state/plan.md` 「D6 19 设计稿同步改写已落」+ plan 自检 §「自检」勾过) — dev 复核 OK |
| CV8·Code Review | 重点核 ① 仅一文件改动(CV2);② 三步顺次且无异步 await / 无 if 分支(CV3);③ 文案不含过时表述(CV4);④ LoginUI / FantasyNetwork / FantasyNetworkConfig / GameApp 零改动(CV5 + CV6);⑤ 19 设计稿同步改写齐(CV7);⑥ 不新增门面方法、不新协议、不新字段、不删真空壳 | 全部满足 | 自审 PASS(留 test 复核) |
| E1·Tier 0 全栈登录链路真往返(PlayMode) | 起 Fantasy 服务端(`dotnet run --project examples/Server/APP/Main/Main.csproj --framework net9.0 -- --m Develop`)+ 本机 MongoDB(`D:\mongodb-portable` 27017 LISTEN)→ Unity PlayMode 启动 → 自动登录链路完整跑通(日志可见 `[Fantasy] ✅ 已连接服务器` + `[Fantasy] ✅ 登录成功 account=dev_<UUID>` + `[Fantasy] 进入游戏...`)→ MainMenuWindow 打开 → MongoDB accounts 集合查 `_id = dev_<UUID>` 落档(首次注册时间 + 末次登录时间 + 状态 = 0) | 链路打通、MongoDB 落档成功 | **未自跑**(dev 不进 PlayMode,留 test) |
| E2·登出按钮真往返(PlayMode) | E1 起服 + 启动至 MainMenuWindow → 打开 SettingsWindow → 点 Logout 按钮 → 日志可见 `[Fantasy] 与服务器断开连接`(或等价断连)+ Shutdown 触发的 Scene.Dispose 级联清态;几秒内见 `[Fantasy] ✅ 已连接服务器` + `[Fantasy] ✅ 登录成功 account=dev_<同一 UUID>` 再次出现 → MongoDB accounts 该 UUID 记录的「末次登录时间」**已更新**、「首次注册时间」**不变** | 断连 + 自动重连完整;同 UUID 触发 server 段 update 分支 | **未自跑**(留 test) |
| E3·既有四特性 E1 真往返零回归(PlayMode) | E1 链路通后,30 兑换码 / 31 排行榜 / 32 邮件 / 33 结算各自已 PASS 端到端用例全部仍 PASS(account 字段语义不变、UUID 派生路径不变、登录链路不变) | 各自 PASS | **未自跑**(留 test) |

#### 异常路径自检(已走查,部分已含上表)

- **Shutdown 前未连接(Editor 首启即点登出 / 登录失败后点登出)**:`FantasyNetwork.Shutdown` 内 `Scene?.Dispose()` 是 null 安全;`Boot` 走「初始化 + 连服 + 自动登录」全流程 = 与初次启动等价。OK。
- **连点登出**:Shutdown 幂等(`Scene?.Dispose()` 容忍 null + 重复 dispose);Boot 幂等(`_initialized` 标记防重复初始化运行时)。最坏 = 多次连服尝试,沿用既有 `OnConnectFail → ScheduleReconnect` 退避。OK(plan §4.3 已识别为可接受档,Tier 0 不加防抖)。
- **Boot 内部失败(连不上服 / MongoDB 不可达)**:沿用既有 `OnConnectFail` → `ScheduleReconnect` 按 `ReconnectMaxAttempts` 重试;客户端 `IsLoggedIn=false` 不挂会话身份(沿用 35 §5.4 兜底)。玩家可再点登出触发等价人工重试。本任务不改重连策略。OK。
- **`#if FANTASY_UNITY` 未定义(理论上 Editor 工具构建上下文)**:OnLogout 退化为只 `ShowPlaceholder`(玩家仍有「已断开连接，正在重新登录…」反馈),与原占位行为等价、不报错;运行期玩法构建均开启 FANTASY_UNITY(参 `FantasyNetwork.cs` 自身 `#if` 套用范式)。OK。
- **ShowPlaceholder 在 Shutdown 前**:玩家先看到反馈、后台异步重连(Boot 内部异步)→ 避免「点了无反应」误判(plan D5 反馈非异步等待)。若 Toast 系统未建则 Log.Info 兜底(`ShowPlaceholder` 第 208 行实现不动)。OK。
- **设置窗自身不关闭**(plan §3.1 步骤 4):OnLogout 不调 `Close()`,玩家可继续看面板,网络重连后台进行。OK,沿现状。
- **UI 主线程不卡**:三步无 await / 无 IO 同步等待;Boot 内部协程发起异步连接,不占主线程。OK。

### 4. 标注(交接必看)

- **涉及热更程序集**:`Assets/GameScripts/HotFix/GameLogic/UI/SettingsWindow.cs` 在 `GameLogic` 程序集(热更)。`GameLogic.asmdef` 未改;asmdef 引用 `FantasyClient` 已就位(沿用既有 `GameApp.cs` 调用 `FantasyClient.FantasyNetwork.Boot` 的可达性)。
- **不需 Luban 重生成**:本段未碰 Luban 源 / 导表 / 配置二进制。
- **协议同步状态(E4)**:**无新增 / 删除协议**,GameProto 零改动,客户端协议生成物零回归(本任务对 proto 零影响,E4 = N/A)。
- **需进 Play 模式手验的功能点(E1 / E2 / E3 真往返)**:本段不接触运行期服务端、不调 RPC;真实联调依赖 Fantasy 服务端 + 本机 MongoDB:
  - **E1**:起 Fantasy 服务端 + Unity PlayMode + MongoDB accounts 集合查 `_id=dev_<UUID>` 首注册 + 状态=0
  - **E2**:点登出按钮 → 断连 + 自动重连 → MongoDB accounts 「末次登录时间」更新、「首次注册时间」不变
  - **E3**:E1 跑通后顺带跑 30/31/32/33 各自已 PASS 端到端用例,核 account 字段值未漂移
  - **BLOCKED 档**:本机 MongoDB(`D:\mongodb-portable`)不可达 / Fantasy 服务端起不来 → E1/E2/E3 判 BLOCKED 非 FAIL(memory `local-mongodb-for-server-roundtrip` + server-test memory `feedback-blocked-vs-fail`);CV1-CV8 照常验
- **EditMode 单测档差(本任务不增 EditMode 测试)**:plan 的验收点 7.1(CV1-CV8)= 「编译验证 + 静态核对」档,无新增 EditMode 用例要求。客观难度:OnLogout 接的是 `FantasyClient.FantasyNetwork`(在 `#if FANTASY_UNITY` 块内,且 `FantasyClient` 程序集带 `defineConstraints:["FANTASY_UNITY"]`),而 `BlockBlast.Tests` asmdef 未引用 `FantasyClient` / `Fantasy.Unity`——加引用会让 test asmdef 跨进 FANTASY_UNITY 约束、影响其它无关测试,违 plan「客户端工程 Assets/ git diff 只命中 SettingsWindow.cs 一文件」守不变量。运行期真往返归 E1/E2 PlayMode 验证,EditMode 不重复核。
- **Code Review 5 红线复核**(对照 `.claude/skills/tengine-dev/SKILL.md` 核心红线):
  - ① 异步优先:OnLogout 无新增同步阻塞 IO;`Shutdown` 同步清态 + `Boot` 内部异步发起连接,UI 主线程不卡。OK。
  - ② 模块访问规范:`SettingsWindow` 引 `FantasyClient.FantasyNetwork`(全限定 + `#if FANTASY_UNITY` 包裹)与 `GameApp.cs` 同范式;不引 server 端 Fantasy.Net、不引 server 段业务集合。OK。
  - ③ 资源释放:`Shutdown` 内 `Scene.Dispose()` 级联清单位视图缓存 + 心跳 + 连接,不在客户端 UI 层留资源。OK。
  - ④ 热更边界:改动全在 `GameScripts/HotFix/GameLogic/UI/` 一文件,不动 GameProto / 不动框架(`Assets/Fantasy/` 不触)/ 不动 asmdef。OK。
  - ⑤ 事件解耦:本接线无新事件;沿用既有 `FantasyNetwork.OnLoggedIn` 事件链(`AutoEnterGame=true` 自动进游戏),无新订阅。OK。

### 5. 设计边界(诚实标注,非缺陷,交 test/boss 知会)

- **登出后既有四个全栈特性的客户端状态**:server 段业务状态在服务端(账户身份重新挂上即恢复),客户端无残留状态需清;客户端本地的 `MergeOrderState` / `MergeMetaSave` 等元层数据**不动**(玩法存档不归账号管,登出 ≠ 切号)— 与 plan D3 「登出 = 重新登录到同一账号」语义一致。
- **`#if !FANTASY_UNITY` 的退化路径** :OnLogout 退化为只 `ShowPlaceholder` 反馈,Shutdown/Boot 不执行——理论上是「未编译网络功能」场景的优雅退化,不是缺陷;运行期玩法构建均开启 FANTASY_UNITY,该路径在正常游戏构建下不触达。
- **设置窗自身不关闭**:沿 plan O6 决策,玩家点登出后设置窗仍开;Tier 1+ 上 LoginUI 实做配合时可改为关闭。
- **二次确认弹窗 = 不做**:沿 plan D5 + O4(工程无确认弹窗组件);Tier 0 登出不损坏数据,无需守护;Tier 1+ 上确认弹窗组件后再接。
- **登出连点防抖 = 不做**:沿 plan O7(架构幂等性兜底);实测连点卡 UI 时 Tier 1+ 加防抖。

### 6. 需 boss/用户复核

无阻塞项。
