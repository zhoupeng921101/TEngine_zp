# 状态:服务端开发(server-dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-server-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:Tier 4 活动系统 · 服务端段 · 第 4 子单(Cumulative 节律落地 + 累计游戏 100 局样例)

**设计基线**:[`design-docs/47-activity-cumulative.md`](../../design-docs/47-activity-cumulative.md) + [plan 交接区](./plan.md)
**承接**:Tier 4 第 3 子单 server PASS(`eff3376b`)— 39 §3.4 发奖编排 + ActivityEvalHelper.Increment/EvaluateAndClaim/ResetCounterIfCrossedWeek 已实做(Login 节律 4 套活动并存),本子单兑现 39 §3.5 留 O3 的 Cumulative 节律接缝。
**编译态**:`dotnet build examples/Server/Server.sln` 0 error / 0 本子单新增 warning(21 个既有 warning 全部来自 Account.cs / Unit.cs / Controller / SphereEvent handler / DotRecast 等 — 均与本子单文件无关)
**起服自检**:`dotnet run ... --m Develop` 12s 启动完成,关键日志(WorldA 双 Gate Scene 各一次):
> `ActivityServiceComponent 初始化完成,活动配置缓存条目数=5(activity_id=1 每日登录奖 Daily + activity_id=2 EVENT 头像 OneShot + activity_id=3 累计 7 天大奖 OneShot + activity_id=4 周累计 5 天周奖 Weekly + activity_id=5 累计游戏 100 局 Cumulative OneShot)`

无 error / exception;Process:1 Startup Complete SceneCount:7

**双端协议生成物对称**:`diff` Fantasy `OuterOpcode.cs` 与 UnityProject 客户端 `OuterOpcode.cs` 输出空(全量相同);新 opcode `C2G_ActivityIncrement=268445457 / G2C_ActivityIncrementResponse=402663185`。**注意**:本次 proto 导出按 proto 文件名重排了既有 opcode(ActivityMessage 排首字母 A 顶到第一位),既有协议 opcode 全量整体下移 — 这是导出工具行为(沿 memory `proto-exporter-quirks` 第 2 条:双端同次重生成共识,opcode 是双端共识的运行期常量、非持久 wire 契约,核对 = `diff` 两端全量相同即可)。

### 改动摘要

本子单核心 = **协议两条 + service 一处 + handler 一处 + Activity 配置加 1 行 + EvaluateAndClaim 返 bool**,服务端复用 39 §3.4 发奖编排零改一行代码,Cumulative 节律共用 Login 节律已实做的 `Increment` + `EvaluateAndClaim`(只是「+counter 起点」从「登录」换为「业务方推 + 服务端进程内调」)。客户端段仅 protoc 生成物同步(双端 OuterMessage / OuterEnum / OuterOpcode / NetworkProtocolHelper 自动生成,`Assets/GameScripts/HotFix/` git diff 空)。

**(1) 新增 proto 文件**(`examples/Config/NetworkProtocol/Outer/ActivityMessage.proto`):
- `enum ActivityIncrementResultCode`:4 个值 `Success=0 / InvalidRequest=1 / NotCumulative=2 / ServiceUnavailable=3`(plan §3.3 错误码集 4 档,`NotCumulative` 单列便客户端段下一刀做差异提示 + 反作弊日志统计「客户端试图推非 Cumulative 类活动」)
- `message C2G_ActivityIncrement // IRequest,G2C_ActivityIncrementResponse`:2 字段 `ActivityId(int32) / Delta(int32)`(身份**不**在协议字段,plan §3.2 + SV12 ① 守不变量;客户端硬塞 account 字段服务端忽略)
- `message G2C_ActivityIncrementResponse // IResponse`:3 字段 `ResultCode / CurrentCounter(int64) / TargetReached(bool)`

**(2) 改 `ActivityServiceComponentSystem.cs` 加 `AuthoritativeDefs` 第 5 行**:
- `activity_id=5, NameTextId=390009, DescTextId=390010, Type=2(Cumulative), Cycle=3(OneShot), Target=100, Reward=5003(复用 43 子单 5003 钻石礼包,GiftPoolSeeds 已注册 — 砍掉新建 giftrandom 增量、沿 47 §3.4 可选 mail_def 路径), SenderTextId=110700, TitleTextId=390009, ContentTextId=390010, ExpireDays=14`
- 同步更新 Init 末尾日志(添加 activity_id=5 描述,sanity 验证 ReconcileDefs + ReloadCache 工作正常)

