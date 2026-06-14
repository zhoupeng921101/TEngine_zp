# 关单总结:排行榜系统 · 数据逻辑层 + 服务器接缝(2026-06-14 PASS)

## 任务定义
- 排行榜系统数据逻辑层 + 两道服务器接缝,按本批方向(继续建底层、留服务器接缝、离线可测)。源 spec `C:\Users\pc\Downloads\1007排行榜底层-----.xlsx`(字段表 sheet1 O18–O33 为权威 schema;系统概述/界面为图片=UI 表现层,本轮不读;sheet2 空)。
- 只交付数据逻辑 + 接缝 + 结算编排 + 红点 getter + Luban 表 + 测试;UI 表现层 + 真实全服榜 + 远程实现转遗留 #27,逻辑层不返工。

## 自治授权
- 模式:自治·放手默认,2026-06-14 用户 `/pipeline-auto 继续排行榜` 激活。授权仅本任务,关单即失效。
- git 基线:`f191b67a`(干净 HEAD,可一键 `git reset` 退回)。边界:不 push / 不 build / 不发布。

## 参与环节与执行
- baton=full(plan→dev→test),opus 全程。Workflow run `wf_87d3a344-379`,agent_count 3,~36 min,~550K token。
- 打回轮次:0(一轮过)。

## 命名空间与产出
- `GameLogic.Rank`(通用系统,同 21 `GameLogic.Mail` 体例),配置桥接 `GameLogic.Config.RankConfigMgr`。全部落热更区 `Assets/GameScripts/HotFix/GameLogic/`。
- 代码 6 个 .cs:`Module/Rank/{RankDef,RankModel,RankPersistence,RankSource,RankService}.cs` + `Config/RankConfigMgr.cs`。
- Luban:`Configs/GameConfig/Datas/rank.xlsx`(demo:id=1 周榜三档 + id=2 总榜一档,共 4 行)+ `__tables__.xlsx` 注册 + 生成 `Rank.cs`/`rank.TbRank.cs`/`Tables.cs` + `rank_tbrank.bytes`。
- 测试:`Assets/Editor/Tests/BlockBlast/RankSystemTests.cs`(24 [Test],覆盖设计 §六 26 验收点)。
- 设计稿 `design-docs/22-rank-system.html` + nav.js GROUPS 加 22 一项(单源,全库侧边栏 + 首页卡片自动同步)。

## 运行验证结论(test PASS)
- 编译 0 error 0 warning;EditMode 359/359(新增 Rank 24 + 既有 335 零回归);C3 LubanDirectRead GREEN(真读 `rank_tbrank.bytes`,解 4 行,聚合 id=1 三档 + id=2 一档、Tiers 按 RankMin 升序、reward 映射 1002/1005/1006/1007 全中,构成 xlsx→bytes 一致性核验);Code Review 5 红线 + 方向约束(grep 无网络 0 命中 / 复用邮件 21 不另造发奖 / 复用既有 Provider / 邮件 21 零改动)全过。
- 本轮纯数据逻辑层 + 接缝,无 Play 手验点(dev 交接区明确「需 Play 手验:无」)。

## 自治决策日志(workflow 返回 decisions[],17 条)
1. 排名数据源抽 `IRankSource` 接缝:离线 `LocalRankSource`(本机成绩 + 配置陪榜,本地排序产出可玩可测的榜)/ 远程 `RemoteRankSource` stub 返空不连网(O1)。
2. 陪榜成绩本轮由配置/注入基准分,不随机生成 NPC(O2)。
3. 结算/每日/点赞奖统一经邮件 21 `IMailService.Send` 下发(spec mail 字段),排名层不碰 MergeOrderState/ItemGrant,奖励内容复用 16 礼包随机库 id(O5)。
4. 排序并列规则 spec 未写,补全为:分数降序 + 同分按入榜时间(AchievedTicks)升序 + 顺序名次 1,2,3,4(O4);周结算时刻精度到天。
5. 结算本轮为纯方法 `CheckAndSettle(now)` + `IsSettleDue` 幂等防重,不起后台定时器,触发时机交调用方(O9)。
6. 一表控所有榜:Luban 另设 `row_id` 唯一主键,spec 的 `id` 当普通字段,同 id 多行按 id 聚合成 `RankDef`(榜级字段取首行)+ `List<RankRewardTier>`(名次档)。
7. 持久化只存本机元层进度(最佳分/上次结算/已结标记/每日点赞领取日期)进键 `Rank.Progress`,他人成绩不进盘;每日/点赞跨天重置存领取日期 + 注入 today 比。
8. `rank_method` 玩法类型本轮整数枚举占位,各玩法接入时经 `SubmitScore` 提交成绩(O3);UI 表现层 + icon 红点显示 + 真实全服榜转遗留延后(O8)。
9. 验收锚 26 条全 EditMode 纯逻辑可测(注入时钟/邮件服务/数据源/持久化);Luban 直读条工具链不可达列 BLOCKED 不判 FAIL(同 num/item/redeem/mail 先例)。
10. C2b 占位条:`EnsureLoaded` 走 ConfigSystem/YooAsset 在 EditMode 不可达,聚合逻辑改由 C3 真实表直读核验(同 mail/item 先例),移除无法构造 Luban 行的 `InitForTestFromRows` 死方法。
11. 周循环结算用 ISO 周(周一为周起点)+ 本轮精度到天(设计 O4 安全默认);FixedTime 的 valid_val 按 Unix 秒解释(DateTimeOffset 往返一致)。
12. 本机 Luban 工具链可达(DOTNET_ROLL_FORWARD=Major + AI_MODE=1 导表成功),C3 已真跑 PASS、不列 BLOCKED;另机重跑若 bytes 缺会自动 Inconclusive=BLOCKED。
13. 发奖三种(结算/每日/点赞)统一经注入 `IMailService.Send` 下发(O5 默认),排名层不碰 MergeOrderState/ItemGrant/GiftOpener;持久化复用既有 `Persistence.Provider` 键 `Rank.Progress`。
14. `LocalRankSource` 的 selfProvider 设计为可注入闭包:生产用 `RankService.GetMyBest` 读进度、测试注固定值,使 SubmitScore→GetBoard 链路与榜形态构造两种测法都成立。
15. 手动功能验证类:本轮纯数据逻辑层+接缝、dev 交接区明确无 Play 手验点,按注入式 EditMode(注入 NowProvider/IMailService fake/IRankSource/InMemory 持久化)完整覆盖功能路径判 PASS,不另起 Play 模式。
16. 配置侧不列 BLOCKED:C3 LubanDirectRead 取 state==Passed(非 Inconclusive),`rank_tbrank.bytes` 真实存在且字段映射全中,构成源 xlsx→bytes 一致性最强交叉核验,导表工具链此处可达。
17. `GetMyBest` 借 `RankText.SettleSender` 作占位玩家名 textId(语义略错位)仅记为备注项、不判 FAIL:属 O6 文案占位范畴、非验收项、不影响任何断言。

## 遗留归属
- 新增遗留 #27(rank 表现层 + 真实服务器/全服榜 + 各玩法接入 + 结算触发时机 + 文案 textId 占位含 GetMyBest 玩家名占位),活条目见主 `state/boss.md`。
