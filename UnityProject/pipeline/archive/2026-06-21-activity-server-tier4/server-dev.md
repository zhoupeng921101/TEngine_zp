# 状态:服务端开发(server-dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-server-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:Tier 4 活动系统 · 服务端段 · 第 1 子单(地基 + 每日登录奖跑通)— 交接 server-test

设计基线 `design-docs/39-activity-server.md` + `pipeline/state/plan.md`(plan 已锁:活动配置表 + 进度集合 + 周期幂等 + 登录触发 + 复用 32 SendMailTo,无客户端 RPC,9 套活动其余 8 套留后续逐套刀)。

### 一、改动摘要(做了什么、为何、关键决策)

**实质交付 = 活动配置 / 进度两集合 + 登录触发达标判定 + 周期幂等抢占 + 复用 32 发奖入口**,共 2 个新 Entity 文件 + 2 个新 Hotfix 文件 + 2 处既有文件外科手术式追加(LoginGameHandler 末尾追加 OnLogin 协程;OnCreateSceneEvent Gate 分支追加 1 个 AddComponent)。

**关键决策(沿用 plan 已锁 + 工程现状定的实现选择,plan §「server-dev 须自行据工程现状定的实现选择」)**:

- **配置加载范式 = 服务端权威表 + ReconcileDefs(upsert)+ ReloadCache 缓存**(同 33 `RankServiceComponentSystem.AuthoritativeDefs` / 32 `MailServiceComponentSystem.BuildBroadcastSeeds` / 30 兑换码 已建范式):**未走 plan 提及的 Luban 路径**。理由 = `pipeline/memory/server-dev.md` 第一条 + 32 `MailServiceComponentSystem` 注释 + 33 `RankServiceComponentSystem` 注释明确写:「服务端工程**无 Luban 集成**(无 TbXxx/.bytes 加载链)」,需要「与客户端配置同源」时按 redeem/rank/mail 先例**在 Awake 内播种 MongoDB 文档**。plan D1「配置存 Luban 不存 MongoDB 集合」实际不可达 — 按 plan §「server-dev 须自行据工程现状定的实现选择」自决调整为 redeem/rank/mail 同范式。
- **`mail_def` 字段展开为内嵌邮件字段**(SenderTextId / TitleTextId / ContentTextId / ExpireDays):非按 plan 提及的「指向 mail.xlsx 邮件模板 id」间接引用。理由 = 32 `MailDecisionHelper.SendMailTo` 签名是 `(self, account, senderTextId, titleTextId, contentTextId, expireDays, rewardId)` 散字段、不吃 mail 模板 id;若引入「activity 指 mail 模板」中间层需再加一次查表 + activity_def 依赖 mail_template 缓存,违 SRP。直接展开 4 字段进 activity_def 是更简洁的接法,同 32 mail_template seed 直接写字段范式。
- **不起 RepeatedTimer 节律**:本子单仅接 Type=Login(玩家登录时随路触发,§3.5),不接 Schedule/Cumulative/Action(O3 留后续刀)。Login 类无需定时器(登录链路就是触发点);Schedule 类未来若加再起 timer 同 33 RankServiceComponentSystem.SettleTimerId 范式。
- **时钟入参化(helper 接 nowMs)**:`ActivityEvalHelper.OnLogin / EvaluateAndClaim / Increment / ComputeCurrentCycleKey` 全部以 nowMs 为入参(同 33 `RankSettleHelper.CheckAndSettleAll(self, nowMs)` 范式)。生产由 LoginGameHandler 调用时传 `TimeHelper.Now`;server-test 可在探针里传可控时刻驱动 SV4/SV5 跨日 / 跨周分支,helper 内部不读 `TimeHelper.Now` / `DateTime.UtcNow`(memory `cycle-trigger-idempotency-pattern` 红线)。
- **周期键算法 = UTC 0:00 ms**(同 33 `ComputeWeeklyDueMs`/22 §3.5.1 同口径):Daily = 今日 0:00 UTC ms / Weekly = 本周一 0:00 UTC ms / OneShot = 1 常量;实现走 `nowMs.Transition()`(Unix ms → UTC DateTime)+ `Date` 取 UTC 00:00 + `Transition()` 回 Unix ms。**非**用 .NET DateTime.Ticks(plan 提了 ticks 但 ms 单位足够、且与 33 周期键单位一致便于跨设计稿对齐)。
- **claim-then-act 抢占在副作用之前**:`EvaluateAndClaim` 先 `TryClaimCycleKey`(原子条件写 `lastClaimedCycleKey < periodKey` → $set periodKey)抢占周期键、抢到才调 `MailDecisionHelper.SendMailTo` 投邮件。抢占在发邮件之前 = SV6 并发幂等核心 + §四诚实取舍(接受漏发窄窗 / 不接受超发窄窗,同 33 §四)。
- **Increment 与 EvaluateAndClaim 拆为两步**:OnLogin 路径先 Increment(counter+1)再 EvaluateAndClaim(判达标 + 抢占 + 发奖)。两步拆开是因为业务侧 Cumulative 类(O3)未来接入触发钩子时,业务系统直接调 Increment 改 counter,然后由统一的「下次登录 OnLogin / 定时器 tick」走 EvaluateAndClaim — 此架构形态使 9 套活动后续每套刀只接 Increment 触发钩子,不动 EvaluateAndClaim。
- **Increment 容错并发首次 +1 撞 _id 主键**:`UpdateOneAsync(filter, $inc/$setOnInsert, IsUpsert=true)` 并发两次首次都走 setOnInsert 撞 _id → DuplicateKey,catch 后单次重试走 update 分支 $inc 累加。同 server-dev memory `mongodb-upsert-onlyinsert-vs-update-fields` 双 catch 范式(MongoWriteException Category==DuplicateKey + MongoCommandException Code==11000)。
- **TryClaimCycleKey IsUpsert=false**:Increment 已确保文档存在(OnLogin 路径先 Increment 后 EvaluateAndClaim),抢占走 `UpdateOneAsync(filter, $set, IsUpsert=false)` + 判 `MatchedCount > 0`。**非**用 `FindOneAndUpdateAsync` + IsUpsert=true(33 用那个范式因为标记文档首次抢占需建文档,activity 进度文档 Increment 已建,IsUpsert=false 更明确不引入额外路径)。若文档因异常缺失 → MatchedCount=0 → 抢占失败、跳过(下次登录由 Increment 建文档后再抢占)。
- **ActivityServiceComponent 挂 Gate Scene**(同 AccountServiceComponent / MailServiceComponent / RedeemServiceComponent / RankServiceComponent / PlayerPropertyServiceComponent 先例):Gate Scene 的 World 配了 MongoDB(同源 `self.Scene.World.Database`),且 LoginGameHandler 跑在 Gate Scene(`session.Scene`),挂载顺序 = 在 `MailServiceComponent` 之后(因 OnLogin 内依赖 MailServiceComponent 投邮件)。
- **登录触发挂钩点 = LoginGameHandler 末尾**(在 `SendInitSnapshotTo` 之后,沿 plan §3.2 处理顺序「Online 之后」):理由 = `Online` 之后会话已完整(GateAccountFlagComponent 挂全 + account.Session 挂全),且 OnLogin 协程化(`.Coroutine()`)不阻塞登录响应、不影响 `G2C_LoginGameResponse` 契约。**非**插在 Online 之前(那样 Online 之内的 Roaming Link 异常会让活动判定异步竞争挂钩点)。**注**:plan 说挂在「在 InitOrLoad players 之后 + AccountManageHelper.Add 之前」是基于 plan §3.2 设计上的「早返失败不挂会话身份」语义;但 ActivityEvalHelper.OnLogin 本身不写 response、不抛、所有失败静默跳过,所以挂在 Online 之后更稳。如 server-test 认为这违反 plan §3.2 步骤 4 严格次序请回 PASS-with-deviation 或交 boss 仲裁,我已在此明示偏差与理由,本判断 = plan §「server-dev 须自行据工程现状定的实现选择」自决。
- **协程化调用**:`ActivityEvalHelper.OnLogin(...).Coroutine()` 不阻塞登录响应返回,沿 `RankSettleHelper.CheckAndSettleAll(self, nowMs).Coroutine()` 范式。
- **`ActivityDefDoc / ActivityProgressDoc` 走原生 MongoDB BSON 文档(非框架 Entity)**:同 `AccountDoc / MailDocs / RedeemDocs / RankDocs / PlayerDoc` 先例,框架 `IDatabase` 高层 API 只能先读后写(SKILL.md / 32 / 35 / 37 明令禁止),并发原子 `FindOneAndUpdate / UpdateOneAsync` 必须走原生 `IMongoCollection<T>`。BSON 字段名默认 = C# 属性名(MongoDB 探针实测,见下 §四「真往返证据」)。
- **复用 `Fantasy.Helper.TimeHelper.Now`**:同 32 / 33 / 35 / 37 先例。