**(3) 改 `ActivityEvalHelper.cs`**:
- 新增 `public const int TypeCumulative = 2`(同 ActivityDefDoc.Type 编号,供 service 层 + handler 层校验复用)
- `EvaluateAndClaim` 签名从 `async FTask` 改为 `async FTask<bool>`:返「本次是否抢占成功 + 已执行发奖逻辑」供 Cumulative 节律消费(handler 回客户端 `TargetReached` 字段,plan §3.2);Login 节律(OnLogin)忽略此值用 `_ = await` 显式丢弃,行为不变(SV11 零回归 — Login 节律 4 套活动行为完全等同前)。`reward=0` / `mail==null` / `SendMailTo 返 null` 三种「抢占已成功但邮件未实际投出」窄窗都返 `true`(玩家语义已达标,服务端账目已记 LastClaimedCycleKey;邮件投递失败由运营补 — 沿设计 §四「漏发可补、超发不可补」诚实取舍)
- 新增 `public static ResetCounterIfCrossedPeriod(self, activityId, account, cycle, nowMs)`:Cumulative / Login 通用跨周期 counter 清零(Daily / Weekly 共用一处实现,OneShot 跳过)。`ResetCounterIfCrossedWeek` 改为内部代理 `ResetCounterIfCrossedPeriod(..., CycleWeekly, ...)`,行为完全等价(SV11 守 Weekly 活动 4 跨周清零行为不变)。设计 §3.5 service 前瞻支持 Daily/Weekly Cumulative + SV12 ⑤ 跨周期清零兑现

**(4) 新增 `ActivityProgressService.cs`**(`Hotfix/.../Activity/ActivityProgressService.cs`):
- `static class ActivityProgressService`,纯静态 service helper(沿 PlayerPropertyServiceHelper / AccountServiceHelper / MailDecisionHelper 同范式)
- `IncrementAsync(ActivityServiceComponent?, MailServiceComponent?, account, activityId, delta, nowMs)` → `FTask<(ActivityIncrementResultCode, long currentCounter, bool targetReached)>`
- 流程(沿设计 §3.5 mermaid 序列图):
  1. 服务组件未就绪 / 配置未加载 → ServiceUnavailable(沿设计 §四 BLOCKED-env 基线)
  2. 读 DefCache 取活动配置不存在 → ServiceUnavailable(防御性兜底;handler 已校验过,这里走到代表配置缓存与 handler 不同步)
  3. **type 校验**`def.Type != ActivityEvalHelper.TypeCumulative` → `NotCumulative`(service 层兜底,纵深防御未来服务端业务方绕 handler 误调推 Login 类活动)
  4. 跨周期 counter 清零(`ActivityEvalHelper.ResetCounterIfCrossedPeriod`,Daily/Weekly 触发,OneShot 跳过)
  5. `ActivityEvalHelper.Increment` `$inc counter += delta` upsert(沿 33 §3.2 + 39 既有原子语义)
  6. Find 一次读写后 counter(供回包 + handler Log)
  7. `ActivityEvalHelper.EvaluateAndClaim` 判达标 + 抢占周期键 + 调 SendMailTo,取其 `bool` 作 targetReached
  8. catch `MongoException` → ServiceUnavailable + Warning,不抛(沿 32/45 同口径)

**(5) 新增 `C2G_ActivityIncrementHandler.cs`**(`Hotfix/.../Activity/C2G_ActivityIncrementHandler.cs`):
- 继承 `MessageRPC<C2G_ActivityIncrement, G2C_ActivityIncrementResponse>`,源生成器编译期注册(无需手注册)
- `MaxDeltaPerCall = 10000` const(plan O3 可改运营可配)
- 校验顺序(plan §3.3 + SV12 ⑥ 顺序声明,任一档失败立即返码、不进 service):
  1. **身份从会话取**(`GetSessionAccountName` 同 mail/property handler 范式 → `session.GetComponent<GateAccountFlagComponent>().Account.Name`);会话未挂账号 → `ServiceUnavailable`(沿 32/45 同口径,**不**新增 NotLoggedIn 码与协议错误码集 4 档保持一致 — plan §3.3 decisions §3)
  2. `request.Delta <= 0` → `InvalidRequest`(SV6)
  3. `ActivityServiceComponent` 未挂 / DefCache 空 → `ServiceUnavailable`(防御性)
  4. `activityId` 存在性(DefCache.TryGetValue) → 不存在 → `InvalidRequest`(归一,**不**泄露「该 id 在配但 type 不对」信息差,plan §3.3 顺序决策 + SV12 ⑥)
  5. `def.Type != TypeCumulative` → `NotCumulative`(SV5,防客户端用此 RPC 推 Login 类活动旁路登录节律)
  6. `clampedDelta = Math.Min(request.Delta, MaxDeltaPerCall)`(降级语义,**不**报错,客户端从 `response.CurrentCounter` 自查实际写入值,沿 45 limit 钳制同范式)
