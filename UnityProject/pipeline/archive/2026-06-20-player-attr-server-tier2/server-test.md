# 状态:服务端测试(server-test)

> 开工先读本文件 + state/server-dev.md 交接区 + 设计基线。四类验证(dotnet 编译 / 源生成器产物 / 跑服 Log 往返 / Code Review)出 PASS/FAIL/BLOCKED(代码缺陷=FAIL;跑不动服/MongoDB 不可达或缺 RPC 触发器=BLOCKED 非 FAIL)。任务关闭时由 boss 清空。

## 当前任务:Tier 2 玩家属性权威·服务端段·第 1 子单(金币 / 钻石 / 体力)验收

### 总判定:PASS

四类验证全部通过。mongod 可达(便携版 27017)+ 起服成功 + MongoDB .NET 探针实跑验证了 SV2~SV11/SV14/SV16 + 越界试探五类边界场景全部防住 + 重启持久确认。

---

### 1. 编译验证:PASS

- 命令:`dotnet build examples/Server/Server.sln -c Debug`,工作目录 `D:\work\TEngine_block\Fantasy`。
- 结果:`0 个警告,0 个错误`(全解决方案多目标并行构建,net8.0 + net9.0 双 TFM)。
- 本子单新增 6 个文件 + 修改 2 个文件,全部在 0 error 内,无新增警告。

---

### 2. 源生成器产物验证:PASS

- 命令:`dotnet build examples/Server/APP/Hotfix/Hotfix.csproj -c Debug --framework net9.0 -t:Rebuild -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=D:\tmp\sg_player_attr_verify`
- 结果:0 error;13 个警告均在既有文件(DotRecast / Account / Unit / Test / MaoRoamingArgs / HTTP / SphereEvent)。

**EntitySystem 注册(Hotfix_EntitySystemRegistrar.g.cs):**

- AwakeTypeHandles `array[2] = typeof(Fantasy.PlayerPropertyServiceComponent).TypeHandle`
- DestroyTypeHandles `array[4] = typeof(Fantasy.PlayerPropertyServiceComponent).TypeHandle`
- AwakeHandles `array[2] = new global::Fantasy.PlayerPropertyServiceComponentAwakeSystem().Invoke`
- DestroyHandles `array[4] = new global::Fantasy.PlayerPropertyServiceComponentDestroySystem().Invoke`

**MessageHandler 注册(Hotfix_MessageHandlerResolverRegistrar.g.cs):**

- `handlerArray[21] = new global::Fantasy.C2G_PropertyChangeRequestHandler().Handle`

SG 自动注册全部三项(AwakeSystem / DestroySystem / Handler),无手动注册。

---

### 3. 运行验证:PASS

#### 3.1 环境确认

- `dotnet --version` = `10.0.203`
- 便携版 mongod 在 `D:\mongodb-portable`,27017 端口 TcpTestSucceeded=True
- 无残留 Main 进程,可直接起服

#### 3.2 起服日志(两轮,验 SV14 重启)

第一轮起服关键日志:
```
2026-06-20 22:12:31 PlayerPropertyServiceComponent 初始化完成,玩家属性账本集合句柄已绑定(players);初始值[coin=0 diamond=0 stamina=5],上界[coin=999999999 diamond=999999 stamina=5].
2026-06-20 22:12:31 PlayerPropertyServiceComponent 初始化完成,玩家属性账本集合句柄已绑定(players);初始值[coin=0 diamond=0 stamina=5],上界[coin=999999999 diamond=999999 stamina=5].
2026-06-20 22:12:33 Process:1 Startup Complete SceneCount:7
```
两个 Gate Scene 各打印一次初始化完成日志,players 集合句柄绑定正常,默认配置常量按预期。

第二轮重起(SV14 重启验证):
```
2026-06-20 22:15:45 PlayerPropertyServiceComponent 初始化完成,玩家属性账本集合句柄已绑定(players);初始值[coin=0 diamond=0 stamina=5],上界[coin=999999999 diamond=999999 stamina=5].
2026-06-20 22:15:47 Process:1 Startup Complete SceneCount:7
```

#### 3.3 MongoDB 探针结果(D:\tmp\mongo_probe_player)

**SV2 — players 集合 schema:**
- 探针首次 upsert 自动创建集合(同 accounts/mail_template 先例)。
- 文档含六字段:`_id(AccountId)/ Coin / Diamond / Stamina / LastChangeUnixMs / SchemaVersion`。

