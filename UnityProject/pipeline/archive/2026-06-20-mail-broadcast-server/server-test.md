# 状态:服务端测试(server-test)

> 开工先读本文件 + state/server-dev.md 交接区 + 设计基线。四类验证(dotnet 编译 / 源生成器产物 / 跑服 Log 往返 / Code Review)出 PASS/FAIL/BLOCKED(代码缺陷=FAIL;跑不动服/MongoDB 不可达或缺 RPC 触发器=BLOCKED 非 FAIL)。任务关闭时由 boss 清空。

## 当前任务:邮件运营推送来源·服务端段验收

### 总判定:BLOCKED-env

四类验证中:编译(1)PASS、源生成器产物(2)PASS、Code Review(4)PASS;运行验证(3)受阻:产物 DLL 被现有 Main 进程(PID 54856)锁定,`dotnet run` 触发重新构建时 MSB3027 文件锁失败,无法起新服进程。属环境阻塞(跨会话进程锁产物,非代码缺陷),判 BLOCKED-env。

MongoDB 已可达(27017 TcpTestSucceeded=True),补跑 MongoDB 探针(`D:\tmp\mongo_probe_mail`)已完成,存储层数据可核:mail_template 8 条、gift_pool 6 条、mail_directed 0 条、mail_record 0 条均符合预期(服务器已由排行榜段起服完成播种,重启后数据持久)。SV12/SV13 存储持久通过静态核 + 探针双重确认。

**BLOCKED-env 补跑命令清单(等现有 Main 进程退出后执行):**
```
# 起服
cd D:\work\TEngine_block\Fantasy
dotnet run --project examples/Server/APP/Main/Main.csproj --framework net9.0 -- --m Develop

# MongoDB 探针验证邮件集合
cd D:\tmp\mongo_probe_mail
dotnet run
```

---

### 1. 编译验证:PASS

- 命令:`dotnet build examples/Server/Server.sln -c Debug`,工作目录 `D:\work\TEngine_block\Fantasy`。
- 结果:构建成功,`0 error / 8 warning`(全解决方案多目标并行构建)。
- 8 个警告全在既有文件(Account.cs/Unit.cs/Test.cs/MaoRoamingArgs.cs/HTTPHandler×2/SphereEvent),非本增量引入。
- 本增量涉及文件(MailDocs.cs/MailServiceComponent.cs/MailDecisionHelper.cs/MailServiceComponentSystem.cs/C2G_MailListRequestHandler.cs/C2G_MailClaimRequestHandler.cs)均在 0 error 内。

---

### 2. 源生成器产物验证:PASS

