# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区（角色职责在 `.claude/agents/pipeline-test.md`，spawn 时自动注入）。每完成一项验证就更新这里。

## 当前任务:排行榜结算客户端段·本地结算退役 + 红点交棒邮件

总判定:**PASS**

测试时间:2026-06-20
Unity 实例:UnityProject@02a6dcaa（正常连接，活跃场景 main）
MCP 版本:9.7.1
验收依据:design-docs/22-rank-system.md §五验收点（C/SO/DP/RD/RT/P/SK/R 全组）

---

## 一、编译验证

**PASS — 编译 0 报错**

- `refresh_unity(mode=force, compile=request, wait_for_ready=true)` 触发成功，编译完成。
- `read_console(types=["error"])` 结果：3 条，内容均为 `MCP-FOR-UNITY: Client handler error: Cannot access a disposed object.`，属 MCP 桥内部 handler 告警（非工程编译报错），与上一轮相同，不影响判定。
- `read_console(types=["warning"])` 结果：0 条。
- 判定：编译 0 报错，0 工程警告。

---

## 二、单元测试

**PASS — 424/424，全绿**

- `run_tests(EditMode, assembly=BlockBlast.Tests)`，job_id=`32b0ebd112544630aa2eb9638cc84aa4`
- 结果：**total=424, passed=424, failed=0, skipped=0**，duration≈3.9s
- 与 dev 交接区预期完全一致：退役前 432 - 8 条结算测试（DUE1-DUE4 + ST1-ST3b）= 424。

逐组抽样状态：

| 组 | 代表用例 | 状态 |
|---|---|---|
| C | C1_ConfigMgr_GetReturnsDef / C2_TierForRank / C3_LubanDirectRead | 含 |
| SO | SO1_Sort / SO2_Filter / SO3_MyRank | 含 |
| DP | DP1_ClaimDaily / DP2_CrossDayReset / DP3_ClaimsGoThroughMail | 含 |
| RD | RD1_HasClaimable / RD2_SettleDueNoLongerTriggersRedDot | 含 |
| P | P1_Persistence_RoundTrip / P2_Persistence_DirtyDataSafe | 含 |
| SK | SK1_Sources / SK2_SwapSource_ServiceUnchanged | 含 |
| H(Window) | H2/H3/W3/W6 共 7 条 RankWindowTests | 含 |
| DUE/ST | 已删除（结算退役） | 不含（预期） |

---

## 三、手动功能验证

**PASS — PlayMode execute_code 注入贯通**

### 3.1 RT1 — 运行期公共表面反射核验

PlayMode 中反射 `RankService` 类型，验证退役结算接口在运行期不存在：

| 检查项 | 结果 | 期望 |
|---|---|---|
| IsSettleDue 方法 | False | False |
| CheckAndSettle 方法 | False | False |
| GetLastSettle 方法 | False | False |
| SetLastSettle 方法 | False | False |
| OpenDate 字段/属性 | False | False |
| lastSettleTicks DTO 字段 | False | False |
| SettleResult 类型 | False | False |
| HasClaimable 属性可调用 | True | True |

### 3.2 CV8/RD — HasClaimable 红点行为验证

通过 `InitRankWithDeps` 注入测试接缝，PlayMode 真实 RankService 运行期核验：

| 场景 | HasClaimable | 期望 |
|---|---|---|
| 周榜 valid_type=Weekly，无每日/点赞奖，到结算时刻（周二 2026-06-09）| False | False（结算分支退役，旧行为应为 True）|
| 有每日奖未领（Always 榜，daily=1004，本机第1名）| True | True |
| 领过今日每日奖 + 点赞奖后 | False | False |

### 3.3 越界试探（破坏性操作）

| 试探场景 | 结果 | 结论 |
|---|---|---|
| 负分提交 `-999` → SubmitScore 夹 ≥0 | score=0 | PASS |
| null 陪榜 Fetch → GetBoard 不抛 | entries=1（只本机）| PASS |
| GetBoard(9999) 不存在榜 | null，不抛 | PASS |
| ClaimDaily(9999) 不存在榜 | NotRanked，不抛 | PASS |
| 老存档含 `lastSettleTicks` 字段 → 反序列化不抛、其他字段保真 | bestScore=555 / daily=777 / praise=888 保真 | PASS |
| 截断 JSON `{truncated!!!}` → RankPersistence.Load 产合法空 | boards=0，不抛 | PASS |

