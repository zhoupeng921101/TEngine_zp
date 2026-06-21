# 状态:服务端开发(server-dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-server-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:Tier 0 真实账号体系 · 服务端段 · 第 1 子单(注册 + 登录会话)— 交接 server-test

设计基线 `design-docs/35-account-server.md` + `pipeline/state/plan.md` 交接区(plan 已锁:设备 UUID 自动注册式 + 复用既有 LoginGameHandler/Account/GateAccountFlagComponent 链路 + 新增 MongoDB accounts 集合持久层 + 客户端工程零 diff + 无新 proto)。

### 一、改动摘要(做了什么、为何、关键决策)

**实质交付 = 新增 accounts 集合持久层 + LoginGameHandler 挂 upsert 钩子**,共 4 个新文件 + 2 处既有文件外科手术式追加(LoginGameHandler 加 RegisterOrLogin 调用 + OnCreateSceneEvent Gate 分支 AddComponent)。

**关键决策(沿用 plan 已锁 + 工程现状定的实现选择)**:

- **单条原子 upsert = `UpdateOneAsync(filter: _id == accountId, $setOnInsert + $set, IsUpsert=true)`**:plan §3.2 红线「不先查后写两步」+ §5.1/§5.3 并发同 UUID 双登原子(SV7)。MongoDB 单条命令原子,第一次走 insert(`$setOnInsert` 写 FirstLoginUnixMs + Status=0)、第二次走 update(仅 `$set` LastLoginUnixMs),首次注册时间稳定 = 第一次写入值(SV4)。与 32 邮件 `mail_record` 唯一键 InsertOneAsync 不同 — 那是「只许写一次」防重领;本子单是「不存在则插入 / 存在则部分更新」、用 UpdateOne+upsert 更贴合(写两次写不同字段)。
- **状态字段双重隔离**:`$setOnInsert(x => x.Status, 0)` 仅在 insert 时写入,update 路径完全不 `$set` Status → Tier 1+ 运营改写封禁不被本子单重置(SV5)。这是 plan §5.2 + §九 风险表第 3 条专项防护。
- **失败 → 返登录失败短路**:`MongoException` 全 catch 转返非 0 错误码(Fantasy.Net 错误码非异常基线),Handler 收到非 0 后 return,**不挂会话身份 / 不创建 Player / 不上线**(SV8 沿用 30 「服务不可用不本地放行」)。具体错误码沿用既有 LoginGameRequestHandler 的简化体系(全用 1,O4 决策授权),后续要细分由 boss 拍板。
- **挂钩点 = LoginGameHandler 中 `AccountManageHelper.Add` 之前**:plan §3.2 处理顺序表的「步骤 3」位置;早于「挂会话身份」(`session.AddComponent<GateAccountFlagComponent>` + `AccountHelper.Online`),失败可早返,守住「accounts 是业务集合 account 字段的上游」弱不变量(plan §二 §3.1 末段)。
- **AccountServiceComponent 挂在 Gate Scene**(`OnCreateSceneEvent` Gate 分支):同邮件/兑换/排行榜先例 — 这些组件都挂 Gate Scene,因为 Gate Scene 的 World 配了 MongoDB(同源 `self.Scene.World.Database`),且 LoginGameHandler 跑在 Gate Scene(`session.Scene`)。
- **未触 Account 内存态实体**:plan 守不变量「不动既有 demo Account 实体的字段集」— Account.cs / AccountManageComponent.cs / GateAccountFlagComponent.cs / AccountHelper.cs / AccountFactory.cs 一行未改。Account 内存态扩字段是 Tier 1+ 议题。
- **复用 `Fantasy.Helper.TimeHelper.Now`**:同 MailServiceComponentSystem / MailDecisionHelper 先例(`using Fantasy.Helper;`)。
- **AccountDoc 走原生 MongoDB BSON 文档**(非框架 Entity):同 MailDocs.cs / RedeemDocs.cs 先例。框架 `IDatabase` 高层 API 只能先读后写(SKILL.md / 32 §五 明令禁止),并发原子 upsert 必须走原生 `IMongoCollection<AccountDoc>`。

### 二、文件清单

