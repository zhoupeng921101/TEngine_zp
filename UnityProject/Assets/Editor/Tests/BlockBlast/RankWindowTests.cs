using System;
using System.Collections.Generic;
using NUnit.Framework;
using GameLogic;
using GameLogic.Config;
using GameLogic.Mail;
using GameLogic.Rank;
using GameLogic.UI;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 排行榜窗表现层验收测试（设计 28 §九 H/W 组，EditMode 可单测部分）。
    /// 全 EditMode 可测：
    ///   H2 — GameContext 持有 RankService（InitRankWithDeps 注入 InMemoryRankPersistence + RecordingMailService
    ///        + RankConfigMgr.InitForTest，不污染真实 PlayerPrefs / 不连网）；
    ///   H3 — 经 GameContext 拿到的服务查榜数据贯通（排序 / 名次回填 / 本机标记，复用设计 22 排序口径）；
    ///   W3 — 窗口「board → 行 VM 列表」+「我的名次条文本」纯方法映射贯通真实数据；
    ///   W6 — 空榜 / 未入榜 / board==null 不抛、显友好态。
    /// 不在此测：W4 点赞（本屏默认省略 → N/A，设计 28 §七 D2）；W5 绑定 path（code review + V 组 Play 核）；
    ///   V 组（对位 / 渲染 / 三种关闭 / 零回归玩法）须 Play 桥跑（设计 28 §9.2）。
    /// </summary>
    [TestFixture]
    public class RankWindowTests
    {
        // ── fake 邮件服务：记录每次 Send（复用设计 22 测试口径）─────────
        private sealed class RecordingMailService : IMailService
        {
            public readonly List<MailDraft> Sent = new List<MailDraft>();
            private long _id = 1;
            public long Send(MailDraft draft) { Sent.Add(draft); return _id++; }
        }

        private const int BoardWeekly = 1;
        private static readonly DateTime T_Mon = new DateTime(2026, 6, 8, 12, 0, 0);  // 周一

        // 周榜：condition=100，praise=1003（与设计 22 测试同口径）。
        private static RankDef WeeklyDef()
            => new RankDef
            {
                Id = BoardWeekly, NameTextId = 110811, Group = 1, Method = 1, Condition = 100,
                PraiseRewardPoolId = 1003, ValidType = RankValidType.Weekly, ValidVal = 1, MailDefId = 1,
                CountMax = 100, ShowMax = 50,
                Tiers = new List<RankRewardTier>
                {
                    new RankRewardTier { RankMin = 1, RankMax = 1, RewardPoolId = 1002, DailyRewardPoolId = 1004 },
                },
            };

        private static RankEntry Filler(long score, long ticks, int name = 9000)
            => new RankEntry { IsSelf = false, Score = score, AchievedTicks = ticks, PlayerNameTextId = name };

        // 固定 selfProvider 本地源（不依赖服务内部进度，便于直接构造榜形态，同设计 22 FixedSource）。
        private static LocalRankSource FixedSource(long selfScore, long selfTicks, IReadOnlyList<RankEntry> fillers)
            => new LocalRankSource(_ => (selfScore, selfTicks, 110820), _ => fillers);

        /// <summary>经 GameContext 注入入口装配一个测试态排行榜服务（InMemory 持久化 + Recording 邮件 + 默认配置源）。</summary>
        private static void InjectRank(LocalRankSource source, RecordingMailService mail)
        {
            GameContext.Instance.InitRankWithDeps(source, new InMemoryRankPersistence(), mail);
            GameContext.Instance.Rank.NowProvider = () => T_Mon;
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
            if (GameContext.IsValid) GameContext.Instance.Release();
        }

        // ════════════ H2：GameContext 持有 RankService ════════════

        [Test] // GameContext.Instance.Rank 非 null；多次 Instance 返同一服务（持有，不每次新建）
        public void H2_GameContext_HoldsRankService_SameInstance()
        {
            var ctx1 = GameContext.Instance;
            var ctx2 = GameContext.Instance;
            Assert.AreSame(ctx1, ctx2, "GameContext.Instance 应返回同一上下文单例");
            Assert.IsNotNull(ctx1.Rank, "Rank 应在 OnInit 时装配好，非空");
            Assert.AreSame(ctx1.Rank, ctx2.Rank, "多次访问应是同一 RankService（持有，不每次新建）");
        }

        [Test] // InitRankWithDeps 注入测试接缝后可用，不依赖真实 PlayerPrefs / 不连网
        public void H2_InitRankWithDeps_InjectsTestSeams()
        {
            RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            var mail = new RecordingMailService();
            InjectRank(FixedSource(700, 100, new List<RankEntry> { Filler(900, 10) }), mail);

            var board = GameContext.Instance.Rank.GetBoard(BoardWeekly);
            Assert.IsNotNull(board, "经注入服务应能查榜");
            Assert.AreEqual(2, board.Entries.Count, "本机 + 1 陪榜 = 2 条（来自注入源，非真实存档）");
            Assert.AreEqual(0, mail.Sent.Count, "查榜不发邮件（无领取 / 结算）");
        }

        // ════════════ H3：经 GameContext 查榜数据贯通（排序 / 名次）════════════

        [Test] // 分降序 + 名次 1 起回填 + 本机 IsSelf + SelfRank/SelfScore 一致（设计 22 SO1 经 GameContext 复验）
        public void H3_GetBoard_ViaContext_SortsAndFillsRank()
        {
            RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            // 本机 500@t100；陪榜 800@t50, 300@t10
            var fillers = new List<RankEntry> { Filler(800, 50), Filler(300, 10) };
            InjectRank(FixedSource(500, 100, fillers), new RecordingMailService());

            var board = GameContext.Instance.Rank.GetBoard(BoardWeekly);
            Assert.AreEqual(3, board.Entries.Count);
            Assert.AreEqual(800, board.Entries[0].Score); Assert.AreEqual(1, board.Entries[0].Rank);
            Assert.AreEqual(500, board.Entries[1].Score); Assert.AreEqual(2, board.Entries[1].Rank);
            Assert.IsTrue(board.Entries[1].IsSelf, "本机 500 应标 IsSelf");
            Assert.AreEqual(300, board.Entries[2].Score); Assert.AreEqual(3, board.Entries[2].Rank);
            Assert.AreEqual(2, board.SelfRank, "本机第2");
            Assert.AreEqual(500, board.SelfScore);
        }

        // ════════════ W3：窗口 board → 行 VM 列表 映射纯方法 ════════════

        [Test] // 行数 == Entries.Count；逐行名次 / 名 / 分 == 数据；本机行 IsSelf 标对
        public void W3_BuildRowModels_MapsEntries()
        {
            RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            var fillers = new List<RankEntry> { Filler(800, 50), Filler(300, 10) };
            InjectRank(FixedSource(500, 100, fillers), new RecordingMailService());
            var board = GameContext.Instance.Rank.GetBoard(BoardWeekly);

            var rows = UIRankPanel.BuildRowModels(board);
            Assert.AreEqual(board.Entries.Count, rows.Count, "行数 == Entries.Count");

            // 首行 = 名次1 / 800 分（陪榜，非本机）
            Assert.AreEqual(1, rows[0].Rank);
            Assert.AreEqual("800", rows[0].Score);
            Assert.IsFalse(rows[0].IsSelf);
            Assert.AreEqual("玩家9000", rows[0].Name, "陪榜名 = 占位「玩家+textId」");

            // 第2行 = 本机 500（IsSelf）
            Assert.AreEqual(2, rows[1].Rank);
            Assert.AreEqual("500", rows[1].Score);
            Assert.IsTrue(rows[1].IsSelf, "本机行 IsSelf=true");
            Assert.AreEqual("玩家110820", rows[1].Name, "本机名 = 占位「玩家+本机 nameTextId」");
        }

        // ════════════ W3：我的名次条文本映射纯方法 ════════════

        [Test] // 入榜：MyRankText==名次、MyScoreText==★分、MyNameText==占位名
        public void W3_MyRankText_InBoard()
        {
            RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            InjectRank(FixedSource(700, 100, new List<RankEntry> { Filler(900, 10) }), new RecordingMailService());
            var board = GameContext.Instance.Rank.GetBoard(BoardWeekly);

            Assert.AreEqual(2, board.SelfRank, "900 第1，本机 700 第2");
            Assert.AreEqual("2", UIRankPanel.MyRankText(board), "我的名次文本 == SelfRank");
            Assert.AreEqual("★700", UIRankPanel.MyScoreText(board), "我的成绩文本 == ★SelfScore");
            Assert.AreEqual("玩家110820", UIRankPanel.MyNameText(board), "我的名字 = 占位本机名");
        }

        // ════════════ W6：未入榜 / 空榜 / null 不崩 ════════════

        [Test] // 未入榜（本机 50 < condition 100）→ SelfRank==0：MyRankText「--」/ MyNameText「未上榜」/ 成绩仍显最佳分
        public void W6_NotRanked_FriendlyState()
        {
            RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            // 本机 50 < 100 不入榜；陪榜 900 入榜
            InjectRank(FixedSource(50, 100, new List<RankEntry> { Filler(900, 10) }), new RecordingMailService());
            var board = GameContext.Instance.Rank.GetBoard(BoardWeekly);

            Assert.AreEqual(0, board.SelfRank, "未达入榜要求 → SelfRank 0");
            Assert.AreEqual("--", UIRankPanel.MyRankText(board), "未入榜名次显 --");
            Assert.AreEqual("未上榜", UIRankPanel.MyNameText(board), "未入榜名字显「未上榜」");
            Assert.AreEqual("★50", UIRankPanel.MyScoreText(board), "未入榜仍显当前最佳分");

            // 行列表只含入榜的陪榜，不含本机 50 分，且不抛
            var rows = UIRankPanel.BuildRowModels(board);
            foreach (var r in rows) Assert.AreNotEqual("50", r.Score, "本机 50 分未入榜，不在行列表");
        }

        [Test] // 空榜 / board==null：BuildRowModels 返空、我的名次条走未入榜友好态，全程不抛（W6）
        public void W6_EmptyAndNullBoard_NoThrow()
        {
            // board == null（榜不存在）
            Assert.DoesNotThrow(() =>
            {
                var rows = UIRankPanel.BuildRowModels(null);
                Assert.AreEqual(0, rows.Count, "null board → 空行列表");
                Assert.AreEqual("--", UIRankPanel.MyRankText(null));
                Assert.AreEqual("未上榜", UIRankPanel.MyNameText(null));
                Assert.AreEqual("★0", UIRankPanel.MyScoreText(null));
            });

            // Entries 为 null 的 board
            Assert.DoesNotThrow(() =>
            {
                var rows = UIRankPanel.BuildRowModels(new RankBoard { Id = 1, Entries = null });
                Assert.AreEqual(0, rows.Count, "Entries==null → 空行列表");
            });

            // 空 Entries 的 board
            Assert.DoesNotThrow(() =>
            {
                var rows = UIRankPanel.BuildRowModels(new RankBoard { Id = 1, Entries = new List<RankEntry>() });
                Assert.AreEqual(0, rows.Count, "空 Entries → 空行列表");
            });
        }

        [Test] // 查不存在的榜 id（GetBoard 返 null）→ 窗口映射不抛（贯通 GameContext + 数据层 W6）
        public void W6_NonexistentBoard_GetBoardNull_NoThrow()
        {
            RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            InjectRank(FixedSource(500, 100, null), new RecordingMailService());

            var board = GameContext.Instance.Rank.GetBoard(999);  // 不存在 → null
            Assert.IsNull(board, "榜不存在 → GetBoard 返 null");
            Assert.DoesNotThrow(() =>
            {
                Assert.AreEqual(0, UIRankPanel.BuildRowModels(board).Count);
                Assert.AreEqual("--", UIRankPanel.MyRankText(board));
            });
        }
    }
}
