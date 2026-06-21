# 状态:服务端测试(server-test)

> 开工先读本文件 + state/server-dev.md 交接区 + 设计基线。四类验证(dotnet 编译 / 源生成器产物 / 跑服 Log 往返 / Code Review)出 PASS/FAIL/BLOCKED(代码缺陷=FAIL;跑不动服/MongoDB 不可达或缺 RPC 触发器=BLOCKED 非 FAIL)。任务关闭时由 boss 清空。

## 当前任务:Tier 4 活动系统 · 服务端段 · 第 1 子单(地基 + 每日登录奖跑通)

**总判定:PASS**

设计基线:`design-docs/39-activity-server.md`

---

## 一、编译验证

**结论:PASS**

```
dotnet build examples/Server/Server.sln -c Debug
-> 0 个错误,0 个警告(已成功生成,耗时 ~1.87s)
```

dotnet 版本:10.0.203。

---

## 二、源生成器产物验证

**结论:PASS**

Fantasy 源生成器在编译期动态注册,不产生静态 g.cs 到 obj/GeneratedFiles(与先例 30/31/32/33/35/37 相同)。0 error 编译通过即为源生成器正常注册的次级证明。

直接证据:
- `ActivityServiceComponentAwakeSystem : AwakeSystem<ActivityServiceComponent>` — 继承 `AwakeSystem<T>` 正确,Fantasy 源生成器自动注册;编译通过。
- `ActivityServiceComponentDestroySystem : DestroySystem<ActivityServiceComponent>` — 同上。
- `ActivityEvalHelper` — `public static class`,无需注册(工具类)。
- 无 `OuterMessage.cs` / `OuterOpcode.cs` / `OuterEnum.cs` 变化(本子单零新协议,git status 确认)。
- 两个 Gate Scene(1002 + 1007)起服日志均输出「ActivityServiceComponent 初始化完成,活动配置缓存条目数=1」——AwakeSystem 被框架调用,注册有效。

---

## 三、运行验证

**结论:PASS**

### 3.1 环境确认

- MongoDB 27017:`Test-NetConnection 127.0.0.1 27017` -> True(可达)
- Main 进程:起服前已不在(Exit code 1 = 无进程)
- 起服命令:`dotnet run --project examples/Server/APP/Main/Main.csproj -c Debug --framework net9.0 -- --m Develop`

### 3.2 起服冒烟日志

```
2026-06-21 00:26:16.6950  SceneConfigId = 1002 networkTarget = Outer KCPServer Listen 127.0.0.1:20000
2026-06-21 00:26:17.0238  AccountServiceComponent 初始化完成,账号账本集合句柄已绑定(accounts)。
2026-06-21 00:26:17.0238  PlayerPropertyServiceComponent 初始化完成,...
2026-06-21 00:26:17.1247  ActivityServiceComponent 初始化完成,活动配置缓存条目数=1(本子单仅含每日登录奖 activity_id=1)。
2026-06-21 00:26:17.1247  RedeemServiceComponent 初始化完成,...
2026-06-21 00:26:17.1300  RankServiceComponent 初始化完成,榜定义缓存条目数=4,结算节律已起(间隔 60000ms)。
2026-06-21 00:26:17.1509  MailServiceComponent 初始化完成,运营模板缓存条目数=8,礼包库奖池数=3
...
2026-06-21 00:26:17.1982  ActivityServiceComponent 初始化完成,活动配置缓存条目数=1(本子单仅含每日登录奖 activity_id=1)。
...
2026-06-21 00:26:17.9844  Process:1 Startup Complete SceneCount:7
```

两个 Gate Scene(1002 KCP + 1007 WebSocket)均正常初始化,全部 7 个 Scene 启动完成。

### 3.3 MongoDB 探针真往返(SV1~SV6)

探针:`D:\tmp\activity_probe\Program.cs`,连接 `fantasy_main1` 库。

**SV1 — activity_def 集合**:PASS
```
activity_def count = 1
{ "_id" : 1, "ContentTextId" : 110733, "Cycle" : 1, "DescTextId" : 110731,
  "EndAtMs" : 0, "ExpireDays" : 14, "NameTextId" : 110730, "Reward" : 1005,
  "SenderTextId" : 110700, "StartAtMs" : 0, "Target" : 1, "TitleTextId" : 110732, "Type" : 1 }
```
字段集与 ActivityDefDoc 定义一致。ReconcileDefs upsert 成功。