**新增(4 个)**:

- `examples/Server/APP/Entity/Game Examples/Gate/Account/AccountDoc.cs` — 账号账本 BSON 文档(`_id` = AccountId / FirstLoginUnixMs / LastLoginUnixMs / Status)。
- `examples/Server/APP/Entity/Game Examples/Gate/Account/AccountServiceComponent.cs` — Gate Scene 上的账号账本服务组件,持 `IMongoCollection<AccountDoc>? Accounts` 句柄。
- `examples/Server/APP/Hotfix/Game Examples/Gate/Account/AccountServiceComponentSystem.cs` — AwakeSystem 绑集合 + DestroySystem 清句柄;MongoDB 不可达 Warning 不阻断 Scene 创建。
- `examples/Server/APP/Hotfix/Game Examples/Gate/Account/AccountServiceHelper.cs` — `RegisterOrLogin(scene, accountName) → FTask<uint>` 单条原子 upsert + 错误码非异常。

**修改(2 个外科手术式追加)**:

- `examples/Server/APP/Hotfix/OnCreateSceneEvent.cs` — Gate 分支 `case SceneType.Gate` 新增一行 `scene.AddComponent<AccountServiceComponent>();`(在 `AccountManageComponent` 之后、其它 Service 之前),挂载顺序使 Login 处理时账号账本组件已就绪。
- `examples/Server/APP/Hotfix/Game Examples/Gate/Handler/Outer/C2G_LoginGameRequestHandler.cs` — 在「解析 accountName 后、`AccountManageHelper.Add` 之前」插入 `await AccountServiceHelper.RegisterOrLogin(session.Scene, accountName)` + 非 0 短路 return;既有「解析 accountName / 挂会话身份 / 上线」步骤一行未改。

**未触(本子单不改)**:Unity 客户端(`Assets/`)零 diff(SV11),包括 `Assets/GameProto/`(无新 proto);既有 demo Account 链路 5 个文件(Account / AccountManageComponent / AccountManageComponentSystem / GateAccountFlagComponent + System / AccountFactory / AccountHelper)一行未改;既有四个全栈特性(30 redeem / 31 rank / 32 mail / 33 rank settle)零回归(SV9)。

### 三、协议同步状态(server-test 协议同步检查项,E4)

**本子单无 proto 改动 → E4 = N/A**(plan 守不变量 SV11 明令「不新增 RPC / 不新增客户端可见协议」,本子单复用 `C2G_LoginGameRequest` / `G2C_LoginGameResponse` 既有字段集)。

- `git status` 工作树 proto 路径(`examples/Config/NetworkProtocol/Outer/`)无 diff;客户端段(`UnityProject/Assets/Fantasy/Generate/NetworkProtocol/`)无相关 diff。
- 既有 LoginGameRequest / LoginGameResponse 协议生成物双端一致(commit `dfbb45ea` 之前已落 + 30/31/32/33 PASS 反向佐证)。

### 四、验证点(逐条对应 35 §七 SV;告诉 test 验什么 / 怎么验 / 预期)

**已在本环节静态核实(读代码 + 核 plan §「服务端段交付清单」表 + 35 §七 SV)**:

