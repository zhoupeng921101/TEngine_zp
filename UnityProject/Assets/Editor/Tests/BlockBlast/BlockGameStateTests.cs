using NUnit.Framework;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast.Tests
{
    [TestFixture]
    public class BlockGameStateTests
    {
        private InMemoryPersistenceProvider _provider;

        [SetUp]
        public void SetUp()
        {
            _provider = new InMemoryPersistenceProvider();
            Persistence.Provider = _provider;
            RandomSource.SetSeed(99999);
            if (BlockGameState.IsValid) BlockGameState.Instance.Release();
            if (DynamicWeightDiff.IsValid) DynamicWeightDiff.Instance.Release();
        }

        [TearDown]
        public void TearDown()
        {
            if (BlockGameState.IsValid) BlockGameState.Instance.Release();
            if (DynamicWeightDiff.IsValid) DynamicWeightDiff.Instance.Release();
        }

        [Test]
        public void Init_EmptyBoardAndZeroScore()
        {
            var s = BlockGameState.Instance;
            Assert.AreEqual(0, s.Score);
            Assert.AreEqual(0, s.HighScore);
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    Assert.AreEqual(-1, s.SaveArr[r][c]);
            Assert.AreEqual(3, s.OperaArr.Length);
            foreach (var p in s.OperaArr) Assert.IsNull(p);
        }

        [Test]
        public void SetFirstHand_FillsThreeSlots()
        {
            var s = BlockGameState.Instance;
            s.SetFirstHand();
            for (int i = 0; i < 3; i++) Assert.IsNotNull(s.OperaArr[i]);
            Assert.AreEqual(9, s.OperaArr[0].ShapeId);
            Assert.AreEqual(39, s.OperaArr[1].ShapeId);
            Assert.AreEqual(24, s.OperaArr[2].ShapeId);
        }

        [Test]
        public void PlacePiece_UpdatesBoardAndClearsSlot()
        {
            var s = BlockGameState.Instance;
            s.SetFirstHand();
            var board = new BinaryBoard();
            s.PlacePiece(0, board, 0, 0);
            // 9 是 2x2 实心：slot 0 应被清，棋盘四个角应有色
            Assert.IsNull(s.OperaArr[0]);
            // saveArr 四个角应有色（非 -1）
            Assert.AreNotEqual(-1, s.SaveArr[0][0]);
            Assert.AreNotEqual(-1, s.SaveArr[0][1]);
            Assert.AreNotEqual(-1, s.SaveArr[1][0]);
            Assert.AreNotEqual(-1, s.SaveArr[1][1]);
            // 棋盘 (0,0)~(1,1) 都该被占
            Assert.IsFalse(board.EmptyAt(0, 0));
            Assert.IsFalse(board.EmptyAt(1, 1));
        }

        [Test]
        public void AddScore_TracksHighScore()
        {
            var s = BlockGameState.Instance;
            s.AddScore(100);
            Assert.AreEqual(100, s.Score);
            Assert.AreEqual(100, s.HighScore);
            s.AddScore(50);
            Assert.AreEqual(150, s.HighScore);
            // 模拟新局：Score 清零，HighScore 不动
            s.Score = 0;
            s.AddScore(80);
            Assert.AreEqual(80, s.Score);
            Assert.AreEqual(150, s.HighScore, "新局低分不应覆盖 HighScore");
        }

        [Test]
        public void ClearRowsAndCols_ZeroesCellsAndCounts()
        {
            var s = BlockGameState.Instance;
            // 行 0 全是色 2，列 1 全是色 3（行 0 + 列 1 都会被消）
            for (int c = 0; c < 8; c++) s.SaveArr[0][c] = 2;
            for (int r = 0; r < 8; r++) s.SaveArr[r][1] = 3;
            int cleared = s.ClearRowsAndCols(new[] { 0 }, new[] { 1 });
            // 行 0 共 8 格 + 列 1 共 8 格，扣去交叉 1 格 = 15 格被清
            Assert.AreEqual(15, cleared);
            // 验证清后全为 -1
            for (int c = 0; c < 8; c++) Assert.AreEqual(-1, s.SaveArr[0][c]);
            for (int r = 0; r < 8; r++) Assert.AreEqual(-1, s.SaveArr[r][1]);
        }

        [Test]
        public void RefillPieces_FillsAllSlots()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.RefillPieces(board);
            for (int i = 0; i < 3; i++) Assert.IsNotNull(s.OperaArr[i]);
            // 二次 refill：槽未空则 no-op
            var snapshot = s.OperaArr[0];
            s.RefillPieces(board);
            Assert.AreSame(snapshot, s.OperaArr[0]);
        }

        [Test]
        public void SaveAndLoad_RoundTrips()
        {
            var s = BlockGameState.Instance;
            s.SetFirstHand();
            s.SaveArr[3][4] = 5; // 写一格色
            s.Score = 1234;
            s.HighScore = 5678;
            s.Save();

            s.Release();
            var s2 = BlockGameState.Instance;
            Assert.IsTrue(s2.Load());
            Assert.AreEqual(1234, s2.Score);
            Assert.AreEqual(5678, s2.HighScore);
            Assert.AreEqual(5, s2.SaveArr[3][4]);
            Assert.AreEqual(9, s2.OperaArr[0].ShapeId);
            Assert.AreEqual(39, s2.OperaArr[1].ShapeId);
            Assert.AreEqual(24, s2.OperaArr[2].ShapeId);
        }

        [Test]
        public void Load_NoSaveExists_ReturnsFalse()
        {
            var s = BlockGameState.Instance;
            Assert.IsFalse(s.Load());
        }
    }
}