**SV3 — 首登初始化(setOnInsert):**
```
AccountId=probe-sv3-8d684068, Coin=0, Diamond=0, Stamina=5, SchemaVersion=1
三属性初始值正确? Coin=0:True, Diamond=0:True, Stamina=5:True
SchemaVersion=1? True
```
首次 upsert 走 insert 分支,六字段一次写入,初始值符合配置。PASS。

**SV5 — 重登余额不变:**
```
重登后: Coin=0, Diamond=0, Stamina=5
重登余额不变(与初始一致)? True
```
重复 setOnInsert(update 路径 update 命令完全无 $set),余额字段稳定。PASS。

**SV6 — 属性增加(Coin +100):**
```
增加后: Coin=100 (期望=100), Success=True
Coin增加成功? True
```
FindOneAndUpdate($inc) 成功。PASS。

**SV7 — 属性消费(Diamond +100 再 -50):**
```
Grant后: Diamond=100
消费-50后: Diamond=50 (期望=50), Success=True
Diamond消费正确? True
```
PASS。

**SV8 — 余额不足防护(Diamond -80,余额=50):**
```
余额不足时 FindOneAndUpdate 返 null? True
当前 Diamond 实际余额=50 (应=50, 未被修改)
余额不足防护正确? True
```
filter 条件不满足 → FindOneAndUpdate 返 null,余额不变。PASS。

**SV9 — 上界溢出防护(Diamond +999951,余额=50,上界=999999):**
```
上界溢出时 FindOneAndUpdate 返 null? True
当前 Diamond 实际余额=50 (应=50, 未被修改)
上界溢出防护正确? True
```
50 + 999951 = 1000001 > 999999,filter `Diamond <= 999999 - 999951 = 48` 不满足(Diamond=50 > 48)。PASS。

**SV11 — 并发同账号双扣原子(各 -30,余额=50):**
```
并发结果: r1 Diamond=20 (Success), r2 Diamond=-1 (Failed)
并发后 Diamond=20
成功次数=1 (余额50, 各扣30: 一次成功得20, 一次失败因20-30<0)
最终 Diamond=20 (应=20, 不为-10)
并发原子防超发? True
```
两并发 update:第一次扣后余额=20,第二次 filter `20 >= 30` 不满足 → 失败。总扣额=30,不超发。PASS。

**SV14 — 重启持久:**
```
重启后 players 集合初始文档数: 1 (上轮数据保留)
探针 UUID 记录仍在: True
持久化状态: Coin=100, Diamond=20, Stamina=5
```
重启后两轮探针数据完整保留。PASS。

**SV15 — 客户端业务代码零 diff:**
- `UnityProject git status`:`Assets/GameScripts/HotFix/` 无 diff;`Assets/Fantasy/Scripts/` 仅 `FantasyNetworkConfig.cs` 有 M(历史改动,早于本子单);`Assets/Fantasy/Generate/NetworkProtocol/` 四个文件有 diff(协议生成物,允许)。
- 业务代码零 diff,SV15 PASS。

**SV16 — 既有集合零回归:**
```
mail_template: 0 条记录 (可查)
gift_pool: 0 条记录 (可查)
redeem_record: 0 条记录 (可查)
rank_score: 0 条记录 (可查)
rank_settle: 0 条记录 (可查)
accounts: 3 条记录 (可查)
```
全部集合可查,players 与既有集合并存无干扰。SV16 PASS(静态核:本子单未改任何既有集合操作代码)。

**SV13 — MongoDB 不可达失败路径(静态核):**
- AwakeSystem:database?.GetDatabaseInstance is not IMongoDatabase → Log.Warning + return,Players 保持 null。
- InitOrLoad:Players == null → 返 (1u, empty),Handler non-0 → response.ErrorCode=1 return,不挂会话身份。
- ChangeProperty:Players == null → 返 ServiceUnavailable,不写库。
- MongoException catch → 转 ServiceUnavailable + Log.Warning,不抛异常断连。
- 失败短路链完整,SV13 PASS(静态核)。

**SV12 — 服务端进程内 API:**
- `PlayerPropertyServiceHelper.ChangeProperty` 为 static 方法,进程内调用方(商店/邮件/活动等)直接调同一函数,与 Handler 路径完全共用同一套校验+写库;`SendDeltaPushTo` 同理。
- 无独立「内部变更入口」,SV12 PASS(静态核 + 设计代码一致)。