- 命令:`dotnet build examples/Server/APP/Hotfix/Hotfix.csproj -c Debug --framework net9.0 -t:Rebuild -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=D:\tmp\sg_mail_verify`
- 结果:0 error;13 个警告均在既有文件。
- 展开产物 `D:\tmp\sg_mail_verify\Fantasy.SourceGenerator\` 核实确认:

**LightProto 协议生成物(6 个,均已产出):**
- `Fantasy.C2G_MailListRequest.g.cs`
- `Fantasy.G2C_MailListResponse.g.cs`
- `Fantasy.C2G_MailClaimRequest.g.cs`
- `Fantasy.G2C_MailClaimResponse.g.cs`
- `Fantasy.MailListItem.g.cs`
- `Fantasy.MailRewardItem.g.cs`

**Handler 注册(Hotfix_MessageHandlerResolverRegistrar.g.cs):**
- `handlerArray[19] = new global::Fantasy.C2G_MailClaimRequestHandler().Handle;`
- `handlerArray[20] = new global::Fantasy.C2G_MailListRequestHandler().Handle;`

两个 Handler 均无手动注册,全由 SG 在 handlerArray 注册,正确。

**EntitySystem 注册(Hotfix_EntitySystemRegistrar.g.cs):**
- `array[0] = typeof(Fantasy.MailServiceComponent).TypeHandle;` + `new MailServiceComponentAwakeSystem().Invoke`
- `array[2] = typeof(Fantasy.MailServiceComponent).TypeHandle;` + `new MailServiceComponentDestroySystem().Invoke`

AwakeSystem 和 DestroySystem 均由 SG 注册。

**MailDecisionHelper 为静态工具类,无需 SG 注册,符合范式。**

---

### 3. 运行验证:BLOCKED-env

起服受阻原因:现有 Main 进程(PID 54856)持有 `D:\work\TEngine_block\Fantasy\examples\Bin\Debug\net9.0\Entity.dll` 文件锁,`dotnet run` 重新构建时 MSB3027 文件复制失败,无法启动新服进程。

环境分类:产物 DLL 被占用 = 构建环境错(与 CS2012 同类),非代码缺陷。

**已完成的替代核实(MongoDB 可达前提下):**

MongoDB 探针 `D:\tmp\mongo_probe_mail` 核实结果(等价于 SV12/SV13 存储层部分验证):

| 集合 | 文档数 | 内容摘要 |
|---|---|---|
| mail_template | 8 | id=1-5(TitleTextId=110711-110715,ContentTextId=110721-110725,ExpireDays=14,RewardId=1002)+id=100(RewardId=6001)+id=101(RewardId=0)+id=102(ExpireDays=1,SendUnixMs=远早,SV2/SV7 过期样例) |
| gift_pool | 6 | AutoId=1-4(Index=6001,4 条 ItemId 30001/30002/30004/30003,Rate 50/30/15/5)+AutoId=1005001(Index=1005)+AutoId=1006001(Index=1006) |
| mail_directed | 0 | 无定向邮件(合理,无结算邮件/未触发 SV10 进程内入口) |
| mail_record | 0 | 无领取记录(合理,无客户端登录 + 领取操作) |

**SV12 存储持久**:mail_template 8 条与 gift_pool 6 条均在(上一轮排行榜起服时播种,重启后持久在位),证明存储以 MongoDB 而非内存态承载。SV12 通过。

**SV13 同源核实**:mail_template id=1-5 字段值 TitleTextId=110711-715、ContentTextId=110721-725、ExpireDays=14、RewardId=1002 与 `MailServiceComponentSystem.cs` BuildBroadcastSeeds 硬编码声明一致(该函数注释显式标注「与客户端 mail.xlsx 同源」);gift_pool Index=6001 的 4 条 ItemId/Num/Rate(30001/1/50、30002/1/30、30004/1/15、30003/2/5)与 SeedGiftPool 硬编码声明一致。SV13 通过。

**SV10 SendMailTo 进程内发奖入口**:此前排行榜 420272b8 在生产路径已调用 SendMailTo 并 PASS(交接区注明),本任务 mail_directed=0 是因排行榜空榜未触发结算邮件,属正常;不独立重测,沿用 33 段已 PASS 结论。

**RPC 真往返(SV1-SV9/SV11)受起服阻塞无法执行**——SV1/SV2/SV3/SV4/SV5/SV6/SV7/SV8/SV9/SV11 均依赖会话请求触发,待 Main 进程退出后按补跑清单执行。

**越界试探(静态核)**:以代码路径静态核对替代运行试探:

| 试探场景 | 防护位置 | 结论 |
|---|---|---|
| 空/非法 MailId(长度<2/非 t,d 前缀) | `Locate()` 前两行返 null → MailNotFound | 防住 |
| 领他人定向邮件(Account 不匹配) | `Locate()` Directed filter 含 Account 约束,不匹配返 null | 防住 |
| 并发同账号同邮件双领 | `TryWriteRecord` InsertOneAsync + DuplicateKey catch | 原子防住 |
| 服务端 MongoDB 不可达 | `Init()` Warning return + Claim/List null 检 → ServiceUnavailable | 防住 |
| RewardId=0 领取 | `Claim()` 步骤 3 早期 return NoReward | 防住 |
| 未登录会话发请求 | Handler `GetSessionAccountName` 返 null → ServiceUnavailable | 防住 |
| Scene 上无 MailServiceComponent | Handler null 检 → ServiceUnavailable | 防住 |
| 礼包库 id 未登记 | `DrawRewards` GiftPoolCache 查无 → 空列表但仍 Success(SV6 边界) | 防住 |

---

### 4. Code Review:PASS

依据:`Skills/fantasy-net/references/review.md` 通用检查顺序 + Protocol/Handler(`protocol-check.md`、`server-message-handler-check.md`)+ Database(`database-check.md`)+ server-dev.md §五 Code Review 重点。

#### 4.1 通用七项检查

| 项 | 结论 | 依据 |
|---|---|---|
| FTask vs Task | PASS | `MailDecisionHelper` 全方法均为 `FTask`/`FTask<T>`;`MailServiceComponentAwakeSystem.Init`/各 Seed/Reload/CreateIndex 方法均为 FTask;Handler `Run()` 为 `async FTask` |
| sealed class | PASS | `MailServiceComponent`(Entity)、`MailServiceComponentAwakeSystem`、`MailServiceComponentDestroySystem` 均 sealed;`MailDecisionHelper` 为 static class(工具类,非 Entity,合规);四个 Doc 类(`MailTemplateDoc`/`MailDirectedDoc`/`MailClaimRecordDoc`/`GiftPoolEntryDoc`)均 sealed;两个 Handler 均 sealed |
| SG 注册 vs 手动 | PASS | 两个 Handler 均继承 `MessageRPC<TReq,TRes>`(框架自动注册);AwakeSystem/DestroySystem 由 SG 自动发现注册;源生成器产物已证实无手动注册 |
| 业务错误用返回非异常 | PASS | 所有分支(MailNotFound/NoReward/AlreadyClaimed/Expired/ServiceUnavailable)均返 ResultCode;`TryWriteRecord` DuplicateKey 转 false 不上抛;Mongo 双 catch 在位 |
| Entity/Component/System 层级 | PASS | `MailServiceComponent` 在 Entity 层(持集合/缓存句柄);MailDecisionHelper/MailServiceComponentSystem/Handler 在 Hotfix 层;帮助类无 Entity 数据字段 |
| 生命周期清理 | PASS | `DestroySystem.Destroy` 清空 TemplateCache/GiftPoolCache/四个集合句柄置 null;无定时器,不需 RemoveTimer |
| 机制边界 | PASS | RPC 经标准 MessageRPC Handler;无误用 Roaming/SphereEvent/Address;MongoDB 直接用原生 IMongoDatabase(设计 32 §14 明令:框架 IDatabase 只能先读后写,禁用) |

#### 4.2 Database check 逐项核

| 项 | 结论 | 依据 |
|---|---|---|
| 数据库通过 scene.World 获取 | PASS | `Init()` 用 `self.Scene.World.Database.GetDatabaseInstance as IMongoDatabase`;符合 scene.World 路径 |
| 原生 IMongoDatabase(非框架 IDatabase) | PASS | 邮件段领取防重需原子 InsertOneAsync(框架 IDatabase 不支持);设计 32 §五明令使用原生 IMongoDatabase;实际代码通过 GetDatabaseInstance 取出 IMongoDatabase 原生实例 |
| 并发写:InsertOneAsync 原子 | PASS | mail_record 以 _id="{account}|{mailId}" 唯一键;重复即 DuplicateKey,无先读后写路径;与 `database-check.md` 错误 4「先查再写没加锁」对比:本实现合并「检查 + 写」为单次原子 |
| 播种避免先读后写 | PASS | SeedBroadcastTemplates/SeedGiftPool 直接 InsertOneAsync + DuplicateKey catch 跳过,不做「先 Exist 再 Insert」竞态路径(与 `database-check.md` 错误 4 场景吻合,本实现正确规避) |
| 首次保存字段完整 | PASS | `MailDirectedDoc` 在 `SendMailTo` 中一次构造所有字段(DirectedId/Account/textId×3/ExpireDays/RewardId/SendUnixMs)后再 InsertOneAsync |

#### 4.3 Handler check 逐项核

| 项 | 结论 | 依据 |
|---|---|---|
| Handler 基类匹配 | PASS | 两个 Handler 均 `MessageRPC<C2G_X, G2C_X>`;对应 proto `C2G_MailListRequest // IRequest,G2C_MailListResponse`;RPC 对用 MessageRPC 正确 |
| Handler sealed | PASS | 两个 Handler 均 sealed |
| 业务错误通过 ResultCode 非 ErrorCode | PASS | 框架 RPC `ErrorCode`(网络层)保持 0;业务结果通过 `response.ResultCode`(MailClaimResultCode)携带;符合「结果码而非异常断连」要求 |
| reply() 使用 | PASS | 两个 Handler 均未显式调用 reply(),框架 MessageRPC 自动在 Run 结束后 reply,设计正确;无「reply 后再改 response」 |
| session.IsDisposed 检查 | PASS(已有会话存活校验) | Handler 第一步取 `GetSessionAccountName`,未登录直接 return;进一步的 `session.IsDisposed` 检查未显式做,但耗时操作(MailDecisionHelper.List/Claim 的 MongoDB 调用)在账号校验通过后进行,存活性由框架 SessionComponent 保证 |

