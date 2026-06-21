# 状态:服务端开发(server-dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-server-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:Tier 4 活动系统 · 服务端段 · 第 2 子单(EVENT 头像解锁通路 · server 段)— 交接 server-test

设计基线 `design-docs/40-event-unlock-relay.md` + `pipeline/state/plan.md`(plan 已锁:沿 [Tier 4 第 1 子单 PASS 基线](Fantasy de5d4da7)加 1 个 EVENT 解锁活动配置 + 1 个 EVENT 礼包配置,共 4 张已有表加行 / 服务端工程现状取「AuthoritativeDefs 加行 + GiftPoolSeeds 加行」两处增量)。

### 一、改动摘要(做了什么、为何、关键决策)

**实质交付 = 2 处既有代码外科手术式追加 + 2 个静态数组各 +1 条目 + 1 处 Log.Info 文案微调**,共 2 个文件改动、新增 0 文件、改既有代码 ≈ 25 行(含注释)。

**关键决策(沿用 plan 已锁 + 工程现状定的实现选择,plan §「server-dev 须自行据工程现状定的实现选择」+ Tier 4 第 1 子单 PASS 时已建的「服务端工程无 Luban → AuthoritativeDefs 范式」基线)**:

- **配置加载范式 = 沿 Tier 4 第 1 子单 PASS 基线(`ActivityServiceComponentSystem.AuthoritativeDefs` 静态权威表 + ReconcileDefs upsert 进 `activity_def`)**:plan 描述的「`__enums__.xlsx` 加 EVENT=5 档 + `item.xlsx` 加道具 + `giftrandom.xlsx` 加礼包 + `activity.xlsx` 加活动」是「客户端 Luban 表」描述;**服务端工程无 Luban 集成**(server-dev memory 第一条 + 32/33/39 注释三处明示),只能按 redeem/rank/mail/活动 第 1 子单已建范式播种 MongoDB,plan §「server-dev 须自行据工程现状定的实现选择」明示此自决面已开放。**`__enums__.xlsx` UseEffect=5 EVENT 档 / `item.xlsx` 30101 道具行 / `giftrandom.xlsx` 6101 礼包行**是客户端段下一刀的责任(UseEffect 解析 + 道具适配器路径),服务端只需:① 在 `activity_def` 注册 EVENT 解锁活动 `activity_id=2`(`AuthoritativeDefs` 加 1 条)+ ② 在 `gift_pool` 注册 EVENT 礼包 `Index=6101`(`MailServiceComponentSystem.GiftPoolSeeds` 加 1 条,同 33 rank 1005/1006 验证奖池注册同范式)。**服务端只守「道具 id × 数量」抵达邮件附件**,不解析 UseEffect 自身(沿 32 §3.4 抽奖语义)。
- **EVENT 礼包配置写在 `MailServiceComponentSystem.GiftPoolSeeds` 数组**(非另起独立组件):理由 = `gift_pool` 是邮件 / 排行榜 / 活动**共享**的奖励礼包注册表(32 §3.4 旁注 + `MailServiceComponentSystem` 第 35-77 行类注释明示),已收纳 33 排行榜结算的 1005/1006 名次档验证奖池;EVENT 礼包 6101 直接进 `GiftPoolSeeds` 静态数组与既有架构正交,不违 plan §守不变量「32 SendMailTo 签名零改 + 既有 32/33 流程零代码改」(零签名改 / 零 schema 改 / 零 mail 流程改,只在共享注册表里加 1 个 Index=6101 条目;`SeedGiftPool` 既有 catch-DuplicateKey 仅插入幂等不覆盖运营)。
- **EVENT 活动 `Cycle=3 OneShot`**(沿 plan §3.5):达标后 `LastClaimedCycleKey=1` 永远 ≥ 1,既有 `ActivityEvalHelper.ComputeCurrentCycleKey` 已实做 `CycleOneShot → return 1L` 分支(第 1 子单 PASS 时已就绪),本子单不动一行代码即可。
- **EVENT 活动 `Type=1 Login`**(沿 plan §3.5):每次登录 +1 触发,既有 `ActivityEvalHelper.OnLogin` 遍历 `def.Type != TypeLogin` 过滤分支自然吃下新 EVENT 活动;`Target=7` 经 `EvaluateAndClaim` 的「`counter < target` → return」自动门控前 6 次登录不发奖、第 7 次达标。**helper 代码 0 行改动**。
- **EVENT 礼包 `Rate=100` 单项必中**(沿 plan §3.4):既有 `MailDecisionHelper.WeightedPick` 按 Rate 权重抽,单项 100 → 必中 30101 × 1;邮件领取响应自然带 `[(itemId=30101, count=1)]`,客户端段下一刀解析 UseEffect=5 EVENT。
- **`GiftPoolEntryDoc.AutoId = 6101001`**(沿 33 rank 1005001/1006001 命名范式):`{Index}` × 1000 + 顺序 = AutoId,主键唯一不冲突;首次插入成功、重启 `SeedGiftPool` 撞 DuplicateKey 跳过(既有播种语义)。
- **EVENT 活动 textId 占位 = 390003 / 390004**(沿 plan §3.5 + 40 §3.5):验收期 textId 多语言未填实,客户端展示空文本不影响 server-test 真往返核(看 mail_directed 集合 + 抽奖落地)。
- **沿 LoginGameHandler 既有 OnLogin 挂钩点不改**:Tier 4 第 1 子单 PASS 时已挂在 `SendInitSnapshotTo` 之后,新增的 EVENT 活动自动被 `ActivityEvalHelper.OnLogin` 遍历到。**LoginGameHandler / OnCreateSceneEvent / ActivityEvalHelper / ActivityDocs 零改动**。
- **EVENT 活动配置缓存初始化日志文案更新**:第 1 子单的「(本子单仅含每日登录奖 activity_id=1)」描述已不准确(本子单加了 activity_id=2),按 conventions §6「过时正文重写或删除」改写为「(activity_id=1 每日登录奖 + activity_id=2 EVENT 头像解锁活动)」。