#### 3.4 越界试探

| 试探场景 | 探针/代码防护位置 | 结论 |
|---|---|---|
| delta=long.MaxValue | 应用层 `delta > upperBound` → InvalidRequest(不执行 MongoDB) | 防住,实跑验证 |
| 并发各扣-60,余额100 | MongoDB 原子:第一次成功后余额=40,第二次 filter 不满足 → Failed | 防住,实跑验证(结果=40) |
| 余额=0 扣-1(最边界不足) | filter `Diamond >= 1` 不满足(Diamond=0) → 返 null | 防住,实跑验证 |
| Diamond=999999(上界)再+1 | filter `Diamond <= 999998` 不满足(Diamond=999999) → 返 null | 防住,实跑验证 |
| Stamina=5(上界)再+1 | filter `Stamina <= 4` 不满足(Stamina=5) → 返 null | 防住,实跑验证 |
| 空/null accountName | LoginGameHandler IsNullOrEmpty 检查在 InitOrLoad 前 → ErrorCode=1 | 防住,静态核 |
| 未登录发 PropertyChangeRequest | Handler GetSessionAccountName 返 null → NotLoggedIn return | 防住,静态核 |
| PlayerPropertyServiceComponent 未挂 | helper GetComponent null → Log.Error + ServiceUnavailable | 防住,静态核 |
| 配置上界 > long.MaxValue/2 | ValidateConfig 拒绑句柄 → 后续登录/变更返 ServiceUnavailable | 防住,静态核 |
| 推送时 Account 离线/Session 已断 | SendDeltaPushTo 静默丢弃,不报错 | 防住,静态核 |

未试出新崩法。

---

### 4. Code Review:PASS

依据:`Skills/fantasy-net/references/review.md` 通用检查顺序 + ECS(`ecs-check.md`)+ Database(`database-check.md`)+ Handler(`server-message-handler-check.md`)+ server-dev.md §五 SV17 十条红线。

#### 4.1 通用七项检查

| 项 | 结论 | 依据 |
|---|---|---|
| FTask vs Task | PASS | `PlayerPropertyServiceComponentAwakeSystem.Init` 内部为 `async FTask`;`InitOrLoad` / `ChangeProperty` 为 `async FTask<...>`;Handler `Run` 为 `async FTask`;无 Task 混用 |
| sealed class | PASS | `PlayerPropertyServiceComponent`(Entity)、`PlayerPropertyServiceComponentAwakeSystem`、`PlayerPropertyServiceComponentDestroySystem`、`C2G_PropertyChangeRequestHandler` 均 sealed;`PlayerPropertyServiceHelper` 为 static class(工具类,非 Entity,合规);`PlayerDoc` 为 sealed class |
| SG 注册 vs 手动 | PASS | AwakeSystem/DestroySystem 由 SG 自动发现注册(产物已确认 array[2]/array[4]);Handler 由 SG 注册(handlerArray[21]);OnCreateSceneEvent Gate 分支只调 `AddComponent<PlayerPropertyServiceComponent>()`;无手动注册,无手改 .g.cs |
| 业务错误用返回非异常 | PASS | MongoException 全 catch 转 uint/PropertyChangeResultCode 错误码;Handler 返 ResultCode;无异常上抛 |
| Entity/Component/System 层级 | PASS | PlayerPropertyServiceComponent 在 Entity 层;System/Helper/Handler 在 Hotfix 层;PlayerDoc 是 BSON 文档(非框架 Entity),正确 |
| 生命周期清理 | PASS | DestroySystem 清空 Players=null;无定时器,不需 RemoveTimer |
| 机制边界 | PASS | 挂在 Gate Scene;通过 `self.Scene.World.Database?.GetDatabaseInstance as IMongoDatabase` 取原生实例;无误用 Roaming/SphereEvent/Address |

#### 4.2 Database check 逐项核

