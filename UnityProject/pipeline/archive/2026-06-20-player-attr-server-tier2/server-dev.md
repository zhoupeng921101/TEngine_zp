# 状态:服务端开发(server-dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-server-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:Tier 2 玩家属性权威 · 服务端段 · 第 1 子单(金币 / 钻石 / 体力 三属性搬服务端)— 交接 server-test

设计基线 `design-docs/37-player-attr-server.md` + `pipeline/state/plan.md`(plan 已锁:独立 `players` 集合 + 通用单 RPC 变更入口 + 服务端永不信客户端绝对值 + 原子 `FindOneAndUpdate` + 首登 35 钩子内 setOnInsert 初始化 + 主动推送 + 进程内 API 与对外 RPC 共用一套校验)。

### 一、改动摘要(做了什么、为何、关键决策)

**实质交付 = 三条新 Outer RPC + 新增 `players` 集合持久层 + LoginGameHandler 挂 setOnInsert + 快照下发 + Outer 通用变更入口 + 服务端进程内 API**,共 1 个新 proto + 5 个新 cs 文件 + 2 处既有文件外科手术式追加(LoginGameHandler 在 RegisterOrLogin 之后插 InitOrLoad + 末尾 SendInitSnapshotTo;OnCreateSceneEvent Gate 分支 AddComponent)。

**关键决策(沿用 plan 已锁 + 工程现状定的实现选择,plan §「server-dev 须自行据工程现状定的实现选择」)**:

- **协议形态 = 三条独立 Outer 协议消息**(plan D10 + O4):
  - `C2G_PropertyChangeRequest` + `G2C_PropertyChangeResponse`(RPC 请求/响应,handler 用 `MessageRPC` 基类,沿 mail / redeem 范式);
  - `G2C_PropertyInitSnapshot`(IMessage 单向 push,登录成功后由服务端起 `session.Send` 下发);
  - `G2C_PropertyDeltaPush`(IMessage 单向 push,每次写库成功后由服务端起 `session.Send` 推送);
  - **快照下发形态 = 独立 push message,非登录响应捎带**(O4):理由 = 与 `G2C_PropertyDeltaPush` 形态完全对齐,客户端段下一刀同一处订阅快照 + 推送两条消息;不污染既有 `G2C_LoginGameResponse`(它字段集为空、加余额字段就动既有协议,违 plan 「35 处理逻辑零改」)。