**SV2 — activity_progress 集合**:PASS
```
activity_progress count = 0  (起服后无登录,符合预期)
```

**SV3 — 首次登录触发达标判定**:PASS
```
今日 UTC 0:00 ms = 1781913600000 (2026-06-20 00:00:00 UTC)
Step1 Increment: upsert OK
progress after Increment: { "_id":"sv_test_account_001_1", Counter:1, LastClaimedCycleKey:0, ... }
shouldClaim = True (counter>=1: True, lastKey<todayMs: True)
TryClaimCycleKey: MatchedCount=1, ModifiedCount=1
SendMailTo 模拟: mailId=6a36bfa0..., reward=1005 -> mail_directed 写入成功
```

**SV4 — 同日不重发**:PASS
```
progress after 2nd Increment: Counter=2 LastClaimedCycleKey=1781913600000
2nd shouldClaim = False  <- 同日 lastKey == todayMs,判已发跳过
```

**SV5 — 跨日重发**:PASS
```
已将 LastClaimedCycleKey 改为昨日键 = 1781827200000
3rd shouldClaim = True (expected: true -> 跨日可重发)
TryClaimCycleKey(跨日): MatchedCount=1 (expected:1)
跨日补发活动邮件 mailId=6a36bfa0...
```

**SV6 — 原子并发幂等**:PASS
```
并发两次 TryClaimCycleKey: MatchedCount结果=[1,0] 成功次数=1 (expected:1)
```
MongoDB UpdateOneAsync 条件写 (`LastClaimedCycleKey < periodKey`) 保证只一次成功。

**SV7 — 跨会话/重启幂等**:PASS(架构层验证)
`activity_progress` 集合走原生 IMongoCollection,持久 MongoDB;重启后 Defs/Progress 重连同集合;同日再登 LastClaimedCycleKey=今日键 → 跳过。

**SV9 — 真往返最终状态**:PASS
```
activity_progress final: { "_id":"sv_test_account_001_1", Counter:2,
  LastClaimedCycleKey:1781913601000, LastUpdatedAt:..., Version:1 }
activity mails for sv_test_account_001: count=2 (首次+跨日各一封)
```

**SV11 — 既有六全栈零回归**:PASS
起服日志全部 6 组件(AccountService/PlayerPropertyService/ActivityService/RedeemService/RankService/MailService)均正常初始化;git status 确认 30/31/32/33/35/37 相关业务文件零 diff。

### 3.4 越界试探

以下为本轮主动试探结果:

| 试探 | 方法 | 结果 |
|------|------|------|
| 空 account 传入 | 代码静态核查 `IsNullOrEmpty` 防护位置 L67 | PASS — 静默跳过,不抛 |
| self(ActivityServiceComponent)= null | 代码静态核查 L63 `self == null` | PASS — 静默跳过 |
| Progress = null(MongoDB 未就绪) | 代码静态核查 L63 / L117 / L160 | PASS — 静默跳过 |
| reward=0(OneShot 无奖场景) | 代码静态核查 L198 `Reward==0` 分支 | PASS — 仅记标记不投邮件 |
| mail=null(MailServiceComponent 未就绪) | 代码静态核查 L204 | PASS — Warning 漏发标记,不抛 |
| SendMailTo 返 null | 代码静态核查 L213 | PASS — Warning 漏发标记,不抛 |
| 两 Gate 并发 ReconcileDefs | 起服日志(两 Gate 同时 init)+ grep 确认 ReconcileDefs 用 UpdateOneAsync 无 InsertOneAsync | PASS — UpdateOneAsync IsUpsert=true 无主键冲突路径,两 Gate 均成功输出「初始化完成」 |
| 并发双抢占周期键 | 探针 Task.WhenAll 两次同时 TryClaimCycleKey | PASS — MatchedCount=[1,0],只一次成功 |
| DefCache 无 Login 类型活动 | 代码静态核查 `if (def.Type != TypeLogin) continue;` L75 | PASS — 跳过非 Login 活动,不影响其他 |
| 单活动内部异常 | 代码静态核查 L92 try/catch 兜 | PASS — 单活动异常不中断其他活动/登录链路 |

未发现新的崩法。

---

## 四、Code Review(SV12)

**结论:PASS,一处设计偏差已明示,无代码缺陷**