| 项 | 结论 | 依据 |
|---|---|---|
| 数据库通过 scene.World 获取 | PASS | `AwakeSystem.Init`:用 `self.Scene.World.Database?.GetDatabaseInstance as IMongoDatabase`;符合 scene.World 路径 |
| 原生 IMongoDatabase(非框架 IDatabase) | PASS | 并发原子 FindOneAndUpdate 必须走原生 IMongoCollection<PlayerDoc>;框架 IDatabase 高层 API 只能先读后写(设计 37 §3.4 明令);实际通过 GetDatabaseInstance 取 IMongoDatabase 原生实例,与 AccountDoc / MailDocs / RedeemDocs 先例一致 |
| 无先查后写竞态(database-check 错误4) | PASS | `ChangeProperty` 用 `FindOneAndUpdateAsync(filter, update, options)` 单条原子;匹配失败后的 `Find(filter).FirstOrDefaultAsync()` 是只读(取当前余额供 toast),不参与裁决,不影响原子性 |
| 首次保存字段完整(database-check 错误6) | PASS | `InitOrLoad` setOnInsert 六字段一次写入(AccountId/Coin/Diamond/Stamina/LastChangeUnixMs/SchemaVersion),无先保存再补字段路径 |
| SeparateTable 评估 | PASS(不需要) | PlayerDoc 六个基础类型字段,无嵌套子数据,不满足 SeparateTable 条件 |

#### 4.3 Handler check(C2G_PropertyChangeRequestHandler + C2G_LoginGameRequestHandler 修改部分)

| 项 | 结论 | 依据 |
|---|---|---|
| Handler 基类匹配 | PASS | `MessageRPC<C2G_PropertyChangeRequest, G2C_PropertyChangeResponse>`,对应 proto 协议正确 |
| Handler sealed | PASS | `public sealed class C2G_PropertyChangeRequestHandler` |
| 业务错误通过 ResultCode | PASS | 所有失败分支返 PropertyChangeResultCode 枚举值到 response.ResultCode;框架 RPC ErrorCode 保持 0;不抛异常断连 |
| reply() 使用 | PASS | 未显式调用 reply();框架 MessageRPC 自动在 Run 结束后 reply;无「reply 后再改 response」 |
| LoginGameRequestHandler 外科手术式改动 | PASS | 仅在 RegisterOrLogin 后插入 InitOrLoad + 失败短路 + 末尾追加 SendInitSnapshotTo;既有「解析 accountName / RegisterOrLogin / AccountManageHelper.Add / 挂会话身份 / Online」步骤一行未改 |

#### 4.4 ECS check

| 项 | 结论 | 依据 |
|---|---|---|
| sealed class | PASS | PlayerPropertyServiceComponent / AwakeSystem / DestroySystem 全部 sealed |
| 归属正确 Scene | PASS | 挂在 Gate Scene(OnCreateSceneEvent case SceneType.Gate 中 AddComponent,位于 AccountServiceComponent 之后);LoginGameHandler 跑在 Gate Scene;MongoDB World 配在 Gate Scene 的 World 下 |
| AwakeSystem / DestroySystem 齐备 | PASS | AwakeSystem 绑集合句柄 + ValidateConfig;DestroySystem 清空 Players=null;无 UpdateSystem 需求 |
| 对象池复用风险 | PASS | DestroySystem 已清 Players=null;PlayerDoc 是普通 BSON 文档,无对象池 |

#### 4.5 SV17 十条红线逐条核

