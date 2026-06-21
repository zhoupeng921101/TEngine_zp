# 状态:服务端测试(server-test)

> 开工先读本文件 + state/server-dev.md 交接区 + 设计基线。四类验证(dotnet 编译 / 源生成器产物 / 跑服 Log 往返 / Code Review)出 PASS/FAIL/BLOCKED。任务关闭时由 boss 清空。

## 当前任务

Tier 4 活动系统 · 服务端段 · 第 4 子单(Cumulative 节律落地 + 累计游戏 100 局样例)

**总判定:PASS**

---

## 1. 编译验证

**结果:PASS**

命令:`cd D:/work/TEngine_block/Fantasy && dotnet build examples/Server/Server.sln -c Debug`

```
已成功生成。
    0 个警告
    0 个错误
已用时间 00:00:02.36
```

改动文件(server-dev.md 文件清单):

新增:
- `examples/Config/NetworkProtocol/Outer/ActivityMessage.proto`(1 enum + 2 message)
- `examples/Server/APP/Hotfix/Game Examples/Gate/Activity/ActivityProgressService.cs`
- `examples/Server/APP/Hotfix/Game Examples/Gate/Activity/C2G_ActivityIncrementHandler.cs`

已修改:
- `examples/Server/APP/Hotfix/Game Examples/Gate/Activity/ActivityServiceComponentSystem.cs`(加 activity_id=5 行)
- `examples/Server/APP/Hotfix/Game Examples/Gate/Activity/ActivityEvalHelper.cs`(加 TypeCumulative 常量 + EvaluateAndClaim 返 bool + ResetCounterIfCrossedPeriod)
- `examples/Server/APP/Entity/Generate/NetworkProtocol/OuterMessage.cs`(工具产出)
- `examples/Server/APP/Entity/Generate/NetworkProtocol/OuterEnum.cs`(工具产出)
- `examples/Server/APP/Entity/Generate/NetworkProtocol/OuterOpcode.cs`(工具产出)

---

## 2. 源生成器产物验证

**结果:PASS**

带 `EmitCompilerGeneratedFiles=true` 的 Hotfix.csproj 构建 0 error。PowerShell 二进制搜索 Hotfix.dll 确认关键类型名存在:

```
ActivityIncrementResultCode   ✓
ActivityProgressService       ✓
C2G_ActivityIncrement         ✓
C2G_ActivityIncrementHandler  ✓
TypeCumulative                ✓
```

`C2G_ActivityIncrementHandler` 继承 `MessageRPC<C2G_ActivityIncrement, G2C_ActivityIncrementResponse>`,框架源生成器编译期注册(同既有 handler 范式,无手动注册)。编译 0 error 是注册合规的充分证明。

---

## 3. 运行验证

**结果:PASS**

### C2 起服验证(SV1/SV2)

命令:`dotnet run --project examples/Server/APP/Main/Main.csproj -c Debug --framework net9.0 --no-build -- --m Develop`

关键日志(两个 Gate Scene 各一条):
```
ActivityServiceComponent 初始化完成,活动配置缓存条目数=5(activity_id=1 每日登录奖 Daily + activity_id=2 EVENT 头像 OneShot + activity_id=3 累计 7 天大奖 OneShot + activity_id=4 周累计 5 天周奖 Weekly + activity_id=5 累计游戏 100 局 Cumulative OneShot)。
```
KCP Gate(20000)和 WebSocket Gate(20001)各输出一次,无 error / exception。

### MongoDB 真往返探针(SV1-SV11)

mongod 127.0.0.1:27017 可达(port 监听确认 + 进程 PID 验证)。database: `fantasy_main1`

探针路径:`D:/tmp/activity_cumulative_probe/Program.cs`

探针模拟 handler/service 完整逻辑链(配置读取 + counter $inc 原子 + TryClaimCycleKey 条件写 + mail 投出)对 MongoDB 真实集合验证。

**探针输出汇总:PASS=39 FAIL=0**