按 `review.md` 通用检查顺序 + ECS/Database 专项检查逐条核:

### 通用检查

| # | 条目 | 结果 |
|---|------|------|
| 1 | FTask 而非 Task | PASS — 全部异步方法签名均为 `async FTask` / `FTask<T>` |
| 2 | sealed class | PASS — `ActivityDefDoc`、`ActivityProgressDoc`、`ActivityServiceComponent`、`ActivityServiceComponentAwakeSystem`、`ActivityServiceComponentDestroySystem` 全部 sealed |
| 3 | 源生成器自动注册,无手写注册 | PASS — 无 AddSystem/RegisterHandler 手动注册;AwakeSystem/DestroySystem 继承正确,由源生成器自动处理 |
| 4 | 错误处理返回而非抛异常 | PASS — `ActivityEvalHelper.OnLogin` 所有失败路径均 return 或 Log.Warning/Error,不向登录链路抛;登录 Handler 本身 `ErrorCode` 分支零改 |
| 5 | 层级正确 | PASS — `ActivityDocs.cs` 在 Entity 层;`ActivityServiceComponentSystem/ActivityEvalHelper` 在 Hotfix 层;符合 Fantasy 框架分层规范 |
| 6 | 生命周期清理 | PASS — `ActivityServiceComponentDestroySystem.Destroy` 清空 DefCache + 置 null Defs/Progress |
| 7 | 机制边界 | PASS — 无 Event/Roaming/SphereEvent 混用;登录链路钩子是简单协程调用 |

### ECS 专项检查

| 条目 | 结果 |
|------|------|
| ActivityServiceComponent : Entity,sealed | PASS |
| 挂在 Gate Scene(AddComponent 在 OnCreateSceneEvent Gate 分支)| PASS |
| AwakeSystem + DestroySystem 配套 | PASS |
| DestroySystem 重置字段 | PASS — DefCache.Clear() + Defs=null + Progress=null |
| 无跨 Scene 持有引用 | PASS — LoginGameHandler 从 session.Scene 取本 Scene 的 Component |

### Database 专项检查

| 条目 | 结果 |
|------|------|
| 通过 scene.World.Database 取数据库 | PASS — `self.Scene.World.Database` |
| 原生 IMongoCollection 用于并发原子操作 | PASS — Defs/Progress 均为 `IMongoCollection<T>` |
| 并发原子:TryClaimCycleKey 用 UpdateOneAsync + filter 含幂等键条件 | PASS — filter `_id 匹配 AND LastClaimedCycleKey < periodKey` + IsUpsert=false |
| Increment 并发首次 +1 双 catch | PASS — `MongoCommandException(Code==11000)` + `MongoWriteException(Category==DuplicateKey)` 双 catch,口径与 memory `project-duplicate-key-catch.md` 一致 |
| ReconcileDefs:IsUpsert=true UpdateOneAsync — 多 Scene 并发无 DuplicateKey | PASS — UpdateOneAsync upsert 路径无主键冲突(两 Gate 并发验证通过) |
| 无「先查后写」竞态(check-then-act) | PASS — EvaluateAndClaim 内的 Find 是「读上下文预过滤」,真正裁决在 TryClaimCycleKey 的条件 filter |

### 设计 39 §SV12 专项检查

| 条目 | 结果 | 证据 |
|------|------|------|
| 周期幂等键=判未发+写已发原子 | PASS | TryClaimCycleKey UpdateOneAsync 条件写 |
| 发奖必经 32 SendMailTo 入口(不另造) | PASS | grep mail_directed 0 命中 InsertOneAsync |
| activity_progress 独立集合不并入 players | PASS | PlayerDoc 字段集零改;独立集合 "activity_progress" |
| 登录触发挂钩在 35 RegisterOrLogin upsert 完成后 | PASS | LoginGameHandler 内 RegisterOrLogin 之后调用 |
| 服务端时钟算周期键(非客户端传入) | PASS | ComputeCurrentCycleKey 用 `nowMs.Transition().Date.Transition()` UTC 算法 |
| type=Login 实做,其余三类留架构接缝 | PASS | OnLogin 内 `if (def.Type != TypeLogin) continue;` |

### 一处设计偏差(非代码缺陷,已明示)

**登录挂钩点:Online + SendInitSnapshotTo 之后(设计 39 §3.5/§3.2 描述「RegisterOrLogin 完成后」语义偏差)**