### 二、文件清单

**修改(2 个外科手术式追加,无新文件)**:

- `examples/Server/APP/Hotfix/Game Examples/Gate/Activity/ActivityServiceComponentSystem.cs`:`AuthoritativeDefs` 静态数组追加 1 条 `ActivityId=2 EVENT 解锁活动`(`Type=1 Login, Cycle=3 OneShot, Target=7, Reward=6101, SenderTextId=110700, TitleTextId/ContentTextId/NameTextId/DescTextId=390003/390004, ExpireDays=14, StartAtMs/EndAtMs=0`);类注释 + AuthoritativeDefs 文档注释同步更新描述本子单交付;`Log.Info` 文案修改(activity_id=1 + activity_id=2 两条说明)。
- `examples/Server/APP/Hotfix/Game Examples/Gate/Mail/MailServiceComponentSystem.cs`:`GiftPoolSeeds` 静态数组追加 1 条 `AutoId=6101001, Index=6101, ItemId=30101, Num=1, Rate=100`(EVENT 礼包单项必中);追加注释说明本子单 Tier 4 第 2 子单引入 + 客户端 luban 30101 道具行的责任分工。

**协议生成物**:无 — 本子单**零新增协议**(plan + 40 §一立项明示「无客户端 RPC 改」)。`OuterMessage.cs` / `OuterEnum.cs` / `OuterOpcode.cs` 三个生成文件零变化;`UnityProject/Assets/Fantasy/Generate/NetworkProtocol/*` 零变化。

**未触(本子单不改)**:
- 既有 39 第 1 子单代码(`ActivityEvalHelper.cs` / `ActivityDocs.cs` / `ActivityServiceComponent.cs` / `C2G_LoginGameRequestHandler.cs` / `OnCreateSceneEvent.cs`)全部零 diff
- 既有六全栈(30 redeem / 31 rank / 32 mail / 33 rank-settle / 35 account / 37 player-attr)业务代码全部零 diff(本次只在 `MailServiceComponentSystem.GiftPoolSeeds` 静态数组追加,非签名 / 集合 schema / handler 流程改动)
- 既有 39 第 1 子单 `activity_id=1` 每日登录奖配置完全不变
- Unity 工程业务代码 `Assets/GameScripts/HotFix/` 零 diff(CV1)
- `Assets/Fantasy/Scripts/` 本子单零相关 diff(`FantasyNetworkConfig.cs M` 是上次任务的预先存在 carry-forward,本会话未触)
- `AvatarUnlockService.cs` 零改(D7 + plan §「客户端段下一刀工作量」)
- `Fantasy.config` 零改(carry-forward,本会话未触)
- MongoDB `activity_def` / `activity_progress` / `gift_pool` 三个集合 schema 全部零字段加(只增量加新行)

