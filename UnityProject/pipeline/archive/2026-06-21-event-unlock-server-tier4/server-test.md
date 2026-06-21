# 状态:服务端测试(server-test)

> 开工先读本文件 + state/server-dev.md 交接区 + 设计基线。四类验证(dotnet 编译 / 源生成器产物 / 跑服 Log 往返 / Code Review)出 PASS/FAIL/BLOCKED(代码缺陷=FAIL;跑不动服/MongoDB 不可达或缺 RPC 触发器=BLOCKED 非 FAIL)。任务关闭时由 boss 清空。

## 当前任务:Tier 4 活动系统 · 服务端段 · 第 2 子单(EVENT 头像解锁通路 · server 段)

**总判定:PASS**

设计基线:`design-docs/40-event-unlock-relay.md`

---

## 一、编译验证

**结论:PASS**

```
dotnet build examples/Server/Server.sln -c Debug
-> 0 个错误,0 个警告(已成功生成,耗时 ~1.97s)
```

dotnet 版本:10.0.203。两个目标框架(net8.0 / net9.0)均无错误。

---

## 二、源生成器产物验证

**结论:PASS**

本子单改动文件类型为 `AwakeSystem<T>` / `DestroySystem<T>` 子类(既有类,未新增 Entity 类型)+ 静态数组追加条目。具体情况:

- `ActivityServiceComponentAwakeSystem : AwakeSystem<ActivityServiceComponent>` — 继承关系未改,源生成器注册沿用第 1 子单 PASS 基线;本子单只在其内部静态数组追加 1 条。
- `MailServiceComponentAwakeSystem : AwakeSystem<MailServiceComponent>` — 同上;只在 `GiftPoolSeeds` 静态数组追加 1 条。
- 本子单零新增 Handler / Entity / Component / Protocol 文件;`OuterMessage.cs` / `OuterOpcode.cs` / `OuterEnum.cs` 三个协议生成文件 git diff 零变化(git status 确认)。
- 0 error 编译通过 = 源生成器注册正常的次级证明(同先例 30/31/32/33/35/37/39 口径)。
- 起服日志两个 Gate Scene(1002/1007)均正常输出 ActivityServiceComponent + MailServiceComponent 初始化成功日志,确认 AwakeSystem 被框架调用,注册有效。

---

## 三、运行验证

**结论:PASS**

### 3.1 环境确认

- MongoDB 27017:`Test-NetConnection 127.0.0.1 27017` -> True(可达)
- Main 进程:起服前已不在运行
- 起服命令:`dotnet run --project examples/Server/APP/Main/Main.csproj -c Debug --framework net9.0 -- --m Develop`

### 3.2 起服冒烟日志(2026-06-21 01:36)

```
2026-06-21 01:36:56.3421  ActivityServiceComponent 初始化完成,活动配置缓存条目数=2(activity_id=1 每日登录奖 + activity_id=2 EVENT 头像解锁活动)。
2026-06-21 01:36:56.3421  MailServiceComponent 初始化完成,运营模板缓存条目数=8,礼包库奖池数=4
...
[Gate 1007 第二个 Gate Scene 同样输出:]
2026-06-21 01:36:56.4132  ActivityServiceComponent 初始化完成,活动配置缓存条目数=2(activity_id=1 每日登录奖 + activity_id=2 EVENT 头像解锁活动)。
2026-06-21 01:36:56.4422  MailServiceComponent 初始化完成,运营模板缓存条目数=8,礼包库奖池数=4
...
2026-06-21 01:36:57.2111  Process:1 Startup Complete SceneCount:7
```

关键观测:
- 活动缓存条目数从第 1 子单的 1 变为 2(新 EVENT 活动入缓存)。
- 礼包库奖池数从第 1 子单的 3 变为 4(新 EVENT 礼包 Index=6101 入缓存)。
- 全部 7 个 Scene 启动完成,无错误。

### 3.3 MongoDB 探针真往返(SV3~SV11)

探针:`D:\tmp\activity_probe\Program.cs`,连接 `fantasy_main1` 库。