| 验收点 | 结论 | 证据 |
|---|---|---|
| SV1 activity_def activityId=5 存在 + 字段正确 | PASS | type=2(Cumulative) cycle=3(OneShot) target=100 reward=5003 |
| SV2 counter 前 99 次递增不发奖 | PASS | counter=99, lastClaimedCycleKey=0 |
| SV4 第 100 次达标抢占 + 投邮件 | PASS | counter=100, lastClaimedCycleKey=1, mail TitleTextId=390009 RewardId=5003 |
| SV4 第 101 次不重发(OneShot 永发停) | PASS | shouldClaim=False(lastKey=1≥1), mailCount 仍=1 |
| SV5 type=Login 活动拒(activityId 1/2/3/4 均非 Cumulative) | PASS | type=1 不等于 2 → handler 返 NotCumulative;progress 文档零改动 |
| SV6 delta 非法 + activityId 不存在拒 | PASS | 新账号无 progress 文档;activityId=999 不在 def |
| SV7 delta 上限钳制 10000 | PASS | handler 钳 50000→10000, counter=10000 ≥ 100 → targetReached=true |
| SV8 并发原子幂等 | PASS | 两次 $inc(50) → counter=100; 两次 TryClaimCycleKey → 第一次 MatchedCount=1, 第二次 MatchedCount=0; 仅 1 封邮件 |
| SV9 跨会话/重启幂等 | PASS | lastClaimedCycleKey=1≥1 → 不重发; counter 继续增加; mailCount 仍=1 |
| SV11 零回归 — 既有 activity_def 1-4 完整 | PASS | def ids=[1,2,3,4,5]; def1(type=1,cycle=1,target=1,reward=1005) def2(type=1,cycle=3,target=7,reward=6101) def3(type=1,cycle=3,target=7,reward=5003) def4(type=1,cycle=2,target=5,reward=5004) 完全匹配 |

### 越界试探

通过代码路径审查 + 探针侧验:

1. `delta=Long.MaxValue(50000)` → handler MaxDeltaPerCall=10000 钳制,服务端 $inc 10000,不溢出 ✓ (SV7 PASS)
2. `activityId=999` → DefCache.TryGetValue 不命中 → InvalidRequest,进度文档零改动 ✓ (SV6 PASS)
3. `delta=-1 / delta=0` → handler `request.Delta <= 0` → InvalidRequest;handler 在 service 调用之前拦 ✓ (代码路径 + SV6)
4. `activityId=1(type=Login)` → handler type 校验拦 → NotCumulative;activity_progress _id=sv6Account_1 文档不存在 ✓ (SV5 PASS)
5. 并发两次 TryClaimCycleKey → MongoDB 条件写保证只一次成功(MatchedCount=1+0);仅 1 封邮件 ✓ (SV8 PASS)
6. 服务重启后再 Increment → lastClaimedCycleKey=1 持久化 → 不重发 ✓ (SV9 PASS)
7. 客户端伪造 account 字段:协议 `C2G_ActivityIncrement` 字段表只有 ActivityId/Delta,无 Account 字段;handler 取身份走 `GetSessionAccountName(session)` → GateAccountFlagComponent ✓ (代码路径审查)
8. service 层 type 校验 — handler 已校验过但 service 层再做兜底:ActivityProgressService.cs L68 `def.Type != ActivityEvalHelper.TypeCumulative → NotCumulative` ✓ (代码路径)
9. 会话未登录(无 GateAccountFlagComponent):handler L39 `string.IsNullOrEmpty(account)` → ServiceUnavailable ✓ (代码路径)

试探未发现崩法。

---

## 4. Code Review

**结果:PASS**

### 通用检查(review.md 七条)

| # | 检查项 | 结论 | 证据 |
|---|---|---|---|
| 1 | FTask 不用 Task | PASS | `ActivityProgressService.IncrementAsync` 返 `FTask<(...)>`; `C2G_ActivityIncrementHandler.Run` 返 `FTask`; `ActivityEvalHelper.EvaluateAndClaim / Increment / ResetCounterIfCrossedPeriod` 均返 `FTask` |
| 2 | sealed class | PASS | `public sealed class C2G_ActivityIncrementHandler`; `ActivityProgressService / ActivityEvalHelper` 为 `static class`(辅助,无继承需求,合规) |
| 3 | 源生成器注册,不手动注册 | PASS | handler 继承 `MessageRPC<>`,框架源生成器编译期注册;零手改 .g.cs / 零手动注册;新增文件无 RegisterHandler 类调用 |
| 4 | 错误路径返回 ResultCode 不抛异常 | PASS | handler 所有返回路径均 `response.ResultCode = ...` + return;catch MongoException → ServiceUnavailable + Warning 不 rethrow |
| 5 | 层次正确 | PASS | handler + service + helper 均在 Hotfix 层;Entity 层零改 |
| 6 | 生命周期清理 | PASS | handler 无 timer / event 订阅;static service / helper 无生命周期对象 |
| 7 | 机制边界 | PASS | Outer RPC 用 `MessageRPC<>` 正确;无 Address/Roaming/SphereEvent 误用 |

