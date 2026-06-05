using NUnit.Framework;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Algorithms;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast.Tests.Algorithms
{
    /// <summary>
    /// 8 个算法的烟雾测试 + 主分发器。验证每个算法都能返回长度=3 且合法的 trio。
    /// </summary>
    [TestFixture]
    public class AlgorithmsTests
    {
        [SetUp]
        public void SetUp()
        {
            // 固定种子，让算法结果可重现
            RandomSource.SetSeed(12345);
        }

        [Test]
        public void UniformRandomShape_ReturnsValidCommonId()
        {
            for (int i = 0; i < 20; i++)
            {
                int id = BlockAlgorithms.UniformRandomShape();
                Assert.IsTrue(System.Linq.Enumerable.Contains(BlockShapeMap.CommonShapeIds, id));
            }
        }

        [Test]
        public void SampleTrio_HasThreeIds()
        {
            var trio = BlockAlgorithms.SampleTrio();
            Assert.AreEqual(3, trio.Length);
            foreach (int id in trio) Assert.IsNotNull(BlockShapeMap.Get(id));
        }

        [TestCase(AlgorithmKind.Fill)]
        [TestCase(AlgorithmKind.RandomNoDie)]
        [TestCase(AlgorithmKind.Add3)]
        [TestCase(AlgorithmKind.EasyDiff)]
        [TestCase(AlgorithmKind.Diff)]
        [TestCase(AlgorithmKind.StraightDeathDiff)]
        [TestCase(AlgorithmKind.ClearAll)]
        [TestCase(AlgorithmKind.AllCombination)]
        public void GenerateTrio_AllAlgorithms_ReturnValidTrio(AlgorithmKind algo)
        {
            var board = new BinaryBoard();
            // 半填棋盘让 ClearAll/FILL 等都有意义
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++) board.PutBlock(1, new Vec2Int(c, r));

            var trio = BlockAlgorithms.GenerateTrio(algo, board);
            Assert.AreEqual(3, trio.Length, $"{algo} trio 长度");
            foreach (int id in trio)
            {
                Assert.IsNotNull(BlockShapeMap.Get(id), $"{algo} 返回的 id={id} 应存在");
            }
        }

        [Test]
        public void FallbackTrio_AlmostFullBoard_ReturnsAllOnes()
        {
            var b = new BinaryBoard();
            // 只剩 (7,7) 1 格，无法放下任何 trio
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    if (!(r == 7 && c == 7)) b.PutBlock(1, new Vec2Int(c, r));
            var trio = BlockAlgorithms.FallbackTrio(b);
            Assert.AreEqual(3, trio.Length);
            // 兜底必然是 [1,1,1]
            foreach (int id in trio) Assert.AreEqual(1, id);
        }

        [Test]
        public void BoardClearGreedyTrio_NonEmptyBoard_ReturnsThreeIds()
        {
            var b = new BinaryBoard();
            // 行 0 缺 1 格
            for (int c = 0; c < 7; c++) b.PutBlock(1, new Vec2Int(c, 0));
            var trio = BlockAlgorithms.BoardClearGreedyTrio(b);
            Assert.AreEqual(3, trio.Length);
        }
    }
}