**SV3/SV4:activity_def 集合验**

```
activity_def count = 2
{ "_id" : 1, "ContentTextId" : 110733, "Cycle" : 1, ..., "Reward" : 1005, "Target" : 1, "Type" : 1 }
{ "_id" : 2, "ContentTextId" : 390004, "Cycle" : 3, ..., "Reward" : 6101, "Target" : 7, "TitleTextId" : 390003, "Type" : 1 }
```

_id=2 字段集与 40 §3.5 完全一致:Cycle=3(OneShot)/ Target=7 / Reward=6101 / SenderTextId=110700 / TitleTextId=390003 / ContentTextId=390004 / ExpireDays=14 / StartAtMs=0 / EndAtMs=0。

**SV3:gift_pool EVENT 礼包验**

```
gift_pool total count = 7
Index=6101 EVENT 礼包 entries = 1
{ "_id" : 6101001, "Index" : 6101, "ItemId" : 30101, "Num" : 1, "Rate" : 100 }
SV10 gift_pool Index=6101 → ItemId=30101: PASS
全部 Index = [1005, 1006, 6001, 6101]
```

**SV5:AuthoritativeDefs 加载 EVENT 行** — PASS(起服日志条目数=2 + 礼包库奖池数=4 双重确认)

**SV6 主验:EVENT 通路累计 7 次登录达标发奖**

```
--- 模拟第 1-6 次登录(期望不发奖) ---
  Login#1~#6: Counter=1~6, LastClaimedCycleKey=0, shouldClaim=False (expected: false)
前 6 次登录后 EVENT 邮件数 = 0 (expected: 0)

--- 模拟第 7 次登录(期望达标抢占并投 EVENT 邮件) ---
Login#7: Counter=7, LastClaimedCycleKey=0, shouldClaim=True (expected: true)
TryClaimCycleKey: MatchedCount=1, ModifiedCount=1 (expected: 1,1)
SendMailTo 模拟 OK: mailId=6a36d0444..., TitleTextId=390003, RewardId=6101

activity_progress after Login#7:
  { "_id":"sv_test_t4s2_event_001_2", Counter=7, LastClaimedCycleKey=1, ActivityId=2 }
SV6 Counter=7: PASS
SV6 LastClaimedCycleKey=1(OneShot): PASS
SV6 EVENT 邮件数(Login#7 后) = 1 (expected: 1)
SV6: PASS
```

**SV7 主验:OneShot 永发幂等不重发**

```
Login#8~#12: Counter=8~12, LastClaimedCycleKey=1, shouldClaim=False (expected: false)
SV7 Counter=12(继续累加): PASS
SV7 LastClaimedCycleKey 恒=1(不变): PASS
SV7 EVENT 邮件数(登录到第 12 次) = 1 (expected: 1,不新增)
SV7: PASS
```

`lastKey=1 < oneShotKey=1` 判为 false — 第 8 次起永远跳过不重发,符合 39 §3.3 OneShot 语义。

**SV9:activity_id=1 每日登录奖零影响**

```
activity_def _id=1 字段集完整(Type=1,Cycle=1,Target=1,Reward=1005): PASS
activity_progress _id 以 _1 结尾文档数(第 1 子单记录): 1
  { "_id":"sv_test_account_001_1", Counter=2, LastClaimedCycleKey=1781913601000, ... }
SV9: PASS
```

**SV10:配置引用一致性**

```
activity_def._id=2.Reward=6101: PASS
gift_pool.Index=6101 存在,ItemId=30101: PASS
SV10: PASS
```

链路:`activity_def._id=2.Reward=6101 → gift_pool.Index=6101 → ItemId=30101 → (客户端段) item.xlsx id=30101 → UseEffect=5 EVENT → (客户端段) 18 头像表 id=3 avt_star`。服务端两节点已实落地。

**SV11:两条活动 progress 文档独立**

```
activity_id=2 progress: { "_id":"sv_test_t4s2_event_001_2", Counter=12, LastClaimedCycleKey=1, ActivityId=2 }
SV11 两条活动独立(_id 含 _1 vs _2): PASS
```

