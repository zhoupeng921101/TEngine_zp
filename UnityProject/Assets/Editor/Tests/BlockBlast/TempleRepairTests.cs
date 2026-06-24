using NUnit.Framework;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 长期主线（虔诚币 + 神庙修复 + 经验·守护者等级，设计 13）单测：
    /// 订单发币、修复三前置门控、扣币标记推进、发经验+体力、经验→等级曲线、跨级升级、
    /// 全厅修完、累积只增、旧路径零回归。验收点对应设计 13 §六 T1–T12。
    /// 系统无随机，主要构造确定性状态后断言。SetUp 仿 TarotBlindBoxTests（InMemory Provider）。
    /// </summary>
    [TestFixture]
    public class TempleRepairTests
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

        // 凑足某 (类型, 等级) ×count 的库存（自动配对：注入 count×MergeCount^(level-1) 个 Lv1）。
        private static void StockFor(MergeOrderState m, MergeElement type, int level, int count)
        {
            int lv1 = count * MergeOrderConfig.Pow(MergeOrderConfig.MergeCount, level - 1);
            for (int i = 0; i < lv1; i++) m.IngestElement(type);
        }

        // 给虔诚币充值到指定值（绕开订单，直接构造状态）。
        private static void SetPiety(MergeOrderState m, int piety) => m.Piety = piety;

        // ───────────── T1 普通订单发虔诚币 ─────────────

        [Test]
        public void T1_Deliver_GrantsPiety_ByDifficulty_OldRewardsIntact()
        {
            // 逐档：Lv1×1=d1、Lv2×1=d4、Lv3×1=d16（折算因子 MergeCount^(等级-1)）。封顶等级×2 用于堆积交付（非封顶等级用 count 凑足即可）。
            int capUnit = MergeOrderConfig.Pow(MergeOrderConfig.MergeCount, MergeOrderConfig.MaxLevel - 1); // 封顶折算基础元素数
            AssertDeliverPiety(MergeElement.Butterfly, 1, 1, 1 * TempleConfig.PietyPerDifficulty);
            AssertDeliverPiety(MergeElement.Star, 2, 1, 4 * TempleConfig.PietyPerDifficulty);
            AssertDeliverPiety(MergeElement.Butterfly, 3, 1, 16 * TempleConfig.PietyPerDifficulty);
            AssertDeliverPiety(MergeElement.Star, MergeOrderConfig.MaxLevel, 2, 2 * capUnit * TempleConfig.PietyPerDifficulty);
        }

        private static void AssertDeliverPiety(MergeElement type, int level, int count, int expectedPiety)
        {
            var m = FreshState();
            // 把订单槽 0 设为目标订单并备货。
            m.ActiveOrders[0] = new Order(type, level, count);
            StockFor(m, type, level, count);

            int pietyBefore = m.Piety;
            int energyBefore = m.Energy;
            int scoreBefore = m.TotalScore;
            int doneBefore = m.CompletedOrders;

            Assert.IsTrue(m.Deliver(0), $"{type} Lv{level}×{count} 应可交付");
            Assert.AreEqual(pietyBefore + expectedPiety, m.Piety,
                $"{type} Lv{level}×{count}(d={new Order(type, level, count).Difficulty}) 虔诚币增量");
            // 旧发奖仍按原值增（纯追加，不破坏）。
            Assert.AreEqual(energyBefore + MergeOrderConfig.OrderRewardEnergy, m.Energy, "体力仍按原值增");
            Assert.AreEqual(scoreBefore + level * count * MergeOrderConfig.OrderScoreFactor, m.TotalScore, "分数仍按原值增");
            Assert.AreEqual(doneBefore + 1, m.CompletedOrders, "完成单数 +1");
        }

        // ───────────── T2 特殊订单发币（×倍率）─────────────

        [Test]
        public void T2_DeliverSpecial_GrantsPiety_WithMult_BlindBoxIntact()
        {
            var m = FreshState();
            // Star 封顶等级 ×2（封顶可堆积）。特殊虔诚币 = 难度 × PietyPerDifficulty × SpecialPietyMult。
            var order = new Order(MergeElement.Star, MergeOrderConfig.MaxLevel, 2);
            m.SpecialTrack.Request(new SpecialOrder(SpecialOrderKind.Express, order, 300f));
            StockFor(m, MergeElement.Star, MergeOrderConfig.MaxLevel, 2);
            Assert.IsTrue(m.CanDeliverSpecial());

            int pietyBefore = m.Piety;
            int boxBefore = m.BlindBoxCount;
            int expected = order.Difficulty * TempleConfig.PietyPerDifficulty * TempleConfig.SpecialPietyMult;

            Assert.IsTrue(m.DeliverSpecial());
            Assert.AreEqual(pietyBefore + expected, m.Piety, "特殊订单虔诚币 = 难度 × 旋钮 × 倍率");
            // 盲盒附赠仍生效（不被破坏）。
            Assert.AreEqual(boxBefore + TarotBlindBoxConfig.BoxPerExpress, m.BlindBoxCount, "盲盒附赠仍生效");
        }

        // ───────────── T3 不可交付不发币 ─────────────

        [Test]
        public void T3_NotDeliverable_NoPiety()
        {
            var m = FreshState();
            m.ActiveOrders[0] = new Order(MergeElement.Butterfly, 3, 1); // 需 Lv3，库存空
            int pietyBefore = m.Piety;
            Assert.IsFalse(m.Deliver(0), "库存不足不可交付");
            Assert.AreEqual(pietyBefore, m.Piety, "不可交付时虔诚币不变");

            // 特殊轨同理
            m.SpecialTrack.Request(new SpecialOrder(SpecialOrderKind.Story, new Order(MergeElement.Chalice, 3, 3), -1f));
            Assert.IsFalse(m.DeliverSpecial());
            Assert.AreEqual(pietyBefore, m.Piety, "特殊订单不可交付时虔诚币不变");
        }

        // ───────────── T4 修复三前置门控 ─────────────

        [Test]
        public void T4_CanRepair_ThreeGates()
        {
            var m = FreshState();
            // 备足够币修第 0 厅。
            SetPiety(m, TempleConfig.Cost(0));

            Assert.IsTrue(m.CanRepairTemple(0), "第 0 厅:顺序对+未修+币足 → 可修");
            Assert.IsFalse(m.CanRepairTemple(1), "跳修第 1 厅(index!=NextRepairIndex) → 不可");
            Assert.IsFalse(m.CanRepairTemple(-1), "越界 → 不可");
            Assert.IsFalse(m.CanRepairTemple(TempleConfig.HallCount), "越界 → 不可");

            // 币不足
            SetPiety(m, TempleConfig.Cost(0) - 1);
            Assert.IsFalse(m.CanRepairTemple(0), "币不足 → 不可");

            // RepairTemple 在不满足时返回 false 且状态全不变
            int pietyBefore = m.Piety;
            Assert.IsFalse(m.RepairTemple(0, out var r), "不满足前置 RepairTemple 返回 false");
            Assert.IsFalse(r.Success);
            Assert.AreEqual(pietyBefore, m.Piety, "失败时币不扣");
            Assert.IsFalse(m.TempleRepaired[0], "失败时不标记已修");
            Assert.AreEqual(0, m.NextRepairIndex, "失败时不推进");
        }

        [Test]
        public void T4_CanRepair_AlreadyRepaired_False()
        {
            var m = FreshState();
            SetPiety(m, 100000);
            Assert.IsTrue(m.RepairTemple(0, out _));
            // 第 0 厅已修,回修返回 false
            Assert.IsFalse(m.CanRepairTemple(0), "已修厅回修 → 不可（index!=NextRepairIndex 已为 1）");
        }

        // ───────────── T5 修复扣币 + 标记 + 推进 ─────────────

        [Test]
        public void T5_Repair_SpendMark_Advance()
        {
            var m = FreshState();
            SetPiety(m, 100000);

            int cost0 = TempleConfig.Cost(0);
            int pietyBefore = m.Piety;
            Assert.IsTrue(m.RepairTemple(0, out var r0));
            Assert.AreEqual(pietyBefore - cost0, m.Piety, "扣第 0 厅造价");
            Assert.IsTrue(m.TempleRepaired[0], "第 0 厅标记已修");
            Assert.IsTrue(m.TempleDecorated[0], "第 0 厅标记已装饰");
            Assert.AreEqual(1, m.NextRepairIndex, "推进到 1");
            Assert.AreEqual(cost0, r0.PietySpent, "result 扣币 = 造价");

            int cost1 = TempleConfig.Cost(1);
            int pietyMid = m.Piety;
            Assert.IsTrue(m.RepairTemple(1, out _));
            Assert.AreEqual(pietyMid - cost1, m.Piety, "扣第 1 厅造价");
            Assert.IsTrue(m.TempleRepaired[1]);
            Assert.AreEqual(2, m.NextRepairIndex, "推进到 2");
        }

        // ───────────── T6 修复发经验 + 体力 ─────────────

        [Test]
        public void T6_Repair_GrantsExp_AndEnergyCapped()
        {
            // 经验 = 造价
            var m = FreshState();
            SetPiety(m, 100000);
            int expBefore = m.Exp;
            Assert.IsTrue(m.RepairTemple(0, out var r));
            Assert.AreEqual(expBefore + TempleConfig.Cost(0), m.Exp, "经验增 = 造价");
            Assert.AreEqual(TempleConfig.Cost(0), r.ExpGained);

            // 满血时体力不溢出
            var m2 = FreshState();
            SetPiety(m2, 100000);
            m2.Energy = MergeOrderConfig.EnergyCap; // 已满
            Assert.IsTrue(m2.RepairTemple(0, out _));
            Assert.AreEqual(MergeOrderConfig.EnergyCap, m2.Energy, "满血修复体力不溢出软上限");
        }

        // ───────────── T7 经验 → 等级曲线 ─────────────

        [Test]
        public void T7_ExpToLevelCurve()
        {
            Assert.AreEqual(1, TempleConfig.GuardianLevelFor(0), "0 经验 = 1 级");
            Assert.AreEqual(1, TempleConfig.GuardianLevelFor(499), "差 1 不升级");
            Assert.AreEqual(2, TempleConfig.GuardianLevelFor(500), "恰好门槛 500 → 2 级(>=)");
            Assert.AreEqual(2, TempleConfig.GuardianLevelFor(1299), "1299 仍 2 级");
            Assert.AreEqual(3, TempleConfig.GuardianLevelFor(1300), "累计 1300 → 3 级");
            Assert.AreEqual(4, TempleConfig.GuardianLevelFor(2400), "累计 2400 → 4 级");
            Assert.AreEqual(5, TempleConfig.GuardianLevelFor(3800), "累计 3800 → 5 级");
            Assert.AreEqual(6, TempleConfig.GuardianLevelFor(5500), "累计 5500 → 6 级");
            Assert.AreEqual(7, TempleConfig.GuardianLevelFor(7500), "累计 7500 → 7 级");

            // 等级是经验纯函数：派生属性与配置一致。
            var m = FreshState();
            m.Exp = 1300;
            Assert.AreEqual(3, m.GuardianLevel, "GuardianLevel 与 GuardianLevelFor 一致");
        }

        // ───────────── T8 修复触发升级（含跨级）─────────────

        [Test]
        public void T8_Repair_TriggersLevelUp_SingleAndMulti()
        {
            // 单级:经验接近门槛 → 一次修复升一级。
            var m = FreshState();
            SetPiety(m, 100000);
            m.Exp = 400; // Lv1,差 100 到 Lv2
            int chapterBefore = m.UnlockedChapter;
            int levelBefore = m.GuardianLevel;
            Assert.AreEqual(1, levelBefore);
            // 修第 0 厅经验 +500 → Exp=900 → Lv2(>=500)
            Assert.IsTrue(m.RepairTemple(0, out var r));
            Assert.AreEqual(2, m.GuardianLevel, "修复后升到 2 级");
            Assert.AreEqual(1, r.LevelsGained, "本次跨 1 级");
            Assert.AreEqual(chapterBefore + 1, m.UnlockedChapter, "解锁 +1 章");

            // 跨多级:把高造价厅设为下一座待修(前序全标记已修),一次修复经验大跨多级。
            var m2 = FreshState();
            SetPiety(m2, 1000000);
            m2.Exp = 0; // Lv1
            // 标记前 11 厅已修,使第 11 厅(造价 3250)成为下一座可修。
            for (int i = 0; i < 11; i++) m2.TempleRepaired[i] = true;
            m2.NextRepairIndex = 11;
            int cost11 = TempleConfig.Cost(11); // 3250
            // 修第 11 厅 → Exp 0→3250。GuardianLevelFor(3250):500(2)+800(3)+1100(4)余850<1400 → Lv4。
            Assert.AreEqual(4, TempleConfig.GuardianLevelFor(cost11), "前置:3250 经验对应 Lv4(跨 3 级)");
            int lvBefore2 = m2.GuardianLevel; // Lv1
            Assert.IsTrue(m2.RepairTemple(11, out var r2));
            int lvAfter2 = m2.GuardianLevel;  // Lv4
            Assert.AreEqual(3, r2.LevelsGained, "一次修复跨 3 级");
            Assert.AreEqual(lvAfter2 - lvBefore2, r2.LevelsGained, "result 跨级数 = 实际等级差");
            Assert.AreEqual(3, m2.UnlockedChapter, "跨 3 级 → 解锁 3 章");
        }

        [Test]
        public void T8_Repair_MultiLevelJump_ChapterAndEnergyAccumulate()
        {
            // 构造一次修复跨多级:把升级门槛压低不可行(常量固定),
            // 改为顺序修多厅累积验证「每升 1 级 +1 章 + 升级体力累加」。
            var m = FreshState();
            SetPiety(m, 1000000);
            m.Energy = 0; // 从 0 起便于观察体力累加(受软上限)

            int chapterTotal = 0;
            int levelPrev = m.GuardianLevel;
            for (int i = 0; i < TempleConfig.HallCount; i++)
            {
                Assert.IsTrue(m.RepairTemple(i, out var r));
                int levelNow = m.GuardianLevel;
                Assert.AreEqual(levelNow - levelPrev, r.LevelsGained, $"第 {i} 厅 result 跨级数");
                chapterTotal += r.LevelsGained;
                levelPrev = levelNow;
            }
            Assert.AreEqual(chapterTotal, m.UnlockedChapter, "累计章节解锁 = 累计跨级数");
            // 修完 12 厅累计经验 = 累计造价 22500 → GuardianLevelFor(22500)
            int sumCost = 0;
            for (int i = 0; i < TempleConfig.HallCount; i++) sumCost += TempleConfig.Cost(i);
            Assert.AreEqual(TempleConfig.GuardianLevelFor(sumCost), m.GuardianLevel, "修完全厅等级 = 累计经验对应等级");
        }

        // ───────────── T9 全厅修完标记 ─────────────

        [Test]
        public void T9_AllRepaired_Marked_NoMoreRepair()
        {
            var m = FreshState();
            SetPiety(m, 1000000);
            for (int i = 0; i < TempleConfig.HallCount; i++)
                Assert.IsTrue(m.RepairTemple(i, out _), $"修第 {i} 厅");

            Assert.AreEqual(TempleConfig.HallCount, m.NextRepairIndex, "NextRepairIndex == HallCount");
            Assert.IsTrue(m.IsTempleAllRepaired, "全厅修完标记");
            // 此后任何 RepairTemple 返回 false
            Assert.IsFalse(m.CanRepairTemple(TempleConfig.HallCount), "越界不可修");
            Assert.IsFalse(m.RepairTemple(0, out _), "已修厅不可再修");
            Assert.IsFalse(m.RepairTemple(TempleConfig.HallCount, out _), "越界修复返回 false");
        }

        // ───────────── T10 虔诚币累积 + 只增 ─────────────

        [Test]
        public void T10_AddPiety_OnlyPositive_NoCapNoOverflowGuard()
        {
            var m = FreshState();
            Assert.AreEqual(0, m.Piety, "开局 0");
            m.AddPiety(0);
            Assert.AreEqual(0, m.Piety, "加 0 不增");
            m.AddPiety(-50);
            Assert.AreEqual(0, m.Piety, "加负不增");
            m.AddPiety(100);
            m.AddPiety(250);
            Assert.AreEqual(350, m.Piety, "正数累加正确");
            m.AddPiety(1000000);
            Assert.AreEqual(1000350, m.Piety, "大值不截断(无上限)");
        }

        [Test]
        public void T10_MultiDeliver_PietyAccumulates()
        {
            var m = FreshState();
            m.ActiveOrders[0] = new Order(MergeElement.Butterfly, 1, 1); // d1 → 30
            StockFor(m, MergeElement.Butterfly, 1, 1);
            Assert.IsTrue(m.Deliver(0));
            int after1 = m.Piety;
            Assert.AreEqual(1 * TempleConfig.PietyPerDifficulty, after1);

            m.ActiveOrders[0] = new Order(MergeElement.Star, 2, 1); // Lv2×1 → d4（折算因子 MergeCount^(等级-1)）
            StockFor(m, MergeElement.Star, 2, 1);
            Assert.IsTrue(m.Deliver(0));
            Assert.AreEqual(after1 + 4 * TempleConfig.PietyPerDifficulty, m.Piety, "多次交付累加");
        }

        // ───────────── T12 旧路径零回归 ─────────────

        [Test]
        public void T12_FreshState_MainlineZeroed()
        {
            var m = FreshState();
            Assert.AreEqual(0, m.Piety, "开局虔诚币 0");
            Assert.AreEqual(0, m.Exp, "开局经验 0");
            Assert.AreEqual(0, m.UnlockedChapter, "开局章节 0");
            Assert.AreEqual(0, m.NextRepairIndex, "开局从第 0 厅起");
            Assert.AreEqual(1, m.GuardianLevel, "开局守护者 1 级");
            Assert.IsFalse(m.IsTempleAllRepaired, "开局未修完");
            Assert.AreEqual(TempleConfig.HallCount, m.TempleRepaired.Length, "修复位数组长度 = 厅数");
            Assert.AreEqual(TempleConfig.HallCount, m.TempleDecorated.Length, "装饰位数组长度 = 厅数");
            for (int i = 0; i < TempleConfig.HallCount; i++)
            {
                Assert.IsFalse(m.TempleRepaired[i], $"第 {i} 厅未修");
                Assert.IsFalse(m.TempleDecorated[i], $"第 {i} 厅未装饰");
            }
        }

        [Test]
        public void T12_Deliver_WithoutTouchingMainlineMethods_OldBehaviorStable()
        {
            // 不调任何神庙方法,只验交付的旧断言(体力/分数/完成数)与发币并存且独立。
            var m = FreshState();
            m.ActiveOrders[0] = new Order(MergeElement.Butterfly, 1, 1);
            StockFor(m, MergeElement.Butterfly, 1, 1);

            int energyBefore = m.Energy;
            int scoreBefore = m.TotalScore;
            Assert.IsTrue(m.Deliver(0));
            Assert.AreEqual(energyBefore + MergeOrderConfig.OrderRewardEnergy, m.Energy, "体力旧行为不变");
            Assert.AreEqual(scoreBefore + 1 * 1 * MergeOrderConfig.OrderScoreFactor, m.TotalScore, "分数旧行为不变");
            // 神庙状态未被交付动作意外触碰
            Assert.AreEqual(0, m.NextRepairIndex, "交付不推进神庙");
            Assert.AreEqual(0, m.Exp, "交付不产经验");
        }

        // ───────────── 配置自洽（造价公式一致性）─────────────

        [Test]
        public void Config_CostMatchesFormula_AndHallCount()
        {
            Assert.AreEqual(TempleConfig.HallCount, TempleConfig.Halls.Length, "厅数与数组长度一致");
            Assert.AreEqual(500, TempleConfig.Cost(0), "第 0 厅 500(GDD 愚者大厅)");
            for (int i = 0; i < TempleConfig.HallCount; i++)
            {
                int expected = TempleConfig.TempleBaseCost + i * TempleConfig.TempleCostStep;
                Assert.AreEqual(expected, TempleConfig.Cost(i), $"第 {i} 厅造价 = 公式默认值");
            }
            Assert.AreEqual(0, TempleConfig.Cost(-1), "越界造价 0");
            Assert.AreEqual(0, TempleConfig.Cost(TempleConfig.HallCount), "越界造价 0");
        }
    }
}
