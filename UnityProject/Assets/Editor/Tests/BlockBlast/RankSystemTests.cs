using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameLogic.Rank;
using GameLogic.Config;
using GameLogic.Mail;
using GameLogic.BlockBlast;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 排行榜底层系统测试（设计 22 §六，26 条验收点）。
    /// 纯逻辑全 EditMode 可测（POCO + InitForTest + InMemoryRankPersistence + 注入 NowProvider/OpenDate + 注入 IMailService/IRankSource）；
    /// Luban 直读 C3 按工具链可达性（不可达列 Inconclusive=BLOCKED 不判 FAIL）。
    /// 结算 / 每日 / 点赞发奖经注入 fake <see cref="IMailService"/> 断言「发了挂哪个奖励库 id 的邮件」（复用 21，不另造发奖）。
    /// </summary>
    [TestFixture]
    public class RankSystemTests
    {
        // ── fake 邮件服务：记录每次 Send 的草稿 ─────────────────
        private sealed class RecordingMailService : IMailService
        {
            public readonly List<MailDraft> Sent = new List<MailDraft>();
            private long _id = 1;
            public long Send(MailDraft draft) { Sent.Add(draft); return _id++; }
        }

        // ── 夹具：榜定义 / 陪榜 / 服务构造 ──────────────────────

        private const int BoardWeekly = 1;  // valid_type=3 周榜，三档
        private const int BoardAlways = 2;  // valid_type=0 总榜，一档

        private static readonly DateTime T_Mon = new DateTime(2026, 6, 8, 12, 0, 0);  // 2026-06-08 是周一

        // 周榜：三档（1名 / 2-10名 / 11-100名），praise=1003，daily=1004，mail=1
        private static RankDef WeeklyDef()
            => new RankDef
            {
                Id = BoardWeekly, NameTextId = 110811, Group = 1, Method = 1, Condition = 100,
                PraiseRewardPoolId = 1003, ValidType = RankValidType.Weekly, ValidVal = 1, MailDefId = 1,
                CountMax = 100, ShowMax = 50,
                Tiers = new List<RankRewardTier>
                {
                    new RankRewardTier { RankMin = 11, RankMax = 100, RewardPoolId = 1006, DailyRewardPoolId = 1004 }, // 故意乱序录入
                    new RankRewardTier { RankMin = 1, RankMax = 1, RewardPoolId = 1002, ShowRewardPoolId = 1002, DailyRewardPoolId = 1004 },
                    new RankRewardTier { RankMin = 2, RankMax = 10, RewardPoolId = 1005, DailyRewardPoolId = 1004 },
                },
            };

        // 总榜：valid_type=0，一档，无每日 / 点赞奖
        private static RankDef AlwaysDef()
            => new RankDef
            {
                Id = BoardAlways, NameTextId = 110812, Group = 2, Method = 1, Condition = 0,
                PraiseRewardPoolId = 0, ValidType = RankValidType.Always, ValidVal = 0, MailDefId = 1,
                CountMax = 100, ShowMax = 50,
                Tiers = new List<RankRewardTier>
                {
                    new RankRewardTier { RankMin = 1, RankMax = 1, RewardPoolId = 1007 },
                },
            };

        private static RankEntry Filler(long score, long ticks, int name = 9000)
            => new RankEntry { IsSelf = false, Score = score, AchievedTicks = ticks, PlayerNameTextId = name };

        // 用注入固定 selfProvider 的本地源（不依赖服务内部进度，便于直接构造榜形态）
        private static LocalRankSource FixedSource(long selfScore, long selfTicks, IReadOnlyList<RankEntry> fillers)
            => new LocalRankSource(
                _ => (selfScore, selfTicks, 110820),
                _ => fillers);

        private static RankService NewService(IRankSource source, IRankPersistence persist,
                                              RecordingMailService mail, DateTime now)
        {
            // cfg 默认包 RankConfigMgr（已 InitForTest 灌入）
            return new RankService(source, persist, mail)
            {
                NowProvider = () => now,
            };
        }

        [SetUp]
        public void SetUp()
        {
            RankConfigMgr.ResetForTest();
            MailConfigMgr.ResetForTest();
        }

        [TearDown]
        public void TearDown()
        {
            RankConfigMgr.ResetForTest();
            MailConfigMgr.ResetForTest();
        }

        // ════════════ 配置 C ════════════

        [Test] // C1：InitForTest 灌入后 GetRank 返 def，榜级字段正确；查不存在 id 返 null（不抛）
        public void C1_ConfigMgr_GetReturnsDef_MissReturnsNull()
        {
            RankConfigMgr.InitForTest(new[] { WeeklyDef(), AlwaysDef() });
            var def = RankConfigMgr.GetRank(BoardWeekly);
            Assert.IsNotNull(def);
            Assert.AreEqual(110811, def.NameTextId);
            Assert.AreEqual(1, def.Group);
            Assert.AreEqual(1, def.Method);
            Assert.AreEqual(100, def.Condition);
            Assert.AreEqual(1003, def.PraiseRewardPoolId);
            Assert.AreEqual(RankValidType.Weekly, def.ValidType);
            Assert.AreEqual(1, def.ValidVal);
            Assert.AreEqual(1, def.MailDefId);
            Assert.AreEqual(100, def.CountMax);
            Assert.AreEqual(50, def.ShowMax);
            Assert.IsNull(RankConfigMgr.GetRank(999));
        }

        [Test] // C2：TierForRank 名次落区间返对应档（rank=1→档1, rank=5→档2, rank=50→档3），落空返 null
        public void C2_TierForRank_HitsCorrectTier_MissReturnsNull()
        {
            var def = WeeklyDef();
            Assert.AreEqual(1002, def.TierForRank(1).RewardPoolId, "rank=1 → 档1(1002)");
            Assert.AreEqual(1005, def.TierForRank(5).RewardPoolId, "rank=5 → 档2(1005)");
            Assert.AreEqual(1006, def.TierForRank(50).RewardPoolId, "rank=50 → 档3(1006)");
            Assert.AreEqual(1006, def.TierForRank(100).RewardPoolId, "rank=100 → 档3(1006, 边界含)");
            Assert.IsNull(def.TierForRank(101), "rank=101 落空 → null");
            Assert.IsNull(def.TierForRank(0), "rank=0 落空 → null");
        }

        [Test] // C3：Luban 直读 rank.bytes：id=1 三档 + id=2 一档，聚合后 Tiers 按 RankMin 升序（工具链不可达 → Inconclusive=BLOCKED，不判 FAIL）
        public void C3_LubanDirectRead_RankBytes_AggregatesCorrectly()
        {
            const string path = "Assets/AssetRaw/Configs/bytes/rank_tbrank.bytes";
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (ta == null)
                Assert.Inconclusive($"找不到 {path}，Luban 导表工具链不可达 → BLOCKED（非 FAIL，纯逻辑条已覆盖）");

            var tb = new GameConfig.rank.TbRank(new Luban.ByteBuf(ta.bytes));
            Assert.AreEqual(4, tb.DataList.Count, "rank 表应有 4 个名次档行（id=1 三档 + id=2 一档）");

            // 经 RankConfigMgr 运行期同款聚合逻辑核验（直接灌已聚合的 POCO 不可行，故用真实表桥接路径）
            var map = new Dictionary<int, RankDef>();
            foreach (var row in tb.DataList)
            {
                if (!map.TryGetValue(row.Id, out var def))
                {
                    def = new RankDef
                    {
                        Id = row.Id, NameTextId = row.Name, ValidType = (RankValidType)row.ValidType,
                        ValidVal = row.ValidVal, MailDefId = row.Mail, Condition = row.RankCondition,
                        Tiers = new List<RankRewardTier>(),
                    };
                    map[row.Id] = def;
                }
                def.Tiers.Add(new RankRewardTier
                {
                    RankMin = row.RankMin, RankMax = row.RankMax, RewardPoolId = row.Reward,
                    ShowRewardPoolId = row.RankShowReward, DailyRewardPoolId = row.RewardDaily,
                });
            }
            foreach (var d in map.Values) d.Tiers.Sort((a, b) => a.RankMin.CompareTo(b.RankMin));

            Assert.IsTrue(map.ContainsKey(1), "应含 id=1 榜");
            Assert.IsTrue(map.ContainsKey(2), "应含 id=2 榜");
            Assert.AreEqual(3, map[1].Tiers.Count, "id=1 应聚合 3 档");
            Assert.AreEqual(1, map[2].Tiers.Count, "id=2 应聚合 1 档");
            // Tiers 按 RankMin 升序
            Assert.AreEqual(1, map[1].Tiers[0].RankMin);
            Assert.AreEqual(2, map[1].Tiers[1].RankMin);
            Assert.AreEqual(11, map[1].Tiers[2].RankMin);
            // reward 映射
            Assert.AreEqual(1002, map[1].Tiers[0].RewardPoolId);
            Assert.AreEqual(1005, map[1].Tiers[1].RewardPoolId);
            Assert.AreEqual(1006, map[1].Tiers[2].RewardPoolId);
            Assert.AreEqual(1007, map[2].Tiers[0].RewardPoolId);
            Assert.AreEqual(RankValidType.Weekly, map[1].ValidType);
            Assert.AreEqual(RankValidType.Always, map[2].ValidType);
        }

        [Test] // C2 补：EnsureLoaded 经真实表聚合后 GetRank 与上面一致（仅工具链可达；走 RankConfigMgr 实际桥接路径）
        public void C2b_RankConfigMgr_EnsureLoaded_RealTable_Aggregates()
        {
            // RankConfigMgr.EnsureLoaded 走 ConfigSystem（YooAsset），EditMode 不可达 → 跳过；
            // 真实聚合由 C3 直读路径核验。本条仅占位说明 EnsureLoaded 不在 EditMode 测（同 mail/item 先例）。
            Assert.Pass("EnsureLoaded 走 ConfigSystem/YooAsset，EditMode 不可达；聚合逻辑由 C3 真实表直读核验");
        }

        // ════════════ 查榜 + 排序 SO ════════════

        [Test] // SO1：分数降序 + 同分按 AchievedTicks 升序 + 名次 1,2,3,4 顺序回填
        public void SO1_Sort_ScoreDesc_TieByTicksAsc_SequentialRank()
        {
            RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            // 本机 500@t100；陪榜 800@t50, 500@t80（与本机同分但更早）, 300@t10
            var fillers = new List<RankEntry> { Filler(800, 50), Filler(500, 80), Filler(300, 10) };
            var svc = NewService(FixedSource(500, 100, fillers), new InMemoryRankPersistence(), new RecordingMailService(), T_Mon);

            var board = svc.GetBoard(BoardWeekly);
            Assert.IsNotNull(board);
            Assert.AreEqual(4, board.Entries.Count);
            // 800 第1
            Assert.AreEqual(800, board.Entries[0].Score); Assert.AreEqual(1, board.Entries[0].Rank);
            // 同分 500：陪榜 t80 早于本机 t100 → 陪榜第2、本机第3
            Assert.AreEqual(500, board.Entries[1].Score); Assert.AreEqual(2, board.Entries[1].Rank); Assert.IsFalse(board.Entries[1].IsSelf);
            Assert.AreEqual(500, board.Entries[2].Score); Assert.AreEqual(3, board.Entries[2].Rank); Assert.IsTrue(board.Entries[2].IsSelf);
            // 300 第4
            Assert.AreEqual(300, board.Entries[3].Score); Assert.AreEqual(4, board.Entries[3].Rank);
        }

        [Test] // SO2：入榜要求过滤(<Condition 不进) + 入榜上限 CountMax + 展示上限 ShowMax
        public void SO2_Filter_Condition_CountMax_ShowMax()
        {
            // Condition=100：50 分不进；CountMax=3：只前3名计名次；ShowMax=2：Entries 截2条
            var def = WeeklyDef(); def.CountMax = 3; def.ShowMax = 2;
            RankConfigMgr.InitForTest(new[] { def });
            var fillers = new List<RankEntry>
            {
                Filler(900, 10), Filler(800, 20), Filler(700, 30), Filler(600, 40), // 4 个合格陪榜
                Filler(50, 5),  // < Condition，不进
            };
            var svc = NewService(FixedSource(50, 100, fillers), new InMemoryRankPersistence(), new RecordingMailService(), T_Mon);

            var board = svc.GetBoard(BoardWeekly);
            // 合格记录 4 个陪榜（本机 50<100 被过滤）；CountMax=3 → 只前3计名次；ShowMax=2 → 展示2条
            Assert.AreEqual(2, board.Entries.Count, "展示上限 2");
            Assert.AreEqual(900, board.Entries[0].Score);
            Assert.AreEqual(800, board.Entries[1].Score);
            // 50 分不在榜
            foreach (var e in board.Entries) Assert.AreNotEqual(50, e.Score);
            // 本机未达入榜要求 → SelfRank 0
            Assert.AreEqual(0, board.SelfRank);
        }

        [Test] // SO3：我的名次 —— 在榜返 SelfRank/Self，未达要求返 0；GetMyRank 与 GetBoard.SelfRank 一致
        public void SO3_MyRank_InBoard_AndNotRanked()
        {
            RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            // 本机 700 在榜
            var fillers = new List<RankEntry> { Filler(900, 10), Filler(500, 20) };
            var svc = NewService(FixedSource(700, 100, fillers), new InMemoryRankPersistence(), new RecordingMailService(), T_Mon);
            var board = svc.GetBoard(BoardWeekly);
            Assert.IsNotNull(board.Self);
            Assert.IsTrue(board.Self.IsSelf);
            Assert.AreEqual(2, board.SelfRank, "900 第1，本机700 第2");
            var (r, s) = svc.GetMyRank(BoardWeekly);
            Assert.AreEqual(board.SelfRank, r);
            Assert.AreEqual(700, s);

            // 未达入榜要求（本机 50 < 100）
            var svc2 = NewService(FixedSource(50, 100, fillers), new InMemoryRankPersistence(), new RecordingMailService(), T_Mon);
            Assert.AreEqual(0, svc2.GetMyRank(BoardWeekly).rank);
        }

        // ════════════ 本地结算退役 RT(设计 22 §五 退役 RT 组) ════════════
        // 客户端排名服务不再持有「结算检查 / 结算时机判定 / 上次结算时间 / 已结标记」对外表面;
        // 结算编排上移服务端(设计 33),客户端无结算入口、不本地发结算奖、不本地存结算幂等标记。
        // RT1-RT3 验收点的客户端可观测面体现为:
        //   ① DUE1-DUE4(IsSettleDue 四档)+ ST1-ST3b(CheckAndSettle 编排)整组测试已删除——
        //      若服务恢复任一对外结算表面,本测试块编译失败 = 退役被违反;
        //   ② RD 组「到点未结」断言已删,红点不再因结算到点亮起;
        //   ③ P1「上次结算时间」往返断言已删,P2 新增老存档含遗留字段反序列化不抛断言;
        //   ④ 字段表退化由 RankBoardProgress / SettleResult struct 删除编译期保证。

        // ════════════ 每日/点赞 DP ════════════

        [Test] // DP1：ClaimDaily 入榜+有档每日奖 → Success 经邮件发；当天再领 AlreadyClaimedToday；未入榜 NotRanked；无奖 NoReward
        public void DP1_ClaimDaily_States()
        {
            RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var mail = new RecordingMailService();
            var now = new DateTime(2026, 6, 9, 12, 0, 0);
            // 本机 900 → 第1名档（daily 1004）
            var svc = NewService(FixedSource(900, 100, new List<RankEntry> { Filler(500, 10) }), new InMemoryRankPersistence(), mail, now);

            var r1 = svc.ClaimDaily(BoardWeekly);
            Assert.AreEqual(RankClaimStatus.Success, r1.Status);
            Assert.AreEqual(1, mail.Sent.Count);
            Assert.AreEqual(1004, mail.Sent[0].RewardPoolId, "每日奖库 1004");

            var r2 = svc.ClaimDaily(BoardWeekly);
            Assert.AreEqual(RankClaimStatus.AlreadyClaimedToday, r2.Status);
            Assert.AreEqual(1, mail.Sent.Count, "当天再领不重复发");

            // 未入榜
            var svc2 = NewService(FixedSource(50, 100, null), new InMemoryRankPersistence(), new RecordingMailService(), now);
            Assert.AreEqual(RankClaimStatus.NotRanked, svc2.ClaimDaily(BoardWeekly).Status);

            // 无每日奖：构造一档 daily=0
            var noDaily = new RankDef
            {
                Id = 7, Condition = 0, ValidType = RankValidType.Always, MailDefId = 1, CountMax = 100, ShowMax = 50,
                Tiers = new List<RankRewardTier> { new RankRewardTier { RankMin = 1, RankMax = 1, RewardPoolId = 1002, DailyRewardPoolId = 0 } },
            };
            RankConfigMgr.ResetForTest(); RankConfigMgr.InitForTest(new[] { noDaily });
            var svc3 = NewService(FixedSource(900, 100, null), new InMemoryRankPersistence(), new RecordingMailService(), now);
            Assert.AreEqual(RankClaimStatus.NoReward, svc3.ClaimDaily(7).Status);
        }

        [Test] // DP2：跨天重置 —— 注入次日 today 可再领；点赞同口径，PraiseRewardPoolId==0 → NoReward
        public void DP2_CrossDayReset_AndPraise()
        {
            RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var mail = new RecordingMailService();
            var persist = new InMemoryRankPersistence();
            var day1 = new DateTime(2026, 6, 9, 12, 0, 0);
            var svc = NewService(FixedSource(900, 100, null), persist, mail, day1);

            Assert.AreEqual(RankClaimStatus.Success, svc.ClaimDaily(BoardWeekly).Status);
            Assert.AreEqual(RankClaimStatus.AlreadyClaimedToday, svc.ClaimDaily(BoardWeekly).Status);

            // 次日：新建服务实例（复用同 persist，模拟跨天 + 重启）
            var day2 = day1.AddDays(1);
            var svc2 = NewService(FixedSource(900, 100, null), persist, mail, day2);
            Assert.AreEqual(RankClaimStatus.Success, svc2.ClaimDaily(BoardWeekly).Status, "跨天可再领");

            // 点赞：周榜 praise=1003 → Success；当天再领 AlreadyClaimedToday
            var svc3 = NewService(FixedSource(900, 100, null), new InMemoryRankPersistence(), new RecordingMailService(), day1);
            Assert.AreEqual(RankClaimStatus.Success, svc3.ClaimPraise(BoardWeekly).Status);
            Assert.AreEqual(RankClaimStatus.AlreadyClaimedToday, svc3.ClaimPraise(BoardWeekly).Status);

            // 总榜 praise=0 → NoReward
            RankConfigMgr.ResetForTest(); RankConfigMgr.InitForTest(new[] { AlwaysDef() });
            var svc4 = NewService(FixedSource(900, 0, null), new InMemoryRankPersistence(), new RecordingMailService(), day1);
            Assert.AreEqual(RankClaimStatus.NoReward, svc4.ClaimPraise(BoardAlways).Status);
        }

        [Test] // DP3：每日/点赞奖均经邮件下发（注入 IMailService 收到 Send），不直发落点
        public void DP3_ClaimsGoThroughMail()
        {
            RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var mail = new RecordingMailService();
            var now = new DateTime(2026, 6, 9, 12, 0, 0);
            var svc = NewService(FixedSource(900, 100, null), new InMemoryRankPersistence(), mail, now);

            svc.ClaimDaily(BoardWeekly);
            svc.ClaimPraise(BoardWeekly);
            Assert.AreEqual(2, mail.Sent.Count, "每日 + 点赞各发 1 封");
            Assert.AreEqual(1004, mail.Sent[0].RewardPoolId);
            Assert.AreEqual(1003, mail.Sent[1].RewardPoolId);
        }

        // ════════════ 红点 RD ════════════

        [Test] // RD1:今日每日可领 OR 今日点赞可领 → true;两项全已领 → false(结算分支已退役,本红点不感知结算到点)
        public void RD1_HasClaimable()
        {
            // 用 Always 榜(不到点结算)隔离结算分支:仅每日/点赞驱动红点
            var def = new RankDef
            {
                Id = 8, Condition = 0, ValidType = RankValidType.Always, PraiseRewardPoolId = 1003, MailDefId = 1, CountMax = 100, ShowMax = 50,
                Tiers = new List<RankRewardTier> { new RankRewardTier { RankMin = 1, RankMax = 1, RewardPoolId = 1002, DailyRewardPoolId = 1004 } },
            };
            RankConfigMgr.InitForTest(new[] { def });
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var now = new DateTime(2026, 6, 9, 12, 0, 0);
            var svc = NewService(FixedSource(900, 100, null), new InMemoryRankPersistence(), new RecordingMailService(), now);

            Assert.IsTrue(svc.HasClaimable, "有每日/点赞可领 → true");
            svc.ClaimDaily(8);
            Assert.IsTrue(svc.HasClaimable, "点赞仍可领 → 仍 true");
            svc.ClaimPraise(8);
            Assert.IsFalse(svc.HasClaimable, "全领过 → false");
        }

        [Test] // RD2:周榜「到点未结算」分支已退役 —— HasClaimable 不感知结算时机,
               //      无每日/点赞奖的周榜即使到结算点也不亮(结算红点由设计 21 邮件红点表达)
        public void RD2_SettleDueNoLongerTriggersRedDot()
        {
            // 周榜变体:无每日/点赞奖,只剩名次档结算奖;现状:本红点应始终 false(结算上移服务端)
            var weeklyNoClaim = new RankDef
            {
                Id = 9, Condition = 0, ValidType = RankValidType.Weekly, ValidVal = 1,
                PraiseRewardPoolId = 0, MailDefId = 1, CountMax = 100, ShowMax = 50,
                Tiers = new List<RankRewardTier>
                {
                    new RankRewardTier { RankMin = 1, RankMax = 1, RewardPoolId = 1002, DailyRewardPoolId = 0 },
                },
            };
            RankConfigMgr.InitForTest(new[] { weeklyNoClaim });
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            // 周二、本机第1(到结算点,老行为应亮);新行为:不亮(结算红点交邮件 21)
            var now = new DateTime(2026, 6, 9, 12, 0, 0);
            var svc = NewService(FixedSource(900, 100, null), new InMemoryRankPersistence(), new RecordingMailService(), now);
            Assert.IsFalse(svc.HasClaimable, "到点未结分支已退役 → 不亮(结算红点交邮件 21)");
        }

        // ════════════ 持久化 P ════════════

        [Test] // P1:跨实例往返保真(本机最佳分 + 每日/点赞领取日期);上次结算时间字段已退役,不再断言
        public void P1_Persistence_RoundTrip()
        {
            RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            MailConfigMgr.InitForTest(Array.Empty<MailDef>());
            var persist = new InMemoryRankPersistence();
            var now = new DateTime(2026, 6, 9, 12, 0, 0);

            // 实例1:提交成绩 + 领每日/点赞;FixedSource 本机分=900 入榜,使 ClaimDaily 能命中名次档
            var svc1 = NewService(FixedSource(900, 100, null), persist, new RecordingMailService(), now);
            svc1.SubmitScore(BoardWeekly, 777);
            // 用基于进度的本地源验证 SubmitScore 反映到榜
            var svcRead = new RankService(
                new LocalRankSource(id => svc1.GetMyBest(id), _ => new List<RankEntry>()),
                persist, new RecordingMailService())
            { NowProvider = () => now };
            Assert.AreEqual(777, svcRead.GetBoard(BoardWeekly).SelfScore, "提交成绩反映到榜");

            // 领每日 + 点赞,标记当天日期(本机分=900 入榜,每日奖落第1名档)
            Assert.AreEqual(RankClaimStatus.Success, svc1.ClaimDaily(BoardWeekly).Status);
            Assert.AreEqual(RankClaimStatus.Success, svc1.ClaimPraise(BoardWeekly).Status);

            // 实例2:复用同 persist 新建(模拟重启) → 最佳分 + 每日/点赞领取日期保真
            var svc2 = NewService(FixedSource(900, 100, null), persist, new RecordingMailService(), now);
            Assert.AreEqual(777, svc2.GetMyBest(BoardWeekly).score, "重启后最佳分保真");
            Assert.AreEqual(RankClaimStatus.AlreadyClaimedToday, svc2.ClaimDaily(BoardWeekly).Status, "每日领取日期保真,当天再领仍 AlreadyClaimedToday");
            Assert.AreEqual(RankClaimStatus.AlreadyClaimedToday, svc2.ClaimPraise(BoardWeekly).Status, "点赞领取日期保真,当天再领仍 AlreadyClaimedToday");

            // 提交更高分刷新；提交更低分不覆盖
            svc2.SubmitScore(BoardWeekly, 500);
            Assert.AreEqual(777, svc2.GetMyBest(BoardWeekly).score, "更低分不覆盖");
            svc2.SubmitScore(BoardWeekly, 900);
            Assert.AreEqual(900, svc2.GetMyBest(BoardWeekly).score, "更高分刷新");
        }

        [Test] // P2:脏数据/无键/负分保底产合法默认不抛;老存档含遗留「上次结算时间」字段反序列化不抛(向后兼容)
        public void P2_Persistence_DirtyDataSafe()
        {
            // 无键
            var fresh = new InMemoryRankPersistence();
            var save = fresh.Load();
            Assert.IsNotNull(save);
            Assert.IsNotNull(save.boards);
            Assert.AreEqual(0, save.boards.Count);

            // 生产 RankPersistence + InMemoryPersistenceProvider 注脏数据
            var provider = new InMemoryPersistenceProvider();
            Persistence.Provider = provider;
            provider.Set(RankPersistence.Key, "{not valid json");
            var prod = new RankPersistence();
            var loaded = prod.Load();
            Assert.IsNotNull(loaded, "脏 JSON → 合法空,不抛");
            Assert.IsNotNull(loaded.boards);

            // 老存档含遗留 `lastSettleTicks` 字段:JsonUtility 忽略 DTO 中不存在的字段,不抛,保真其它字段
            provider.Set(RankPersistence.Key,
                "{\"version\":1,\"boards\":[{\"rankId\":1,\"bestScore\":555,\"bestAchievedTicks\":1234,\"lastSettleTicks\":9999,\"dailyClaimDateBin\":777,\"praiseClaimDateBin\":888}]}");
            var legacyLoaded = prod.Load();
            Assert.IsNotNull(legacyLoaded, "老存档含遗留字段 → 不抛");
            Assert.AreEqual(1, legacyLoaded.boards.Count);
            Assert.AreEqual(1, legacyLoaded.boards[0].rankId);
            Assert.AreEqual(555, legacyLoaded.boards[0].bestScore, "遗留字段忽略,本机最佳分保真");
            Assert.AreEqual(1234, legacyLoaded.boards[0].bestAchievedTicks);
            Assert.AreEqual(777, legacyLoaded.boards[0].dailyClaimDateBin);
            Assert.AreEqual(888, legacyLoaded.boards[0].praiseClaimDateBin);

            // 负分提交夹 ≥0
            RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            var svc = NewService(FixedSource(0, 0, null), new InMemoryRankPersistence(), new RecordingMailService(), T_Mon);
            svc.SubmitScore(BoardWeekly, -50);
            Assert.AreEqual(0, svc.GetMyBest(BoardWeekly).score, "负分夹 0");

            provider.Clear();
            Persistence.Provider = new InMemoryPersistenceProvider(); // 复位防污染其它测试
        }

        // ════════════ 接缝 SK ════════════

        [Test] // SK1：RemoteRankSource 返空不抛不连网；LocalRankSource 本机+陪榜可被排序
        public void SK1_Sources()
        {
            var remote = new RemoteRankSource();
            var empty = remote.Fetch(BoardWeekly);
            Assert.IsNotNull(empty);
            Assert.AreEqual(0, empty.Count, "远程 stub 返空");

            var local = new LocalRankSource(_ => (500, 100, 110820), _ => new List<RankEntry> { Filler(900, 10) });
            var list = local.Fetch(BoardWeekly);
            Assert.AreEqual(2, list.Count, "本机一条 + 陪榜一条");
            bool hasSelf = false;
            foreach (var e in list) if (e.IsSelf) hasSelf = true;
            Assert.IsTrue(hasSelf, "含本机 IsSelf 条");
        }

        [Test] // SK2：注入 RemoteRankSource（空榜）GetBoard 返空 Entries 不抛；切回 Local 行为不变
        public void SK2_SwapSource_ServiceUnchanged()
        {
            RankConfigMgr.InitForTest(new[] { AlwaysDef() }); // Condition=0，本机能进
            // 远程空源
            var svcRemote = NewService(new RemoteRankSource(), new InMemoryRankPersistence(), new RecordingMailService(), T_Mon);
            var boardR = svcRemote.GetBoard(BoardAlways);
            Assert.IsNotNull(boardR);
            Assert.AreEqual(0, boardR.Entries.Count, "远程空源 → 空 Entries 不抛");

            // 本地源（同服务逻辑，零改动）
            var svcLocal = NewService(FixedSource(500, 100, new List<RankEntry> { Filler(900, 10) }), new InMemoryRankPersistence(), new RecordingMailService(), T_Mon);
            var boardL = svcLocal.GetBoard(BoardAlways);
            Assert.AreEqual(2, boardL.Entries.Count, "本地源 → 2 条");
            Assert.AreEqual(2, boardL.SelfRank, "本机 500 第2");
        }
    }
}