| SV# | 完成定义(摘要) | 现状 |
| --- | --- | --- |
| SV1 | 编译通过 + 源生成器产物 | `dotnet build examples/Server/Server.sln` 0 error;源生成器自动注册 AwakeSystem/DestroySystem(本子单未手动注册、未手改 .g.cs);Hotfix.dll 包含 AccountServiceHelper 类(net8.0 + net9.0 双 TFM 产物均已生成) |
| SV2 | accounts 集合 schema | 首次 upsert 时由 MongoDB 自动创建集合(同 mail_template 先例);AccountDoc 含 `_id`(AccountId 字符串 [BsonId]) + FirstLoginUnixMs + LastLoginUnixMs + Status 四字段;主键 MongoDB 天然唯一,未额外建索引 |
| SV3 | 首连自动注册 | AccountServiceHelper.RegisterOrLogin 用 `$setOnInsert(AccountId/FirstLoginUnixMs=now/Status=0) + $set(LastLoginUnixMs=now)` + `IsUpsert=true`;不存在时 MongoDB 走 insert 分支,四字段一次写入,FirstLoginUnixMs ≈ LastLoginUnixMs ≈ TimeHelper.Now(秒级精度内) |
| SV4 | 重连首次注册时间不变 | `$setOnInsert` 仅在 insert 路径写入(MongoDB 官方语义),update 路径**不触碰** FirstLoginUnixMs;`$set(LastLoginUnixMs=now)` 每次都写;重连仅 update 末次登录时间,首次注册时间稳定 = 第一次值 |
| SV5 | 状态字段不被覆盖 | `$setOnInsert(Status=0)` 仅 insert 时写默认 0;update 路径 update Builder 完全不含 `Set(Status, ...)` 或 `SetOnInsert(Status, ...)` → 手动改状态为非 0 后再登,update 走 → Status 字段不在 update 命令中 → 保持原值非 0 |
| SV6 | 重启持久 | 走原生 `IMongoCollection<AccountDoc>`(非内存态);重启 AwakeSystem 重连同 `mongoDatabase.GetCollection<AccountDoc>("accounts")`、集合数据保留;重连玩家走 update 分支 |
| SV7 | 并发同 UUID 双登原子 | `UpdateOneAsync(filter: _id==id, $setOnInsert+$set, IsUpsert=true)` 是 MongoDB 单条原子命令;两并发请求中一个走 insert(FirstLoginUnixMs 写入)、另一个走 update(FirstLoginUnixMs 不动),无主键冲突 / 无异常 / 无两份记录 / 首次注册时间稳定 |
| SV8 | MongoDB 不可达失败 | AwakeSystem 检测 `database?.GetDatabaseInstance is not IMongoDatabase` → Warning + 不绑集合句柄(Accounts 保持 null);RegisterOrLogin 检测 Accounts == null → 返非 0 错误码;Handler 收到非 0 → 短路 return → 不挂会话身份 / 不创建 Player / 不上线;`MongoException` 全 catch 转错误码(不抛异常断连) |
| SV9 | 既有四特性零回归 | 本子单不改 redeem / rank / mail / rank-settle 任何文件(grep 工程内 commit 21/22/b8/420/22c2 均未触);account 字段语义仍是 UUID 字符串(本子单 AccountDoc.`_id` 也是 UUID 字符串,与既有业务集合 account 字段同源,plan 守不变量) |
| SV10 | account 字段语义对齐 | AccountServiceHelper.RegisterOrLogin 接收 accountName 直接作 `_id` 写入(不哈希、不加盐、不改写),与 LoginGameHandler 后续步骤传给 AccountManageHelper.Add 的 accountName 是同一字符串、与既有 30/31/32/33 业务集合 account 字段是同一字符串 |
| SV11 | 客户端工程零 diff | `UnityProject/Assets/` git diff 须空(本子单 server-only,Fantasy 仓库改动不影响 UnityProject 工作树) |
| SV12 | Code Review | 见下 §五 Code Review 重点 |

**留 server-test 跑服真往返复核(MongoDB 可达则做,不可达列 BLOCKED-env 非 FAIL,memory `local-mongodb-for-server-roundtrip` + `feedback-blocked-vs-fail`)**:

