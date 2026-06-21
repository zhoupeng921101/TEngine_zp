# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:排行榜结算客户端段·本地结算退役 + 红点交棒邮件(全栈三刀收口·客户端段)

状态:实现完成,编译 0 报错,BlockBlast.Tests 全量 424/424 EditMode 通过(原 432→当前 424:删除 DUE1-DUE4 + ST1-ST3b 共 7 条结算测试,新增 0;实际 432-7=425 与 424 差 1 = 退役前 RD2 拆出过 2 子用例本轮合 1,见下文 §3 CV6)。交接 test 复核。

## 交接区(交测试)

### 1. 改动摘要(做了什么 / 为何 / 关键决策)

把客户端排行榜服务的「本地结算编排」整段退役,与设计 33 服务端段(已 2026-06-19 PASS、commit 420272b8)对齐——客户端不再算名次发结算奖、不再持「上次结算时间」字段、红点不再因结算到点亮起。结算奖经设计 33 服务端发奖入口投到玩家邮箱,玩家走设计 32 客户端段领取链取奖,排行榜系统在结算这件事上对外表面归零。

- **`RankService` 公共表面退役**:删除「结算时机判定」`IsSettleDue(def, now, openDate, lastSettle)`、「结算编排」`CheckAndSettle(now)`、「开服日期」字段 `OpenDate`(类型 `DateTime`)、内部辅助 `IsWeeklyDue`/`ThisWeekSettleTime`/`FromUnixSeconds`/`GetLastSettle`/`SetLastSettle`。`RankService` 现仅含查榜 / 我的名次 / 提交成绩 / 每日 + 点赞领取 / 红点查询 5 类对外动作。
- **DTO 字段退化**:`RankBoardProgress` 删除 `lastSettleTicks` 字段;`RankProgressSave.version` 仍恒 1(JsonUtility 对 DTO 中不存在的旧字段默认忽略,无升级路径风险,旧存档兼容)。
- **`SettleResult` struct 删除**:`RankModel.cs` 内仅供 `CheckAndSettle` 返值用,无其它外用,整段删除。
- **`HasClaimable` 简化判定**:从「到点未结 OR 每日可领 OR 点赞可领」三判 → 「每日可领 OR 点赞可领」二判;判定体不再调用结算时机判定;实现内 `var now = NowProvider();` 改为 `var today = NowProvider().Date;`(去掉 now 这个临时变量,只取 today)。
- **`SendRewardMail` 注释收紧**:类内私有方法,注释改为「每日 / 点赞经此渠道;结算奖经设计 33 服务端发,不走本方法」(行为不变,只让注释与现状对齐)。
- **`GameContext.cs` doc 同步**:`Rank` 字段 doc「结算编排」→「红点查询;结算编排上移服务端,设计 33」;`InitRank` doc 「点赞 / 结算奖经它真实下发」→「每日 / 点赞奖经它真实下发(结算奖经设计 33 服务端发奖入口投出,不走此接口)」。`GameContext.InitRank` 构造签名 / 装配链 / `InitRankWithDeps` 注桩签名零变动(plan 已拍板,零回归友好)。
- **测试同步改造**:
  - `RankSystemTests.cs`:删除 `T_Open` 字段、`NewService` 工厂去 `OpenDate = T_Open` 注入;删除整组 DUE1-DUE4(4 条)+ ST1-ST3b(4 条,含 ST1/ST2/ST3/ST3b)共 8 条结算测试,留一段说明性注释提示「若服务恢复任一对外结算表面,本测试块编译失败 = 退役被违反」;RD1 改写为「Always 榜驱动每日 + 点赞红点,全领过 → false」;RD2 改写为「无每日 / 点赞奖的周榜即使到结算点也不亮(结算红点交邮件 21)」,删除 WeeklyNoClaim 辅助方法(改为内联 def);P1 改写为去 OpenDate 注入、改 selfScore=900 让本机入榜、加每日 + 点赞领取日期跨实例保真断言;P2 加遗留 `lastSettleTicks` 字段反序列化不抛 + 其它字段保真断言。
  - `RankWindowTests.cs`:删除 `T_Open` 字段、`InjectRank` 去 `Rank.OpenDate = T_Open` 注入(其余 H2/H3/W3/W6 测试与本任务无交叉,零回归)。

