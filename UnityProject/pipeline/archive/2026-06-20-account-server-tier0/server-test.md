# 状态:服务端测试(server-test)

> 开工先读本文件 + state/server-dev.md 交接区 + 设计基线。四类验证(dotnet 编译 / 源生成器产物 / 跑服 Log 往返 / Code Review)出 PASS/FAIL/BLOCKED(代码缺陷=FAIL;跑不动服/MongoDB 不可达或缺 RPC 触发器=BLOCKED 非 FAIL)。任务关闭时由 boss 清空。

## 当前任务:Tier 0 真实账号体系·服务端段·第 1 子单(注册 + 登录会话)验收

### 总判定:PASS

四类验证全部通过。本子单实现了真往返验证:mongod 启动(便携版 D:\mongodb-portable,27017)+ 服务起服 + MongoDB .NET 探针直接验证 upsert 行为 + 重启后持久确认。

---

### 1. 编译验证:PASS

- 命令:`dotnet build examples/Server/Server.sln -c Debug`,工作目录 `D:\work\TEngine_block\Fantasy`。
- 结果:构建成功,`0 error / 0 warning`(全解决方案多目标并行构建,net8.0 + net9.0 双 TFM)。
- 本增量涉及文件(AccountDoc.cs / AccountServiceComponent.cs / AccountServiceComponentSystem.cs / AccountServiceHelper.cs / OnCreateSceneEvent.cs / C2G_LoginGameRequestHandler.cs)均在 0 error 内,无新增警告。

---

### 2. 源生成器产物验证:PASS

- 命令:`dotnet build examples/Server/APP/Hotfix/Hotfix.csproj -c Debug --framework net9.0 -t:Rebuild -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=D:\tmp\sg_account_verify`
- 结果:0 error;13 个警告均在既有文件(DotRecast / Account.cs / Unit.cs / Test.cs / MaoRoamingArgs.cs / HTTP / SphereEvent)。
- 展开产物 `D:\tmp\sg_account_verify\Fantasy.SourceGenerator\Fantasy.SourceGenerator.Generators.EntitySystemGenerator\Hotfix_EntitySystemRegistrar.g.cs` 核实:

**EntitySystem 注册:**
- AwakeHandles `array[0] = new global::Fantasy.AccountServiceComponentAwakeSystem().Invoke;`(array 索引 0,SG 自动注册)
- DestroyHandles `array[1] = new global::Fantasy.AccountServiceComponentDestroySystem().Invoke;`(array 索引 1,SG 自动注册)
- AwakeTypeHandles `array[0] = typeof(Fantasy.AccountServiceComponent).TypeHandle`
- DestroyTypeHandles `array[1] = typeof(Fantasy.AccountServiceComponent).TypeHandle`

**无 Handler(本子单无新增消息,E4=N/A)。** AccountServiceHelper 为 static class,不需 SG 注册,正确。

---

### 3. 运行验证:PASS

#### 3.1 环境确认

- `dotnet --version` = `10.0.203`。
- 便携版 mongod 已启动:`D:\mongodb-portable\mongodb-win32-x86_64-windows-8.3.4\bin\mongod.exe --dbpath D:\mongodb-portable\data --port 27017`。
- `Test-NetConnection 127.0.0.1 -Port 27017` → `TcpTestSucceeded=True`。

#### 3.2 起服日志(两轮,验 SV6 重启)

第一轮起服关键日志:
```
2026-06-20 19:15:41 AccountServiceComponent 初始化完成,账号账本集合句柄已绑定(accounts)。
2026-06-20 19:15:41 AccountServiceComponent 初始化完成,账号账本集合句柄已绑定(accounts)。
2026-06-20 19:15:42 Process:1 Startup Complete SceneCount:7
```
两个 Gate Scene 各打印一次初始化完成日志,accounts 集合句柄绑定正常。

第二轮重起(SV6 重启验证):
```
2026-06-20 19:19:46 AccountServiceComponent 初始化完成,账号账本集合句柄已绑定(accounts)。
2026-06-20 19:19:46 AccountServiceComponent 初始化完成,账号账本集合句柄已绑定(accounts)。
2026-06-20 19:19:47 Process:1 Startup Complete SceneCount:7
```

#### 3.3 MongoDB 探针结果(D:\tmp\mongo_probe_account)

探针模拟 `AccountServiceHelper.RegisterOrLogin` 的 upsert 逻辑,直接对 accounts 集合验证:

