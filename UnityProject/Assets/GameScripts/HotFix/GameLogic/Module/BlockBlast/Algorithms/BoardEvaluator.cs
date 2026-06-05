using System;
using System.Collections.Generic;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast.Algorithms
{
    /// <summary>(shapeId, pos) 放置项。</summary>
    public readonly struct Placement
    {
        public readonly int Id;
        public readonly Vec2Int Pos;
        public Placement(int id, Vec2Int pos) { Id = id; Pos = pos; }
    }

    /// <summary>findBest 的结果。</summary>
    public sealed class BestResult
    {
        public List<Placement> Sequence;
        public double Score;
        public int Cleared;
    }

    /// <summary>
    /// 棋盘评估器：被算法用来评分候选 trio。
    /// </summary>
    public static class BoardEvaluator
    {
        /// <summary>拷贝 RowBinary。</summary>
        public static int[] CloneRows(BinaryBoard board)
        {
            var r = new int[BinaryBoard.RowCount];
            for (int i = 0; i < BinaryBoard.RowCount; i++) r[i] = board.RowBinary[i];
            return r;
        }

        public static void RestoreRows(BinaryBoard board, int[] rows)
        {
            for (int i = 0; i < BinaryBoard.RowCount; i++) board.RowBinary[i] = rows[i];
        }

        /// <summary>
        /// 枚举所有解（trio 的合法放置序列），最多 limit 个就提前返回。
        /// 返回每个解的 Placement[] 路径。
        /// </summary>
        public static List<List<Placement>> EnumerateSolutions(BinaryBoard board, int[] trio, int limit = 64)
        {
            var validList = new List<int>(trio.Length);
            for (int i = 0; i < trio.Length; i++) if (trio[i] > 0) validList.Add(trio[i]);

            var solutions = new List<List<Placement>>();
            if (validList.Count == 0) { solutions.Add(new List<Placement>()); return solutions; }

            var path = new List<Placement>(validList.Count);
            Dfs(board, validList, path, solutions, limit);
            return solutions;
        }

        private static bool Dfs(BinaryBoard board, List<int> remaining, List<Placement> path, List<List<Placement>> solutions, int limit)
        {
            if (solutions.Count >= limit) return true;
            if (remaining.Count == 0)
            {
                solutions.Add(new List<Placement>(path));
                return solutions.Count >= limit;
            }
            for (int i = 0; i < remaining.Count; i++)
            {
                int id = remaining[i];
                var positions = board.GetCanPutPoss(id);
                for (int p = 0; p < positions.Count; p++)
                {
                    var pos = positions[p];
                    var backup = CloneRows(board);
                    board.PutBlock(id, pos);
                    path.Add(new Placement(id, pos));
                    var rest = new List<int>(remaining.Count - 1);
                    for (int j = 0; j < remaining.Count; j++) if (j != i) rest.Add(remaining[j]);
                    bool done = Dfs(board, rest, path, solutions, limit);
                    path.RemoveAt(path.Count - 1);
                    RestoreRows(board, backup);
                    if (done) return true;
                }
            }
            return false;
        }

        /// <summary>仅统计解数量，limit 控上限。</summary>
        public static int CountSolutions(BinaryBoard board, int[] trio, int limit = 64)
            => EnumerateSolutions(board, trio, limit).Count;

        /// <summary>
        /// 模拟一种放置序列，返回该序列消除的格子数与剩余 RowBinary 快照。
        /// 期间会消除满行/满列（与游戏内一致）。
        /// </summary>
        public static (int cleared, int[] finalRows, bool clearedAll) Simulate(BinaryBoard board, List<Placement> sequence)
        {
            var backup = CloneRows(board);
            int cleared = 0;
            for (int i = 0; i < sequence.Count; i++)
            {
                board.PutBlock(sequence[i].Id, sequence[i].Pos);
                var r = board.CanClearRowCols(true);
                // 与源一致：rows × 8 + cols × (8 - rows.length)，避免交叉点重计
                cleared += r.Rows.Count * 8 + r.Cols.Count * (8 - r.Rows.Count);
            }
            var finalRows = CloneRows(board);
            bool clearedAll = true;
            for (int i = 0; i < finalRows.Length; i++) if (finalRows[i] != 0) { clearedAll = false; break; }
            RestoreRows(board, backup);
            return (cleared, finalRows, clearedAll);
        }

        /// <summary>
        /// 找到使 scoreFn 最大的放置序列。
        /// scoreFn(finalRows, cleared) → 自定义打分。
        /// </summary>
        public static BestResult FindBest(
            BinaryBoard board,
            int[] trio,
            Func<int[], int, double> scoreFn,
            int solutionLimit = 32)
        {
            var sols = EnumerateSolutions(board, trio, solutionLimit);
            if (sols.Count == 0) return null;
            BestResult best = null;
            for (int i = 0; i < sols.Count; i++)
            {
                var seq = sols[i];
                var sim = Simulate(board, seq);
                double sc = scoreFn(sim.finalRows, sim.cleared);
                if (best == null || sc > best.Score)
                {
                    best = new BestResult { Sequence = seq, Score = sc, Cleared = sim.cleared };
                }
            }
            return best;
        }

        /// <summary>
        /// 熵：相邻格子状态不同的边数。0=全空/全满（最有序），更大=越乱。
        /// </summary>
        public static int Entropy(int[] rows)
        {
            const int ROWS = 8, COLS = 8;
            int e = 0;
            // 水平相邻
            for (int r = 0; r < ROWS; r++)
            {
                for (int c = 0; c < COLS - 1; c++)
                {
                    int a = (rows[r] >> (COLS - c - 1)) & 1;
                    int b = (rows[r] >> (COLS - c - 2)) & 1;
                    if (a != b) e++;
                }
            }
            // 垂直相邻
            for (int c = 0; c < COLS; c++)
            {
                int mask = 1 << (COLS - c - 1);
                for (int r = 0; r < ROWS - 1; r++)
                {
                    int a = (rows[r] & mask) != 0 ? 1 : 0;
                    int b = (rows[r + 1] & mask) != 0 ? 1 : 0;
                    if (a != b) e++;
                }
            }
            return e;
        }

        /// <summary>已占格子数。</summary>
        public static int FilledCount(int[] rows)
        {
            int n = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                int x = rows[i];
                while (x != 0) { n += x & 1; x >>= 1; }
            }
            return n;
        }

        /// <summary>trio 总格子数。</summary>
        public static int TrioCells(int[] trio)
        {
            int n = 0;
            for (int i = 0; i < trio.Length; i++) n += BlockShapeMap.GetCellCount(trio[i]);
            return n;
        }

        /// <summary>trio 的最大单块尺寸（max(width, height)），用于偏好难放的大块。</summary>
        public static int TrioMaxDim(int[] trio)
        {
            int m = 0;
            for (int i = 0; i < trio.Length; i++)
            {
                var s = BlockShapeMap.Get(trio[i]);
                if (s != null)
                {
                    if (s.Width > m) m = s.Width;
                    if (s.Height > m) m = s.Height;
                }
            }
            return m;
        }
    }
}