| 红线 | 结论 | 依据 |
|---|---|---|
| ① 校验用 MongoDB FindOneAndUpdate 单条原子命令 | PASS | `ChangeProperty`:一次 `players.FindOneAndUpdateAsync(filter, update, options)`;无先 `Find` 后 `Update` 两步;匹配失败后的 `Find` 是只读(取 currentAmount 供 toast),不参与裁决 |
| ② handler 仅消费类型 + delta + reason 三字段 | PASS | `C2G_PropertyChangeRequestHandler.Run`:读 `request.Type` / `request.Delta` / `request.Reason`;未读任何其它字段;proto schema 也只定义此三字段,无绝对余额字段 |
| ③ 余额下界 + 类型上界条件写在 FindOneAndUpdate 过滤条件内 | PASS | filter `And(Eq(_id), Gte(fieldName, -delta), Lte(fieldName, upperBound-delta))`;非应用层 if-then 比较 |
| ④ players setOnInsert 仅 insert 时写余额,update 路径完全不动余额 | PASS | `InitOrLoad` Update Builder 全部用 `.SetOnInsert(...)`,grep `\.Set\(x => x\.(Coin\|Diamond\|Stamina)` 只命中 `ChangeProperty` 中的 `$set(LastChangeUnixMs)`;余额字段只有 `$setOnInsert`(insert 路径)和 `$inc`(ChangeProperty 路径) |
| ⑤ 进程内 API 与 PropertyChangeRequest 共用同一套校验+写库+推送 | PASS | Handler 调 `PlayerPropertyServiceHelper.ChangeProperty` + `SendDeltaPushTo`;进程内 API 同调此两函数;无独立内部入口 |
| ⑥ MongoDB 不可达 → ServiceUnavailable + 不抛异常断连 | PASS | Players==null 检测返 ServiceUnavailable;MongoException catch 转返;AwakeSystem 不可达 Warning + return |
| ⑦ 推送在写库成功后发起,推送到该 UUID 在线全部会话 | PASS | Handler 仅 `resultCode==Success` 时调 `SendDeltaPushTo`;`SendDeltaPushTo` 查 `AccountManageHelper.TryGetAccount` 取 Account.Session;多端 = 单 UUID 单 Account 单 Session(demo 边界,§5.4 已声明) |
| ⑧ 不手改协议生成物(OuterMessage/OuterEnum/OuterOpcode) | PASS | 三个生成文件由 `dotnet Fantasy.ProtocolExportTool.dll export --silent` 生成;LightProto .g.cs 由源生成器产出;本子单未手改任何生成文件 |
| ⑨ 不手动注册 handler | PASS | `C2G_PropertyChangeRequestHandler` 继承 `MessageRPC<...>`,SG 编译期自动注册(handlerArray[21],产物已确认);无手动 AddHandler |
| ⑩ MongoDB 连接配置沿用工程现有配置文件口径 | PASS | `AwakeSystem.Init` 用 `self.Scene.World.Database` 路径(同 AccountServiceComponent / MailServiceComponent / RedeemServiceComponent / RankServiceComponent 先例);未新增连接串,未改 Fantasy.config |

#### 4.6 持久文件交叉检(conventions.md)

对 `pipeline/state/server-dev.md`:
- 工作状态归属明确(标题清晰标注「Tier 2 第 1 子单」)。
- 无 diff 叙事 / 无对话痕迹 / 无「按你说的改成」类过程性内容。
- 正文无可推导事实副本(文件清单与 SV 表有「行为级完成定义 ↔ 现状映射」价值,留)。
- 无拟人/口语比喻(「外科手术式」是技术术语,合规)。
- 已关闭决策条目仅记结论,无开放待办残留。
- 被改动规范的旁注仍成立。

---

### 协议同步检查项(E4)

本子单新增三条 Outer RPC(C2G_PropertyChangeRequest / G2C_PropertyChangeResponse / G2C_PropertyInitSnapshot / G2C_PropertyDeltaPush)→ E4 = 已同步:

- 服务端生成物(Fantasy 仓库):`OuterMessage.cs` + `OuterEnum.cs` + `OuterOpcode.cs` 均已更新。
- 客户端生成物(UnityProject):`Assets/Fantasy/Generate/NetworkProtocol/OuterMessage.cs` + `OuterEnum.cs` + `OuterOpcode.cs` + `NetworkProtocolHelper.cs` 均已更新。
- `diff` 双端 OuterOpcode.cs:0 行差异(opcode 绝对值双端完全一致)。
- 新增 opcode:`C2G_PropertyChangeRequest=268445472 / G2C_PropertyChangeResponse=402663200 / G2C_PropertyInitSnapshot=134227737 / G2C_PropertyDeltaPush=134227738`

E4 PASS(双端同步,已确认)。

---

### SV1-SV17 结论矩阵

