# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:排行榜系统 · 数据逻辑层 + 服务器接缝(离线可测)

设计稿:`design-docs/22-rank-system.html`(已写,nav.js GROUPS 加 22 一项,全库侧边栏 23 + 首页卡片 18 自动同步,断链/锚点/BOM/node --check 全过,浏览器预览渲染正确零控制台错误)。源 spec:`C:\Users\pc\Downloads\1007排行榜底层-----.xlsx`(字段表 sheet1 O18–O33 为权威 schema;系统概述/界面展示为图片=UI 表现层,本轮不读;sheet2「排行榜」空)。

### 本轮范围(plan 已按放手默认拍板,全部范围开关走默认,无 blocker)
排行榜数据逻辑层 + 两道接缝,离线可测。**只做数据逻辑 + 接缝 + 结算编排 + 红点 getter + Luban 表 + 测试**;UI 表现层 + 真实全服榜 + 远程实现转遗留,不返工。

### 交接区 · 给开发(dev)的落地清单

命名空间 `GameLogic.Rank`(通用系统,同 21 `GameLogic.Mail` 体例);配置桥接 `RankConfigMgr` 归 `GameLogic.Config`(与 `MailConfigMgr` 并列)。物理目录 `Assets/GameScripts/HotFix/GameLogic/Module/Rank/`。全部落热更区。详见设计稿 §五 dev 改动清单(8 项,符号名已 grep 核实标注)。

**核心复用(只调用不改,Code Review 会核)**:
- 结算/每日/点赞发奖 → 调既有 `GameLogic.Mail.IMailService.Send(MailDraft)`(`Module/Mail/MailboxService.cs`,设计 21,本地真实实现);草稿 `GameLogic.Mail.MailDraft`,可 `MailDraft.FromTemplate(mailDefId, senderTextId)`(`Module/Mail/MailItem.cs`)。**排名层不碰 MergeOrderState / ItemGrant / GiftOpener**——奖励经邮件下发,领取时由邮件 21 的 Claim 链展开走 16。
- 持久化 → 既有 `GameLogic.BlockBlast.Persistence.Provider`(`Module/BlockBlast/Persistence.cs`),专用键 `Rank.Progress`,`JsonUtility` 序列化(范本 `MailPersistence`)。
- 配置桥接范本 `MailConfigMgr`(`EnsureLoaded` 走 ConfigSystem + `InitForTest` 注入绕 YooAsset)。

