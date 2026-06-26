using System.Collections.Generic;
using NUnit.Framework;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Core;
using GameLogic.BlockBlast.Algorithms;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 核心玩法补全（设计 11）新系统单测：连消/多消/全清结算、订单双轨、灵力祈愿、
    /// 智能生成 R1–R3 仲裁、女神进度、宝箱三选一。验收点对应设计 11 §十一。
    /// </summary>
    [TestFixture]
    public class CoreLoopCompletionTests
    {
        [SetUp]
        public void SetUp()
        {
            Persistence.Provider = new InMemoryPersistenceProvider();
            RandomSource.SetSeed(20260613);
        }

        private static MergeOrderState FreshState()
        {
            var m = new MergeOrderState();
            m.Reset();
            return m;
        }

        // ───────────── 设计 11 §5.2 多消元素产出（纯 N 函数）─────────────

        [Test]
        public void ElementsForLines_PerTier_MatchesDesignTable()
        {
            // 纯 N 函数总产出 (Lv1, Lv2, Lv3)：单消 1 初；双消 3 初；三消 1 初+1 中；
            // 四消 3 初+1 中；五消 2 初+2 中；6+ 仅 1 高。
            Assert.AreEqual((1, 0, 0), MergeOrderConfig.ElementsForLines(1));
            Assert.AreEqual((3, 0, 0), MergeOrderConfig.ElementsForLines(2));
            Assert.AreEqual((1, 1, 0), MergeOrderConfig.ElementsForLines(3));
            Assert.AreEqual((3, 1, 0), MergeOrderConfig.ElementsForLines(4));
            Assert.AreEqual((2, 2, 0), MergeOrderConfig.ElementsForLines(5));
            Assert.AreEqual((0, 0, 1), MergeOrderConfig.ElementsForLines(6));
            Assert.AreEqual((0, 0, 1), MergeOrderConfig.ElementsForLines(9));
        }

        [Test]
        public void MultiLabel_PerTier()
        {
            Assert.AreEqual("Good", MergeOrderConfig.MultiClearLabelFor(1));
            Assert.AreEqual("Great", MergeOrderConfig.MultiClearLabelFor(3));
            Assert.AreEqual("Excellent", MergeOrderConfig.MultiClearLabelFor(5));
            Assert.AreEqual("Excellent", MergeOrderConfig.MultiClearLabelFor(8), "6+ 封顶 Excellent");
        }

        // ───────────── 设计 11 §5.3 连消倍率 ─────────────

        [Test]
        public void ComboMult_Ramp_CapsAt2x()
        {
            Assert.AreEqual(1000, MergeOrderConfig.ComboMultPermilleFor(1));
            Assert.AreEqual(1200, MergeOrderConfig.ComboMultPermilleFor(2));
            Assert.AreEqual(1500, MergeOrderConfig.ComboMultPermilleFor(3));
            Assert.AreEqual(1800, MergeOrderConfig.ComboMultPermilleFor(4));
            Assert.AreEqual(2000, MergeOrderConfig.ComboMultPermilleFor(5));
            Assert.AreEqual(2000, MergeOrderConfig.ComboMultPermilleFor(9), "封顶 ×2.0");
        }

        // ───────────── 设计 11 §5.5 结算流水线 ─────────────

        [Test]
        public void Settle_NoClear_ResetsComboAndArmsAllClear()
        {
            var m = FreshState();
            m.ComboChain = 4;
            m.AllClearArmed = false;
            var r = ClearSettlement.Settle(m, 0, 0, false, MergeElement.Butterfly);
            Assert.AreEqual(0, r.Lines);
            Assert.AreEqual(1, m.ComboChain, "链断回 1");
            Assert.IsTrue(m.AllClearArmed, "无消除落子重新武装全清");
        }

        [Test]
        public void Settle_ComboMultOnlyAffectsDisplayScore_ElementsByLines()
        {
            var m = FreshState();
            // 让链长推到 5（×2.0）：先连续若干次消除
            m.ComboChain = 4; // 下一次 +1 = 5 封顶
            int clearedCells = 8;
            int lines = 1;
            int baseScore = BlockScoring.ClearScore(clearedCells, lines);
            int lv1Expected = MergeOrderConfig.ElementsForLines(lines).lv1; // 纯 N 函数，与得分/连消无关

            var r = ClearSettlement.Settle(m, lines, clearedCells, false, MergeElement.Butterfly);

            Assert.AreEqual(baseScore, r.BaseScore, "基础分不含连消");
            Assert.AreEqual(baseScore * 2000 / 1000, r.DisplayScore, "显示分 = 基础分 ×2.0");
            Assert.AreEqual(lv1Expected, r.BaseElementsK, "Lv1 产出按 N 表，不随得分/连消变");
            Assert.AreEqual(1, r.BaseElementsK, "单消（N=1）产 1 Lv1");
            // 元素入队数 = Lv1 表值（NeededTypes 非空）
            Assert.AreEqual(lv1Expected, m.PendingElements.Count);
        }

        [Test]
        public void Settle_TripleClear_DirectAddsLv2_SkipsMerge()
        {
            var m = FreshState();
            // 三消 → 里程碑 +1 Lv2 直发（跳过合成）
            ClearSettlement.Settle(m, 3, 20, false, MergeElement.Star);
            Assert.AreEqual(1, m.InventoryCount(MergeElement.Star, 2), "三消直发 1 个 Lv2");
        }

        [Test]
        public void Settle_AllClear_GivesLv3_AdvancesGoddess_ArmedConsumed()
        {
            var m = FreshState();
            Assert.IsTrue(m.AllClearArmed);
            var r = ClearSettlement.Settle(m, 4, 32, true, MergeElement.Chalice);
            Assert.IsTrue(r.AllClearRewarded);
            // 全清奖额外 1 Lv3（叠加 N=4 表产出 3 Lv1+1 Lv2，均不进 Lv3 计数；1 Lv2 不足 MergeCount 不级联）
            Assert.AreEqual(1, m.InventoryCount(MergeElement.Chalice, 3), "全清额外发 1 Lv3");
            Assert.AreEqual(1, m.GoddessRating, "全清推进女神 +1");
            Assert.IsFalse(m.AllClearArmed, "发奖后武装位清空");
        }

        [Test]
        public void Settle_ConsecutiveAllClear_SecondNotRewarded()
        {
            var m = FreshState();
            ClearSettlement.Settle(m, 1, 8, true, MergeElement.Butterfly);  // 第 1 次全清发奖
            int lv3After1 = m.InventoryCount(MergeElement.Butterfly, 3);
            int goddessAfter1 = m.GoddessRating;
            var r2 = ClearSettlement.Settle(m, 1, 8, true, MergeElement.Butterfly); // 第 2 次连续全清
            Assert.IsFalse(r2.AllClearRewarded, "连续第 2 次全清不发奖");
            Assert.AreEqual(lv3After1, m.InventoryCount(MergeElement.Butterfly, 3), "Lv3 不再增");
            Assert.AreEqual(goddessAfter1, m.GoddessRating, "女神不再推进");
        }

        [Test]
        public void Settle_AllClearRearmsAfterNonAllClearMove()
        {
            var m = FreshState();
            ClearSettlement.Settle(m, 1, 8, true, MergeElement.Butterfly);   // 全清，武装位清
            Assert.IsFalse(m.AllClearArmed);
            ClearSettlement.Settle(m, 1, 8, false, MergeElement.Butterfly);  // 非全清消除 → 重新武装
            Assert.IsTrue(m.AllClearArmed);
            var r = ClearSettlement.Settle(m, 1, 8, true, MergeElement.Butterfly); // 再全清 → 发奖
            Assert.IsTrue(r.AllClearRewarded);
        }

        // ───────────── 设计 11 §三 订单双轨（特殊轨） ─────────────

        [Test]
        public void SpecialTrack_EmptyByDefault()
        {
            var m = FreshState();
            Assert.IsFalse(m.SpecialTrack.HasOccupied, "demo 默认特殊轨空");
            Assert.AreEqual(0, m.SpecialTrack.WaitingCount);
        }

        [Test]
        public void SpecialTrack_RequestEmptySlot_Occupies()
        {
            var track = new SpecialOrderTrack();
            track.Reset();
            var express = new SpecialOrder(SpecialOrderKind.Express, new Order(MergeElement.Star, 3, 2), 300f);
            Assert.IsTrue(track.Request(express));
            Assert.IsTrue(track.HasOccupied);
            Assert.AreEqual(SpecialOrderKind.Express, track.Occupied.Kind);
        }

        [Test]
        public void SpecialTrack_OccupiedNotEvicted_HigherPriorityQueues()
        {
            var track = new SpecialOrderTrack();
            track.Reset();
            var express = new SpecialOrder(SpecialOrderKind.Express, new Order(MergeElement.Star, 3, 2), 300f);
            var story = new SpecialOrder(SpecialOrderKind.Story, new Order(MergeElement.Chalice, 3, 3));
            track.Request(express);
            // 剧情优先级更高，但加急已占槽 → 不踢出，剧情入等待队列
            Assert.IsFalse(track.Request(story));
            Assert.AreEqual(SpecialOrderKind.Express, track.Occupied.Kind, "已占槽不被中途踢出");
            Assert.AreEqual(1, track.WaitingCount);
        }

        [Test]
        public void SpecialTrack_OnDelivered_PromotesHighestPriorityFromQueue()
        {
            var track = new SpecialOrderTrack();
            track.Reset();
            track.Request(new SpecialOrder(SpecialOrderKind.Express, new Order(MergeElement.Star, 3, 2), 300f));
            // 队列同时积压黄金时段 + 剧情，剧情优先级最高
            track.Request(new SpecialOrder(SpecialOrderKind.GoldenHour, new Order(MergeElement.Butterfly, 1, 1)));
            track.Request(new SpecialOrder(SpecialOrderKind.Story, new Order(MergeElement.Chalice, 3, 3)));
            track.OnDelivered();
            Assert.AreEqual(SpecialOrderKind.Story, track.Occupied.Kind, "升起队列中优先级最高的剧情单");
            Assert.AreEqual(1, track.WaitingCount, "黄金时段仍在队列");
        }

        [Test]
        public void SpecialTrack_SamePriority_FIFO()
        {
            var track = new SpecialOrderTrack();
            track.Reset();
            track.Request(new SpecialOrder(SpecialOrderKind.Story, new Order(MergeElement.Chalice, 3, 2)));
            var storyA = new SpecialOrder(SpecialOrderKind.Express, new Order(MergeElement.Star, 3, 2), 100f);
            var storyB = new SpecialOrder(SpecialOrderKind.Express, new Order(MergeElement.Butterfly, 3, 2), 100f);
            track.Request(storyA);
            track.Request(storyB);
            track.OnDelivered();
            Assert.AreEqual(MergeElement.Star, track.Occupied.Req.Type, "同优先级取最早入队（FIFO）");
        }

        [Test]
        public void SpecialTrack_ExpressCountdown_ExpiresAndPromotes()
        {
            var track = new SpecialOrderTrack();
            track.Reset();
            track.Request(new SpecialOrder(SpecialOrderKind.Express, new Order(MergeElement.Star, 3, 2), 10f));
            track.Request(new SpecialOrder(SpecialOrderKind.GoldenHour, new Order(MergeElement.Butterfly, 1, 1)));
            Assert.IsFalse(track.TickCountdown(5f), "未到点");
            Assert.IsTrue(track.TickCountdown(10f), "到点过期");
            Assert.AreEqual(SpecialOrderKind.GoldenHour, track.Occupied.Kind, "过期后升起队列");
        }

        [Test]
        public void DeliverSpecial_ConsumesInventory_Rewards_Promotes()
        {
            var m = FreshState();
            int perCap = MergeOrderConfig.Pow(MergeOrderConfig.MergeCount, MergeOrderConfig.MaxLevel - 1);
            m.SpecialTrack.Request(new SpecialOrder(SpecialOrderKind.Express, new Order(MergeElement.Star, MergeOrderConfig.MaxLevel, 2), 300f));
            // 凑 2 个 Star 封顶图案：2 份封顶折算量 Lv1 → 2 个封顶图案（封顶可堆积）
            for (int i = 0; i < 2 * perCap; i++) m.IngestElement(MergeElement.Star);
            Assert.AreEqual(2, m.InventoryCount(MergeElement.Star, MergeOrderConfig.MaxLevel));
            Assert.IsTrue(m.CanDeliverSpecial());

            int energyBefore = m.Energy;
            int completedBefore = m.CompletedOrders;
            Assert.IsTrue(m.DeliverSpecial());
            Assert.AreEqual(0, m.InventoryCount(MergeElement.Star, MergeOrderConfig.MaxLevel), "扣库存");
            Assert.AreEqual(energyBefore + MergeOrderConfig.OrderRewardEnergy, m.Energy);
            Assert.AreEqual(completedBefore + 1, m.CompletedOrders);
            Assert.IsFalse(m.SpecialTrack.HasOccupied, "交付腾空特殊槽");
        }

        // ───────────── 设计 11 §四/§7.1 灵力 + 祈愿 ─────────────

        [Test]
        public void Soul_AddAndSpend_ViaWish()
        {
            var m = FreshState();
            Assert.AreEqual(0, m.Soul);
            m.AddSoul(50);
            Assert.AreEqual(50, m.Soul);

            m.Energy = 5;
            Assert.IsTrue(m.CanWishForEnergy());
            Assert.IsTrue(m.WishForEnergy());
            Assert.AreEqual(50 - MergeOrderConfig.WishSoulCost, m.Soul, "扣灵力");
            Assert.AreEqual(5 + MergeOrderConfig.WishEnergyGain, m.Energy, "补体力");
            Assert.AreEqual(1, m.WishUsedToday);
        }

        [Test]
        public void Wish_CappedToEnergyCap_NoOverflow()
        {
            var m = FreshState();
            m.AddSoul(100);
            m.Energy = MergeOrderConfig.EnergyCap - 2; // 28
            Assert.IsTrue(m.WishForEnergy());
            Assert.AreEqual(MergeOrderConfig.EnergyCap, m.Energy, "祈愿补体力封顶软上限，不溢出");
        }

        [Test]
        public void Wish_DailyLimitEnforced()
        {
            var m = FreshState();
            m.AddSoul(1000);
            for (int i = 0; i < MergeOrderConfig.WishPerDayLimit; i++)
                Assert.IsTrue(m.WishForEnergy(), $"第 {i + 1} 次祈愿应成功");
            Assert.IsFalse(m.CanWishForEnergy(), "超每日上限");
            Assert.IsFalse(m.WishForEnergy());
        }

        [Test]
        public void Wish_InsufficientSoul_Rejected()
        {
            var m = FreshState();
            m.AddSoul(MergeOrderConfig.WishSoulCost - 1);
            Assert.IsFalse(m.CanWishForEnergy());
            Assert.IsFalse(m.WishForEnergy());
        }

        [Test]
        public void HammerCost_IsEight()
        {
            Assert.AreEqual(8, MergeOrderConfig.HammerCost, "消除锤代价 = 8（设计 11 §7.2 拍板）");
        }

        // ───────────── 设计 11 §十 女神 ─────────────

        [Test]
        public void Goddess_TenAllClears_LevelsUpAndResets()
        {
            var m = FreshState();
            Assert.AreEqual(1, m.GoddessLevel);
            for (int i = 0; i < MergeOrderConfig.GoddessRatingGoal - 1; i++)
            {
                Assert.IsFalse(m.AdvanceGoddess(), "未满档不升级");
            }
            Assert.AreEqual(MergeOrderConfig.GoddessRatingGoal - 1, m.GoddessRating);
            Assert.IsTrue(m.AdvanceGoddess(), "第 10 次升档");
            Assert.AreEqual(0, m.GoddessRating, "升档后好评条清零");
            Assert.AreEqual(2, m.GoddessLevel, "好感等级 +1（只升不降）");
        }

        // ───────────── 设计 11 §八 智能生成 R1–R3 仲裁 ─────────────

        [Test]
        public void Arbiter_NoTrigger_FallsThroughToDynamicWeight()
        {
            var board = new BinaryBoard(); // 空盘
            var ctx = new HandGenContext();
            // 空盘、无连续未消除、无异形配额 → 无 P0/P1；空盘排除 P2；无近完成行排除 P3
            var d = HandGenerationArbiter.Decide(board, ctx);
            Assert.IsFalse(d.Override, "四规则都不触发 → 回落 dynamicWeight，现状行为零改变");
        }

        [Test]
        public void Arbiter_NoClearStreak_TriggersP0NoDie()
        {
            var board = new BinaryBoard();
            var ctx = new HandGenContext { NoClearStreak = MergeOrderConfig.NoClearThreshold };
            var d = HandGenerationArbiter.Decide(board, ctx);
            Assert.IsTrue(d.Override);
            Assert.AreEqual(AlgorithmKind.Fill, d.Algo, "防卡死走可消除算法");
            Assert.AreEqual("P0_NoDie", d.Rule);
        }

        [Test]
        public void Arbiter_P0_BeatsP1_WhenBothArmed()
        {
            var board = new BinaryBoard();
            // 同时满足防卡死 + 清盘后增难配额 → P0 优先（防卡死压倒增难）
            var ctx = new HandGenContext
            {
                NoClearStreak = MergeOrderConfig.NoClearThreshold,
                AntiStreak = MergeOrderConfig.AntiStreakCount,
            };
            var d = HandGenerationArbiter.Decide(board, ctx);
            Assert.AreEqual("P0_NoDie", d.Rule, "P0 防卡死优先于 P1 增难");
            Assert.AreEqual(MergeOrderConfig.AntiStreakCount, ctx.AntiStreak, "P0 优先时 antiStreak 此手不递减");
        }

        [Test]
        public void Arbiter_AntiStreak_TriggersP1AndDecrements()
        {
            var board = new BinaryBoard();
            var ctx = new HandGenContext { AntiStreak = 2 };
            var d = HandGenerationArbiter.Decide(board, ctx);
            Assert.AreEqual("P1_AntiStreak", d.Rule);
            Assert.AreEqual(AlgorithmKind.Diff, d.Algo, "增难走困难算法");
            Assert.AreEqual(1, ctx.AntiStreak, "消耗一个异形配额");
        }

        [Test]
        public void Arbiter_NearEmptyBoard_TriggersP2ClearGuide()
        {
            var board = new BinaryBoard();
            board.RowBinary[0] = 0b11000000; // 仅 2 格占用 ≤ ClearGuideThreshold(8) 且非空
            var ctx = new HandGenContext();
            var d = HandGenerationArbiter.Decide(board, ctx);
            Assert.AreEqual("P2_ClearGuide", d.Rule);
            Assert.AreEqual(AlgorithmKind.ClearAll, d.Algo);
        }

        [Test]
        public void Arbiter_OnPlaced_CountersAdvance()
        {
            var ctx = new HandGenContext();
            ctx.OnPlaced(0, false);
            ctx.OnPlaced(0, false);
            Assert.AreEqual(2, ctx.NoClearStreak, "无消除累加");
            ctx.OnPlaced(2, false);
            Assert.AreEqual(0, ctx.NoClearStreak, "有消除归零");
            ctx.OnPlaced(1, true);
            Assert.AreEqual(MergeOrderConfig.AntiStreakCount, ctx.AntiStreak, "全清武装异形配额");
        }

        // ───────────── 设计 11 §九 宝箱 ─────────────

        [Test]
        public void Chest_AcquireUntilFull_RejectsBeyondFour()
        {
            var chests = new ChestSystem();
            chests.Reset();
            for (int i = 0; i < ChestSystem.SlotCount; i++)
                Assert.IsTrue(chests.TryAcquire(ChestTier.Common), $"第 {i + 1} 个入位");
            Assert.IsTrue(chests.IsFull);
            Assert.IsFalse(chests.TryAcquire(ChestTier.Common), "满 4 时新宝箱不入位");
            Assert.AreEqual(ChestSystem.SlotCount, chests.OccupiedCount);
        }

        [Test]
        public void Chest_CountdownGatesOpen()
        {
            var chests = new ChestSystem();
            chests.Reset();
            chests.TryAcquire(ChestTier.Common); // 300s
            Assert.IsFalse(chests.CanOpen(0), "倒计时未到不可开");
            chests.Tick(300f);
            Assert.IsTrue(chests.CanOpen(0), "到点可开");
            Assert.IsNull(chests.Open(99), "无效箱位返回 null");
        }

        [Test]
        public void Chest_OpenThreePick_DistinctKinds()
        {
            var chests = new ChestSystem();
            chests.Reset();
            chests.TryAcquire(ChestTier.Epic);
            chests.Tick(ChestSystem.TierCountdownSec[(int)ChestTier.Epic]);
            var picks = chests.Open(0);
            Assert.IsNotNull(picks);
            Assert.AreEqual(ChestSystem.PickCount, picks.Count, "三选一抽 3 张");
            var kinds = new HashSet<ChestRewardKind>();
            foreach (var p in picks) Assert.IsTrue(kinds.Add(p.Kind), "三张奖励类型不重复");
        }

        [Test]
        public void Chest_ClaimFreesSlot()
        {
            var chests = new ChestSystem();
            chests.Reset();
            chests.TryAcquire(ChestTier.Common);
            chests.Tick(300f);
            Assert.AreEqual(1, chests.OccupiedCount);
            Assert.IsTrue(chests.Claim(0), "领取腾位");
            Assert.AreEqual(0, chests.OccupiedCount);
        }

    }
}