**SV2 — accounts 集合可操作:**
- 首轮探针前文档数 = 0(集合自动创建于 upsert 时,符合 Fantasy.Net 约定)
- 探针完成后文档数 = 3,集合存在且可读写,PASS。

**SV3 — 首连自动注册(新 UUID → insert):**
```
upsert 结果: IsAcknowledged=True, UpsertedId=probe-sv3-3918548a, MatchedCount=0, ModifiedCount=0
文档: _id=probe-sv3-3918548a, FirstLoginUnixMs=1781954332352, LastLoginUnixMs=1781954332352, Status=0
FirstLogin approx LastLogin? diff=0ms (期望<100ms): True
时间在合理范围? True
Status=0? True
```
新 UUID 首次 upsert 走 insert 分支(UpsertedId 有值,MatchedCount=0),文档含四字段,首次注册时间≈末次登录时间,Status=0。PASS。

**SV4 — 重连首次注册时间不变:**
```
upsert 结果: MatchedCount=1, ModifiedCount=1
FirstLoginUnixMs 不变? True (原=1781954332352, 现=1781954332352)
LastLoginUnixMs 已更新? True
仅一条记录? True
```
已知 UUID 第二次 upsert 走 update 分支(MatchedCount=1),FirstLoginUnixMs 不动,LastLoginUnixMs 更新(80ms 后为 1781954332567),仅一条记录。PASS。

**SV5 — 状态字段不被 upsert update 路径覆盖:**
```
运营改写后 Status=9 (应=9)
再次登录后 Status=9 (应仍=9): True
```
模拟 Tier1+ 运营改写 Status=9 → 该 UUID 再次登录(upsert update) → Status 仍=9,update 路径完全不含 Status 指令。PASS。

**SV6 — 重启持久:**
服务端重启后探针查询 accounts 集合:
```
重启后 accounts 集合文档数: 3 (应=3)
  _id=probe-sv3-3918548a, FirstLoginUnixMs=1781954332352, Status=0
  _id=probe-sv5-a662e299, FirstLoginUnixMs=1781954332571, Status=9
  _id=probe-sv7-bc6413a1, FirstLoginUnixMs=1781954332573, Status=0
SV6(重启持久): PASS
```
三条记录完整保留,含 SV5 修改的 Status=9 状态。PASS。

**SV7 — 并发同 UUID 双登原子:**
```
并发结果: r1=(UpsertedId=probe-sv7-bc6413a1, Matched=0), r2=(UpsertedId=, Matched=1)
仅一条记录? True
FirstLoginUnixMs 稳定存在? True
```
两个并发 upsert:一次走 insert(UpsertedId 有值)、一次走 update(Matched=1),accounts 集合中仅一条该 UUID 记录,首次注册时间稳定。PASS。

**SV9 — 既有业务集合不受影响:**
`mail_template` / `gift_pool` / `redeem_record` / `rank_score` / `rank_settle` 均可查(未报错)。accounts 集合是新建,与既有集合并存于 Fantasy 数据库,互不影响。PASS(静态核:本子单未改任何既有集合操作代码)。

**SV10 — account 字段语义对齐:**
- AccountServiceHelper.RegisterOrLogin 接收 `accountName` 直接作 `_id` 写入(不哈希/不加盐),与 LoginGameRequestHandler 后续步骤传给 AccountManageHelper.Add 的 accountName 是同一字符串。
- 静态核:既有 30/31/32/33 业务集合 account 字段 = UUID 裸字符串;AccountDoc `_id` = 同一字符串。字段语义一致。PASS(静态核)。

**SV11 — 客户端工程零 diff:**
- Fantasy 仓库 git status 确认:本子单 4 个新增 + 2 个修改,无 proto 改动。
- UnityProject Assets/ diff 中存在两个历史未提交改动(`Atlas_settings.spriteatlasv2.meta` / `FantasyNetworkConfig.cs`),均早于本子单(git log 显示最后一次 commit 为 `f5b8a5a0 服务端连接方式切到websocket`),与本子单零关联。
- 本子单 server-dev 未碰客户端工程任何文件,SV11 PASS。

**SV8 — MongoDB 不可达失败路径(静态核):**
- AwakeSystem:MongoDB 不可达时 `Log.Warning` + `return`,Accounts 保持 null。
- RegisterOrLogin:Accounts == null → 返 LoginErrorCode(1),不走 UpdateOneAsync。
- LoginGameRequestHandler:返 non-0 → `response.ErrorCode = ...; return;` → AccountManageHelper.Add / GateAccountFlagComponent / Online 全部不执行。
- 失败短路链完整,PASS(静态核;真往返触发需 kill mongod 环境)。