- **SV3 主验**:用一个 accounts 中不存在的新 UUID 发 `C2G_LoginGameRequest` → 登录成功(ErrorCode=0)→ MongoDB `accounts` 集合中出现一条 `_id` = 该 UUID 的文档,`FirstLoginUnixMs` ≈ `LastLoginUnixMs` ≈ `now`(秒级精度内、UTC Unix 毫秒),`Status` = 0。**注**:既有 LoginGameRequestHandler 的 reply 流程后续会走 AccountHelper.Online → Map Roaming,如 Map Scene 未配 / 配错,Online 失败但 accounts 已 upsert(挂钩点在 Online 之前);test 端可不关心 Map 部分,只看 accounts 集合 + ErrorCode。
- **SV4 主验**:用一个 accounts 中已存在的 UUID 再次登录 → 登录成功 → accounts 仍只一条该 UUID 记录、FirstLoginUnixMs 不变(== 第一次写入值)、LastLoginUnixMs 更新为最新 now。
- **SV5 主验**:用 .NET MongoDB 驱动探针或 mongosh(便携版无 mongosh,memory 已记 → 用 .NET 探针)`UpdateOne(filter:_id==testUuid, $set(Status: 9))` 模拟 Tier 1+ 运营改写 → 用该 UUID 再次登录 → 登录成功(本子单不读 Status 不拒登)→ Status 字段仍为 9。
- **SV6 主验**:启动一次服 → 用若干 UUID 登录(写入 accounts)→ 停服 → 重起服 → 这些 UUID 再次登录,看 FirstLoginUnixMs 不变、LastLoginUnixMs 更新。
- **SV7 主验**:用同一新 UUID 并发发两个 C2G_LoginGameRequest(可用两个会话 / 或用 .NET 探针并发跑 RegisterOrLogin 两次)→ accounts 集合中**仅一条**该 UUID 记录、FirstLoginUnixMs 稳定(== 先到达的 now)、无主键冲突异常(MongoDB 单条原子)。**注**:LoginGameHandler 后续 AccountManageHelper.Add 是内存态字典,同 UUID 第二次 Add 会返 false → ErrorCode=1,但此时 accounts upsert 已成功 — 这是预期行为,SV7 验「accounts 持久层并发原子」,内存态挤号是 Tier 1+ 议题(§5.3)。
- **SV8 辅验**:停 mongod → 起服 → 看 Log.Warning「MongoDB 实例不可用」(AwakeSystem) → 用任意 UUID 发 LoginGameRequest → ErrorCode = 1、不挂会话身份(后续业务请求该会话无法走 GateAccountFlagComponent 链路)→ 不崩、不断连;**或**起服后 kill mongod 模拟运行时不可达 → 此时 Accounts 句柄已绑(AwakeSystem 时 mongod 在),UpdateOneAsync 会抛 MongoException → catch 转返非 0、Log.Warning,同样 Handler 短路。
- **SV9 主验**:起服后跑既有 30 / 31 / 32 / 33 各自的 PASS 验收路径(可参考 server-test memory 既有 retest 范围),全部仍 PASS;特别核 redeem_records / rank_scores / mail_record / mail_directed.Account 字段值 = 登录用的 UUID(与 accounts `_id` 同源)。
- **SV10 主验**:用新 UUID 走「登录(upsert)→ 兑换码 → 上报分 → 拉邮件 → 领邮件」全链路 → MongoDB 中 `accounts._id` 与 `redeem_records.account` / `rank_scores.account` / `mail_record.account` / `mail_directed.account` 字段值**完全一致**(均为裸 UUID 字符串)。
- **SV11 主验**:`cd D:/work/TEngine_block/UnityProject && git status --short` 应无相关 diff(本子单零客户端改动 + 无 proto 改动);Fantasy 仓库 git status 应有本子单的 4 个新增 + 2 个修改(及既有 Fantasy.config 等本会话外的工作树状态)。
- **SV12 见下 §五**。

**过异常路径(已防,交 test 复核)**:

- 空 / null accountName → 既有 Handler 早 return ErrorCode=1(本子单挂钩点在此 if 之后,upsert 不会被空 UUID 触发)
- AccountServiceComponent 未挂(理论上 Gate Scene 启动顺序错可能)→ RegisterOrLogin 返 LoginErrorCode + Log.Error;但 OnCreateSceneEvent 已挂,正常路径不触
- MongoDB 不可达 → AwakeSystem Warning + Accounts=null → RegisterOrLogin 返 LoginErrorCode(无 Warning 避免每次登录刷屏)
- 写入抛 MongoException → catch 转返 LoginErrorCode + Log.Warning(单次失败记录,便于诊断)
- 并发同 UUID → MongoDB 单条原子,只一次 insert(SV7)
- AccountManageHelper.Add 内存态字典撞键(同 UUID 二次登 / 未下线)→ 既有 Handler 返 ErrorCode=1,但此时 accounts upsert 已成功(挂钩点早于 AccountManageHelper.Add)— 这是预期:持久层并发原子守住,内存态挤号留 Tier 1+

