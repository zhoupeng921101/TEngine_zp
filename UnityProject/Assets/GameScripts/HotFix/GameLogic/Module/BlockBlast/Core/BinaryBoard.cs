using System.Collections.Generic;
using System.Text;

namespace GameLogic.BlockBlast.Core
{
    /// <summary>
    /// 8×8 棋盘核心逻辑：每行用 8 位掩码表示，bit (COL_COUNT-col-1)=1 表示该格被占用。
    /// 例：8 列时，最左列对应 bit 7 (128)。
    /// </summary>
    public sealed class BinaryBoard
    {
        public const int RowCount = 8;
        public const int ColCount = 8;
        /// <summary>整行满的位掩码 = 0b11111111 = 255。</summary>
        public const int FullRow = (1 << ColCount) - 1;

        /// <summary>每行的位掩码。外部仅做读取/批量备份，写入请走 PutBlock/CanClearRowCols。</summary>
        public int[] RowBinary = new int[RowCount];

        /// <summary>从 2D 数组初始化（关卡 Map 加载 / 存档恢复）。-1 表示空，其它表示占用。</summary>
        public void ConvertFromArr(int[][] saveArr)
        {
            for (int r = 0; r < RowCount; r++)
            {
                int bits = 0;
                for (int c = 0; c < ColCount; c++)
                {
                    if (saveArr[r][c] != -1)
                    {
                        bits |= 1 << (ColCount - c - 1);
                    }
                }
                RowBinary[r] = bits;
            }
        }

        /// <summary>棋盘是否为空。</summary>
        public bool IsEmpty()
        {
            for (int r = 0; r < RowCount; r++)
            {
                if (RowBinary[r] != 0) return false;
            }
            return true;
        }

        /// <summary>克隆。</summary>
        public BinaryBoard Clone()
        {
            var b = new BinaryBoard();
            for (int r = 0; r < RowCount; r++) b.RowBinary[r] = RowBinary[r];
            return b;
        }

        /// <summary>指定格子是否为空。</summary>
        public bool EmptyAt(int col, int row)
            => (RowBinary[row] & (1 << (ColCount - col - 1))) == 0;

        /// <summary>把形状放到起点 pos（col=左, row=上） —— 不检查冲突。</summary>
        public void PutBlock(int shapeId, Vec2Int pos)
        {
            var shape = BlockShapeMap.Get(shapeId);
            if (shape == null) return;
            int xShift = ColCount - shape.Width - pos.X;
            for (int i = 0; i < shape.Height; i++)
            {
                RowBinary[i + pos.Y] |= shape.Shape[i] << xShift;
            }
        }

        /// <summary>校验形状能否放到起点 pos。</summary>
        public bool CanPutBlock(int shapeId, Vec2Int pos)
        {
            var shape = BlockShapeMap.Get(shapeId);
            if (shape == null) return false;
            if (pos.X < 0 || pos.Y < 0) return false;
            if (pos.X + shape.Width > ColCount) return false;
            if (pos.Y + shape.Height > RowCount) return false;

            int xShift = ColCount - shape.Width - pos.X;
            for (int i = 0; i < shape.Height; i++)
            {
                if ((RowBinary[i + pos.Y] & (shape.Shape[i] << xShift)) > 0)
                    return false;
            }
            return true;
        }

        /// <summary>返回该形状的所有可放置位置。</summary>
        public List<Vec2Int> GetCanPutPoss(int shapeId)
        {
            var result = new List<Vec2Int>();
            var shape = BlockShapeMap.Get(shapeId);
            if (shape == null) return result;
            for (int x = 0; x <= ColCount - shape.Width; x++)
            {
                for (int y = 0; y <= RowCount - shape.Height; y++)
                {
                    var pos = new Vec2Int(x, y);
                    if (CanPutBlock(shapeId, pos)) result.Add(pos);
                }
            }
            return result;
        }

        /// <summary>该形状能否放到当前棋盘任何位置。</summary>
        public bool CanPut(int shapeId) => GetCanPutPoss(shapeId).Count > 0;

        /// <summary>检查 3 个候选形状是否至少有一种放置顺序能全放下（GameOver 判定）。</summary>
        public bool CheckPutAllBlocks(int[] shapeIds)
        {
            var valid = new List<int>(shapeIds.Length);
            for (int i = 0; i < shapeIds.Length; i++)
            {
                if (shapeIds[i] > 0) valid.Add(shapeIds[i]);
            }
            if (valid.Count == 0) return true;
            return TryAll(valid);
        }

        private bool TryAll(List<int> remaining)
        {
            if (remaining.Count == 0) return true;
            for (int i = 0; i < remaining.Count; i++)
            {
                int id = remaining[i];
                var positions = GetCanPutPoss(id);
                for (int p = 0; p < positions.Count; p++)
                {
                    var backup = new int[RowCount];
                    for (int r = 0; r < RowCount; r++) backup[r] = RowBinary[r];
                    PutBlock(id, positions[p]);
                    var rest = new List<int>(remaining.Count - 1);
                    for (int j = 0; j < remaining.Count; j++) if (j != i) rest.Add(remaining[j]);
                    bool ok = TryAll(rest);
                    for (int r = 0; r < RowCount; r++) RowBinary[r] = backup[r];
                    if (ok) return true;
                }
            }
            return false;
        }

        /// <summary>每个形状能否独立放下（更宽松的 GameOver 判定）。</summary>
        public bool CanPutAnyOf(int[] shapeIds)
        {
            for (int i = 0; i < shapeIds.Length; i++)
            {
                if (shapeIds[i] > 0 && CanPut(shapeIds[i])) return true;
            }
            return false;
        }

        /// <summary>找出当前所有满行/满列，可选择就地消除。</summary>
        public ClearResult CanClearRowCols(bool doClear)
        {
            var result = new ClearResult();

            // 满行
            for (int r = 0; r < RowCount; r++)
            {
                if (RowBinary[r] == FullRow) result.Rows.Add(r);
            }

            // 满列：所有行同列都是 1
            int colBits = FullRow;
            for (int r = 0; r < RowCount; r++) colBits &= RowBinary[r];
            for (int c = ColCount - 1; colBits > 0; c--)
            {
                if ((colBits & 1) != 0) result.Cols.Add(c);
                colBits >>= 1;
            }

            if (doClear)
            {
                for (int i = 0; i < result.Rows.Count; i++) RowBinary[result.Rows[i]] = 0;
                for (int i = 0; i < result.Cols.Count; i++)
                {
                    int c = result.Cols[i];
                    int clearMask = ~(1 << (ColCount - c - 1)) & FullRow;
                    for (int r = 0; r < RowCount; r++) RowBinary[r] &= clearMask;
                }
            }

            return result;
        }

        /// <summary>转回 2D 数组（用于渲染/存档），空格用 -1，占用用 1。</summary>
        public int[][] ConvertToArr()
        {
            var arr = new int[RowCount][];
            for (int r = 0; r < RowCount; r++)
            {
                arr[r] = new int[ColCount];
                for (int c = 0; c < ColCount; c++)
                {
                    arr[r][c] = EmptyAt(c, r) ? -1 : 1;
                }
            }
            return arr;
        }

        /// <summary>调试用：打印棋盘。</summary>
        public string DebugPrint()
        {
            var sb = new StringBuilder();
            for (int r = 0; r < RowCount; r++)
            {
                int row = RowBinary[r];
                for (int c = 0; c < ColCount; c++)
                {
                    bool filled = ((row >> (ColCount - c - 1)) & 1) != 0;
                    sb.Append(filled ? '■' : '·');
                }
                if (r < RowCount - 1) sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
