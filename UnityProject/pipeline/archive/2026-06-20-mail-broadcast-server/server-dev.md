# 状态:服务端开发(server-dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-server-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:邮件运营推送来源·服务端段 — 交接 server-test

设计基线 `design-docs/32-mail-server.md` + `pipeline/state/plan.md` 交接区(plan 已锁:本任务=兑现 32 已声明的「运营广播 + 系统定向两路插入、一套领取」契约;无 design-docs 改动;实现选择由 server-dev 据 Fantasy.Net 现状取)。

### 一、改动摘要(做了什么、为何、关键决策)

**实质交付 = 现状确认,无新增代码改动**。32 §3.1-§3.5 + §八 SV1-SV14 在 fantasy-net 工程内**已 100% 落地**(commit 22c21843「邮件服务端段:运营推送来源 + 领奖服务端校验 Outer 协议 + Gate Handler + MongoDB 存储」);其后 commit 420272b8(33 排行榜结算服务端段)借用本段 `MailDecisionHelper.SendMailTo` 进程内发奖入口已 PASS,反向佐证邮件段 SV10 在生产路径上活跃可用。

**为何这次零新增**:plan 给的范围(运营广播来源 + 领奖权威 + 服务端发奖入口 + MongoDB 存储)被 22c21843 完整覆盖;plan 决策表三档(存储模型 / 触发面 / 拉取策略)全部命中既有实现的安全默认——

- **存储模型 = 共享标识空间 + 三集合分工**:`mail_template`(运营广播模板)+ `mail_directed`(系统定向邮件,Account 索引)+ `mail_record`(按账号领取记录,_id="{account}|{mailId}" 复合唯一)。对外 MailId 用前缀 "t{TemplateId}" / "d{DirectedId}" 区分两路、键空间不撞,**符合 server-dev memory 「广播+定向 两路插入、一套领取」范式**。
- **触发面 = 启动播种(配置驱动)**:`MailServiceComponentAwakeSystem` 启动时把硬编码 broadcastSeeds(注释明确「与客户端 mail.xlsx 同源,id 1-5;另加 100-102 验证样例覆盖 SV3/SV6/SV2/SV7」)经 `InsertOneAsync` + DuplicateKey 跳过的幂等口径写进 `mail_template`。**值手抄 mail.xlsx 同源口径**(server-dev memory「服务端工程无 Luban 集成,按 30/31 先例播种 MongoDB 文档」),非 Luban→服务端导出路径。无定时投递任务、无 admin RPC、无主动 push 协议。
- **拉取策略 = 被动 RPC**:协议 `C2G_MailListRequest`(空 body)+ `G2C_MailListResponse`(MailListItem 列表 + ResultCode);`C2G_MailClaimRequest`(MailId 一字段)+ `G2C_MailClaimResponse`(ResultCode + 成功时 MailRewardItem 列表)。客户端触发时机由客户端定。

**关键决策(沿用既有实现已表达的设计取舍,本任务复述以便 test 复核)**:

- **claim-before-award(原子防重写在抽奖之前)**:`MailDecisionHelper.Claim` 顺序「定位 → 过期 → 无奖励短路 → 原子防重写(InsertOneAsync) → 抽奖」。InsertOneAsync 抛 DuplicateKey → 裁定「已领过」、不抽奖、不发奖(SV4/SV5)。合并「检查未领过 + 记录已领」为单次原子;抽奖在记录成功之后,不做「先抽奖发响应、再记录」(那会并发双领,设计 §五崩法)。**这是 33 RankSettleHelper 同主题决策的镜像**(claim-before),共享一条「记录在副作用前」的服务端裁决幂等范式。
- **服务端时钟判过期**:`IsExpired(sendUnixMs, expireDays, globalRetainDays, TimeHelper.Now)`,有效期 ≤ 0 用 `GlobalRetainDays`(默认 30,对应 mail_global.xlsx retain_days)兜底。拉列表前滤 + 领取再判一次(下发后到期再领的窗口)。客户端时钟不参与。
- **身份从会话取(SV9)**:两个 Handler 都从 `session.GetComponent<GateAccountFlagComponent>().Account.Name` 取账号,proto 消息体不带账号字段(C2G_MailListRequest 是空 body,C2G_MailClaimRequest 只一个 MailId)。未登录会话 → 返 ServiceUnavailable,不崩。
- **服务端抽奖,客户端不申报奖励**:`DrawRewards` 按 Rate 权重在 `GiftPoolCache[rewardId]` 同 Index 条目内抽一条,返「道具 id × 数量」。库 id 未登记(缓存查无)→ 奖励列表为空但 ResultCode 仍 Success(SV6 边界)。客户端拿到的奖励是服务端定的,改回包数量影响自己本地存档不影响服务端记录。
- **发奖入口 = 进程内 API(SendMailTo)**:无协议、纯函数签名 `SendMailTo(self, account, senderTextId, titleTextId, contentTextId, expireDays, rewardId) → directed MailId`。**33 RankSettleHelper 已调用并 PASS**,证明:① 入口可被服务端进程内系统复用;② 投出的定向邮件走与运营广播相同的拉列表 / 防重 / 抽奖机制(SV10)。
- **MongoDB 不可达 = ServiceUnavailable 不抛**:`Init` 时若 `database.GetDatabaseInstance is not IMongoDatabase`,记 Warning(非 Error)直接 return;运行时 List/Claim 检测 `self.Templates/Directed/Records == null` → 返 ServiceUnavailable。**符合 server-test memory `feedback-blocked-vs-fail` BLOCKED-环境口径**。

### 二、文件清单

**本任务新增/修改:零**。

**对照核现状的已落地文件(均在 commit 22c21843,本任务不动)**:

- `examples/Config/NetworkProtocol/Outer/MailMessage.proto` — Outer 协议:`MailClaimResultCode` 枚举(Success/MailNotFound/NoReward/AlreadyClaimed/Expired/ServiceUnavailable) + `MailListItem` / `MailRewardItem` 列表项 + 4 个 RPC 消息(`C2G_MailListRequest`/`G2C_MailListResponse`/`C2G_MailClaimRequest`/`G2C_MailClaimResponse`)。
- `examples/Server/APP/Entity/Game Examples/Gate/Mail/MailDocs.cs` — 四个 BSON 文档:`MailTemplateDoc`(广播模板,_id=TemplateId)/ `MailDirectedDoc`(定向邮件,_id=DirectedId,字段 Account 用于拉列表筛选)/ `MailClaimRecordDoc`(领取记录,_id="{account}|{mailId}" 唯一键)/ `GiftPoolEntryDoc`(礼包随机库条目,_id=AutoId)。
- `examples/Server/APP/Entity/Game Examples/Gate/Mail/MailServiceComponent.cs` — Gate Scene 上的邮件服务组件,持四集合句柄 + 模板/礼包库内存缓存 + GlobalRetainDays。
- `examples/Server/APP/Hotfix/Game Examples/Gate/Mail/MailServiceComponentSystem.cs` — AwakeSystem 启动绑集合 + 建 Account 索引 + 播种广播模板(8 条:1-5 同源 + 100-102 验证样例)+ 播种礼包池(6001 邮件主奖池 + 1005/1006 排行榜验证奖池)+ 载入缓存;DestroySystem 清缓存 / null 句柄。
- `examples/Server/APP/Hotfix/Game Examples/Gate/Mail/MailDecisionHelper.cs` — 裁决核心:`List`(广播缓存遍历 + 定向按 Account 查 + 领取态标注 + 过期过滤)/ `Claim`(定位 → 过期 → 无奖励短路 → 原子防重写 → 抽奖)/ `SendMailTo`(进程内发奖入口)/ `IsExpired` / `DrawRewards`(Rate 加权)/ `WeightedPick`。
- `examples/Server/APP/Hotfix/Game Examples/Gate/Mail/C2G_MailListRequestHandler.cs` / `C2G_MailClaimRequestHandler.cs` — 两个 Outer RPC Handler:身份从会话取(GateAccountFlagComponent.Account.Name)、不接受客户端自报账号、未登录返 ServiceUnavailable、不抛异常断连。