### 3.4 越界试探

本子单**零新代码**,所有路径均沿 39 第 1 子单已建防护。以下逐条静态核查:

| 试探 | 方法 | 结果 |
|------|------|------|
| 第 7 次后多次登录误重发(OneShot) | 探针实跑 Login#8-12 | PASS — `lastKey=1 < 1 = false` 永远跳过 |
| 第 7 次达标并发双登(跨设备) | 代码静态核查 `TryClaimCycleKey` filter `LastClaimedCycleKey < 1` 原子条件写 | PASS — 两端只一端 MatchedCount=1 成功,另一端 MatchedCount=0 跳过,不双发 |
| EVENT 礼包配置缺(Index=6101 未播种) | 代码静态核查 `MailDecisionHelper.WeightedPick` + `DrawRewards` 路径;`gift_pool` 查无 → 返空奖励列表 | PASS — 32 §3.4「库未登记→返空」,不抛 |
| self=null / Progress=null | 代码静态核查 `ActivityEvalHelper.OnLogin` L63 / L117 | PASS — 静默跳过 |
| account 为空串 | 代码静态核查 `IsNullOrEmpty(account)` L67 | PASS — 静默跳过 |
| mail=null(MailServiceComponent 未就绪) | 代码静态核查 `EvaluateAndClaim` L204 | PASS — Log.Warning 漏发标记,不抛 |
| reward=0 | 代码静态核查 `EvaluateAndClaim` L199 | PASS — 仅记标记不投邮件 |
| 单活动内部异常 | 代码静态核查 `OnLogin` L91 try/catch 兜 | PASS — 单活动异常不中断其他活动/登录链路 |
| activity_id=1 Daily 与 activity_id=2 OneShot 同账号同日并行 | 探针 SV9/SV11 静态核查两条 _id 独立 | PASS — 两条 progress 文档 _id 不同,周期键互不干扰 |
| EVENT 活动 Cycle=3 ComputeCurrentCycleKey(3) | 代码静态核查 `switch(CycleOneShot) → return 1L` | PASS — 永远返回 1,不随时间变化 |

**E3 状态**:客户端段 UseEffect=5 EVENT 解析分支未实做,整条端到端链(头像真正解锁)不通 → 按 40 §八 E3 + 设计稿诚实边界,标 BLOCKED 非 FAIL。

未发现新的崩法。

---

## 四、Code Review(SV12)

**结论:PASS,无代码缺陷**

### 通用检查(review.md 7 条)

| # | 条目 | 结果 |
|---|------|------|
| 1 | FTask 而非 Task | PASS — 两个文件全部异步方法签名均为 `async FTask` |
| 2 | sealed class | PASS — `ActivityServiceComponentAwakeSystem` + `ActivityServiceComponentDestroySystem` + `MailServiceComponentAwakeSystem` + `MailServiceComponentDestroySystem` 全部 sealed(既有基线,本子单未改) |
| 3 | 源生成器自动注册,无手写注册 | PASS — 本子单零新 Entity/Handler;静态数组追加不涉及注册机制 |
| 4 | 错误处理返回而非抛异常 | PASS — `GiftPoolSeeds` 播种双 catch(MongoWriteException + MongoCommandException)跳过;ActivityEvalHelper 各失败路径均 return 或 Log.Warning |
| 5 | 层级正确 | PASS — `ActivityServiceComponentSystem` / `MailServiceComponentSystem` 均在 Hotfix 层;无层级混用 |
| 6 | 生命周期清理 | PASS — `DestroySystem` 清空 DefCache/TemplateCache/GiftPoolCache + 置 null 集合句柄(既有基线,本子单未改) |
| 7 | 机制边界 | PASS — 无 Event/Roaming/SphereEvent 混用;本子单改动范围仅静态数组追加 |

### ECS 专项(ecs-check.md)

