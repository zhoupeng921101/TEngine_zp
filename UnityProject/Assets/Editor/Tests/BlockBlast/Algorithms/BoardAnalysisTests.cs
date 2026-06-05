using NUnit.Framework;
using GameLogic.BlockBlast.Algorithms;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast.Tests.Algorithms
{
    /// <summary>
    /// 验证棋盘分析工具（位运算 + 最大空矩形 + 形状索引）。
    /// </summary>
    [TestFixture]
    public class BoardAnalysisTests
    {
        [Test]
        public void RowsMissing_DetectsRowsByGapCount()
        {
            var b = new BinaryBoard();
            // 第 0 行：占 7 个，缺 1 个
            for (int c = 0; c < 7; c++) b.PutBlock(1, new Vec2Int(c, 0));
            var rows = BoardAnalysis.RowsMissing(b, 1);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(0, rows[0].R);
        }

        [Test]
        public void RowsMissingRange_RespectsRange()
        {
            var b = new BinaryBoard();
            // 第 1 行缺 3 格（占 0,1,2,3,4 5 格）
            for (int c = 0; c < 5; c++) b.PutBlock(1, new Vec2Int(c, 1));
            var rows = BoardAnalysis.RowsMissingRange(b, 1, 3);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(3, rows[0].Missing);
        }

        [Test]
        public void LargestEmptyRect_EmptyBoard_Is8x8()
        {
            var b = new BinaryBoard();
            var rect = BoardAnalysis.LargestEmptyRect(b);
            Assert.AreEqual(8, rect.W);
            Assert.AreEqual(8, rect.H);
        }

        [Test]
        public void LargestEmptyRect_FullBoard_Is0()
        {
            var b = new BinaryBoard();
            for (int r = 0; r < 8; r++) b.RowBinary[r] = BinaryBoard.FullRow;
            var rect = BoardAnalysis.LargestEmptyRect(b);
            Assert.AreEqual(0, rect.W * rect.H);
        }

        [Test]
        public void FillRatio_HalfBoard_Is_0_5()
        {
            var b = new BinaryBoard();
            for (int r = 0; r < 4; r++) b.RowBinary[r] = BinaryBoard.FullRow;
            double ratio = BoardAnalysis.FillRatio(b);
            Assert.AreEqual(0.5, ratio, 1e-9);
        }

        [Test]
        public void HorizontalLineShape_Width3_IsId5()
        {
            // shape id=5 是 3x1 横线
            int id = BoardAnalysis.HorizontalLineShape(3);
            Assert.AreEqual(5, id);
        }

        [Test]
        public void VerticalLineShape_Height3_IsId4()
        {
            // shape id=4 是 1x3 竖线
            int id = BoardAnalysis.VerticalLineShape(3);
            Assert.AreEqual(4, id);
        }

        [Test]
        public void ShapesFittingRect_3x3_ExcludesLargerShapes()
        {
            var fitting = BoardAnalysis.ShapesFittingRect(3, 3);
            foreach (int id in fitting)
            {
                var s = BlockShapeMap.Get(id);
                Assert.LessOrEqual(s.Width, 3);
                Assert.LessOrEqual(s.Height, 3);
            }
        }
    }
}