- 调 `ActivityProgressService.IncrementAsync(activitySvc, mailSvc, account, request.ActivityId, clampedDelta, TimeHelper.Now)`(handler / service 统一逻辑路径 — 不复制两份发奖编排,plan §3.5 + SV12 ③)
- `Log.Debug` 摘要(account / activityId / delta / clamped / result / counter / targetReached)便于排查
- 所有失败以 ResultCode 回包,不抛异常断连(SV12);框架 RPC ErrorCode 始终保持 0

### 文件清单

| 路径(相对 `D:/work/TEngine_block/Fantasy/`) | 改动 |
|---|---|
| `examples/Config/NetworkProtocol/Outer/ActivityMessage.proto` | **新增**(enum + 2 message,沿 PlayerPropertyMessage / RedeemMessage 同范式) |
| `examples/Server/APP/Hotfix/Game Examples/Gate/Activity/ActivityServiceComponentSystem.cs` | AuthoritativeDefs 加 activity_id=5 行 + Init 日志同步追加 |
| `examples/Server/APP/Hotfix/Game Examples/Gate/Activity/ActivityEvalHelper.cs` | 加 TypeCumulative 常量 + EvaluateAndClaim 返 bool + 抽出 ResetCounterIfCrossedPeriod 通用版(Daily/Weekly 共用,OneShot 跳过) |
| `examples/Server/APP/Hotfix/Game Examples/Gate/Activity/ActivityProgressService.cs` | **新增**(Cumulative 节律服务端进程内 API,handler / 服务端业务方共用) |
| `examples/Server/APP/Hotfix/Game Examples/Gate/Activity/C2G_ActivityIncrementHandler.cs` | **新增**(Outer RPC handler,源生成器编译期注册) |
| `examples/Server/APP/Entity/Generate/NetworkProtocol/OuterMessage.cs` | 导出工具产出(追加 2 个 partial class + dispose);**不**手改 |
| `examples/Server/APP/Entity/Generate/NetworkProtocol/OuterEnum.cs` | 导出工具产出(追加 ActivityIncrementResultCode enum);**不**手改 |
| `examples/Server/APP/Entity/Generate/NetworkProtocol/OuterOpcode.cs` | 导出工具产出(本次整体重排 — 双端同次产出共识,沿 memory proto-exporter-quirks);**不**手改 |

**客户端 UnityProject 同步产出**(`D:/work/TEngine_block/UnityProject/Assets/Fantasy/Generate/NetworkProtocol/`):

| 文件 | 来源 |
|---|---|
| `OuterMessage.cs` | 同次导出工具产出,与服务端同字节同尺寸 |
| `OuterEnum.cs` | 同次导出工具产出 |
| `OuterOpcode.cs` | 同次导出工具产出,`diff` 服务端版全量相同(双端 opcode 共识,CV1 + E1) |
| `NetworkProtocolHelper.cs` | 同次导出工具产出(消息序列化注册) |

**设计稿同步重写**(UnityProject `design-docs/`,sweep 闭合):
- `design-docs/39-activity-server.md` §3.1 type 字段说明从「本子单只接 Login,其余留 O3」→「Login / Cumulative 已交付;Schedule/Action 留 O3」;§3.1 旁注 type 扩展点表 Cumulative 行 + §3.5 节律表 Cumulative 行 + §3.5 旁注 + §七 O2 / O3 + §八 BLOCKED 列表 6 处状态行(全部 plan 已先期更新,本子单仅补 §3.1 type 字段说明那一行)
- `design-docs/47-activity-cumulative.md`(plan 阶段新增,server-dev 不动)
- `design-docs/assets/nav.js`(plan 阶段加 47 入口) + `design-docs/index.html`(plan 阶段递增 `?v=12`)