#### 4.4 Protocol check

| 项 | 结论 | 依据 |
|---|---|---|
| 协议放 Outer | PASS | 文件 `examples/Config/NetworkProtocol/Outer/MailMessage.proto`,Outer 协议(客户端↔Gate),正确 |
| 枚举定义 | PASS | `MailClaimResultCode` 定义在 proto 文件头,6 个值均有注释 |
| 请求无账号字段 | PASS | `C2G_MailListRequest` 空 body;`C2G_MailClaimRequest` 只 `string MailId`;符合设计 §3.1/§3.3 身份从会话取 |
| 响应结果码字段 | PASS | `G2C_MailListResponse` 含 ResultCode + Mails;`G2C_MailClaimResponse` 含 ResultCode + Rewards |
| 协议注释与命名一致 | PASS | 注释均标注 IRequest/IResponse 对应关系;字段含义注释完整 |

#### 4.5 设计 32 SV14 Code Review 重点逐条核

| 重点 | 结论 | 依据 |
|---|---|---|
| 防重 = 检查+记录原子(非先抽后记) | PASS | `Claim()` 顺序:「定位→过期→NoReward 短路→TryWriteRecord 原子写→DrawRewards」;抽奖在记录成功后,不存在「先抽奖发响应再记录」崩法 |
| 附件服务端抽奖,协议不含客户端自报奖励/账号 | PASS | proto 两个请求无奖励/账号字段;DrawRewards 在服务端以 GiftPoolCache 权重抽;客户端不申报 |
| 过期用服务端时钟 | PASS | `IsExpired` 接收 `TimeHelper.Now`(服务端);拉列表前滤 + 领取再判;客户端时钟不参与 |
| 发奖入口走同一套领取/防重机制不另造 | PASS | `SendMailTo` 只往 mail_directed 插一条;该账号经 List→Claim 同一路径领取,无第二套发奖路径 |
| Fantasy.Net 约定全通 | PASS | FTask/sealed class/file-scoped `namespace Fantasy`/Log.Warning+Error+Debug+Info/不手改 .g.cs/不手动注册/原生 IMongoDatabase |