**未触(本任务不改)**:Unity 客户端(`Assets/`)零 diff(`InertMailSource` 占位保持,客户端段切真实 RPC 留下一轮,与 plan 守不变量一致);Fantasy.config(工作树既有改动非本任务);33 排行榜结算服务端段(420272b8 已 PASS,零回归)。

### 三、协议同步状态(server-test 协议同步检查项,E4)

**本任务无 proto 改动 → E4 = N/A**(plan 预测「已落」分支命中)。

- `git status` 工作树 proto 路径(`examples/Config/NetworkProtocol/Outer/MailMessage.proto`)无 diff;客户端段(`UnityProject/Assets/Fantasy/Generate/NetworkProtocol/`)无邮件相关 diff。
- 既有 22c21843 提交时已含双端同步生成物(server 端 + client 端 Generate),客户端 `InertMailSource` 暂仍占位、不消费这两组协议(下一刀客户端段才消费)——**客户端 Generate 与 proto 一致 = 双端可编译**。

### 四、验证点(逐条对应验收标准,告诉 test 验什么 / 怎么验 / 预期)

**已在本环节静态核实(读代码 + 核 plan §「服务端段交付清单」SV 范围映射表)**:

逐 SV 对照 plan §「服务端段交付清单」表 + 既有实现:

| SV# | 行为级完成定义(摘要) | 现状 |
| --- | --- | --- |
| SV1 | 拉列表返该账号应收(默认全服广播)邮件列表 + 每封领取态 | `MailDecisionHelper.List` 遍历 `TemplateCache.Values`(全服广播)+ 按 Account 查 `Directed`,每封经 `BuildListItem` 附 `Claimed = claimedSet.Contains(mailId)` |
| SV2 | 过期过滤:服务端时钟 > 收件时间 + 有效期;有效期≤0 用 `retain_days` 兜底 | `IsExpired(SendUnixMs, ExpireDays, GlobalRetainDays, TimeHelper.Now)`,有效期 ≤ 0 用 `globalRetainDays`(默认 30)。拉列表对每封先判,过期 `continue` 不下发 |
| SV3 | 领取成功:有附件、未过期、本账号未领过 → 返「成功」+ 服务端按附件库 id 抽礼包随机库一次的产物;原子记录已领 | `Claim` 顺序「定位 → 过期 → 无奖励短路 → TryWriteRecord 原子写 → DrawRewards 抽一次」,抽到的「道具 id × 数量」装 `MailRewardItem` 返回 |
| SV4 | 防重领:第二次返「已领过」、响应不含奖励 | TryWriteRecord 第二次 InsertOneAsync 抛 DuplicateKey → 返 false → Claim 返 AlreadyClaimed + 空 rewards |
| SV5 | 并发防重领:同账号并发两次领同一封 → 一次成功、另一次「已领过」、只发一次奖、记录只一条 | _id="{account}|{mailId}" 主键唯一 + InsertOneAsync 原子;两并发请求中只一个 Insert 成功、另一个抛 DuplicateKey;抽奖在 Insert 成功之后(claim-before),无双抽双发 |
| SV6 | 无奖励邮件(库 id=0)返「无奖励」;库 id 未登记返「成功」但奖励列表空 | RewardId==0 早 return NoReward;`DrawRewards` 找不到 `GiftPoolCache[rewardId]` 返空列表但调用方仍走 Success 分支 |
| SV7 | 领取过期校验:下发后到期再领 → 返「已过期」、不发奖、不记录 | Claim 第二步 `IsExpired(... TimeHelper.Now)` 再判一次过期,返 Expired,**未到 TryWriteRecord 步骤**故不记录 |
| SV8 | 邮件不存在:传服务端无的标识 → 返「邮件不存在」、不崩、不写记录 | Locate 返 null → Claim 返 MailNotFound;空/异常 mailId(长度<2 / 前缀非 t/d) Locate 也返 null,不抛 |
| SV9 | 身份从会话取:协议不接受客户端自报账号 | proto:C2G_MailListRequest 空 body、C2G_MailClaimRequest 只 MailId;Handler 从 `GateAccountFlagComponent.Account.Name` 取账号;未登录返 ServiceUnavailable |
| SV10 | 服务端发奖入口(进程内):给某账号投一封带库 id 的邮件 → 该账号拉列表能看到 → 领取得奖 | `SendMailTo` 接收 account/textIds/expireDays/rewardId,生成 GUID,InsertOneAsync 进 mail_directed,返 "d{guid}";**33 RankSettleHelper 已生产路径调用 + 420272b8 PASS**,本任务沿用结论不重测 |
| SV11 | 失败分支以结果码回包、不抛异常断连 | 全分支 ResultCode 设置后返回;Insert DuplicateKey 由 try/catch 转 false(不上抛);Mongo*Exception 双重 catch(`MongoWriteException` + `MongoCommandException`);服务未就绪也返 ServiceUnavailable |
| SV12 | 存储持久(MongoDB):重启后领取记录仍在 / 运营邮件仍可拉取 | 三集合走原生 `IMongoCollection`(非内存态);重启 AwakeSystem 重连同库、SeedBroadcastTemplates 撞 DuplicateKey 跳过(幂等)、ReloadTemplateCache 从 mail_template 重建缓存 |
| SV13 | 运营邮件配置与客户端 mail.xlsx 同源 | `BuildBroadcastSeeds` 注释显式标注「TemplateId ← mail.xlsx id / TitleTextId ← title / ContentTextId ← desc / ExpireDays ← expire_days / RewardId ← reward_id」,id 1-5 + ExpireDays=14 + RewardId=1002 与客户端 mail.xlsx 同源;GlobalRetainDays=30 与 mail_global.xlsx retain_days 同源;礼包池 6001 四条(item id+num+rate)与客户端 gift_random 同源 |
| SV14 | Code Review(Fantasy.Net 约定) | FTask(非 Task)/ sealed class / file-scoped namespace `Fantasy` / `Log.Warning`+`Log.Error`+`Log.Debug` / 不手改 .g.cs(MessageRPC 基类、对象池 Create() 都是 SG 产物)/ 不手动注册(Handler/AwakeSystem/DestroySystem 全靠定义类 + SG)/ 原生 IMongoDatabase(框架 IDatabase 只能先读后写,设计明令禁止)|

