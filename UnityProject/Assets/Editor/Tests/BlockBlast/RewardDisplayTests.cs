using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using GameLogic.Config;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Item;
using GameLogic.BlockBlast.Numeric;
using GameLogic.BlockBlast.Reward;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 通用奖励展示归一层验收测试（设计 17 §六）。全纯逻辑：转换(V) / 品质色(Q) / 数量文本(N) /
    /// 边界降级(B) 直接调；涉及注册表查询用 <c>InitForTest</c> 注入，绕 ConfigSystem（不碰 YooAsset / Unity 运行时）。
    /// </summary>
    [TestFixture]
    public class RewardDisplayTests
    {
        // 注册表注入隔离：每条用例前后清缓存，避免相互污染。
        [SetUp]
        public void Reset()
        {
            NumericConfigMgr.ResetForTest();
            ItemConfigMgr.ResetForTest();
        }

        [TearDown]
        public void Cleanup()
        {
            NumericConfigMgr.ResetForTest();
            ItemConfigMgr.ResetForTest();
        }

        // 样例数值表：经验(id=1) / 体力(id=4)。
        private static List<NumericEntry> NumEntries() => new List<NumericEntry>
        {
            new NumericEntry { NumId = 1, NameTextId = 100001, IconName = "icon_exp",    NumType = 1, Quality = 1 },
            new NumericEntry { NumId = 4, NameTextId = 100004, IconName = "icon_energy", NumType = 4, Quality = 2 },
        };

        // 样例道具：材料 id=7001(Type=2 材料) / 自选礼包道具 id=7005(Type=5)。
        private static List<ItemDef> ItemDefs() => new List<ItemDef>
        {
            new ItemDef { Id = 7001, Name = 110001, Icon = "icon_mat", Quality = 3, Type = 2 },
            new ItemDef { Id = 7005, Name = 110005, Icon = "icon_gift", Quality = 4, Type = 5 },
        };

        // ───────────────────────── V 类：各源 → RewardView 转换 ─────────────────────────

        [Test]
        public void V1_Grant_Numeric()
        {
            NumericConfigMgr.InitForTest(NumEntries());
            var v = RewardDisplay.From(new GrantPayload(GrantKind.Numeric, 1, 5000, 0, 0));
            Assert.AreEqual("icon_exp", v.IconName);
            Assert.AreEqual(100001, v.NameTextId);
            Assert.AreEqual("x5K", v.CountText);
            Assert.AreEqual(RewardBadge.Currency, v.Badge);
            Assert.AreEqual(5000L, v.RawAmount);
        }

        [Test]
        public void V2_Grant_Pattern()
        {
            // 钻石(100) Lv2 ×4。
            var v = RewardDisplay.From(new GrantPayload(GrantKind.Pattern, 100, 4, 2, 0));
            Assert.AreEqual("pattern_100", v.IconName);
            Assert.AreEqual(RewardBadge.Pattern, v.Badge);
            Assert.AreEqual("Lv2 x4", v.CountText);
            Assert.AreEqual(RewardDisplay.QualityColor(RewardDisplay.PatternQuality(2)), v.QualityColor);
        }

        [Test]
        public void V3_Grant_None_Material()
        {
            ItemConfigMgr.InitForTest(ItemDefs());
            // None：TargetId 是道具 id（ItemGrant.Resolve default 分支 def.Id），走 FromItem。
            var v = RewardDisplay.From(new GrantPayload(GrantKind.None, 7001, 3, 0, 0));
            Assert.AreEqual("icon_mat", v.IconName);
            Assert.AreEqual(110001, v.NameTextId);
            Assert.AreEqual(RewardBadge.Material, v.Badge);
        }

        [Test]
        public void V4_Grant_Gift()
        {
            // 随机礼包 index=6001，开 3 次。
            var v = RewardDisplay.From(new GrantPayload(GrantKind.GiftRandom, 6001, 0, 0, 3));
            Assert.AreEqual(RewardBadge.Gift, v.Badge);
            Assert.AreEqual("x3", v.CountText);   // CountText 反映 Times
            Assert.AreEqual(3L, v.RawAmount);
        }

        [Test]
        public void V5_Chest_Energy()
        {
            NumericConfigMgr.InitForTest(NumEntries());
            var v = RewardDisplay.From(new ChestReward(ChestRewardKind.Energy, 12));
            Assert.AreEqual("icon_energy", v.IconName); // 走 FromNumeric(Energy=4, 12)
            Assert.AreEqual("x12", v.CountText);
            Assert.AreEqual(RewardBadge.Currency, v.Badge);
        }

        [Test]
        public void V6_Chest_Soul_NoThrow()
        {
            // 灵力无 num_id，走本层私有占位，不查 num 表，不抛。
            Assert.DoesNotThrow(() =>
            {
                var v = RewardDisplay.From(new ChestReward(ChestRewardKind.Soul, 200));
                Assert.AreEqual(RewardBadge.Currency, v.Badge);
                Assert.AreEqual("Soul", v.IconName);
                Assert.AreEqual("x200", v.CountText);
            });
        }

        [Test]
        public void V7_Chest_Pattern()
        {
            // Lv3 ×1（PatternLevel=3，Amount=1）。
            var v = RewardDisplay.From(new ChestReward(ChestRewardKind.Pattern, 1, 3));
            Assert.AreEqual(RewardBadge.Pattern, v.Badge);
            StringAssert.Contains("Lv3", v.CountText);
            Assert.AreEqual(RewardDisplay.QualityColor(RewardDisplay.PatternQuality(3)), v.QualityColor);
        }

        [Test]
        public void V8_Chest_Wish_Function()
        {
            var w = RewardDisplay.From(new ChestReward(ChestRewardKind.WishCharge, 5));
            Assert.AreEqual(RewardBadge.Function, w.Badge);
            Assert.AreEqual("x5", w.CountText);
            Assert.AreEqual(RewardDisplay.QualityColor(1), w.QualityColor); // 品质退化白
        }

        [Test]
        public void V9_FromNumeric_FromItem_Direct()
        {
            NumericConfigMgr.InitForTest(NumEntries());
            ItemConfigMgr.InitForTest(ItemDefs());

            var n = RewardDisplay.FromNumeric(1, 200);
            Assert.AreEqual("icon_exp", n.IconName);
            Assert.AreEqual(100001, n.NameTextId);
            Assert.AreEqual("x200", n.CountText);
            Assert.AreEqual(RewardBadge.Currency, n.Badge);

            var i = RewardDisplay.FromItem(7001, 1);
            Assert.AreEqual("", i.CountText); // 单件不带 "x1"
        }

        // ───────────────────────── Q 类：6 档品质色 ─────────────────────────

        [Test]
        public void Q1_SixTiers_AllDistinct()
        {
            var colors = new[]
            {
                RewardDisplay.QualityColor(1), RewardDisplay.QualityColor(2),
                RewardDisplay.QualityColor(3), RewardDisplay.QualityColor(4),
                RewardDisplay.QualityColor(5), RewardDisplay.QualityColor(6),
            };
            for (int a = 0; a < colors.Length; a++)
            {
                for (int b = a + 1; b < colors.Length; b++)
                {
                    Assert.AreNotEqual(colors[a], colors[b], $"品质 {a + 1} 与 {b + 1} 色应不等");
                }
            }
            AssertColor(new Color(0.85f, 0.85f, 0.85f), RewardDisplay.QualityColor(1)); // 1==白
        }

        [Test]
        public void Q2_OutOfRange_FallsBackWhite()
        {
            var white = new Color(0.85f, 0.85f, 0.85f);
            AssertColor(white, RewardDisplay.QualityColor(0));
            AssertColor(white, RewardDisplay.QualityColor(7));
            AssertColor(white, RewardDisplay.QualityColor(-1));
        }

        [Test]
        public void Q3_RgbMatchesSpec()
        {
            AssertColor(new Color(0.85f, 0.85f, 0.85f), RewardDisplay.QualityColor(1)); // 白
            AssertColor(new Color(0.36f, 0.84f, 0.63f), RewardDisplay.QualityColor(2)); // 绿
            AssertColor(new Color(0.42f, 0.55f, 1.00f), RewardDisplay.QualityColor(3)); // 蓝
            AssertColor(new Color(0.69f, 0.49f, 1.00f), RewardDisplay.QualityColor(4)); // 紫
            AssertColor(new Color(1.00f, 0.66f, 0.30f), RewardDisplay.QualityColor(5)); // 橙
            AssertColor(new Color(1.00f, 0.48f, 0.54f), RewardDisplay.QualityColor(6)); // 红
        }

        // ───────────────────────── N 类：数量文本 ─────────────────────────

        [Test]
        public void N1_CountText_ReusesNumericFormat()
        {
            Assert.AreEqual("x200", RewardDisplay.CountText(200));
            Assert.AreEqual("x999.9K", RewardDisplay.CountText(999999)); // 截断不进位
            Assert.AreEqual("", RewardDisplay.CountText(1));             // 单件不带 x1
            Assert.AreEqual("x0", RewardDisplay.CountText(0));
        }

        [Test]
        public void N2_PatternCountText_WithLevel()
        {
            Assert.AreEqual("Lv2 x3", RewardDisplay.PatternCountText(2, 3));
            Assert.AreEqual("Lv1", RewardDisplay.PatternCountText(1, 1)); // 单个图案只显等级
        }

        // ───────────────────────── B 类：查无降级不抛 ─────────────────────────

        [Test]
        public void B1_Numeric_MissDegradesNoThrow()
        {
            // 不 InitForTest（缓存空）。注：FromNumeric 内部 NumericConfigMgr.Get 会 EnsureLoaded，
            // 但本测试在 EditMode 无 ConfigSystem 运行时 → 若未注入将访问 ConfigSystem 而非降级。
            // 故先注入空集合，模拟「表已加载但无此 id」的查无降级路径。
            NumericConfigMgr.InitForTest(new List<NumericEntry>());
            RewardView v = default;
            Assert.DoesNotThrow(() => v = RewardDisplay.FromNumeric(999, 100));
            Assert.IsNull(v.IconName);
            Assert.AreEqual(0, v.NameTextId);
            Assert.AreEqual(RewardDisplay.QualityColor(1), v.QualityColor); // 品质退化白
            Assert.AreEqual("x100", v.CountText);                          // 数量仍正常
        }

        [Test]
        public void B2_Item_MissDegradesNoThrow()
        {
            ItemConfigMgr.InitForTest(new List<ItemDef>());
            RewardView v = default;
            Assert.DoesNotThrow(() => v = RewardDisplay.FromItem(999999, 1));
            Assert.IsNull(v.IconName);
            Assert.AreEqual(0, v.NameTextId);
            Assert.AreEqual(RewardDisplay.QualityColor(1), v.QualityColor);
        }

        // ───────────────────────── Z 类：全链纯逻辑 ─────────────────────────

        [Test]
        public void Z1_PureLogic_NoConfigSystem()
        {
            // 本测试能在 EditMode（无 Unity 运行时资源模块）跑通本身即证：未触 ConfigSystem.Instance.Tables / YooAsset。
            NumericConfigMgr.InitForTest(NumEntries());
            ItemConfigMgr.InitForTest(ItemDefs());
            Assert.AreEqual("icon_exp", RewardDisplay.From(new GrantPayload(GrantKind.Numeric, 1, 100, 0, 0)).IconName);
            Assert.AreEqual("pattern_100", RewardDisplay.From(new GrantPayload(GrantKind.Pattern, 100, 1, 1, 0)).IconName);
            Assert.AreEqual(new Color(0.85f, 0.85f, 0.85f), RewardDisplay.QualityColor(1));
        }

        // 浮点容差比对（设计 17 §六 Q3：容差 0.01）。
        private static void AssertColor(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 0.01f, "R");
            Assert.AreEqual(expected.g, actual.g, 0.01f, "G");
            Assert.AreEqual(expected.b, actual.b, 0.01f, "B");
        }
    }
}