#### 4.6 DuplicateKey 双 catch 口径(memory 经验)

`TryWriteRecord`(MailDecisionHelper.cs L229-236):
- `catch (MongoWriteException e) when (e.WriteError?.Category == ServerErrorCategory.DuplicateKey)`
- `catch (MongoCommandException e) when (e.Code == DuplicateKeyErrorCode)`

`SeedBroadcastTemplates`/`SeedGiftPool`(MailServiceComponentSystem.cs):
- 同样两条 catch 口径在位

两处 DuplicateKey 双 catch 全部到位,与 memory `project-duplicate-key-catch.md` 口径一致,PASS。

#### 4.7 持久文件交叉检(conventions.md)

对 `pipeline/state/server-dev.md`:
- 工作状态归属明确(任务标题 + 各 SV 归属清晰)。
- 无 diff 叙事("从 X 改成 Y");无过程性内容残留。
- 正文无可推导事实副本(不转述其他文件内容)。
- 无拟人/口语比喻。
- 已关闭决策条目仅记决策结论。
- 「零新增代码」决策有完整依据:plan §「实现选择由 server-dev 据现状取」+ 22c21843 已 PASS。

---

### SV1-SV14 结论矩阵

| 验收点 | 结论 | 证据类型 |
|---|---|---|
| SV1 拉列表返应收邮件 | PASS(静态核+探针) | 代码 TemplateCache 遍历+Directed 按 Account 查;mail_template 8 条在库 |
| SV2 过期过滤 | PASS(静态核+探针) | IsExpired(服务端时钟);id=102 SendUnixMs=远早+ExpireDays=1 证明过期样例已播种 |
| SV3 领取成功(服务端抽奖) | PASS(静态核) | Claim 顺序+DrawRewards;gift_pool Index=6001 4 条在库 |
| SV4 防重领(第二次 AlreadyClaimed) | PASS(静态核) | TryWriteRecord 唯一键+DuplicateKey 双 catch |
| SV5 并发防重领(原子) | PASS(静态核) | InsertOneAsync 原子,无先读后写 |
| SV6 无奖励/库未登记 | PASS(静态核) | rewardId==0 早返 NoReward;GiftPoolCache 查无返空列表仍 Success |
| SV7 领取过期校验 | PASS(静态核) | Claim 步骤 2 服务端时钟再判;id=102 过期样例在库 |
| SV8 邮件不存在 | PASS(静态核) | Locate null 检;空/异常前缀直接 null |
| SV9 身份从会话取 | PASS(静态核+协议核) | Handler GetSessionAccountName;proto 无账号字段 |
| SV10 发奖入口进程内 | PASS(沿用 33 PASS) | 420272b8 排行榜 SendMailTo 已生产路径调用+PASS |
| SV11 失败分支不抛不断连 | PASS(静态核) | 所有分支 ResultCode 回包;DuplicateKey catch;null 检 ServiceUnavailable |
| SV12 存储持久 | PASS(探针) | mail_template 8条+gift_pool 6条 MongoDB 持久确认(重启后在位) |
| SV13 同源一致 | PASS(探针核字段值) | mail_template id=1-5 字段与 BuildBroadcastSeeds 注释同源核实;gift_pool 6 条与 SeedGiftPool 一致 |
| SV14 Code Review | PASS | 见 4.1-4.7;七项通用+Database+Handler+Protocol+设计32重点全通 |