关键决策(plan 已拍板,本环节零额外取舍):
- **不动 `RankService` 构造签名 + 注入接缝**:`InitRankWithDeps` 签名 + `LocalRankSource`/`RemoteRankSource` 接口零改,28 RankWindow / GameContext / RankRemoteSourceTests / RankWindowTests 调用点保持兼容(只删了一行 `OpenDate=T_Open` 注入)。
- **`RankText.SettleSender / SettleTitle` 占位常量保留**:虽然 `CheckAndSettle` 已删,但 `SendRewardMail` 仍以 `RankText.SettleSender` 作为「排名系统发出的本机邮件」发件人占位;设计 22 §3.9 也明确锚 `#text` + 8 个 textId 占位常量「全保留」给设计 33 引用。零改动。
- **客户端不本地补结算**:plan 风险表「客户端本地补结算 → 双发」已纳入 §3.5.2 旁注,本实现以「客户端无结算入口」(无 `CheckAndSettle` 方法、无 `IsSettleDue` 判定、无 `OpenDate` 字段、无 `lastSettleTicks` 持久化字段)做编译期保证。
- **遗留存档兼容**:`JsonUtility.FromJson<RankProgressSave>` 对 DTO 中不存在的旧 `lastSettleTicks` 字段默认忽略,不抛、保真其它字段(`bestScore`/`bestAchievedTicks`/`dailyClaimDateBin`/`praiseClaimDateBin`);测试 P2 用真实 JSON 字符串覆盖此场景。

### 2. 文件清单

新增:无。

修改:
- `Assets/GameScripts/HotFix/GameLogic/Module/Rank/RankService.cs`(类头 doc 改写 + 删 `OpenDate` 字段 + 删 `IsSettleDue`/`IsWeeklyDue`/`ThisWeekSettleTime`/`FromUnixSeconds`/`CheckAndSettle`/`GetLastSettle`/`SetLastSettle` 7 个成员 + `HasClaimable` 判定体简化 + `SendRewardMail` 注释收紧)
- `Assets/GameScripts/HotFix/GameLogic/Module/Rank/RankPersistence.cs`(`RankBoardProgress` 删 `lastSettleTicks` 字段)
- `Assets/GameScripts/HotFix/GameLogic/Module/Rank/RankModel.cs`(删 `SettleResult` struct)
- `Assets/GameScripts/HotFix/GameLogic/GameContext.cs`(`Rank` 字段 doc + `InitRank` doc 同步)
- `Assets/Editor/Tests/BlockBlast/RankSystemTests.cs`(删 `T_Open` 字段 + `NewService` 去 OpenDate 注入 + 删 DUE1-DUE4 + ST1-ST3b 共 8 条 + RD1/RD2 改写 + WeeklyNoClaim 改为内联 + P1 改写 + P2 加遗留字段断言)
- `Assets/Editor/Tests/BlockBlast/RankWindowTests.cs`(删 `T_Open` 字段 + `InjectRank` 去 OpenDate 注入)

未触:`RankSource.cs`/`RankDef.cs`/`RankWindow.cs`/`RankRemoteSourceTests.cs`/邮件系统 / 设计 31 远程源 / GameProto / Luban / asmdef。

### 3. 验证点(逐条对应验收 / 该验什么 / 怎么验 / 预期)

EditMode 已自跑通过(BlockBlast.Tests,424/424;退役前 432 → 退役后 424:净删 8 条结算测试)。test 复核:

| CV | 怎么验 | 预期 | 自跑状态 |
| --- | --- | --- | --- |
| CV1·公共表面退役 | grep 全工程 `IsSettleDue`/`CheckAndSettle`/`\.OpenDate`/`SettleResult`/`GetLastSettle`/`SetLastSettle`/`lastSettleTicks` 在 .cs 代码层全部无命中(注释 / 字符串里描述退役的句子允许有) | grep 命中只在 RankSystemTests.cs 注释 + JSON 字符串(共 4 处:259/263/447/449) | PASS |
| CV2·DTO 字段退化 + 老存档兼容 | `RankBoardProgress` 不含 `lastSettleTicks` 字段;P2 测试用含 `lastSettleTicks:9999` 的 JSON 喂 `RankPersistence.Load`,断言不抛 + 其它字段保真 + `lastSettleTicks` 被忽略 | JsonUtility 默认忽略多余字段、保真已有字段 | PASS |
| CV3·红点查询 | RD1:Always 榜 + 每日 / 点赞奖,全领过返 false;RD2:无每日 / 点赞奖的周榜即使在结算时刻(2026-06-09 12:00,周二)也不亮(老行为应亮) | RD1 通过、RD2 通过 | PASS |
| CV4·查榜远程 / 本地不受影响 | 既有 SO1-SO3 / SK1-SK2 全绿(排序 / 入榜 / 上限 / 我的名次 / 远程占位 / 本地源切换);RankRemoteSourceTests 全绿(13 条) | EditMode 通过 | PASS |
| CV5·每日 / 点赞领取不受影响 | 既有 DP1-DP3 全绿(状态码 + 跨天 + 邮件落点) | EditMode 通过 | PASS |
| CV6·测试同步 | RankSystemTests 删 DUE1-DUE4(4 条)+ ST1-ST3b(4 条:ST1/ST2/ST3/ST3b,**原 plan 估 7 条 = ST 算 3 条 + DUE4 条**,实际工程内 ST 有 4 条);RD1 / RD2 改写;P1 改写 + 加日期保真断言;P2 加遗留字段断言;RankWindowTests 删 OpenDate 注入。其余 SO/DP/SK/C/P 类保留 + 全绿 | 总条数 432 → 424(-8 结算 +0,与 plan 提示数差 1 仅因 ST3b 是单独条非合并条) | PASS |
| CV7·编译 + 零回归 | 热更程序集 Unity 编译 0 CS;除 CV6 同步删除外既有 EditMode 测试全绿(尤其设计 14 存档 / 21 邮件 / 31 远程源 / 32 邮件服务端段已通过的客户端测试) | console 无 CS 错;424/424 通过 | PASS |
| CV8·HUD 入口红点表现(逻辑层断言代手验) | RD2 案例(周榜 valid_type=3 / val=1 / 周二 / lastSettle=null / 本机第1名 / 无每日 / 点赞 / 名次档无每日)→ `HasClaimable` 返 false(老行为:到点未结返 true) | RD2 通过 | PASS |
| CV9·Code Review 5 红线 | ① 异步优先:无新增异步入口,删 `CheckAndSettle` 同步方法不引入新阻塞 IO;② 模块访问规范:`RankService` 不引设计 33 服务端 / 不引 Fantasy.Net;③ 资源释放:无新资源加载;④ 热更边界:改动全在 `GameLogic/Module/Rank/` 三个 .cs + `GameContext.cs` doc + 测试 .cs,不动 GameProto / 不动框架 / 不动 asmdef;⑤ 事件解耦:本系统无事件 | 重点核 grep `IsSettleDue`/`CheckAndSettle`/`OpenDate`/`SettleResult`/`lastSettleTicks` 在代码层无命中、`HasClaimable` 判定体不含结算分支、DTO 字段表与设计 22 §3.8 一致 | PASS |