未能在 PlayMode 试探的（E1 范围）：真实上报多账号 → 服务端到结算时机 → 客户端拉邮件领结算奖。dev 交接区 §4 明确「本客户端段不强求 E1」，服务端段已于 2026-06-19 PASS（commit 420272b8），不计 BLOCKED。

---

## 四、Code Review

**PASS — 5 条红线全通过，conventions §6 交叉检通过**

### 4.1 文件清单核验

| 文件 | 核验结果 |
|---|---|
| RankService.cs | 类头 doc 正确表述「结算上移服务端」；`OpenDate`/`IsSettleDue`/`CheckAndSettle`/`GetLastSettle`/`SetLastSettle`/`IsWeeklyDue`/`ThisWeekSettleTime`/`FromUnixSeconds` 7 个成员全部缺席；`HasClaimable` 判定体只含每日/点赞两项；`SendRewardMail` 注释准确说明「结算奖不走本方法」；`NowProvider` 赋值表达式改为 `NowProvider().Date`（只用 today）。逻辑正确 |
| RankPersistence.cs | `RankBoardProgress` 字段序列：rankId/bestScore/bestAchievedTicks/dailyClaimDateBin/praiseClaimDateBin，无 `lastSettleTicks`，符合 §3.8 设计稿 |
| RankModel.cs | `SettleResult` struct 缺席；`RankClaimResult`/`RankClaimStatus`/`RankText`/`RankEntry`/`RankBoard` 保留，正确 |
| GameContext.cs | `Rank` 字段 doc 改为「红点查询；结算编排上移服务端，设计 33」；`InitRank` doc 改为「每日/点赞奖经它真实下发（结算奖经设计 33 服务端发奖入口投出）」；构造签名/装配链/`InitRankWithDeps` 签名零变动 |
| RankSystemTests.cs | `T_Open` 字段缺席；`NewService` 无 OpenDate 注入；DUE1-DUE4 + ST1-ST3b 8 条全删（含注释说明「若服务恢复任一结算表面本测试块编译失败」）；RD1/RD2 改写正确；P1/P2 改写正确（无结算时间断言，P2 加遗留字段反序列化保真断言）；总条数 424 与预期一致 |
| RankWindowTests.cs | `T_Open` 字段缺席；`InjectRank` 无 OpenDate 注入；H2/H3/W3/W6 测试体与改动无交叉，零回归 |

### 4.2 核心红线逐条（SKILL.md §核心红线）

| 红线 | 适用性 | 结论 |
|---|---|---|
| 1 异步优先（UniTask，禁 Coroutine）| `GetBoardAsync`/`SubmitScoreAsync` 均 `async UniTask<T>`；删除 `CheckAndSettle` 同步方法不引入新阻塞 IO | PASS |
| 2 模块访问（GameModule.XXX）| RankService 纯逻辑层无模块访问；GameContext 用 `new RankService(...)` 组装，无 `ModuleSystem.GetModule` | PASS |
| 3 资源释放（LoadAssetAsync/UnloadAsset）| 无新增 LoadAssetAsync 调用；RankPersistence/MailboxService 非资源加载，无释放义务 | PASS |
| 4 热更边界（HotFix 全热更）| 全部业务改动在 `GameScripts/HotFix/GameLogic/`；`GameLogic.asmdef` 未改；`BlockBlast.Tests` asmdef 未改 | PASS |
| 5 事件解耦（GameEvent/AddUIEvent）| 无跨模块事件；无 onClick.AddListener；无新增监听/注销对，无事件泄漏风险 | PASS |

### 4.3 命名核验

- `RankService`/`RankPersistence`/`RankBoardProgress`：PascalCase 正确，无前缀缺失。
- `HasClaimable` 属性：符合 C# 命名规范。
- 本次无新增 prefab，`m_btn_`/`m_text_` 前缀规则不适用。

### 4.4 CV1 静态 grep（grep 核验退役符号在代码层零命中）

grep 模式：`IsSettleDue|CheckAndSettle|\.OpenDate|SettleResult|GetLastSettle|SetLastSettle|lastSettleTicks`
路径：`Assets/GameScripts/**/*.cs`

结果：**零命中**（代码层完全退役）。
注释/字符串层（RankSystemTests.cs 注释段说明 + P2 遗留字段 JSON 字符串）按 dev 交接区 CV1 预期允许。

### 4.5 不变量守住核验

