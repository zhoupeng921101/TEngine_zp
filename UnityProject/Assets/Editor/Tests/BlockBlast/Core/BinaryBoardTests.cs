using NUnit.Framework;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast.Tests.Core
{
    /// <summary>
    /// 对应源项目 tests/board.test.ts，把 8 大类断言移植到 NUnit。
    /// </summary>
    [TestFixture]
    public class BinaryBoardTests
    {
        [Test]
        public void EmptyBoard_IsEmpty()
        {
            var b = new BinaryBoard();
            Assert.IsTrue(b.IsEmpty());
            Assert.IsTrue(b.EmptyAt(0, 0));
            Assert.IsTrue(b.CanPut(13)); // 3×3 实心可以放在空板上
        }

        [Test]
        public void PutSingleCell_OccupiesCell()
        {
            var b = new BinaryBoard();
            b.PutBlock(1, new Vec2Int(0, 0));
            Assert.IsFalse(b.EmptyAt(0, 0));
            Assert.IsTrue(b.EmptyAt(1, 0));
            Assert.IsFalse(b.CanPutBlock(1, new Vec2Int(0, 0)), "重复放同位失败");
            Assert.IsTrue(b.CanPutBlock(1, new Vec2Int(1, 0)));
        }

        [Test]
        public void Boundary_3x3_Reject_OutOfBounds()
        {
            var b = new BinaryBoard();
            Assert.IsFalse(b.CanPutBlock(13, new Vec2Int(6, 6)), "3×3 放 (6,6) 越界");
            Assert.IsTrue(b.CanPutBlock(13, new Vec2Int(5, 5)), "3×3 放 (5,5) 边界内");
        }

        [Test]
        public void FullRow_IsDetectedAndCleared()
        {
            var b = new BinaryBoard();
            b.PutBlock(11, new Vec2Int(0, 0)); // 5x1
            b.PutBlock(5, new Vec2Int(5, 0));  // 3x1
            var r = b.CanClearRowCols(true);
            Assert.AreEqual(1, r.Rows.Count);
            Assert.AreEqual(0, r.Rows[0]);
            Assert.IsTrue(b.IsEmpty(), "消除后棋盘空");
        }

        [Test]
        public void FullColumn_IsDetectedAndCleared()
        {
            var b = new BinaryBoard();
            for (int r = 0; r < 8; r++) b.PutBlock(1, new Vec2Int(0, r));
            var result = b.CanClearRowCols(true);
            Assert.AreEqual(1, result.Cols.Count);
            Assert.AreEqual(0, result.Cols[0]);
            Assert.IsTrue(b.IsEmpty());
        }

        [Test]
        public void RowAndColumn_BothCleared_Simultaneously()
        {
            var b = new BinaryBoard();
            for (int c = 0; c < 8; c++) b.PutBlock(1, new Vec2Int(c, 0));
            for (int r = 1; r < 8; r++) b.PutBlock(1, new Vec2Int(0, r));
            var result = b.CanClearRowCols(true);
            Assert.Contains(0, result.Rows);
            Assert.Contains(0, result.Cols);
        }

        [Test]
        public void CanPut_ReturnsFalse_WhenNoFit()
        {
            var b = new BinaryBoard();
            // 几乎填满（除右下角 1 格）
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 7; c++) b.PutBlock(1, new Vec2Int(c, r));
            for (int c = 0; c < 7; c++) b.PutBlock(1, new Vec2Int(c, 7));
            Assert.IsTrue(b.CanPut(1), "单格还能放在 (7,7)");
            Assert.IsFalse(b.CanPut(9), "2×2 放不下");
        }

        [Test]
        public void CheckPutAllBlocks_EmptyBoard()
        {
            var b = new BinaryBoard();
            Assert.IsTrue(b.CheckPutAllBlocks(new[] { 1, 1, 1 }), "空棋盘可放 3 个单格");
        }

        [Test]
        public void CheckPutAllBlocks_OnlyOneCellLeft()
        {
            var b = new BinaryBoard();
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    if (!(r == 7 && c == 7)) b.PutBlock(1, new Vec2Int(c, r));
            Assert.IsFalse(b.CheckPutAllBlocks(new[] { 1, 1, 1 }), "只剩 1 格放不下 3 个单格");
            Assert.IsTrue(b.CheckPutAllBlocks(new[] { 1 }), "只剩 1 格能放 1 个单格");
        }

        [Test]
        public void ConvertFromArrThenToArr_RoundTrips()
        {
            var b = new BinaryBoard();
            var arr = new int[8][];
            for (int r = 0; r < 8; r++)
            {
                arr[r] = new int[8];
                for (int c = 0; c < 8; c++) arr[r][c] = (r + c) % 2 == 0 ? -1 : 1;
            }
            b.ConvertFromArr(arr);
            var back = b.ConvertToArr();
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    Assert.AreEqual(arr[r][c], back[r][c], $"r={r},c={c}");
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var b = new BinaryBoard();
            b.PutBlock(9, new Vec2Int(0, 0));
            var clone = b.Clone();
            clone.PutBlock(1, new Vec2Int(7, 7));
            Assert.IsTrue(b.EmptyAt(7, 7));
            Assert.IsFalse(clone.EmptyAt(7, 7));
            Assert.IsFalse(b.EmptyAt(0, 0));
            Assert.IsFalse(clone.EmptyAt(0, 0));
        }

        [Test]
        public void GetCanPutPoss_EmptyBoard_MaxPositions()
        {
            var b = new BinaryBoard();
            // 1x1：64 个位置
            var poss1 = b.GetCanPutPoss(1);
            Assert.AreEqual(64, poss1.Count);
            // 3x3：6×6 = 36 个
            var poss3 = b.GetCanPutPoss(13);
            Assert.AreEqual(36, poss3.Count);
        }

        [Test]
        public void CanPutAnyOf_ReturnsTrueIfAnyFits()
        {
            var b = new BinaryBoard();
            // 几乎填满，只留 (7,7)
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    if (!(r == 7 && c == 7)) b.PutBlock(1, new Vec2Int(c, r));
            Assert.IsTrue(b.CanPutAnyOf(new[] { 1, 9 }), "至少 1 能放");
            Assert.IsFalse(b.CanPutAnyOf(new[] { 9, 13 }), "都放不下");
        }
    }
}