### 三、协议同步状态(server-test 协议同步检查项,E4)

**本子单零新增协议 → E4 = 不适用 / 无需同步**:服务端无新 proto、无新 outer / inner message;客户端 `Assets/Fantasy/Generate/NetworkProtocol/` 自然零 diff。EVENT 头像玩家可观测路径 = 邮箱拉邮件(走既有 32 客户端段拉邮件 / 领奖链),客户端段下一刀只需要在 [16 §3.7](#16-item-system::useeffect) UseEffect 解析层加 EVENT=5 分支 + 1 个适配器调 GrantUnlock,不动 proto。

### 四、验证点(逐条对应 40 §八 SV / CV / E + plan)

**已在本环节静态核实(读代码 + 跑编译 + 起服冒烟验组件初始化日志 + MongoDB 探针验集合)**:

| SV# | 完成定义(摘要) | 现状 |
| --- | --- | --- |
| SV1 | `__enums__.xlsx` 加 EVENT=5 档 | **服务端段 N/A**:服务端工程无 Luban 集成(memory 第一条);UseEffect=5 枚举是客户端 Luban 表的事,客户端段下一刀加。**SV1 服务端段判 N/A,核对客户端段下一刀的 luban 改动而非本子单 server 段。** |
| SV2 | `item.xlsx` EVENT 道具行(id=30101) | **服务端段 N/A**:同 SV1;客户端段下一刀加 30101 道具行(`UseEffect=5, 效果目标=3, 自动使用=1`);服务端只守「ItemId=30101 × 1 抵达邮件附件」(SV5 已验)。 |
| SV3 | `giftrandom.xlsx` EVENT 礼包行(所属礼包=6101) | **PASS**:服务端段 `gift_pool` 集合 Index=6101 已落地(MongoDB 探针实测 + 起服日志 礼包库奖池数=4 即 6001/1005/1006/6101);客户端段下一刀 luban 同步 6101 礼包配置即可。 |
| SV4 | `activity.xlsx` EVENT 解锁活动行(activity_id=2) | **PASS**:服务端 `activity_def` 集合 _id=2 已落地(MongoDB 探针 BSON 完整字段):`{ _id:2, Type:1 Login, Cycle:3 OneShot, Target:7, Reward:6101, SenderTextId:110700, TitleTextId/NameTextId=390003, ContentTextId/DescTextId=390004, ExpireDays:14, StartAtMs/EndAtMs:0 }`;字段值与 40 §3.5 + plan §核心档完全一致。客户端段下一刀 luban 同步 activity_id=2 即可。 |
| SV5 | `AuthoritativeDefs` 加载 EVENT 行 | **PASS**:起服冒烟日志 `ActivityServiceComponent 初始化完成,活动配置缓存条目数=2`(从第 1 子单的 1 涨到 2,新加 EVENT 活动入缓存)+ `MailServiceComponent 礼包库奖池数=4`(从 3 涨到 4,新加 EVENT 礼包入缓存);MongoDB 探针实测两条新记录均已 upsert/insert 落库。 |
| SV6 | EVENT 活动经 39 §3.4 发奖编排 | 架构已就位:既有 `ActivityEvalHelper.OnLogin` 遍历 `def.Type == TypeLogin` 自动吃下 EVENT 活动;Login 类 `EvaluateAndClaim`「`counter < target=7` → return」自动门控前 6 次不发奖、第 7 次抢占周期键 + 投活动邮件(`SenderTextId=110700, TitleTextId=390003, ContentTextId=390004, ExpireDays=14, RewardId=6101`)。**真往返核**留 server-test 跑 7 次 `C2G_LoginGameRequest`。 |
| SV7 | OneShot 永发幂等 | `Cycle=3 OneShot` → 既有 `ComputeCurrentCycleKey(CycleOneShot, nowMs) → return 1L`;第 7 次抢占成功 → LastClaimedCycleKey=1;第 8 / 9 / ... 次 `lastKey=1 ≥ 1 = false` 跳过不重发(`EvaluateAndClaim` line 184)。**架构已就位,留 server-test 跑 8 次以上登录验**。 |
| SV8 | 真往返(力争) | 见 §四「留 server-test 跑真往返复核」清单。**本环节静态证据**:起服 + MongoDB 探针确认 `activity_def._id=2 + gift_pool.Index=6101` 同时落库;OnLogin 钩子 + EvaluateAndClaim 已含 `Type=1 Login + Cycle=3 OneShot + Target=7 + Reward=6101` 自然路径,无需新代码。 |
| SV9 | 不破 39 第 1 子单 PASS | **PASS**:`activity_def._id=1` 每日登录奖配置完整不变(MongoDB 探针确认 _id=1 行字段不变);既有 `activity_progress` 已发记录(`sv_test_account_001_1, Counter=2, LastClaimedCycleKey=1781913601000`)保留 = 跨重启幂等;`ActivityEvalHelper.cs` / `ActivityDocs.cs` / LoginGameHandler / OnCreateSceneEvent 全部零 diff。 |
| SV10 | 配置层引用一致性 | **PASS**:`activity_def._id=2.Reward=6101 → gift_pool.Index=6101 (1 条) → ItemId=30101 → (客户端段) item.xlsx id=30101 → UseEffect=5 EVENT, 效果目标=3 → (客户端段) 18 头像表 id=3 avt_star`;链路上服务端段两节点已实落地(MongoDB 探针实测),后两节点客户端段下一刀对齐。 |
| SV11 | 既有六全栈 + 35/36/37/38 + 39 零回归 | **PASS**:30 redeem / 31 rank / 32 mail / 33 rank-settle / 35 account / 37 player-attr / 36 / 38 业务代码零 untouched;`accounts / players / mail_template / mail_directed / mail_record / gift_pool / rank_score / rank_def / rank_settle / redeem_records` 集合 schema 零改;起服日志显示 30/31/32/33/35/37 + 活动 6 个组件全部初始化成功(`MailServiceComponent 初始化完成,运营模板缓存条目数=8,礼包库奖池数=4`)。 |
| SV12 | Code Review | 见下 §五 Code Review 重点 |
| CV1 | 客户端工程零 diff | `git status` 显示 `Assets/GameScripts/HotFix/` 零改动;`Assets/Fantasy/Generate/NetworkProtocol/` 零改动(本子单零新协议);`Assets/Fantasy/Scripts/` 仅 `FantasyNetworkConfig.cs M` 是上次任务 carry-forward,本会话未触。**PASS**。 |
| E1-E3 | 联调 | 真往返需 server-test 跑客户端 RPC 7 次 `C2G_LoginGameRequest` 验整链路(SV6 + SV7);**E3 客户端段未实做 UseEffect=5 EVENT 解析致整条链不通**列 **BLOCKED 非 FAIL**(本子单 server 段固有切片状态,沿 plan §BLOCKED 边界 + 40 §八)。 |

**真往返证据(本环节探针采集,2026-06-21 01:30)**:

```
=== activity_def ===
activity_def count = 2
{ "_id" : 1, "ContentTextId" : 110733, "Cycle" : 1, "DescTextId" : 110731, "EndAtMs" : 0, "ExpireDays" : 14, "NameTextId" : 110730, "Reward" : 1005, "SenderTextId" : 110700, "StartAtMs" : 0, "Target" : 1, "TitleTextId" : 110732, "Type" : 1 }
{ "_id" : 2, "ContentTextId" : 390004, "Cycle" : 3, "DescTextId" : 390004, "EndAtMs" : 0, "ExpireDays" : 14, "NameTextId" : 390003, "Reward" : 6101, "SenderTextId" : 110700, "StartAtMs" : 0, "Target" : 7, "TitleTextId" : 390003, "Type" : 1 }

=== gift_pool (EVENT 6101 + 既有 Index) ===
gift_pool total count = 7
Index=6101 EVENT 礼包 entries = 1
{ "_id" : 6101001, "Index" : 6101, "ItemId" : 30101, "Num" : 1, "Rate" : 100 }
全部 Index = [1005, 1006, 6001, 6101]

=== activity_progress (Tier 4 第 1 子单遗留,本子单零改) ===
activity_progress count = 1
{ "_id" : "sv_test_account_001_1", "Account" : "sv_test_account_001", "ActivityId" : 1, "Counter" : 2, "LastClaimedCycleKey" : 1781913601000, "LastUpdatedAt" : 1781972896094, "Version" : 1 }

=== mail_directed (TitleTextId=390003 EVENT-sourced) ===
mail_directed total count = 2
EVENT activity mails (TitleTextId=390003) count = 0  (本子单冒烟无登录触发,符合预期)
```

**起服日志关键片段**(冒烟 PASS):

```
ActivityServiceComponent 初始化完成,活动配置缓存条目数=2(activity_id=1 每日登录奖 + activity_id=2 EVENT 头像解锁活动)。
MailServiceComponent 初始化完成,运营模板缓存条目数=8,礼包库奖池数=4
```

**留 server-test 跑真往返复核(MongoDB 可达则做,不可达列 BLOCKED-env 非 FAIL)**:

- **SV6 主验(EVENT 通路真往返)**:server-test 用 .NET 探针起客户端 RPC 走 `C2G_LoginGameRequest` 7 次(同一 accountName),期望:
  - 前 6 次:`activity_progress` 集合 `{account}_2` 文档 Counter 从 1 → 6,LastClaimedCycleKey=0(未发);`mail_directed` 无新增 TitleTextId=390003 邮件
  - 第 7 次:Counter=7,LastClaimedCycleKey=1(OneShot 抢占成功);`mail_directed` 多 1 封 TitleTextId=390003、SenderTextId=110700、RewardId=6101 的 EVENT 活动邮件
- **SV7 主验(OneShot 永发幂等)**:第 7 次后再连续登录 5 次 → `activity_progress` Counter 继续累加到 12(OnLogin 每次都 Increment),但 LastClaimedCycleKey 恒=1 不变;`mail_directed` 不新增第二封 EVENT 邮件
- **SV8 主验(端到端,客户端段 32 已 PASS)**:第 7 次登录后,客户端 32 拉邮件列表 → 见 TitleTextId=390003 的 EVENT 邮件;领取 → 服务端按 RewardId=6101 抽 → 单项必中 → 响应「成功 · 已发奖」+ 奖励列表 `[(itemId=30101, count=1)]`
- **SV10 配置引用一致性(可在探针读)**:`activity_def._id=2.Reward` 应 =6101;`gift_pool` 按 `Index=6101` 查应 ≥ 1 条且 `ItemId=30101`;链断则 server-test 标配置错(40 §五 崩法表「EVENT 头像 id 不存在」)
- **SV11 零回归(并发 EVENT 活动 1+2)**:同账号同日同时登录,期望:`activity_id=1` 每日登录奖 + `activity_id=2` EVENT 活动同时被 OnLogin 遍历;activity 1 Daily 当日重发幂等 + activity 2 OneShot 永发幂等,两条独立周期键互不干扰

**过异常路径(已防,交 test 复核)**:

- 本子单**零新代码** = 异常路径全部沿 39 第 1 子单已建防护(空 / null accountName 静默跳过 / ActivityServiceComponent 未挂返 null / MongoDB 不可达静默跳过 / Increment 并发首次 +1 撞 _id 双 catch 重试 / TryClaimCycleKey 并发抢占 MatchedCount > 0 判一次成功 / 发邮件中途崩 = 漏发窄窗运营可补 / SendMailTo 返 null Log.Warning 漏发标记 / 单活动异常 try/catch 不中断其他活动)。本子单**新加的 EVENT 活动经同一防护路径**,无新攻击面。

### 五、Code Review 重点(SV12,交 test 核)

逐条对应 plan §SV12 + 40 §SV12 + 39 §SV12:

- **① 无 32 SendMailTo 签名改**:`grep -rn "SendMailTo" examples/Server/APP/` → 签名 `(self, account, senderTextId, titleTextId, contentTextId, expireDays, rewardId)` 零字符改;本子单 EVENT 活动经 `ActivityEvalHelper.EvaluateAndClaim` 复用同一 SendMailTo 入参形态(不需要新增参数)。
- **② 无 39 ActivityDef schema 字段加**:`ActivityDocs.cs` 文件本会话零 diff;`ActivityDefDoc` 字段集 = `{ActivityId, NameTextId, DescTextId, Type, Cycle, Target, Reward, SenderTextId, TitleTextId, ContentTextId, ExpireDays, StartAtMs, EndAtMs}`,本子单不加 `EventUnlockId` / `EventTargetType` 等任何字段(plan §安全默认采纳 D2 红线 + 简报方案 A 被否)。
- **③ 无新 handler / 集合 / 协议**:本子单仅在 2 个既有静态数组追加条目;0 个新文件;`OuterMessage.cs` / `OuterEnum.cs` / `OuterOpcode.cs` 三个生成产物零变化(可 git diff 核)。
- **④ EVENT 通路完全沿 39 §3.4 发奖编排流程**:`grep -rn "ActivityEvalHelper" examples/Server/APP/` → 零新调用点;EVENT 活动经既有 `OnLogin` 遍历 + 既有 `EvaluateAndClaim` 判达标 + 抢占 + 发邮件流程;LoginGameHandler.Run 末尾的 `ActivityEvalHelper.OnLogin(activitySvc, mailSvc, accountName, TimeHelper.Now).Coroutine()` 一行未改。
- **⑤ 16 道具表 + 18 头像表零改**(服务端段):服务端工程无 16/18 表加载(无 Luban);客户端段下一刀 luban 同步 30101 道具行 + 引用 18 头像表 id=3 avt_star 占位 — 服务端段验收 N/A,核客户端段下一刀。
- **⑥ EVENT 道具进背包 vs 立即结算**(服务端段 N/A):服务端只负责把 ItemId=30101 × 1 抵达邮件附件,「自动使用=1 立即结算」是客户端 16 §3.7 UseEffect 解析层 + 适配器的事;服务端段 SV12 仅核「ItemId 抵达邮件」(SV6 探针)。
- **⑦ EVENT 头像 id 类型(头像 vs 框)分流**(服务端段 N/A):服务端只守 ItemId=30101 + Num=1,头像 vs 框由客户端段下一刀适配器查 18 头像表「类型」字段决定;服务端不参与。
- **⑧ 跨设备同 UUID 双登第 7 次并发**:沿 39 §3.4 既有 `TryClaimCycleKey` 原子条件写 + MatchedCount > 0 判一次成功;EVENT 活动 OneShot 抢占 LastClaimedCycleKey 从 0 → 1,并发两端只一端写入成功 + 发邮件,另一端跳过,不双发(架构已就位,本子单无新代码)。
- **⑨ MongoDB 连接配置沿用既有**:`MailServiceComponentSystem.Init` + `ActivityServiceComponentSystem.Init` 用 `self.Scene.World.Database` 取数据库实例;本子单未新增连接串、未改 `Fantasy.config`。
- **⑩ 外科手术式改动**(沿 plan + fantasy-net guideline §3):
  - `ActivityServiceComponentSystem.AuthoritativeDefs` 既有 1 条 `activity_id=1` 一字符未改;只在数组末尾追加 1 条 `activity_id=2`;Log.Info 文案微调(过时描述重写,沿 conventions §6)
  - `MailServiceComponentSystem.GiftPoolSeeds` 既有 6 条(6001 × 4 + 1005001 + 1006001)一字符未改;只在数组末尾追加 1 条 `6101001`

### 六、决策记录(decisions)

- **配置加载路径走「AuthoritativeDefs + GiftPoolSeeds 加行」非 Luban**:plan §3.1 描述「4 张 Luban 表加行」是客户端 Luban 表的视角(客户端段下一刀的责任);服务端工程无 Luban,沿 Tier 4 第 1 子单 PASS 时已建的「服务端权威表」范式,**本子单本质 = 2 处既有静态数组各 +1 条**(activity_def 加行 + gift_pool 加行)。决策不阻塞、不入 blockers(plan §「server-dev 须自行据工程现状定的实现选择」开放此自决面;同 Tier 4 第 1 子单 PASS 已有先例)。
- **EVENT 礼包配置写在 `MailServiceComponentSystem.GiftPoolSeeds` 数组**:理由 = gift_pool 是邮件 / 排行榜 / 活动**共享**奖励礼包注册表(32 §3.4 注释明示),既有架构正交,不违 plan §守不变量「32 SendMailTo 签名零改 + 既有 32/33 流程零代码改」;同 33 rank 1005/1006 验证奖池注册同范式。
- **EVENT 活动 SenderTextId=110700 + TitleTextId/NameTextId=390003 + ContentTextId/DescTextId=390004**:占位符复用,server-test 看 `mail_directed` 集合 TitleTextId=390003 即可识别 EVENT-sourced 邮件;运营后续配多语言时改本声明 + 重启 `ReconcileDefs` 自动 upsert 写入。
- **`GiftPoolEntryDoc.AutoId = 6101001`**:沿 33 rank 1005001/1006001 命名范式(`{Index}` × 1000 + 顺序),主键唯一不冲突;首次插入成功、重启 SeedGiftPool 撞 DuplicateKey 跳过(既有既有播种语义)。
- **EVENT 活动 cycle=3 OneShot + Target=7**:沿 plan §3.5 自治默认(累计登录 7 次永久解锁限定头像,符合「事件限定」语义);既有 `ActivityEvalHelper.ComputeCurrentCycleKey(CycleOneShot)` + `EvaluateAndClaim` 自然路径吃下,helper 代码 0 行改。

### 七、阻塞 / 风险(交接 test 时 boss 需知)

- **无阻塞**:编译 0 error 0 新增 warning;冒烟测试组件初始化日志正常(活动缓存条目数=2 + 礼包库奖池数=4);MongoDB 探针确认 `activity_def._id=2` + `gift_pool.Index=6101` 同时落库(SV3/SV4/SV5 PASS);**不依赖未落地的客户端段**(本子单零客户端业务代码 diff)。
- **风险 1(test 端跑服真往返)**:本机 mongod 当前在跑(PID 8044,LISTEN 127.0.0.1:27017,本会话探测确认 + 探针实测连通);test 端 `Test-NetConnection 127.0.0.1 27017` 探测,不可达判 BLOCKED-env 非 FAIL。
- **风险 2(Main 进程跑服)**:Main.exe 本会话冒烟后已 kill(test 端可直接 `cd D:/work/TEngine_block/Fantasy && dotnet run --project examples/Server/APP/Main/Main.csproj -c Debug --framework net9.0 -- --m Develop` 起服;`--framework net9.0` 必带,memory 已记)。
- **风险 3(E3 客户端段未实做整链不通)**:沿 40 §八 + plan §BLOCKED 边界,本子单 server 段固有切片状态;test 验「server 段配置层接通(SV1-SV12 server 视角)」,不验「头像真正解锁」(留客户端段下一刀);E3 显式标 BLOCKED 非 FAIL。
- **风险 4(临时探针文件残留)**:本会话用 `C:\Users\pc\AppData\Local\Temp\activity-probe` 做了 MongoDB 探针验证;只读残留无影响,操作系统会自动清。
- **风险 5(server-test 跑前 mongod 数据状态)**:本会话冒烟时 `activity_progress` 已有 Tier 4 第 1 子单遗留的 `sv_test_account_001_1` 已发记录(Counter=2, LastClaimedCycleKey=1781913601000);test 端若要从干净状态验 EVENT 通路需先 `db.activity_progress.deleteMany({})` 清,或用全新 accountName 触发(推荐后者避免污染既有 Tier 4 第 1 子单基线)。

### 八、自检(conventions §「收尾必做」)

- [x] 过程性内容不在正文(本交接区无 diff 叙事 / 无「按你说的改成」/ 无对话痕迹;关键决策与文件清单全是事实陈述)
- [x] 正文无可推导事实的副本(文件清单是 git 可推导但「行为级完成定义 ↔ 现状」映射表需现场推导才能匹配 SV 与代码,留)
- [x] 正文无拟人 / 口语比喻(grep 「死/打死/收口/钉死/焊死/绑死」 0 命中)
- [x] 工作态内容可识别所属任务(本 state 文件头部明标「Tier 4 第 2 子单 EVENT 头像解锁通路 server 段」)
- [x] 被改动规范的旁注仍成立(本子单无改既有稿,无孤儿旁注)
- [x] 过时正文已重写或删除(本 state 覆盖式重写为本子单状态;Tier 4 第 1 子单交接区由 boss 关单事务归档)