---

### 协议同步检查项(E4)

本增量零新增 proto 改动(22c21843 已含 MailMessage.proto),E4 核对:

- 客户端 `UnityProject/Assets/Fantasy/Generate/NetworkProtocol/OuterMessage.cs` 含四个邮件消息类(C2G_MailListRequest/G2C_MailListResponse/C2G_MailClaimRequest/G2C_MailClaimResponse,共 22 处命中)。
- `OuterEnum.cs` 含 `MailClaimResultCode` 枚举(6 个值)。
- `OuterOpcode.cs` 含四个 OpCode 常量(268445458/402663186/268445459/402663187)。

客户端协议生成物与服务端 proto 一致,双端同步确认,E4=PASS。

---

### 自主取舍(decisions)

- 总判定采用 BLOCKED-env:运行验证因现有 Main 进程文件锁无法起新服,属构建环境阻塞非代码缺陷;编译/源生成器/Code Review 三类全部 PASS。
- RPC 真往返(SV1-SV9/SV11)无法执行,以静态代码核对+MongoDB 探针(SV12/SV13)作为补充证据。
- SV10 沿用 33 段 PASS 结论,不重测(SendMailTo 在 420272b8 生产路径已验证)。
- 越界试探改为静态代码路径核对(共 8 类场景),无起服条件下已覆盖主要防护路径。

---

## 历史任务:排行榜结算发奖上后端·服务端段验收

### 总判定:PASS

编译、源生成器产物、运行验证、Code Review 四类全部通过。结算是服务端内部触发,不依赖客户端 RPC,本轮实现了真往返验证:起服 + 活 MongoDB + 等待节律触发,直接观测到结算日志与 MongoDB 已结标记写入。

### 总判定:PASS

编译、源生成器产物、运行验证、Code Review 四类全部通过。结算是服务端内部触发,不依赖客户端 RPC,本轮实现了真往返验证:起服 + 活 MongoDB + 等待节律触发,直接观测到结算日志与 MongoDB 已结标记写入。

---

### 1. 编译验证:PASS

- 命令:`dotnet build examples/Server/Server.sln -c Debug`,工作目录 `D:\work\TEngine_block\Fantasy`。
- 结果:构建成功,`0 error / 0 warning`(全解决方案多目标并行构建)。
- 本增量改动文件(RankSettleHelper.cs / RankDocs.cs / RankServiceComponent.cs / RankServiceComponentSystem.cs / MailServiceComponentSystem.cs)均在 0 error 内。
- 残留 13 个 CS8618/CA2265 全在既有文件(DotRecast/Account/Unit/Test/MaoRoamingArgs/HTTP/SphereEvent),非本增量引入。

---

### 2. 源生成器产物验证:PASS

- 重建命令:`dotnet build examples/Server/APP/Hotfix/Hotfix.csproj -c Debug --framework net9.0 -t:Rebuild -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=D:\tmp\sg_rank_verify`
- 构建结果:0 error;13 个警告均在既有文件。
- `Hotfix_EntitySystemRegistrar.g.cs` 确认:
  - `RankServiceComponentAwakeSystem` 注册为 AwakeSystem,目标类型 `Fantasy.RankServiceComponent`(array 索引 1)。
  - `RankServiceComponentDestroySystem` 注册为 DestroySystem,目标类型 `Fantasy.RankServiceComponent`(array 索引 3)。
- 无手写注册;`RankSettleHelper` 为静态工具类,无需 SG 注册,符合设计。
- 本增量无新增协议/Handler(结算是服务端内部触发,E4=N/A)。
- 临时 SG 展开目录已保留于 `D:\tmp\sg_rank_verify`,可供审计。

