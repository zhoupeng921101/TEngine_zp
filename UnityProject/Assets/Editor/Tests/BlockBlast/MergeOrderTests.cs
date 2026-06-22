using System.Collections.Generic;
using NUnit.Framework;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 合成+订单+体力 切片核心逻辑测试：体力 / 合成自动配对 / 订单交付刷新 /
    /// 需求拉动 + 得分驱动元素生成（映射·入队·轮转·封顶·抽干）/ 悔棋回滚 / 通关 / off 回归。
    /// 验收点编号对应 state/plan.md 交接区。
    /// </summary>
    [TestFixture]
    public class MergeOrderTests
    {
        private InMemoryPersistenceProvider _provider;

        [SetUp]
        public void SetUp()
        {
            _provider = new InMemoryPersistenceProvider();
            Persistence.Provider = _provider;
            RandomSource.SetSeed(20260612);
            if (BlockGameState.IsValid) BlockGameState.Instance.Release();
            if (DynamicWeightDiff.IsValid) DynamicWeightDiff.Instance.Release();
        }

        [TearDown]
        public void TearDown()
        {
            if (BlockGameState.IsValid) BlockGameState.Instance.Release();
            if (DynamicWeightDiff.IsValid) DynamicWeightDiff.Instance.Release();
        }

        // ───────────────────────── #1 门控 & 重置 ─────────────────────────

        [Test]
        public void Default_MergeOrderModeOff_NoStateNoInject()
        {
            var s = BlockGameState.Instance;
            Assert.IsFalse(s.MergeOrderMode);
            Assert.IsNull(s.MergeState);
            Assert.IsNull(s.ElementArr);
            // off 模式 BuildPiece 不注入
            Assert.IsNull(s.BuildPiece(13).Elements, "off 模式候选块不应携带元素");
        }

        [Test]
        public void ResetForMergeOrder_FreshStateEachEntry()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();

            s.ResetForMergeOrder(board);
            var m = s.MergeState;
            Assert.IsTrue(s.MergeOrderMode);
            Assert.IsNotNull(m);
            Assert.IsNotNull(s.ElementArr);
            Assert.AreEqual(MergeOrderConfig.EnergyStart, m.Energy);
            Assert.AreEqual(0, m.CompletedOrders);
            Assert.AreEqual(0, m.Inventory.Count);
            Assert.AreEqual(MergeOrderConfig.UndoCharges, m.UndoCharges);
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                {
                    Assert.AreEqual(-1, s.SaveArr[r][c]);
                    Assert.AreEqual(MergeElement.None, s.ElementArr[r][c]);
                }

            // 弄脏后再次进入应回到初始态
            m.Energy = 3;
            m.CompletedOrders = 4;
            m.IngestElement(MergeElement.Diamond);
            s.ResetForMergeOrder(board);
            var m2 = s.MergeState;
            Assert.AreEqual(MergeOrderConfig.EnergyStart, m2.Energy);
            Assert.AreEqual(0, m2.CompletedOrders);
            Assert.AreEqual(0, m2.Inventory.Count);
        }

        [Test]
        public void ExitMergeOrder_ZeroResidue_BackToClassic()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);
            s.ExitMergeOrder();

            Assert.IsFalse(s.MergeOrderMode);
            Assert.IsNull(s.MergeState);
            Assert.IsNull(s.ElementArr);
            // 回到 Classic 后 BuildPiece 不再注入
            Assert.IsNull(s.BuildPiece(13).Elements);
        }

        // ───────────────────────── #2/#3 体力 ─────────────────────────

        [Test]
        public void Energy_StartSpendAfford()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);
            var m = s.MergeState;

            Assert.AreEqual(20, m.Energy);
            Assert.IsTrue(m.CanAffordPlace);
            m.SpendPlaceCost();
            Assert.AreEqual(19, m.Energy);

            m.Energy = 0;
            Assert.IsFalse(m.CanAffordPlace, "体力 0 付不起落子");
            m.Energy = MergeOrderConfig.PlaceCost;
            Assert.IsTrue(m.CanAffordPlace);
        }

        [Test]
        public void RefundEnergy_ClampedToCap_RewardOverflows()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);
            var m = s.MergeState;

            // 返还受软上限约束
            m.Energy = 25;
            m.RefundEnergy(3);
            Assert.AreEqual(28, m.Energy);
            m.RefundEnergy(5);
            Assert.AreEqual(MergeOrderConfig.EnergyCap, m.Energy, "返还封顶软上限，不溢出");

            // 订单奖励可溢出软上限
            m.Energy = 28;
            m.IngestElement(MergeElement.Diamond); // 满足初始订单0：Diamond Lv1 ×1
            Assert.IsTrue(m.CanDeliver(0));
            m.Deliver(0);
            Assert.AreEqual(28 + MergeOrderConfig.OrderRewardEnergy, m.Energy, "奖励体力可溢出软上限");
        }

        // ───────────────────────── #6 消除元素入合成区 ─────────────────────────

        [Test]
        public void HarvestClearedElements_OutputsList_ClearsOverlay()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);

            s.ElementArr[0][0] = MergeElement.Diamond;
            s.ElementArr[0][1] = MergeElement.Diamond;
            s.ElementArr[0][2] = MergeElement.Star;

            var outList = new List<MergeElement>();
            int gained = s.HarvestClearedElements(new[] { 0 }, new int[0], outList);

            Assert.AreEqual(3, gained);
            Assert.AreEqual(3, outList.Count);
            int diamonds = 0, stars = 0;
            foreach (var e in outList)
            {
                if (e == MergeElement.Diamond) diamonds++;
                if (e == MergeElement.Star) stars++;
            }
            Assert.AreEqual(2, diamonds);
            Assert.AreEqual(1, stars);
            Assert.AreEqual(MergeElement.None, s.ElementArr[0][0], "overlay 清空");
        }

        [Test]
        public void HarvestClearedElements_RowColIntersection_CountedOnce()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);

            // 行 0 放 3 个 Diamond，列 1 放 1 个 Star，其中 (0,1) 属行列交叉
            s.ElementArr[0][0] = MergeElement.Diamond;
            s.ElementArr[0][1] = MergeElement.Diamond;
            s.ElementArr[0][2] = MergeElement.Diamond;
            s.ElementArr[3][1] = MergeElement.Star;

            int gained = s.HarvestClearedElements(new[] { 0 }, new[] { 1 });
            Assert.AreEqual(4, gained, "3 Diamond + 1 Star，交叉格只计一次");
            Assert.AreEqual(MergeElement.None, s.ElementArr[0][1]);
            Assert.AreEqual(MergeElement.None, s.ElementArr[3][1]);
        }

        // ───────────────────────── 落子元素转移（行优先顺序）─────────────────────────

        [Test]
        public void PlacePiece_TransfersElementsInFillOrder()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);

            // 形状 9 = 2x2 实心，4 个填充格，行优先顺序 (0,0)(0,1)(1,0)(1,1)
            var piece = new PendingPiece(9, BlockColor.Blue)
            {
                Elements = new[]
                {
                    MergeElement.Diamond,  // (0,0)
                    MergeElement.None,     // (0,1)
                    MergeElement.Star,     // (1,0)
                    MergeElement.Diamond,  // (1,1)
                }
            };
            s.OperaArr[0] = piece;
            s.PlacePiece(0, board, 0, 0);

            Assert.AreEqual(MergeElement.Diamond, s.ElementArr[0][0]);
            Assert.AreEqual(MergeElement.None, s.ElementArr[0][1]);
            Assert.AreEqual(MergeElement.Star, s.ElementArr[1][0]);
            Assert.AreEqual(MergeElement.Diamond, s.ElementArr[1][1]);
        }

        [Test]
        public void OffMode_PlacePiece_DoesNotTouchElements()
        {
            var s = BlockGameState.Instance;
            s.SetFirstHand();
            var board = new BinaryBoard();
            // 手动给 piece 塞元素，但 off 模式应被忽略（ElementArr 仍 null）
            s.OperaArr[0].Elements = new[] { MergeElement.Diamond, MergeElement.Diamond, MergeElement.Diamond, MergeElement.Diamond };
            s.PlacePiece(0, board, 0, 0);
            Assert.IsNull(s.ElementArr, "off 模式不应分配/写入元素层");
        }

        // ───────────────────────── #7 合成区自动两两合并升级 ─────────────────────────

        [Test]
        public void Ingest_TwoSame_MakesOneLv2()
        {
            var m = new MergeOrderState();
            m.Reset();
            m.IngestElement(MergeElement.Diamond);
            m.IngestElement(MergeElement.Diamond);
            Assert.AreEqual(0, m.InventoryCount(MergeElement.Diamond, 1));
            Assert.AreEqual(1, m.InventoryCount(MergeElement.Diamond, 2));
        }

        [Test]
        public void Ingest_FourSame_MakesOneLv3()
        {
            var m = new MergeOrderState();
            m.Reset();
            for (int i = 0; i < 4; i++) m.IngestElement(MergeElement.Star);
            Assert.AreEqual(0, m.InventoryCount(MergeElement.Star, 1));
            Assert.AreEqual(0, m.InventoryCount(MergeElement.Star, 2));
            Assert.AreEqual(1, m.InventoryCount(MergeElement.Star, 3));
        }

        [Test]
        public void Ingest_EightSame_MakesTwoLv3_CapStacks()
        {
            var m = new MergeOrderState();
            m.Reset();
            for (int i = 0; i < 8; i++) m.IngestElement(MergeElement.Leaf);
            Assert.AreEqual(2, m.InventoryCount(MergeElement.Leaf, 3), "封顶 Lv3 不再合并，堆积成 2");
        }

        // ───────────────────────── #9/#10/#11 订单 ─────────────────────────

        [Test]
        public void Orders_InitialTwoFromPoolFront()
        {
            var m = new MergeOrderState();
            m.Reset();
            Assert.AreEqual(MergeOrderConfig.ActiveOrders, m.ActiveOrders.Length);
            Assert.AreEqual(MergeOrderConfig.OrderPool[0].Type, m.ActiveOrders[0].Type);
            Assert.AreEqual(MergeOrderConfig.OrderPool[0].Level, m.ActiveOrders[0].Level);
            Assert.AreEqual(MergeOrderConfig.OrderPool[1].Type, m.ActiveOrders[1].Type);
        }

        [Test]
        public void Deliver_ConsumesInventory_GivesReward_RefreshesSlot()
        {
            var m = new MergeOrderState();
            m.Reset();
            // 初始订单1 = Star Lv2 ×1 → 摄入 2 Star 得 1 Lv2
            Assert.IsFalse(m.CanDeliver(1), "库存不足按钮置灰");
            m.IngestElement(MergeElement.Star);
            m.IngestElement(MergeElement.Star);
            Assert.IsTrue(m.CanDeliver(1));

            int scoreBefore = m.TotalScore;
            int energyBefore = m.Energy;
            var nextExpected = MergeOrderConfig.OrderPool[2]; // 交付后该槽刷新为池下一项

            Assert.IsTrue(m.Deliver(1));
            Assert.AreEqual(0, m.InventoryCount(MergeElement.Star, 2), "交付扣除合成物");
            Assert.AreEqual(energyBefore + MergeOrderConfig.OrderRewardEnergy, m.Energy);
            Assert.AreEqual(scoreBefore + 2 * 1 * MergeOrderConfig.OrderScoreFactor, m.TotalScore);
            Assert.AreEqual(1, m.CompletedOrders);
            Assert.AreEqual(nextExpected.Type, m.ActiveOrders[1].Type, "槽刷新为下一单");
            Assert.AreEqual(nextExpected.Level, m.ActiveOrders[1].Level);
        }

        [Test]
        public void NextOrder_CyclesPool()
        {
            var m = new MergeOrderState();
            m.Reset(); // 取走 pool[0],[1]，游标=2
            var pool = MergeOrderConfig.OrderPool;
            for (int i = 2; i < pool.Length; i++)
            {
                var o = m.NextOrder();
                Assert.AreEqual(pool[i].Type, o.Type);
            }
            // 越过尾部应回到 pool[0]
            var wrapped = m.NextOrder();
            Assert.AreEqual(pool[0].Type, wrapped.Type);
            Assert.AreEqual(pool[0].Level, wrapped.Level);
        }

        // ───────────────────────── #12 需求拉动 + 得分驱动元素生成 ─────────────────────────

        [Test]
        public void NeededTypes_UnionOfActiveOrders()
        {
            var m = new MergeOrderState();
            m.Reset();
            var needed = m.NeededTypes();
            Assert.Contains(m.ActiveOrders[0].Type, needed);
            Assert.Contains(m.ActiveOrders[1].Type, needed);
        }

        [Test]
        public void ElementsForScore_MapsPerTier()
        {
            // 负/0 分 → 0；逐档：110→1、200→1、280→2、500→3、780→4；超高连消封顶 4；保底 1
            Assert.AreEqual(0, MergeOrderConfig.ElementsForScore(-50));
            Assert.AreEqual(0, MergeOrderConfig.ElementsForScore(0));
            Assert.AreEqual(1, MergeOrderConfig.ElementsForScore(1), "任何正得分保底 1");
            Assert.AreEqual(1, MergeOrderConfig.ElementsForScore(110));
            Assert.AreEqual(1, MergeOrderConfig.ElementsForScore(200));
            Assert.AreEqual(2, MergeOrderConfig.ElementsForScore(280));
            Assert.AreEqual(3, MergeOrderConfig.ElementsForScore(500));
            Assert.AreEqual(4, MergeOrderConfig.ElementsForScore(780));
            Assert.AreEqual(4, MergeOrderConfig.ElementsForScore(2000), "封顶 MaxElementsPerClear");
        }

        [Test]
        public void HigherScore_YieldsMoreElements()
        {
            // 四消得分 > 单消得分 → 元素数更多（同 NeededTypes 下）
            int single = MergeOrderConfig.ElementsForScore(BlockScoring.ClearScore(8, 1));   // 单消 ≈110 → 1
            int quad = MergeOrderConfig.ElementsForScore(BlockScoring.ClearScore(30, 4));     // 四消 ≈780 → 4
            Assert.Greater(quad, single, "得分越高产元素越多");
        }

        [Test]
        public void EnqueueScoreElements_TypesSubsetOfNeeded_DistributedAcrossTrio()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);
            var m = s.MergeState;
            Assert.AreEqual(0, m.PendingElements.Count, "开局队列空");

            m.EnqueueScoreElements(2);
            Assert.AreEqual(2, m.PendingElements.Count, "入队 k 个");
            var needed = m.NeededTypes();
            foreach (var e in m.PendingElements) Assert.Contains(e, needed, "类型 ⊆ NeededTypes");

            // trio 级容量加权随机下:BuildPiece 单独调用不消费 PendingElements 队列(元素分配在 trio 补牌时统一进行)。
            int beforeCount = m.PendingElements.Count;
            var p = s.BuildPiece(13);
            Assert.IsNull(p.Elements, "BuildPiece 单独调用不再分配元素");
            Assert.AreEqual(beforeCount, m.PendingElements.Count, "BuildPiece 单独调用不再吃队列");

            // RefillPieces 触发 trio 级分摊:吃掉 min(队列, totalCap) 个元素
            int originalQueue = m.PendingElements.Count;
            // 清空 OperaArr 让 RefillPieces 走分支
            for (int i = 0; i < 3; i++) s.OperaArr[i] = null;
            s.RefillPieces(board);
            int totalCap = 0;
            int placed = 0;
            for (int i = 0; i < 3; i++)
            {
                int cells = BlockShapeMap.GetCellCount(s.OperaArr[i].ShapeId);
                totalCap += cells;
                if (s.OperaArr[i].Elements != null)
                {
                    for (int j = 0; j < s.OperaArr[i].Elements.Length; j++)
                        if (s.OperaArr[i].Elements[j] != MergeElement.None) placed++;
                }
            }
            int expectedDrained = System.Math.Min(originalQueue, totalCap);
            Assert.AreEqual(expectedDrained, placed, "trio 内非 None 元素总数 = min(原队列, totalCap)");
            Assert.AreEqual(originalQueue - expectedDrained, m.PendingElements.Count, "队列剩 = 原队列 - 分摊掉的");
        }

        [Test]
        public void NoClear_QueueEmpty_PiecesClean()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);

            // 开局首手三块全干净（队列初始空）
            for (int i = 0; i < 3; i++)
                Assert.IsNull(s.OperaArr[i].Elements, "队空时候选块不应携带元素");
            // 不消除（不入队）→ 继续 BuildPiece 仍纯方块
            Assert.IsNull(s.BuildPiece(13).Elements, "队空 → 纯方块");
        }

        // ─── trio 级容量加权随机分摊 ────────────────────────────────

        [Test]
        public void DistributeElements_SpreadsAcrossMultiplePieces()
        {
            // 入队顶到 MaxPendingElements(12),trio 用 3 块 cellCount≥4 的块手工塞入
            // → 大概率(种子固定)至少 2 块拿到 ≥1 元素,杜绝「FIFO 全堆第 1 块」的旧行为回归。
            // 种子已在 SetUp 固定(20260612),分布稳定可复现。
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);
            var m = s.MergeState;

            m.EnqueueScoreElements(12);
            Assert.AreEqual(MergeOrderConfig.MaxPendingElements, m.PendingElements.Count, "队列顶到上限");

            // 手工三块都用 3x3 (shapeId=13, cellCount=9),totalCap=27 > 12 → 队列必被吃光
            s.OperaArr[0] = new PendingPiece(13, BlockColor.Red);
            s.OperaArr[1] = new PendingPiece(13, BlockColor.Blue);
            s.OperaArr[2] = new PendingPiece(13, BlockColor.Green);

            // 反射调私有 DistributePendingElementsAcrossTrio(IList<PendingPiece>)
            var mi = typeof(BlockGameState).GetMethod(
                "DistributePendingElementsAcrossTrio",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(mi, "私有方法签名应稳定");
            mi.Invoke(s, new object[] { s.OperaArr });

            int piecesWithElements = 0;
            int totalNonNone = 0;
            for (int i = 0; i < 3; i++)
            {
                if (s.OperaArr[i].Elements == null) continue;
                int nonNone = 0;
                for (int j = 0; j < s.OperaArr[i].Elements.Length; j++)
                    if (s.OperaArr[i].Elements[j] != MergeElement.None) nonNone++;
                if (nonNone > 0) piecesWithElements++;
                totalNonNone += nonNone;
            }
            Assert.AreEqual(12, totalNonNone, "12 元素全分摊出去(队列被吃光)");
            Assert.GreaterOrEqual(piecesWithElements, 2, "至少 2 块拿到元素(分布性,反 FIFO 集中第 1 块)");
            Assert.AreEqual(0, m.PendingElements.Count, "队列吃光");
        }

        [Test]
        public void DistributeElements_RespectsCapacityCeiling()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);
            var m = s.MergeState;

            // case A: totalCap > 队列 → 全分摊、队列剩 0、各块 Elements 总和 = 原队列
            m.EnqueueScoreElements(4);
            int originalQ = m.PendingElements.Count;
            s.OperaArr[0] = new PendingPiece(13, BlockColor.Red);   // 9 格
            s.OperaArr[1] = new PendingPiece(13, BlockColor.Blue);  // 9 格
            s.OperaArr[2] = new PendingPiece(13, BlockColor.Green); // 9 格,totalCap=27
            var mi = typeof(BlockGameState).GetMethod(
                "DistributePendingElementsAcrossTrio",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            mi.Invoke(s, new object[] { s.OperaArr });

            int placedA = CountNonNoneInTrio(s);
            Assert.AreEqual(originalQ, placedA, "totalCap>队列 → 全分摊");
            Assert.AreEqual(0, m.PendingElements.Count, "队列吃光");

            // case B: totalCap < 队列 → bucket 总和 = totalCap、队列剩 (队列 - totalCap)
            s.OperaArr[0] = null; s.OperaArr[1] = null; s.OperaArr[2] = null;
            m.PendingElements.Clear();
            m.EnqueueScoreElements(12); // 顶到 12
            int originalQ2 = m.PendingElements.Count;
            // 三块都 1x1 (shapeId=1, cellCount=1),totalCap=3
            s.OperaArr[0] = new PendingPiece(1, BlockColor.Red);
            s.OperaArr[1] = new PendingPiece(1, BlockColor.Blue);
            s.OperaArr[2] = new PendingPiece(1, BlockColor.Green);
            mi.Invoke(s, new object[] { s.OperaArr });

            int placedB = CountNonNoneInTrio(s);
            Assert.AreEqual(3, placedB, "totalCap<队列 → bucket 总和 = totalCap");
            Assert.AreEqual(originalQ2 - 3, m.PendingElements.Count, "队列剩 = 原队列 - totalCap");
        }

        [Test]
        public void DistributeElements_OffMode_NoAllocation()
        {
            // off 模式零回归:RefillPieces 后所有块 Elements == null
            var s = BlockGameState.Instance;
            Assert.IsFalse(s.MergeOrderMode, "默认 off");
            s.RefillPieces(new BinaryBoard());
            for (int i = 0; i < 3; i++)
            {
                Assert.IsNotNull(s.OperaArr[i], "off 模式仍补满 3 块");
                Assert.IsNull(s.OperaArr[i].Elements, "off 模式不分配 Elements");
            }
        }

        private static int CountNonNoneInTrio(BlockGameState s)
        {
            int n = 0;
            for (int i = 0; i < 3; i++)
            {
                if (s.OperaArr[i] == null || s.OperaArr[i].Elements == null) continue;
                for (int j = 0; j < s.OperaArr[i].Elements.Length; j++)
                    if (s.OperaArr[i].Elements[j] != MergeElement.None) n++;
            }
            return n;
        }

        [Test]
        public void EnqueueScoreElements_RotatesAcrossNeededTypes()
        {
            var m = new MergeOrderState();
            m.Reset();
            var needed = m.NeededTypes();
            Assert.GreaterOrEqual(needed.Count, 2, "初始双订单类型不同（Diamond/Star）");

            m.EnqueueScoreElements(4); // 轮转游标从 0 起 → 覆盖两类、不偏科
            var list = new List<MergeElement>(m.PendingElements);
            Assert.IsTrue(list.Contains(needed[0]) && list.Contains(needed[1]), "轮转应覆盖多个所需类型");
        }

        [Test]
        public void EnqueueScoreElements_CapsAtMaxPending()
        {
            var m = new MergeOrderState();
            m.Reset();
            m.EnqueueScoreElements(100); // 远超上限
            Assert.AreEqual(MergeOrderConfig.MaxPendingElements, m.PendingElements.Count, "积压封顶不超上限");
            m.EnqueueScoreElements(5);   // 已满，不再增长
            Assert.AreEqual(MergeOrderConfig.MaxPendingElements, m.PendingElements.Count);
        }

        // ───────────────────────── #13 悔棋（全量单步回滚） ─────────────────────────

        [Test]
        public void Undo_RestoresPendingElementsQueue()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);
            var m = s.MergeState;

            m.CaptureSnapshot(s, board);
            int before = m.PendingElements.Count; // 0
            m.EnqueueScoreElements(3);            // 模拟落子引发消除后的入队
            Assert.AreEqual(before + 3, m.PendingElements.Count);
            Assert.IsTrue(m.Undo(s, board));
            Assert.AreEqual(before, m.PendingElements.Count, "悔棋回滚预算队列");
        }

        [Test]
        public void Undo_RollsBackAllState_ReturnsPiece_RefundsEnergy()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);
            var m = s.MergeState;

            // 构造确定性手牌（2x2 实心，4 格全 Diamond）
            var piece = new PendingPiece(9, BlockColor.Blue)
            {
                Elements = new[]
                {
                    MergeElement.Diamond, MergeElement.Diamond,
                    MergeElement.Diamond, MergeElement.Diamond,
                }
            };
            s.OperaArr[0] = piece;
            int energyBefore = m.Energy;
            int chargesBefore = m.UndoCharges;

            // 落子前快照
            m.CaptureSnapshot(s, board);

            // 模拟窗口落子流程的全套副作用
            s.PlacePiece(0, board, 0, 0);
            m.SpendPlaceCost();
            m.IngestElement(MergeElement.Diamond);
            m.CompletedOrders = 2;
            m.TotalScore = 999;

            Assert.IsFalse(board.IsEmpty());
            Assert.IsNull(s.OperaArr[0]);
            Assert.AreEqual(energyBefore - 1, m.Energy);

            // 悔棋
            Assert.IsTrue(m.CanUndo);
            Assert.IsTrue(m.Undo(s, board));

            Assert.IsTrue(board.IsEmpty(), "棋盘回滚");
            Assert.AreEqual(MergeElement.None, s.ElementArr[0][0], "元素层回滚");
            Assert.AreEqual(-1, s.SaveArr[0][0], "SaveArr 回滚");
            Assert.IsNotNull(s.OperaArr[0], "方块退回待选槽");
            Assert.AreEqual(energyBefore, m.Energy, "退回该次扣的体力");
            Assert.AreEqual(0, m.InventoryCount(MergeElement.Diamond, 1), "合成区回滚");
            Assert.AreEqual(0, m.CompletedOrders, "订单进度回滚");
            Assert.AreEqual(0, m.TotalScore, "得分回滚");
            Assert.AreEqual(chargesBefore - 1, m.UndoCharges, "消耗一次悔棋次数");
        }

        [Test]
        public void Undo_ChargesExhausted_CannotUndo()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);
            var m = s.MergeState;
            m.UndoCharges = 0;

            m.CaptureSnapshot(s, board); // 次数 0 → 不压栈
            Assert.IsFalse(m.CanUndo);
            Assert.IsFalse(m.Undo(s, board));
        }

        [Test]
        public void Deliver_ClearsUndoStack()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);
            var m = s.MergeState;

            m.CaptureSnapshot(s, board);
            m.IngestElement(MergeElement.Diamond); // 满足初始订单0
            Assert.IsTrue(m.CanUndo);
            m.Deliver(0);
            Assert.IsFalse(m.CanUndo, "交付为已提交动作，清空悔棋栈");
        }

        // ───────────────────────── #14 demo 通关 ─────────────────────────

        [Test]
        public void IsDemoComplete_AtGoalOrders()
        {
            var m = new MergeOrderState();
            m.Reset();
            m.CompletedOrders = MergeOrderConfig.DemoGoalOrders - 1;
            Assert.IsFalse(m.IsDemoComplete());
            m.CompletedOrders = MergeOrderConfig.DemoGoalOrders;
            Assert.IsTrue(m.IsDemoComplete());
        }

    }
}
