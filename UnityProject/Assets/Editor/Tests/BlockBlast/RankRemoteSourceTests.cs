using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using GameLogic.Rank;
using GameLogic.Config;
using GameLogic.Mail;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 排行榜客户端段（上后端，设计 31 §8.2 CV1-CV5）：远程数据源切真实 RPC + 断服回退本地源 + 不自报账号 + 服务层行为不变。
    /// 纯逻辑 EditMode：注入桩 <see cref="IRemoteRankSource"/> 模拟各分支回包（成功 / 服务不可用 / 榜不存在），
    /// 断服分支用真实 <see cref="RemoteRankSource"/> 在测试环境（无会话）验证降级不抛。
    /// 不连真服（真往返交联调 E1，PlayMode）。
    /// </summary>
    /// <remarks>
    /// UniTask 同步驱动：桩只 await CompletedTask、真 RemoteRankSource 在无会话时同步返回，
    /// 故 <c>task.GetAwaiter().GetResult()</c> 可同步取结果（同 redeem 客户端段单测口径，dev memory）。
    /// </remarks>
    [TestFixture]
    public class RankRemoteSourceTests
    {
        private const int BoardId = 1;     // Condition=100（同 RankSystemTests WeeklyDef）
        private const int BoardAlways = 2; // Condition=0

        private sealed class RecordingMailService : IMailService
        {
            public long Send(MailDraft draft) => 1;
        }

        // ── 桩远程源：按构造注入的回包模拟 RPC 结果（不连网）─────────
        private sealed class StubRemoteRankSource : IRemoteRankSource
        {
            private readonly RankBoard _board;            // null = 模拟断服 / 服务不可用 / 榜不存在
            private readonly RankSubmitOutcome _submit;
            public int QueryCallCount;
            public int SubmitCallCount;
            public int LastSubmitRankId = -1;
            public long LastSubmitScore = long.MinValue;

            public StubRemoteRankSource(RankBoard board, RankSubmitOutcome submit)
            {
                _board = board;
                _submit = submit;
            }

            public IReadOnlyList<RankEntry> Fetch(int rankId) => System.Array.Empty<RankEntry>();

            public async UniTask<RankSubmitOutcome> SubmitScoreAsync(int rankId, long score)
            {
                SubmitCallCount++;
                LastSubmitRankId = rankId;
                LastSubmitScore = score;
                await UniTask.CompletedTask;
                return _submit;
            }

            public async UniTask<RankBoard> QueryBoardAsync(int rankId)
            {
                QueryCallCount++;
                await UniTask.CompletedTask;
                return _board;
            }
        }

        private static RankDef WeeklyDef()
            => new RankDef
            {
                Id = BoardId, NameTextId = 110811, Group = 1, Method = 1, Condition = 100,
                ValidType = RankValidType.Weekly, ValidVal = 1, MailDefId = 1, CountMax = 100, ShowMax = 50,
                Tiers = new List<RankRewardTier>
                {
                    new RankRewardTier { RankMin = 1, RankMax = 1, RewardPoolId = 1002 },
                },
            };

        private static RankDef AlwaysDef()
            => new RankDef
            {
                Id = BoardAlways, NameTextId = 110812, Group = 2, Method = 1, Condition = 0,
                ValidType = RankValidType.Always, ValidVal = 0, MailDefId = 1, CountMax = 100, ShowMax = 50,
                Tiers = new List<RankRewardTier> { new RankRewardTier { RankMin = 1, RankMax = 1, RewardPoolId = 1007 } },
            };

        private static RankEntry Filler(long score, long ticks, int name = 9000)
            => new RankEntry { IsSelf = false, Score = score, AchievedTicks = ticks, PlayerNameTextId = name };

        private static LocalRankSource FixedLocal(long selfScore, long selfTicks, IReadOnlyList<RankEntry> fillers)
            => new LocalRankSource(_ => (selfScore, selfTicks, 110820), _ => fillers);

        private static T Sync<T>(UniTask<T> task) => task.GetAwaiter().GetResult();
        private static void Sync(UniTask task) => task.GetAwaiter().GetResult();

        [SetUp]
        public void SetUp() { RankConfigMgr.ResetForTest(); MailConfigMgr.ResetForTest(); }

        [TearDown]
        public void TearDown() { RankConfigMgr.ResetForTest(); MailConfigMgr.ResetForTest(); }

        // ════════════ CV1：远程切真实 RPC，成功渲染 ════════════

        [Test] // CV1a：远程返已排序快照 → GetBoardAsync 直接采用（短路本地排序），渲染成功
        public void CV1_RemoteSuccess_BoardFromServer()
        {
            RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            // 服务端已排好：第1名 900（他人）、第2名 700（本人），MyRank=2/MyScore=700
            var serverBoard = new RankBoard
            {
                Id = BoardId,
                Entries = new List<RankEntry>
                {
                    new RankEntry { Rank = 1, Score = 900, RemoteName = "acc_A", IsSelf = false },
                    new RankEntry { Rank = 2, Score = 700, RemoteName = "acc_Me", IsSelf = true },
                },
                Self = null, SelfRank = 2, SelfScore = 700,
            };
            var remote = new StubRemoteRankSource(serverBoard, default);
            var svc = new RankService(FixedLocal(0, 0, null), new InMemoryRankPersistence(),
                                      new RecordingMailService(), cfg: null, remote: remote);

            var board = Sync(svc.GetBoardAsync(BoardId));
            Assert.AreEqual(1, remote.QueryCallCount, "走了远程 RPC");
            Assert.IsNotNull(board);
            Assert.AreEqual(2, board.Entries.Count);
            Assert.AreEqual(900, board.Entries[0].Score); Assert.AreEqual(1, board.Entries[0].Rank);
            Assert.AreEqual(700, board.Entries[1].Score); Assert.AreEqual(2, board.Entries[1].Rank);
            Assert.AreEqual(2, board.SelfRank, "服务端算的名次直接采用");
            Assert.AreEqual(700, board.SelfScore);
        }

        [Test] // CV1b：上报成功 → 返服务端裁决码 + 最佳分；本地最佳也同步更新（断服时仍有本机记录）
        public void CV1_RemoteSubmit_ReturnsServerVerdict_AndUpdatesLocal()
        {
            RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            var remote = new StubRemoteRankSource(null, new RankSubmitOutcome(RankSubmitCode.BestRefreshed, 850));
            var persist = new InMemoryRankPersistence();
            var svc = new RankService(FixedLocal(0, 0, null), persist, new RecordingMailService(), cfg: null, remote: remote);

            var outcome = Sync(svc.SubmitScoreAsync(BoardId, 850));
            Assert.AreEqual(1, remote.SubmitCallCount);
            Assert.AreEqual(RankSubmitCode.BestRefreshed, outcome.Code);
            Assert.AreEqual(850, outcome.BestScore, "服务端回的当前最佳");
            Assert.AreEqual(850, svc.GetMyBest(BoardId).score, "本地最佳同步更新（断服时本地源仍记本机最佳）");
        }

        // ════════════ CV1/CV4：断服回退本地源，不阻断玩法 ════════════

        [Test] // CV1c：远程查榜返 null（断服 / 服务不可用 / 榜不存在）→ 回退本地源同步榜，不伪造全服名次
        public void CV1_RemoteUnavailable_FallsBackToLocalBoard()
        {
            RankConfigMgr.InitForTest(new[] { AlwaysDef() }); // Condition=0，本机能进
            var remote = new StubRemoteRankSource(null, RankSubmitOutcome.ServiceUnavailable); // null = 断服
            var local = FixedLocal(500, 100, new List<RankEntry> { Filler(900, 10) });
            var svc = new RankService(local, new InMemoryRankPersistence(), new RecordingMailService(), cfg: null, remote: remote);

            var board = Sync(svc.GetBoardAsync(BoardAlways));
            Assert.AreEqual(1, remote.QueryCallCount, "尝试了远程");
            Assert.IsNotNull(board, "回退本地源");
            Assert.AreEqual(2, board.Entries.Count, "本地源：本机 + 陪榜");
            Assert.AreEqual(2, board.SelfRank, "本机 500 第2（本地排序），非伪造全服名次");
        }

        [Test] // CV4：真 RemoteRankSource 在测试环境（无会话）→ QueryBoardAsync 返 null、SubmitScoreAsync 返 ServiceUnavailable，绝不抛
        public void CV4_RealRemote_NoSession_DegradesNotThrow()
        {
            var remote = new RemoteRankSource();
            RankBoard board = null;
            RankSubmitOutcome submit = default;
            Assert.DoesNotThrow(() => board = Sync(remote.QueryBoardAsync(BoardId)), "无会话查榜不抛");
            Assert.IsNull(board, "无会话 → 返 null（调用方回退本地源）");
            Assert.DoesNotThrow(() => submit = Sync(remote.SubmitScoreAsync(BoardId, 500)), "无会话上报不抛");
            Assert.AreEqual(RankSubmitCode.ServiceUnavailable, submit.Code, "无会话上报 → 服务不可用");
        }

        [Test] // CV4b：经服务层断服上报 → 本地最佳仍更新（玩法不阻断），返回服务不可用
        public void CV4_Service_SubmitDegrade_StillUpdatesLocal()
        {
            RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            var remote = new StubRemoteRankSource(null, RankSubmitOutcome.ServiceUnavailable);
            var svc = new RankService(FixedLocal(0, 0, null), new InMemoryRankPersistence(),
                                      new RecordingMailService(), cfg: null, remote: remote);

            var outcome = Sync(svc.SubmitScoreAsync(BoardId, 600));
            Assert.AreEqual(RankSubmitCode.ServiceUnavailable, outcome.Code, "断服 → 服务不可用");
            Assert.AreEqual(600, svc.GetMyBest(BoardId).score, "断服仍记本机最佳，玩法不阻断（设计 31 §四）");
        }

        // ════════════ CV2：短路本地排序，两条源同一快照结构 ════════════

        [Test] // CV2：远程源不重排服务端结果（即便条目顺序「乱」也原样采用），不按本地 Ticks 重排
        public void CV2_RemoteShortCircuitsLocalSort()
        {
            RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            // 故意给一个「未按客户端规则排序」的服务端快照（名次字段是权威）：客户端不得重排
            var serverBoard = new RankBoard
            {
                Id = BoardId,
                Entries = new List<RankEntry>
                {
                    new RankEntry { Rank = 1, Score = 300, RemoteName = "first_by_server", AchievedTicks = 0 },
                    new RankEntry { Rank = 2, Score = 999, RemoteName = "second_by_server", AchievedTicks = 0 },
                },
                Self = null, SelfRank = 0, SelfScore = 0,
            };
            var remote = new StubRemoteRankSource(serverBoard, default);
            var svc = new RankService(FixedLocal(0, 0, null), new InMemoryRankPersistence(),
                                      new RecordingMailService(), cfg: null, remote: remote);

            var board = Sync(svc.GetBoardAsync(BoardId));
            // 若客户端误重排，300 分会被排到 999 之后；短路本地排序则保留服务端顺序
            Assert.AreEqual(300, board.Entries[0].Score, "服务端顺序原样保留（未按分数重排）");
            Assert.AreEqual(1, board.Entries[0].Rank, "名次用服务端的");
            Assert.AreEqual(999, board.Entries[1].Score);
            Assert.AreEqual("first_by_server", board.Entries[0].RemoteName, "展示名用服务端回的（账号占位）");
        }

        // ════════════ CV2：服务端响应 → 查询快照转换（IsSelf 按名次匹配）════════════

        [Test] // CV2b：BuildBoardFromServer —— 本人 = 名次匹配 MyRank 的条目；MyRank=0/落展示外 → self=null 但名次/分照回
        public void CV2_BuildBoardFromServer_IsSelfByRank()
        {
            var sorted = new List<RankEntry>
            {
                new RankEntry { Rank = 1, Score = 900, RemoteName = "A" },
                new RankEntry { Rank = 2, Score = 700, RemoteName = "Me" },
                new RankEntry { Rank = 3, Score = 500, RemoteName = "C" },
            };
            var board = RemoteRankSource.BuildBoardFromServer(BoardId, sorted, myRank: 2, myScore: 700);
            Assert.AreEqual(3, board.Entries.Count, "条目原样采用，不重排");
            Assert.AreEqual(1, board.Entries[0].Rank); Assert.IsFalse(board.Entries[0].IsSelf);
            Assert.IsTrue(board.Entries[1].IsSelf, "名次=2 == MyRank → 本人");
            Assert.AreSame(board.Entries[1], board.Self, "Self 指向名次匹配的条目");
            Assert.AreEqual(2, board.SelfRank);
            Assert.AreEqual(700, board.SelfScore);

            // MyRank=0（未入榜）→ 无 self，但分照回（供距上榜差值）
            var board0 = RemoteRankSource.BuildBoardFromServer(BoardId, sorted, myRank: 0, myScore: 120);
            Assert.IsNull(board0.Self);
            foreach (var e in board0.Entries) Assert.IsFalse(e.IsSelf, "MyRank=0 → 无本人标记");
            Assert.AreEqual(0, board0.SelfRank);
            Assert.AreEqual(120, board0.SelfScore, "未入榜分数照回");

            // MyRank 在展示条目外（如第 5 名但只展示前 3）→ self=null、名次/分照回
            var boardOut = RemoteRankSource.BuildBoardFromServer(BoardId, sorted, myRank: 5, myScore: 300);
            Assert.IsNull(boardOut.Self, "名次落展示外 → 展示条目里无本人");
            Assert.AreEqual(5, boardOut.SelfRank, "名次仍按服务端权威回");
            Assert.AreEqual(300, boardOut.SelfScore);

            // 空条目（空榜）不抛
            var empty = RemoteRankSource.BuildBoardFromServer(BoardId, null, myRank: 0, myScore: 0);
            Assert.IsNotNull(empty.Entries);
            Assert.AreEqual(0, empty.Entries.Count);
        }

        // ════════════ CV3：请求不自报账号（协议契约 + 服务层透传）════════════

        [Test] // CV3：上报 RPC 只传榜 id + 分数（无账号字段，编译期保证）；服务层透传 rankId/score 不附身份
        public void CV3_SubmitsRankIdAndScore_NoAccount()
        {
            RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            var remote = new StubRemoteRankSource(null, new RankSubmitOutcome(RankSubmitCode.BestRefreshed, 777));
            var svc = new RankService(FixedLocal(0, 0, null), new InMemoryRankPersistence(),
                                      new RecordingMailService(), cfg: null, remote: remote);

            Sync(svc.SubmitScoreAsync(BoardId, 777));
            Assert.AreEqual(BoardId, remote.LastSubmitRankId, "服务层原样透传榜 id（无账号附加）");
            Assert.AreEqual(777, remote.LastSubmitScore, "服务层原样透传分数（无账号附加）");
            // 客户端 API 面只接 rankId + score、不接账号参数（IRemoteRankSource.SubmitScoreAsync / QueryBoardAsync 签名仅榜 id[+分数]）；
            // 协议请求消息无账号字段（身份从会话取，SV8）由生成物 + 服务端段保证，client EditMode 不重复核生成物字段。
        }

        // ════════════ CV4(服务层行为不变)：无远程源时异步入口等价同步本地路径 ════════════

        [Test] // CV4c：无远程源（离线）→ GetBoardAsync 等价 GetBoard；SubmitScoreAsync 等价 SubmitScore + 返 ServiceUnavailable
        public void CV4_NoRemote_AsyncEqualsSyncLocal()
        {
            RankConfigMgr.InitForTest(new[] { AlwaysDef() });
            var local = FixedLocal(500, 100, new List<RankEntry> { Filler(900, 10) });
            var svc = new RankService(local, new InMemoryRankPersistence(), new RecordingMailService()); // remote=null

            var asyncBoard = Sync(svc.GetBoardAsync(BoardAlways));
            var syncBoard = svc.GetBoard(BoardAlways);
            Assert.AreEqual(syncBoard.Entries.Count, asyncBoard.Entries.Count, "离线异步入口 == 同步本地路径");
            Assert.AreEqual(syncBoard.SelfRank, asyncBoard.SelfRank);

            // 离线上报：本地最佳更新 + 返 ServiceUnavailable（无远程裁决）
            RankConfigMgr.ResetForTest(); RankConfigMgr.InitForTest(new[] { WeeklyDef() });
            var svc2 = new RankService(FixedLocal(0, 0, null), new InMemoryRankPersistence(), new RecordingMailService());
            var outcome = Sync(svc2.SubmitScoreAsync(BoardId, 650));
            Assert.AreEqual(RankSubmitCode.ServiceUnavailable, outcome.Code, "离线无远程裁决 → 服务不可用");
            Assert.AreEqual(650, svc2.GetMyBest(BoardId).score, "离线本地最佳照常更新");
        }

        // ════════════ CV5(短路对称)：远程成功不触本地源 Fetch；本地回退才走 Fetch ════════════

        [Test] // CV5：远程查榜成功时不读本地源（短路）；远程不可用回退时才读本地源
        public void CV5_RemoteSuccess_DoesNotReadLocalSource()
        {
            RankConfigMgr.InitForTest(new[] { AlwaysDef() });
            int localFetchCount = 0;
            var countingLocal = new LocalRankSource(
                _ => { return (500, 100, 110820); },
                rid => { localFetchCount++; return new List<RankEntry> { Filler(900, 10) }; });

            // 远程成功：服务端快照直接用，不读本地
            var serverBoard = new RankBoard
            {
                Id = BoardAlways,
                Entries = new List<RankEntry> { new RankEntry { Rank = 1, Score = 1000, RemoteName = "s" } },
                Self = null, SelfRank = 0, SelfScore = 0,
            };
            var svcOk = new RankService(countingLocal, new InMemoryRankPersistence(),
                                        new RecordingMailService(), cfg: null, remote: new StubRemoteRankSource(serverBoard, default));
            Sync(svcOk.GetBoardAsync(BoardAlways));
            Assert.AreEqual(0, localFetchCount, "远程成功 → 不读本地源（短路本地排序，CV2）");

            // 远程不可用：回退读本地源
            localFetchCount = 0;
            var svcDown = new RankService(countingLocal, new InMemoryRankPersistence(),
                                          new RecordingMailService(), cfg: null, remote: new StubRemoteRankSource(null, default));
            Sync(svcDown.GetBoardAsync(BoardAlways));
            Assert.AreEqual(1, localFetchCount, "远程不可用 → 回退读本地源（CV1）");
        }
    }
}
