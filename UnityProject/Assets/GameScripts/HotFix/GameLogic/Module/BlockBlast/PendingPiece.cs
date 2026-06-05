using System;
using GameLogic.BlockBlast.Algorithms;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 候选槽中的待落子方块。
    /// </summary>
    [Serializable]
    public sealed class PendingPiece
    {
        public int ShapeId;
        public BlockColor Color;

        /// <summary>
        /// 该 piece 由哪个动态调度算法 offer 出来。落子时回写到 DynamicWeightDiff.AddWeight。
        /// HasAlgo=false 表示首发或调试注入，跳过权重反馈。
        /// </summary>
        public bool HasAlgo;
        public AlgorithmKind Algo;

        public PendingPiece(int shapeId, BlockColor color)
        {
            ShapeId = shapeId;
            Color = color;
            HasAlgo = false;
        }

        public void SetAlgo(AlgorithmKind algo)
        {
            HasAlgo = true;
            Algo = algo;
        }
    }
}
