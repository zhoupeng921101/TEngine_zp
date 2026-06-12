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

        // ─── 合成+订单+体力模式（Merge-Order Demo 切片）─────────────
        // 红线：所有 merge-order 状态/逻辑由 MergeOrderMode 门控；off 时落子/消除/补块/存档
        //       与 Classic 现状逐字节一致（回归硬验收）。

        /// <summary>与 SaveArr 平行的元素叠加层（None=该格无元素）。仅 merge-order 模式分配/使用。</summary>
        public MergeElement[][] ElementArr;

        /// <summary>合成+订单+体力模式开关。off 时所有 merge-order 分支短路，Classic 行为零变化。</summary>
        public bool MergeOrderMode;

        /// <summary>该模式的新系统状态（合成区/订单/体力/保底/悔棋）。仅 MergeOrderMode 时非空。</summary>
        public MergeOrderState MergeState;

        protected override void OnInit()
        {
            SaveArr = MakeEmptyBoard();
            OperaArr = new PendingPiece[3];
            Score = 0;
            HighScore = 0;
            Combo = 0;
            ElementArr = null;
            MergeOrderMode = false;
            MergeState = null;
        }

        private static MergeElement[][] MakeEmptyElementArr()
        {
            var b = new MergeElement[8][];
            for (int r = 0; r < 8; r++)
            {
                b[r] = new MergeElement[8]; // 默认 None(=0)
            }
            return b;
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
        {
            var piece = new PendingPiece(shapeId, RandomColor());
            if (MergeOrderMode && MergeState != null) DrainPendingElementsInto(piece, shapeId);
            return piece;
        }

        /// <summary>
        /// merge-order 模式补牌：从 <see cref="MergeOrderState.PendingElements"/> 队头按填充格行优先顺序
        /// FIFO 抽取元素写入 <paramref name="piece"/>。队空则不分配 <c>Elements</c>（=纯方块）；
        /// 队列元素少于填充格时，余下格留空（None）。元素来源由消除得分驱动（见 EnqueueScoreElements）。
        /// </summary>
        private void DrainPendingElementsInto(PendingPiece piece, int shapeId)
        {
            var queue = MergeState.PendingElements;
            if (queue.Count == 0) return;

            int cellCount = BlockShapeMap.GetCellCount(shapeId);
            if (cellCount <= 0) return;

            var elements = new MergeElement[cellCount];
            for (int i = 0; i < cellCount && queue.Count > 0; i++)
                elements[i] = queue.Dequeue();
            piece.Elements = elements;
        }

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
                // merge-order 模式：按相同的「填充格行优先顺序」把 piece.Elements[cellIdx] 转移到 ElementArr。
                // off 时 transferElements=false（ElementArr/Elements 均 null），SaveArr 结果逐字节不变。
                bool transferElements = MergeOrderMode && ElementArr != null && piece.Elements != null;
                int cellIdx = 0;
                for (int r = 0; r < shape.Height; r++)
                {
                    for (int c = 0; c < shape.Width; c++)
                    {
                        int colBit = (shape.Shape[r] >> (shape.Width - c - 1)) & 1;
                        if (colBit != 0)
                        {
                            SaveArr[posRow + r][posCol + c] = colorIdx;
                            if (transferElements && cellIdx < piece.Elements.Length)
                            {
                                var el = piece.Elements[cellIdx];
                                if (el != MergeElement.None)
                                    ElementArr[posRow + r][posCol + c] = el;
                            }
                            cellIdx++;
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

        /// <summary>
        /// 清除棋盘上指定的行/列（在 BinaryBoard.CanClearRowCols 之后调用），返回清掉的格数。
        /// 返回值供计分（Classic 计分、merge-order 得分驱动元素生成）使用。
        /// </summary>
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

        // ─── merge-order 模式专用方法（全部由 MergeOrderMode 门控）────────────

        /// <summary>
        /// 统计被清行/列上的元素并清该格 overlay，经 <paramref name="output"/> 输出本次被清元素列表
        /// 供合成区摄入。应在窗口拿到 CanClearRowCols 结果后、与 ClearRowsAndCols 配套调用。
        /// 与 ClearRowsAndCols 一致：行列交叉格只计一次（行 pass 已清，列 pass 见 None 跳过）。
        /// 返回本次被清的元素总数。模式 off 时返回 0、不做任何事。
        /// </summary>
        public int HarvestClearedElements(IList<int> rows, IList<int> cols, List<MergeElement> output = null)
        {
            if (!MergeOrderMode || ElementArr == null) return 0;
            int gained = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                int r = rows[i];
                for (int c = 0; c < 8; c++) gained += HarvestAt(r, c, output);
            }
            for (int i = 0; i < cols.Count; i++)
            {
                int c = cols[i];
                for (int r = 0; r < 8; r++) gained += HarvestAt(r, c, output);
            }
            return gained;
        }

        private int HarvestAt(int r, int c, List<MergeElement> output)
        {
            var el = ElementArr[r][c];
            if (el == MergeElement.None) return 0;
            ElementArr[r][c] = MergeElement.None;
            output?.Add(el);
            return 1;
        }

        /// <summary>
        /// 重置进入合成+订单+体力 Demo：开启 MergeOrderMode + 空棋盘 + 清元素层 + 新建 MergeState
        /// （起始体力/空合成区/初始订单/空元素预算队列/满悔棋次数）+ 补满 3 块。反复进入每次都从初始态开始。
        /// 候选块元素由 PendingElements 队列驱动（开局队空→首手纯方块），DrainPendingElementsInto 读
        /// MergeState，故须在 RefillPieces 之前建好 MergeState。
        /// </summary>
        public void ResetForMergeOrder(BinaryBoard board)
        {
            MergeOrderMode = true;
            SaveArr = MakeEmptyBoard();
            ElementArr = MakeEmptyElementArr();
            OperaArr = new PendingPiece[3];
            Score = 0;
            Combo = 0;
            MergeState = new MergeOrderState();
            MergeState.Reset();
            if (board != null) board.ConvertFromArr(SaveArr);
            RefillPieces(board);
        }

        /// <summary>退出 merge-order Demo：关闭门控 + 释放元素层 + 丢弃 MergeState，回到 Classic 零残留。</summary>
        public void ExitMergeOrder()
        {
            MergeOrderMode = false;
            ElementArr = null;
            MergeState = null;
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
