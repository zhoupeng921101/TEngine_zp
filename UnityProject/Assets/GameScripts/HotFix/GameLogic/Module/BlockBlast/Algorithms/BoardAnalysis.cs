using System.Collections.Generic;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast.Algorithms
{
    /// <summary>
    /// 棋盘位运算分析工具。所有以 row/col 为单位的接口都用 8-bit 掩码（MSB=最左列），
    /// 与 BinaryBoard.RowBinary 一致；位=1 表示已占用。
    /// </summary>
    public static class BoardAnalysis
    {
        private const int N = BinaryBoard.ColCount;       // 8
        private const int Full = BinaryBoard.FullRow;     // 255

        /// <summary>缺口信息：r/c 索引、gapMask（取反&amp;FullRow）、missing 缺多少格。</summary>
        public readonly struct RowGap
        {
            public readonly int R;
            public readonly int GapMask;
            public readonly int Missing;
            public RowGap(int r, int mask, int missing) { R = r; GapMask = mask; Missing = missing; }
        }

        public readonly struct ColGap
        {
            public readonly int C;
            public readonly int GapMask;
            public ColGap(int c, int mask) { C = c; GapMask = mask; }
        }

        public readonly struct Rect
        {
            public readonly int W, H, R, C;
            public Rect(int w, int h, int r, int c) { W = w; H = h; R = r; C = c; }
        }

        private static int Popcount(int x)
        {
            int n = 0;
            while (x != 0) { n += x & 1; x >>= 1; }
            return n;
        }

        /// <summary>找出还缺恰好 missing 格才能消除的行。</summary>
        public static List<RowGap> RowsMissing(BinaryBoard board, int missing)
        {
            var result = new List<RowGap>();
            for (int r = 0; r < N; r++)
            {
                int filled = board.RowBinary[r];
                if (filled == Full || filled == 0) continue;
                int gapMask = (~filled) & Full;
                if (Popcount(gapMask) == missing) result.Add(new RowGap(r, gapMask, missing));
            }
            return result;
        }

        /// <summary>同 RowsMissing，但 missing 在 [min,max] 范围内。</summary>
        public static List<RowGap> RowsMissingRange(BinaryBoard board, int min, int max)
        {
            var result = new List<RowGap>();
            for (int r = 0; r < N; r++)
            {
                int filled = board.RowBinary[r];
                if (filled == Full || filled == 0) continue;
                int gapMask = (~filled) & Full;
                int m = Popcount(gapMask);
                if (m >= min && m <= max) result.Add(new RowGap(r, gapMask, m));
            }
            return result;
        }

        /// <summary>计算每列的填充位掩码：bit (N-1-c) = 1 表示第 c 列已占用。</summary>
        private static int[] ColFilledMask(BinaryBoard board)
        {
            var cols = new int[N];
            for (int r = 0; r < N; r++)
            {
                int row = board.RowBinary[r];
                for (int c = 0; c < N; c++)
                {
                    int bit = (row >> (N - c - 1)) & 1;
                    if (bit != 0) cols[c] |= 1 << (N - r - 1);
                }
            }
            return cols;
        }

        /// <summary>找还缺 missing 格的列。</summary>
        public static List<ColGap> ColsMissing(BinaryBoard board, int missing)
        {
            var cols = ColFilledMask(board);
            var result = new List<ColGap>();
            for (int c = 0; c < N; c++)
            {
                int filled = cols[c];
                if (filled == Full || filled == 0) continue;
                int gapMask = (~filled) & Full;
                if (Popcount(gapMask) == missing) result.Add(new ColGap(c, gapMask));
            }
            return result;
        }

        /// <summary>最大全空矩形（直方图 + 单调栈，O(N²)）。</summary>
        public static Rect LargestEmptyRect(BinaryBoard board)
        {
            var heights = new int[N];
            var best = new Rect(0, 0, 0, 0);
            var stack = new List<int>(N + 1);
            for (int r = 0; r < N; r++)
            {
                int row = board.RowBinary[r];
                for (int c = 0; c < N; c++)
                {
                    bool empty = ((row >> (N - c - 1)) & 1) == 0;
                    heights[c] = empty ? heights[c] + 1 : 0;
                }
                stack.Clear();
                for (int c = 0; c <= N; c++)
                {
                    int h = c == N ? 0 : heights[c];
                    while (stack.Count > 0 && heights[stack[stack.Count - 1]] > h)
                    {
                        int top = stack[stack.Count - 1];
                        stack.RemoveAt(stack.Count - 1);
                        int left = stack.Count == 0 ? -1 : stack[stack.Count - 1];
                        int width = c - left - 1;
                        int height = heights[top];
                        if (width * height > best.W * best.H)
                        {
                            best = new Rect(width, height, r - height + 1, left + 1);
                        }
                    }
                    stack.Add(c);
                }
            }
            return best;
        }

        /// <summary>棋盘已填充率（0~1）。</summary>
        public static double FillRatio(BinaryBoard board)
        {
            int n = 0;
            for (int r = 0; r < N; r++) n += Popcount(board.RowBinary[r]);
            return (double)n / (N * N);
        }

        // ─── 形状索引（按 (w,h) 分组），首次访问时懒构建 ──────────────────

        private static Dictionary<int, List<int>> _shapeByWidth;
        private static Dictionary<int, List<int>> _shapeByHeight;
        private static Dictionary<int, int> _horizontalLines;  // width -> shape id
        private static Dictionary<int, int> _verticalLines;    // height -> shape id

        private static void BuildShapeIndexIfNeeded()
        {
            if (_shapeByWidth != null) return;
            _shapeByWidth = new Dictionary<int, List<int>>();
            _shapeByHeight = new Dictionary<int, List<int>>();
            _horizontalLines = new Dictionary<int, int>();
            _verticalLines = new Dictionary<int, int>();

            foreach (int id in BlockShapeMap.CommonShapeIds)
            {
                var s = BlockShapeMap.Get(id);
                if (s == null) continue;
                if (!_shapeByWidth.TryGetValue(s.Width, out var lw)) { lw = new List<int>(); _shapeByWidth[s.Width] = lw; }
                lw.Add(id);
                if (!_shapeByHeight.TryGetValue(s.Height, out var lh)) { lh = new List<int>(); _shapeByHeight[s.Height] = lh; }
                lh.Add(id);
                // 纯横一行：height=1, shape=[(1<<w)-1]
                if (s.Height == 1 && s.Shape[0] == (1 << s.Width) - 1)
                {
                    if (!_horizontalLines.ContainsKey(s.Width)) _horizontalLines[s.Width] = id;
                }
                // 纯竖一列：width=1, shape 全 1
                if (s.Width == 1)
                {
                    bool allOne = true;
                    for (int i = 0; i < s.Shape.Length; i++) if (s.Shape[i] != 1) { allOne = false; break; }
                    if (allOne && !_verticalLines.ContainsKey(s.Height)) _verticalLines[s.Height] = id;
                }
            }
        }

        public static IReadOnlyList<int> ShapesByWidth(int w)
        {
            BuildShapeIndexIfNeeded();
            return _shapeByWidth.TryGetValue(w, out var l) ? l : (IReadOnlyList<int>)System.Array.Empty<int>();
        }

        public static IReadOnlyList<int> ShapesByHeight(int h)
        {
            BuildShapeIndexIfNeeded();
            return _shapeByHeight.TryGetValue(h, out var l) ? l : (IReadOnlyList<int>)System.Array.Empty<int>();
        }

        /// <summary>纯横一行 width=w 的形状 ID，找不到返回 -1。</summary>
        public static int HorizontalLineShape(int width)
        {
            BuildShapeIndexIfNeeded();
            return _horizontalLines.TryGetValue(width, out var id) ? id : -1;
        }

        /// <summary>纯竖一列 height=h 的形状 ID，找不到返回 -1。</summary>
        public static int VerticalLineShape(int height)
        {
            BuildShapeIndexIfNeeded();
            return _verticalLines.TryGetValue(height, out var id) ? id : -1;
        }

        /// <summary>能塞进 w×h 矩形的所有形状（width&lt;=w 且 height&lt;=h）。</summary>
        public static List<int> ShapesFittingRect(int w, int h)
        {
            var result = new List<int>();
            foreach (int id in BlockShapeMap.CommonShapeIds)
            {
                var s = BlockShapeMap.Get(id);
                if (s == null) continue;
                if (s.Width <= w && s.Height <= h) result.Add(id);
            }
            return result;
        }

        /// <summary>几乎填满 w×h 矩形的形状（占用面积 / (w*h) ≥ minRatio）。</summary>
        public static List<int> ShapesFillingRect(int w, int h, double minRatio = 0.6)
        {
            var result = new List<int>();
            foreach (int id in BlockShapeMap.CommonShapeIds)
            {
                var s = BlockShapeMap.Get(id);
                if (s == null) continue;
                if (s.Width > w || s.Height > h) continue;
                int cells = 0;
                for (int i = 0; i < s.Shape.Length; i++) cells += Popcount(s.Shape[i]);
                if ((double)cells / (w * h) >= minRatio) result.Add(id);
            }
            return result;
        }
    }
}
