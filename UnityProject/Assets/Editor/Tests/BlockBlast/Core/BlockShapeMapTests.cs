using NUnit.Framework;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast.Tests.Core
{
    /// <summary>
    /// 验证 73 个形状定义、COMMON 池长度、首发列表、早期屏蔽列表与 BlockNumMap 一致性。
    /// </summary>
    [TestFixture]
    public class BlockShapeMapTests
    {
        [Test]
        public void ShapeMap_ContainsExpectedKeys()
        {
            // 71 个形状：1-42 (42) + 53-70 (18) + 101-111 (11)
            Assert.AreEqual(71, BlockShapeMap.Map.Count);
            // 边界检验
            Assert.IsNotNull(BlockShapeMap.Get(1), "1×1 单格存在");
            Assert.IsNotNull(BlockShapeMap.Get(111), "5×5 实心存在");
            Assert.IsNull(BlockShapeMap.Get(999), "不存在的 ID 返回 null");
        }

        [Test]
        public void Shape_9_Is_2x2_Solid()
        {
            var s = BlockShapeMap.Get(9);
            Assert.AreEqual(2, s.Width);
            Assert.AreEqual(2, s.Height);
            Assert.AreEqual(3, s.Shape[0]);  // 0b11
            Assert.AreEqual(3, s.Shape[1]);
            Assert.AreEqual(4, BlockShapeMap.GetCellCount(9));
        }

        [Test]
        public void Shape_13_Is_3x3_Solid()
        {
            var s = BlockShapeMap.Get(13);
            Assert.AreEqual(3, s.Width);
            Assert.AreEqual(3, s.Height);
            Assert.AreEqual(9, BlockShapeMap.GetCellCount(13));
        }

        [Test]
        public void Shape_11_Is_5x1_Horizontal()
        {
            var s = BlockShapeMap.Get(11);
            Assert.AreEqual(5, s.Width);
            Assert.AreEqual(1, s.Height);
            Assert.AreEqual(31, s.Shape[0]);
            Assert.AreEqual(5, BlockShapeMap.GetCellCount(11));
        }

        [Test]
        public void CommonShapeIds_HasExactly39()
        {
            Assert.AreEqual(39, BlockShapeMap.CommonShapeIds.Count);
        }

        [Test]
        public void CommonShapeIds_DoesNotContain_1x1()
        {
            Assert.IsFalse(System.Linq.Enumerable.Contains(BlockShapeMap.CommonShapeIds, 1),
                "id=1 (1×1) 不应在 COMMON 池里，避免送给玩家过于简单的安全块");
        }

        [Test]
        public void FirstHand_Is_9_39_24()
        {
            var fh = BlockShapeMap.FirstHandShapeIds;
            Assert.AreEqual(3, fh.Count);
            Assert.AreEqual(9, fh[0]);
            Assert.AreEqual(39, fh[1]);
            Assert.AreEqual(24, fh[2]);
        }

        [Test]
        public void EarlyGameBlockedIds_ExpectedSet()
        {
            var set = BlockShapeMap.EarlyGameBlockedIds;
            Assert.AreEqual(12, set.Count);
            int[] expected = { 8, 30, 33, 34, 37, 38, 39, 40, 53, 54, 55, 56 };
            foreach (int id in expected) Assert.IsTrue(set.Contains(id), $"应屏蔽 {id}");
        }

        [Test]
        public void BlockNumMap_AllShapesCounted()
        {
            // 每个形状的 cell count 应大于 0 且不超过 width*height
            foreach (var kv in BlockShapeMap.Map)
            {
                int n = BlockShapeMap.GetCellCount(kv.Key);
                Assert.Greater(n, 0, $"shape {kv.Key} cell 数应 > 0");
                Assert.LessOrEqual(n, kv.Value.Width * kv.Value.Height,
                    $"shape {kv.Key} cell 数不应超过 W×H");
            }
        }
    }
}