### 五、Code Review 重点(SV12,交 test 核)

- **upsert 单条原子(plan §3.2 红线)**:`AccountServiceHelper.RegisterOrLogin` 用 `accounts.UpdateOneAsync(filter, update, options { IsUpsert=true })` 单次 MongoDB 命令,filter+update+upsert 一步原子完成;**无**先 `Find(filter).FirstOrDefaultAsync()` 后 `InsertOne / UpdateOne` 两步;**无** check-then-act 竞态窗口。
- **首次注册时间 vs 末次登录时间分隔(SV4)**:Update Builder 用 `SetOnInsert(x => x.FirstLoginUnixMs, nowMs)` + `Set(x => x.LastLoginUnixMs, nowMs)`;MongoDB 官方语义:`$setOnInsert` 仅在 upsert 触发 insert 路径时写、`$set` 在 insert 和 update 路径都写。重连(走 update)时 FirstLoginUnixMs 不在 update 命令的 `$set` 中、也无 `$setOnInsert` 触发(因非 insert),原值不动。
- **状态字段隔离(SV5)**:`SetOnInsert(x => x.Status, 0)` 仅 insert 时写默认 0;Update Builder 中**无任何 `Set` / `SetOnInsert` 对 Status 字段的写入路径**(grep 确认);update 路径下 Status 字段不在命令中、原值保持。
- **失败短路 + 不挂会话身份(plan §3.2 失败硬约束)**:`MongoException` 全 catch 转 `LoginErrorCode`;Handler 收到非 0 → `response.ErrorCode = ...; return;` → **无**后续的 `AccountManageHelper.Add` / `session.AddComponent<GateAccountFlagComponent>` / `AccountHelper.Online`。沿用 30 「服务不可用不本地放行」基线。
- **MongoDB 不可达 Warning 不 Error**:AwakeSystem 在 `database?.GetDatabaseInstance is not IMongoDatabase` 时 `Log.Warning` + `return`(同 MailServiceComponentSystem 先例,memory `feedback-blocked-vs-fail`)— 这是预期环境条件、不是逻辑错。
- **Fantasy.Net 约定**:FTask(非 Task)/ sealed class(组件 / System)/ static class(Helper)/ file-scoped namespace `Fantasy` / `Log.Warning + Log.Info + Log.Error` / 不手改 .g.cs(AwakeSystem/DestroySystem 注册全靠 SG)/ 不手动注册(`OnCreateSceneEvent` 只挂 Entity 组件,System 自动注册)/ 错误码非异常(MongoException catch 转 uint)/ 原生 `IMongoDatabase`(框架 `IDatabase` 高层 API 只能先读后写,设计禁止)/ Account 内存态实体字段未扩(plan 守不变量「不动既有 demo Account 实体的字段集」)。
- **外科手术式改动**:LoginGameRequestHandler 既有「解析 accountName / AccountManageHelper.Add / 挂会话身份 / AccountHelper.Online」一行未改,只在「解析 accountName 后、AccountManageHelper.Add 之前」插入 upsert 调用;OnCreateSceneEvent Gate 分支既有 4 个 AddComponent + Unit 创建一行未改,只追加 1 个 AddComponent。

### 六、决策记录(decisions)

