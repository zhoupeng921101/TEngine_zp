using System.Collections.Generic;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast.Algorithms
{
    /// <summary>
    /// 8 种算法实现 + 主分发器。每种返回 3 个 shapeId。
    /// FILL / DIFF / STRAIGHT_DEATH_DIFF 用 bit-aware 启发式（先识别棋盘模式定向选块），
    /// 其余继续走 Monte-Carlo 采样。
    /// </summary>
    public static class BlockAlgorithms
    {
        // ─── 采样次数（v1 内联硬编码；P2 接 Luban 后可外置）────────────
        private const int SamplesFill              = 80;
        private const int SamplesAdd3              = 60;
        private const int SamplesEasyDiff          = 80;
        private const int SamplesDiff              = 160;
        private const int SamplesStraightDeath     = 320;
        private const int SamplesClearAll          = 120;
        private const int SamplesAllCombination    = 150;

        /// <summary>从 39 个白名单 ID 中均匀随机抽一个。</summary>
        public static int UniformRandomShape()
            => BlockShapeMap.CommonShapeIds[RandomSource.Index(BlockShapeMap.CommonShapeIds.Count)];

        public static int[] SampleTrio()
            => new[] { UniformRandomShape(), UniformRandomShape(), UniformRandomShape() };

        // ─── 难块子池：cells>=5 或 max(w,h)>=4 ────────────────────────
        private static int[] _hardShapeIds;
        private static int[] HardShapeIds
        {
            get
            {
                if (_hardShapeIds == null)
                {
                    var list = new List<int>();
                    foreach (int id in BlockShapeMap.CommonShapeIds)
                    {
                        int cells = BlockShapeMap.GetCellCount(id);
                        var sh = BlockShapeMap.Get(id);
                        int maxDim = sh == null ? 0 : (sh.Width > sh.Height ? sh.Width : sh.Height);
                        if (cells >= 5 || maxDim >= 4) list.Add(id);
                    }
                    _hardShapeIds = list.ToArray();
                }
                return _hardShapeIds;
            }
        }

        private static int HardShape()
        {
            var pool = HardShapeIds;
            return pool[RandomSource.Index(pool.Length)];
        }

        /// <summary>难题倾向采样：70% 难块、30% 普通块。</summary>
        private static int[] SampleHardBiasedTrio()
        {
            int Pick() => RandomSource.NextDouble() < 0.7 ? HardShape() : UniformRandomShape();
            return new[] { Pick(), Pick(), Pick() };
        }

        /// <summary>公共后备：随机无死亡。</summary>
        public static int[] FallbackTrio(BinaryBoard board)
        {
            for (int i = 0; i < 50; i++)
            {
                var t = SampleTrio();
                if (board.CheckPutAllBlocks(t)) return t;
            }
            return new[] { 1, 1, 1 };
        }

        // ─── #0 FILL ────────────────────────────────────────────────
        public static int[] FillTrio(BinaryBoard board, int samplesCount = SamplesFill)
        {
            // 1) 找近完成行
            var rowCandidates = BoardAnalysis.RowsMissingRange(board, 1, 3);
            var colCandidates = new List<BoardAnalysis.ColGap>();
            colCandidates.AddRange(BoardAnalysis.ColsMissing(board, 1));
            colCandidates.AddRange(BoardAnalysis.ColsMissing(board, 2));
            colCandidates.AddRange(BoardAnalysis.ColsMissing(board, 3));

            var keyShapes = new List<int>();

            foreach (var rg in rowCandidates)
            {
                if (IsContiguousMask(rg.GapMask))
                {
                    int sid = BoardAnalysis.HorizontalLineShape(rg.Missing);
                    if (sid > 0) keyShapes.Add(sid);
                }
                else keyShapes.Add(1);
            }
            foreach (var cg in colCandidates)
            {
                if (IsContiguousMask(cg.GapMask))
                {
                    int m = BitCount(cg.GapMask);
                    int sid = BoardAnalysis.VerticalLineShape(m);
                    if (sid > 0) keyShapes.Add(sid);
                }
                else keyShapes.Add(1);
            }

            // 2) 用 key + 2 个其它块试 trio
            if (keyShapes.Count > 0)
            {
                int keyTries = samplesCount < 40 ? samplesCount : 40;
                int[] bestTrio = null;
                int bestCleared = 0;
                for (int i = 0; i < keyTries; i++)
                {
                    int[] t;
                    if (keyShapes.Count >= 3)
                    {
                        var shuffled = new List<int>(keyShapes);
                        RandomSource.Shuffle(shuffled);
                        t = new[] { shuffled[0], shuffled[1], shuffled[2] };
                    }
                    else if (keyShapes.Count == 2)
                    {
                        t = new[] { keyShapes[0], keyShapes[1], UniformRandomShape() };
                    }
                    else
                    {
                        t = new[] { keyShapes[0], UniformRandomShape(), UniformRandomShape() };
                    }
                    if (!board.CheckPutAllBlocks(t)) continue;
                    var result = BoardEvaluator.FindBest(board, t, (_rows, cleared) => cleared, 24);
                    if (result != null && result.Cleared > 0)
                    {
                        if (bestTrio == null || result.Cleared > bestCleared)
                        {
                            bestTrio = t;
                            bestCleared = result.Cleared;
                        }
                        if (bestCleared >= 24) return bestTrio;
                    }
                }
                if (bestTrio != null) return bestTrio;
            }

            // 3) 退回纯采样
            {
                int[] bestTrio = null;
                int bestCleared = 0;
                for (int i = 0; i < samplesCount; i++)
                {
                    var t = SampleTrio();
                    if (!board.CheckPutAllBlocks(t)) continue;
                    var result = BoardEvaluator.FindBest(board, t, (_rows, cleared) => cleared, 24);
                    if (result != null && result.Cleared > 0)
                    {
                        if (bestTrio == null || result.Cleared > bestCleared)
                        {
                            bestTrio = t;
                            bestCleared = result.Cleared;
                        }
                        if (bestCleared >= 16) break;
                    }
                }
                return bestTrio ?? FallbackTrio(board);
            }
        }

        private static bool IsContiguousMask(int mask)
        {
            if (mask == 0) return false;
            int firstSet = -1, lastSet = -1;
            for (int i = 0; i < 8; i++)
            {
                if (((mask >> i) & 1) != 0)
                {
                    if (firstSet < 0) firstSet = i;
                    lastSet = i;
                }
            }
            int expected = ((1 << (lastSet - firstSet + 1)) - 1) << firstSet;
            return mask == expected;
        }

        private static int BitCount(int x)
        {
            int n = 0;
            while (x != 0) { n += x & 1; x >>= 1; }
            return n;
        }

        // ─── #1 RANDOM_NO_DIE ───────────────────────────────────────
        public static int[] RandomNoDieTrio(BinaryBoard board) => FallbackTrio(board);

        // ─── #2 ADD3 熵增 ────────────────────────────────────────────
        public static int[] Add3Trio(BinaryBoard board, int samplesCount = SamplesAdd3)
        {
            int[] bestTrio = null;
            double bestEntropy = double.NegativeInfinity;
            for (int i = 0; i < samplesCount; i++)
            {
                var t = SampleTrio();
                var result = BoardEvaluator.FindBest(board, t, (rows, _) => BoardEvaluator.Entropy(rows), 16);
                if (result == null) continue;
                if (bestTrio == null || result.Score > bestEntropy)
                {
                    bestTrio = t;
                    bestEntropy = result.Score;
                }
            }
            return bestTrio ?? FallbackTrio(board);
        }

        // ─── #3 EASY_DIFF ──────────────────────────────────────────
        public static int[] EasyDiffTrio(BinaryBoard board, int samplesCount = SamplesEasyDiff)
        {
            const int targetMin = 5, targetMax = 30;
            int[] bestTrio = null;
            double bestDist = double.PositiveInfinity;
            for (int i = 0; i < samplesCount; i++)
            {
                var t = SampleTrio();
                int solCount = BoardEvaluator.CountSolutions(board, t, targetMax + 1);
                if (solCount < 1) continue;
                double idealMid = (targetMin + targetMax) / 2.0;
                double dist = System.Math.Abs(solCount - idealMid);
                if (bestTrio == null || dist < bestDist) { bestTrio = t; bestDist = dist; }
            }
            return bestTrio ?? FallbackTrio(board);
        }

        // ─── #4 DIFF 困难难题 ──────────────────────────────────────
        public static int[] HardDiffTrio(BinaryBoard board, int samplesCount = SamplesDiff)
        {
            var rect = BoardAnalysis.LargestEmptyRect(board);
            int[] bestTrio = null;
            int bestCount = int.MaxValue;
            const int countLimit = 12;

            // 1) 锚点
            if (rect.W >= 3 || rect.H >= 3)
            {
                var anchorCandidates = PickAnchorShapes(rect.W, rect.H);
                int anchorTries = (samplesCount / 2) < 60 ? (samplesCount / 2) : 60;
                for (int i = 0; i < anchorTries; i++)
                {
                    int anchor = anchorCandidates[i % anchorCandidates.Count];
                    var t = new[] { anchor, UniformRandomShape(), UniformRandomShape() };
                    int c = BoardEvaluator.CountSolutions(board, t, countLimit);
                    if (c < 1) continue;
                    if (bestTrio == null || c < bestCount) { bestTrio = t; bestCount = c; }
                    if (bestCount == 1) return bestTrio;
                }
            }

            // 2) hard-biased 采样
            for (int i = 0; i < samplesCount; i++)
            {
                var t = SampleHardBiasedTrio();
                int c = BoardEvaluator.CountSolutions(board, t, countLimit);
                if (c < 1) continue;
                if (bestTrio == null || c < bestCount) { bestTrio = t; bestCount = c; }
                if (bestCount == 1) break;
                if (bestCount <= 3 && i > samplesCount / 2) break;
            }
            return bestTrio ?? FallbackTrio(board);
        }

        // ─── #5 STRAIGHT_DEATH_DIFF ────────────────────────────────
        public static int[] StraightDeathTrio(BinaryBoard board, int samplesCount = SamplesStraightDeath)
        {
            var rect = BoardAnalysis.LargestEmptyRect(board);
            int[] bestTrio = null;
            int bestCount = int.MaxValue;
            const int countLimit = 8;

            // 1) 双锚点
            if (rect.W >= 3 && rect.H >= 3)
            {
                var anchorCandidates = PickAnchorShapes(rect.W, rect.H);
                int anchorTries = (samplesCount / 2) < 80 ? (samplesCount / 2) : 80;
                for (int i = 0; i < anchorTries; i++)
                {
                    int a1 = anchorCandidates[i % anchorCandidates.Count];
                    int a2 = anchorCandidates[(i + 1) % anchorCandidates.Count];
                    var t = new[] { a1, a2, UniformRandomShape() };
                    int c = BoardEvaluator.CountSolutions(board, t, countLimit);
                    if (c == 1) return t;
                    if (c < 1) continue;
                    if (bestTrio == null || c < bestCount) { bestTrio = t; bestCount = c; }
                }
            }

            // 2) hard-biased 采样
            for (int i = 0; i < samplesCount; i++)
            {
                var t = SampleHardBiasedTrio();
                int c = BoardEvaluator.CountSolutions(board, t, countLimit);
                if (c == 1) return t;
                if (c < 1) continue;
                if (bestTrio == null || c < bestCount) { bestTrio = t; bestCount = c; }
            }
            return bestTrio ?? FallbackTrio(board);
        }

        /// <summary>给定 w×h，返回能塞进去的、面积大的形状候选（按面积降序，取前 10）。</summary>
        private static List<int> PickAnchorShapes(int w, int h)
        {
            var fitting = BoardAnalysis.ShapesFittingRect(w, h);
            if (fitting.Count == 0) return new List<int> { UniformRandomShape() };
            fitting.Sort((a, b) => BlockShapeMap.GetCellCount(b) - BlockShapeMap.GetCellCount(a));
            if (fitting.Count > 10) fitting = fitting.GetRange(0, 10);
            return fitting;
        }

        // ─── #6 CLEAR_ALL 清盘 Plus ────────────────────────────────
        public static int[] ClearAllTrio(BinaryBoard board, int samplesCount = SamplesClearAll)
        {
            int filled = BoardEvaluator.FilledCount(board.RowBinary);
            if (filled < 10) return FallbackTrio(board);

            for (int i = 0; i < samplesCount; i++)
            {
                var t = SampleTrio();
                if (!board.CheckPutAllBlocks(t)) continue;
                if (BoardEvaluator.TrioCells(t) < filled) continue;
                var result = BoardEvaluator.FindBest(board, t,
                    (rows, _) =>
                    {
                        bool allZero = true;
                        for (int j = 0; j < rows.Length; j++) if (rows[j] != 0) { allZero = false; break; }
                        return allZero ? 10000.0 : -BoardEvaluator.FilledCount(rows);
                    }, 32);
                if (result != null && result.Score >= 10000) return t;
            }
            return FillTrio(board, 50); // 退而求其次：能消除就行
        }

        // ─── #7 ALL_COMBINATION ────────────────────────────────────
        public static int[] AllCombinationTrio(BinaryBoard board, int samplesCount = SamplesAllCombination)
        {
            int[] bestTrio = null;
            int bestCleared = 0;
            for (int i = 0; i < samplesCount; i++)
            {
                var t = SampleTrio();
                if (!board.CheckPutAllBlocks(t)) continue;
                var result = BoardEvaluator.FindBest(board, t, (_rows, cleared) => cleared, 48);
                if (result != null && result.Cleared > 0)
                {
                    if (bestTrio == null || result.Cleared > bestCleared)
                    {
                        bestTrio = t;
                        bestCleared = result.Cleared;
                    }
                    if (bestCleared >= 24) break;
                }
            }
            return bestTrio ?? FillTrio(board, 50);
        }

        // ─── BOARD_CLEAR_GREEDY 清屏窗口专用 ───────────────────────
        public static int[] BoardClearGreedyTrio(BinaryBoard board, int samplesCount = 240)
        {
            int filledNow = BoardEvaluator.FilledCount(board.RowBinary);
            if (filledNow == 0) return FallbackTrio(board);

            var keys = CollectClearKeys(board);
            var winningSet = new HashSet<int>();
            var allSet = new HashSet<int>();
            foreach (var k in keys)
            {
                allSet.Add(k.Id);
                if (k.Winning) winningSet.Add(k.Id);
            }
            var winning = new List<int>(winningSet);
            var allKeys = new List<int>(allSet);

            // Phase 1: 格子少时尝试一次性清盘
            if (filledNow <= 24)
            {
                int tries = samplesCount < 160 ? samplesCount : 160;
                for (int i = 0; i < tries; i++)
                {
                    int[] t;
                    double r = RandomSource.NextDouble();
                    if (winning.Count > 0 && r < 0.8) t = SampleWinningTrio(winning);
                    else t = SampleTrio();
                    if (!board.CheckPutAllBlocks(t)) continue;
                    if (BoardEvaluator.TrioCells(t) < filledNow) continue;
                    var rr = BoardEvaluator.FindBest(board, t,
                        (rows, _) =>
                        {
                            bool allZero = true;
                            for (int j = 0; j < rows.Length; j++) if (rows[j] != 0) { allZero = false; break; }
                            return allZero ? 10000.0 : -BoardEvaluator.FilledCount(rows);
                        }, 48);
                    if (rr != null && rr.Score >= 10000) return t;
                }
            }

            // Phase 2: 贪心
            int[] bestTrio = null;
            double bestScore = double.NegativeInfinity;
            for (int i = 0; i < samplesCount; i++)
            {
                int[] t;
                double r = RandomSource.NextDouble();
                if (winning.Count > 0 && r < 0.75) t = SampleWinningTrio(winning);
                else if (allKeys.Count > 0 && r < 0.9)
                {
                    int k = allKeys[RandomSource.Index(allKeys.Count)];
                    t = new[] { k, UniformRandomShape(), UniformRandomShape() };
                }
                else t = SampleTrio();
                if (!board.CheckPutAllBlocks(t)) continue;
                var result = BoardEvaluator.FindBest(board, t,
                    (rows, cleared) => cleared * 100.0 - BoardEvaluator.FilledCount(rows), 48);
                if (result == null) continue;
                if (bestTrio == null || result.Score > bestScore)
                {
                    bestTrio = t;
                    bestScore = result.Score;
                }
            }
            return bestTrio ?? FillTrio(board, 50);
        }

        private readonly struct ClearKey
        {
            public readonly int Id;
            public readonly bool Winning;
            public ClearKey(int id, bool winning) { Id = id; Winning = winning; }
        }

        /// <summary>
        /// 收集"清屏钥匙":
        ///   winning=true：放下这个线条能直接清空对应行/列
        ///   winning=false：能填进 gap 但不一定立刻清行/列
        /// </summary>
        private static List<ClearKey> CollectClearKeys(BinaryBoard board)
        {
            var result = new List<ClearKey>();
            // 行
            for (int r = 0; r < 8; r++)
            {
                int row = board.RowBinary[r];
                if (row == 0 || row == 255) continue;
                var gaps = RowContiguousGapLengths(row);
                bool allCoverable = AllGapsCoverable(gaps);
                foreach (int len in gaps)
                {
                    int id;
                    if (len == 1) id = 1;
                    else id = BoardAnalysis.HorizontalLineShape(len < 5 ? len : 5);
                    if (id > 0) result.Add(new ClearKey(id, allCoverable));
                }
            }
            // 列
            for (int c = 0; c < 8; c++)
            {
                var gaps = ColContiguousGapLengths(board, c);
                int sum = 0;
                for (int i = 0; i < gaps.Count; i++) sum += gaps[i];
                int filledInCol = 8 - sum;
                if (filledInCol == 0) continue;
                bool allCoverable = AllGapsCoverable(gaps);
                foreach (int len in gaps)
                {
                    int id;
                    if (len == 1) id = 1;
                    else id = BoardAnalysis.VerticalLineShape(len < 5 ? len : 5);
                    if (id > 0) result.Add(new ClearKey(id, allCoverable));
                }
            }
            return result;
        }

        private static bool AllGapsCoverable(List<int> gaps)
        {
            for (int i = 0; i < gaps.Count; i++)
            {
                int len = gaps[i];
                if (len < 1 || len > 5) return false;
            }
            return true;
        }

        private static List<int> RowContiguousGapLengths(int row)
        {
            var result = new List<int>();
            int i = 0;
            while (i < 8)
            {
                if (((row >> (8 - i - 1)) & 1) == 0)
                {
                    int j = i;
                    while (j < 8 && ((row >> (8 - j - 1)) & 1) == 0) j++;
                    result.Add(j - i);
                    i = j;
                }
                else i++;
            }
            return result;
        }

        private static List<int> ColContiguousGapLengths(BinaryBoard board, int c)
        {
            int mask = 1 << (8 - c - 1);
            var result = new List<int>();
            int i = 0;
            while (i < 8)
            {
                if ((board.RowBinary[i] & mask) == 0)
                {
                    int j = i;
                    while (j < 8 && (board.RowBinary[j] & mask) == 0) j++;
                    result.Add(j - i);
                    i = j;
                }
                else i++;
            }
            return result;
        }

        /// <summary>小补丁池：1×1 / 1×2 / 2×1 —— 在 winning key 旁减少冗余格数。</summary>
        private static readonly int[] SmallFillers = { 1, 2, 3 };
        private static int SmallFiller() => SmallFillers[RandomSource.Index(SmallFillers.Length)];

        /// <summary>
        /// k 的分布：50% k=1, 30% k=2, 20% k=3。
        /// winning 钥匙允许有放回采样（譬如棋盘 3 行各缺 3 格 → winning=[5]，trio 可以 [5,5,5]）。
        /// </summary>
        private static int[] SampleWinningTrio(IReadOnlyList<int> winning)
        {
            double r = RandomSource.NextDouble();
            int k = r < 0.5 ? 1 : r < 0.8 ? 2 : 3;
            var trio = new int[3];
            int idx = 0;
            for (int j = 0; j < k; j++)
            {
                trio[idx++] = winning[RandomSource.Index(winning.Count)];
            }
            while (idx < 3) trio[idx++] = SmallFiller();
            return trio;
        }

        // ─── 主分发器 ──────────────────────────────────────────────
        public static int[] GenerateTrio(AlgorithmKind algo, BinaryBoard board)
        {
            switch (algo)
            {
                case AlgorithmKind.Fill:              return FillTrio(board);
                case AlgorithmKind.RandomNoDie:       return RandomNoDieTrio(board);
                case AlgorithmKind.Add3:              return Add3Trio(board);
                case AlgorithmKind.EasyDiff:          return EasyDiffTrio(board);
                case AlgorithmKind.Diff:              return HardDiffTrio(board);
                case AlgorithmKind.StraightDeathDiff: return StraightDeathTrio(board);
                case AlgorithmKind.ClearAll:          return ClearAllTrio(board);
                case AlgorithmKind.AllCombination:    return AllCombinationTrio(board);
                default:                              return RandomNoDieTrio(board);
            }
        }
    }
}
