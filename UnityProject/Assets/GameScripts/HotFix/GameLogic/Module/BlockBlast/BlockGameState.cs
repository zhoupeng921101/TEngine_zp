using System;
using System.Collections.Generic;
using GameLogic.BlockBlast.Algorithms;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// Block Blast 全局游戏状态（单例）。继承 TEngine 的 Singleton&lt;T&gt;。
    /// 仅 Classic 模式（v1 不含 Adventure/Collection）。
    /// </summary>
    public sealed class BlockGameState : SimpleSingleton<BlockGameState>
    {
        private const string StorageKey = "block_blast_save_v1";

        /// <summary>8×8 棋盘：-1=空，0..7=BlockColor 索引。</summary>
        public int[][] SaveArr;

        /// <summary>手持的 3 个候选方块（落子后置 null）。</summary>
        public PendingPiece[] OperaArr = new PendingPiece[3];

        /// <summary>当前局得分。</summary>
        public int Score;
        /// <summary>历史最高分（Classic）。</summary>
        public int HighScore;
        /// <summary>连击数（连续消除 +1，未消除清零）。</summary>
        public int Combo;

        protected override void OnInit()
        {
            SaveArr = MakeEmptyBoard();
            OperaArr = new PendingPiece[3];
            Score = 0;
            HighScore = 0;
            Combo = 0;
        }

        private static int[][] MakeEmptyBoard()
        {
            var b = new int[8][];
            for (int r = 0; r < 8; r++)
            {
                b[r] = new int[8];
                for (int c = 0; c < 8; c++) b[r][c] = -1;
            }
            return b;
        }

        /// <summary>重置到新一局开始状态（清棋盘 + 清槽 + 清分数 + 补满 3 块）。</summary>
        public void ResetForLevel(BinaryBoard board)
        {
            SaveArr = MakeEmptyBoard();
            OperaArr = new PendingPiece[3];
            Score = 0;
            Combo = 0;
            RefillPieces(board);
        }

        /// <summary>从 39 个白名单形状 ID 中均匀随机抽。</summary>
        private int RandomShapeId()
        {
            // 无尽模式分数 < 10000 时剔除 EARLY_GAME_BLOCKED
            IReadOnlyList<int> pool = BlockShapeMap.CommonShapeIds;
            if (Score < BlockShapeMap.EarlyGameBlockScoreThreshold)
            {
                var filtered = new List<int>();
                foreach (int id in BlockShapeMap.CommonShapeIds)
                {
                    if (!BlockShapeMap.EarlyGameBlockedIds.Contains(id)) filtered.Add(id);
                }
                pool = filtered;
            }
            return pool[RandomSource.Index(pool.Count)];
        }

        private static BlockColor RandomColor()
        {
            int idx = RandomSource.Range(0, 8);
            return (BlockColor)idx;
        }

        public PendingPiece BuildPiece(int shapeId)
            => new PendingPiece(shapeId, RandomColor());

        /// <summary>3 个形状互不重复的随机 trio（池耗尽时回落到允许重复）。</summary>
        private List<PendingPiece> RandomDistinctTrio()
        {
            IReadOnlyList<int> pool = BlockShapeMap.CommonShapeIds;
            if (Score < BlockShapeMap.EarlyGameBlockScoreThreshold)
            {
                var filtered = new List<int>();
                foreach (int id in BlockShapeMap.CommonShapeIds)
                {
                    if (!BlockShapeMap.EarlyGameBlockedIds.Contains(id)) filtered.Add(id);
                }
                pool = filtered;
            }
            var available = new List<int>(pool);
            var ids = new List<int>(3);
            for (int i = 0; i < 3 && available.Count > 0; i++)
            {
                int idx = RandomSource.Index(available.Count);
                ids.Add(available[idx]);
                available.RemoveAt(idx);
            }
            while (ids.Count < 3) ids.Add(pool[RandomSource.Index(pool.Count)]);
            var trio = new List<PendingPiece>(3);
            for (int i = 0; i < 3; i++) trio.Add(BuildPiece(ids[i]));
            return trio;
        }

        /// <summary>
        /// 当 OperaArr 全空时补满 3 个。
        /// - board=null：纯加权随机
        /// - score &lt; ActivationScore：随机无死亡（加形状去重）
        /// - 分数 ≥ ActivationScore 且 DynamicWeightDiff 已初始化：动态调度
        /// 兜底退到 3 个 1×1。
        /// </summary>
        public void RefillPieces(BinaryBoard board)
        {
            bool allEmpty = true;
            for (int i = 0; i < OperaArr.Length; i++)
            {
                if (OperaArr[i] != null) { allEmpty = false; break; }
            }
            if (!allEmpty) return;

            // 走动态调度
            var dyn = DynamicWeightDiff.Instance;
            if (board != null && dyn.IsInitialized())
            {
                var off = dyn.OfferTrio(board, Score);
                for (int i = 0; i < 3; i++)
                {
                    var p = BuildPiece(off.Ids[i]);
                    p.SetAlgo(off.Algo);
                    OperaArr[i] = p;
                }
                return;
            }

            // 旧行为：随机无死亡 + 形状去重
            List<PendingPiece> chosen = null;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                var trio = RandomDistinctTrio();
                if (board == null) { chosen = trio; break; }
                var ids = new int[3];
                for (int i = 0; i < 3; i++) ids[i] = trio[i].ShapeId;
                if (board.CheckPutAllBlocks(ids)) { chosen = trio; break; }
            }
            if (chosen == null)
            {
                chosen = new List<PendingPiece>(3)
                {
                    new PendingPiece(1, RandomColor()),
                    new PendingPiece(1, RandomColor()),
                    new PendingPiece(1, RandomColor()),
                };
            }
            for (int i = 0; i < 3; i++) OperaArr[i] = chosen[i];
        }

        /// <summary>首发 3 个固定形状（默认 [9,39,24]），颜色随机。不带算法标签。</summary>
        public void SetFirstHand()
        {
            var ids = GameConfigBB.FirstHand;
            for (int i = 0; i < 3; i++) OperaArr[i] = BuildPiece(ids[i]);
        }

        /// <summary>把指定槽位的方块放置到棋盘上（不做校验）。</summary>
        public void PlacePiece(int slotIdx, BinaryBoard board, int posCol, int posRow)
        {
            var piece = OperaArr[slotIdx];
            if (piece == null) return;

            // 1) 更新二进制棋盘
            board.PutBlock(piece.ShapeId, new Vec2Int(posCol, posRow));

            // 2) 更新 SaveArr（带颜色）
            int colorIdx = (int)piece.Color;
            var shape = BlockShapeMap.Get(piece.ShapeId);
            if (shape != null)
            {
                for (int r = 0; r < shape.Height; r++)
                {
                    for (int c = 0; c < shape.Width; c++)
                    {
                        int colBit = (shape.Shape[r] >> (shape.Width - c - 1)) & 1;
                        if (colBit != 0)
                        {
                            SaveArr[posRow + r][posCol + c] = colorIdx;
                        }
                    }
                }
            }

            // 3) 清空槽位
            OperaArr[slotIdx] = null;

            // 4) 动态难度反馈
            if (piece.HasAlgo)
            {
                DynamicWeightDiff.Instance.AddWeight(piece.Algo);
            }
        }

        /// <summary>清除棋盘上指定的行/列（在 BinaryBoard.CanClearRowCols 之后调用），返回清掉的格数。</summary>
        public int ClearRowsAndCols(IList<int> rows, IList<int> cols)
        {
            int cleared = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                int r = rows[i];
                for (int c = 0; c < 8; c++)
                {
                    if (SaveArr[r][c] != -1) { SaveArr[r][c] = -1; cleared++; }
                }
            }
            for (int i = 0; i < cols.Count; i++)
            {
                int c = cols[i];
                for (int r = 0; r < 8; r++)
                {
                    if (SaveArr[r][c] != -1) { SaveArr[r][c] = -1; cleared++; }
                }
            }
            return cleared;
        }

        /// <summary>加分。</summary>
        public void AddScore(int amount)
        {
            Score += amount;
            if (Score > HighScore) HighScore = Score;
        }

        // ─── 持久化 ──────────────────────────────────────────────

        [Serializable]
        private sealed class SaveData
        {
            public int[] flatBoard; // 8×8 拍平
            public PendingPieceData[] opera;
            public int score;
            public int highScore;
        }

        [Serializable]
        private sealed class PendingPieceData
        {
            public bool isNull;
            public int shapeId;
            public int color;
            public bool hasAlgo;
            public int algo;
        }

        public void Save()
        {
            try
            {
                var data = new SaveData
                {
                    flatBoard = new int[64],
                    opera = new PendingPieceData[3],
                    score = Score,
                    highScore = HighScore,
                };
                for (int r = 0; r < 8; r++)
                    for (int c = 0; c < 8; c++)
                        data.flatBoard[r * 8 + c] = SaveArr[r][c];
                for (int i = 0; i < 3; i++)
                {
                    var p = OperaArr[i];
                    var pd = new PendingPieceData();
                    if (p == null) pd.isNull = true;
                    else
                    {
                        pd.isNull = false;
                        pd.shapeId = p.ShapeId;
                        pd.color = (int)p.Color;
                        pd.hasAlgo = p.HasAlgo;
                        pd.algo = (int)p.Algo;
                    }
                    data.opera[i] = pd;
                }
                Persistence.Provider.Set(StorageKey, UnityEngine.JsonUtility.ToJson(data));
            }
            catch { /* ignore */ }
        }

        public bool Load()
        {
            try
            {
                if (!Persistence.Provider.TryGet(StorageKey, out string raw) || string.IsNullOrEmpty(raw)) return false;
                var data = UnityEngine.JsonUtility.FromJson<SaveData>(raw);
                if (data == null) return false;
                if (data.flatBoard != null && data.flatBoard.Length == 64)
                {
                    SaveArr = MakeEmptyBoard();
                    for (int r = 0; r < 8; r++)
                        for (int c = 0; c < 8; c++)
                            SaveArr[r][c] = data.flatBoard[r * 8 + c];
                }
                OperaArr = new PendingPiece[3];
                if (data.opera != null)
                {
                    for (int i = 0; i < 3 && i < data.opera.Length; i++)
                    {
                        var pd = data.opera[i];
                        if (pd == null || pd.isNull) { OperaArr[i] = null; continue; }
                        var p = new PendingPiece(pd.shapeId, (BlockColor)pd.color);
                        if (pd.hasAlgo) p.SetAlgo((AlgorithmKind)pd.algo);
                        OperaArr[i] = p;
                    }
                }
                Score = data.score;
                HighScore = data.highScore;
                return true;
            }
            catch { return false; }
        }
    }
}
