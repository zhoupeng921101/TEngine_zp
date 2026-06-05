using NUnit.Framework;
using GameLogic.BlockBlast.Algorithms;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast.Tests.Algorithms
{
    [TestFixture]
    public class BoardEvaluatorTests
    {
        [Test]
        public void EnumerateSolutions_EmptyTrio_HasOneEmptySolution()
        {
            var b = new BinaryBoard();
            var sols = BoardEvaluator.EnumerateSolutions(b, new int[0]);
            Assert.AreEqual(1, sols.Count);
            Assert.AreEqual(0, sols[0].Count);
        }

        [Test]
        public void CountSolutions_EmptyBoard_TripleSingle_RespectsLimit()
        {
            var b = new BinaryBoard();
            // 3 个 1x1 在空板上有海量解，limit 应起作用
            int n = BoardEvaluator.CountSolutions(b, new[] { 1, 1, 1 }, 10);
            Assert.AreEqual(10, n);
        }

        [Test]
        public void Simulate_FullsRowAndClears()
        {
            var b = new BinaryBoard();
            // 预填行 0 的 7 格
            for (int c = 0; c < 7; c++) b.PutBlock(1, new Vec2Int(c, 0));
            // 模拟在 (7,0) 落 1×1
            var seq = new System.Collections.Generic.List<Placement>
            {
                new Placement(1, new Vec2Int(7, 0))
            };
            var sim = BoardEvaluator.Simulate(b, seq);
            Assert.AreEqual(8, sim.cleared);
            // simulate 应当不修改原棋盘
            Assert.AreEqual(7, BoardEvaluator.FilledCount(b.RowBinary));
        }

        [Test]
        public void Entropy_EmptyBoard_IsZero()
        {
            var rows = new int[8];
            Assert.AreEqual(0, BoardEvaluator.Entropy(rows));
        }

        [Test]
        public void Entropy_FullBoard_IsZero()
        {
            var rows = new int[8];
            for (int r = 0; r < 8; r++) rows[r] = BinaryBoard.FullRow;
            Assert.AreEqual(0, BoardEvaluator.Entropy(rows));
        }

        [Test]
        public void Entropy_CheckerBoard_IsMaximum()
        {
            // 棋盘格：相邻全不同
            var rows = new int[8];
            for (int r = 0; r < 8; r++) rows[r] = r % 2 == 0 ? 0b10101010 : 0b01010101;
            // 水平边 8 行 × 7 边 = 56；垂直边 8 列 × 7 边 = 56；总 112
            Assert.AreEqual(112, BoardEvaluator.Entropy(rows));
        }

        [Test]
        public void TrioCells_SumsCellCounts()
        {
            // 9=4, 13=9, 1=1
            Assert.AreEqual(14, BoardEvaluator.TrioCells(new[] { 9, 13, 1 }));
        }

        [Test]
        public void FilledCount_CountsBits()
        {
            var rows = new[] { 0xFF, 0, 0, 0, 0, 0, 0, 0 };
            Assert.AreEqual(8, BoardEvaluator.FilledCount(rows));
        }
    }
}