**留 server-test 跑服真往返复核(MongoDB 可达则做,不可达列 BLOCKED-env 非 FAIL,memory `local-mongodb-for-server-roundtrip` + `feedback-blocked-vs-fail`)**:

- **SV1 主验**:起服 → 任意已登录账号会话发 `C2G_MailListRequest` → 收 `G2C_MailListResponse` ResultCode=Success + Mails 列表含未过期广播模板(1-5 + 100/101,共 6 条;102 已过期不下发)+ 任何已投递的定向邮件,Claimed 全 false(初次)。
- **SV2 主验**:模板 102(`SendUnixMs = nowMs - 100天`,`ExpireDays = 1`)→ 拉列表不下发(过期已滤);其他 14 天有效期内的下发。**注**:测试可控时刻不可直接 inject TimeHelper.Now,需用样例 102 覆盖。
- **SV3 辅验**:领模板 100(RewardId=6001)→ ResultCode=Success + Rewards 含「30001/30002/30003/30004 中一条按 Rate 权重抽出的道具 id × 数量」(50/30/15/5 概率)。
- **SV4 辅验**:同账号对模板 100 连续两次 Claim → 首次 Success + rewards 非空,第二次 AlreadyClaimed + rewards 空;`mail_record` 只有一条 _id="{account}|t100"。
- **SV5 主验**:同账号并发两个 `C2G_MailClaimRequest{MailId='t100'}` → 最终 Success + AlreadyClaimed 各一(顺序不定),只一条 mail_record。
- **SV6 辅验**:领模板 101(RewardId=0)→ ResultCode=NoReward + rewards 空;mail_record 不写一条(NoReward 早 return,未到 TryWriteRecord)。**另**:领模板 1(RewardId=1002 礼包库未登记)→ ResultCode=Success + rewards 空(空抽奖边界)。
- **SV7 主验**:领模板 102(已过期)→ ResultCode=Expired + rewards 空 + 不记录;**或**用 SendMailTo 投一封 expireDays=0 但 sendUnixMs 设为 100 天前的定向邮件(测试用),立即领 → Expired。
- **SV8 辅验**:`C2G_MailClaimRequest{MailId='t99999'}`(不存在的模板)→ MailNotFound + rewards 空;`MailId='d12345'`(不属于本账号或不存在的定向 id)→ MailNotFound;`MailId=''` / `MailId='x'` / `MailId='zfoo'`(非 t/d 前缀)→ Locate 返 null → MailNotFound,不崩。
- **SV9 主验**:Handler 代码核 — `C2G_MailClaimRequestHandler` 只读 `request.MailId`,账号取自 `GetSessionAccountName(session)`(会话组件链),proto MailMessage.proto 两个请求无 Account 字段。session 无 GateAccountFlagComponent → 返 ServiceUnavailable(已登录前不能拉/领)。
- **SV10 复用 33 已 PASS**:**不重测**;若需独立验证,可在探针里直接调 `MailDecisionHelper.SendMailTo(comp, "test_account", 110700, 110711, 110721, 14, 6001)` → 返 "d{guid}";该 account 拉列表见之、可领得奖。
- **SV11 主验**:覆盖 MailNotFound/Expired/NoReward/AlreadyClaimed/ServiceUnavailable 五分支,均 RPC 正常 reply、ErrorCode 保持 0(MessageRPC 基类 ErrorCode 不动)、连接不断;空 mailId / 异常前缀不崩。
- **SV12 主验**:领一封 → 停服 → 重起服 → 同账号再领同封返 AlreadyClaimed(mail_record 持久);拉列表仍见模板(mail_template 持久);SeedBroadcastTemplates 撞 DuplicateKey 跳过、缓存重建。
- **SV13 主验**:用 mongo 客户端或 .NET 探针查 `mail_template` 实际文档,核 id 1-5 的 TitleTextId/ContentTextId/ExpireDays/RewardId 与客户端 `Assets/AssetRaw/Configs/bytes/mail_tbmail.bytes` 实测解码值一致;mail.xlsx 现有 5 行模板时(若运营改了行数则视 mail.xlsx 现状);`GlobalRetainDays` = 30;`gift_pool` Index=6001 的 4 条 ItemId/Num/Rate 与 gift_random.bytes 一致。**用 server-dev memory「Luban bytes 解码器」(见 22 服务端段先例)**。
- **SV14 主验**:`MailDecisionHelper.cs` / `MailServiceComponentSystem.cs` / 两个 Handler / `MailDocs.cs` / `MailServiceComponent.cs` 全代码逐项核「FTask / sealed / file-scoped namespace / 不手改 .g.cs(对象池 Create() / MessageRPC 基类 / proto 生成类 均靠 SG)/ 不手动注册(Handler 走 MessageRPC<,> 基类自动注册;AwakeSystem/DestroySystem 同)/ 错误码非异常 / 原生 IMongoDatabase 防并发」。