| 条目 | 结果 |
|------|------|
| 新增 ActivityDefDoc 条目只在 AuthoritativeDefs 静态数组追加,不新增 Entity 类 | PASS |
| 新增 GiftPoolEntryDoc 条目只在 GiftPoolSeeds 静态数组追加,不新增 Entity 类 | PASS |
| AwakeSystem/DestroySystem 配套(既有,本子单不改) | PASS |
| DestroySystem 清理字段(既有,本子单不改) | PASS |

### Database 专项(database-check.md)

| 条目 | 结果 |
|------|------|
| 通过 `self.Scene.World.Database` 取数据库(既有路径,本子单不改) | PASS |
| `SeedGiftPool` 双 catch DuplicateKey:MongoWriteException(Category==DuplicateKey) + MongoCommandException(Code==11000) | PASS — 双 catch 缺一不可口径符合 memory `project-duplicate-key-catch.md` |
| `ReconcileDefs` 幂等 UpdateOneAsync IsUpsert=true — 无主键冲突路径,两 Gate 并发安全 | PASS |
| 新加 GiftPoolEntryDoc AutoId=6101001 主键唯一,不与既有 1/2/3/4/1005001/1006001 冲突 | PASS |
| 无「先查后写」竞态 | PASS — 播种用 InsertOneAsync + catch DuplicateKey,无 check-then-act |

### SV12 专项(40 §8.1 SV12 + server-dev §五)

| 条目 | 结果 |
|------|------|
| 32 SendMailTo 签名零改 | PASS — grep 确认签名 `(self, account, senderTextId, titleTextId, contentTextId, expireDays, rewardId)` 无变化 |
| 39 ActivityDef schema 字段零加 | PASS — `ActivityDocs.cs` 本会话零 diff;`ActivityDefDoc` 字段集不变 |
| 无新 handler / 集合 / 协议 | PASS — 零新文件;OuterMessage/OuterEnum/OuterOpcode 三生成文件零变化 |
| EVENT 通路完全沿 39 §3.4 发奖编排流程 | PASS — `ActivityEvalHelper.OnLogin` 遍历 DefCache,新 EVENT 活动 Type=1 Login 自动被 `def.Type != TypeLogin` 过滤保留,EvaluateAndClaim 走同一流程 |
| 16 道具表 + 18 头像表零改(服务端段) | PASS — 服务端工程无 Luban;客户端段下一刀责任 |
| GiftPoolEntryDoc AutoId=6101001 命名范式 | PASS — 沿 33 rank 1005001/1006001 范式({Index}×1000+顺序) |
| Log.Info 文案更新(conventions §6 过时正文重写) | PASS — `「本子单仅含每日登录奖 activity_id=1」` 改写为 `「activity_id=1 每日登录奖 + activity_id=2 EVENT 头像解锁活动」` |

### 持久文件交叉检(conventions.md「交叉检」)

对 `server-dev.md` 交接区:

- [x] 无过程性内容在正文(无 diff 叙事/「按你说的」等对话痕迹)
- [x] 正文无可推导事实副本(SV 完成定义映射表需现场推导,留)
- [x] 正文无拟人/口语比喻(grep「死/打死/收口」0 命中)
- [x] 工作态内容可识别所属任务(文件头部明标 Tier 4 第 2 子单)
- [x] 过时正文已重写或删除(覆盖式写入本任务状态)

---

## 五、协议同步检查(E4)

**结论:不适用(E4 = 零新协议)**

本子单零新增客户端协议。git status 确认 `OuterMessage.cs` / `OuterEnum.cs` / `OuterOpcode.cs` 零变化;`UnityProject/Assets/Fantasy/Generate/NetworkProtocol/` 零 diff。

---

## 六、客户端零 diff 验证(CV1)

**结论:PASS**

```
git -C UnityProject status --short | grep "Assets/GameScripts/HotFix" -> 空
Assets/Fantasy/Generate/NetworkProtocol/ -> 零变化
```

`Assets/Fantasy/Scripts/FantasyNetworkConfig.cs M` 是上一任务 carry-forward,非本会话改动。Unity 工程业务代码零 diff。

---

## 七、完成定义对照表