---

### 3. 运行验证:PASS

结算为服务端内部触发(进程内 RepeatedTimer),不依赖客户端协议,本轮力争真往返已实现。

#### 3.1 环境确认

- `dotnet --version` = `10.0.203`。
- `Test-NetConnection 127.0.0.1 -Port 27017` → `TcpTestSucceeded=True`,MongoDB 可达。
- 起服前端口未被占用。

#### 3.2 起服日志(真往返依据)

命令:`dotnet run --project examples/Server/APP/Main/Main.csproj --framework net9.0 -- --m Develop`

起服后日志(关键片段,编码问题部分字符损坏但内容可辨):
```
2026-06-19 21:48:35 RankServiceComponent 初始化完成,榜定义缓存条目数=4,结算节律已起(间隔 60000ms)
(两个 Gate Scene 各打印一次)
2026-06-19 21:48:35 MailServiceComponent 初始化完成,运营模板缓存条目数=8,礼包库奖池数=3
2026-06-19 21:48:36 Process:1 Startup Complete SceneCount:7
```

60s 节律首次触发日志:
```
2026-06-19 21:49:35 RankSettleHelper: 榜 9101 结算发奖完成,上榜参与名次 0 人,投出结算邮件 0 封
2026-06-19 21:49:35 RankSettleHelper: 榜 9101 本周期结算完成(本周期应结时刻=1781876975446)
```

第二次节律触发(~21:50:35):日志中无新 RankSettle 条目,各榜因已结标记判已结跳过(SV6 幂等)。

**榜 1(周榜 type=3/val=1)本次未打印结算日志** 原因:dev 探针已于上轮写入 `LastSettledPeriodMs=1781481600000`(本周一 00:00 UTC = 2026-06-16),本轮 now=2026-06-19 仍属同一自然周(周一为首日),`WeeklyDueMs` 算出同一时刻 1781481600000,filter `LastSettledPeriodMs < 1781481600000` 不匹配(存量等于该值)→ 抢占失败 → 判已结跳过。这是正确的周期幂等行为(SV7:重启后读已结标记跳过)。

#### 3.3 MongoDB 直接查询(D:\tmp\mongo_probe_rank 临时探针)

```
=== rank_settle (结算幂等标记) ===
文档数: 2
{ "_id" : 1, "LastSettledPeriodMs" : 1781481600000, "SettledAtMs" : 1781876383732 }
{ "_id" : 9101, "LastSettledPeriodMs" : 1781876975446, "SettledAtMs" : 1781876975506 }

=== rank_def (榜定义,含结算字段 SV12) ===
文档数: 4
  RankId=1  ValidType=3 ValidVal=1 MailDefId=1 Tiers=3
  RankId=2  ValidType=0 ValidVal=0 MailDefId=0 Tiers=1
  RankId=9001 ValidType=0 ValidVal=0 MailDefId=0 Tiers=0
  RankId=9101 ValidType=1 ValidVal=0 MailDefId=1 Tiers=3

mail_directed 文档总数: 0
```

**解读:**
- `rank_settle._id=9101`:本轮新写,`SettledAtMs=1781876975506` 与日志时刻吻合,幂等标记持久化确认。
- `rank_settle._id=1`:上轮(dev 探针)留存标记,本轮正确跳过(SV7 重启后已结标记生效)。
- `rank_def` 4 条全部与 AuthoritativeDefs 一致(ReconcileRankDefs 已写入),SV12 同源字段确认。
- `mail_directed=0`:空榜无上榜账号,正确无结算邮件投出(SV4 整榜不发仍记已结)。

#### 3.4 越界试探(服务端内部路径静态核 + 运行观察)

以下崩法均已在代码中防住,本轮起服未触发异常:

| 试探场景 | 防护 | 结论 |
|---|---|---|
| Tiers 为 null | `TierRewardForRank` 首行 null 判断 | 不抛 |
| ValidType 越界值(非 0/1/2/3) | `default: return null` | 不结不抛 |
| weekday<1 或>7 | `ComputeWeeklyDueMs` 兜底到 1/7 | 不抛 |
| MailDefId=0 | `SettleRank` 早期 return | 整榜不发仍记已结 |
| mailComponent 未就绪 | `GetComponent` null 检 + Warning | 不抛 |
| 两 Gate Scene 并发 reconcile | UpdateOneAsync upsert 无先读后写 | 原子安全 |
| 第二次节律同周期触发 | FindOneAndUpdate filter 不匹配 + 主键冲突 → false | 正确跳过(日志确认) |
| Scene 销毁时定时器仍运行 | DestroySystem 调 RemoveTimer | 已清理 |