### 二、文件清单

**新增(4 个)**:

- `examples/Server/APP/Entity/Game Examples/Gate/Activity/ActivityDocs.cs` — `ActivityDefDoc`(_id=ActivityId / NameTextId / DescTextId / Type / Cycle / Target / Reward / SenderTextId / TitleTextId / ContentTextId / ExpireDays / StartAtMs / EndAtMs)+ `ActivityProgressDoc`(_id="{account}_{activityId}" / Account / ActivityId / Counter / LastClaimedCycleKey / LastUpdatedAt / Version)。
- `examples/Server/APP/Entity/Game Examples/Gate/Activity/ActivityServiceComponent.cs` — Gate Scene 上的活动系统服务组件,持 `IMongoCollection<ActivityDefDoc>? Defs` + `IMongoCollection<ActivityProgressDoc>? Progress` + `Dictionary<int, ActivityDefDoc> DefCache`。
- `examples/Server/APP/Hotfix/Game Examples/Gate/Activity/ActivityServiceComponentSystem.cs` — AwakeSystem(`AuthoritativeDefs` 静态权威表 + `ReconcileDefs` upsert 进 activity_def + `ReloadCache` 进 DefCache)+ DestroySystem 清。MongoDB 不可达 Warning 不阻断 Scene 创建。
- `examples/Server/APP/Hotfix/Game Examples/Gate/Activity/ActivityEvalHelper.cs` — 四入口:① `OnLogin(self, mail, account, nowMs)` 登录触发遍历 Type=Login 活动;② `Increment(self, account, activityId, delta, nowMs)` 进度计数器 upsert($inc Counter + $setOnInsert 首次字段);③ `EvaluateAndClaim(self, mail, account, def, nowMs)` 判达标 + 抢占周期键 + 调 SendMailTo;④ `ComputeCurrentCycleKey(cycle, nowMs)` 周期键算法(Daily/Weekly/OneShot)。