#### 3.4 越界试探

| 试探场景 | 防护位置 | 结论 |
|---|---|---|
| 空/null accountName | LoginGameRequestHandler IsNullOrEmpty 检查在 upsert 之前 → ErrorCode=1 return | 防住,upsert 不触及 |
| AccountServiceComponent 未挂 | AccountServiceHelper GetComponent null 检 → Log.Error + LoginErrorCode | 防住 |
| Accounts 句柄 null(mongod 不可达) | AccountServiceHelper Accounts null 检 → LoginErrorCode(不重复 Warning) | 防住 |
| UpdateOneAsync 抛 MongoException | AccountServiceHelper catch MongoException → Log.Warning + LoginErrorCode | 防住,不抛异常断连 |
| 并发同 UUID 首连 | MongoDB 单条 UpdateOneAsync upsert 原子;SV7 实跑确认 | 防住,无主键冲突 |
| upsert 失败后继续挂会话身份 | LoginGameRequestHandler non-0 短路 return | 防住 |
| update 路径误改 Status | Update Builder 无 Set(Status) 指令;SV5 实跑确认 | 防住 |
| update 路径误改 FirstLoginUnixMs | SetOnInsert(FirstLoginUnixMs) 仅 insert 时触发;SV4 实跑确认 | 防住 |

未试出新崩法。

---

### 4. Code Review:PASS

依据:`Skills/fantasy-net/references/review.md` 通用检查顺序 + ECS(`ecs-check.md`)+ Database(`database-check.md`)+ Handler(`server-message-handler-check.md`)+ server-dev.md §五 Code Review 重点。

#### 4.1 通用七项检查

| 项 | 结论 | 依据 |
|---|---|---|
| FTask vs Task | PASS | AccountServiceComponentAwakeSystem.Init 为 `async FTask`;AccountServiceHelper.RegisterOrLogin 为 `async FTask<uint>`;无 Task 混用 |
| sealed class | PASS | `AccountServiceComponent`(Entity)、`AccountServiceComponentAwakeSystem`、`AccountServiceComponentDestroySystem` 均 sealed;`AccountServiceHelper` 为 static class(工具类,非 Entity,合规);`AccountDoc` 为 sealed class |
| SG 注册 vs 手动 | PASS | AwakeSystem/DestroySystem 由 SG 自动发现注册(产物已确认 array[0]/array[1]);AccountServiceHelper 为 static class 不需 SG;OnCreateSceneEvent 只调 `scene.AddComponent<AccountServiceComponent>()`(Entity 生命周期),System 注册由 SG 负责 |
| 业务错误用返回非异常 | PASS | MongoException 全 catch 转 uint 错误码;Handler 收到非 0 → return;无异常上抛 |
| Entity/Component/System 层级 | PASS | AccountServiceComponent 在 Entity 层(持集合句柄);AccountServiceComponentSystem / AccountServiceHelper / LoginGameRequestHandler 在 Hotfix 层;AccountDoc 是原生 BSON 文档(非框架 Entity),正确 |
| 生命周期清理 | PASS | DestroySystem 清空 Accounts = null;无定时器,不需 RemoveTimer |
| 机制边界 | PASS | 挂在 Gate Scene,通过 `scene.World.Database.GetDatabaseInstance as IMongoDatabase` 取原生实例;无误用 Roaming/SphereEvent/Address |

#### 4.2 Database check 逐项核

| 项 | 结论 | 依据 |
|---|---|---|
| 数据库通过 scene.World 获取 | PASS | `AccountServiceComponentSystem.Init`:用 `self.Scene.World.Database?.GetDatabaseInstance as IMongoDatabase`;符合 scene.World 路径 |
| 原生 IMongoDatabase(非框架 IDatabase) | PASS | 并发原子 upsert 需 UpdateOneAsync;框架 IDatabase 高层 API 只能先读后写(设计 35 / 32 §五 明令);实际通过 GetDatabaseInstance 取 IMongoDatabase 原生实例,与 MailDocs / RedeemDocs 先例一致 |
| 无先查后写竞态(database-check 错误4) | PASS | UpdateOneAsync + upsert=true 是单条原子命令;无 `First/Exist` + `Insert` 两步路径;设计 §3.2 红线「不先查后写两步」满足 |
| 首次保存字段完整(database-check 错误6) | PASS | SetOnInsert 一次性写入 AccountId / FirstLoginUnixMs / Status;Set 写 LastLoginUnixMs;首次 insert 四字段全写,无先保存再补字段路径 |
| SeparateTable 评估 | PASS(不需要) | AccountDoc 仅四个基础类型字段,无嵌套子数据,不满足 SeparateTable 条件 |