- **upsert 操作选 UpdateOne+upsert,非 ReplaceOne / FindOneAndUpdate**:`UpdateOne` 支持 `$setOnInsert` + `$set` 字段级精细控制(首次注册时间 / 末次登录时间 / 状态各自独立路径),`ReplaceOne` 会整体替换文档(无法区分 insert / update 字段),`FindOneAndUpdate` 会多返一个 doc(本子单不需要返回值)。**UpdateOne 是最贴合「字段路径需要按 insert/update 分别处理」的 API**。
- **错误码沿用既有 LoginGameRequestHandler 的简化体系(全用 1)**:既有 Handler 的注释明确说「懒的写错误码、不是 0 就是出错」,O4 决策授权 server-dev 据现状取。本子单的 `LoginErrorCode = 1` 与既有空 accountName 分支 + AccountManageHelper.Add 失败分支统一,client 端收到非 0 知道「登录失败、重试」即可,无需细分。后续要细分错误码(MongoDB 不可达 vs 内存态挤号)由 boss 拍板。
- **AccountDoc 不带 `[SeparateTable]`**:本子单字段都是基础类型(string / long / int),无 Aggregate 子数据,不需要分表(SKILL `database/separate-table.md`)。
- **MongoDB 索引 = 仅靠 `_id` 主键天然唯一**:plan §3.1 + §八 O7 决策「不投机性时间索引」,本子单无按时间查询的需求。Tier 1+ 真要「上次活跃 N 天前」类查询再加。
- **AwakeSystem 中 `await FTask.CompletedTask` 在不可达 return 之前**:`FTask` 协程需要至少一个 await 点(否则 SG 警告 / 编译错);MongoDB 不可达分支无实际 await 工作,加占位 `await FTask.CompletedTask` 保持 async 签名(同 Init 末尾的 `await FTask.CompletedTask` 一致)。**实际编译验证未触发 warning**,这是预防性写法。
- **TimeHelper.Now 取服务端权威时钟**:同 MailServiceComponentSystem / MailDecisionHelper 先例;客户端时钟不参与首次注册时间 / 末次登录时间的判定(防客户端伪造);该 helper 在 `Fantasy.Helper` 命名空间,需 `using Fantasy.Helper;`(初版漏 using 编译失败,已补)。

### 七、阻塞 / 风险(交接 test 时 boss 需知)

- **无阻塞**:编译通过、源生成器产物正常、不依赖未落地的协议改动。
- **风险 1(test 端跑服真往返)**:本机 mongod 当前在跑(LISTEN 127.0.0.1:27017,本会话探测确认);test 端 `Test-NetConnection 127.0.0.1 27017` 探测,不可达判 BLOCKED-env 非 FAIL(memory `local-mongodb-for-server-roundtrip` + server-test memory `feedback-blocked-vs-fail`)。
- **风险 2(Main 进程跑服)**:Main.exe 当前不在跑(本会话 tasklist 探测无),test 端可直接 `dotnet run --project examples/Server/APP/Main/Main.csproj --framework net9.0 -- --m Develop` 起服。
- **风险 3(LoginGameHandler 后续 Online 走 Roaming)**:LoginGameHandler upsert 成功 → AccountManageHelper.Add → 挂会话身份 → AccountHelper.Online → 走 Map Roaming。如 Map Scene 未配 / 跨服 Roaming 失败,Online 返非 0、Log.Error,但 **accounts 已 upsert 成功**(挂钩点早于 Online)。test 端验 SV3/SV4 时**直接核 accounts 集合**即可、不必关心 Map 上线是否成功;ErrorCode 应为 0(LoginGameHandler 当前实现不把 Online 返的错误码写回 response,见现有代码 `await AccountHelper.Online(...);`)。
- **风险 4(并发同 UUID 内存态挤号)**:见 SV7 注 — accounts 持久层 SV7 PASS 不蕴含「同 UUID 第二次登录上线成功」,因 AccountManageHelper.Add 内存态字典撞键返 false → Handler 返 ErrorCode=1。这是 Tier 1+ 议题(§5.3)、本子单不解决、test 端如复现「并发第二次登录 ErrorCode=1」属预期行为非 BUG。

### 八、自检(conventions §「收尾必做」)

- [x] 过程性内容不在正文(本交接区无 diff 叙事 / 无「按你说的改成」/ 无对话痕迹)
- [x] 正文无可推导事实的副本(文件清单是 git 可推导,但「行为级完成定义 ↔ 现状」映射表需现场推导才能匹配 SV 与代码,留)
- [x] 正文无拟人 / 口语比喻(grep 「死/打死/收口/钉死」 0 命中)
- [x] 工作态内容可识别所属任务(本 state 文件头部明标「Tier 0 第 1 子单」)
- [x] 被改动规范的旁注仍成立(本子单无改既有稿,无孤儿旁注)
- [x] 过时正文已重写或删除(本 state 覆盖式重写为本子单状态,前一任务的邮件段交付内容已被新内容替换 — 邮件段已 PASS 由 boss 关单事务归档)