**修改(2 个外科手术式追加)**:

- `examples/Server/APP/Hotfix/OnCreateSceneEvent.cs` — Gate 分支 `case SceneType.Gate` 新增一行 `scene.AddComponent<ActivityServiceComponent>();`(在 `MailServiceComponent` 之后)。
- `examples/Server/APP/Hotfix/Game Examples/Gate/Handler/Outer/C2G_LoginGameRequestHandler.cs` — 在末尾 `SendInitSnapshotTo` 之后追加 3 行:取 `ActivityServiceComponent` + `MailServiceComponent` + `ActivityEvalHelper.OnLogin(...).Coroutine()`。既有「解析 accountName / RegisterOrLogin / InitOrLoad / AccountManageHelper.Add / 挂会话身份 / Online / SendInitSnapshotTo」步骤一行未改。

**协议生成物**:无 — 本子单**零新增协议**(plan + 39 §一立项明示「无客户端 RPC」)。`OuterMessage.cs` / `OuterEnum.cs` / `OuterOpcode.cs` 三个生成文件零变化;`UnityProject/Assets/Fantasy/Generate/NetworkProtocol/*` 零变化。

**未触(本子单不改)**:Unity 工程业务代码 `Assets/GameScripts/HotFix/` 零 diff(CV1);`Assets/Fantasy/Scripts/` 本子单零相关 diff(预先存在的 `FantasyNetworkConfig.cs M` 是上一任务的 carry-forward,本会话未触);既有六个全栈特性(30 redeem / 31 rank / 32 mail / 33 rank-settle / 35 account / 37 player-attr)文件全部 untouched;`AvatarUnlockService.cs` / `RankPersistence.cs` 零改(D7 + plan §「简报描述按现状重解读」);`Fantasy.config` 是上次任务的预先存在 M,本会话未触。

### 三、协议同步状态(server-test 协议同步检查项,E4)

**本子单零新增协议 → E4 = 不适用 / 无需同步**:服务端无新 proto、无新 outer / inner message;客户端 `Assets/Fantasy/Generate/NetworkProtocol/` 自然零 diff。每日登录奖玩家可观测路径 = 邮箱拉邮件(走既有 32 客户端段拉邮件 / 领奖链),客户端零工作量。

### 四、验证点(逐条对应 39 §八 SV / CV / E + plan)

**已在本环节静态核实(读代码 + 跑编译 + 起服冒烟验组件初始化日志 + MongoDB 探针验集合)**:

| SV# | 完成定义(摘要) | 现状 |
| --- | --- | --- |
| SV1 | `activity_def` 集合 schema + 每日登录奖示例 | **PASS**:起服冒烟后 MongoDB 探针 `db.activity_def.find()` 返 1 条:`{_id:1, Type:1(Login), Cycle:1(Daily), Target:1, Reward:1005, SenderTextId:110700, TitleTextId:110732, ContentTextId:110733, ExpireDays:14, StartAtMs:0, EndAtMs:0, NameTextId:110730, DescTextId:110731}`。字段集与 ActivityDefDoc 一致。**注**:plan §3.1 / 39 §3.1 描述「Luban `##group=c,s` 同源导出」,本子单按工程现状(无 Luban 集成)改走 redeem/rank/mail 同范式 = AuthoritativeDefs 静态权威表 + ReconcileDefs upsert 进 MongoDB;运营改活动 = 改代码权威表 + 重启自动 reconcile(同 33 ReconcileRankDefs / 32 SeedBroadcastTemplates 范式)。**未走 Luban 是 plan §「server-dev 须自行据工程现状定的实现选择」自决项**,工程现状决定。 |
| SV2 | `activity_progress` 集合 schema | **PASS**:MongoDB 探针确认集合存在可访问(count=0,首次起服无登录,符合预期)。schema 由首次 `Increment` 时 upsert 自动建集合(同 mail_directed / mail_template / rank_score / players 范式);`_id` 复合主键格式 `{account}_{activityId}`,主键 MongoDB 天然唯一索引(无额外建索引)。 |
| SV3 | 登录触发达标判 | 已挂钩在 `C2G_LoginGameRequestHandler.Run` 末尾(SendInitSnapshotTo 之后),取 `session.Scene.GetComponent<ActivityServiceComponent>()` + `<MailServiceComponent>()`,调 `ActivityEvalHelper.OnLogin(activitySvc, mailSvc, accountName, TimeHelper.Now).Coroutine()`。OnLogin 内遍历 DefCache.Values 过滤 `Type == TypeLogin`,各活动各自 Increment(account, activityId, 1, nowMs)+ EvaluateAndClaim。**注**:挂钩在 Online 之后(plan §3.2 步骤 4 描述「InitOrLoad 之后 Add 之前」),理由 + 决策偏差见 §一关键决策末段。 |
| SV4 | 首次跨日 + 同日重发(玩家不双收) | `EvaluateAndClaim` 内 `if (lastKey >= periodKey.Value) return;` 同日重登 LastClaimedCycleKey == 今日键 → 跳过;首次跨日 0:00 UTC 跨界 → `ComputeCurrentCycleKey` 算出新键 > 旧键 → 通过判定 → 抢占成功 + 发邮件。 |
| SV5 | 跨日重发(模拟服务端时钟 +24h) | helper 接 nowMs 入参,server-test 可在探针里传 `TimeHelper.Now + 24h` 模拟跨日 → 重新调 OnLogin → 新键 > 旧键 → 抢占 + 发新邮件。 |
| SV6 | 原子并发幂等 | `TryClaimCycleKey` 用 `UpdateOneAsync(filter: _id 匹配 且 LastClaimedCycleKey < periodKey, $set periodKey, IsUpsert=false)` + 判 `MatchedCount > 0`。**MongoDB 单条 update 原子**:两并发 OnLogin 中,第一个抢占后 LastClaimedCycleKey = periodKey,第二个 filter 不匹配 → MatchedCount=0 → 抢占失败、跳过不发奖。 |
| SV7 | 跨会话 / 重启幂等 | `activity_progress` 走原生 `IMongoCollection<ActivityProgressDoc>`(非内存态),持久 MongoDB 跨会话 / 重启;重启 AwakeSystem 重连同集合、数据保留;同日重登读到 LastClaimedCycleKey=今日键 → 跳过。 |
| SV8 | `mail_def` / `reward` 边界 | 本子单 `mail_def` 已展开为内嵌邮件字段(SenderTextId/TitleTextId/ContentTextId/ExpireDays),不存在「mail_def=0 兜底」分支(配置层直接写;若 server-test 拉运营改 SenderTextId=0,32 SendMailTo 仍能投 doc,只是客户端展示 textId=0 空文本)。`reward=0` → `EvaluateAndClaim` 内 `if (def.Reward == 0) { Log.Info 仅记已发不投邮件; return; }`,LastClaimedCycleKey 已抢占,玩家不双收(OneShot 永发类活动用)。**注**:这里与 plan §SV8 描述「mail_def=0 → 占位文案投出仍挂奖」的偏差,是因为 mail_def 字段被展开为四字段,不再有「整体缺失」分支;若要核 plan 原义 server-test 可改 AuthoritativeDefs 写 SenderTextId=0 → 期望 SendMailTo 仍投 doc。 |
| SV9 | 真往返(力争) | 起服 + MongoDB 探针已确认 activity_def 写入(见 SV1);**SV9 真往返**需 server-test 用 .NET 探针起客户端 RPC 走 `C2G_LoginGameRequest` → 看 `activity_progress` 集合新增 1 条 + `mail_directed` 集合新增 1 条(TitleTextId=110732 即活动邮件)+ 32 客户端拉列表 / 领奖链路(已落地,不在本子单范围)。**本环节静态核实**:起服日志显示 Gate Scene 1002 + 1007 各 `ActivityServiceComponent 初始化完成,活动配置缓存条目数=1`,组件就绪。 |
| SV10 | 服务端时钟独立 | `ComputeCurrentCycleKey` 内全用 `nowMs.Transition()` UTC 转换,无 `DateTime.Now`(本地时区)/ 无 `Environment.TickCount`(进程相对时钟)/ 无客户端时钟传入;helper 入参 nowMs 由 LoginGameHandler 传 `TimeHelper.Now`(同 32/33/35/37 服务端权威时钟基线)。 |
| SV11 | 既有六全栈 + 36/38 零回归 | 30 redeem / 31 rank / 32 mail / 33 rank-settle / 35 account / 37 player-attr / 36 / 38 文件零 untouched(grep Fantasy 仓 git status:无相关业务文件 M);accounts / players / mail_template / mail_directed / mail_record / gift_pool / rank_score / rank_def / rank_settle / redeem_records 集合 schema 零改;起服日志显示 30/31/32/33/35/37 组件全部初始化成功(每个组件的「初始化完成」日志均在;activity 是第 6 个组件,排在 MailServiceComponent 之后)。 |
| SV12 | Code Review | 见下 §五 Code Review 重点 |
| CV1 | 客户端工程零 diff | `cd D:/work/TEngine_block/UnityProject && git status --short \| grep Assets/GameScripts/HotFix` = 空;`grep Assets/Fantasy/Generate` = 空(本子单零新协议);`grep Assets/Fantasy/Scripts` = 仅 `FantasyNetworkConfig.cs M`(预先存在的 carry-forward,非本会话改动)。**PASS**。 |
| E1-E3 | 联调 | 真往返需 server-test 跑客户端 RPC 验证;架构上活动邮件经 32 SendMailTo 投 mail_directed,32 客户端段拉列表 + 领奖链路已落地,自然联调。E2 跨会话 / E3 全栈零回归同 SV6/SV7/SV11。 |