#### 4.3 Handler check(修改的 C2G_LoginGameRequestHandler)

| 项 | 结论 | 依据 |
|---|---|---|
| Handler 基类匹配 | PASS | `MessageRPC<C2G_LoginGameRequest, G2C_LoginGameResponse>`,对应既有 proto 协议,正确 |
| Handler sealed | PASS | `public sealed class C2G_LoginGameRequestHandler` |
| 业务错误通过 response.ErrorCode | PASS | upsert 非 0 → `response.ErrorCode = accountUpsertErrorCode; return;`;既有分支同样用 `response.ErrorCode = 1` |
| reply() 使用 | PASS | 未显式调用 reply(),框架 MessageRPC 自动在 Run 结束后 reply;无「reply 后再改 response」 |
| session.IsDisposed | PASS(合理) | 耗时操作(RegisterOrLogin MongoDB upsert)在账号名校验后进行;upsert 本身是 await 单条命令,耗时可接受;既有链路未改(session 存活性由框架保证) |
| 外科手术式改动 | PASS | 仅在「空账号检查后、AccountManageHelper.Add 之前」插入 upsert 调用 + non-0 短路;既有「解析/Add/挂会话身份/Online」步骤一行未改 |

#### 4.4 ECS check

| 项 | 结论 | 依据 |
|---|---|---|
| sealed class | PASS | AccountServiceComponent / System × 2 全部 sealed |
| 归属正确 Scene | PASS | 挂在 Gate Scene(OnCreateSceneEvent case SceneType.Gate 中 AddComponent);LoginGameHandler 跑在 Gate Scene;MongoDB World 配在 Gate Scene 的 World 下 |
| AwakeSystem / DestroySystem 齐备 | PASS | AwakeSystem 绑集合句柄;DestroySystem 清空 Accounts=null;无 UpdateSystem 需求 |
| 对象池复用风险 | PASS | DestroySystem 已清 Accounts=null;AccountDoc 是普通 BSON 文档,无对象池 |

#### 4.5 设计 35 SV12 Code Review 重点逐条核

| 重点 | 结论 | 依据 |
|---|---|---|
| upsert 用单条原子命令(非先查后写) | PASS | `accounts.UpdateOneAsync(filter, update, { IsUpsert=true })` 单次;无 Find + Insert 两步 |
| 仅在 insert 时写首次注册时间 | PASS | `SetOnInsert(x => x.FirstLoginUnixMs, nowMs)`;update 路径的 Set 仅含 LastLoginUnixMs,无 FirstLoginUnixMs |
| 每次都写末次登录时间 | PASS | `Set(x => x.LastLoginUnixMs, nowMs)` 在 insert 和 update 两条路径都执行 |
| insert/update 两路径均不触碰状态字段(除 insert 时写默认 0) | PASS | `SetOnInsert(x => x.Status, 0)` 仅 insert;update Builder 的 Set 部分完全不含 Status;SV5 实跑确认 |
| upsert 失败 → 返结果码 + 短路后续 | PASS | MongoException catch 转 LoginErrorCode;Handler non-0 → return 不执行 AccountManageHelper.Add / GateAccountFlagComponent / Online |
| 不手改 SG 产物 | PASS | 无 .g.cs 手改;AwakeSystem/DestroySystem 由 SG 注册(产物已确认) |
| 不手动注册 Handler | PASS | 本子单无新增 Handler;AccountServiceHelper 是静态工具类,无需注册 |
| MongoDB 连接沿用工程现有配置文件 | PASS | `self.Scene.World.Database` 路径沿用 Fantasy.config 的 `<database>` 配置;无新增连接串 |
| Fantasy.Net 约定全通 | PASS | FTask/sealed class/file-scoped namespace Fantasy/Log.Warning+Info+Error/不手改 .g.cs/不手动注册/原生 IMongoDatabase |

#### 4.6 持久文件交叉检(conventions.md)

对 `pipeline/state/server-dev.md`:
- 工作状态归属明确(标题清晰标注「Tier 0 第 1 子单」)。
- 无 diff 叙事 / 无对话痕迹 / 无「按你说的改成」类过程性内容。
- 正文无可推导事实副本(不转述其他文件内容)。
- 无拟人/口语比喻(grep「死/打死/收口」0 命中;`kill mongod` 是技术术语,合规)。
- 已关闭决策条目仅记决策结论,无开放待办残留。
- 被改动规范的旁注成立(旁注均有具体依据)。