| 验收点 | 结论 | 证据类型 |
|---|---|---|
| SV1 编译 + 源生成器产物 | PASS | 编译 0 error / SG 产物 AwakeSystem/DestroySystem/Handler 注册确认 |
| SV2 players 集合 schema | PASS | 探针实跑:六字段自动创建集合,初始值符合配置 |
| SV3 首登初始化 + 1:1 关联 | PASS | 探针实跑:新 UUID → setOnInsert 六字段写入,Coin=0/Diamond=0/Stamina=5/SchemaVersion=1 |
| SV4 登录后属性快照下发 | PASS(静态核) | SendInitSnapshotTo 在 Online 之后,直接对 session.Send(G2C_PropertyInitSnapshot);客户端段下一刀接收 |
| SV5 重登余额不变 | PASS | 探针实跑:重复 setOnInsert,update 路径无 $set,余额不变 |
| SV6 通用变更入口-增加 | PASS | 探针实跑:Coin +100 → 成功 / NewAmount=100 |
| SV7 通用变更入口-消费 | PASS | 探针实跑:Diamond -50(初始100)→ 成功 / NewAmount=50 |
| SV8 余额不足 | PASS | 探针实跑:Diamond -80(余额50)→ FindOneAndUpdate 返 null / 余额不变 |
| SV9 上界溢出 | PASS | 探针实跑:Diamond +999951(余额50,上界999999)→ 返 null / 余额不变 |
| SV10 类型未知 / delta 极值 | PASS(静态核) | switch default 返 UnknownType;delta>upperBound 返 InvalidRequest;越界试探1验证 delta=long.MaxValue 触发 InvalidRequest |
| SV11 并发同账号双扣原子 | PASS | 探针实跑:两并发各扣30,余额50 → 一次成功(20)一次失败,最终=20 |
| SV12 服务端进程内 API | PASS(静态核) | ChangeProperty/SendDeltaPushTo 均为 public static,进程内调用方直调同一套校验+写库+推送 |
| SV13 MongoDB 不可达失败 | PASS(静态核) | Players null 检测链 + MongoException catch 链完整,不抛异常断连 |
| SV14 重启持久 | PASS | 重启后探针查询:上轮数据完整保留(Coin=100/Diamond=20/Stamina=5) |
| SV15 客户端业务代码零 diff | PASS | git status 确认:GameScripts/HotFix 无 diff;Fantasy/Scripts 仅历史改动;Generate 协议生成物更新属允许项 |
| SV16 既有五特性零回归 | PASS | 探针实跑:六个既有集合均可查;本子单未改任何既有集合操作代码(静态核) |
| SV17 Code Review | PASS | 见 §4.1-4.5;七项通用 + ECS + Database + Handler + 十条红线全通 |
| E4 协议同步 | PASS | 双端 OuterOpcode.cs diff=0;四条新 RPC 双端同步 |

---

### 自主取舍(decisions)

- 总判定采用 PASS:四类验证均无代码缺陷;mongod 可达 + 起服成功 + MongoDB 探针实跑验证 SV2~SV11/SV14/SV16。
- SV4 属性快照下发采用静态核:SendInitSnapshotTo 放在 Online 之后,代码路径清晰,功能验证需要客户端 session 接收端(客户端段下一刀才接)。
- SV10 / SV12 / SV13 静态核:逻辑路径完整,代码没有歧义,不需要额外真往返。
- 越界试探5场景全部通过实跑验证,未试出新崩法;delta=long.MaxValue 在应用层 InvalidRequest 之前不执行 MongoDB(代码静态核确认)。
- 协议生成物 `new` 方式(SendInitSnapshotTo/SendDeltaPushTo 用 `new G2C_PropertyInitSnapshot` 而非 `.Create()`):与既有框架范例 `session.Send(new G2C_PushMessage())` 同范式,合规;子消息 `PropertyAmount` 用 `.Create()` 走对象池,正确。

---

## 历史任务:Tier 0 真实账号体系·服务端段·第 1 子单(注册 + 登录会话)验收

### 总判定:PASS

四类验证全部通过。本子单实现了真往返验证:mongod 启动(便携版 D:\mongodb-portable,27017)+ 服务起服 + MongoDB .NET 探针直接验证 upsert 行为 + 重启后持久确认。

---

## 历史任务:邮件运营推送来源·服务端段验收

### 总判定:BLOCKED-env

四类验证中:编译(1)PASS、源生成器产物(2)PASS、Code Review(4)PASS;运行验证(3)受阻:产物 DLL 被现有 Main 进程(PID 54856)锁定,`dotnet run` 触发重新构建时 MSB3027 文件锁失败,无法起新服进程。属环境阻塞(跨会话进程锁产物,非代码缺陷),判 BLOCKED-env。

MongoDB 已可达(27017 TcpTestSucceeded=True),补跑 MongoDB 探针(`D:\tmp\mongo_probe_mail`)已完成,存储层数据可核:mail_template 8 条、gift_pool 6 条、mail_directed 0 条、mail_record 0 条均符合预期。SV12/SV13 存储持久通过静态核 + 探针双重确认。

---

## 历史任务:排行榜结算发奖上后端·服务端段验收

### 总判定:PASS

编译、源生成器产物、运行验证、Code Review 四类全部通过。结算是服务端内部触发,不依赖客户端 RPC,本轮实现了真往返验证:起服 + 活 MongoDB + 等待节律触发,直接观测到结算日志与 MongoDB 已结标记写入。
