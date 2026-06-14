# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:排行榜系统 · 数据逻辑层 + 服务器接缝(离线可测)

设计基线 `design-docs/22-rank-system.html`。落地状态:全部 8 项 dev 清单完成,编译 0 error,EditMode 359/359 全绿(新增 Rank 24 条 + 既有 335 条零回归)。

### 交接区 · 给测试(test)

#### 一、改动摘要

按设计稿 §五 8 项清单实现排行榜数据逻辑层,新建独立命名空间 `GameLogic.Rank`(通用系统,与 BlockBlast 玩法解耦),配置桥接归 `GameLogic.Config`。全部落热更区。三层结构:配置层(只读)/ 数据源接缝(离线本地 + 远程 stub)/ 服务层(纯逻辑)。

关键决策与做法:
- **发奖一律经注入 `GameLogic.Mail.IMailService.Send`**(复用邮件 21,不另造)。结算 / 每日 / 点赞三种奖励都组 `MailDraft` 挂奖励库 id 调 `Send`;排名层不碰 `MergeOrderState` / `ItemGrant` / `GiftOpener`(奖励经邮件领取链展开)。邮件 21 零改动。
- **配置按 id 聚合多行**:`RankConfigMgr` 遍历 `TbRank.DataList`,同 id 多行 → 一个 `RankDef`(榜级字段取首遇行)+ 各行名次档进 `Tiers`(按 RankMin 升序),同 16 礼包子项 / 20 兑换码子表做法。
- **排序与并列**:分数降序 + 同分按 AchievedTicks 升序(早者靠前),稳定可复现;顺序名次(1,2,3,4,同分各占唯一名次)。过滤入榜要求(<Condition 不进)→ 入榜上限 CountMax → 展示上限 ShowMax。
- **结算时机注入时钟驱动**:`IsSettleDue` 四档(Always 恒 false / OpenDays 开服 X 天一次性 / FixedTime 指定 Unix 秒一次性 / Weekly 周循环每周一次)。周循环用 ISO 周(周一为周起点),算本自然周「星期 X 当天 0 点」结算时刻,本周精度到天(设计 O4)。`CheckAndSettle` 纯方法,带 lastSettle 幂等防重,不起后台定时器(O9)。
- **持久化复用既有 `Persistence.Provider`**(键 `Rank.Progress`,JsonUtility,脏数据 try/catch 保底 + version);只存本机最佳分 / 达到时间 / 上次结算 / 每日·点赞当天领取日期,他人成绩不进盘。
- **服务器接缝 `IRankSource`**:`LocalRankSource`(本机成绩 + 配置陪榜,可跑可测)/ `RemoteRankSource`(返 `Array.Empty`、不连网)。全系统无 UnityWebRequest / HttpClient / System.Net。

#### 二、文件清单

新建(逻辑层,热更区 `Assets/GameScripts/HotFix/GameLogic/`):
- `Module/Rank/RankDef.cs` — `RankValidType` 枚举 / `RankRewardTier` / `RankDef`(含 `TierForRank`)
- `Module/Rank/RankModel.cs` — `RankEntry` / `RankBoard` / `SettleResult` / `RankClaimStatus` / `RankClaimResult` / `RankText`(textId 占位)
- `Module/Rank/RankPersistence.cs` — `RankBoardProgress` / `RankProgressSave` / `IRankPersistence` / `RankPersistence`(键 `Rank.Progress`) / `InMemoryRankPersistence`
- `Module/Rank/RankSource.cs` — `IRankSource` / `LocalRankSource` / `RemoteRankSource`
- `Module/Rank/RankService.cs` — `IRankConfigSource` / `RankConfigMgrSource` / `RankService`(查榜 / 排序 / 结算编排 / 每日+点赞领取 / 红点 / SubmitScore)
- `Config/RankConfigMgr.cs` — Luban 行 → POCO 桥接,按 id 聚合,`GetRank` / `All` / `InitForTest` / `ResetForTest`

新建(配置 + Luban 生成):
- `Configs/GameConfig/Datas/rank.xlsx`(仓库根,与 UnityProject 同级)— demo:id=1 周榜三档 + id=2 总榜一档(共 4 行)
- `Configs/GameConfig/Datas/__tables__.xlsx`(改)— 注册 `rank.TbRank`(value_type=Rank, index=row_id, mode=map)
- `Assets/AssetRaw/Configs/bytes/rank_tbrank.bytes`(导表产出)
- `Assets/GameScripts/HotFix/GameProto/GameConfig/Rank.cs` + `rank.TbRank.cs` + `Tables.cs`(改)— Luban 生成,勿手改