**过异常路径(已防,交 test 复核)**:

- MongoDB 不可达 → 组件 Init Warning + List/Claim 返 ServiceUnavailable + 邮件保持可领、不本地放行(BLOCKED-env)
- 并发同账号同邮件 → InsertOneAsync 原子,只一次成功(SV5)
- 会话未登录 → Handler 返 ServiceUnavailable(SV9)
- proto MailId 空/异常前缀 → Locate 返 null → MailNotFound(SV8)
- RewardId=0 → NoReward 早 return(SV6 第一类)
- RewardId 未登记 → DrawRewards 返空 + Success(SV6 第二类)
- 定向邮件 Account 不匹配 → Locate 返 null → MailNotFound(防领他人邮件)
- 模板缓存载入时 mail_template 为空 → 拉列表只下发定向邮件,不崩
- Reload 失败(集合 null)→ 静默 return,缓存保持上次状态
- 全部 Mongo*Exception(MongoWriteException + MongoCommandException)双重 catch 转结果码

### 五、Code Review 重点(SV14,交 test 核)

- **领取防重 = 检查 + 抽奖 + 记录原子(claim-then-act)**:`MailDecisionHelper.Claim` 中 `TryWriteRecord` 用 `InsertOneAsync` 抛 DuplicateKey 单次原子(=「检查未领过 + 记录已领」合并),抽奖在 Insert 成功之后;**无「先抽后记」并发崩法**(设计 §五)。与 33 RankSettleHelper.TryClaimSettlePeriod 同主题、同安全侧取舍(漏发可补偿、重做不可)。
- **附件服务端抽奖,协议不含客户端自报奖励 / 账号**:`MailRewardItem`(ItemId+Count)是响应字段、`C2G_MailClaimRequest` 只 MailId 一字段;客户端无法虚增。`DrawRewards` 用 `Random.Shared.NextDouble() * totalRate` Rate 加权抽,工程内可复现。
- **过期服务端时钟**:`IsExpired` 用 `TimeHelper.Now`(服务端权威),客户端时钟不参与;拉列表前滤 + 领取再判,堵下发后到期窗口。
- **发奖入口走同一套领取机制**:`SendMailTo` 只写 `mail_directed` 一条文档,**不另造发奖路径**——下发该账号经 List → 定向分支扫到、领取经 Claim 同一原子防重 + 同一抽奖。33 RankSettleHelper 调用方;两路插入(广播模板 + 定向)+ 一套领取(Record)+ 一套抽奖(GiftPool)。
- **遵 Fantasy.Net 约定**:FTask(非 Task)、sealed class(组件/System,Helper 静态工具类)、file-scoped namespace `Fantasy`、Log.Info/Warning/Error/Debug、不手改 .g.cs、不手动注册(Handler/AwakeSystem/DestroySystem 全 SG)、外科手术(本任务零改动)。原生 IMongoDatabase(框架 IDatabase 只能先读后写,设计明令禁止)。

