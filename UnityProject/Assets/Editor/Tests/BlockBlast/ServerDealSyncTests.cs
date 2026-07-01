using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Algorithms;
using GameLogic.BlockBlast.Core;
using GameLogic.BlockBlast.Player;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 服务端权威发牌预测/对账引擎 <see cref="ServerDealSync"/> 的纯逻辑单测。
    ///
    /// 核心断言:客户端预测(同服务端 seed + 服务端镜像权重配置)与服务端权威逐位一致 → 对账永不触发覆盖
    /// (<see cref="ServerDealSync.LastReconcileCorrected"/> 全程 false)。服务端权威态由 <see cref="ServerSim"/> 用与生产服务端
    /// <c>GameSessionHelper</c> 同口径的发牌节律就地模拟(发牌核心已被确定性 harness 证明与 golden 逐位一致)。
    /// 另测:故意制造分歧时对账正确覆盖;幂等/超前码不误判;快照恢复正确。
    /// </summary>
    [TestFixture]
    public class ServerDealSyncTests
    {
        private const long Seed = 1337;

        // ─── 服务端权威态就地模拟(与 GameSessionHelper 同口径)──────────────
        // 复刻服务端单局发牌:portable PRNG(同 seed)+ 服务端镜像权重配置 + persistence=null,
        // 落子每次 AddWeight、整批消耗才 OfferTrio,计分用 BlockScoring。这套节律即确定性 golden 基线。
        private sealed class ServerSim
        {
            public readonly BinaryBoard Board = new BinaryBoard();
            public int Step;
            public int Score;
            public readonly List<int> CandidateQueue = new List<int>();
            private readonly DynamicWeightDiff _gen;
            private AlgorithmKind _lastTrioAlgo = AlgorithmKind.RandomNoDie;

            public ServerSim(long seed)
            {
                _gen = new DynamicWeightDiff(new XorShift128PlusRng(seed));
                _gen.ForceAlgorithm = null;
                _gen.Init(BlockGenWeightConfig.ServerMirror());
                _gen.Reset();
                _gen.BeginGame();
                _gen.LastAlgo = null;
                _gen.LastTierId = null;
                RefillTrio();
            }

            public List<int> InitialTrio => new List<int>(CandidateQueue);

            public GenStateView Gen()
            {
                // 全态导出(含 PRNG 游标 + LastAlgo/LastTierId),与生产服务端 rehydrate 回带的 BlockGenState 同口径,
                // 使客户端可经 ImportFullState 完全复位(含游标),而非只对齐 5 标量。
                var full = _gen.ExportFullState();
                return new GenStateView(new List<int>(CandidateQueue), full.DynamicWeight,
                    full.PreDynamicWeight, full.RefillIndex, full.BcInWindow, full.BcCooldown,
                    full.RngS0, full.RngS1, full.LastAlgo, full.LastTierId);
            }

            /// <summary>服务端发牌器当前游标(供单测断言客户端复位后逐位接续)。</summary>
            public (ulong s0, ulong s1) RngCursor()
            {
                var full = _gen.ExportFullState();
                return (full.RngS0, full.RngS1);
            }

            public List<int> BoardRows()
            {
                var rows = new List<int>(BinaryBoard.RowCount);
                for (int r = 0; r < BinaryBoard.RowCount; r++) rows.Add(Board.RowBinary[r]);
                return rows;
            }

            /// <summary>权威落子(与 GameSessionHelper.Place 同口径)。返回 (resultCode, eliminatedLines, newCandidate)。</summary>
            public PlaceResult Place(int candidateIndex, int posX, int posY)
            {
                if (candidateIndex < 0 || candidateIndex >= CandidateQueue.Count)
                    return PlaceResult.Fail(DealResultCode.IllegalPlacement);
                int shapeId = CandidateQueue[candidateIndex];
                if (shapeId <= 0) return PlaceResult.Fail(DealResultCode.IllegalPlacement);
                var pos = new Vec2Int(posX, posY);
                if (!Board.CanPutBlock(shapeId, pos)) return PlaceResult.Fail(DealResultCode.IllegalPlacement);

                int placedCells = BlockShapeMap.GetCellCount(shapeId);
                Board.PutBlock(shapeId, pos);
                CandidateQueue[candidateIndex] = 0;

                var clear = Board.CanClearRowCols(true);
                int rows = clear.Rows.Count, cols = clear.Cols.Count;
                int lines = rows + cols;
                int clearedCells = rows * BinaryBoard.ColCount + cols * BinaryBoard.RowCount - rows * cols;
                int placeScore = BlockScoring.PlacementScore(placedCells);
                int clearScore = lines > 0 ? BlockScoring.ClearScore(clearedCells, lines) : 0;
                Score += placeScore + clearScore;

                _gen.AddWeight(_lastTrioAlgo);

                int newCandidate = -1;
                if (AllConsumed()) newCandidate = RefillTrio();
                Step++;

                return new PlaceResult(DealResultCode.Ok, Step, Score, lines, newCandidate, BoardRows(), Gen());
            }

            /// <summary>权威消除道具(与 GameSessionHelper.ClearTool 同口径):清整行整列 + Step +1,不动候选/发牌器/分数。</summary>
            public ClearToolResult ClearTool(int posX, int posY)
            {
                if (posX < 0 || posX >= BinaryBoard.ColCount || posY < 0 || posY >= BinaryBoard.RowCount)
                    return ClearToolResult.Fail(DealResultCode.OutOfRange);

                int before = CountOccupied(Board);
                Board.RowBinary[posY] = 0;
                int colClearMask = ~(1 << (BinaryBoard.ColCount - posX - 1)) & BinaryBoard.FullRow;
                for (int r = 0; r < BinaryBoard.RowCount; r++) Board.RowBinary[r] &= colClearMask;
                int cleared = before - CountOccupied(Board);
                Step++; // 消除道具作为 board-mutating 动作推进 Step,但不消耗候选、不推进发牌
                return new ClearToolResult(DealResultCode.Ok, Step, Score, cleared, BoardRows(), Gen(), newEnergy: 0);
            }

            private static int CountOccupied(BinaryBoard board)
            {
                int count = 0;
                for (int r = 0; r < BinaryBoard.RowCount; r++)
                {
                    int bits = board.RowBinary[r] & BinaryBoard.FullRow;
                    while (bits != 0) { bits &= bits - 1; count++; }
                }
                return count;
            }

            private int RefillTrio()
            {
                var offer = _gen.OfferTrio(Board, Score);
                CandidateQueue.Clear();
                for (int i = 0; i < 3; i++) CandidateQueue.Add(offer.Ids[i]);
                _lastTrioAlgo = offer.Algo;
                return CandidateQueue.Count > 0 ? CandidateQueue[0] : -1;
            }

            private bool AllConsumed()
            {
                for (int i = 0; i < CandidateQueue.Count; i++) if (CandidateQueue[i] > 0) return false;
                return true;
            }
        }

        /// <summary>桩网关:直接驱动 <see cref="ServerSim"/> 充当服务端,使预测可对账真权威态(无网络)。</summary>
        private sealed class SimGateway : IBlockGameGateway
        {
            private readonly long _seed;
            public ServerSim Sim;

            public SimGateway(long seed) => _seed = seed;

            public UniTask<GameStartResult> GameStartAsync()
            {
                Sim = new ServerSim(_seed);
                var r = new GameStartResult(DealResultCode.Ok, 42, _seed, Sim.InitialTrio, 0, Sim.Gen());
                return UniTask.FromResult(r);
            }

            public UniTask<PlaceResult> PlaceAsync(long gameId, int baseStep, int candidateIndex, int posX, int posY)
                => UniTask.FromResult(Sim.Place(candidateIndex, posX, posY));

            public UniTask<SnapshotResult> GameSnapshotAsync(long gameId)
            {
                var r = new SnapshotResult(DealResultCode.Ok, Sim.BoardRows(), Sim.Score, Sim.Step,
                    new List<int>(Sim.CandidateQueue), Sim.Gen());
                return UniTask.FromResult(r);
            }

            public UniTask<ClearToolResult> ClearToolAsync(long gameId, int baseStep, int posX, int posY)
                => UniTask.FromResult(Sim.ClearTool(posX, posY));
        }

        /// <summary>玩家确定性策略(与 GenCoreDeterminismHarness 同口径):首个有合法落点的槽,落点取 GetCanPutPoss 固定序首个。</summary>
        private static bool PickPlacement(IList<int> queue, BinaryBoard board, out int slot, out int posX, out int posY)
        {
            slot = -1; posX = 0; posY = 0;
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i] <= 0) continue;
                var poss = board.GetCanPutPoss(queue[i]);
                if (poss.Count > 0) { slot = i; posX = poss[0].X; posY = poss[0].Y; return true; }
            }
            return false;
        }

        [Test]
        public void Prediction_MatchesServer_NoReconcileCorrection()
        {
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);

            bool started = deal.StartGameAsync().GetAwaiter().GetResult();
            Assert.IsTrue(started, "GameStart 应成功建局");
            Assert.IsTrue(deal.HasGame);

            // 建局后预测候选 == 服务端首批
            CollectionAssert.AreEqual(gateway.Sim.CandidateQueue, deal.CandidateQueue, "建局首批候选应与服务端一致");

            int placed = 0;
            for (int step = 0; step < 240; step++)
            {
                // 卡死(三槽皆无处可放):确定性清全盘续局(与 harness 同口径)——清客户端预测盘 + 服务端盘。
                if (!PickPlacement(deal.CandidateQueue, deal.Board, out int slot, out int x, out int y))
                {
                    ClearBoard(deal.Board);
                    ClearBoard(gateway.Sim.Board);
                    if (!PickPlacement(deal.CandidateQueue, deal.Board, out slot, out x, out y)) break;
                }

                int baseStep = deal.Step;
                var predicted = deal.PredictPlace(slot, x, y);
                Assert.IsTrue(predicted.Accepted, $"step {step}: 预测应接受落子");

                var result = deal.PlaceAsync(baseStep, slot, x, y).GetAwaiter().GetResult();
                Assert.AreEqual(DealResultCode.Ok, result.Code, $"step {step}: 服务端应 StepAdvanced");
                Assert.IsFalse(deal.LastReconcileCorrected,
                    $"step {step}: 预测应与服务端逐位一致、对账不应覆盖(预测 step={deal.Step} score={deal.Score})");

                placed++;
            }

            Assert.Greater(placed, 0, "应至少成功落子若干步");
            // 整局结束后预测态与服务端态逐字段一致
            Assert.AreEqual(gateway.Sim.Step, deal.Step, "终局 step 应一致");
            Assert.AreEqual(gateway.Sim.Score, deal.Score, "终局 score 应一致");
            CollectionAssert.AreEqual(gateway.Sim.BoardRows(), BoardRows(deal.Board), "终局盘面应一致");
            CollectionAssert.AreEqual(gateway.Sim.CandidateQueue, deal.CandidateQueue, "终局候选应一致");
        }

        [Test]
        public void Reconcile_OverwritesWhenServerDiverges()
        {
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);
            deal.StartGameAsync().GetAwaiter().GetResult();

            PickPlacement(deal.CandidateQueue, deal.Board, out int slot, out int x, out int y);
            int baseStep = deal.Step;
            deal.PredictPlace(slot, x, y);

            // 造一个与预测分歧的服务端响应:score 故意 +999、board 清零。
            var divergent = new PlaceResult(DealResultCode.Ok, deal.Step, deal.Score + 999, 0, -1,
                new List<int> { 0, 0, 0, 0, 0, 0, 0, 0 },
                new GenStateView(new List<int> { 5, 5, 5 }, 7, 3, 9, true, 1));
            bool corrected = deal.ReconcilePlace(divergent);

            Assert.IsTrue(corrected, "分歧响应应触发对账覆盖");
            Assert.IsTrue(deal.LastReconcileCorrected);
            Assert.AreEqual(deal.Score, divergent.Score, "对账后分数应取服务端权威");
            CollectionAssert.AreEqual(new List<int> { 5, 5, 5 }, deal.CandidateQueue, "对账后候选应取服务端权威");
        }

        [Test]
        public void Reconcile_IdempotentReplay_NoSpuriousCorrectionOnAgreement()
        {
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);
            deal.StartGameAsync().GetAwaiter().GetResult();

            // 建局后预测态 == 服务端态(同 seed/配置)。用服务端 sim 的当前态构造一个「幂等回当前权威态」响应,
            // 且与预测逐位一致 → 对账不应误判覆盖(==/</> 三分支都以服务端态对齐,但一致时无可见跳变)。
            var agree = new PlaceResult(DealResultCode.IdempotentReplay, gateway.Sim.Step, gateway.Sim.Score,
                0, -1, gateway.Sim.BoardRows(), gateway.Sim.Gen());
            bool corrected = deal.ReconcilePlace(agree);
            Assert.IsFalse(corrected, "幂等且与预测一致的响应不应触发覆盖");
        }

        [Test]
        public void Reconcile_GameOver_ExposesFinalAndBestScore_AndMarksGameOver()
        {
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);
            deal.StartGameAsync().GetAwaiter().GetResult();

            Assert.IsFalse(deal.GameOver, "建局后不应处于终局态");

            PickPlacement(deal.CandidateQueue, deal.Board, out int slot, out int x, out int y);
            int baseStep = deal.Step;
            deal.PredictPlace(slot, x, y);

            // 服务端终局响应:本步是 jam 终局,回带 GameOver=true + 权威最终分/最佳分。
            // 与预测一致的盘面/分数(取预测当前态),只叠加终局字段——验证终局字段被正确暴露,且非"覆盖才记终局"。
            var terminal = new PlaceResult(DealResultCode.Ok, deal.Step, deal.Score, 0, -1,
                BoardRows(deal.Board),
                new GenStateView(new List<int>(deal.CandidateQueue), 0, 0, 0, false, 0),
                gameOver: true, finalScore: 4242, bestScore: 9999L);

            deal.ReconcilePlace(terminal);

            Assert.IsTrue(deal.GameOver, "服务端 GameOver=true 应置终局态");
            Assert.AreEqual(4242, deal.FinalScore, "应暴露服务端权威最终分");
            Assert.AreEqual(9999L, deal.BestScore, "应暴露服务端权威最佳分");
        }

        [Test]
        public void AfterGameOver_PredictAndPlaceRejected()
        {
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);
            deal.StartGameAsync().GetAwaiter().GetResult();

            // 先正常落一步并令服务端回终局。
            PickPlacement(deal.CandidateQueue, deal.Board, out int slot, out int x, out int y);
            int baseStep = deal.Step;
            deal.PredictPlace(slot, x, y);
            var terminal = new PlaceResult(DealResultCode.Ok, deal.Step, deal.Score, 0, -1,
                BoardRows(deal.Board),
                new GenStateView(new List<int>(deal.CandidateQueue), 0, 0, 0, false, 0),
                gameOver: true, finalScore: 100, bestScore: 100L);
            deal.ReconcilePlace(terminal);
            Assert.IsTrue(deal.GameOver);

            int stepBefore = deal.Step;
            int scoreBefore = deal.Score;

            // 终局后预测被拒:Accepted=false 且不改态。
            var rejected = deal.PredictPlace(0, 0, 0);
            Assert.IsFalse(rejected.Accepted, "终局后 PredictPlace 应拒绝");
            Assert.AreEqual(stepBefore, deal.Step, "终局后预测被拒不应推进 step");
            Assert.AreEqual(scoreBefore, deal.Score, "终局后预测被拒不应改分");

            // 终局后落子被拒:短路回 GameNotFound,不发 RPC。
            var place = deal.PlaceAsync(stepBefore, 0, 0, 0).GetAwaiter().GetResult();
            Assert.AreEqual(DealResultCode.GameNotFound, place.Code, "终局后 PlaceAsync 应短路回 GameNotFound");
        }

        [Test]
        public void NewGameAfterGameOver_ResetsTerminalState()
        {
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);
            deal.StartGameAsync().GetAwaiter().GetResult();

            PickPlacement(deal.CandidateQueue, deal.Board, out int slot, out int x, out int y);
            deal.PredictPlace(slot, x, y);
            deal.ReconcilePlace(new PlaceResult(DealResultCode.Ok, deal.Step, deal.Score, 0, -1,
                BoardRows(deal.Board),
                new GenStateView(new List<int>(deal.CandidateQueue), 0, 0, 0, false, 0),
                gameOver: true, finalScore: 100, bestScore: 100L));
            Assert.IsTrue(deal.GameOver);

            // 下次开窗(新局):SimGateway 默认 Resumed=false → ApplyGameStart 复位终局态,可再落子。
            bool started = deal.StartGameAsync().GetAwaiter().GetResult();
            Assert.IsTrue(started, "终局后再 GameStart 应成功(新局)");
            Assert.IsFalse(deal.GameOver, "新局应复位终局态");
            Assert.AreEqual(0, deal.FinalScore, "新局应复位 FinalScore");
            Assert.AreEqual(0L, deal.BestScore, "新局应复位 BestScore");

            // 新局可正常预测落子。
            Assert.IsTrue(PickPlacement(deal.CandidateQueue, deal.Board, out int s2, out int x2, out int y2));
            var predicted = deal.PredictPlace(s2, x2, y2);
            Assert.IsTrue(predicted.Accepted, "新局应可正常落子");
        }

        [Test]
        public void Snapshot_LoadsAuthoritativeStateAndMarksCorrected()
        {
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);
            deal.StartGameAsync().GetAwaiter().GetResult();

            var snap = new SnapshotResult(DealResultCode.Ok,
                new List<int> { 255, 0, 0, 0, 0, 0, 0, 0 }, 123, 5,
                new List<int> { 9, 0, 24 },
                new GenStateView(new List<int> { 9, 0, 24 }, 2, 1, 6, false, 0));
            deal.ApplySnapshot(snap, deal.GameId);

            Assert.AreEqual(123, deal.Score);
            Assert.AreEqual(5, deal.Step);
            CollectionAssert.AreEqual(new List<int> { 9, 0, 24 }, deal.CandidateQueue);
            Assert.AreEqual(255, deal.Board.RowBinary[0]);
            Assert.IsTrue(deal.LastReconcileCorrected, "快照恢复后应标记需整屏重绘");
        }

        [Test]
        public void Snapshot_FullStateImport_GenCursorMatchesServer()
        {
            // 用服务端 sim 跑若干步,使发牌器游标 + 全标量都已推进到非初值;再以其全态做 snapshot,
            // 断言客户端经 ImportFullState 完全复位后,预测发牌器全态(含 PRNG 游标)与服务端逐字段一致。
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);
            deal.StartGameAsync().GetAwaiter().GetResult();

            for (int step = 0; step < 30; step++)
            {
                if (!PickPlacement(gateway.Sim.CandidateQueue, gateway.Sim.Board, out int slot, out int x, out int y))
                {
                    ClearBoard(gateway.Sim.Board);
                    if (!PickPlacement(gateway.Sim.CandidateQueue, gateway.Sim.Board, out slot, out x, out y)) break;
                }
                gateway.Sim.Place(slot, x, y);
            }

            var snap = new SnapshotResult(DealResultCode.Ok, gateway.Sim.BoardRows(),
                gateway.Sim.Score, gateway.Sim.Step, new List<int>(gateway.Sim.CandidateQueue), gateway.Sim.Gen());
            deal.ApplySnapshot(snap, deal.GameId);

            var serverCursor = gateway.Sim.RngCursor();
            var clientFull = deal.ExportGenFullState();
            Assert.AreEqual(serverCursor.s0, clientFull.RngS0, "复位后 PRNG 游标 s0 应与服务端一致");
            Assert.AreEqual(serverCursor.s1, clientFull.RngS1, "复位后 PRNG 游标 s1 应与服务端一致");

            var serverFull = ServerGenFull(gateway.Sim);
            Assert.AreEqual(serverFull.DynamicWeight, clientFull.DynamicWeight, "DynamicWeight 应一致");
            Assert.AreEqual(serverFull.PreDynamicWeight, clientFull.PreDynamicWeight, "PreDynamicWeight 应一致");
            Assert.AreEqual(serverFull.RefillIndex, clientFull.RefillIndex, "RefillIndex 应一致");
            Assert.AreEqual(serverFull.BcInWindow, clientFull.BcInWindow, "BcInWindow 应一致");
            Assert.AreEqual(serverFull.BcCooldown, clientFull.BcCooldown, "BcCooldown 应一致");
            Assert.AreEqual(serverFull.LastAlgo, clientFull.LastAlgo, "LastAlgo 应一致");
            Assert.AreEqual(serverFull.LastTierId, clientFull.LastTierId, "LastTierId 应一致");
        }

        [Test]
        public void AfterTrueDivergence_FullStateImport_SubsequentDealsReconverge()
        {
            // 先建局,再用一个与预测真发散的 snapshot(服务端跑过若干步的全态)覆盖客户端;
            // 复位后客户端从该游标续接续发,后续 Place 对账应再不发生覆盖(逐位接续,根治游标无法复位的局限)。
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);
            deal.StartGameAsync().GetAwaiter().GetResult();

            // 制造发散:服务端单独推进 40 步(客户端不跟),再 snapshot 覆盖客户端 → 游标真错位后被完全复位。
            for (int step = 0; step < 40; step++)
            {
                if (!PickPlacement(gateway.Sim.CandidateQueue, gateway.Sim.Board, out int slot, out int x, out int y))
                {
                    ClearBoard(gateway.Sim.Board);
                    if (!PickPlacement(gateway.Sim.CandidateQueue, gateway.Sim.Board, out slot, out x, out y)) break;
                }
                gateway.Sim.Place(slot, x, y);
            }
            var snap = new SnapshotResult(DealResultCode.Ok, gateway.Sim.BoardRows(),
                gateway.Sim.Score, gateway.Sim.Step, new List<int>(gateway.Sim.CandidateQueue), gateway.Sim.Gen());
            deal.ApplySnapshot(snap, deal.GameId);

            // 复位后继续对局:客户端预测应与服务端逐位接续,对账不再覆盖。
            int placed = 0;
            for (int step = 0; step < 60; step++)
            {
                if (!PickPlacement(deal.CandidateQueue, deal.Board, out int slot, out int x, out int y))
                {
                    ClearBoard(deal.Board);
                    ClearBoard(gateway.Sim.Board);
                    if (!PickPlacement(deal.CandidateQueue, deal.Board, out slot, out x, out y)) break;
                }
                int baseStep = deal.Step;
                var predicted = deal.PredictPlace(slot, x, y);
                Assert.IsTrue(predicted.Accepted, $"step {step}: 复位后预测应接受落子");
                var result = deal.PlaceAsync(baseStep, slot, x, y).GetAwaiter().GetResult();
                Assert.AreEqual(DealResultCode.Ok, result.Code, $"step {step}: 服务端应 StepAdvanced");
                Assert.IsFalse(deal.LastReconcileCorrected,
                    $"step {step}: 完全复位(含游标)后对账不应再覆盖(预测 step={deal.Step} score={deal.Score})");
                placed++;
            }
            Assert.Greater(placed, 0, "复位后应至少成功落子若干步");
            Assert.AreEqual(gateway.Sim.Step, deal.Step, "复位续局后终局 step 应一致");
            Assert.AreEqual(gateway.Sim.Score, deal.Score, "复位续局后终局 score 应一致");
            CollectionAssert.AreEqual(gateway.Sim.BoardRows(), BoardRows(deal.Board), "复位续局后终局盘面应一致");
            CollectionAssert.AreEqual(gateway.Sim.CandidateQueue, deal.CandidateQueue, "复位续局后终局候选应一致");
        }

        [Test]
        public void ApplyGameStart_Resumed_RestoresBoardScoreStepAndMarksCorrected()
        {
            // 续局响应(Resumed=true):board/score/step/候选/genState 全是恢复出的中断前态。
            // 断言客户端整体恢复(非空盘新局)并标记需整屏重绘。
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);

            var gen = new GenStateView(new List<int> { 9, 0, 24 }, 7, 3, 6, false, 1,
                rngS0: 0x1234567890ABCDEFUL, rngS1: 0xFEDCBA0987654321UL, lastAlgo: 0, lastTierId: 2);
            var resumed = new GameStartResult(DealResultCode.Ok, 77, Seed,
                new List<int> { 9, 0, 24 }, 12, gen,
                resumed: true, score: 4321,
                board: new List<int> { 255, 1, 0, 0, 0, 0, 0, 0 });
            deal.ApplyGameStart(resumed);

            Assert.IsTrue(deal.HasGame);
            Assert.AreEqual(12, deal.Step, "续局应恢复中断前 step");
            Assert.AreEqual(4321, deal.Score, "续局应恢复中断前 score(非 0)");
            Assert.AreEqual(255, deal.Board.RowBinary[0], "续局应恢复盘面(非空盘)");
            Assert.AreEqual(1, deal.Board.RowBinary[1]);
            CollectionAssert.AreEqual(new List<int> { 9, 0, 24 }, deal.CandidateQueue, "续局应恢复候选");
            Assert.IsTrue(deal.LastReconcileCorrected, "续局须整屏重绘(LastReconcileCorrected=true)");

            // 发牌器游标应被完全复位到回带值(逐位接续)。
            var full = deal.ExportGenFullState();
            Assert.AreEqual(0x1234567890ABCDEFUL, full.RngS0, "续局应复位 PRNG 游标 s0");
            Assert.AreEqual(0xFEDCBA0987654321UL, full.RngS1, "续局应复位 PRNG 游标 s1");
            Assert.AreEqual(7, full.DynamicWeight);
            Assert.AreEqual(6, full.RefillIndex);
        }

        [Test]
        public void ApplyGameStart_NotResumed_StartsFreshEmptyBoardScoreZero()
        {
            // 新建响应(Resumed=false):空盘 + score 0 + step 0 + 首批 trio。
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);
            bool started = deal.StartGameAsync().GetAwaiter().GetResult(); // SimGateway 默认 Resumed=false
            Assert.IsTrue(started);
            Assert.AreEqual(0, deal.Step, "新建 step 应为 0");
            Assert.AreEqual(0, deal.Score, "新建 score 应为 0");
            for (int r = 0; r < BinaryBoard.RowCount; r++)
                Assert.AreEqual(0, deal.Board.RowBinary[r], $"新建应空盘(row {r})");
            Assert.IsFalse(deal.LastReconcileCorrected, "新建不强制整屏重绘标志");
            CollectionAssert.AreEqual(gateway.Sim.CandidateQueue, deal.CandidateQueue, "新建首批候选应与服务端一致");
        }

        // ─── 消除道具预测 / 对账 ─────────────────────────────────────

        /// <summary>在 deal 预测盘上放一个满行/满列前的可见占用图案(不经候选,直接写位掩码)供消除道具测试。</summary>
        private static void FillCell(BinaryBoard b, int col, int row)
            => b.RowBinary[row] |= 1 << (BinaryBoard.ColCount - col - 1);

        [Test]
        public void ClearTool_Predict_ClearsRowAndCol_StepPlusOne_NoCandidateNorGenChange()
        {
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);
            deal.StartGameAsync().GetAwaiter().GetResult();

            // 预置占用:目标格 (col=3,row=2) 所在整行 + 整列 + 一个不在行列上的对照格 (col=0,row=0)。
            for (int c = 0; c < BinaryBoard.ColCount; c++) FillCell(deal.Board, c, 2); // 行 2 全占
            for (int r = 0; r < BinaryBoard.RowCount; r++) FillCell(deal.Board, 3, r); // 列 3 全占
            FillCell(deal.Board, 0, 0);                                                 // 对照:不该被清

            int stepBefore = deal.Step;
            var candBefore = new List<int>(deal.CandidateQueue);
            var genBefore = deal.ExportGenFullState();

            var outcome = deal.PredictClearTool(3, 2); // PosX=col=3, PosY=row=2

            Assert.IsTrue(outcome.Accepted, "界内消除道具预测应接受");
            Assert.AreEqual(stepBefore + 1, deal.Step, "消除道具应推进 Step +1");
            Assert.AreEqual(deal.Step, outcome.Step, "outcome.Step 应为推进后步号");

            // 行 2 全清、列 3 全清。
            Assert.AreEqual(0, deal.Board.RowBinary[2], "目标行应整行清零");
            for (int r = 0; r < BinaryBoard.RowCount; r++)
            {
                int colBit = 1 << (BinaryBoard.ColCount - 3 - 1);
                Assert.AreEqual(0, deal.Board.RowBinary[r] & colBit, $"目标列在行 {r} 应清零");
            }
            // 对照格 (0,0) 不在目标行列上,应保留。
            Assert.AreNotEqual(0, deal.Board.RowBinary[0] & (1 << (BinaryBoard.ColCount - 0 - 1)), "非目标行列的占用格应保留");

            // 候选队列 + 发牌器态不动(消除道具不消耗候选、不推进发牌)。
            CollectionAssert.AreEqual(candBefore, deal.CandidateQueue, "消除道具不应动候选队列");
            var genAfter = deal.ExportGenFullState();
            Assert.AreEqual(genBefore.RngS0, genAfter.RngS0, "消除道具不应推进 PRNG 游标 s0");
            Assert.AreEqual(genBefore.RngS1, genAfter.RngS1, "消除道具不应推进 PRNG 游标 s1");
            Assert.AreEqual(genBefore.DynamicWeight, genAfter.DynamicWeight, "消除道具不应改 DynamicWeight");
            Assert.AreEqual(genBefore.RefillIndex, genAfter.RefillIndex, "消除道具不应改 RefillIndex");
        }

        [Test]
        public void ClearTool_Predict_RejectsOutOfRange_NoStateChange()
        {
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);
            deal.StartGameAsync().GetAwaiter().GetResult();
            int stepBefore = deal.Step;

            foreach (var (x, y) in new[] { (-1, 0), (0, -1), (8, 0), (0, 8) })
            {
                var outcome = deal.PredictClearTool(x, y);
                Assert.IsFalse(outcome.Accepted, $"越界 ({x},{y}) 应拒绝");
                Assert.AreEqual(stepBefore, deal.Step, "越界预测不应推进 Step");
            }
        }

        [Test]
        public void ClearTool_Predict_RejectedAfterGameOver()
        {
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);
            deal.StartGameAsync().GetAwaiter().GetResult();

            // 令终局。
            PickPlacement(deal.CandidateQueue, deal.Board, out int slot, out int x, out int y);
            deal.PredictPlace(slot, x, y);
            deal.ReconcilePlace(new PlaceResult(DealResultCode.Ok, deal.Step, deal.Score, 0, -1,
                BoardRows(deal.Board), new GenStateView(new List<int>(deal.CandidateQueue), 0, 0, 0, false, 0),
                gameOver: true, finalScore: 1, bestScore: 1L));
            Assert.IsTrue(deal.GameOver);

            int stepBefore = deal.Step;
            var outcome = deal.PredictClearTool(0, 0);
            Assert.IsFalse(outcome.Accepted, "终局后消除道具预测应拒绝");
            Assert.AreEqual(stepBefore, deal.Step, "终局后消除道具预测不应推进 Step");
        }

        [Test]
        public void ClearTool_PredictThenReconcile_MatchesServer_NoCorrection()
        {
            // 预测消除道具 + 对账真权威态(经 SimGateway 驱动 ServerSim.ClearTool),二者同口径 → 对账不应覆盖。
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);
            deal.StartGameAsync().GetAwaiter().GetResult();

            // 客户端预测盘与服务端盘同步预置占用(行 4 全占 + 列 5 全占),再消除道具 (col=5,row=4)。
            for (int c = 0; c < BinaryBoard.ColCount; c++) { FillCell(deal.Board, c, 4); FillCell(gateway.Sim.Board, c, 4); }
            for (int r = 0; r < BinaryBoard.RowCount; r++) { FillCell(deal.Board, 5, r); FillCell(gateway.Sim.Board, 5, r); }

            int baseStep = deal.Step;
            var outcome = deal.PredictClearTool(5, 4);
            Assert.IsTrue(outcome.Accepted);

            var result = deal.ClearToolAsync(baseStep, 5, 4).GetAwaiter().GetResult();
            Assert.AreEqual(DealResultCode.Ok, result.Code, "服务端应 Cleared");
            Assert.IsFalse(deal.LastReconcileCorrected, "预测与服务端逐位一致,对账不应覆盖");
            Assert.AreEqual(gateway.Sim.Step, deal.Step, "对账后 step 应一致");
            CollectionAssert.AreEqual(gateway.Sim.BoardRows(), BoardRows(deal.Board), "对账后盘面应一致");
        }

        [Test]
        public void ClearTool_ThenPlace_NoReconcileDivergence()
        {
            // 本任务修复的核心:消除道具后 deal.Board 与服务端权威一致 → 下一步落子对账不再把道具清掉的行列覆盖回来。
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);
            deal.StartGameAsync().GetAwaiter().GetResult();

            // 客户端 + 服务端同步预置占用后消除道具。
            for (int c = 0; c < BinaryBoard.ColCount; c++) { FillCell(deal.Board, c, 1); FillCell(gateway.Sim.Board, c, 1); }
            int baseStep = deal.Step;
            deal.PredictClearTool(0, 1);
            deal.ClearToolAsync(baseStep, 0, 1).GetAwaiter().GetResult();
            Assert.IsFalse(deal.LastReconcileCorrected, "消除道具对账不应覆盖");

            // 随后落子:预测 + 对账应逐位一致、不覆盖(道具清掉的行不回弹)。
            Assert.IsTrue(PickPlacement(deal.CandidateQueue, deal.Board, out int slot, out int x, out int y));
            int placeBase = deal.Step;
            var predicted = deal.PredictPlace(slot, x, y);
            Assert.IsTrue(predicted.Accepted);
            var place = deal.PlaceAsync(placeBase, slot, x, y).GetAwaiter().GetResult();
            Assert.AreEqual(DealResultCode.Ok, place.Code);
            Assert.IsFalse(deal.LastReconcileCorrected, "消除道具后落子对账不应发散(道具清掉的行列不回弹)");
        }

        [Test]
        public void ClearTool_Reconcile_OverwritesWhenServerDiverges()
        {
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);
            deal.StartGameAsync().GetAwaiter().GetResult();

            int baseStep = deal.Step;
            deal.PredictClearTool(0, 0);

            // 造分歧:服务端回一个与预测不同的盘面 + 候选。
            var divergent = new ClearToolResult(DealResultCode.Ok, deal.Step, deal.Score, 8,
                new List<int> { 255, 0, 0, 0, 0, 0, 0, 0 },
                new GenStateView(new List<int> { 7, 7, 7 }, 3, 1, 5, false, 0), newEnergy: 12);
            bool corrected = deal.ReconcileClearTool(divergent);

            Assert.IsTrue(corrected, "分歧响应应触发对账覆盖");
            Assert.IsTrue(deal.LastReconcileCorrected);
            Assert.AreEqual(255, deal.Board.RowBinary[0], "对账后盘面应取服务端权威");
            CollectionAssert.AreEqual(new List<int> { 7, 7, 7 }, deal.CandidateQueue, "对账后候选应取服务端权威");
        }

        [Test]
        public void ClearTool_Reconcile_NotEnoughEnergy_DoesNotOverwritePredictedState()
        {
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);
            deal.StartGameAsync().GetAwaiter().GetResult();

            int baseStep = deal.Step;
            deal.PredictClearTool(0, 0); // 乐观清 + Step +1
            int stepAfterPredict = deal.Step;

            // 服务端拒(体力不足):不应覆盖预测态(由宿主回滚乐观清)。
            var reject = ClearToolResult.Fail(DealResultCode.NotEnoughEnergy);
            bool corrected = deal.ReconcileClearTool(reject);

            Assert.IsFalse(corrected, "NotEnoughEnergy 不应触发覆盖");
            Assert.IsFalse(deal.LastReconcileCorrected);
            Assert.AreEqual(stepAfterPredict, deal.Step, "失败码不应改预测 step(宿主负责回滚)");
        }

        [Test]
        public void ClearTool_AsyncRejected_AfterGameOver_NoRpc()
        {
            var gateway = new SimGateway(Seed);
            var deal = new ServerDealSync(gateway);
            deal.StartGameAsync().GetAwaiter().GetResult();

            PickPlacement(deal.CandidateQueue, deal.Board, out int slot, out int x, out int y);
            deal.PredictPlace(slot, x, y);
            deal.ReconcilePlace(new PlaceResult(DealResultCode.Ok, deal.Step, deal.Score, 0, -1,
                BoardRows(deal.Board), new GenStateView(new List<int>(deal.CandidateQueue), 0, 0, 0, false, 0),
                gameOver: true, finalScore: 1, bestScore: 1L));
            Assert.IsTrue(deal.GameOver);

            var result = deal.ClearToolAsync(deal.Step, 0, 0).GetAwaiter().GetResult();
            Assert.AreEqual(DealResultCode.GameNotFound, result.Code, "终局后 ClearToolAsync 应短路回 GameNotFound");
        }

        /// <summary>从服务端 sim 取全态(经 GenStateView 中转,逐字段镜像)供断言对照。</summary>
        private static DynamicWeightDiff.FullState ServerGenFull(ServerSim sim)
        {
            var g = sim.Gen();
            return new DynamicWeightDiff.FullState(g.RngS0, g.RngS1, g.DynamicWeight, g.PreDynamicWeight,
                g.RefillIndex, g.BcInWindow, g.BcCooldown, g.LastAlgo, g.LastTierId);
        }

        private static void ClearBoard(BinaryBoard b)
        {
            for (int r = 0; r < BinaryBoard.RowCount; r++) b.RowBinary[r] = 0;
        }

        private static List<int> BoardRows(BinaryBoard b)
        {
            var rows = new List<int>(BinaryBoard.RowCount);
            for (int r = 0; r < BinaryBoard.RowCount; r++) rows.Add(b.RowBinary[r]);
            return rows;
        }
    }
}
