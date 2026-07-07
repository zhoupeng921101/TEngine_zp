using System.Collections.Generic;
using NUnit.Framework;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Core;
using GameLogic.Config;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 合成+订单+体力 切片核心逻辑测试：体力 / 合成自动配对 / 订单交付刷新 /
    /// 需求拉动 + 得分驱动元素生成（映射·入队·轮转·封顶·抽干）/ off 回归。
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
            // DynamicWeightDiff 现为 BlockGameState 的逐局实例字段,随 BlockGameState 释放,无独立单例可释。
            if (BlockGameState.IsValid) BlockGameState.Instance.Release();
            // 服务端权威接线经 BlockGameState 类级静态 hook(GameApp 仅在 Play 装配)跨 EditMode 域存活:
            // Release 只清单例实例、清不掉静态字段,交互式 Editor 跑过 Play 后残留的 hook 会在 ResetForMergeOrder
            // 末尾被触发,把本应本地权威的单测活态误切服务端分支——置 ServerDeal(后续 RefillPieces 走服务端投影→
            // OperaArr 空→NRE)+ 置 ServerAuthoritativeOrders(Deliver 不本地发体力→奖励丢失)。清空静态 hook,
            // 使本地权威基线不受 Play 污染(纯逻辑单测本就不接服务端栈)。
            BlockGameState.OnMergeStateReady = null;
            BlockGameState.OnMergeStateClosed = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (BlockGameState.IsValid) BlockGameState.Instance.Release();
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
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                {
                    Assert.AreEqual(-1, s.SaveArr[r][c]);
                    Assert.AreEqual(MergeElement.None, s.ElementArr[r][c]);
                }

            // 弄脏后再次进入应回到初始态
            m.Energy = 3;
            m.CompletedOrders = 4;
            m.IngestElement(MergeElement.Butterfly);
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
            m.IngestElement(MergeElement.Butterfly); // 满足初始订单0：OrderPool[0] = Butterfly Lv1 ×1
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

            s.ElementArr[0][0] = MergeElement.Butterfly;
            s.ElementArr[0][1] = MergeElement.Butterfly;
            s.ElementArr[0][2] = MergeElement.Star;

            var outList = new List<MergeElement>();
            int gained = s.HarvestClearedElements(new[] { 0 }, new int[0], outList);

            Assert.AreEqual(3, gained);
            Assert.AreEqual(3, outList.Count);
            int butterflies = 0, stars = 0;
            foreach (var e in outList)
            {
                if (e == MergeElement.Butterfly) butterflies++;
                if (e == MergeElement.Star) stars++;
            }
            Assert.AreEqual(2, butterflies);
            Assert.AreEqual(1, stars);
            Assert.AreEqual(MergeElement.None, s.ElementArr[0][0], "overlay 清空");
        }

        [Test]
        public void HarvestClearedElements_RowColIntersection_CountedOnce()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);

            // 行 0 放 3 个 Butterfly，列 1 放 1 个 Star，其中 (0,1) 属行列交叉
            s.ElementArr[0][0] = MergeElement.Butterfly;
            s.ElementArr[0][1] = MergeElement.Butterfly;
            s.ElementArr[0][2] = MergeElement.Butterfly;
            s.ElementArr[3][1] = MergeElement.Star;

            int gained = s.HarvestClearedElements(new[] { 0 }, new[] { 1 });
            Assert.AreEqual(4, gained, "3 Butterfly + 1 Star，交叉格只计一次");
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
                    MergeElement.Butterfly,  // (0,0)
                    MergeElement.None,       // (0,1)
                    MergeElement.Star,       // (1,0)
                    MergeElement.Butterfly,  // (1,1)
                }
            };
            s.OperaArr[0] = piece;
            s.PlacePiece(0, board, 0, 0);

            Assert.AreEqual(MergeElement.Butterfly, s.ElementArr[0][0]);
            Assert.AreEqual(MergeElement.None, s.ElementArr[0][1]);
            Assert.AreEqual(MergeElement.Star, s.ElementArr[1][0]);
            Assert.AreEqual(MergeElement.Butterfly, s.ElementArr[1][1]);
        }

        [Test]
        public void OffMode_PlacePiece_DoesNotTouchElements()
        {
            var s = BlockGameState.Instance;
            s.SetFirstHand();
            var board = new BinaryBoard();
            // 手动给 piece 塞元素，但 off 模式应被忽略（ElementArr 仍 null）
            s.OperaArr[0].Elements = new[] { MergeElement.Butterfly, MergeElement.Butterfly, MergeElement.Butterfly, MergeElement.Butterfly };
            s.PlacePiece(0, board, 0, 0);
            Assert.IsNull(s.ElementArr, "off 模式不应分配/写入元素层");
        }

        // ───────────────────────── #7 合成区自动两两合并升级 ─────────────────────────

        [Test]
        public void Ingest_FourSame_MakesOneLv2()
        {
            var m = new MergeOrderState();
            m.Reset();
            // 4合1：满 MergeCount 个 Lv1 合成 1 个 Lv2。
            for (int i = 0; i < MergeOrderConfig.MergeCount; i++) m.IngestElement(MergeElement.Butterfly);
            Assert.AreEqual(0, m.InventoryCount(MergeElement.Butterfly, 1));
            Assert.AreEqual(1, m.InventoryCount(MergeElement.Butterfly, 2));
        }

        [Test]
        public void Ingest_SixteenSame_CascadesToOneLv3()
        {
            var m = new MergeOrderState();
            m.Reset();
            // 4合1 两级级联：MergeCount^2 个 Lv1 → MergeCount 个 Lv2 → 1 个 Lv3，低级清零。
            int lv1ForLv3 = MergeOrderConfig.Pow(MergeOrderConfig.MergeCount, 2);
            for (int i = 0; i < lv1ForLv3; i++) m.IngestElement(MergeElement.Star);
            Assert.AreEqual(0, m.InventoryCount(MergeElement.Star, 1));
            Assert.AreEqual(0, m.InventoryCount(MergeElement.Star, 2));
            Assert.AreEqual(1, m.InventoryCount(MergeElement.Star, 3));
        }

        [Test]
        public void Ingest_TwoCapWorth_MakesTwoMaxLevel_CapStacks()
        {
            var m = new MergeOrderState();
            m.Reset();
            // 封顶等级折算基础元素数 = MergeCount^(MaxLevel-1)；摄入 2 份即应堆积成 2 个封顶图案（封顶不再合并）。
            int perCap = MergeOrderConfig.Pow(MergeOrderConfig.MergeCount, MergeOrderConfig.MaxLevel - 1);
            for (int i = 0; i < 2 * perCap; i++) m.IngestElement(MergeElement.Chalice);
            Assert.AreEqual(2, m.InventoryCount(MergeElement.Chalice, MergeOrderConfig.MaxLevel),
                "封顶等级不再合并，堆积成 2");
        }

        [Test]
        public void Difficulty_IsCountTimesMergeCountPow()
        {
            // 折算因子 = MergeCount^(等级-1)；Difficulty = 数量 × 折算因子（纯函数，与 MaxLevel 无关）。
            Assert.AreEqual(7 * 1,   new Order(MergeElement.Star, 1, 7).Difficulty, "Lv1 折算 ×1");
            Assert.AreEqual(3 * 4,   new Order(MergeElement.Star, 2, 3).Difficulty, "Lv2 折算 ×4");
            Assert.AreEqual(2 * 16,  new Order(MergeElement.Star, 3, 2).Difficulty, "Lv3 折算 ×16");
            Assert.AreEqual(1 * 64,  new Order(MergeElement.Star, 4, 1).Difficulty, "Lv4 折算 ×64");
            Assert.AreEqual(1 * 256, new Order(MergeElement.Star, 5, 1).Difficulty, "Lv5 折算 ×256");
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
        public void Deliver_ConsumesInventory_GivesReward_EmptiesSlot()
        {
            var m = new MergeOrderState();
            m.Reset();
            // 初始订单1 = OrderPool[1] = Chalice Lv2 ×1 → 摄入 MergeCount 个 Chalice 得 1 Lv2
            Assert.IsFalse(m.CanDeliver(1), "库存不足按钮置灰");
            for (int i = 0; i < MergeOrderConfig.MergeCount; i++) m.IngestElement(MergeElement.Chalice);
            Assert.IsTrue(m.CanDeliver(1));

            int scoreBefore = m.TotalScore;
            int energyBefore = m.Energy;
            // 新模型：交付后该槽置空（不补单），其余槽不变。
            var slot0Before = m.ActiveOrders[0];

            Assert.IsTrue(m.Deliver(1));
            Assert.AreEqual(0, m.InventoryCount(MergeElement.Chalice, 2), "交付扣除合成物");
            Assert.AreEqual(energyBefore + MergeOrderConfig.OrderRewardEnergy, m.Energy);
            Assert.AreEqual(scoreBefore + 2 * 1 * MergeOrderConfig.OrderScoreFactor, m.TotalScore);
            Assert.AreEqual(1, m.CompletedOrders);
            Assert.IsFalse(m.ActiveOrders[1].IsValid, "交付后该槽置空（不补单）");
            Assert.AreEqual(slot0Before.Type, m.ActiveOrders[0].Type, "未交付的其余槽不变");
            Assert.AreEqual(slot0Before.Level, m.ActiveOrders[0].Level);
            Assert.AreEqual(slot0Before.Count, m.ActiveOrders[0].Count);
        }

        [Test]
        public void NextOrder_CyclesPool()
        {
            var m = new MergeOrderState();
            m.Reset(); // 取走前 ActiveOrders 张，游标 = ActiveOrders
            var pool = MergeOrderConfig.OrderPool;
            // 从当前游标位继续取到池尾，逐项应等于 pool[游标..len-1]（按池长取模匹配）。
            int cursor = MergeOrderConfig.ActiveOrders;
            for (int i = cursor; i < pool.Length; i++)
            {
                var o = m.NextOrder();
                Assert.AreEqual(pool[i % pool.Length].Type, o.Type);
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
        public void ElementsForLines_MapsPerTier()
        {
            // 纯 N 函数：N≤0 全 0；逐档 (Lv1,Lv2,Lv3)。
            Assert.AreEqual((0, 0, 0), MergeOrderConfig.ElementsForLines(0));
            Assert.AreEqual((0, 0, 0), MergeOrderConfig.ElementsForLines(-3));
            Assert.AreEqual((1, 0, 0), MergeOrderConfig.ElementsForLines(1));
            Assert.AreEqual((3, 0, 0), MergeOrderConfig.ElementsForLines(2));
            Assert.AreEqual((1, 1, 0), MergeOrderConfig.ElementsForLines(3));
            Assert.AreEqual((3, 1, 0), MergeOrderConfig.ElementsForLines(4));
            Assert.AreEqual((2, 2, 0), MergeOrderConfig.ElementsForLines(5));
            Assert.AreEqual((0, 0, 1), MergeOrderConfig.ElementsForLines(6), "≥6 封顶 1 Lv3");
            Assert.AreEqual((0, 0, 1), MergeOrderConfig.ElementsForLines(9), "≥6 仍 1 Lv3");
        }

        [Test]
        public void ElementsForLines_DecoupledFromScore()
        {
            // 元素产出只看 N，不看得分：同 N 不同得分，产出相同。
            Assert.AreEqual(MergeOrderConfig.ElementsForLines(1), MergeOrderConfig.ElementsForLines(1));
            // N 升档时 Lv1 不再单调随分递增（双消 3 个 > 三消 1 个），口径已从得分驱动改纯 N 表。
            Assert.AreEqual(3, MergeOrderConfig.ElementsForLines(2).lv1, "双消 3 Lv1");
            Assert.AreEqual(1, MergeOrderConfig.ElementsForLines(3).lv1, "三消仅 1 Lv1（+1 Lv2）");
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

        [Test]
        public void DistributeElements_ScattersWithinBlock_NotFrontPacked()
        {
            // 块内落格随机:分到某块的元素应能与空格随机穿插,而非恒定堆在前若干格。
            // 单块「填充格数 > 分到元素数」、多种子跑:前部堆叠恒使非空占 0..count-1(last==count-1),
            // 出现 last>count-1 即证有空格穿插(散布);跨多种子取证,免单一种子偶合前部堆叠的脆性。
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            var mi = typeof(BlockGameState).GetMethod(
                "DistributePendingElementsAcrossTrio",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(mi, "私有方法签名应稳定");

            bool sawGap = false;
            for (int seed = 1; seed <= 12 && !sawGap; seed++)
            {
                RandomSource.SetSeed(seed);
                s.ResetForMergeOrder(board);
                var m = s.MergeState;
                m.PendingElements.Clear();
                m.EnqueueScoreElements(3); // 3 个元素

                // 单块 3x3(shapeId=13, 9 格),3 个元素必全落此块 → 块内落格随机可观测
                var trio = new PendingPiece[] { new PendingPiece(13, BlockColor.Red) };
                mi.Invoke(s, new object[] { trio });

                var els = trio[0].Elements;
                Assert.IsNotNull(els, "命中块应挂上 Elements");
                int last = -1, count = 0;
                for (int k = 0; k < els.Length; k++)
                    if (els[k] != MergeElement.None) { last = k; count++; }
                Assert.AreEqual(3, count, "元素总数守恒(随机只改落格、不改数量)");
                if (last > count - 1) sawGap = true;
            }
            Assert.IsTrue(sawGap, "多种子内应至少一次元素与空格穿插(证块内落格随机、非前部堆叠)");
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

        // ───────────────────────── #14 无尽:无通关终点 ─────────────────────────
        // 无尽模型（设计 49）删通关:IsDemoComplete / DemoGoalOrders 已移除,订单无限刷新无胜利终点。
        // 原 IsDemoComplete_AtGoalOrders 用例随之删除（断言的概念已不存在）。

        // ═══════════════ 无尽模型 · 时基恢复（设计 49 §3.2 / B8/B9/B10）═══════════════

        // B8：时基恢复纯时间驱动 + 封顶软上限不溢出。注入 now（Unix 秒），不触发任何落子/消除。
        [Test]
        public void TimeRegen_PureTimeDriven_CapsAtSoftLimit()
        {
            var m = new MergeOrderState();
            m.Reset();                              // Energy=20, LastEnergyRegenTime=0
            const long t0 = 1_700_000_000L;
            m.ApplyTimeRegen(t0);                   // 首次:初始化记录时刻、本次不补
            Assert.AreEqual(MergeOrderConfig.EnergyStart, m.Energy, "首次不补恢复");
            Assert.AreEqual(t0, m.LastEnergyRegenTime, "首次以 now 初始化记录时刻");

            int interval = (int)MergeOrderConfig.RegenIntervalSec;
            // 推进 3 个 tick：体力 20 → 23（未达软上限 30，按速率恢复）。
            m.ApplyTimeRegen(t0 + interval * 3L);
            Assert.AreEqual(MergeOrderConfig.EnergyStart + 3 * MergeOrderConfig.RegenPerTick, m.Energy, "按速率恢复 3 tick");

            // 推进巨量时间：封顶软上限不溢出。
            m.ApplyTimeRegen(t0 + interval * 100000L);
            Assert.AreEqual(MergeOrderConfig.EnergyCap, m.Energy, "恢复封顶软上限不溢出");
        }

        // B9：离线恢复 = floor(N/interval) × perTick，夹软上限。构造「上次记录=T0，当前=T0+N 秒」。
        [Test]
        public void TimeRegen_Offline_FloorTicksClampedToCap()
        {
            int interval = (int)MergeOrderConfig.RegenIntervalSec;
            const long t0 = 1_700_000_000L;

            var m = new MergeOrderState();
            m.Reset();
            m.Energy = 5;
            m.LastEnergyRegenTime = t0;             // 已有记录（模拟存档读回）

            // N = interval*4 + 余秒：应补 4 tick（余秒不算）。
            long n = interval * 4L + (interval / 2);
            m.ApplyTimeRegen(t0 + n);
            int expected = System.Math.Min(MergeOrderConfig.EnergyCap, 5 + 4 * MergeOrderConfig.RegenPerTick);
            Assert.AreEqual(expected, m.Energy, "离线补 floor(N/interval) tick");
            // 余秒留到下次：记录时刻只推进整除掉的秒数。
            Assert.AreEqual(t0 + interval * 4L, m.LastEnergyRegenTime, "记录时刻只推进整除掉的秒数，余秒留存");
        }

        // B9 余秒累计：两次短间隔进入，余秒不被吞，跨两次凑满一个 tick 仍恢复。
        [Test]
        public void TimeRegen_RemainderSeconds_AccumulateAcrossCalls()
        {
            int interval = (int)MergeOrderConfig.RegenIntervalSec;
            const long t0 = 1_700_000_000L;

            var m = new MergeOrderState();
            m.Reset();
            m.Energy = 5;
            m.LastEnergyRegenTime = t0;

            // 第一次：interval-1 秒（不满 1 tick）→ 不恢复、记录不动。
            m.ApplyTimeRegen(t0 + interval - 1);
            Assert.AreEqual(5, m.Energy, "不满 1 tick 不恢复");
            Assert.AreEqual(t0, m.LastEnergyRegenTime, "不满 1 tick 记录不动（余秒留存）");

            // 第二次：再加 1 秒（累计满 interval）→ 恢复 1 tick。
            m.ApplyTimeRegen(t0 + interval);
            Assert.AreEqual(5 + MergeOrderConfig.RegenPerTick, m.Energy, "余秒累计满 1 tick 后恢复");
        }

        // B10：负时差兜底——当前 < 上次记录 → 恢复 0、不倒扣、不抛、不更新记录。
        [Test]
        public void TimeRegen_NegativeDelta_NoCreditNoThrow()
        {
            const long t0 = 1_700_000_000L;
            var m = new MergeOrderState();
            m.Reset();
            m.Energy = 5;
            m.LastEnergyRegenTime = t0;

            Assert.DoesNotThrow(() => m.ApplyTimeRegen(t0 - 100));
            Assert.AreEqual(5, m.Energy, "负时差不倒扣、不补");
            Assert.AreEqual(t0, m.LastEnergyRegenTime, "负时差不更新记录时刻（待时间走正再补）");
        }

        // 时基恢复不动「订单溢出软上限」的体力（体力本就 > 软上限则保持）。
        [Test]
        public void TimeRegen_AboveSoftLimit_LeftUntouched()
        {
            int interval = (int)MergeOrderConfig.RegenIntervalSec;
            const long t0 = 1_700_000_000L;
            var m = new MergeOrderState();
            m.Reset();
            m.Energy = MergeOrderConfig.EnergyCap + 8; // 订单奖励溢出
            m.LastEnergyRegenTime = t0;

            m.ApplyTimeRegen(t0 + interval * 5L);
            Assert.AreEqual(MergeOrderConfig.EnergyCap + 8, m.Energy, "体力 > 软上限：时基恢复不动溢出部分");
        }

        // ═══════════════ 无尽模型 · 消除道具 gate（设计 49 §3.1 / B4/B5/B7）═══════════════

        // B7：cost ≤ EnergyCap 配置层不变量（默认表值 5 ≤ 30）。
        [Test]
        public void ClearTool_CostNotExceedEnergyCap()
        {
            Assert.LessOrEqual(MergeOrderConfig.ClearToolCost, MergeOrderConfig.EnergyCap,
                "硬约束:消除道具 cost ≤ 体力软上限,否则封顶后仍用不起 → 脱困死结");
        }

        // B4：体力 ≥ cost 可用、< 置灰；用一次扣 cost。
        [Test]
        public void ClearTool_GateByEnergy_SpendCost()
        {
            var m = new MergeOrderState();
            m.Reset();

            m.Energy = MergeOrderConfig.ClearToolCost;
            Assert.IsTrue(m.CanUseClearTool, "体力 = cost 可用");
            Assert.IsTrue(m.SpendClearToolCost(), "扣 cost 成功");
            Assert.AreEqual(0, m.Energy, "扣 cost 后体力归零（cost==Energy）");

            m.Energy = MergeOrderConfig.ClearToolCost - 1;
            Assert.IsFalse(m.CanUseClearTool, "体力 < cost 不可用（置灰）");
            Assert.IsFalse(m.SpendClearToolCost(), "体力不足扣 cost 失败、不扣");
            Assert.AreEqual(MergeOrderConfig.ClearToolCost - 1, m.Energy, "失败不改体力");
        }

        // B5：无限可用——只 gate 体力，不消耗任何持有计数、不限次数（连续多次只要体力够）。
        [Test]
        public void ClearTool_UnlimitedUse_OnlyGatedByEnergy()
        {
            var m = new MergeOrderState();
            m.Reset();
            m.Energy = MergeOrderConfig.ClearToolCost * 3; // 够用 3 次

            int uses = 0;
            while (m.CanUseClearTool && m.SpendClearToolCost()) uses++;
            Assert.AreEqual(3, uses, "只 gate 体力:体力够几次就能用几次,无持有计数 / 次数上限");
            Assert.AreEqual(0, m.Energy, "三次扣完体力归零");
        }

        // ═══════════════ 无尽模型 · 消除道具清一行一列（设计 49 §3.1/§四 / B6/B13）═══════════════

        // B6：在构造的卡死棋盘上用一次 → 清一行一列后必有合法落点（朝可落前进）。
        [Test]
        public void ClearTool_RowCol_OpensLandingSpot()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);

            // 构造满盘（8×8 全占）= 极端卡死：任何方块都放不下。
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    s.SaveArr[r][c] = 0;
            board.ConvertFromArr(s.SaveArr);
            Assert.IsFalse(board.CanPut(1), "满盘:1×1 都放不下（卡死）");

            int cleared = s.ClearToolRowCol(board, 3, 4); // 清第 3 行 + 第 4 列
            Assert.AreEqual(8 + 8 - 1, cleared, "清一行(8)+一列(8)-交叉格(1)=15 格");
            Assert.IsTrue(board.CanPut(1), "清后必有合法落点（朝可落前进）");
        }

        // B13：清一行一列不清空全盘 → 不触发全清判定（全清奖不被白嫖）。
        [Test]
        public void ClearTool_RowCol_DoesNotClearWholeBoard()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);

            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    s.SaveArr[r][c] = 0;
            board.ConvertFromArr(s.SaveArr);

            s.ClearToolRowCol(board, 3, 4);
            Assert.IsFalse(board.IsEmpty(), "清一行一列后棋盘非空 → 不触发全清判定（B13 不白嫖全清奖）");
        }

        // 清一行一列同步清元素 overlay（merge-order 模式）+ BinaryBoard 对齐 SaveArr。
        [Test]
        public void ClearTool_RowCol_SyncsElementAndBinary()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);

            s.SaveArr[2][5] = 0;
            s.ElementArr[2][5] = MergeElement.Butterfly;
            s.SaveArr[6][5] = 0; // 同列另一格
            board.ConvertFromArr(s.SaveArr);

            int cleared = s.ClearToolRowCol(board, 2, 5);
            Assert.AreEqual(2, cleared, "清掉 (2,5) 与同列 (6,5) 共 2 占格");
            Assert.AreEqual(-1, s.SaveArr[2][5], "SaveArr 清空");
            Assert.AreEqual(MergeElement.None, s.ElementArr[2][5], "ElementArr overlay 同步清空");
            Assert.IsTrue(board.EmptyAt(5, 2), "BinaryBoard 同步对齐 SaveArr");
        }

        // 越界格不动状态、返回 0。
        [Test]
        public void ClearTool_RowCol_OutOfBounds_NoOp()
        {
            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);
            s.SaveArr[0][0] = 0;
            board.ConvertFromArr(s.SaveArr);

            Assert.AreEqual(0, s.ClearToolRowCol(board, -1, 0), "越界行返回 0");
            Assert.AreEqual(0, s.ClearToolRowCol(board, 0, 8), "越界列返回 0");
            Assert.AreEqual(0, s.SaveArr[0][0], "越界不动棋盘");
        }

        // ═══════════════ 无尽模型 · 体力进盘 + 离线补算往返（设计 14 §3.7 / B-持久化）═══════════════

        // 真实无尽档（lastEnergyRegenTime>0）：体力随档续存 + 离线补算（导入时按 now 补）。
        [Test]
        public void EnergyPersist_RealSave_RestoresAndAppliesOfflineRegen()
        {
            int interval = (int)MergeOrderConfig.RegenIntervalSec;
            const string today = "2026-06-22";
            const long t0 = 1_700_000_000L;

            var src = new MergeOrderState();
            src.Reset();
            src.Energy = 5;
            src.LastEnergyRegenTime = t0;     // 真实档:已有记录
            var dto = src.ExportMeta();
            Assert.AreEqual(5, dto.energy, "体力进盘");
            Assert.AreEqual(t0, dto.lastEnergyRegenTime, "记录时刻进盘");

            // 往返 → 导入到新 state（ImportMeta 信真实档体力）。
            string json = MergeMetaPersistence.Serialize(dto);
            var back = MergeMetaPersistence.Deserialize(json);
            var dst = new MergeOrderState();
            dst.Reset();
            dst.ImportMeta(back);
            Assert.AreEqual(5, dst.Energy, "真实档:体力续存（lastEnergyRegenTime>0 信存档值）");
            Assert.AreEqual(t0, dst.LastEnergyRegenTime, "记录时刻续存");

            // 进窗补算离线（now = T0 + 4 tick）：体力 5 → 9。
            dst.ApplyTimeRegen(t0 + interval * 4L);
            Assert.AreEqual(5 + 4 * MergeOrderConfig.RegenPerTick, dst.Energy, "导入后按 now 补离线恢复");
        }

        // 无记录档（lastEnergyRegenTime==0，旧档 / 缺字段）：体力夹回起始值（不信缺省 0）。
        [Test]
        public void EnergyPersist_NoRecord_ClampsToStartEnergy()
        {
            const string today = "2026-06-22";
            // 直接构造缺字段 DTO（energy=0, lastEnergyRegenTime=0）。
            var dto = new MergeMetaSave { version = 1, goddessLevel = 1 };
            var dst = new MergeOrderState();
            dst.Reset();
            dst.ImportMeta(dto);
            Assert.AreEqual(MergeOrderConfig.EnergyStart, dst.Energy, "无记录档:体力夹回起始值（不信缺省 0）");
            Assert.AreEqual(0, dst.LastEnergyRegenTime, "无记录档:记录时刻保持 0,进窗 ApplyTimeRegen 再初始化");
        }

        // 篡改负体力夹回 0（真实档）。
        [Test]
        public void EnergyPersist_NegativeTampered_ClampsToZero()
        {
            const string today = "2026-06-22";
            var dto = new MergeMetaSave { version = 1, goddessLevel = 1, energy = -50, lastEnergyRegenTime = 1_700_000_000L };
            var dst = new MergeOrderState();
            dst.Reset();
            dst.ImportMeta(dto);
            Assert.AreEqual(0, dst.Energy, "篡改负体力夹回 0");
        }

        // ═══════════════ global 配置接入（id 1/2/3/4/5 运行时读表）═══════════════
        // 用 GlobalConfigMgr.InitForTest 注入确定性表值，验证 MergeOrderConfig 各参数走配置而非硬编码；
        // TearDown 在每个用例后调 ResetForTest 隔离（见下方 [TearDown] 同名清理）。

        [Test]
        public void GlobalConfig_FourParams_ReadFromTable()
        {
            GlobalConfigMgr.InitForTest(new Dictionary<int, string>
            {
                { GlobalConfigMgr.OrderCount, "4" },
                { GlobalConfigMgr.OrderRefreshSeconds, "123" },
                { GlobalConfigMgr.EnergyRecoverSeconds, "77" },
                { GlobalConfigMgr.EnergyRecoverCap, "42" },
            });
            try
            {
                Assert.AreEqual(4, MergeOrderConfig.ActiveOrders, "订单数读 id=1");
                Assert.AreEqual(123, MergeOrderConfig.OrderRefreshIntervalSec, "订单刷新间隔读 id=2");
                Assert.AreEqual(77f, MergeOrderConfig.RegenIntervalSec, "体力恢复间隔读 id=3（bare int 向后兼容作间隔）");
                Assert.AreEqual(1, MergeOrderConfig.RegenPerTick, "体力恢复点数：bare int 旧格式回退 amount=1");
                Assert.AreEqual(42, MergeOrderConfig.EnergyCap, "体力上限读 id=4");
            }
            finally { GlobalConfigMgr.ResetForTest(); }
        }

        // id=3 新格式 "amount#interval"：amount=点数、interval=间隔秒，分别驱动 RegenPerTick / RegenIntervalSec。
        [Test]
        public void GlobalConfig_EnergyRecover_AmountIntervalFormat_Parsed()
        {
            GlobalConfigMgr.InitForTest(new Dictionary<int, string>
            {
                { GlobalConfigMgr.EnergyRecoverSeconds, "2#15" },
            });
            try
            {
                Assert.AreEqual(2, MergeOrderConfig.RegenPerTick, "点数段读 amount=2");
                Assert.AreEqual(15f, MergeOrderConfig.RegenIntervalSec, "间隔段读 interval=15");
            }
            finally { GlobalConfigMgr.ResetForTest(); }
        }

        // 新格式 amount>1 时 ApplyTimeRegen 按 amount 通用恢复（非写死 1）：每满 interval 回 amount 点。
        [Test]
        public void GlobalConfig_EnergyRecover_AmountDrivesRegenPerTick()
        {
            GlobalConfigMgr.InitForTest(new Dictionary<int, string>
            {
                { GlobalConfigMgr.EnergyRecoverSeconds, "3#10" }, // 每 10 秒回 3 点
                { GlobalConfigMgr.EnergyRecoverCap, "30" },
            });
            try
            {
                const long t0 = 1_700_000_000L;
                var m = new MergeOrderState();
                m.Reset();
                m.Energy = 5;
                m.LastEnergyRegenTime = t0;
                m.ApplyTimeRegen(t0 + 10 * 2L);          // 2 个间隔 → 回 2*3=6 点
                Assert.AreEqual(11, m.Energy, "每 tick 回 amount=3 点，2 tick 回 6（5→11）");
            }
            finally { GlobalConfigMgr.ResetForTest(); }
        }

        // 局部非法（仅一半坏）：坏的那半各自回退默认，好的那半仍取合法值，不抛。
        [Test]
        public void GlobalConfig_EnergyRecover_PartialMalformed_PerHalfFallback()
        {
            // "a#10"：点数段非法 → amount 回退 1；间隔段合法 → 10。
            GlobalConfigMgr.InitForTest(new Dictionary<int, string>
            {
                { GlobalConfigMgr.EnergyRecoverSeconds, "a#10" },
            });
            try
            {
                Assert.AreEqual(1, MergeOrderConfig.RegenPerTick, "点数段非法回退 amount=1");
                Assert.AreEqual(10f, MergeOrderConfig.RegenIntervalSec, "间隔段合法仍取 10");
            }
            finally { GlobalConfigMgr.ResetForTest(); }

            // "2#"：间隔段缺失 → interval 回退默认 360；点数段合法 → 2。
            GlobalConfigMgr.InitForTest(new Dictionary<int, string>
            {
                { GlobalConfigMgr.EnergyRecoverSeconds, "2#" },
            });
            try
            {
                Assert.AreEqual(2, MergeOrderConfig.RegenPerTick, "点数段合法仍取 2");
                Assert.AreEqual(360f, MergeOrderConfig.RegenIntervalSec, "间隔段缺失回退默认 360");
            }
            finally { GlobalConfigMgr.ResetForTest(); }
        }

        [Test]
        public void GlobalConfig_MissingKeysOrEmptyTable_FallBackToDefaultsNoThrow()
        {
            // 空表：所有键缺失 → 各便捷属性回退默认（与 global.xlsx 初值一致），不抛。
            GlobalConfigMgr.InitForTest(new Dictionary<int, string>());
            try
            {
                Assert.AreEqual(3, MergeOrderConfig.ActiveOrders, "缺键回退订单数默认 3");
                Assert.AreEqual(300, MergeOrderConfig.OrderRefreshIntervalSec, "缺键回退刷新间隔默认 300");
                Assert.AreEqual(360f, MergeOrderConfig.RegenIntervalSec, "缺键回退体力间隔默认 360");
                Assert.AreEqual(30, MergeOrderConfig.EnergyCap, "缺键回退体力上限默认 30");
                Assert.AreEqual(5, MergeOrderConfig.ClearToolCost, "缺键回退消除道具体力默认 5");
                Assert.DoesNotThrow(() =>
                {
                    var m = new MergeOrderState();
                    m.Reset();
                }, "空表下 Reset 不崩");
            }
            finally { GlobalConfigMgr.ResetForTest(); }
        }

        [Test]
        public void GlobalConfig_ActiveOrders_DrivesActiveOrderArrayLength()
        {
            GlobalConfigMgr.InitForTest(new Dictionary<int, string> { { GlobalConfigMgr.OrderCount, "3" } });
            try
            {
                var m = new MergeOrderState();
                m.Reset();
                Assert.AreEqual(3, m.ActiveOrders.Length, "激活订单数组长度 = 配置订单数");
                foreach (var o in m.ActiveOrders) Assert.IsTrue(o.IsValid, "每槽都有合法订单");
            }
            finally { GlobalConfigMgr.ResetForTest(); }
        }

        // id=5：消除道具体力消耗读表 → ClearToolCost 取注入值；gate/扣费按该值生效。
        [Test]
        public void GlobalConfig_ClearToolCost_ReadFromTable_DrivesGateAndSpend()
        {
            GlobalConfigMgr.InitForTest(new Dictionary<int, string>
            {
                { GlobalConfigMgr.ClearToolEnergyCost, "7" },
            });
            try
            {
                Assert.AreEqual(7, MergeOrderConfig.ClearToolCost, "消除道具体力消耗读 id=5");

                var m = new MergeOrderState();
                m.Reset();

                m.Energy = 6;
                Assert.IsFalse(m.CanUseClearTool, "体力 6 < cost 7 不可用（置灰）");

                m.Energy = 7;
                Assert.IsTrue(m.CanUseClearTool, "体力 7 = cost 7 可用");
                Assert.IsTrue(m.SpendClearToolCost(), "扣 cost 成功");
                Assert.AreEqual(0, m.Energy, "扣 7 后归零（cost==Energy）");

                m.Energy = 10;
                Assert.IsTrue(m.SpendClearToolCost(), "体力 10 ≥ cost 7 可扣");
                Assert.AreEqual(3, m.Energy, "扣对应表值 7：10-7=3");
            }
            finally { GlobalConfigMgr.ResetForTest(); }
        }

        // id=5 缺键：ClearToolCost 回退默认 5，gate/扣费按 5 生效，不崩。
        [Test]
        public void GlobalConfig_ClearToolCost_MissingKey_FallBackToDefault5()
        {
            GlobalConfigMgr.InitForTest(new Dictionary<int, string>());
            try
            {
                Assert.AreEqual(5, MergeOrderConfig.ClearToolCost, "缺键回退默认 5");

                var m = new MergeOrderState();
                m.Reset();

                m.Energy = 4;
                Assert.IsFalse(m.CanUseClearTool, "体力 4 < 默认 cost 5 不可用");

                m.Energy = 5;
                Assert.IsTrue(m.CanUseClearTool, "体力 5 = 默认 cost 5 可用");
                Assert.IsTrue(m.SpendClearToolCost(), "缺键下扣默认 cost 不崩");
                Assert.AreEqual(0, m.Energy, "扣默认 5 后归零");
            }
            finally { GlobalConfigMgr.ResetForTest(); }
        }

        // ═══════════════ 订单按时整批刷新（id=2，仿时基恢复）═══════════════

        // 首次无记录：以 now 初始化、本次不刷（不凭空刷一批），订单不变。
        [Test]
        public void OrderRefresh_FirstCall_InitsRecordNoRefresh()
        {
            GlobalConfigMgr.InitForTest(new Dictionary<int, string>
            {
                { GlobalConfigMgr.OrderCount, "3" },
                { GlobalConfigMgr.OrderRefreshSeconds, "300" },
            });
            try
            {
                const long t0 = 1_700_000_000L;
                var m = new MergeOrderState();
                m.Reset(); // LastOrderRefreshTime=0
                var before = (Order[])m.ActiveOrders.Clone();

                bool refreshed = m.ApplyOrderRefresh(t0);
                Assert.IsFalse(refreshed, "首次不刷");
                Assert.AreEqual(t0, m.LastOrderRefreshTime, "首次以 now 初始化记录时刻");
                for (int i = 0; i < before.Length; i++)
                    Assert.AreEqual(before[i].Type, m.ActiveOrders[i].Type, "首次订单不变");
            }
            finally { GlobalConfigMgr.ResetForTest(); }
        }

        // 到点（≥interval）整批替换；记录时刻对齐 now。
        [Test]
        public void OrderRefresh_AfterInterval_ReplacesWholeBatch()
        {
            GlobalConfigMgr.InitForTest(new Dictionary<int, string>
            {
                { GlobalConfigMgr.OrderCount, "3" },
                { GlobalConfigMgr.OrderRefreshSeconds, "300" },
            });
            try
            {
                const long t0 = 1_700_000_000L;
                var m = new MergeOrderState();
                m.Reset();
                m.LastOrderRefreshTime = t0; // 已有记录
                int cursorBefore = m.OrderCursor;

                bool refreshed = m.ApplyOrderRefresh(t0 + 300);
                Assert.IsTrue(refreshed, "到点整批刷新");
                Assert.AreEqual(t0 + 300, m.LastOrderRefreshTime, "记录时刻对齐 now");
                Assert.AreEqual(cursorBefore + m.ActiveOrders.Length, m.OrderCursor, "整批每槽各取一次 NextOrder（游标推进 = 订单数）");
            }
            finally { GlobalConfigMgr.ResetForTest(); }
        }

        // 未到点（<interval）：不刷、记录不动。
        [Test]
        public void OrderRefresh_BeforeInterval_NoRefresh()
        {
            GlobalConfigMgr.InitForTest(new Dictionary<int, string>
            {
                { GlobalConfigMgr.OrderCount, "3" },
                { GlobalConfigMgr.OrderRefreshSeconds, "300" },
            });
            try
            {
                const long t0 = 1_700_000_000L;
                var m = new MergeOrderState();
                m.Reset();
                m.LastOrderRefreshTime = t0;
                int cursorBefore = m.OrderCursor;

                Assert.IsFalse(m.ApplyOrderRefresh(t0 + 299), "未到一个间隔不刷");
                Assert.AreEqual(t0, m.LastOrderRefreshTime, "未到点记录不动");
                Assert.AreEqual(cursorBefore, m.OrderCursor, "未刷游标不动");
            }
            finally { GlobalConfigMgr.ResetForTest(); }
        }

        // 离线很久（多个间隔）：只刷一批、不堆叠；记录时刻对齐 now。
        [Test]
        public void OrderRefresh_LongOffline_RefreshesOnceNotStacked()
        {
            GlobalConfigMgr.InitForTest(new Dictionary<int, string>
            {
                { GlobalConfigMgr.OrderCount, "3" },
                { GlobalConfigMgr.OrderRefreshSeconds, "300" },
            });
            try
            {
                const long t0 = 1_700_000_000L;
                var m = new MergeOrderState();
                m.Reset();
                m.LastOrderRefreshTime = t0;
                int cursorBefore = m.OrderCursor;

                bool refreshed = m.ApplyOrderRefresh(t0 + 300L * 1000); // 离线 1000 个间隔
                Assert.IsTrue(refreshed, "离线很久也刷一次");
                Assert.AreEqual(cursorBefore + m.ActiveOrders.Length, m.OrderCursor, "只刷一批（游标只推进一批量，不刷 N 批）");
                Assert.AreEqual(t0 + 300L * 1000, m.LastOrderRefreshTime, "记录时刻对齐 now（基准重置）");
            }
            finally { GlobalConfigMgr.ResetForTest(); }
        }

        // 负时差（玩家回调时钟）：不刷、不抛、不更新记录。
        [Test]
        public void OrderRefresh_NegativeDelta_NoRefreshNoThrow()
        {
            GlobalConfigMgr.InitForTest(new Dictionary<int, string>
            {
                { GlobalConfigMgr.OrderRefreshSeconds, "300" },
            });
            try
            {
                const long t0 = 1_700_000_000L;
                var m = new MergeOrderState();
                m.Reset();
                m.LastOrderRefreshTime = t0;

                Assert.DoesNotThrow(() => m.ApplyOrderRefresh(t0 - 100));
                Assert.IsFalse(m.ApplyOrderRefresh(t0 - 100), "负时差不刷");
                Assert.AreEqual(t0, m.LastOrderRefreshTime, "负时差不更新记录时刻");
            }
            finally { GlobalConfigMgr.ResetForTest(); }
        }

        // 刷新记录时刻随局内态往返续存（Export/ImportIngame）。
        [Test]
        public void OrderRefresh_RecordTime_RoundTripsViaIngameSave()
        {
            GlobalConfigMgr.InitForTest(new Dictionary<int, string> { { GlobalConfigMgr.OrderCount, "3" } });
            try
            {
                const long t0 = 1_700_000_000L;
                var src = new MergeOrderState();
                src.Reset();
                src.LastOrderRefreshTime = t0;

                var dto = new MergeIngameSave();
                src.ExportIngame(dto);
                Assert.AreEqual(t0, dto.lastOrderRefreshTime, "刷新记录时刻进盘");

                var dst = new MergeOrderState();
                dst.Reset();
                dst.ImportIngame(dto);
                Assert.AreEqual(t0, dst.LastOrderRefreshTime, "刷新记录时刻续存");
            }
            finally { GlobalConfigMgr.ResetForTest(); }
        }

        // ═══════════════ 老存档订单数组长度不匹配的优雅降级 ═══════════════

        // 老档订单数组长度=5、当前配置=3：ImportIngame 走「保持 Reset 建好的订单」兜底，不崩、订单长度=新配置。
        [Test]
        public void OldSave_OrderArrayLengthMismatch_GracefulFallback()
        {
            GlobalConfigMgr.InitForTest(new Dictionary<int, string> { { GlobalConfigMgr.OrderCount, "3" } });
            try
            {
                // 构造老档：激活订单 5 张（旧 ActiveOrders 常量）。
                var dto = new MergeIngameSave
                {
                    ordType = new int[5],
                    ordLevel = new int[5],
                    ordCount = new int[5],
                };
                for (int i = 0; i < 5; i++)
                {
                    dto.ordType[i] = (int)MergeElement.Butterfly;
                    dto.ordLevel[i] = 1;
                    dto.ordCount[i] = 1;
                }

                var dst = new MergeOrderState();
                dst.Reset(); // 先建好 3 张缺省订单
                Assert.DoesNotThrow(() => dst.ImportIngame(dto), "老档长度不符 ImportIngame 不抛");
                Assert.AreEqual(3, dst.ActiveOrders.Length, "保持 Reset 建好的新配置长度（3）");
                foreach (var o in dst.ActiveOrders) Assert.IsTrue(o.IsValid, "兜底订单合法");
            }
            finally { GlobalConfigMgr.ResetForTest(); }
        }

        // ═══════════════ 不补单模型：交付置空 + 全空整批刷新 + 空槽存档 ═══════════════

        // 交付一单后该槽置空（不补单）、其余槽不变。
        [Test]
        public void Deliver_EmptiesOnlyThatSlot_OthersUnchanged()
        {
            var m = new MergeOrderState();
            m.Reset();
            // 初始订单0 = OrderPool[0] = Butterfly Lv1 ×1 → 摄入一个 Butterfly 即可交付。
            var slot1Before = m.ActiveOrders[1];
            m.IngestElement(MergeElement.Butterfly);
            Assert.IsTrue(m.CanDeliver(0));

            Assert.IsTrue(m.Deliver(0));
            Assert.IsFalse(m.ActiveOrders[0].IsValid, "交付后该槽置空");
            Assert.AreEqual(slot1Before.Type, m.ActiveOrders[1].Type, "其余槽不变");
            Assert.AreEqual(slot1Before.Level, m.ActiveOrders[1].Level);
            Assert.AreEqual(slot1Before.Count, m.ActiveOrders[1].Count);
        }

        // 全部交付后 TryRefreshIfAllDelivered(now)：返回 true、三槽全有效（新一批）、记录时刻=now。
        [Test]
        public void TryRefreshIfAllDelivered_WhenAllEmpty_RefreshesAndResetsTimer()
        {
            const long now = 1_700_000_000L;
            var m = new MergeOrderState();
            m.Reset();
            // 模拟三槽全部交付完：逐槽置空。
            for (int i = 0; i < m.ActiveOrders.Length; i++) m.ActiveOrders[i] = default;
            m.LastOrderRefreshTime = now - 12345; // 旧基准，验证被重置到 now

            bool refreshed = m.TryRefreshIfAllDelivered(now);
            Assert.IsTrue(refreshed, "全空 → 立即整批刷新");
            Assert.AreEqual(now, m.LastOrderRefreshTime, "记录时刻对齐 now（到时倒计时基准重置）");
            foreach (var o in m.ActiveOrders) Assert.IsTrue(o.IsValid, "刷出一整批有效新订单");
        }

        // 未全空（仍有有效订单）：TryRefreshIfAllDelivered 返回 false、不动订单与计时。
        [Test]
        public void TryRefreshIfAllDelivered_WhenNotAllEmpty_NoOp()
        {
            const long now = 1_700_000_000L;
            var m = new MergeOrderState();
            m.Reset();
            // 只清掉部分槽，留至少一个有效。
            m.ActiveOrders[0] = default; // slot0 空，slot1.. 仍有效
            m.LastOrderRefreshTime = now;
            var ordersBefore = (Order[])m.ActiveOrders.Clone();
            int cursorBefore = m.OrderCursor;

            bool refreshed = m.TryRefreshIfAllDelivered(now + 999);
            Assert.IsFalse(refreshed, "仍有有效订单 → 不刷");
            Assert.AreEqual(now, m.LastOrderRefreshTime, "未刷记录时刻不动");
            Assert.AreEqual(cursorBefore, m.OrderCursor, "未刷游标不动");
            for (int i = 0; i < ordersBefore.Length; i++)
            {
                Assert.AreEqual(ordersBefore[i].Type, m.ActiveOrders[i].Type, "未刷订单不动");
                Assert.AreEqual(ordersBefore[i].Level, m.ActiveOrders[i].Level);
                Assert.AreEqual(ordersBefore[i].Count, m.ActiveOrders[i].Count);
            }
        }

        // 空数组防御：TryRefreshIfAllDelivered 不抛、返回 false。
        [Test]
        public void TryRefreshIfAllDelivered_EmptyOrNullArray_NoThrowReturnsFalse()
        {
            var m = new MergeOrderState();
            m.ActiveOrders = null;
            Assert.DoesNotThrow(() => m.TryRefreshIfAllDelivered(1_700_000_000L));
            Assert.IsFalse(m.TryRefreshIfAllDelivered(1_700_000_000L), "null 数组返回 false");

            m.ActiveOrders = new Order[0];
            Assert.IsFalse(m.TryRefreshIfAllDelivered(1_700_000_000L), "空数组返回 false");
        }

        // 空槽如实存档往返：部分完成（某槽 default）→ Export→Import，空槽仍空、有效槽仍在、不被补单。
        [Test]
        public void EmptySlot_RoundTripsViaIngameSave_NotRefilled()
        {
            GlobalConfigMgr.InitForTest(new Dictionary<int, string> { { GlobalConfigMgr.OrderCount, "3" } });
            try
            {
                var src = new MergeOrderState();
                src.Reset();
                // 构造部分完成态 [有效][空][有效]：置中间槽为空。
                src.ActiveOrders[1] = default;
                var slot0 = src.ActiveOrders[0];
                var slot2 = src.ActiveOrders[2];
                Assert.IsTrue(slot0.IsValid && slot2.IsValid, "前置：两端槽有效");

                var dto = new MergeIngameSave();
                src.ExportIngame(dto);
                Assert.AreEqual(0, dto.ordType[1], "空槽导出为 None(0)");
                Assert.AreEqual(0, dto.ordLevel[1]);
                Assert.AreEqual(0, dto.ordCount[1]);

                var dst = new MergeOrderState();
                dst.Reset();
                dst.ImportIngame(dto);
                Assert.IsFalse(dst.ActiveOrders[1].IsValid, "空槽续存后仍空（不被兜底补单）");
                Assert.AreEqual(slot0.Type, dst.ActiveOrders[0].Type, "有效槽0续存");
                Assert.AreEqual(slot0.Level, dst.ActiveOrders[0].Level);
                Assert.AreEqual(slot0.Count, dst.ActiveOrders[0].Count);
                Assert.AreEqual(slot2.Type, dst.ActiveOrders[2].Type, "有效槽2续存");
                Assert.AreEqual(slot2.Level, dst.ActiveOrders[2].Level);
                Assert.AreEqual(slot2.Count, dst.ActiveOrders[2].Count);
            }
            finally { GlobalConfigMgr.ResetForTest(); }
        }

    }
}