---

### SV1-SV12 结论矩阵

| 验收点 | 结论 | 证据类型 |
|---|---|---|
| SV1 编译 + 源生成器产物 | PASS | 编译 0 error / SG 产物 AwakeSystem/DestroySystem 注册确认 |
| SV2 accounts 集合 schema | PASS | 探针首次 upsert 自动创建集合;四字段(AccountId/FirstLoginUnixMs/LastLoginUnixMs/Status)均在 |
| SV3 首连自动注册 | PASS | 探针实跑:新 UUID → UpsertedId 有值 + 四字段写入 + Status=0 |
| SV4 重连首次注册时间不变 | PASS | 探针实跑:FirstLoginUnixMs 不变 / LastLoginUnixMs 更新 / 仅一条记录 |
| SV5 状态字段不被覆盖 | PASS | 探针实跑:运营改 Status=9 → 再登 → 仍=9 |
| SV6 重启持久 | PASS | 重起服后探针查询:3 条记录(含 Status=9)完整保留 |
| SV7 并发同 UUID 双登原子 | PASS | 探针实跑:两并发 upsert → 一 insert 一 update → 仅一条记录 |
| SV8 MongoDB 不可达失败 | PASS(静态核) | AwakeSystem Warning+null / RegisterOrLogin null 检 + MongoException catch / Handler non-0 短路 |
| SV9 既有四特性零回归 | PASS(静态核) | 本子单未改任何既有集合操作;accounts 集合与既有业务集合独立并存 |
| SV10 account 字段语义对齐 | PASS(静态核) | AccountDoc._id = 裸 UUID 字符串,与既有四特性 account 字段同源 |
| SV11 客户端工程零 diff | PASS | 本子单 server-only,Unity Assets/ 的两处历史 diff 早于本子单 |
| SV12 Code Review | PASS | 见 4.1-4.6;七项通用 + ECS + Database + Handler + 设计 35 重点全通 |

---

### 协议同步检查项(E4)

本子单无 proto 改动(design-docs/35 §范围:复用既有 C2G_LoginGameRequest / G2C_LoginGameResponse,不新增 RPC)。Fantasy 仓库 git status 未显示 proto 路径(examples/Config/NetworkProtocol/Outer/)有 diff;客户端 NetworkProtocol/ 生成物无相关 diff。E4=N/A。

---

### 自主取舍(decisions)

- 总判定采用 PASS:四类验证均无代码缺陷;mongod 可达(便携版)+ 起服成功 + MongoDB 探针实跑验证了 SV2/SV3/SV4/SV5/SV6/SV7。
- SV6 重启持久采用「写入后重起服再用探针查询」验证(无 shell 内 kill/restart server 的直接手段,但两次起服 + 探针查询等价)。
- SV8/SV9/SV10/SV11 静态核:无起 mongod-kill 的环境条件;代码路径静态核已完整覆盖这些场景。
- 越界试探未试出新崩法;8 类场景代码路径全部静态验证为防住。
- 客户端 Assets/ 两处历史 diff 与本子单无关(git log 确认来自更早的「接入 Fantasy 服务端」相关 commit),SV11 PASS。

---

## 历史任务:邮件运营推送来源·服务端段验收

### 总判定:BLOCKED-env

四类验证中:编译(1)PASS、源生成器产物(2)PASS、Code Review(4)PASS;运行验证(3)受阻:产物 DLL 被现有 Main 进程(PID 54856)锁定,`dotnet run` 触发重新构建时 MSB3027 文件锁失败,无法起新服进程。属环境阻塞(跨会话进程锁产物,非代码缺陷),判 BLOCKED-env。

MongoDB 已可达(27017 TcpTestSucceeded=True),补跑 MongoDB 探针(`D:\tmp\mongo_probe_mail`)已完成,存储层数据可核:mail_template 8 条、gift_pool 6 条、mail_directed 0 条、mail_record 0 条均符合预期。SV12/SV13 存储持久通过静态核 + 探针双重确认。

---

## 历史任务:排行榜结算发奖上后端·服务端段验收

### 总判定:PASS

编译、源生成器产物、运行验证、Code Review 四类全部通过。结算是服务端内部触发,不依赖客户端 RPC,本轮实现了真往返验证:起服 + 活 MongoDB + 等待节律触发,直接观测到结算日志与 MongoDB 已结标记写入。