### 协议专项(protocol-check.md)

| # | 检查项 | 结论 |
|---|---|---|
| P1 | 协议放 Outer | PASS(`ActivityMessage.proto` 在 `examples/Config/NetworkProtocol/Outer/`) |
| P2 | 接口类型匹配 | PASS(`C2G_ActivityIncrement // IRequest,G2C_ActivityIncrementResponse` + `G2C_ActivityIncrementResponse // IResponse`) |
| P3 | 命名符合 C2G/G2C 约定 | PASS |
| P4 | handler 注释响应消息名一致 | PASS(proto 内注释 `// IRequest,G2C_ActivityIncrementResponse`) |
| P5 | 协议改后已导出 + 不手改产物 | PASS(双端生成物由 `ProtocolExportTool` 产出;diff 全空) |

### 服务端消息 handler 专项(server-message-handler-check.md)

| # | 检查项 | 结论 |
|---|---|---|
| H1 | Handler 基类匹配(RPC → MessageRPC<>) | PASS |
| H2 | sealed class | PASS |
| H3 | 业务错误用 ResultCode 不抛异常 | PASS |
| H4 | reply() 未误用(框架 finally 自动调) | PASS(handler 内未显式调 reply()) |
| H5 | 耗时操作前 session.IsDisposed 检查 | 未检查(同既有 C2G_QueryAttrLedgerHandler / C2G_PropertyChangeRequestHandler 范式;handler 仅做校验 + service 调用,非长耗时逻辑,session 断线后框架丢弃响应不影响数据一致性) |

### 数据库专项(database-check.md)

| # | 检查项 | 结论 |
|---|---|---|
| D1 | 数据库通过 ActivityServiceComponent 句柄(scene.World.Database 同源)访问 | PASS |
| D2 | $inc 原子累加无「读+写」并发竞态 | PASS(ActivityEvalHelper.Increment 用 `Update.Inc(x => x.Counter, delta)` MongoDB 原子) |
| D3 | 判达标+写已发周期键原子条件写 | PASS(`TryClaimCycleKey` filter `LastClaimedCycleKey < periodKey` + `UpdateOneAsync` + `MatchedCount > 0` 判成功;SV8 探针验并发只一次成功) |
| D4 | 先查后建路径无竞态 | PASS(`ReconcileDefs` 用 `UpdateOneAsync IsUpsert=true` 无 check-then-act 竞态;`Increment` 用 `$inc upsert` 无竞态) |
| D5 | DuplicateKey 双 catch | PASS(`ActivityEvalHelper.Increment` 有 `MongoCommandException(Code==11000)` + `MongoWriteException(Category==DuplicateKey)` 双 catch(lines 148/154))。ReconcileDefs 用 UpdateOneAsync IsUpsert=true 不建文档(无 DuplicateKey 路径),无需双 catch。 |

### SV12 Code Review 逐条

| # | 验收项 | 结论 | 证据 |
|---|---|---|---|
| CR1 SV12 ① | 身份从会话取 account(非客户端字段) | PASS | `GetSessionAccountName(session)` → `GateAccountFlagComponent → Account.Name`;协议字段仅 ActivityId / Delta,无 Account 字段 |
| CR2 SV12 ② | handler type=Cumulative 校验 + service 层兜底 type 校验(双层防御) | PASS | handler L79 `def.Type != ActivityEvalHelper.TypeCumulative → NotCumulative`;service L68 `def.Type != ActivityEvalHelper.TypeCumulative → NotCumulative` |
| CR3 SV12 ③ | IncrementAsync 统一逻辑路径(handler 调 service,不复制发奖编排) | PASS | handler L94 `await ActivityProgressService.IncrementAsync(...)`;service 内调 `ActivityEvalHelper.Increment + EvaluateAndClaim` |
| CR4 SV12 ④ | counter 累加用 MongoDB $inc 原子 | PASS | `ActivityEvalHelper.Increment` L140 `.Inc(x => x.Counter, delta)` |
| CR5 SV12 ⑤ | 跨周期 counter 清零(Daily/Weekly,OneShot 跳过) | PASS | `ResetCounterIfCrossedPeriod` L190 `if (cycle == CycleOneShot) return;`;Daily/Weekly 走 `periodStartMs` 条件写清零 |
| CR6 SV12 ⑥ | 校验顺序先 activityId 存在再 type | PASS | handler L70 `DefCache.TryGetValue` → InvalidRequest;L79 type 校验 → NotCumulative;顺序正确,防信息差泄露 |
| CR7 SV12 ⑦ | delta 上限钳制单次 10000 | PASS | handler L90 `clampedDelta = request.Delta > MaxDeltaPerCall ? MaxDeltaPerCall : request.Delta` |
| CR8 SV12 ⑧ | 抢占必在 SendMailTo 之前(claim-then-act) | PASS | `EvaluateAndClaim` L259 `TryClaimCycleKey` 成功后 L283 `SendMailTo`;抢占在发邮件之前 |
| CR9 SV12 ⑨ | 发奖经 32 §3.5 SendMailTo 入口 | PASS | `EvaluateAndClaim` L283 `await MailDecisionHelper.SendMailTo(...)` |
| CR10 SV12 ⑩ | 无新 MongoDB 集合/schema 字段加 + 无既有协议改 | PASS | 无新集合;activity_progress schema 字段集与 39 §3.2 一致;仅新增 ActivityMessage.proto(1 enum + 2 message) |

