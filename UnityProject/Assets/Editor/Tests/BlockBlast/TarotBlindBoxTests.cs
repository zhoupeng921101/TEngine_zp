using System.Collections.Generic;
using NUnit.Framework;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 神秘塔罗盲盒（设计 12）单测：奖池加权抽样确定性、双重保底、开盒扣计数+发放、
    /// 连消/全清解锁阈值、无消除不发、特殊订单附赠。验收点对应设计 12 §六 A1–A10。
    /// SetUp 仿 CoreLoopCompletionTests：InMemory Provider + RandomSource.SetSeed。
    /// </summary>
    [TestFixture]
    public class TarotBlindBoxTests
    {
        [SetUp]
        public void SetUp()
        {
            Persistence.Provider = new InMemoryPersistenceProvider();
            RandomSource.SetSeed(20260614);
        }

        private static MergeOrderState FreshState()
        {
            var m = new MergeOrderState();
            m.Reset();
            return m;
        }

        // 清空所有激活订单（构造「无订单缺口」场景：NeededHigh 应降级 PatternHigh）。
        private static void ClearAllOrders(MergeOrderState m)
        {
            if (m.ActiveOrders == null) return;
            for (int i = 0; i < m.ActiveOrders.Length; i++)
                m.ActiveOrders[i] = default; // 无效订单（IsValid=false）→ NeededTypes 空、无缺口
        }

        // ───────────── A1 奖池加权抽样确定性 ─────────────

        [Test]
        public void A1_RollReward_DeterministicSequence_AndFrequencyMatchesWeights()
        {
            var m = FreshState();

            // 固定种子 → 两次连掷的 Kind 序列必须逐项一致（可复现）。
            RandomSource.SetSeed(12345);
            var seqA = new List<BlindBoxRewardKind>();
            for (int i = 0; i < 50; i++) seqA.Add(TarotBlindBoxConfig.RollReward(m).Kind);

            RandomSource.SetSeed(12345);
            var seqB = new List<BlindBoxRewardKind>();
            for (int i = 0; i < 50; i++) seqB.Add(TarotBlindBoxConfig.RollReward(m).Kind);

            CollectionAssert.AreEqual(seqA, seqB, "同种子掷出序列可复现");

            // 大样本频次与权重比大致吻合。统计「实际掷出的 Kind」——
            // FreshState 有订单缺口，NeededHigh 不降级，故五类都可能出现。
            RandomSource.SetSeed(777);
            var counts = new Dictionary<BlindBoxRewardKind, int>();
            const int trials = 20000;
            for (int i = 0; i < trials; i++)
            {
                var k = TarotBlindBoxConfig.RollReward(m).Kind;
                counts.TryGetValue(k, out int c);
                counts[k] = c + 1;
            }

            // Low 权重 40 > Mid 22 > Energy 20 > High 10 > NeededHigh 8。
            // 用宽松区间核对（容差 ±25% 相对，避免随机抖动 flaky）。
            AssertFreqApprox(counts, BlindBoxRewardKind.PatternLow, 40, trials);
            AssertFreqApprox(counts, BlindBoxRewardKind.PatternMid, 22, trials);
            AssertFreqApprox(counts, BlindBoxRewardKind.Energy, 20, trials);
            AssertFreqApprox(counts, BlindBoxRewardKind.PatternHigh, 10, trials);
            AssertFreqApprox(counts, BlindBoxRewardKind.NeededHigh, 8, trials);
        }

        private static void AssertFreqApprox(Dictionary<BlindBoxRewardKind, int> counts,
            BlindBoxRewardKind kind, int weight, int trials)
        {
            counts.TryGetValue(kind, out int c);
            double expected = trials * weight / 100.0;
            double ratio = c / expected;
            Assert.IsTrue(ratio > 0.75 && ratio < 1.25,
                $"{kind} 频次 {c} 与期望 {expected:F0}(权重{weight}) 偏差过大 ratio={ratio:F2}");
        }

        // ───────────── A2 下限保底（无空奖） ─────────────

        [Test]
        public void A2_AnySeed_AlwaysValidReward_NoEmpty()
        {
            var m = FreshState();
            for (int seed = 0; seed < 200; seed++)
            {
                RandomSource.SetSeed(seed);
                var r = TarotBlindBoxConfig.RollReward(m);
                // 必为「有效图案」或「有效体力」，二者必居其一。
                bool valid = (r.IsPattern && r.PatternLevel >= 1 && r.PatternCount >= 1)
                             || (r.IsEnergy && r.EnergyGain >= 1);
                Assert.IsTrue(valid, $"seed={seed} 掷出空奖 kind={r.Kind} lvl={r.PatternLevel} eng={r.EnergyGain}");
            }
        }

        // ───────────── A3 NeededHigh 降级保底 ─────────────

        [Test]
        public void A3_NeededHigh_NoGap_DowngradesToPatternHigh()
        {
            var m = FreshState();
            ClearAllOrders(m); // 无任何订单缺口
            Assert.AreEqual(0, m.NeededTypes().Count, "前置：无所需类型");

            // 直接验证 Materialize 路径：扫所有 seed，凡掷出 NeededHigh 的应已降级为 PatternHigh(封顶 Lv MaxLevel)。
            // 无缺口下不应出现 Kind==NeededHigh 的最终结果。
            for (int seed = 0; seed < 300; seed++)
            {
                RandomSource.SetSeed(seed);
                var r = TarotBlindBoxConfig.RollReward(m);
                Assert.AreNotEqual(BlindBoxRewardKind.NeededHigh, r.Kind,
                    $"seed={seed} 无缺口仍返回 NeededHigh（应降级）");
                if (r.IsPattern)
                    Assert.LessOrEqual(r.PatternLevel, MergeOrderConfig.MaxLevel, "图案等级不超封顶");
            }
        }

        [Test]
        public void A3_NeededHigh_WithGap_GivesHighestGapLevel()
        {
            var m = FreshState();
            ClearAllOrders(m);
            // 构造缺口：Star Lv3 ×1（最高等级缺口）+ Butterfly Lv1 ×1。库存空 → 均为缺口。
            m.ActiveOrders[0] = new Order(MergeElement.Star, 3, 1);
            m.ActiveOrders[1] = new Order(MergeElement.Butterfly, 1, 1);

            // 找一个掷出 NeededHigh 的 seed，断言落到最高缺口 Star Lv3。
            bool sawNeededHigh = false;
            for (int seed = 0; seed < 500 && !sawNeededHigh; seed++)
            {
                RandomSource.SetSeed(seed);
                var r = TarotBlindBoxConfig.RollReward(m);
                if (r.Kind == BlindBoxRewardKind.NeededHigh)
                {
                    sawNeededHigh = true;
                    Assert.AreEqual(MergeElement.Star, r.PatternType, "NeededHigh 取最高缺口类型 Star");
                    Assert.AreEqual(3, r.PatternLevel, "NeededHigh 取最高缺口等级 Lv3");
                }
            }
            Assert.IsTrue(sawNeededHigh, "500 seed 内应至少掷出一次 NeededHigh（权重 8%）");
        }

        // ───────────── A4 开盒扣计数 + 发放 ─────────────

        [Test]
        public void A4_OpenBlindBox_DecrementsCount_AndGrants()
        {
            var m = FreshState();
            m.BlindBoxCount = 2;

            Assert.IsTrue(m.CanOpenBlindBox);
            int invBefore = TotalInventory(m);
            int energyBefore = m.Energy;

            Assert.IsTrue(m.OpenBlindBox(out var reward));
            Assert.AreEqual(1, m.BlindBoxCount, "开盒扣 1");

            if (reward.IsPattern)
            {
                // 图案项使收集区净折算元素数 +reward 折算量（级联合并不丢量）。
                int invAfter = TotalInventory(m);
                Assert.Greater(invAfter, invBefore, "图案进收集区，折算库存增加");
            }
            else if (reward.IsEnergy)
            {
                Assert.AreEqual(System.Math.Min(MergeOrderConfig.EnergyCap, energyBefore + reward.EnergyGain),
                    m.Energy, "体力项增（不超 EnergyCap）");
            }
        }

        [Test]
        public void A4_OpenBlindBox_ZeroCount_ReturnsFalse_NoChange()
        {
            var m = FreshState();
            m.BlindBoxCount = 0;
            Assert.IsFalse(m.CanOpenBlindBox);
            int energyBefore = m.Energy;
            int invBefore = TotalInventory(m);

            Assert.IsFalse(m.OpenBlindBox(out _), "计数 0 开盒返回 false");
            Assert.AreEqual(0, m.BlindBoxCount, "计数不变");
            Assert.AreEqual(energyBefore, m.Energy, "体力不变");
            Assert.AreEqual(invBefore, TotalInventory(m), "库存不变");
        }

        [Test]
        public void A4_OpenBlindBox_EnergyReward_NoOverflowCap()
        {
            var m = FreshState();
            m.BlindBoxCount = 1;
            m.Energy = MergeOrderConfig.EnergyCap; // 已满
            ClearAllOrders(m); // 无缺口降级，但与体力无关；此处只验体力封顶

            // 找一个掷出 Energy 的 seed。
            for (int seed = 0; seed < 500; seed++)
            {
                RandomSource.SetSeed(seed);
                if (TarotBlindBoxConfig.RollReward(m).Kind == BlindBoxRewardKind.Energy)
                {
                    var m2 = FreshState();
                    m2.BlindBoxCount = 1;
                    m2.Energy = MergeOrderConfig.EnergyCap;
                    RandomSource.SetSeed(seed);
                    Assert.IsTrue(m2.OpenBlindBox(out var r));
                    Assert.AreEqual(BlindBoxRewardKind.Energy, r.Kind);
                    Assert.AreEqual(MergeOrderConfig.EnergyCap, m2.Energy, "体力满时开出体力不溢出");
                    return;
                }
            }
            Assert.Fail("500 seed 内未掷出 Energy（权重 20%，不应发生）");
        }

        // ───────────── A5 全清解锁盲盒 ─────────────

        [Test]
        public void A5_AllClear_GrantsOneBlindBox()
        {
            var m = FreshState();
            Assert.AreEqual(0, m.BlindBoxCount);
            Assert.IsTrue(m.AllClearArmed);

            var r = ClearSettlement.Settle(m, 4, 32, true, MergeElement.Chalice);
            Assert.IsTrue(r.AllClearRewarded);
            Assert.AreEqual(1, m.BlindBoxCount, "全清发 1 盲盒");
            Assert.AreEqual(1, r.BlindBoxGained, "结算结果暴露本手获得 1");
        }

        [Test]
        public void A5_ConsecutiveAllClear_SecondGrantsNone()
        {
            var m = FreshState();
            ClearSettlement.Settle(m, 1, 8, true, MergeElement.Butterfly);   // 第 1 次全清 → +1
            Assert.AreEqual(1, m.BlindBoxCount);
            var r2 = ClearSettlement.Settle(m, 1, 8, true, MergeElement.Butterfly); // 第 2 次连续全清（武装位已消）
            Assert.IsFalse(r2.AllClearRewarded, "连续第 2 次全清不发奖");
            Assert.AreEqual(0, r2.BlindBoxGained, "连续第 2 次不发盲盒");
            Assert.AreEqual(1, m.BlindBoxCount, "计数不再增");
        }

        // ───────────── A6 连消阈值解锁（逐档代入 §3.4） ─────────────

        [Test]
        public void A6_ComboThreshold_GrantsOnCrossingHand_Only()
        {
            // 序列「连续 6 手都消除」：各手 ComboChain = 2,3,4,5,6,7；仅 ComboChain 跨到 4 的那手 +1。
            var m = FreshState();
            int[] expectedChain = { 2, 3, 4, 5, 6, 7 };
            int totalGained = 0;
            for (int hand = 0; hand < expectedChain.Length; hand++)
            {
                var r = ClearSettlement.Settle(m, 1, 8, false, MergeElement.Butterfly);
                Assert.AreEqual(expectedChain[hand], m.ComboChain, $"第 {hand + 1} 手链值");
                totalGained += r.BlindBoxGained;
                // 仅链值 == 阈值 4 那手发盲盒
                Assert.AreEqual(m.ComboChain == TarotBlindBoxConfig.BoxComboThreshold ? 1 : 0, r.BlindBoxGained,
                    $"第 {hand + 1} 手(链{m.ComboChain})盲盒产出");
            }
            Assert.AreEqual(1, totalGained, "连续 6 手仅发 1 个盲盒");
            Assert.AreEqual(1, m.BlindBoxCount);
        }

        [Test]
        public void A6_ComboBreak_ThenReachThreshold_GrantsAgain()
        {
            // 「3 连消后断链，再 4 连消」：2,3,4(发) | 断 | 2,3,4(发) → 共 +2。
            var m = FreshState();
            ClearSettlement.Settle(m, 1, 8, false, MergeElement.Butterfly); // chain 2
            ClearSettlement.Settle(m, 1, 8, false, MergeElement.Butterfly); // chain 3
            var a = ClearSettlement.Settle(m, 1, 8, false, MergeElement.Butterfly); // chain 4 → +1
            Assert.AreEqual(1, a.BlindBoxGained);

            ClearSettlement.Settle(m, 0, 0, false, MergeElement.None);    // 断链 → chain 回 1
            Assert.AreEqual(1, m.ComboChain);

            ClearSettlement.Settle(m, 1, 8, false, MergeElement.Butterfly); // chain 2
            ClearSettlement.Settle(m, 1, 8, false, MergeElement.Butterfly); // chain 3
            var b = ClearSettlement.Settle(m, 1, 8, false, MergeElement.Butterfly); // chain 4 → +1
            Assert.AreEqual(1, b.BlindBoxGained, "链断后再达阈值重新发");

            Assert.AreEqual(2, m.BlindBoxCount, "共 +2");
        }

        [Test]
        public void A6_NoComboChain_NeverReachesThreshold()
        {
            // 「全程无连消（每手孤立消除）」：每手消除后被无消除落子打断 → 链长封顶 2，不达 4。
            var m = FreshState();
            for (int i = 0; i < 6; i++)
            {
                var r = ClearSettlement.Settle(m, 1, 8, false, MergeElement.Butterfly); // chain → 2
                Assert.AreEqual(0, r.BlindBoxGained, "链长 2 不达阈值");
                ClearSettlement.Settle(m, 0, 0, false, MergeElement.None);            // 断链回 1
            }
            Assert.AreEqual(0, m.BlindBoxCount, "孤立消除永不发盲盒");
        }

        // ───────────── A7 无消除不发 ─────────────

        [Test]
        public void A7_NoClear_GrantsNoBlindBox()
        {
            var m = FreshState();
            int before = m.BlindBoxCount;
            var r = ClearSettlement.Settle(m, 0, 0, false, MergeElement.None);
            Assert.AreEqual(0, r.BlindBoxGained);
            Assert.AreEqual(before, m.BlindBoxCount, "无消除不增盲盒");
        }

        // ───────────── A8 特殊订单附赠各 Kind ─────────────

        [Test]
        public void A8_DeliverSpecial_Express_GrantsBoxPerExpress()
            => AssertSpecialGrant(SpecialOrderKind.Express, TarotBlindBoxConfig.BoxPerExpress);

        [Test]
        public void A8_DeliverSpecial_Story_GrantsBoxPerStory()
            => AssertSpecialGrant(SpecialOrderKind.Story, TarotBlindBoxConfig.BoxPerStory);

        [Test]
        public void A8_DeliverSpecial_Golden_GrantsBoxPerGolden()
            => AssertSpecialGrant(SpecialOrderKind.GoldenHour, TarotBlindBoxConfig.BoxPerGolden);

        private static void AssertSpecialGrant(SpecialOrderKind kind, int expectedBoxes)
        {
            var m = FreshState();
            // 用 Star 封顶等级 ×2（封顶等级可堆积，N≥2 可达）；凑 2 份封顶折算量 → 2 个封顶图案。
            int perCap = MergeOrderConfig.Pow(MergeOrderConfig.MergeCount, MergeOrderConfig.MaxLevel - 1);
            m.SpecialTrack.Request(new SpecialOrder(kind, new Order(MergeElement.Star, MergeOrderConfig.MaxLevel, 2), 300f));
            for (int i = 0; i < 2 * perCap; i++) m.IngestElement(MergeElement.Star);
            Assert.IsTrue(m.CanDeliverSpecial());

            int boxBefore = m.BlindBoxCount;
            Assert.IsTrue(m.DeliverSpecial());
            Assert.AreEqual(boxBefore + expectedBoxes, m.BlindBoxCount, $"{kind} 交付附赠 {expectedBoxes} 盲盒");
        }

        [Test]
        public void A8_DeliverSpecial_NotDeliverable_ReturnsFalse_NoChange()
        {
            var m = FreshState();
            m.SpecialTrack.Request(new SpecialOrder(SpecialOrderKind.Story, new Order(MergeElement.Chalice, 3, 3), -1f));
            // 库存不足 → 不可交付
            Assert.IsFalse(m.CanDeliverSpecial());
            int boxBefore = m.BlindBoxCount;
            Assert.IsFalse(m.DeliverSpecial());
            Assert.AreEqual(boxBefore, m.BlindBoxCount, "不可交付时计数不变");
        }

        // ───────────── A10 旧路径零回归 ─────────────

        [Test]
        public void A10_NoTrigger_SettlementUnchanged()
        {
            // 无盲盒触发的结算（链长未达阈值、非全清）：BlindBoxGained==0 且原有结算字段不受影响。
            var m = FreshState();
            // 三消但非全清、链长 2（未达阈值 4）
            var r = ClearSettlement.Settle(m, 3, 20, false, MergeElement.Star);
            Assert.AreEqual(0, r.BlindBoxGained, "未触发不发盲盒");
            Assert.AreEqual(0, m.BlindBoxCount);
            // 原有里程碑产出不变：三消直发 1 Lv2
            Assert.AreEqual(1, m.InventoryCount(MergeElement.Star, 2), "三消里程碑产出不受盲盒钩子影响");
        }

        [Test]
        public void A10_FreshState_BlindBoxCountZero()
        {
            var m = FreshState();
            Assert.AreEqual(0, m.BlindBoxCount, "Reset 后盲盒计数为 0");
            Assert.IsFalse(m.CanOpenBlindBox);
        }

        // 收集区折算基础元素总量（用于「图案进区库存增加」的不依赖级联细节的断言）。
        private static int TotalInventory(MergeOrderState m)
        {
            int sum = 0;
            foreach (var kv in m.Inventory)
                sum += kv.Value * MergeOrderConfig.Pow(MergeOrderConfig.MergeCount, kv.Key.level - 1); // 折算因子 Lv1=1 Lv2=4 Lv3=16
            return sum;
        }
    }
}
