using NUnit.Framework;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 收集 Demo 切片核心逻辑测试：门控 / 转移 / 收集计数 / 达标 / 重置 / off 回归。
    /// </summary>
    [TestFixture]
    public class CollectDemoTests
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

        // ── 验收 #2：CollectMode 默认 off，且 off 时不碰元素层 ──
        [Test]
        public void Default_CollectModeOff_NoElementArr()
        {
            var s = BlockGameState.Instance;
            Assert.IsFalse(s.CollectMode);
            Assert.IsNull(s.ElementArr);
            // off 模式 BuildPiece 不注入元素
            var p = s.BuildPiece(9);
            Assert.IsNull(p.Elements, "off 模式候选块不应携带元素");
        }

        [Test]
        public void OffMode_PlacePiece_DoesNotTouchElements()
        {
            var s = BlockGameState.Instance;
            s.SetFirstHand();
            var board = new BinaryBoard();
            // 手动给 piece 塞元素，但 off 模式应被忽略（ElementArr 仍 null）
            s.OperaArr[0].Elements = new[] { CollectElement.Diamond, CollectElement.Diamond, CollectElement.Diamond, CollectElement.Diamond };
            s.PlacePiece(0, board, 0, 0);
            Assert.IsNull(s.ElementArr, "off 模式不应分配/写入元素层");
        }

        // ── 验收 #8：ResetForCollectDemo 解析目标、清零、空棋盘、开门控 ──
        [Test]
        public void ResetForCollectDemo_SetsTargetsAndEmptyBoard()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForCollectDemo(board);

            Assert.IsTrue(s.CollectMode);
            Assert.IsNotNull(s.ElementArr);
            Assert.Greater(s.CollectionTargets.Count, 0);
            foreach (var kv in s.CollectionTargets)
            {
                Assert.AreEqual(0, s.Collected[kv.Key], "初值应为 0/target");
                Assert.Greater(kv.Value, 0);
            }
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                {
                    Assert.AreEqual(-1, s.SaveArr[r][c]);
                    Assert.AreEqual(CollectElement.None, s.ElementArr[r][c]);
                }
            // 补满 3 块
            for (int i = 0; i < 3; i++) Assert.IsNotNull(s.OperaArr[i]);
        }

        // ── 验收 #4：落子按填充格行优先顺序转移元素，无错位 ──
        [Test]
        public void PlacePiece_TransfersElementsInFillOrder()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForCollectDemo(board);

            // 形状 9 = 2x2 实心，4 个填充格，行优先顺序 (0,0)(0,1)(1,0)(1,1)
            var piece = new PendingPiece(9, BlockColor.Blue)
            {
                Elements = new[]
                {
                    CollectElement.Diamond,  // (0,0)
                    CollectElement.None,     // (0,1)
                    CollectElement.Star,     // (1,0)
                    CollectElement.Diamond,  // (1,1)
                }
            };
            s.OperaArr[0] = piece;
            s.PlacePiece(0, board, 0, 0);

            Assert.AreEqual(CollectElement.Diamond, s.ElementArr[0][0]);
            Assert.AreEqual(CollectElement.None, s.ElementArr[0][1]);
            Assert.AreEqual(CollectElement.Star, s.ElementArr[1][0]);
            Assert.AreEqual(CollectElement.Diamond, s.ElementArr[1][1]);
        }

        // ── 验收 #5：消除时统计元素、清 overlay；行列交叉只计一次 ──
        [Test]
        public void CollectClearedElements_CountsAndClears()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForCollectDemo(board);

            // 在行 0 放 3 个 Diamond，列 1 放 1 个 Star（其中 (0,1) 属交叉）
            s.ElementArr[0][0] = CollectElement.Diamond;
            s.ElementArr[0][1] = CollectElement.Diamond; // 交叉格
            s.ElementArr[0][2] = CollectElement.Diamond;
            s.ElementArr[3][1] = CollectElement.Star;

            int gained = s.CollectClearedElements(new[] { 0 }, new[] { 1 });
            Assert.AreEqual(4, gained, "3 Diamond + 1 Star，交叉格只计一次");
            Assert.AreEqual(3, s.Collected[CollectElement.Diamond]);
            Assert.AreEqual(1, s.Collected[CollectElement.Star]);
            // overlay 清空
            Assert.AreEqual(CollectElement.None, s.ElementArr[0][0]);
            Assert.AreEqual(CollectElement.None, s.ElementArr[0][1]);
            Assert.AreEqual(CollectElement.None, s.ElementArr[3][1]);
        }

        // ── 验收 #6：所有目标达标才胜利 ──
        [Test]
        public void IsCollectionComplete_OnlyWhenAllTargetsMet()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForCollectDemo(board);

            Assert.IsFalse(s.IsCollectionComplete(), "初始未达标");
            // 全部填满目标
            foreach (var kv in new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<CollectElement, int>>(s.CollectionTargets))
                s.Collected[kv.Key] = kv.Value;
            Assert.IsTrue(s.IsCollectionComplete());

            // 退出收集后即使 Collected 满也不算达标（门控）
            s.ExitCollectMode();
            Assert.IsFalse(s.IsCollectionComplete());
        }

        // ── 验收 #3：注入只出「仍需收集」类型；已达标类型不再出 ──
        [Test]
        public void BuildPiece_DoesNotInjectSatisfiedTypes()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForCollectDemo(board);

            // 把所有类型标记为已达标 → needed 为空 → 不注入
            foreach (var kv in new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<CollectElement, int>>(s.CollectionTargets))
                s.Collected[kv.Key] = kv.Value;

            for (int i = 0; i < 30; i++)
            {
                var p = s.BuildPiece(13); // 3x3 实心，9 格，注入概率高
                Assert.IsNull(p.Elements, "全部达标后不应再注入任何元素");
            }
        }

        // ── ExitCollectMode 清理，回到 Classic 零残留 ──
        [Test]
        public void ExitCollectMode_ClearsState()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForCollectDemo(board);
            s.ExitCollectMode();

            Assert.IsFalse(s.CollectMode);
            Assert.IsNull(s.ElementArr);
            Assert.AreEqual(0, s.CollectionTargets.Count);
            Assert.AreEqual(0, s.Collected.Count);
            // 回到 off 后 BuildPiece 不再注入
            Assert.IsNull(s.BuildPiece(9).Elements);
        }
    }
}