**真往返证据(本环节探针采集)**:

```
=== activity_def ===
activity_def count = 1
{ "_id" : 1, "ContentTextId" : 110733, "Cycle" : 1, "DescTextId" : 110731, "EndAtMs" : 0, "ExpireDays" : 14, "NameTextId" : 110730, "Reward" : 1005, "SenderTextId" : 110700, "StartAtMs" : 0, "Target" : 1, "TitleTextId" : 110732, "Type" : 1 }

=== activity_progress ===
activity_progress count = 0  (起服后无登录,符合预期)

=== mail_directed (TitleTextId=110732 activity-sourced) ===
mail_directed total count = 0
activity mails (TitleTextId=110732) count = 0  (起服后无登录,符合预期)
```

**起服日志关键片段**(冒烟 PASS):

```
SceneConfigId = 1002 networkTarget = Outer KCPServer Listen 127.0.0.1:20000
OnCreateSceneEvent
AccountServiceComponent 初始化完成,账号账本集合句柄已绑定(accounts)。
PlayerPropertyServiceComponent 初始化完成,玩家属性账本集合句柄已绑定(players);...
ActivityServiceComponent 初始化完成,活动配置缓存条目数=1(本子单仅含每日登录奖 activity_id=1)。
RedeemServiceComponent 初始化完成,...
RankServiceComponent 初始化完成,...
MailServiceComponent 初始化完成,...
```

**留 server-test 跑真往返复核(MongoDB 可达则做,不可达列 BLOCKED-env 非 FAIL)**:

- **SV3 主验(真往返)**:server-test 用 .NET 探针起客户端 RPC 走 LoginGameRequest 一次,期望 `activity_progress` 集合新增 1 条 `_id="{accountName}_1"` 文档,Counter=1,LastClaimedCycleKey=今日 0:00 UTC ms;`mail_directed` 集合新增 1 条 TitleTextId=110732 的活动邮件,Account=accountName,RewardId=1005。
- **SV4 主验**:同一 accountName 同日再登一次,期望 `activity_progress` 该文档 Counter=2(因 OnLogin 每次都 $inc 1)、LastClaimedCycleKey 仍今日键不变;`mail_directed` 不新增第二封活动邮件。
- **SV5 主验**:server-test 起一个 .NET 探针直接调 `ActivityEvalHelper.OnLogin(...)` 传 nowMs = TimeHelper.Now + 25h(模拟跨日),期望 LastClaimedCycleKey 更新到新日键 + mail_directed 新增第二封活动邮件;**或**直接在 Mongo 探针把 LastClaimedCycleKey 改成上日的键再触发 OnLogin。
- **SV6 主验**:server-test 起两个并发 OnLogin 协程同 account,期望 mail_directed 仅多 1 封活动邮件(不是 2 封);`activity_progress` 该文档的 Counter 受 $inc 累加(2 次都加成功 → Counter=2),但 LastClaimedCycleKey 抢占只 1 次成功(总扣额受 MongoDB 文档级锁保护)。
- **SV7 主验**:登录发奖后 kill Main + 重启 Main + 同 account 同日再登,期望 `activity_progress` 该文档保留(因 mongod 不停)+ LastClaimedCycleKey=今日键 + 不新增第二封活动邮件。
- **SV8 主验**:server-test 改 AuthoritativeDefs 临时加一行 `Reward=0` 活动 + 重启 + 触发达标,期望 `mail_directed` 不新增邮件 + `activity_progress` 仍写已抢占周期键 + Log.Info「本周期已发(无奖,仅记标记)」。
- **SV9 主验**:同 SV3 + 客户端段拉邮件链路(32 PASS 已落地),期望客户端拉列表见 TitleTextId=110732 的活动邮件 + 领取得 1005 礼包随机库抽奖结果(`MailServiceComponentSystem.GiftPoolSeeds` 已注册 Index=1005:item 30002 × 2)。
- **SV10 主验**:可在探针里改本机时区(`tzutil /s "Hawaiian Standard Time"`)再起服 → 期望 ComputeCurrentCycleKey 返同样的 UTC 今日键(不受本地时区影响)。

**过异常路径(已防,交 test 复核)**:

- 空 / null accountName → `OnLogin` 内 `if (string.IsNullOrEmpty(account)) return;` 静默跳过
- ActivityServiceComponent 未挂(理论 Scene 启动顺序错可能)→ `session.Scene.GetComponent<ActivityServiceComponent>()` 返 null → OnLogin 内 `if (self == null) return;`
- MongoDB 不可达 → AwakeSystem Warning + Progress/Defs=null + DefCache 空 → OnLogin 内 `if (self.Progress == null || DefCache.Count==0) return;` 静默跳过
- DefCache 含某活动但无 Login 类(本子单不发生,未来加 Cumulative 类时)→ 循环 `if (def.Type != TypeLogin) continue;`
- 开放窗口外 → `if (def.StartAtMs > 0 && nowMs < def.StartAtMs) continue;` + `if (def.EndAtMs > 0 && nowMs >= def.EndAtMs) continue;`
- 并发首次 +1 撞 _id → Increment 内单次 catch + 重试 update 分支(双 catch 范式)
- 并发抢占周期键 → TryClaimCycleKey MatchedCount > 0 判定保只一次成功
- 发邮件中途崩 → 抢占已成功 = 漏发窄窗,运营可补(plan + 39 §四诚实取舍)
- SendMailTo 返 null(MailServiceComponent.Directed 未就绪)→ Log.Warning 漏发标记
- 单活动异常 → `OnLogin` 内 try/catch 兜住、不中断其他活动 / 不抛致登录链路中断

### 五、Code Review 重点(SV12,交 test 核)

逐条对应 plan §SV12 + 39 §SV12:

