using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Algorithms;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 方块生成器「确定性测量 harness」(服务端权威化第 1a 步:只测量,不移植)。
    ///
    /// 通过真实发牌路径 <see cref="BlockGameState.RefillPieces"/> 驱动一段固定脚本化对局,
    /// 把每手 trio(shapeId/颜色/算法/tier)+ 棋盘哈希 + 分数 + 生成器状态向量记成 golden trace。
    /// 两遍同 seed 同脚本须逐字符一致(同运行时确定性),并把 trace 落盘成运行时中立产物
    /// (纯文本,非 UnityEngine.JsonUtility),供后续 .NET 服务端 runner 回放比对。
    ///
    /// 玩家策略确定性:逐槽 0..2 取首个有合法落点的块,落点取 BinaryBoard.GetCanPutPoss
    /// 固定扫描序(x 外 y 内)的第一个;三块皆无处可放(卡死)→ 确定性清空全盘续局。
    /// 计分用 BlockScoring(计分单一信息源)推进,使分数过 tier 阈值,纳入分数驱动的调度分支。
    /// </summary>
    [TestFixture]
    public class GeneratorDeterminismHarnessTests
    {
        private const int Seed = 1337;
        private const int Steps = 240;

        private InMemoryPersistenceProvider _provider;

        [SetUp]
        public void SetUp()
        {
            _provider = new InMemoryPersistenceProvider();
            Persistence.Provider = _provider;
            if (BlockGameState.IsValid) BlockGameState.Instance.Release();
        }

        [TearDown]
        public void TearDown()
        {
            if (BlockGameState.IsValid) BlockGameState.Instance.Release();
        }

        /// <summary>把 System.Random 适配成 IRandomSource(本 harness 量 System.Random 路径)。</summary>
        private sealed class SystemRandomSource : IRandomSource
        {
            private readonly System.Random _r;
            public SystemRandomSource(int seed) => _r = new System.Random(seed);
            public double NextDouble() => _r.NextDouble();
            public int Next(int minInclusive, int maxExclusive) => _r.Next(minInclusive, maxExclusive);
        }

        /// <summary>
        /// 复位所有「跨手影响发牌」的态到已知基线。去单例化后发牌调度器是 BlockGameState 的逐局实例:
        /// 用固定 seed new 一个独立随机源的调度器注入,使经 RefillPieces 的客户端路径两遍可复现。
        /// 颜色仍走静态 RandomSource(本 harness trace 含颜色),故同样固定其 seed(与生成器各自独立流,
        /// 两遍各自重置即逐位一致)。
        /// </summary>
        private static void ResetGenerationStateToBaseline(int seed, IList<WeightConfigEntry> cfg)
        {
            // (a1) 颜色用的静态随机源:固定 seed(与发牌生成器为各自独立的确定性流)
            RandomSource.SetSeed(seed);

            // (a5) BlockGameState 单例:整例丢弃重建到 OnInit 缺省(空盘 / 空槽 / 分数 0 / Classic 模式)
            if (BlockGameState.IsValid) BlockGameState.Instance.Release();
            var s = BlockGameState.Instance;
            s.MergeOrderMode = false;            // 走 Classic 纯生成路径(不触 merge-order 元素分摊 RNG)

            // (a2) 逐局发牌调度器:固定 seed 的 System.Random 源 + 内存持久化,注入本局,避免上一用例残留
            var dyn = new DynamicWeightDiff(new SystemRandomSource(seed), new InMemoryPersistenceProvider());
            dyn.ForceAlgorithm = null;           // (a3) HUD 调试强制算法:确保关闭
            dyn.Init(cfg);                       // 内部:_weightConfig / _initialized / Load() / RegisterDefaults()
            dyn.Reset();                         // _dynamicWeight=0 / _preDynamicWeight=0 / _refillIndex=0
            dyn.BeginGame();                     // _refillIndex=0 / _bcInWindow=false / _bcCooldown=0
            dyn.LastAlgo = null;
            dyn.LastTierId = null;
            s.InjectDynamic(dyn);

            // (a4) OfferRegistry 静态单例:Init 内 RegisterDefaults 已 Clear+重注册,无需额外处理
        }

        /// <summary>两 tier 配置:与既有 DynamicWeightDiffTests 同源,自包含写入 trace 头便于服务端复算。</summary>
        private static List<WeightConfigEntry> HarnessWeightConfig()
        {
            return new List<WeightConfigEntry>
            {
                new WeightConfigEntry
                {
                    Id = 1,
                    FillBlankOdds = 80, RandomOdds = 20,
                    EntropyOdds = 0, EasyOdds = 0, HardOdds = 0,
                    IntuitionOdds = 0, Clearboard = 0, Allunite = 0,
                    HighScoreMin = 0, HighScoreMax = -1,
                    FactorLow = -100, FactorHigh = 0,
                },
                new WeightConfigEntry
                {
                    Id = 2,
                    FillBlankOdds = 0, RandomOdds = 10,
                    EntropyOdds = 10, EasyOdds = 10, HardOdds = 60,
                    IntuitionOdds = 10, Clearboard = 0, Allunite = 0,
                    HighScoreMin = 0, HighScoreMax = -1,
                    FactorLow = 0, FactorHigh = 200,
                },
            };
        }

        [Test]
        public void GoldenTrace_SameRuntime_IsReproducible()
        {
            string traceA = RunScriptedGame(Seed, Steps);
            string traceB = RunScriptedGame(Seed, Steps);

            if (!string.Equals(traceA, traceB, StringComparison.Ordinal))
            {
                int line = FirstDivergenceLine(traceA, traceB);
                Assert.Fail($"同运行时两遍 trace 不一致,首个发散在第 {line} 行。" +
                            "说明发牌路径存在未被基线复位的全局态或非确定性来源。");
            }
            Assert.Pass();
        }

        [Test]
        public void GoldenTrace_WrittenToFixtures()
        {
            string trace = RunScriptedGame(Seed, Steps);

            string fixturesDir = Path.Combine(
                UnityEngine.Application.dataPath, "Editor", "Tests", "BlockBlast", "Fixtures");
            Directory.CreateDirectory(fixturesDir);
            string path = Path.Combine(fixturesDir, "generator_golden_trace.txt");
            File.WriteAllText(path, trace, new UTF8Encoding(false));

            Assert.IsTrue(File.Exists(path), $"golden trace 应写到 {path}");
            Assert.Greater(new FileInfo(path).Length, 0, "golden trace 不应为空");
        }

        // ─────────────────────────────────────────────────────────────
        // 脚本化对局 + trace 生成
        // ─────────────────────────────────────────────────────────────

        private string RunScriptedGame(int seed, int steps)
        {
            var cfg = HarnessWeightConfig();
            ResetGenerationStateToBaseline(seed, cfg);

            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            var dyn = s.Dynamic;

            var sb = new StringBuilder(64 * 1024);
            AppendHeader(sb, seed, steps, cfg);

            // 首发:固定首手(不消耗调度,颜色随机消耗 RNG),与生产 SetFirstHand 同口径
            s.SetFirstHand();

            for (int step = 0; step < steps; step++)
            {
                // 三块皆无处可放(卡死)→ 确定性清空全盘续局,记录事件
                bool forcedClear = false;
                if (!AnySlotPlaceable(s, board))
                {
                    ClearWholeBoard(s, board);
                    forcedClear = true;
                }

                // 玩家确定性选择:首个有合法落点的槽,落点取 GetCanPutPoss 固定序首个
                int slot = -1; Vec2Int pos = default;
                for (int i = 0; i < 3; i++)
                {
                    var piece = s.OperaArr[i];
                    if (piece == null) continue;
                    var poss = board.GetCanPutPoss(piece.ShapeId);
                    if (poss.Count > 0) { slot = i; pos = poss[0]; break; }
                }

                if (slot < 0)
                {
                    // 清盘后仍无处可放(理论极端:单块大于空盘)——记录并跳出,不污染后续确定性
                    sb.Append("step=").Append(step).Append(" EVENT=stuck_after_clear\n");
                    break;
                }

                int placedCells = BlockShapeMap.GetCellCount(s.OperaArr[slot].ShapeId);

                // 落子(更新棋盘 + SaveArr + 触发 AddWeight 权重反馈)
                s.PlacePiece(slot, board, pos.X, pos.Y);

                // 消除 + 计分(BlockScoring = 计分单一信息源)
                int placeScore = BlockScoring.PlacementScore(placedCells);
                var clear = board.CanClearRowCols(true);
                int lines = clear.Rows.Count + clear.Cols.Count;
                int clearedCells = ApplyClearToSaveArr(s, clear.Rows, clear.Cols);
                int clearScore = lines > 0 ? BlockScoring.ClearScore(clearedCells, lines) : 0;
                s.AddScore(placeScore + clearScore);
                s.Combo = lines > 0 ? s.Combo + 1 : 0;

                // 补牌(全空才补):走真实发牌路径
                bool refilled = false;
                if (AllSlotsEmpty(s))
                {
                    s.RefillPieces(board);
                    refilled = true;
                }

                // 记一条 trace 行:落子后的完整生成器状态向量
                AppendStepLine(sb, step, slot, pos, lines, clearedCells, forcedClear, refilled, s, dyn, board);
            }

            return sb.ToString();
        }

        private static void AppendHeader(StringBuilder sb, int seed, int steps, IList<WeightConfigEntry> cfg)
        {
            sb.Append("# block_blast generator golden trace v1\n");
            sb.Append("# runtime-neutral plain text. fields are stable; consumed by .NET server replay runner.\n");
            sb.Append("rng=System.Random\n");          // 服务端须用同一 RNG 实现回放
            sb.Append("seed=").Append(seed).Append('\n');
            sb.Append("steps=").Append(steps).Append('\n');
            sb.Append("activation_score=").Append(GameConfigBB.ActivationScore).Append('\n');
            sb.Append("board_clear_score_threshold=15000\n");
            sb.Append("early_game_block_score_threshold=").Append(BlockShapeMap.EarlyGameBlockScoreThreshold).Append('\n');
            sb.Append("first_hand=");
            var fh = GameConfigBB.FirstHand;
            for (int i = 0; i < fh.Length; i++) { if (i > 0) sb.Append(','); sb.Append(fh[i]); }
            sb.Append('\n');
            sb.Append("player_strategy=first_placeable_slot;first_pos_in_GetCanPutPoss_order\n");
            sb.Append("stuck_recovery=clear_whole_board\n");
            // weightcfg 全量,自包含可重放
            sb.Append("weightcfg_count=").Append(cfg.Count).Append('\n');
            foreach (var t in cfg)
            {
                sb.Append("weightcfg id=").Append(t.Id)
                  .Append(" odds=").Append(t.FillBlankOdds).Append(',').Append(t.RandomOdds).Append(',')
                  .Append(t.EntropyOdds).Append(',').Append(t.EasyOdds).Append(',').Append(t.HardOdds).Append(',')
                  .Append(t.IntuitionOdds).Append(',').Append(t.Clearboard).Append(',').Append(t.Allunite)
                  .Append(" hs=").Append(t.HighScoreMin).Append(',').Append(t.HighScoreMax)
                  .Append(" factor=").Append(t.FactorLow).Append(',').Append(t.FactorHigh)
                  .Append('\n');
            }
            sb.Append("# --- steps ---\n");
        }

        private static void AppendStepLine(
            StringBuilder sb, int step, int slot, Vec2Int pos, int lines, int clearedCells,
            bool forcedClear, bool refilled, BlockGameState s, DynamicWeightDiff dyn, BinaryBoard board)
        {
            sb.Append("step=").Append(step);
            sb.Append(" slot=").Append(slot);
            sb.Append(" pos=").Append(pos.X).Append(',').Append(pos.Y);
            sb.Append(" lines=").Append(lines);
            sb.Append(" clearedCells=").Append(clearedCells);
            sb.Append(" forcedClear=").Append(forcedClear ? 1 : 0);
            sb.Append(" refilled=").Append(refilled ? 1 : 0);

            // 本手 trio(补牌后的当前 3 槽快照):shapeId / 颜色 / 算法标签
            sb.Append(" trio=");
            for (int i = 0; i < 3; i++)
            {
                if (i > 0) sb.Append(';');
                var p = s.OperaArr[i];
                if (p == null) { sb.Append("null"); continue; }
                sb.Append(p.ShapeId).Append('/').Append((int)p.Color).Append('/')
                  .Append(p.HasAlgo ? ((int)p.Algo).ToString(CultureInfo.InvariantCulture) : "-");
            }

            sb.Append(" score=").Append(s.Score);
            sb.Append(" combo=").Append(s.Combo);
            sb.Append(" boardHash=").Append(BoardHash(board));

            // 完整生成器状态向量(枚举到的全部跨手累积态)
            sb.Append(" dynamicWeight=").Append(dyn.DynamicWeight);
            sb.Append(" preDynamicWeight=").Append(dyn.InternalPreDynamicWeight);
            sb.Append(" refillIndex=").Append(dyn.InternalRefillIndex);
            var bc = dyn.GetBoardClearState();
            sb.Append(" bcInWindow=").Append(bc.inWindow ? 1 : 0);
            sb.Append(" bcCooldown=").Append(bc.cooldown);
            sb.Append(" lastAlgo=").Append(dyn.LastAlgo.HasValue ? ((int)dyn.LastAlgo.Value).ToString(CultureInfo.InvariantCulture) : "-");
            sb.Append(" lastTier=").Append(dyn.LastTierId.HasValue ? dyn.LastTierId.Value.ToString(CultureInfo.InvariantCulture) : "-");
            sb.Append('\n');
        }

        // ─────────────────────────────────────────────────────────────
        // 玩家策略辅助(纯确定性,不消耗发牌 RNG)
        // ─────────────────────────────────────────────────────────────

        private static bool AnySlotPlaceable(BlockGameState s, BinaryBoard board)
        {
            for (int i = 0; i < 3; i++)
            {
                var p = s.OperaArr[i];
                if (p != null && board.CanPut(p.ShapeId)) return true;
            }
            return false;
        }

        private static bool AllSlotsEmpty(BlockGameState s)
        {
            for (int i = 0; i < 3; i++) if (s.OperaArr[i] != null) return false;
            return true;
        }

        /// <summary>确定性清空全盘(卡死续局):同步清 SaveArr 与 BinaryBoard,不消耗 RNG。</summary>
        private static void ClearWholeBoard(BlockGameState s, BinaryBoard board)
        {
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    s.SaveArr[r][c] = -1;
            board.ConvertFromArr(s.SaveArr);
        }

        /// <summary>把被清行列在 SaveArr 上清空,返回被清格数(行列交叉只计一次)。</summary>
        private static int ApplyClearToSaveArr(BlockGameState s, IList<int> rows, IList<int> cols)
        {
            return s.ClearRowsAndCols(rows, cols);
        }

        /// <summary>棋盘 8 行位掩码拼成稳定 16 进制哈希字符串(运行时中立)。</summary>
        private static string BoardHash(BinaryBoard board)
        {
            var sb = new StringBuilder(16);
            for (int r = 0; r < BinaryBoard.RowCount; r++)
                sb.Append(board.RowBinary[r].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        private static int FirstDivergenceLine(string a, string b)
        {
            var la = a.Split('\n');
            var lb = b.Split('\n');
            int n = Math.Min(la.Length, lb.Length);
            for (int i = 0; i < n; i++)
                if (!string.Equals(la[i], lb[i], StringComparison.Ordinal)) return i + 1;
            return n + 1;
        }
    }
}
