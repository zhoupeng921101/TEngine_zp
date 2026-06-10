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

        /// <summary>
        /// 收集模式下携带的元素：按形状「填充格行优先顺序」每格一个（None=无）。
        /// 非收集模式恒为 null（Classic 零影响）。落子时随方块格转移到 ElementArr。
        /// </summary>
        public CollectElement[] Elements;

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