**额外说明**:
- `examples/Server/APP/Entity/Fantasy.config` 在 git diff 中显示已改 — 是 Tier 2 第 3 子单 server-dev 改的本机 MongoDB 连接串(沿 server-dev memory `local-mongodb-for-server-roundtrip`),保留不动、不归属本子单
- `Assets/Fantasy/Scripts/FantasyNetworkConfig.cs` 已 stage 改动 — 非本子单产物(可能是其他刀的客户端段刀已 stage),不归属
- Untracked `AGENTS.md` / `Docs/学习课件/` 非本子单产物,不归属
- 客户端段 `Assets/GameScripts/HotFix/` 零业务 diff(SV12 + PVC2 要求,已验)

### decisions(本环节自治拍板的取舍)

1. **EvaluateAndClaim 改返 bool(而非新建 EvaluateAndClaimWithResult 包装)** → 取此最小改动。理由:① 既有调用方只有 OnLogin 一处,改 `await EvaluateAndClaim(...)` 为 `_ = await EvaluateAndClaim(...)` 一行即可(语义 100% 等价 — C# await void task 与 await<T> task 丢弃返回值都不阻塞);② 包装新函数会重复读 progress + 重复算 periodKey + 重复抢占,本质没省事;③ EvaluateAndClaim 本身已经知道「抢占是否成功」(`TryClaimCycleKey` 返 bool 内部用),只是没透传出去,改一处签名 = 兑现既有信息;④ Login 节律的「玩家可观测路径 = 邮箱」语义不依赖此返回值,改造不影响 SV11 零回归。可逆。

2. **抽出 `ResetCounterIfCrossedPeriod` 通用版(Daily/Weekly 共用,OneShot 跳过)** → 取此前瞻支持,沿 plan SV9 + SV12 ⑤ + 设计 §3.5 「跨周期 counter 清零(沿 [43 §3.2 旁注] 范式)」。理由:① 设计 §3.5 mermaid 显式画了「`cycle ∈ {Daily, Weekly}` 且 `lastClaimedCycleKey < 本周期键` → 跨周期重置 counter」;② 当下本子单样例是 OneShot,跨周期清零分支不会触发,但 service 前瞻支持后续 Daily / Weekly Cumulative 活动加 1 行配置零代码改;③ 既有 `ResetCounterIfCrossedWeek` 只处理 Weekly,改通用 = 加一个 cycle 参数 + 1 行 OneShot 跳过分支,代码量小;④ `ResetCounterIfCrossedWeek` 不删,改为内部代理调通用版以守 OnLogin 行为完全等价(SV11 Weekly 活动 4 跨周清零零回归)。可逆。

3. **service 层兜底 type 校验时返 `ServiceUnavailable` 而非 InvalidRequest(对 activityId 不存在场景)** → 取此语义差异化。理由:① handler 已校验过 activityId 存在(用同一 DefCache),走到 service 层意味着「handler 校验时存在 + service 校验时不存在」= 在两步之间被外部改 — 这是「服务问题」(reconcile 跑了 / 缓存被 hot reload 等)非「客户端参数错」;② InvalidRequest 是「客户端发包错」语义,这里语义不符;③ 客户端体验角度:重试一次大概率就好,与 ServiceUnavailable 重试体验一致;④ 实际生产中触发概率极低,主要是防御性兜底。可逆。

4. **服务端业务方直调 `IncrementAsync` 不再做 delta 上限钳制(只 handler 层钳)** → 取此分层职责。理由:① handler 是「客户端边界守门」(身份从会话取 + delta 上限钳),service 是「内部业务通路」(假设入参可信);② 服务端业务方(未来运营 GM 工具 / 自动赠送)若需「一次性 +1000 钻奖励」会直调 service,不应受 10000 钳制(运营内部工具,可信调用方);③ 防恶意推爆 counter 是「客户端边界」问题,服务端业务方不在此攻击面;④ service 层不重复实现 handler 的 delta 上限钳让两路职责清晰、不漂移。可逆(若运营接口未来也想限上限可在 service 加一个可选 maxDelta 参数,但暂不投机)。

5. **Reward=5003 复用 43 子单已加的钻石礼包(砍 giftrandom.xlsx + mail.xlsx 增量)** → 取 plan §3.4 默认推荐路径之一。理由:① plan §3.4 明示「可选加 giftrandom 5005 + mail 7005;亦可复用 43 5003 钻石礼包 / 40 6101 EVENT 礼包;mail_def=0 走兜底文案,验收等价」;② 服务端工程**无 Luban 配置集成**(server-dev memory 第 1 条):activity 配置在服务端是 AuthoritativeDefs 声明 + reconcile,不存在「加 xlsx 行」概念;`Reward=5003` 直接复用 43 已注册的 GiftPoolSeeds 条目(Index=5003 中型钻石/材料包,详 MailServiceComponentSystem.GiftPoolSeeds),零运行期改动 — 服务端起服时复用既有礼包配置即可,SV4/SV10 验通;③ 邮件文案占位 TitleTextId=390009 / ContentTextId=390010(沿 43 同范式)是占位 textId,运营后续配 i18n 表即填实文案 — 本子单只验「邮件投出 + 挂 reward」,文案展示是客户端表现层职责。可逆(运营未来要专属 5005 礼包行只需扩 GiftPoolSeeds 一行 + 改 activity_id=5 的 Reward 值,零侵入)。

6. **handler 不在 5 项校验前先校 `request.Delta > 0`(挪到第 2 步)** → 沿设计 §3.3 mermaid 顺序。理由:① 设计 §3.3 mermaid 标的「校验 5 项」实际执行顺序是「身份 → activityId 存在 → type=Cumulative → delta > 0 → delta 钳制」;② 但实测 implementation 把「delta > 0」放到第 2 步(身份后立即),理由:`delta <= 0` 是最便宜的校验(无 MongoDB / DefCache lookup),先 fail-fast 省内部计算 + 客户端伪造攻击成本最低,先拦最划算;③ 顺序调整不破设计行为 — 都返 InvalidRequest,只是先后顺序;④ activityId 存在性 / type=Cumulative 校验顺序仍按设计 §3.3 + SV12 ⑥ 「先存在再 type」严守(防泄露信息差)。微调可逆,不抵触设计。

7. **不新建错误码 NotLoggedIn(会话未挂账号 → 走 ServiceUnavailable)** → 沿 plan §3.3 + decisions §3 同口径。理由:① plan §3.3 错误码集 4 档已声明,引入 NotLoggedIn 破协议字段表;② 沿 32 / 45 handler 已建范式(会话未登录走 ServiceUnavailable + Warning Log);③ 实战中「会话有但 GateAccountFlagComponent 没挂」窗口本来就极窄(35 登录链路未完成 + 客户端早发请求 = 罕见 race condition);④ 客户端段下一刀对 ServiceUnavailable / NotLoggedIn 体验等价(都是「提示 + 重连」),不必协议层细分。可逆(若 server-test 实测「会话未登录」场景需要单独码,Tier 4+ 加,O2)。

### 验证点(交 server-test 逐条核)

> 详细完成定义见 [设计 47 §8.1](../../design-docs/47-activity-cumulative.md) 表 SV1–SV12 + §8.2 PVC1/PVC2 + §8.3 E1-E3,本节给「该验什么、怎么验、预期结果」。

#### 编译 + 起服自检

- **C1 编译干净**:`cd D:/work/TEngine_block/Fantasy && dotnet build examples/Server/Server.sln` → 0 error / 0 本子单新增 warning(21 个既有 warning 全部来自非本子单文件) ✓ 已过
- **C2 起服日志(SV1 / SV2)**:`Get-Process Main | Stop-Process -Force; dotnet run --project examples/Server/APP/Main/Main.csproj -c Debug --framework net9.0 --no-build -- --m Develop` → 期望日志含 `ActivityServiceComponent 初始化完成,活动配置缓存条目数=5(... + activity_id=5 累计游戏 100 局 Cumulative OneShot)` × 2(WorldA 双 Gate Scene)+ 无 error / exception ✓ 已过
- **C3 客户端生成物对称(PVC1)**:`diff D:/work/TEngine_block/Fantasy/examples/Server/APP/Entity/Generate/NetworkProtocol/OuterOpcode.cs D:/work/TEngine_block/UnityProject/Assets/Fantasy/Generate/NetworkProtocol/OuterOpcode.cs` → 全量相同(双端 opcode 共识) ✓ 已过;`git status -- Assets/GameScripts/HotFix/` → 空(零业务接入,PVC2) ✓ 已过

#### MongoDB 真往返(SV3-SV10 + E1-E3,**依赖本机 mongod 127.0.0.1:27017,不可达则 BLOCKED 非 FAIL**)

> mongod 启动详见 memory 全局 `local-mongodb-for-server-roundtrip` + server-dev memory「先探测再起」。

- **V1 SV3 handler 实装 + 身份从会话取**:全新 UUID 在 `accounts` 注册登录(经 C2G_LoginGameRequest)→ 用同会话发 `C2G_ActivityIncrement { ActivityId=5, Delta=1 }` → 期望响应 `{ ResultCode=Success(0), CurrentCounter=1, TargetReached=false }`;**会话未登录**直接发(skip 登录步骤)→ 期望 `{ ResultCode=ServiceUnavailable(3), CurrentCounter=0, TargetReached=false }`
- **V2 SV4 累计 100 局达标 + 发邮件 + 后续无重发**:登录后发 100 次 `(5, 1)` 或 1 次 `(5, 100)` →
  - 前 99 次:`{ Success, CurrentCounter=N, TargetReached=false }`
  - 第 100 次:`{ Success, CurrentCounter=100, TargetReached=true }`,且 `activity_progress` 集合 `{account}_5` 文档 `counter=100, LastClaimedCycleKey=1`,`mails` 集合该账号收件箱多一封邮件(SenderTextId=110700, TitleTextId=390009, ContentTextId=390010, ExpireDays=14, 附 reward=5003)
  - 第 101 次:`{ Success, CurrentCounter=101, TargetReached=false }`(OneShot 永发停 — `lastKey=1 >= periodKey=1` 跳过抢占);`mails` 集合无新增邮件
- **V3 SV5 type 校验拒不符活动**:登录后发 `(1, 1)` 推 activity_id=1(每日登录奖,type=Login)→ 期望 `{ ResultCode=NotCumulative(2), CurrentCounter=0, TargetReached=false }`;查 `activity_progress` 集合 `{account}_1` 文档 counter **零改动**(不进 service,不调 `$inc`);同理推 activityId=2 / 3 / 4 → 全返 NotCumulative
- **V4 SV6 delta 负数 / 零 / activityId 不存在拒**:
  - `(5, -1)` → `{ ResultCode=InvalidRequest(1), CurrentCounter=0, TargetReached=false }`
  - `(5, 0)` → InvalidRequest
  - `(999, 1)` activityId 不存在 → InvalidRequest(顺序:身份 OK → delta > 0 OK → activityId 不存在,沿设计 §3.3 + decisions §6 调整顺序)
  - `activity_progress` 文档零改动(handler 返码不进 service)
- **V5 SV7 delta 上限钳制**:`(5, 50000)` → handler 钳到 10000 → service `$inc counter += 10000`(若达标则同时抢占 + 投奖)→ 期望 `{ Success, CurrentCounter=10000(若先前 0)或更大, TargetReached=true(超过 100 必达标) }`;**不**返错码(降级语义,客户端从 CurrentCounter 自查实际写入)
- **V6 SV8 同账号并发原子幂等**:同账号同时发两次 `(5, 50)` → MongoDB `$inc` 文档级原子 + `FindOneAndUpdate` 条件写抢占 → 最终 `counter=100, LastClaimedCycleKey=1`(无漏 delta + 无双发邮件);`mails` 集合该账号收件箱**仅 1 封**活动 5 邮件
- **V7 SV9 跨会话 / 重启幂等**:counter 达 100 + 发奖后重启服务端(`Stop-Process Main; dotnet run ...`)+ 同账号再 Increment `(5, 1)` → 读 `lastClaimedCycleKey=1 >= OneShot key=1` → 跳过抢占;`{ Success, CurrentCounter=101, TargetReached=false }`;`mails` 无新邮件
- **V8 SV10 真往返**(力争):全链 - 起服 + 新账号登录 + 发 100 次 `(5, 1)` + `targetReached=true` + mongod 探针验 `activity_progress {account}_5 {counter:100, LastClaimedCycleKey:1, LastUpdatedAt: 最新}` + `mails` 集合多一封活动邮件挂 reward=5003 + 客户端走 32 邮件拉列表见活动邮件 + 走 32 领取协议拿到礼包奖励
- **V9 SV11 既有 39 / 40 / 43 行为零回归**:
  - 活动 1(每日登录奖 Daily):登录触发自动 +1 + Daily 跨日重发 — 行为等同 39 第 1 子单 PASS 后
  - 活动 2(EVENT 头像 OneShot):累计 7 次登录抢占 OneShot key=1 + 投 EVENT 礼包邮件 6101 — 行为等同 40 第 2 子单 PASS 后
  - 活动 3(累计 7 天大奖 OneShot)+ 活动 4(周累计 5 天周奖 Weekly):行为等同 43 第 3 子单 PASS 后(Weekly 跨周一 counter 清零仍触发,因 `ResetCounterIfCrossedWeek` 改为内部代理 `ResetCounterIfCrossedPeriod(..., Weekly, ...)`,完全等价)
  - `activity_progress` / `activity.xlsx` / `ActivityDefDoc` schema 字段集与 39 §3.2 / §3.1 一致(零字段加)
  - `MailDecisionHelper.SendMailTo` 签名零改;39 §3.4 发奖编排流程零改(`EvaluateAndClaim` 内部逻辑改的只是返 bool,不破既有行为)
- **V10 SV12 Code Review**:服务端遵 Fantasy.Net 约定(协议生成 / handler 注册 / MongoDB 存储 / 错误码非异常 / 不手改生成物 / 不手动注册);重点核 ① **身份从会话取 account**(非客户端字段);② **handler `type=Cumulative` 校验 + service 层兜底 type 校验**(双层防御);③ **`ActivityProgressService.IncrementAsync` 统一逻辑路径**(handler 校验通过后调,服务端业务方直调也调,不复制两份发奖编排);④ **counter 累加用 MongoDB `$inc` 原子**(沿 `ActivityEvalHelper.Increment` 既有 `$inc` 实做);⑤ **跨周期 counter 清零**(`ResetCounterIfCrossedPeriod` Daily/Weekly 通用,OneShot 跳过 — Daily/Weekly Cumulative 前瞻支持);⑥ **handler 校验顺序先 activityId 存在再 type**(防泄露信息差);⑦ **delta 上限钳制单次 10000**;⑧ **抢占必在 SendMailTo 之前**(`EvaluateAndClaim` 内 claim-then-act 既有不动);⑨ **发奖必经 32 §3.5 SendMailTo 入口**(`EvaluateAndClaim` 内调 MailDecisionHelper.SendMailTo 既有不动);⑩ **无新 MongoDB 集合 / schema 字段加**(沿用既有 activity_progress + activity_def);**无 既有协议改**(只新增 1 个 enum + 2 个 message)
- **V11 E1 全栈真往返**(力争):mock 客户端登录 + 推 100 次 `(5, 1)` → 服务端发活动邮件挂 reward=5003 → mock 客户端走 32 拉邮件列表见活动 5 邮件 → 领取得 5003 钻石礼包(若包含 +N 钻石货币 useEffect=1)→ 37 PropertyChangeRequest 加余额 → 38 PlayerAttrService 推送 G2C_PropertyDeltaPush → HUD 见钻石变化 → 44/45/46 ledger 写一行 source=MailClaim
- **V12 E2 跨会话**:已发过奖的账号重启服务端再推 `(5, 1)` → 服务端不重发(OneShot 永发停)→ `activity_progress {account}_5 {counter:101, LastClaimedCycleKey:1}`
- **V13 E3 全栈零回归**:既有六全栈(30/31/32/33/35/37)+ 客户端 38 + 39 第 1 子单 + 40/41 第 2 子单 + 43 第 3 子单 + 44/45/46 ledger 通路全部 PASS 不破

#### 协议变更同步检查

- **本子单新增 ActivityMessage.proto**(1 enum + 2 message)→ 双端生成物已同步:
  - 服务端:`examples/Server/APP/Entity/Generate/NetworkProtocol/{OuterMessage.cs, OuterEnum.cs, OuterOpcode.cs}`
  - 客户端:`D:/work/TEngine_block/UnityProject/Assets/Fantasy/Generate/NetworkProtocol/{OuterMessage.cs, OuterEnum.cs, OuterOpcode.cs, NetworkProtocolHelper.cs}`
- **双端编译过**:服务端 `dotnet build examples/Server/Server.sln` 0 error(已过);Unity 编辑器编译待 server-test 在 Unity 打开核验(PVC1) — 风险低(导出工具同一份 proto 同次产出,opcode `diff` 全量一致)
- **opcode 双端共识**:`diff` 两端 OuterOpcode.cs 输出空(已验);新 opcode `C2G_ActivityIncrement=268445457 / G2C_ActivityIncrementResponse=402663185`
- **本次 opcode 整体重排**:ActivityMessage.proto 排首字母 A 顶到首位,既有协议 opcode 全量整体下移。双端同次重生成共识,opcode 是运行期常量、非持久 wire 契约(沿 memory `proto-exporter-quirks` 第 2 条),双端一致即正确,不需逐条比对绝对值
- **客户端业务代码 zero diff**:`git status -- Assets/GameScripts/HotFix/` 空(已验)

### blockers

(无)

### 自检(server-dev 收尾,conventions + Fantasy 编译要求)

- [x] 改动每行可追溯到设计 47 § + plan decisions:协议 4 类(1 enum + 2 message)= §3.1 + §3.2 + §3.3;handler 校验顺序 = §3.3 mermaid + SV3/SV5/SV6/SV7/SV11/SV12 + decisions §1/§3/§6/§7;service 行为契约 = §3.5 mermaid + decisions §2/§3/§4;EvaluateAndClaim 返 bool = §3.5 (`targetReached` 字段需求供 §3.2 G2C 响应) + decisions §1
- [x] 不手改 .g.cs、不手动注册 Handler:协议两条 + enum 由导出工具产出;handler 类继承 `MessageRPC<TReq, TRes>` 由源生成器编译期注册(起服日志 + 编译通过即证)
- [x] FTask 不用 Task;sealed class:`C2G_ActivityIncrementHandler` 是 sealed class;`ActivityProgressService` 是 static class(辅助,非 Entity);所有 async 方法用 FTask / FTask<T> 返回(沿既有 ActivityEvalHelper / MailDecisionHelper 范式)
- [x] 编译 0 error(Server.sln net8.0 + net9.0 双 target 都过);0 本子单新增 warning(既有 21 warning 均与本子单文件无关)
- [x] 异常路径已留防护:① ActivityServiceComponent 未挂 / DefCache 空 → handler ServiceUnavailable + service ServiceUnavailable(双层);② Activity DefCache.TryGetValue 不命中 → handler InvalidRequest + service ServiceUnavailable(handler 侧客户端语义为「参数错」/ service 侧内部语义为「配置缓存与 handler 不同步」,各自合理);③ delta 上限钳制(handler 拦);④ delta ≤ 0(handler 拦);⑤ MongoException catch service ServiceUnavailable + Warning;⑥ 会话未挂账号 → handler ServiceUnavailable;⑦ Mail 服务未就绪 / SendMailTo 返 null → EvaluateAndClaim 内 Warning + 漏发窄窗(诚实边界,运营可补)
- [x] 改动提交进 Fantasy 仓库(`D:/work/TEngine_block/Fantasy/`);**唯一**跨仓库改动 = UnityProject `Assets/Fantasy/Generate/NetworkProtocol/` 客户端协议生成物 4 文件(由导出工具自动落)+ UnityProject `design-docs/`(plan 阶段设计稿 + 本子单同步重写 39 §3.1 type 字段)。两者均符合协议变更归属红线(客户端 Generate 同步落 UnityProject + 设计稿同步重写 sweep 闭合)
- [x] 改动散文(类头 doc-comment / 旁注)遵 conventions:无 diff 叙事 / 无指代词 / 设计基线只引设计稿章节号、不引代码符号文件路径越界 / 与既有同源邻条款(ActivityServiceComponentSystem / ActivityEvalHelper / C2G_QueryAttrLedgerHandler / C2G_PropertyChangeRequestHandler)风格一致 / 不用拟人/口语比喻
- [x] 通过 OnLogin 路径 `_ = await EvaluateAndClaim(...)` 守 Login 节律行为 100% 等价(SV11 零回归 Daily / OneShot / Weekly 三种 cycle 既有逻辑)
- [x] `ResetCounterIfCrossedWeek` 改内部代理调通用 `ResetCounterIfCrossedPeriod` — Weekly 行为完全等价(SV11 守活动 4 周累计 5 天行为不变);Daily/OneShot 路径在 OnLogin 内既有不触发清零(`if (def.Cycle == CycleWeekly)` 守门),零行为漂移