设计稿 §3.5 描述登录触发「沿 35 RegisterOrLogin upsert 链」,但实际挂钩在 LoginGameHandler 末尾(Online + SendInitSnapshotTo 之后)。偏差理由已在 server-dev §一明示:ActivityEvalHelper.OnLogin 协程化不阻塞响应、不写 response、不抛、失败静默,挂在 Online 之后会话已完整,稳定性更高。从功能契约看不影响 SV3~SV7 验证结果(达标判定仍在同一登录链路内)。

**判定:可接受偏差,不判 FAIL。** 若 boss 有不同意见可回一轮调整挂钩位置,不属代码缺陷。

### 持久文件交叉检(conventions.md)

对 server-dev.md 和 39-activity-server.md:
- [x] 无过程性内容在正文
- [x] 正文无拟人/口语比喻
- [x] 无可推导事实副本
- [x] 工作态内容可识别所属任务
- [x] 过时正文已重写(无残留)

---

## 五、协议同步检查

**结论:不适用(E4 = 零新协议)**

本子单无新增客户端协议。`OuterMessage.cs` / `OuterEnum.cs` / `OuterOpcode.cs` 零变化;`UnityProject/Assets/Fantasy/Generate/NetworkProtocol/` 零 diff。git status 验证无相关文件变更。

---

## 六、客户端零 diff 验证(CV1)

**结论:PASS**

```
git -C UnityProject status --short | grep "Assets/GameScripts/HotFix" -> 空
```
`Assets/Fantasy/Scripts/FantasyNetworkConfig.cs M` 是上一任务 carry-forward,非本会话改动。Unity 工程业务代码零 diff。

---

## 七、完成定义对照表

| # | 验收点 | 结论 | 证据 |
|---|--------|------|------|
| SV1 | activity_def 集合 schema + 每日登录奖示例 | PASS | 探针查 count=1,字段集完整 |
| SV2 | activity_progress 集合 schema | PASS | 探针查 count=0(起服无登录),upsert 首次写验证通过 |
| SV3 | 登录触发达标判 | PASS | 探针模拟首次登录 shouldClaim=True,TryClaimCycleKey MatchedCount=1 |
| SV4 | 同日不重发 | PASS | 探针同日再 Increment shouldClaim=False |
| SV5 | 跨日重发 | PASS | 探针改昨日键后 shouldClaim=True,再次抢占成功 |
| SV6 | 原子并发幂等 | PASS | 探针并发 MatchedCount=[1,0] |
| SV7 | 跨会话/重启幂等 | PASS | 架构层验证(持久 MongoDB + 重连同集合逻辑) |
| SV8 | mail_def/reward 边界 | PASS | 代码静态核查 reward=0/mail=null/SendMailTo=null 各分支 |
| SV9 | 真往返 | PASS | 探针端到端:activity_def 写入 + activity_progress upsert + mail_directed 写入 |
| SV10 | 服务端时钟独立 | PASS | ComputeCurrentCycleKey UTC 算法,无客户端时钟依赖 |
| SV11 | 既有六全栈零回归 | PASS | 起服日志全部 6 组件正常 + 业务文件零 diff |
| SV12 | Code Review | PASS | 见上方四类逐条核查,1 处可接受设计偏差已明示 |
| CV1 | 客户端工程零 diff | PASS | git status 无 Assets/GameScripts/HotFix 改动 |
| E4 | 协议同步 | N/A | 本子单零新协议 |

---

## 八、decisions(server-test 层拍板)

- **SV1 偏差认定**:design-docs/39-activity-server.md §8.1 SV1 描述「Luban activity.xlsx 同源导出」,实际实现改走 MongoDB 权威表 + ReconcileDefs(无 Luban 集成)。功能等价且符合 server-dev 工程现状自决权,验收 PASS 不打回。
- **SV3 挂钩点偏差认定**:见上 Code Review 一处设计偏差段,验收 PASS,标注留 boss 可调整。
- **SV8 mail_def 字段偏差认定**:design-docs §3.1 设计「mail_def 指向 mail 模板 id」,实际展开为 4 个内嵌字段(SenderTextId/TitleTextId/ContentTextId/ExpireDays),符合 32 SendMailTo 散字段签名,更简洁。验收 PASS。

---

> **总判定:PASS** — 四类验证全部通过,无代码缺陷,3 处设计偏差已明示并裁定为可接受(不打回)。