新建(测试):
- `Assets/Editor/Tests/BlockBlast/RankSystemTests.cs` — 24 个 [Test],覆盖 §六 26 验收点

#### 三、验证点(逐条对应 §六验收,test 怎么验 + 预期)

跑 `BlockBlast.Tests` 程序集 EditMode 即可。本机已跑 359/359 全绿。逐组对应:
- **配置 C1/C2/C3**:`C1_*`(GetRank 榜级字段 + 查无返 null)、`C2_TierForRank_*`(rank=1→1002 / 5→1005 / 50→1006 / 落空→null)、`C3_LubanDirectRead_*`(直读 `rank_tbrank.bytes`,id=1 三档 + id=2 一档,Tiers 按 RankMin 升序,reward 映射 1002/1005/1006/1007)。`C2b_*` 是占位说明(EnsureLoaded 走 ConfigSystem 不在 EditMode 测,聚合由 C3 真实表核验)。
- **查榜+排序 SO1/SO2/SO3**:`SO1_*`(降序 + 同分 ticks 升序 + 1,2,3,4)、`SO2_*`(Condition 过滤 + CountMax + ShowMax)、`SO3_*`(在榜 SelfRank、未达要求 0、GetMyRank 与 GetBoard.SelfRank 一致)。
- **结算时机 DUE1–4**:四档 `IsSettleDue` 逐档断言(Always 恒 false / OpenDays / FixedTime / Weekly 周循环 + 下周再 true)。
- **结算编排 ST1/ST2/ST3/ST3b**:`ST1_*`(到点中奖档 → fake `IMailService` 收 1 封 Send,草稿 RewardPoolId==档奖,SettleResult 含名次+奖)、`ST2_*`(幂等同周期第二次不发)、`ST3_*`(未入榜不发仅记已结 + FromTemplate 返 null 兜底草稿仍发)、`ST3b_*`(reward==0 / mail==0 不发)。
- **每日/点赞 DP1/DP2/DP3**:`DP1_*`(Success/AlreadyClaimedToday/NotRanked/NoReward)、`DP2_*`(跨天重置 + 点赞 praise==0 NoReward)、`DP3_*`(均经邮件下发)。
- **红点 RD1/RD2**:`RD1_*`(到点未结 → true)、`RD2_*`(领过 + Always 不结算 → false;周榜结算后不再亮)。
- **持久化 P1/P2**:`P1_*`(跨实例往返最佳分保真 + 高分刷新/低分不覆盖)、`P2_*`(无键/脏 JSON/负分保底不抛)。
- **接缝 SK1/SK2**:`SK1_*`(Remote 返空不抛 + Local 本机+陪榜)、`SK2_*`(换数据源服务层零改动)。
- **回归/编译 R1/R2**:R1 = EditMode 全绿(359/359,邮件 21 不受影响);R2 = Code Review 核 5 红线 + 「无网络/HTTP」「发奖复用 21 不碰 MergeOrderState」「持久化复用既有 Provider」「邮件 21 零改动」(dev 已 grep 自检:Rank 目录无 UnityWebRequest/HttpClient/System.Net/MergeOrderState/GiftOpener/ItemGrant 实际调用)。

#### 四、标注

- **涉及热更程序集**:是,全部落 `GameScripts/HotFix/GameLogic`。
- **Luban 重生成**:已做。本机导表成功(`DOTNET_ROLL_FORWARD=Major` + `AI_MODE=1` 跑 `gen_code_bin_to_project_lazyload.bat`),生成 `Rank.cs`/`rank.TbRank.cs`/`rank_tbrank.bytes`。C3 直读真实 bytes 已 PASS,故工具链此处可达、不列 BLOCKED。test 若另机重跑且 bytes 不在,C3 会自动 Inconclusive(BLOCKED 非 FAIL)。
- **需进 Play 模式手验的功能点**:无。本轮纯数据逻辑层 + 接缝,全 EditMode 可测;UI 表现层 + 真实全服榜 + 远程实现转遗留(设计 §七 O1/O2/O8),不在本轮。