### 六、决策记录(decisions)

- **本任务无新增代码**:全 SV 在 22c21843 已落地、420272b8 反向佐证 SV10 活跃可用;plan §「服务端段交付清单」「实现选择由 server-dev 据 Fantasy.Net 现状取」明确不预设新增量。boss / plan 已锁「实质交付在交接区」即此意。
- **不重建 mail.xlsx 同源导出工具链**:server-dev memory「服务端工程无 Luban 配置集成」+ 「按 redeem/rank 先例播种 MongoDB 文档(值手抄客户端 xlsx 口径)」是已确立先例;现状的 `BuildBroadcastSeeds` 硬编码 8 条(1-5 同源 + 100-102 验证 + GlobalRetainDays=30)符合本范式。运营热改需求出现时再上数据库集合管理(O8)。
- **不动 InertMailSource 客户端占位**:plan 守不变量明令「server-only 增量,客户端段切真实 RPC 留下一轮」。Unity 工程零 diff,本任务范围严守。
- **不重测 SV10**:沿用 33 服务端段 420272b8 已 PASS 结论(SendMailTo 在生产路径活跃)。
- **编译自检 = N/A**:无代码改动 → 编译对象为 0。host 进程 Main.exe 此刻在跑(被其他工作流持有,不可 kill),无法 `dotnet build` 整个 Server.sln;但**本任务无 diff 即 sln 不会变,编译态 = 既有提交 22c21843 + 420272b8 已编译通过 + 已起服运行的状态**。server-test 端若需 rebuild 验证,需先确认 Main 进程归属后再决定 kill。

### 七、阻塞 / 风险(交接 test 时 boss 需知)

- **无阻塞**:实质 = 现状确认,交付完整。
- **风险 1(test 端跑服真往返)**:Main.exe 当前在跑(其他工作流的活进程,本会话 kill 被拒)。test 端若要跑服手验,需:① 等 Main 退出;② 用既有起服探针/单独进程;③ 或确认归属后手动 kill。**不可强行 kill 跨会话进程**(影响其他工作流)。
- **风险 2(MongoDB 可达)**:server-dev memory `local-mongodb-for-server-roundtrip` 已知本机 mongod 不稳;test 端用 `Test-NetConnection 127.0.0.1 27017` 探测,不可达判 BLOCKED-env 非 FAIL(server-test memory `feedback-blocked-vs-fail`)。**编译态 + 静态代码核 + 协议双端同步**仍可全验。