**新建文件(§五 表 1–8)**:
1. `Configs/GameConfig/Datas/rank.xlsx`(一行一名次档,§3.1 字段表 18 列 + demo:id=1 三档 valid_type=3 周结算 + id=2 一档 valid_type=0)→ Luban `GameConfig.Rank` + `TbRank`。导表带 `DOTNET_ROLL_FORWARD=Major`(本机无 .NET7,见 boss 遗留 #18);工具链不可达 → Luban 直读条列 BLOCKED 不判 FAIL。
2. `Module/Rank/RankDef.cs`:`RankDef` / `RankRewardTier` / `RankValidType` POCO;`RankDef.TierForRank(rank)` 按名次查档(§3.2)。
3. `Config/RankConfigMgr.cs`:**按 id 聚合多行**(同 id → 一 RankDef + 各档 Tiers 按 RankMin 升序,榜级字段取首行)+ `GetRank`/`All`/`InitForTest`/`ResetForTest`(§3.2)。
4. `Module/Rank/RankModel.cs`:`RankEntry`/`RankBoard`/`SettleResult`/`RankClaimStatus`/`RankClaimResult`/`RankText`(文案 textId 占位,各返互不相同非 0 值,§3.3/§3.5/§3.9)。
5. 同目录持久化层:`RankProgressSave`/`RankBoardProgress`(`[Serializable]`)/`IRankPersistence`/`RankPersistence`(键 `Rank.Progress`,脏数据保底 + version)/`InMemoryRankPersistence`(§3.8)。**只存本机最佳分/达到时间/上次结算/每日·点赞领取日期;他人成绩不进盘**。
6. 同目录接缝:`IRankSource` + `LocalRankSource`(本机成绩注入 + 配置陪榜 → 一组参赛记录)+ `RemoteRankSource`(stub 返空、不连网)(§3.6)。
7. 同目录服务:`RankService`(查榜 `GetBoard`/`GetMyRank`/`SubmitScore`、排序并列、结算编排 `IsSettleDue`+`CheckAndSettle`、`ClaimDaily`/`ClaimPraise`、红点 `HasClaimable`)；结算/每日/点赞发奖经注入 `IMailService.Send`;注入 `NowProvider`+`OpenDate`+`IRankSource`+`IRankPersistence`+`IMailService`(§3.3–§3.7)。可加薄接口 `IRankConfigSource` 包 RankConfigMgr 便于注 fake。
8. `Assets/Editor/Tests/BlockBlast/RankSystemTests.cs`:覆盖 §六 26 条。asmdef 已含 GameLogic+GameProto+TEngine.Runtime,直达。

**dev 须按 num/item/redeem/mail 先例处理配置验收**:运行期 `ConfigSystem.Instance.Tables` 纯 C#/EditMode 跑不通;配置验收锚「`AssetDatabase.LoadAssetAtPath<TextAsset>(.../rank.bytes)` → `new TbRank(ByteBuf)`」直读(范本 WeightCfgLubanTests / MailSystemTests);纯逻辑经 InitForTest + InMemory + 注入时钟/邮件/数据源单测。

### 验收标准(给 test 逐条核对,完整 26 条见设计稿 §六)
- **配置 C1–C3**:InitForTest 灌入 GetRank 字段正确/查无返 null;**聚合** 同 id 3 行 → 3 档按 RankMin 升序、TierForRank 名次命中正确;Luban 直读 demo 行(不可达 BLOCKED)。
- **查榜+排序 SO1–SO3**:分数降序 + 同分按 AchievedTicks 升序 + 顺序名次(1,2,3,4);入榜要求过滤(<Condition 不进)+ 入榜上限 CountMax + 展示上限 ShowMax;我的名次(IsSelf 在榜返 SelfRank,未达要求返 0,GetMyRank 与 GetBoard.SelfRank 一致)。
- **结算时机 DUE1–DUE4**:四档 IsSettleDue(0 恒 false / 1 开服 X 天一次性 / 2 指定时间一次性 / 3 周循环每周一次,下周再 true)。
- **结算编排 ST1–ST3**:到点中奖档 → 注入 IMailService 收到 Send 且草稿 RewardPoolId==该档(spec「奖励写到邮件中」);**幂等** 同周期第二次 CheckAndSettle 不再发(lastSettle 已写);边界(rank=0/tier=null/reward=0/mail=0 不发仅记已结,FromTemplate null 兜底占位草稿仍挂奖发出)。
- **每日/点赞 DP1–DP3**:ClaimDaily 入榜+有档每日奖 → Success 经邮件发,当天再领 AlreadyClaimedToday,未入榜 NotRanked,无奖 NoReward;**跨天重置**(注入次日 today 可再领);均经邮件下发(不直发落点)。
- **红点 RD1–RD2**:HasClaimable(到点未结 / 今日每日可领 / 今日点赞可领任一 → true);领过/结算后该项不再亮(结算红点交邮件 21,不重复)。
- **持久化 P1–P2**:跨实例往返保真(最佳分/lastSettle/领取日期);脏数据/无键/负分保底产合法默认不抛。
- **接缝 SK1–SK2**:RemoteRankSource 返空不抛不连网,LocalRankSource 本机+陪榜可被排序;grep 全系统无 UnityWebRequest/HttpClient;换数据源服务层零改动。
- **回归/编译 R1–R2**:编译 0 error + 现有 EditMode 全绿(尤其邮件 21 不受影响)+ 新增 Rank 全绿;Code Review 5 红线 + 核「无真实网络/HTTP」「发奖复用 21 不另造、不碰 MergeOrderState」「持久化复用既有 Provider」「邮件 21 零改动」。

### 不在本轮(boss 授权遗留,数据层不返工)
真实全服排名(无网络)+ 真实他人数据(陪榜配置生成)+ 排行榜界面/列表/我的名次条/点赞按钮/奖励预览/头像 UI + icon 红点显示 + 主界面入口接线 + 结算自动触发时机(登录/tick 由表现层接) → 表现层延后轮 + 远程实现未来轮。范围开关 O1–O9 见设计稿 §七(全走安全默认)。