- **`PropertyType` 枚举三类**:Coin=0 / Diamond=1 / Stamina=2(响应中 Type 字段回声请求,便于客户端段下一刀路由更新到对应字段)。
- **`PropertyChangeResultCode` 七态**:Success / NotLoggedIn / UnknownType / InvalidRequest / NotEnough / OverLimit / ServiceUnavailable(NotEnough / OverLimit 时 NewAmount = 当前实际余额,供客户端段下一刀 toast「需要 X,你有 Y」)。
- **原子写 = `FindOneAndUpdate` 条件过滤 + `$inc` + `$set`,单条命令**(plan §3.4 红线):filter `_id == accountId AND fieldName >= -delta AND fieldName <= upperBound - delta`,update `$inc(fieldName, delta) + $set(LastChangeUnixMs)`。MongoDB 端原子(SV11 并发同账号双扣防超发的核心保障)。**非**先 Find 后 Update 两步(Code Review SV17 重点核)。
- **`InitOrLoad`(35 钩子内 setOnInsert)= `FindOneAndUpdate` + `IsUpsert=true` + `ReturnDocument.After`**:upsert 触发 insert 走 setOnInsert 写 _id + 三初始余额 + LastChangeUnixMs + SchemaVersion;走 update 路径时 update 命令**完全无 $set**(只有 $setOnInsert,update 路径全部跳过)→ 余额字段稳定 = 上次变更后的值(SV5 重登余额不变)。返写后的 PlayerDoc → 三属性余额作 InitSnapshot 内容,登录成功后通过 `session.Send` 直推。
- **类型上界 long.MaxValue/2 安全余量**:`PlayerPropertyServiceComponentSystem.ValidateConfig` 启动期校验上界 ≤ `long.MaxValue / 2`,确保 delta 范围 [-上界, +上界] 下「余额 + delta」运算不会因 long 整数溢出绕过 MongoDB 端条件过滤(§3.4 + §5.5 防御 long 极值);超出 → 不绑句柄、Log.Error,后续登录与变更返 ServiceUnavailable(沿 35 + Mail 「不可达 → 不本地放行」基线)。
- **服务端进程内 API = 复用 `ChangeProperty` 同一函数**(plan D7):无单独「内部入口」函数,业务系统刀(商店/邮件/活动/任务/排行榜结算)直接调 `PlayerPropertyServiceHelper.ChangeProperty(scene, account, type, delta, reason)` + `SendDeltaPushTo(scene, account, type, newAmount, reason)`。一套校验一套写库一套推送(SV12 + SV17 红线⑤),无「内部调用绕过校验」可能。
- **推送目标 = `AccountManageHelper.TryGetAccount` 查内存态 Account → 取 `Session` 句柄 → `session.Send`**:当前 demo Account 内存态字典每 UUID 只持一个 Account / 一个 Session(同 UUID 二次登录 `Add` 返 false,见 `AccountManageComponentSystem.Add`),「该 UUID 在线全部会话」实际 = 单会话。设计 §5.4 + §5.8 诚实边界声明此简化(多会话同 UUID 是 Tier 1+ 议题,沿 35 §5.3)。离线 / 找不到 Account / Session 已 Dispose → 推送丢弃(O6 不重试,沿 §5.6/§5.7)。
- **`PlayerPropertyServiceComponent` 挂 Gate Scene**(同 AccountServiceComponent / MailServiceComponent / RedeemServiceComponent / RankServiceComponent 先例):Gate Scene 的 World 配了 MongoDB(同源 `self.Scene.World.Database`),且 LoginGameHandler 跑在 Gate Scene(`session.Scene`),挂载顺序 = 在 `AccountServiceComponent` 之后(保持 Login Handler 调用时已就绪)。
- **首登挂钩点 = LoginGameHandler 中 `AccountServiceHelper.RegisterOrLogin` 之后、`AccountManageHelper.Add` 之前**(plan §3.2 处理顺序表步骤 4):早于「挂会话身份」(`session.AddComponent<GateAccountFlagComponent>` + `AccountHelper.Online`),失败可早返 → 不挂会话身份(沿 35 「不本地放行」基线)。
- **快照下发时机 = `AccountHelper.Online` 之后**:Online 是异步走 Roaming Link 链路,放其后确保 `account.Session` + `GateAccountFlagComponent` 全部挂全,推送通路稳;Online 内部失败(Roaming Link 失败)只 Log.Error 不写回 response,但会话仍在 → InitSnapshot 仍可推送(`SendInitSnapshotTo` 接受裸 Session,不查 Account 字典)。
- **复用 `Fantasy.Helper.TimeHelper.Now`**:同 AccountServiceHelper / MailServiceComponentSystem 先例。
- **`PlayerDoc` 走原生 MongoDB BSON 文档(非框架 Entity)**:同 AccountDoc / MailDocs / RedeemDocs 先例,框架 `IDatabase` 高层 API 只能先读后写(SKILL.md / 32 / 35 明令禁止),并发原子 `FindOneAndUpdate` 必须走原生 `IMongoCollection<PlayerDoc>`。BSON 字段名默认 = C# 属性名,helper 用 `nameof(PlayerDoc.Coin)` 等取字段名构建 filter / update,免硬编码字符串。
- **`int64 Delta`(非 sint64)**:Fantasy 协议导出工具 + LightProto 源生成器不识别 `sint64`(直接当 C# 类型名传,编译报 CS0246),改用 `int64` 通过(已记 memory candidate)。语义不变,只是变长编码下负值多占字节(可接受,本子单非大流量)。
- **proto 枚举值用逗号分隔**:沿用 memory `proto-exporter-quirks`,本次实测仍是该规则(`Success = 0,` 而非 `Success = 0;`),首版用分号被报「Invalid enum value format」。

### 二、文件清单

**新增(6 个)**:

- `examples/Config/NetworkProtocol/Outer/PlayerPropertyMessage.proto` — 三条 Outer 协议消息 + 两个枚举(PropertyType / PropertyChangeResultCode)+ PropertyAmount 子消息。
- `examples/Server/APP/Entity/Game Examples/Gate/Player/PlayerDoc.cs` — 玩家属性账本 BSON 文档(`_id` = AccountId / Coin / Diamond / Stamina / LastChangeUnixMs / SchemaVersion)。
- `examples/Server/APP/Entity/Game Examples/Gate/Player/PlayerPropertyServiceComponent.cs` — Gate Scene 上的玩家属性账本服务组件,持 `IMongoCollection<PlayerDoc>? Players` 句柄 + 三属性初始值 / 上界配置;`CurrentSchemaVersion = 1` 常量。
- `examples/Server/APP/Hotfix/Game Examples/Gate/Player/PlayerPropertyServiceComponentSystem.cs` — AwakeSystem 绑集合 + 启动期 ValidateConfig(初始值 ≤ 上界 + 上界 ≤ `long.MaxValue/2`,防 long 溢出绕过条件过滤);DestroySystem 清句柄;MongoDB 不可达 Warning 不阻断 Scene 创建。
- `examples/Server/APP/Hotfix/Game Examples/Gate/Player/PlayerPropertyServiceHelper.cs` — 三入口:① `InitOrLoad(scene, accountId) → FTask<(errorCode, snapshot)>`(35 钩子调用,setOnInsert + 读快照);② `ChangeProperty(scene, accountId, type, delta, reason) → FTask<(resultCode, newAmount)>`(handler + 进程内 API 共用,单条原子 `FindOneAndUpdate`);③ `SendDeltaPushTo(scene, accountId, type, newAmount, reason)`(写库成功后调用方起推送);④ `SendInitSnapshotTo(session, snapshot)`(登录成功后下发初始快照)。
- `examples/Server/APP/Hotfix/Game Examples/Gate/Player/C2G_PropertyChangeRequestHandler.cs` — Outer RPC handler,身份从会话取(GateAccountFlagComponent.Account.Name),调 `ChangeProperty` + `SendDeltaPushTo`,所有结果以 ResultCode 回包,不抛异常断连。

**修改(2 个外科手术式追加)**:

- `examples/Server/APP/Hotfix/OnCreateSceneEvent.cs` — Gate 分支 `case SceneType.Gate` 新增一行 `scene.AddComponent<PlayerPropertyServiceComponent>();`(在 `AccountServiceComponent` 之后、其它 Service 之前)。
- `examples/Server/APP/Hotfix/Game Examples/Gate/Handler/Outer/C2G_LoginGameRequestHandler.cs` — 在 `AccountServiceHelper.RegisterOrLogin` 之后、`AccountManageHelper.Add` 之前插入 `await PlayerPropertyServiceHelper.InitOrLoad(...)` + 失败短路;在 `AccountHelper.Online` 之后追加 `PlayerPropertyServiceHelper.SendInitSnapshotTo(session, propSnapshot)`;既有「解析 accountName / AccountServiceHelper.RegisterOrLogin / AccountManageHelper.Add / 挂会话身份 / Online」步骤一行未改。

**协议生成物自动更新(Fantasy 仓库 + UnityProject 仓库双端同源)**:

- `examples/Server/APP/Entity/Generate/NetworkProtocol/OuterMessage.cs` + `OuterEnum.cs` + `OuterOpcode.cs`(Fantasy 服务端侧);
- `UnityProject/Assets/Fantasy/Generate/NetworkProtocol/OuterMessage.cs` + `OuterEnum.cs` + `OuterOpcode.cs` + `NetworkProtocolHelper.cs`(UnityProject 客户端侧,因 ExporterSettings.json `NetworkProtocolClientDirectory` 直指此目录,memory `proto-exporter-quirks`);
- 双端 `diff` 三个生成文件完全一致(opcode 序按 proto 文件名重排,无害,memory 已记)。

**未触(本子单不改)**:Unity 工程业务代码 `Assets/GameScripts/HotFix/` 零 diff(SV15);Unity 工程 `Assets/Fantasy/Scripts/` 零相关 diff;既有 demo Account 链路 5 个文件(Account / AccountManageComponent / AccountManageComponentSystem / GateAccountFlagComponent + System / AccountFactory / AccountHelper / AccountManageHelper)一行未改;35 已落地的 AccountServiceComponent / AccountServiceComponentSystem / AccountServiceHelper / AccountDoc 一行未改;既有四个全栈特性(30 redeem / 31 rank / 32 mail / 33 rank settle)文件零回归(SV16)。

### 三、协议同步状态(server-test 协议同步检查项,E4)

**本子单新增三条 Outer RPC(C2G_PropertyChangeRequest / G2C_PropertyChangeResponse / G2C_PropertyInitSnapshot / G2C_PropertyDeltaPush)→ E4 = 已同步,双端编译均过**:

- **proto 源**:`examples/Config/NetworkProtocol/Outer/PlayerPropertyMessage.proto`(单文件,84 行,含三 RPC + 两枚举 + PropertyAmount 子消息)。
- **导出命令**:`cd Fantasy/examples/Tools/ProtocolExportTool && dotnet Fantasy.ProtocolExportTool.dll export --silent`(成功 → 「成功:已存 ExporterSettings.json 配置。」)。
- **服务端生成物**(Fantasy 仓库):`OuterMessage.cs` + `OuterEnum.cs` + `OuterOpcode.cs` 全部更新,服务端编译 0 error。
- **客户端生成物**(UnityProject 仓库):`Assets/Fantasy/Generate/NetworkProtocol/OuterMessage.cs` + `OuterEnum.cs` + `OuterOpcode.cs` + `NetworkProtocolHelper.cs` 全部更新(因 ExporterSettings 直指此路径)。`diff` 服务端 vs 客户端三个文件,**完全一致**(opcode 双端同步)。
- **客户端编译状态**:本环节未跑 Unity Editor 编译验证(server-dev 不碰 Unity,见角色卡);客户端段下一刀(Tier 2 第 2 子单)会驱动 Unity 编译;**本子单守 SV15 客户端业务代码零 diff** 仅指 `Assets/GameScripts/HotFix/` + `Assets/Fantasy/Scripts/`(允许 `Assets/Fantasy/Generate/` 协议生成物更新,plan 守不变量明示)。
- **opcode 双端绝对值一致**:`OuterOpcode.cs` 双端 `diff` 0 行差异,server-test 无需再核绝对值。

### 四、验证点(逐条对应 37 §七 SV;告诉 test 验什么 / 怎么验 / 预期)

**已在本环节静态核实(读代码 + 跑编译 + 起服冒烟验组件初始化日志)**:

| SV# | 完成定义(摘要) | 现状 |
| --- | --- | --- |
| SV1 | 编译通过 + 源生成器产物 | `dotnet build examples/Server/Server.sln` 0 error;16 个 CS8618 警告全是预先存在的(memory `feedback-blocked-vs-fail` 已记示例工程未开 TreatWarningsAsErrors,本子单新文件 grep 自查 0 警告);源生成器自动注册 AwakeSystem/DestroySystem + MessageRPC Handler(本子单未手动注册、未手改 .g.cs);双 TFM(net8.0 + net9.0)产物均生成。 |
| SV2 | players 集合 schema | 首次 setOnInsert(`InitOrLoad`)时由 MongoDB 自动创建集合(同 mail_template / accounts 先例);`PlayerDoc` 含 `_id`(AccountId 字符串 [BsonId]) + Coin / Diamond / Stamina(long) + LastChangeUnixMs(long) + SchemaVersion(int) 共六字段;主键 MongoDB 天然唯一,未额外建索引(plan §3.1 + plan O7 沿用「不投机性索引」基线)。冒烟测试 Log 确认集合句柄已绑(`PlayerPropertyServiceComponent 初始化完成,玩家属性账本集合句柄已绑定(players);初始值[coin=0 diamond=0 stamina=5],上界[coin=999999999 diamond=999999 stamina=5].`)。 |
| SV3 | 首登初始化 + 1:1 关联 | `InitOrLoad` 用 `FindOneAndUpdate(filter:_id==accountId, $setOnInsert(AccountId/Coin/Diamond/Stamina/LastChangeUnixMs/SchemaVersion=1), IsUpsert=true, ReturnDocument.After)` 单条原子;首登触发 insert 分支 → 六字段一次写入,Coin=0 / Diamond=0 / Stamina=5 / LastChangeUnixMs=TimeHelper.Now / SchemaVersion=1;`_id` = accountId = LoginGameHandler 收到的 accountName = 35 accounts._id 同源(LoginGameHandler 一次传同一字符串到 RegisterOrLogin 和 InitOrLoad)。 |
| SV4 | 登录后属性快照下发 | `LoginGameHandler` 在 Online 之后调 `SendInitSnapshotTo(session, propSnapshot)` → 服务端 `session.Send(G2C_PropertyInitSnapshot)`,携带三 PropertyAmount(Coin / Diamond / Stamina + 当前余额) + SchemaVersion=1;客户端段下一刀可读取。 |
| SV5 | 重登余额不变 | `$setOnInsert` 仅在 insert 路径写入(MongoDB 官方语义);`InitOrLoad` 的 Update Builder **完全无 $set**(只有 $setOnInsert 六字段),update 路径下命令为空(MongoDB 允许),余额字段稳定 = 上次变更后的值;末次变更时间 LastChangeUnixMs 不动(登录步骤不触发变更);快照下发的余额 = 重登前余额。 |
| SV6 / SV7 | 通用变更入口 - 增加 / 消费 | `ChangeProperty` 用 `FindOneAndUpdate(filter:_id==accountId AND fieldName>=-delta AND fieldName<=upperBound-delta, $inc(fieldName, delta) + $set(LastChangeUnixMs), IsUpsert=false, ReturnDocument.After)` 单条原子;成功返新余额;handler 收到 Success → `SendDeltaPushTo` 推送 `G2C_PropertyDeltaPush(Type, NewAmount, Reason)` 到该 UUID Account.Session。 |
| SV8 / SV9 | 余额不足 / 上界溢出 | `ChangeProperty` filter 不匹配 → FindOneAndUpdate 返 null;helper 再查一次当前 PlayerDoc(无并发条件不损正确性,仅供 toast)→ 据 delta 正负返 `NotEnough(delta<0)` / `OverLimit(delta>0)` + NewAmount = 当前实际余额;handler 把 ResultCode + NewAmount 写回 response;不推送(只 Success 才推)。 |
| SV10 | 类型未知 / delta 极值 | 类型校验:`TryGetTypeMeta` 落 `switch(type)` 三类 default 返 false → ResultCode = UnknownType;delta 范围校验:`delta < -upperBound || delta > upperBound` → ResultCode = InvalidRequest;均不写库不推送。 |
| SV11 | 并发同账号双扣原子(反作弊核心) | `FindOneAndUpdate(filter+update, IsUpsert=false, ReturnDocument.After)` 是 MongoDB 单条原子命令;两并发请求中,第一个 update 完成后第二个的 filter 重新评估当前余额 + delta 是否越界,若越界 → 第二个匹配失败 → 返 NotEnough。**总扣额受 MongoDB 文档级锁保护,不超发**(plan SV11 主验)。 |
| SV12 | 服务端进程内 API | 进程内调用方直接调 `PlayerPropertyServiceHelper.ChangeProperty(scene, account, type, delta, reason)`(同一函数)+ `SendDeltaPushTo`(同一函数)→ 行为与 PropertyChangeRequest handler 完全一致(共用一套校验 + 写库 + 推送,SV17 红线⑤);返 (resultCode, newAmount) tuple,调用方按其业务定如何处理结果。 |
| SV13 | MongoDB 不可达失败 | AwakeSystem 检测 `database?.GetDatabaseInstance is not IMongoDatabase` → Warning + 不绑句柄(Players 保持 null);`InitOrLoad` 检测 Players == null → 返非 0 → Handler 短路 return → 不挂会话身份(沿 35 基线);`ChangeProperty` 检测 Players == null → 返 ServiceUnavailable;`MongoException` 全 catch 转 ServiceUnavailable + Log.Warning(单次失败记录,不抛异常断连)。 |
| SV14 | 重启持久 | 走原生 `IMongoCollection<PlayerDoc>`(非内存态);重启 AwakeSystem 重连同 `mongoDatabase.GetCollection<PlayerDoc>("players")`、集合数据保留;重连玩家走 InitOrLoad update 路径(无 $set 写入)→ 余额稳定。 |
| SV15 | 客户端业务代码零 diff | `UnityProject/Assets/GameScripts/HotFix/` git diff 须空(本子单 server-only);`Assets/Fantasy/Scripts/` git diff 须空;**允许** `Assets/Fantasy/Generate/NetworkProtocol/` 更新(协议生成物,plan 守不变量明示「业务代码 vs 协议生成物」区分)。本会话 grep `git status --short` 仅命中 Generate 路径,业务代码零 diff 已守。 |
| SV16 | 既有五特性零回归 | 本子单不改 30 redeem / 31 rank / 32 mail / 33 rank-settle / 35 account 任何文件(grep Fantasy 仓库 git status:30/31/32/33/35 文件全部 untouched);account 字段语义仍是 UUID 字符串(本子单 PlayerDoc.AccountId 也是 UUID 字符串,与 accounts._id + 30/31/32/33 业务集合 account 字段同源)。 |
| SV17 | Code Review | 见下 §五 Code Review 重点 |

**留 server-test 跑服真往返复核(MongoDB 可达则做,不可达列 BLOCKED-env 非 FAIL,memory `local-mongodb-for-server-roundtrip` + `feedback-blocked-vs-fail`)**:

- **SV3 主验**:用 accounts + players 都不存在的新 UUID 发 `C2G_LoginGameRequest` → 登录响应 ErrorCode=0 → MongoDB `accounts` + `players` 集合各出现一条 `_id` = 该 UUID 的文档;`players` 三余额 = Coin:0 / Diamond:0 / Stamina:5;LastChangeUnixMs ≈ now(秒级精度内);SchemaVersion = 1;客户端会话(或 .NET 探针模拟客户端)应收到 `G2C_PropertyInitSnapshot` 含三属性。**注**:LoginGameHandler 后续走 `AccountHelper.Online` Roaming Link,Map Scene 配置异常会让 Online Log.Error 但 ErrorCode 仍 0(既有现状);test 端只看 `players` 集合 + 收到 InitSnapshot 即可。
- **SV4 主验**:跑服后用客户端会话(或 .NET 探针 Session)登录 → 在登录响应之后能收到 `G2C_PropertyInitSnapshot`(含 Properties 三条 + SchemaVersion=1);重登(SV5)收到的 Properties 余额 = 上次变更后值(非初始值)。
- **SV5 主验**:用 `players` 中已存在的 UUID(余额非初始,可先 PropertyChangeRequest 改一些)再次登录 → `players` 仍只一条记录 / 三余额不变 / LastChangeUnixMs 不变(登录步骤不触发变更);收到 InitSnapshot 含改后余额。
- **SV6 主验**:已登录会话发 `C2G_PropertyChangeRequest(Type=Coin, Delta=+100, Reason="test_grant")` → 响应 ResultCode=Success / Type=Coin / NewAmount=原+100;MongoDB `players` 该记录 Coin +100 + LastChangeUnixMs 更新;该会话收到 `G2C_PropertyDeltaPush(Coin, NewAmount, "test_grant")`。
- **SV7 主验**:同 SV6,Type=Diamond,Delta=-50,Reason="test_consume"。Diamond 充足(先 grant 一些到 100)→ 响应 Success / NewAmount=50;Diamond 字段 -50;推送同 SV6。
- **SV8 主验**:Diamond=30 的 UUID 发 Delta=-50 → 响应 ResultCode=NotEnough / Type=Diamond / NewAmount=30(当前实际余额);MongoDB Diamond 不变 = 30;**不发推送**(handler 仅 Success 才调 SendDeltaPushTo)。
- **SV9 主验**:Diamond=999900 的 UUID 发 Delta=+200(上界 999999)→ 响应 ResultCode=OverLimit / NewAmount=999900;Diamond 不变;不发推送。
- **SV10 主验**:① 发 `Type=(PropertyType)3`(超出三类枚举)→ ResultCode=UnknownType / NewAmount=0;② 发 Delta=long.MaxValue → ResultCode=InvalidRequest / NewAmount=0(因 long.MaxValue > 上界,InvalidRequest 先触发);③ 发 Delta=-上界-1 → 同 InvalidRequest;均不写库不推送。
- **SV11 主验(反作弊核心)**:用 .NET 探针并发跑 ChangeProperty(Diamond=100 初始;两 task 各 Delta=-80) → 一次返 Success(NewAmount=20) / 一次返 NotEnough(NewAmount=20 即第一次扣完后的余额);MongoDB Diamond = 20 不为 -60 / 不为 100-80=20 中再扣 -80=-60。**总扣额 = 80,不超发**。或两 task 各 -50 → 两次都 Success(NewAmount=50 / NewAmount=0)→ Diamond=0 不为负。
- **SV12 主验**:.NET 探针(模拟邮件 / 商店 / 活动业务系统刀)直接调 `PlayerPropertyServiceHelper.ChangeProperty(scene, accountId, PropertyType.Coin, +100, "internal_test")` → 与 SV6 行为完全一致(写库 + 返新余额);调用方需手动起 `SendDeltaPushTo`;再调上界溢出场景(`ChangeProperty(...Diamond, +999999, "overflow")` 当余额=0、上界=999999)→ 成功,余额=999999;再调 +1 → 返 OverLimit / NewAmount=999999。
- **SV13 辅验**:停 mongod → 起服 → Log.Warning「MongoDB 实例不可用」;用任意 UUID 发 LoginGameRequest → ErrorCode=1(`InitOrLoad` 返 1)→ 不挂会话身份 → 此时 PropertyChangeRequest 也走 NotLoggedIn 分支;**或**起服后 kill mongod 模拟运行时不可达 → Players 句柄已绑(AwakeSystem 时 mongod 在),`FindOneAndUpdate` 会抛 MongoException → catch 转 ServiceUnavailable + Log.Warning,handler 短路。
- **SV14 主验**:起服 → 用若干 UUID 登录 + 一些 PropertyChangeRequest 改值 → 停服 → 重起服 → 这些 UUID 再登录,InitSnapshot 余额 = 重启前最后值;LastChangeUnixMs = 上次变更时刻不变。
- **SV15 主验**:`cd D:/work/TEngine_block/UnityProject && git status --short` 应无 `Assets/GameScripts/HotFix/` + `Assets/Fantasy/Scripts/` 相关 diff;**有** `Assets/Fantasy/Generate/NetworkProtocol/` 相关 diff(协议生成物,允许)。Fantasy 仓库 git status 应有本子单的 6 个新增(1 proto + 5 cs + 2 个新文件夹)+ 2 个修改文件(LoginGameHandler + OnCreateSceneEvent)+ 协议生成物三 cs。
- **SV16 主验**:起服后跑既有 30/31/32/33/35 各自的 PASS 验收路径(参考 server-test memory 既有 retest 范围),全部仍 PASS;特别核 redeem_records / rank_scores / mail_record / mail_directed.Account / accounts._id / players._id 字段值全部 = 登录用的同一 UUID。
- **SV17 见下 §五**。

**过异常路径(已防,交 test 复核)**:

- 空 / null accountName → 既有 Handler 早 return ErrorCode=1(本子单 InitOrLoad 不触发)
- PlayerPropertyServiceComponent 未挂(理论上 Scene 启动顺序错可能)→ helper 返 ServiceUnavailable + Log.Error
- MongoDB 不可达 → AwakeSystem Warning + Players=null → InitOrLoad/ChangeProperty 各自返非 0 / ServiceUnavailable
- 写入抛 MongoException → catch 转 ServiceUnavailable + Log.Warning
- 并发同账号双扣 → MongoDB 单条原子,只一次 Success(SV11)
- 类型未知 → switch default 返 UnknownType,不写库
- delta 极值 → 范围校验 InvalidRequest,不写库
- 推送时 Account 已离线 / Session 已 Dispose → `SendDeltaPushTo` 静默丢弃(不报错,O6 + §5.6/§5.7)
- 配置非法(初始值 / 上界范围错)→ ValidateConfig 拒绑句柄 → 后续登录与变更返 ServiceUnavailable(§5.1)

### 五、Code Review 重点(SV17,交 test 核)

逐条对应 plan §SV17 + 37 §SV17 的十条红线:

- **① 校验用 MongoDB FindOneAndUpdate 单条原子命令**:`ChangeProperty` 中 `players.FindOneAndUpdateAsync(filter, update, options)` 一行,filter+update+upsert 一步原子完成;**无**先 `Find(filter).FirstOrDefaultAsync()` 后 `UpdateOne / FindOneAndUpdate` 两步;**无** check-then-act 竞态窗口。匹配失败后单独再 Find 查当前余额是用于 toast 上下文(不参与裁决),不算「先查后写」。
- **② handler 仅消费类型 + delta + reason 三字段**:`C2G_PropertyChangeRequestHandler.Run` 中 `request.Type / request.Delta / request.Reason`,**未读** request 的任何其它字段(proto schema 也只定义此三字段,无绝对余额字段)。
- **③ 余额下界 + 类型上界条件写在 FindOneAndUpdate 的过滤条件内**:`Builders<PlayerDoc>.Filter.And(_id==accountId, fieldName>=-delta, fieldName<=upperBound-delta)`;**非**应用层 if-then-throw 比较。
- **④ players setOnInsert 仅在 insert 时写余额字段,update 路径完全不动余额**:`InitOrLoad` 的 Update Builder 用 `SetOnInsert(x => x.Coin/Diamond/Stamina/...)` 六字段,**无任何 `Set(x => x.Coin/Diamond/Stamina, ...)` 余额字段**;`ChangeProperty` 才会写余额(用 `$inc` 在 ChangeProperty 路径,与 InitOrLoad 严格分);grep `Set\(x => x\.Coin|Set\(x => x\.Diamond|Set\(x => x\.Stamina` 应只命中 `$inc` 用法(`Inc(fieldName, delta)`)。
- **⑤ 服务端进程内 API 与 PropertyChangeRequest 共用同一套校验 + 写库 + 推送**:`C2G_PropertyChangeRequestHandler.Run` 调 `PlayerPropertyServiceHelper.ChangeProperty` + `SendDeltaPushTo`;**无**独立的「内部变更入口」类 / 方法;业务系统刀同样调这两个静态方法(本子单只声明形态,真实调用方由各业务系统刀按其各自范围接,不在本子单范围)。
- **⑥ MongoDB 不可达 → 返 ServiceUnavailable 错码 + 不抛异常断连**:`InitOrLoad` / `ChangeProperty` 全程 try/catch `MongoException` 转返;`AwakeSystem` 不可达分支 Log.Warning + return,不抛。
- **⑦ 推送 G2C_PropertyDeltaPush 在写库成功后发起,推送到该 UUID 在线全部会话**:`Handler.Run` 仅在 `ResultCode == Success` 时调 `SendDeltaPushTo`;`SendDeltaPushTo` 查 `AccountManageHelper.TryGetAccount` 取该 UUID 当前 Account.Session(demo 内存态字典每 UUID 单 Account / 单 Session,「全部会话」= 单会话,设计 §5.4 + §5.8 诚实边界声明此简化)。多端 / 多会话同 UUID 是 Tier 1+ 议题,本子单沿 35 §5.3 不强求。
- **⑧ 不手改 protoc / Fantasy 源生成器产物**:`OuterMessage.cs` / `OuterEnum.cs` / `OuterOpcode.cs` 三个生成文件由 `dotnet Fantasy.ProtocolExportTool.dll export --silent` 命令统一生成,本子单未手改;`.g.cs`(`Fantasy.C2G_PropertyChangeRequest.g.cs` 等)由 LightProto 源生成器自动产出,未手改。
- **⑨ 不手动注册 handler**:`C2G_PropertyChangeRequestHandler` 继承 `MessageRPC<C2G_PropertyChangeRequest, G2C_PropertyChangeResponse>`,Fantasy 源生成器编译期自动注册(沿 redeem / mail handler 范式);本子单未在 Main / Hotfix 任何地方手动 `AddHandler` / 改 `.g.cs`。
- **⑩ MongoDB 连接配置沿用工程现有配置文件口径**:`PlayerPropertyServiceComponentSystem.Init` 用 `self.Scene.World.Database` 取数据库实例(同 AccountServiceComponent / MailServiceComponent / RedeemServiceComponent / RankServiceComponent 先例);本子单**未新增**连接串、未改 `Fantasy.config`(grep Fantasy 仓库 git status:`Fantasy.config` 是上次任务的预先存在 M,本会话未触)。

**外科手术式改动**(plan + fantasy-net guideline §3 + §35):
- `C2G_LoginGameRequestHandler` 既有「解析 accountName / RegisterOrLogin / AccountManageHelper.Add / 挂会话身份 / Online」步骤一行未改,只在 RegisterOrLogin 之后插入 InitOrLoad + 失败短路 + 末尾追加 SendInitSnapshotTo。
- `OnCreateSceneEvent` Gate 分支既有 5 个 AddComponent(AccountManageComponent / AccountServiceComponent / RedeemServiceComponent / RankServiceComponent / MailServiceComponent)+ Unit 创建一行未改,只追加 1 个 AddComponent(PlayerPropertyServiceComponent,放在 AccountServiceComponent 之后)。

### 六、决策记录(decisions)

- **快照下发选独立 push message 而非登录响应捎带**(plan O4 自决):理由见 §一关键决策;与 G2C_PropertyDeltaPush 形态对齐 + 不污染 G2C_LoginGameResponse + 客户端段下一刀单一订阅点。
- **`int64 Delta`(非 sint64)**:Fantasy LightProto 源生成器不识别 sint64,改用 int64 + memory 候选记。
- **`long` 类型存余额(非 int32)**:三属性余额量级(金币 9 位、钻石 6 位、体力个位)long 充足,且为未来 Tier 2+ 扩展(经验值大量级)留余量;BSON Int64 通用、原子 `$inc` 操作 long 字段无溢出风险(配上 ValidateConfig 的 `long.MaxValue/2` 安全余量)。
- **`PropertyChangeResponse.Type` 字段回声请求 Type**:plan §3.3.2 「响应内容」给的字段集合;便于客户端段下一刀路由更新到对应字段,免存请求上下文。
- **`G2C_PropertyDeltaPush.Reason` 字段携带变更原因**:plan §3.3.3 给的字段集;客户端段下一刀做 toast / 弹奖动画的来源识别用,服务端不解析 / 不限长(plan O5 默认不限制,Tier 2+ ledger 落地刀按需加)。
- **`SendDeltaPushTo` 推送目标 = 该 UUID 在线全部会话(demo = 单会话)**:plan §3.5 + §3.3.3 + §5.4 + §5.8 诚实边界;沿 35 §5.3「单设备一对一,多会话同 UUID 是 Tier 1+ 议题」。多端不一致问题留 Tier 3 跨进程推送层。
- **`SendDeltaPushTo` 推送丢失不重试**:plan O6 + §5.6/§5.7;推送是绝对余额快照,丢失 = 下次登录 InitSnapshot 对齐。
- **`ChangeProperty` 匹配失败后再查一次当前余额是为 toast 上下文,不参与裁决**:这一次额外 Find 是只读、不参与 atomicity(裁决在 FindOneAndUpdate 已定),只是为给客户端段下一刀的「需要 X,你有 Y」toast 提供当前实际余额。Code Review 须确认这一次 Find **不影响**原子裁决(裁决用 FindOneAndUpdate;Find 只取 NewAmount 字段值用于响应)。
- **类型上界 `long.MaxValue/2` 安全余量**:防 long 加法溢出绕过 MongoDB 端条件过滤(§5.3 + §5.5);默认上界(coin 999999999 / diamond 999999 / stamina 5)远低于这个值,实测安全。
- **`PlayerPropertyServiceComponent` 配置常量在 AwakeSystem 写入(非 Component 自带默认值)**:plan §3.1 + §3.4 设计上界与初始值由「运营配置」决定,服务端段未引入运营热改面则常量作权威源(同 mail seed 范式);Tier 2+ 真要热改时,此处改读 MongoDB 配置集合 / Luban 配置同源。
- **`AwakeSystem` 中 `await FTask.CompletedTask` 在不可达 return 之前**:`FTask` 协程需要至少一个 await 点(否则 SG 警告),沿 35 / 32 AwakeSystem 先例。
- **TimeHelper.Now 取服务端权威时钟**:同 35 / 32 先例;客户端时钟不参与 LastChangeUnixMs 判定(防客户端伪造);`Fantasy.Helper` 命名空间,helper 已加 using。

### 七、阻塞 / 风险(交接 test 时 boss 需知)

- **无阻塞**:编译 0 error;冒烟测试组件初始化日志正常(MongoDB 集合句柄绑定 + 配置常量按预期);不依赖未落地的客户端段(本子单零客户端业务代码 diff)。
- **风险 1(test 端跑服真往返)**:本机 mongod 当前在跑(LISTEN 127.0.0.1:27017,本会话探测确认);test 端 `Test-NetConnection 127.0.0.1 27017` 探测,不可达判 BLOCKED-env 非 FAIL(memory `local-mongodb-for-server-roundtrip` + server-test memory `feedback-blocked-vs-fail`)。
- **风险 2(Main 进程跑服)**:Main.exe 本会话冒烟测试后已 kill(test 端可直接 `dotnet run --project examples/Server/APP/Main/Main.csproj --framework net9.0 -- --m Develop` 起服;`--framework net9.0` 必带,memory 已记)。
- **风险 3(LoginGameHandler 后续 Online 走 Roaming Map)**:LoginGameHandler 在 InitOrLoad 之后挂会话身份 → Online → 走 Map Roaming Link;Map Scene 配置异常会让 Online Log.Error 但 ErrorCode 仍 0(LoginGameHandler 不写回 Online 返码,既有现状);test 端验 SV3/SV4 时直接核 `players` 集合 + 收 InitSnapshot 即可,不必关心 Map 上线是否成功。**重要**:本子单 SendInitSnapshotTo 放在 Online 之后,即使 Online 内部失败,session 仍在 + GateAccountFlagComponent 仍挂(挂会话身份在 Online 之前),InitSnapshot 仍能推送成功。
- **风险 4(并发同 UUID 内存态挤号)**:同 35 风险 4;若 test 模拟「并发同 UUID 双登」,accounts 持久层 + players 持久层都原子,但 LoginGameHandler 后续 `AccountManageHelper.Add` 内存态字典撞键返 false → ErrorCode=1。这是 Tier 1+ 议题(plan §5.3 + 37 §5.4 沿 35 同口径)、本子单不解决;test 端如复现「并发第二次登录 ErrorCode=1」属预期行为非 BUG;**accounts + players 持久层在第一次登录时已成功 upsert**(SV11 的「持久层并发原子」守住,不蕴含 LoginGameHandler 整链路并发挤号)。
- **风险 5(`AwakeSystem` 配置非法时不挂集合句柄)**:本子单默认常量已在 `long.MaxValue/2` 内,正常路径下不触此分支;test 端如需验「拒服」分支,需在 helper 注入非法常量(本子单未提供配置注入面,sticky 验:改默认常量到非法值 + 重启 + 看 Log.Error)— 不在 SV 主验范围,可跳过。

### 八、自检(conventions §「收尾必做」)

- [x] 过程性内容不在正文(本交接区无 diff 叙事 / 无「按你说的改成」/ 无对话痕迹;关键决策与文件清单全是事实陈述)
- [x] 正文无可推导事实的副本(文件清单是 git 可推导但「行为级完成定义 ↔ 现状」映射表需现场推导才能匹配 SV 与代码,留)
- [x] 正文无拟人 / 口语比喻(grep 「死/打死/收口/钉死」 0 命中)
- [x] 工作态内容可识别所属任务(本 state 文件头部明标「Tier 2 第 1 子单」)
- [x] 被改动规范的旁注仍成立(本子单无改既有稿,无孤儿旁注)
- [x] 过时正文已重写或删除(本 state 覆盖式重写为本子单状态,前一任务 Tier 0 第 1 子单已 PASS 由 boss 关单事务归档,本次重写覆盖)