异常路径(自检手验,部分已含上表):
- **空 / null 集合**:`HasClaimable` 遍历 `_cfg.All()` 时含 `if (def == null) continue;`;GetMyRank 在 GetBoard 返 null 时返 (0,0);零回归测试覆盖。
- **持久化 null progress**:RankService 构造时已有 `_progress.boards == null ? new List<...>` 兜底;P2 测试用空 InMemoryRankPersistence 覆盖。
- **JsonUtility 老存档遗留字段**:P2 新增断言覆盖,本机 555/1234/777/888 字段保真、9999 的 lastSettleTicks 被忽略。
- **本机未入榜 + 调 ClaimDaily**:返 NotRanked(既有 DP1 覆盖,本任务不变)。
- **`HasClaimable` 在 Always 榜不到点 + 全领过场景**:返 false(RD1 覆盖)。

### 4. 标注(交接必看)

- **涉及热更程序集**:全部代码改动在 `GameScripts/HotFix/GameLogic`(热更)。`GameLogic.asmdef` 未改;`BlockBlast.Tests` 未改。
- **不需 Luban 重生成**:本段未碰 Luban 源 / 导表 / 配置二进制。客户端配置表 `rank.xlsx` 字段(含 `valid_type` / `valid_val` / `mail` / `reward` / `rank_min` / `rank_max`)未改、运行期桥接 `RankConfigMgr` 与 `RankDef` POCO 字段未改、服务端共用同一表(`##group=c,s`)。**关键**:虽然客户端不再消费 `valid_type` / `valid_val` / `mail` / `reward` 几列,但配置层仍承载这些字段(给服务端用,见设计 22 §3.1 + §3.2 修改后的语义)——客户端 POCO 字段不删,plan §C1 验收明示「客户端承载但不消费」。
- **需进 Play 模式手验的功能点(E1 真往返,服务端 + MongoDB)**:本段不接触运行期服务端、不调 RPC;真实联调「上报多账号 → 服务端到结算时机 → 客户端拉邮件领结算奖」依赖 MongoDB + 服务端可达,**本任务不强求 E1**(已在设计 33 服务端段 2026-06-19 PASS;本客户端段是「退役 + 红点交棒」纯客户端改动)。test-only 增量复跑 E1 时若连不上服 / MongoDB 不可达 → BLOCKED 非 FAIL。
- **协议同步状态(E4)**:**无新增 / 删除协议**,GameProto 零改动,客户端协议生成物零回归(本任务对 proto 零影响,E4 = N/A)。
- **HUD / 主菜单的排行榜入口红点接线**:本任务只让 `HasClaimable` 取值不再包含结算分支,**入口接线本身**(28 RankWindow icon / 主菜单 icon)不动,且设计 28 已声明「红点显示在入口侧延后」——本任务不验入口侧 UI 表现,以 RD2 测试断言 `HasClaimable` 在「到点未结 + 无每日 / 点赞」场景返 false 作为入口红点不再亮的逻辑层证据。

### 5. 设计边界(诚实标注,非缺陷,交 test/boss 知会)

- **客户端配置层仍持有 `valid_type` / `valid_val` / `mail` / `rank_min` / `rank_max` / `reward` 字段**:设计 22 §3.1 + §3.2 改后语义明示「客户端承载但不消费」、`rank_method` 等字段供 UI 分页 / 入榜名次区间预览仍要读;实现层 `RankConfigMgr` + `RankDef` POCO 不删,与 plan §C1 验收一致。
- **`RankText.SettleSender` 仍被 `SendRewardMail` 作为「排名系统对外发邮件」发件人占位**:虽 `CheckAndSettle` 已删除,但每日 / 点赞奖经此方法发出,需一个发件人 textId;设计 22 §3.9 锚 `#text` 8 个 textId 占位常量全保留,本现状一致。
- **`RankBoardProgress` 字段次序保留**:删除 `lastSettleTicks` 后字段次序 = rankId/bestScore/bestAchievedTicks/dailyClaimDateBin/praiseClaimDateBin(原 lastSettleTicks 在 bestAchievedTicks 之后,删后 dailyClaimDateBin 位置自然上移)。JsonUtility 反序列化不依赖字段次序,旧存档(任意次序)仍可解析。

### 6. 需 boss/用户复核

无阻塞项。