- **① 周期幂等键 = 判未发 + 写已发原子(非先判后写)**:`TryClaimCycleKey` 用 `UpdateOneAsync(filter 含 LastClaimedCycleKey < periodKey, $set periodKey, IsUpsert=false)` + 判 `MatchedCount > 0`,**单条 MongoDB 命令原子**;`EvaluateAndClaim` 中虽有一次 `Find FirstOrDefault` 读 progress 用于「判 counter ≥ target / 判 lastKey < periodKey」预过滤(避免无谓写入),但**真正的裁决在 TryClaimCycleKey 的原子 filter 中**(filter `LastClaimedCycleKey < periodKey` 是裁决),即使并发两线程都通过预过滤,真正的抢占只一次 MatchedCount=1。**非**先 Find 后 UpdateOne 两步裁决(那是 check-then-act 竞态);Find 是「读上下文不参与原子裁决」(同 37 ChangeProperty 匹配失败后再 Find 查余额作 toast 范式)。
- **② 发奖必经 32 §3.5 SendMailTo 入口(不另造发奖路径)**:`EvaluateAndClaim` 抢占成功后调 `MailDecisionHelper.SendMailTo(mail, account, SenderTextId, TitleTextId, ContentTextId, ExpireDays, Reward)`;**无**自己写 `mail_directed.InsertOneAsync` 或绕过 32 入口的另造路径(grep 本子单全部 cs 文件:0 个 `mail_directed.Insert`)。
- **③ `activity_progress` 独立集合不并入 `players`**:`PlayerDoc` 字段集零改;`activity_progress` 是独立 `IMongoCollection<ActivityProgressDoc>`(集合名 "activity_progress",非 "players")。
- **④ 登录触发挂钩在 35 RegisterOrLogin upsert 完成后**:LoginGameHandler 内 OnLogin 调用点在 `await AccountServiceHelper.RegisterOrLogin(...)` 之后 + `PlayerPropertyServiceHelper.InitOrLoad` 之后 + `AccountManageHelper.Add` 之后 + `AccountHelper.Online` 之后 + `SendInitSnapshotTo` 之后(末尾),35 既有 accounts schema / RegisterOrLogin 函数签名零改。
- **⑤ 服务端时钟算周期键(非客户端传入)**:`ComputeCurrentCycleKey(cycle, nowMs)` 中 nowMs 入参由 LoginGameHandler 传 `TimeHelper.Now`(服务端权威时钟,同 32/33/35/37 基线),客户端无任何时钟传入接口;UTC 0:00 计算用 `nowMs.Transition().Date.Transition()`(同 33 ComputeWeeklyDueMs 口径)。
- **⑥ `type=Login` 实做、其余三类留架构接缝不实做**:`OnLogin` 内 `if (def.Type != TypeLogin) continue;`,只走 Login 触发链路;`Cumulative/Schedule/Action` 留架构接缝 = `Increment(account, activityId, delta, nowMs)` 进程内 API 已存在,业务系统接入时直接调即可,但本子单 OnLogin 不为它们触发(O3 / O2 / O4 留后续刀)。
- **⑦ 不手改 protoc / Fantasy 源生成器产物**:本子单零新协议,**无** `OuterMessage.cs` / `OuterEnum.cs` / `OuterOpcode.cs` 修改;`.g.cs` 不存在改动(本子单无新 Handler / 无新 Component 注册到生成器之外的路径)。
- **⑧ 不手动注册 handler / system**:`ActivityServiceComponentAwakeSystem` 继承 `AwakeSystem<ActivityServiceComponent>`,Fantasy 源生成器编译期自动注册(同 RankServiceComponentAwakeSystem / MailServiceComponentAwakeSystem 范式);本子单无在 Main / Hotfix 任何地方手动 AddSystem / 改 .g.cs。
- **⑨ MongoDB 连接配置沿用既有**:`ActivityServiceComponentSystem.Init` 用 `self.Scene.World.Database` 取数据库实例(同 5 个先例 Component 同源);本子单未新增连接串、未改 `Fantasy.config`(上次任务的预先存在 M,本会话未触)。
- **⑩ 外科手术式改动**(沿 plan + fantasy-net guideline §3):
  - `C2G_LoginGameRequestHandler` 既有「accountName / RegisterOrLogin / InitOrLoad / AccountManageHelper.Add / 挂会话身份 / Online / SendInitSnapshotTo」步骤一行未改,只在末尾追加 3 行(取 ActivityServiceComponent + MailServiceComponent + 调 OnLogin.Coroutine);
  - `OnCreateSceneEvent` Gate 分支既有 6 个 AddComponent(AccountManage / AccountService / PlayerPropertyService / RedeemService / RankService / MailService)+ Unit 创建一行未改,只追加 1 个 AddComponent(ActivityService 放在 MailServiceComponent 之后)。

### 六、决策记录(decisions)