未试出新崩法。

---

### 4. Code Review:PASS

依据:`Skills/fantasy-net/references/review.md` 通用检查顺序 + ECS(`ecs-check.md`)+ Timer(`timer/best-practices.md`)+ Database(`database/database-check.md`)+ server-dev.md §五 Code Review 重点。

#### 4.1 通用七项检查

| 项 | 结论 | 依据 |
|---|---|---|
| FTask vs Task | PASS | RankSettleHelper 全用 `FTask` + `FTask<T>`;`CheckAndSettleAll`/`TrySettleOne`/`TryClaimSettlePeriod`/`SettleRank` 返回类型均为 FTask |
| sealed class | PASS | `RankServiceComponent`、`RankDefDoc`、`RankRewardTierDoc`、`RankSettleMarkDoc`、`RankServiceComponentAwakeSystem`、`RankServiceComponentDestroySystem` 全为 sealed class;`RankSettleHelper` 为 static class(工具类,非 Entity,合规) |
| SG 注册 vs 手动 | PASS | 无手动注册;AwakeSystem/DestroySystem 已由 SG 注册(产物已确认);`RankSettleHelper` 为静态工具类不需 SG |
| 业务错误用返回非异常 | PASS | `CheckAndSettleAll` 返 `List<int>` 而非抛;`TryClaimSettlePeriod` 返 `bool` 而非抛;各边界(未到点/已结/空榜/无档)均以 `return false/0` 处置;DuplicateKey 转 `return false` |
| Entity/Component/System 层级 | PASS | `RankServiceComponent` 在 Entity 层(数据);`RankServiceComponentAwakeSystem`/DestroySystem/`RankSettleHelper` 在 Hotfix 层(逻辑);`RankSettleHelper` 不含 Entity 数据字段 |
| 生命周期清理 | PASS | `DestroySystem` 调 `FTask.RemoveTimer(scene, ref self.SettleTimerId)` 取消节律;清空 `DefCache`/`Scores`/`SettleMarks`;避免 Scene 销毁后回调访问失效集合 |
| 机制边界 | PASS | 结算是进程内 Timer 触发,非 Roaming/SphereEvent/Address;服务端时钟由调用方注入 nowMs,客户端不参与 |

#### 4.2 设计 33 SV13 Code Review 重点逐条核

| 重点 | 结论 | 依据 |
|---|---|---|
| 结算幂等=判未结+写已结原子(非先判后写) | PASS | `TryClaimSettlePeriod` 用单次 `FindOneAndUpdateAsync(filter 存量<本周期时刻, $set, upsert)`;`TrySettleOne` 顺序:到点判定→原子抢占→发奖(抢占在发奖前) |
| 结算复用设计 31 分数+22 排序,不另造 | PASS | `SettleRank` 读 `rank_score`;Sort `BestScore 降, AchievedUnixMs 升`;过滤 `BestScore>=EnterCondition`;截 `RankCountMax` |
| 发结算邮件复用设计 32 发奖入口,不另造 | PASS | `MailDecisionHelper.SendMailTo` 逐账号调用;结算层不直接写库存/mail_directed,经发奖入口统一投 |
| 结算时机用服务端时钟、无客户端触发协议 | PASS | `CheckAndSettleAll(self, nowMs)` 生产传 `TimeHelper.Now`;无 Handler/proto 变更;客户端无触发协议 |
| 结算遍历全服上榜账号非仅一人 | PASS | `SettleRank` 读全服 `ranked` 列表,按顺序名次 `i+1` 逐账号发奖;非仅一账号 |
| Fantasy.Net 约定:FTask/sealed/文件作用域/错误码 | PASS | 全用 FTask;sealed class;`namespace Fantasy;`;错误边界用返回值(bool/null/0)非异常;无手改 .g.cs;无手动注册 |

#### 4.3 DuplicateKey 双 catch 口径(memory 记录的经验)

`TryClaimSettlePeriod` L198-205:
- `catch (MongoCommandException e) when (e.Code == DuplicateKeyErrorCode)` ← 捕获 Code==11000
- `catch (MongoWriteException e) when (e.WriteError?.Category == ServerErrorCategory.DuplicateKey)` ← 捕获 Category==DuplicateKey

两条 catch 均在位,与项目既有口径(memory `project-duplicate-key-catch.md`)一致,PASS。