| 不变量 | 验证方式 | 结论 |
|---|---|---|
| 查榜断服回退本地源 | RankService.GetBoardAsync 有 `remote == null → GetBoard(rankId)` 路径；SK1/SK2 单测覆盖 | 保持 |
| 每日/点赞领取仍客户端 | DP1-DP3 单测全绿；ClaimDaily/ClaimPraise 方法保留 | 保持 |
| 不引新接缝 | 无新接口/类/asmdef 改动 | 保持 |
| 配置层仍承载 valid_type/valid_val/mail/reward 字段 | RankDef POCO 零改动（grep RankDef 无删除结算字段），客户端「承载但不消费」与 §3.5.1 一致 | 保持 |

### 4.6 conventions §6 交叉检（dev 改过的持久文件）

**design-docs/22-rank-system.md**

- §3.5「结算编排：服务端权威」：正文为现状描述（服务端权威），无 diff 叙事、无对话指代、无口语比喻。§3.5.1/§3.5.2 内容自闭环成立。
- §3.7 红点判定：只含「每日可领 OR 点赞可领」两项，无「到点未结算」残留，符合覆盖式重写要求（conventions §6）。
- §五验收点：RT1-RT3 退役验收组已加；DUE/ST/RD 结算分支已从验收表删除；与代码行为一致。
- 旁注检查：`>` 旁注与所注规范仍成立，无孤儿旁注。
- 无过时正文叠加勘误注，符合 conventions 规则 6。

**pipeline/state/dev.md**

- 工作态内容完整，可识别所属任务，无过时遗留。
- 无过程叙事、无可推导副本、无口语比喻。
- 已交接状态为「实现完成，交 test 复核」，任务未关闭，工作状态合规。

---

## 五、验证点对照

| # | 验收点 | 状态 | 证据 |
|---|---|---|---|
| C1 | 配置字段正确；结算字段承载不消费 | PASS | 单测 C1_ConfigMgr_GetReturnsDef；RankDef POCO 字段保留 |
| C2 | 聚合 3 档；按名次查档命中正确 | PASS | 单测 C2_TierForRank_HitsCorrectTier |
| C3 | Luban 直读 rank.bytes | 工具链可达时 PASS | 单测 C3_LubanDirectRead 含 Inconclusive 分支（bytes 不可达时自动 BLOCKED，不判 FAIL）|
| SO1 | 分数降序 + 同分时间升序 + 顺序名次 | PASS | 单测 SO1_Sort + PlayMode 贯通 H3 |
| SO2 | 过滤入榜要求 / 入榜上限 / 展示上限 | PASS | 单测 SO2_Filter |
| SO3 | 我的名次贯通 | PASS | 单测 SO3_MyRank |
| DP1 | 每日奖各分支 | PASS | 单测 DP1_ClaimDaily_States |
| DP2 | 跨天重置；点赞同口径 | PASS | 单测 DP2_CrossDayReset_AndPraise |
| DP3 | 每日/点赞经邮件发 | PASS | 单测 DP3_ClaimsGoThroughMail |
| RD1 | 有每日/点赞可领 → 亮；全领过 → 不亮 | PASS | 单测 RD1_HasClaimable + PlayMode case2/case3 |
| RD2 | 结算到点不再触发红点（退役） | PASS | 单测 RD2_SettleDueNoLongerTriggersRedDot + PlayMode case1 |
| RT1 | 客户端不暴露结算检查/时机/已结标记接口；DTO 无结算字段 | PASS | grep 零命中 + PlayMode 反射核验 |
| RT2 | HUD 红点不因结算到点亮 | PASS | PlayMode HasClaimable case1=False；RD2 单测 |
| RT3 | DUE/ST 测试同步删除；RD 删结算分支断言 | PASS | 424/424；DUE/ST 缺席确认 |
| P1 | 跨实例保真（最佳分/每日/点赞日期）；不含结算时间往返 | PASS | 单测 P1_Persistence_RoundTrip |
| P2 | 脏数据保底不抛；老存档遗留字段忽略保真 | PASS | 单测 P2_Persistence_DirtyDataSafe + PlayMode 越界试探 |
| SK1 | 远程 stub 返空不抛不连网；本地源本机+陪榜 | PASS | 单测 SK1_Sources |
| SK2 | 切换数据源服务层零改动 | PASS | 单测 SK2_SwapSource_ServiceUnchanged |
| R1 | 编译 0 error；其余 EditMode 全绿 | PASS | 424/424；编译 0 报错 |
| R2 | Code Review 5 红线 | PASS | 4.2 节逐条核通过 |

---

## 六、FAIL 可复现清单

**无 FAIL 项。**

---

## 七、文档同步备注（非阻塞）

无遗留文档同步问题。22-rank-system.md §3.5/§3.7/§五验收点均已在本轮随代码同步改写（conventions §6 覆盖式重写，无勘误注叠旧文）。