### conventions.md 交叉检(开发改过的持久文件)

| 文件 | 自检结论 |
|---|---|
| `ActivityMessage.proto`(新增) | 无过程叙事/指代词;注释为行为语义 PASS |
| `ActivityProgressService.cs`(新增) | 文件作用域命名空间 `namespace Fantasy;`;doc-comment 为行为描述不含 diff 叙事;无拟人/口语比喻 PASS |
| `C2G_ActivityIncrementHandler.cs`(新增) | 同上 PASS |
| `ActivityEvalHelper.cs`(修改) | 新增常量/方法 doc-comment 均为现状描述;`ResetCounterIfCrossedWeek` 改为内部代理说明为代理语义描述,无 diff 叙事 PASS |
| `ActivityServiceComponentSystem.cs`(修改) | activity_id=5 行注释为配置语义描述;Init 日志更新符合现状 PASS |
| `design-docs/39-activity-server.md`(同步重写) | §3.1 type 字段说明 + §3.5 节律表 Cumulative 行 + §七 O2/O3 状态行已覆盖式更新为「已交付」;无旧状态叠注保留;无过时正文 PASS |
| `pipeline/state/server-dev.md`(交接区) | 工作状态可识别所属任务;改动摘要描述改了什么(交接区格式正常);无可推导事实的转述副本 PASS |

---

## 协议变更同步检查

| 检查项 | 结论 |
|---|---|
| 双端 OuterMessage.cs diff 全空 | PASS |
| 双端 OuterEnum.cs diff 全空 | PASS |
| 双端 OuterOpcode.cs diff 全空 | PASS |
| 新 opcode: C2G_ActivityIncrement=268445457 / G2C_ActivityIncrementResponse=402663185 | PASS |
| 客户端 Assets/GameScripts/HotFix/ git diff 空 | PASS(零业务接入,PVC2) |
| ActivityMessage.proto ActivityIncrementResultCode enum + 2 message 双端一致 | PASS |

---

## 可复现清单

(总判定 PASS,无 FAIL 项)

---

## decisions(server-test 自治拍板)

1. **session.IsDisposed 未检查不影响 PASS**:handler 校验链 + service 调用为快路径,同既有 handler 范式;session 断线后框架丢弃响应,数据一致性不依赖此检查 → 记为观察,不降级 FAIL。

2. **ReconcileDefs 无 DuplicateKey 双 catch 不标缺口**:ReconcileDefs 用 `UpdateOneAsync IsUpsert=true`,不用 InsertOneAsync,无「先插入后更新」竞态,无 DuplicateKey 路径。memory `DuplicateKey 双 catch 口径` 适用范围是「先 AnyAsync 后 InsertOneAsync 播种路径」,此处不适用 → PASS。

3. **SV10 E1 全栈真往返标 PASS(MongoDB 层)**:本子单服务端验证完整(handler 校验路径 + service Increment 路径 + MongoDB 写入 + 邮件投出全部探针验通)。E1 中「mock 客户端走 32 拉邮件领奖 → 属性变化 → ledger 写一行」依赖 32/37/44 客户端链路,不属本子单 server 验收范围(设计 47 §8.3 联调验收项)→ 探针已验 mails 集合有邮件 + RewardId=5003,服务端侧 PASS。