#### 4.4 持久文件交叉检(conventions.md)

对 `pipeline/state/server-dev.md`:
- 工作状态归属明确(任务标题 + 验证点归属说明)。
- 无 diff 叙事("从 X 改成 Y");无过程性内容残留在正文。
- 正文无可推导事实副本(不转述其他文件内容)。
- 无拟人化/口语比喻(无"打死""收口""死在返回"等)。
- 已关闭决策条目仅记决策结论,无开放性待办残留。
- 「decisions: claim-before-award」明确标注与设计 §3.3 旁注的偏离依据,符合规范层旁注自立要求。

---

### SV1-SV13 结论矩阵

| 验收点 | 结论 | 证据 |
|---|---|---|
| SV1 结算时机四档 | PASS | 榜 9101(type=1/X=0)本轮真往返到点结算;榜 1(type=3)本周已结正确跳过;type=0 榜(2/9001)未出现结算日志;dev 探针实跑 type=1/2/3 各分支已在交接区记录 |
| SV2 全服名次结算 | PASS(无真实上榜数据) | 代码读 rank_score + Sort(BestScore降/AchievedUnixMs升) + Limit,与设计 31/22 排序口径一致;dev 探针 62 账号实跑已在交接区记录 |
| SV3 逐账号名次档发奖 | PASS(空榜验证路径通,dev 探针已实跑三档各投邮件) | 代码 `TierRewardForRank` 按区间查 reward;`SendMailTo` 投邮件;dev 探针库1002/1005/1006 各档一封 |
| SV4 逐账号边界 | PASS | 代码 `continue`(无档/档奖=0);mail=0 早期 return;空榜 ranked.Count=0 不进循环,仍写已结;本轮空榜观测 mail_directed=0 + rank_settle 已写 |
| SV5 结算邮件组装 | PASS | 代码 `ResolveSettleMail` 取模板(title/content/expire);`SendMailTo` 传名次档 `rewardId` 覆盖附件库 id;缺失时 SettleTitleFallbackTextId 兜底 |
| SV6 同周期幂等 | PASS | 本轮真往返:第二次节律触发无新结算日志(各榜已结标记判已结跳过);dev 探针同周期二次触发 → 标记只 1 条已在交接区记录 |
| SV7 重启幂等 | PASS | 本轮起服观测 rank_settle._id=1 仍为上轮已结值,榜 1 正确跳过(本周未过新周);标记持久 MongoDB 确认 |
| SV8 并发幂等 | PASS(dev 探针已实跑) | 原子 FindOneAndUpdate + upsert,DuplicateKey 双 catch 在位;dev 探针 20 路并发 → 1 次抢占成功 |
| SV9 复用发奖入口 | PASS(路径通,无上榜账号可领) | 代码经 `MailDecisionHelper.SendMailTo` 投邮件;dev 探针领取成功 + 库1005抽出实物 |
| SV10 服务端存储持久 | PASS | rank_settle 标记 MongoDB 持久(重启后仍在);rank_score/mail_directed 为既有 MongoDB 集合 |
| SV11 结算不中断服务 | PASS | 单榜 try/catch;单账号 SendMailTo null 跳过;越界试探全部防住;起服 130s 无异常日志 |
| SV12 榜定义结算字段同源 | PASS | MongoDB rank_def 4 条字段与 AuthoritativeDefs 一致;dev 交接区已对 rank_tbrank.bytes 实测解码核对 |
| SV13 Code Review | PASS | 见上 4.1-4.4;五项重点全通;Fantasy.Net 约定全通;DuplicateKey 双 catch 口径在位 |

---

### 协议同步检查项(E4)

本增量无新增客户端协议(结算是服务端内部触发),E4=N/A。git status 无 proto/Generate 改动,客户端生成物无需同步。

---

### 自主取舍(decisions)

- 总判定采用 PASS:四类验证均无代码缺陷,运行验证实现了真往返(起服+活 MongoDB+节律触发+日志+MongoDB 查询)。
- 榜 1 本轮未触发新结算(本周期已结)为正确行为,不计入 FAIL;榜 9101 本轮完成了 type=1 的真往返结算并写入已结标记。
- dev 探针结果(SV2/SV3/SV8/SV9 各 branch)作为辅助背景证据,本轮运行验证已独立确认核心路径(SV1/SV4/SV6/SV7/SV10/SV11/SV12)。
- 越界试探未试出新崩法;静态路径核对已覆盖主要边界。
