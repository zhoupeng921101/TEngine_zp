using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GameLogic.BlockBlast.Algorithms;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 「只测生成核心」的跨运行时确定性 harness(服务端权威发牌移植·确定性测量步)。
    ///
    /// 与 BlockGameState 全程解耦:直接驱动 BinaryBoard(落子/消除)+ BlockScoring(推分)
    /// + DynamicWeightDiff.OfferTrio(补牌)+ AddWeight(落子后权重反馈),不抽颜色
    /// (颜色不影响棋盘几何与生成逻辑,剥掉以隔离生成器并缩小 .NET 编译闭包)。
    ///
    /// 玩家策略纯棋盘判定、不消耗 RNG:逐槽 0..2 取首个有合法落点的块,落点取
    /// BinaryBoard.GetCanPutPoss 固定扫描序(x 外 y 内)第一个;三块皆无处可放(卡死)
    /// → 确定性清空全盘续局。因策略无 RNG,两端各自独立跑出同一对局,无需记录/回放输入。
    ///
    /// 输出 runtime-neutral 文本 trace:自包含头部(seed/rng/weightcfg/策略)+ 每手一行
    /// (step/slot/pos/lines/trio shapeIds+算法+tier/boardHash/完整调度状态向量)。
    /// 两端用同一 seed + 同一 rng 实现跑出的 trace 应逐字符一致。
    /// </summary>
    public static class GenCoreDeterminismHarness
    {
        /// <summary>
        /// 多 seed 扩测的代表性种子组。客户端 Unity 测试与 .NET 服务端实验链接同一份源、
        /// 跑同一组 seed,使跨端逐位 diff 覆盖多条独立 RNG 序列(把强证据提到很强证据)。
        /// 单一事实源:两端不各自硬编码种子表,均引用本数组。
        /// </summary>
        public static readonly int[] Seeds = { 1337, 1, 7, 42, 99, 12345 };

        /// <summary>每局脚本化对局步数(两端须一致)。</summary>
        public const int DefaultSteps = 240;

        /// <summary>随机源选择:供 trace 头标注,服务端须用同一实现回放。</summary>
        public enum RngKind
        {
            /// <summary>System.Random(.NET 与 Unity 实现不同,仅供量化 c3 跨运行时发散)。</summary>
            SystemRandom,
            /// <summary>可移植 xorshift128+(跨运行时确定,供量化 c1/c2)。</summary>
            Portable,
        }

        /// <summary>两 tier 配置:与既有 DynamicWeightDiffTests / 1a harness 同源,自包含写入 trace 头便于服务端复算。</summary>
        public static List<WeightConfigEntry> DefaultWeightConfig()
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

        /// <summary>
        /// 按 RngKind + seed 建一个全新逐局发牌调度器实例(无单例残留),复位到已知基线。
        /// 去单例化后两遍各自 new 独立实例,天然无跨遍残留;此处只需保证同 seed 同基线即可逐位对齐。
        /// </summary>
        private static DynamicWeightDiff NewScheduler(int seed, RngKind rngKind, IList<WeightConfigEntry> cfg)
        {
            // (a1) 本局随机源:按选择装入对应实现 + 固定 seed
            IRandomSource rng = rngKind == RngKind.Portable
                ? (IRandomSource)new XorShift128PlusRng(seed)
                : new SystemRandomSource(seed);

            // (a2) 逐局实例:持久化走内存(InMemory),避免污染本地 PlayerPrefs;Init 内部走 Load(此处空盘)。
            var dyn = new DynamicWeightDiff(rng, new InMemoryPersistenceProvider());
            dyn.ForceAlgorithm = null;
            dyn.Init(cfg);     // 内部:_weightConfig / _initialized / Load() / RegisterDefaults()
            dyn.Reset();       // _dynamicWeight=0 / _preDynamicWeight=0 / _refillIndex=0
            dyn.BeginGame();   // _refillIndex=0 / _bcInWindow=false / _bcCooldown=0
            dyn.LastAlgo = null;
            dyn.LastTierId = null;
            return dyn;
        }

        /// <summary>把 System.Random 适配成 IRandomSource(System.Random 模式,仅供量化 c3 跨运行时发散)。</summary>
        private sealed class SystemRandomSource : IRandomSource
        {
            private readonly System.Random _r;
            public SystemRandomSource(int seed) => _r = new System.Random(seed);
            public double NextDouble() => _r.NextDouble();
            public int Next(int minInclusive, int maxExclusive) => _r.Next(minInclusive, maxExclusive);
        }

        /// <summary>
        /// 跑一段脚本化对局,返回 runtime-neutral 文本 trace。
        /// </summary>
        public static string RunScriptedGame(int seed, int steps, RngKind rngKind)
        {
            var cfg = DefaultWeightConfig();
            var dyn = NewScheduler(seed, rngKind, cfg);

            var board = new BinaryBoard();

            int score = 0;
            int combo = 0;
            // 当前 3 槽(只存 shapeId;颜色被剥离)。-1 表示空槽。
            var trio = new int[] { -1, -1, -1 };
            // 与最近一次补牌的 trio 对应的算法/tier(写入 trace 行)。
            var trioAlgo = AlgorithmKind.RandomNoDie;
            int? trioTier = null;

            var sb = new StringBuilder(64 * 1024);
            AppendHeader(sb, seed, steps, rngKind, cfg);

            // 首发:走真实发牌路径(空盘 + score 0),与之后补牌同口径,使整序生成器驱动。
            RefillTrio(board, score, dyn, trio, ref trioAlgo, ref trioTier);

            for (int step = 0; step < steps; step++)
            {
                // 卡死(三块皆无处可放)→ 确定性清空全盘续局
                bool forcedClear = false;
                if (!AnySlotPlaceable(trio, board))
                {
                    ClearWholeBoard(board);
                    forcedClear = true;
                }

                // 玩家确定性选择:首个有合法落点的槽,落点取 GetCanPutPoss 固定序首个
                int slot = -1; Vec2Int pos = default;
                for (int i = 0; i < 3; i++)
                {
                    if (trio[i] <= 0) continue;
                    var poss = board.GetCanPutPoss(trio[i]);
                    if (poss.Count > 0) { slot = i; pos = poss[0]; break; }
                }

                if (slot < 0)
                {
                    // 清盘后仍无处可放(理论极端:单块大于空盘)——记录并跳出
                    sb.Append("step=").Append(step).Append(" EVENT=stuck_after_clear\n");
                    break;
                }

                int placedCells = BlockShapeMap.GetCellCount(trio[slot]);

                // 落子:更新棋盘
                board.PutBlock(trio[slot], pos);
                trio[slot] = -1; // 消耗该槽

                // 消除 + 计分(BlockScoring = 计分单一信息源)
                int placeScore = BlockScoring.PlacementScore(placedCells);
                var clear = board.CanClearRowCols(true);
                int lines = clear.Rows.Count + clear.Cols.Count;
                int clearedCells = CountClearedCells(clear, board);
                int clearScore = lines > 0 ? BlockScoring.ClearScore(clearedCells, lines) : 0;
                score += placeScore + clearScore;
                combo = lines > 0 ? combo + 1 : 0;

                // 落子后权重反馈:用本手 trio 对应算法推进 dynamicWeight
                dyn.AddWeight(trioAlgo);

                // 补牌(三槽皆空才补):走真实发牌路径
                bool refilled = false;
                if (AllSlotsEmpty(trio))
                {
                    RefillTrio(board, score, dyn, trio, ref trioAlgo, ref trioTier);
                    refilled = true;
                }

                AppendStepLine(sb, step, slot, pos, lines, clearedCells, forcedClear, refilled,
                    trio, score, combo, dyn, board);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 多 seed 入口·单 seed:跑一局 Portable(生产路径)对局,返回该 seed 的 trace。
        /// 客户端 Unity 测试与 .NET Program 均调用此口,确保两端对同一 seed 跑出可逐位 diff 的 trace。
        /// </summary>
        public static string RunSeed(int seed)
            => RunScriptedGame(seed, DefaultSteps, RngKind.Portable);

        /// <summary>
        /// 多 seed 入口·全组:对 <see cref="Seeds"/> 逐 seed 跑 Portable 对局,返回 seed→trace。
        /// 两端据此对齐全组结果。
        /// </summary>
        public static Dictionary<int, string> RunAllSeeds()
        {
            var result = new Dictionary<int, string>(Seeds.Length);
            foreach (int seed in Seeds)
                result[seed] = RunSeed(seed);
            return result;
        }

        /// <summary>调用真实发牌路径补满 3 槽,记录本手算法/tier。</summary>
        private static void RefillTrio(
            BinaryBoard board, int score, DynamicWeightDiff dyn,
            int[] trio, ref AlgorithmKind trioAlgo, ref int? trioTier)
        {
            var offer = dyn.OfferTrio(board, score);
            for (int i = 0; i < 3; i++) trio[i] = offer.Ids[i];
            trioAlgo = offer.Algo;
            trioTier = offer.TierId;
        }

        private static void AppendHeader(
            StringBuilder sb, int seed, int steps, RngKind rngKind, IList<WeightConfigEntry> cfg)
        {
            sb.Append("# block_blast gen-core determinism trace v1\n");
            sb.Append("# runtime-neutral plain text. generation-core only (no BlockGameState, no colors).\n");
            sb.Append("rng=").Append(rngKind == RngKind.Portable ? "XorShift128Plus" : "System.Random").Append('\n');
            sb.Append("seed=").Append(seed).Append('\n');
            sb.Append("steps=").Append(steps).Append('\n');
            sb.Append("activation_score=").Append(GameConfigBB.ActivationScore).Append('\n');
            sb.Append("board_clear_score_threshold=15000\n");
            sb.Append("early_game_block_score_threshold=").Append(BlockShapeMap.EarlyGameBlockScoreThreshold).Append('\n');
            sb.Append("player_strategy=first_placeable_slot;first_pos_in_GetCanPutPoss_order\n");
            sb.Append("stuck_recovery=clear_whole_board\n");
            sb.Append("first_trio_source=OfferTrio_on_empty_board_score0\n");
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
            bool forcedClear, bool refilled, int[] trio, int score, int combo,
            DynamicWeightDiff dyn, BinaryBoard board)
        {
            sb.Append("step=").Append(step);
            sb.Append(" slot=").Append(slot);
            sb.Append(" pos=").Append(pos.X).Append(',').Append(pos.Y);
            sb.Append(" lines=").Append(lines);
            sb.Append(" clearedCells=").Append(clearedCells);
            sb.Append(" forcedClear=").Append(forcedClear ? 1 : 0);
            sb.Append(" refilled=").Append(refilled ? 1 : 0);

            // 当前 3 槽快照(补牌后):只记 shapeId(颜色已剥离),空槽记 null。
            sb.Append(" trio=");
            for (int i = 0; i < 3; i++)
            {
                if (i > 0) sb.Append(';');
                if (trio[i] <= 0) sb.Append("null"); else sb.Append(trio[i]);
            }
            // 本手发牌算法/tier(补牌后 = 新一手的;未补牌 = 上一手沿用值)。
            sb.Append(" algo=").Append((int)dyn.LastAlgo.GetValueOrDefault(AlgorithmKind.RandomNoDie));
            sb.Append(" tier=").Append(dyn.LastTierId.HasValue ? dyn.LastTierId.Value.ToString(CultureInfo.InvariantCulture) : "-");

            sb.Append(" score=").Append(score);
            sb.Append(" combo=").Append(combo);
            sb.Append(" boardHash=").Append(BoardHash(board));

            // 完整生成器跨手累积态向量
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

        // ─── 玩家策略辅助(纯确定性,不消耗发牌 RNG)───────────────

        private static bool AnySlotPlaceable(int[] trio, BinaryBoard board)
        {
            for (int i = 0; i < 3; i++)
                if (trio[i] > 0 && board.CanPut(trio[i])) return true;
            return false;
        }

        private static bool AllSlotsEmpty(int[] trio)
        {
            for (int i = 0; i < 3; i++) if (trio[i] > 0) return false;
            return true;
        }

        /// <summary>确定性清空全盘(卡死续局),不消耗 RNG。</summary>
        private static void ClearWholeBoard(BinaryBoard board)
        {
            for (int r = 0; r < BinaryBoard.RowCount; r++) board.RowBinary[r] = 0;
        }

        /// <summary>被清行列覆盖的格数(行列交叉只计一次):用消除前的占用快照逐格统计。</summary>
        private static int CountClearedCells(ClearResult clear, BinaryBoard boardAfter)
        {
            // CanClearRowCols(true) 已就地清除,故无法从 boardAfter 复原被清格;
            // 满行 8 格、满列 8 格,交叉点 = rows*cols,去重后即被清格数。
            int rows = clear.Rows.Count;
            int cols = clear.Cols.Count;
            return rows * BinaryBoard.ColCount + cols * BinaryBoard.RowCount - rows * cols;
        }

        /// <summary>棋盘 8 行位掩码拼成稳定 16 进制哈希(运行时中立)。</summary>
        private static string BoardHash(BinaryBoard board)
        {
            var sb = new StringBuilder(16);
            for (int r = 0; r < BinaryBoard.RowCount; r++)
                sb.Append(board.RowBinary[r].ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