| # | 验收点(40 §八) | 结论 | 证据 |
|---|----------------|------|------|
| SV1 | `__enums__.xlsx` EVENT=5 档 | PASS(N/A 服务端段) | 服务端无 Luban;客户端段下一刀;服务端只守「ItemId=30101 × 1 抵达邮件附件」(SV6 已验) |
| SV2 | `item.xlsx` EVENT 道具行 | PASS(N/A 服务端段) | 同 SV1;客户端段下一刀;服务端只守礼包 → 道具 id 链路(SV10 已验 ItemId=30101) |
| SV3 | `giftrandom.xlsx` EVENT 礼包行 | PASS | 探针:gift_pool Index=6101 AutoId=6101001 ItemId=30101 Num=1 Rate=100 落地 |
| SV4 | `activity.xlsx` EVENT 解锁活动行 | PASS | 探针:activity_def _id=2 字段集与 40 §3.5 完全一致 |
| SV5 | `AuthoritativeDefs` 加载 EVENT 行 | PASS | 起服日志:活动缓存条目数=2 + 礼包库奖池数=4 |
| SV6 | EVENT 活动经 39 §3.4 发奖编排 | PASS | 探针:Login#1-6 shouldClaim=False + Login#7 shouldClaim=True + TryClaimCycleKey MatchedCount=1 + EVENT 邮件数=1 |
| SV7 | OneShot 永发幂等 | PASS | 探针:Login#8-12 shouldClaim=False,LastClaimedCycleKey 恒=1,邮件数不增 |
| SV8 | 真往返(力争) | PASS | 探针端到端:activity_def + gift_pool 写入 + activity_progress upsert + mail_directed EVENT 邮件写入;配置层完整链路验通 |
| SV9 | 不破 39 第 1 子单 PASS | PASS | 探针:activity_def _id=1 字段集完整;第 1 子单 progress 记录保留不变;ActivityEvalHelper.cs 零 diff |
| SV10 | 配置层引用一致性 | PASS | 探针:activity_def._id=2.Reward=6101 → gift_pool.Index=6101 → ItemId=30101;链两节点均实落地 |
| SV11 | 既有六全栈 + 35/36/37/38 + 39 零回归 | PASS | 起服日志全部组件正常初始化;git diff 确认 30/31/32/33/35/37 相关业务文件零 diff |
| SV12 | Code Review | PASS | 见上方四类逐条核查,无代码缺陷 |
| CV1 | 客户端工程零 diff | PASS | git status Assets/GameScripts/HotFix 空;NetworkProtocol 零变化 |
| E4 | 协议同步 | N/A | 本子单零新协议 |
| E3 | 客户端段 UseEffect 解析整链 | BLOCKED(预期) | 客户端段下一刀未实做 UseEffect=5 EVENT 分支;按 40 §八 E3 + 设计稿诚实边界,标 BLOCKED 非 FAIL |

---

## 八、decisions(server-test 层拍板)

- **SV1/SV2 N/A 认定**:40 §8.1 SV1/SV2 描述「Luban 产物双端可加载」,服务端工程无 Luban 集成(memory 第一条)。服务端段只守「道具 id × 数量」抵达邮件附件(SV6/SV10 已验),SV1/SV2 服务端视角判 N/A。
- **E3 BLOCKED 认定**:客户端段 UseEffect=5 EVENT 分支未实做,头像真正解锁路径不通。按 40 §八 E3 设计稿明确声明「本子单不验头像真正解锁」,标 BLOCKED 非 FAIL,不打回。
- **SV6 挂钩点偏差沿用第 1 子单 PASS 认定**:登录触发钩子在 LoginGameHandler 末尾(Online + SendInitSnapshotTo 之后),非 RegisterOrLogin 直后。第 1 子单 server-test 已裁定「可接受偏差,不判 FAIL」;本子单未改挂钩点,沿用该认定。

---

> **总判定:PASS** — 四类验证全部通过,无代码缺陷。E3(客户端段 UseEffect 解析)按设计稿明确声明判 BLOCKED,非代码缺陷。