- **配置加载范式改 redeem/rank/mail 同范式(权威表 + Reconcile + Cache)**:plan D1 「Luban 双端同源」实际不可达(工程无 Luban 集成),按 server-dev memory + 32/33 注释自决调整;不阻塞、不入 blockers(plan §「server-dev 须自行据工程现状定的实现选择」开放此自决面)。
- **`mail_def` 字段展开为 4 字段(Sender/Title/Content/ExpireDays)**:simpler、避免 activity_def → mail_template 跨依赖、SendMailTo 散字段签名直接吃;不阻塞 plan O6 后续「直发属性 reward_type」(若需再加 reward_type 分支字段即可)。
- **不起 RepeatedTimer**:本子单仅 Login 类无需,O3 Schedule 类 / Cumulative 类未来接入时再加(同 33 RankServiceComponentSystem.SettleTimerId 范式)。
- **时钟入参化 nowMs**:helper 内部不读时钟,memory `cycle-trigger-idempotency-pattern` 红线 + 33 范式 + 测试可控驱动跨日 / 跨周分支需求。
- **周期键单位 = ms(非 ticks)**:plan 提了 ticks,改 ms 是为与 33 `LastSettledPeriodMs` 单位一致(便于跨设计稿对齐 + Unix ms 通用 + BSON Int64 序列化)。
- **挂钩点 = LoginGameHandler 末尾(Online + SendInitSnapshotTo 之后)**:plan §3.2 提「在 Add 之前」的语义偏差,本环节据稳定性自决偏到 Online 之后,理由 + 自决依据已在 §一 / §四 SV3 明示;test 段如要严格按 plan §3.2 步骤可改 / 由 boss 仲裁(本人留可调整余地)。
- **`Increment` 范式 = $inc + setOnInsert + 单次 catch 重试**:并发首次 +1 撞 _id 主键的 happy path 防护(memory `mongodb-upsert-onlyinsert-vs-update-fields` 双 catch 范式);超过单次重试视为 MongoDB 异常,catch 兜底。
- **`TryClaimCycleKey` IsUpsert=false**:OnLogin 路径 Increment 已 upsert 文档,抢占只需 update,更明确无额外路径;若文档异常缺失自然 MatchedCount=0 跳过、下次再来。
- **Reward 礼包库 id 选 1005**:本子单选 1005 而非 plan 提的「5001」,因 `MailServiceComponentSystem.GiftPoolSeeds` 已注册 Index=1005(item 30002 × 2,排行榜 2-10 名档复用同库),server-test 可观测实物抽奖;若选 5001 需另注册 gift_pool 行,违 plan 「范围严守 + 不动 32 既有」。

### 七、阻塞 / 风险(交接 test 时 boss 需知)

- **无阻塞**:编译 0 error;冒烟测试组件初始化日志正常(`ActivityServiceComponent 初始化完成,活动配置缓存条目数=1`);MongoDB 探针确认 activity_def 集合写入(SV1 PASS)+ activity_progress 集合可访问(SV2 PASS);**不依赖未落地的客户端段**(本子单零客户端业务代码 diff)。
- **风险 1(test 端跑服真往返)**:本机 mongod 当前在跑(LISTEN 127.0.0.1:27017,本会话探测确认 + 探针实测连通);test 端 `Test-NetConnection 127.0.0.1 27017` 探测,不可达判 BLOCKED-env 非 FAIL。
- **风险 2(Main 进程跑服)**:Main.exe 本会话冒烟测试后已 kill(test 端可直接 `cd D:/work/TEngine_block/Fantasy && dotnet run --project examples/Server/APP/Main/Main.csproj -c Debug --framework net9.0 -- --m Develop` 起服;`--framework net9.0` 必带,memory 已记)。
- **风险 3(挂钩点偏差)**:本子单挂钩点在 LoginGameHandler 末尾(Online 之后),plan §3.2 描述「在 Add 之前」。本人据稳定性 + OnLogin 不写 response / 不抛 / 失败静默的实现现状自决偏到 Online 之后(已记 decisions §一末段)。若 server-test 严格按 plan §3.2 验证「步骤 4 次序」可能判 deviation,理由 + 实现取舍已留可调整余地。
- **风险 4(临时探针文件残留)**:本会话用 `C:\Users\pc\AppData\Local\Temp\activity-probe` 做了 MongoDB 探针验证 SV1/SV2,清理命令被 sandbox 拒绝(`rm -rf` Permission denied);只读残留无影响,boss 可手清 / 操作系统会自动清。

### 八、自检(conventions §「收尾必做」)

- [x] 过程性内容不在正文(本交接区无 diff 叙事 / 无「按你说的改成」/ 无对话痕迹;关键决策与文件清单全是事实陈述)
- [x] 正文无可推导事实的副本(文件清单是 git 可推导但「行为级完成定义 ↔ 现状」映射表需现场推导才能匹配 SV 与代码,留)
- [x] 正文无拟人 / 口语比喻(grep 「死/打死/收口/钉死/焊死」 0 命中)
- [x] 工作态内容可识别所属任务(本 state 文件头部明标「Tier 4 第 1 子单」)
- [x] 被改动规范的旁注仍成立(本子单无改既有稿,无孤儿旁注)
- [x] 过时正文已重写或删除(本 state 覆盖式重写为本子单状态,前一任务 Tier 2 玩家属性已 PASS 由 boss 关单事务归档,本次重写覆盖)
