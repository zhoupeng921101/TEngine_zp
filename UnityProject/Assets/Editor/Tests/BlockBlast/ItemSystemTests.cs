using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameLogic.Config;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Item;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 道具底层系统验收测试（设计 16 §六）。
    /// 配置表(C) = AssetDatabase 直读 item_*.bytes（仿 NumericSystemTests，绕 YooAsset）；
    /// 注册表(R)/礼包(G)/UseEffect(U)/背包(B)/隔离(Z) = 纯逻辑 InitForTest / new。
    /// </summary>
    [TestFixture]
    public class ItemSystemTests
    {
        private const string ItemBytes = "Assets/AssetRaw/Configs/bytes/item_tbitemdef.bytes";
        private const string GiftRandomBytes = "Assets/AssetRaw/Configs/bytes/item_tbgiftrandom.bytes";
        private const string GiftSelectBytes = "Assets/AssetRaw/Configs/bytes/item_tbgiftselect.bytes";

        // ───────────────────────── 直读 .bytes 辅助 ─────────────────────────

        private static GameConfig.item.TbItemDef LoadItemTable()
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(ItemBytes);
            Assert.IsNotNull(ta, $"找不到 {ItemBytes}，请先运行 Luban 导表");
            return new GameConfig.item.TbItemDef(new Luban.ByteBuf(ta.bytes));
        }

        private static GameConfig.item.TbGiftRandom LoadGiftRandomTable()
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(GiftRandomBytes);
            Assert.IsNotNull(ta, $"找不到 {GiftRandomBytes}，请先运行 Luban 导表");
            return new GameConfig.item.TbGiftRandom(new Luban.ByteBuf(ta.bytes));
        }

        private static GameConfig.item.TbGiftSelect LoadGiftSelectTable()
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(GiftSelectBytes);
            Assert.IsNotNull(ta, $"找不到 {GiftSelectBytes}，请先运行 Luban 导表");
            return new GameConfig.item.TbGiftSelect(new Luban.ByteBuf(ta.bytes));
        }

        // ───────────────────────── 配置表（C1–C6，直读 .bytes） ─────────────────────────

        [Test]
        public void C1_ItemTable_HasAtLeast8Rows()
        {
            var table = LoadItemTable();
            Assert.GreaterOrEqual(table.DataList.Count, 8, "道具表样例 ≥8 行");
        }

        [Test]
        public void C2_Item30006_FieldsMatchSource()
        {
            var table = LoadItemTable();
            var row = table.GetOrDefault(30006); // 随机礼包道具
            Assert.IsNotNull(row, "id=30006 行应存在");
            Assert.AreEqual(6, (int)row.Type);        // GIFT_RANDOM=6
            Assert.AreEqual(4, row.UseEffect);        // 4 随机礼包
            Assert.AreEqual(6001, row.UseValue);      // 随机礼包 index
            Assert.AreEqual(1, row.Param);            // 抽 1 次
            Assert.AreEqual(5, (int)row.Quality);     // 传说橙
            Assert.AreEqual(0, row.Stacking);         // 礼包不可叠
        }

        [Test]
        public void C3_Quality_Covers1To6()
        {
            var table = LoadItemTable();
            var qualities = new HashSet<int>();
            foreach (var row in table.DataList) qualities.Add((int)row.Quality);
            for (int q = 1; q <= 6; q++)
                Assert.IsTrue(qualities.Contains(q), $"品质 {q} 应至少有一行覆盖");
            Assert.AreEqual(6, (int)GameConfig.item.EItemQuality.MYTH, "MYTH==6");
        }

        [Test]
        public void C4_ItemType_EnumValuesMatchSpec_Skip4()
        {
            Assert.AreEqual(1, (int)GameConfig.item.EItemType.CURRENCY);
            Assert.AreEqual(2, (int)GameConfig.item.EItemType.MATERIAL);
            Assert.AreEqual(3, (int)GameConfig.item.EItemType.FUNC_MATERIAL);
            Assert.AreEqual(5, (int)GameConfig.item.EItemType.GIFT_SELECT);
            Assert.AreEqual(6, (int)GameConfig.item.EItemType.GIFT_RANDOM);
        }

        [Test]
        public void C5_GiftRandom_Index6001_Aggregates4_WeightSum100()
        {
            var table = LoadGiftRandomTable();
            int count = 0, weightSum = 0;
            foreach (var row in table.DataList)
            {
                if (row.Index == 6001) { count++; weightSum += row.Rate; }
            }
            Assert.AreEqual(4, count, "index=6001 聚 4 项");
            Assert.AreEqual(100, weightSum, "权重和=100");
        }

        [Test]
        public void C6_GiftSelect_Index5001_Aggregates3()
        {
            var table = LoadGiftSelectTable();
            int count = 0;
            foreach (var row in table.DataList)
                if (row.Index == 5001) count++;
            Assert.AreEqual(3, count, "index=5001 聚 3 项");
        }

        // ───────────────────────── 样例 POCO（与 itemdef.xlsx 同值） ─────────────────────────

        private static ItemDef ExpItem() => new ItemDef
        { Id = 30001, Type = 1, UseEffect = 1, UseValue = 1, UseNum = 5000, Stacking = 0, Quality = 1, Automatic = 1, Icon = "icon_item_exp" };

        private static ItemDef EnergyItem() => new ItemDef
        { Id = 30002, Type = 1, UseEffect = 1, UseValue = 4, UseNum = 30, Stacking = 0, Quality = 1, Automatic = 1, Icon = "icon_item_energy" };

        private static ItemDef PlainMaterial() => new ItemDef
        { Id = 30003, Type = 2, UseEffect = 0, UseValue = 0, UseNum = 0, Stacking = 1, Quality = 2, Icon = "icon_item_mat" };

        private static ItemDef PatternItem() => new ItemDef
        { Id = 30004, Type = 3, UseEffect = 2, UseValue = 1, UseNum = 2, UseLevel = 2, Stacking = 1, Quality = 3, Automatic = 1 }; // UseValue=1 = Butterfly 枚举值

        private static ItemDef GiftSelectItem() => new ItemDef
        { Id = 30005, Type = 5, UseEffect = 3, UseValue = 5001, Param = 1, Stacking = 0, Quality = 4 };

        private static ItemDef GiftRandomItem() => new ItemDef
        { Id = 30006, Type = 6, UseEffect = 4, UseValue = 6001, Param = 1, Stacking = 0, Quality = 5, Automatic = 1 };

        private static ItemDef NonStackMaterial() => new ItemDef
        { Id = 30008, Type = 2, UseEffect = 0, Stacking = 0, Quality = 2, Icon = "icon_item_mat2" };

        private static List<ItemDef> SampleItems() => new List<ItemDef>
        {
            ExpItem(), EnergyItem(), PlainMaterial(), PatternItem(),
            GiftSelectItem(), GiftRandomItem(), NonStackMaterial(),
        };

        private static List<(int, GiftEntry)> SampleRandomPool() => new List<(int, GiftEntry)>
        {
            (6001, new GiftEntry { ItemId = 30001, Num = 1, Rate = 50 }),
            (6001, new GiftEntry { ItemId = 30002, Num = 1, Rate = 30 }),
            (6001, new GiftEntry { ItemId = 30004, Num = 1, Rate = 15 }),
            (6001, new GiftEntry { ItemId = 30003, Num = 2, Rate = 5 }),
        };

        private static List<(int, GiftEntry)> SampleSelectPool() => new List<(int, GiftEntry)>
        {
            (5001, new GiftEntry { ItemId = 30001, Num = 1, Rate = 0 }),
            (5001, new GiftEntry { ItemId = 30002, Num = 1, Rate = 0 }),
            (5001, new GiftEntry { ItemId = 30004, Num = 1, Rate = 0 }),
        };

        [SetUp]
        public void ResetMgr() => ItemConfigMgr.ResetForTest();

        [TearDown]
        public void CleanupMgr() => ItemConfigMgr.ResetForTest();

        // ───────────────────────── 注册表（R1–R3） ─────────────────────────

        [Test]
        public void R1_GetItem_HitAndMiss()
        {
            ItemConfigMgr.InitForTest(SampleItems());
            var exp = ItemConfigMgr.GetItem(30001);
            Assert.IsNotNull(exp);
            Assert.AreEqual(1, exp.UseEffect);
            Assert.AreEqual(5000, exp.UseNum);
            Assert.IsNull(ItemConfigMgr.GetItem(999999), "未命中应返 null 不抛");
        }

        [Test]
        public void R2_GetGiftRandom_PoolAndEmpty()
        {
            ItemConfigMgr.InitForTest(SampleItems(), SampleRandomPool(), SampleSelectPool());
            Assert.AreEqual(4, ItemConfigMgr.GetGiftRandom(6001).Count);
            var none = ItemConfigMgr.GetGiftRandom(99999);
            Assert.IsNotNull(none);
            Assert.AreEqual(0, none.Count, "查无返空集合不抛");
        }

        [Test]
        public void R3_ToItemDef_PreservesFields()
        {
            // 从真实 .bytes 行转 POCO，逐字段比对（既验桥接也验 .bytes 对得上）。
            var table = LoadItemTable();
            var row = table.GetOrDefault(30006);
            var def = ItemConfigMgr.ToItemDef(row);
            Assert.AreEqual(row.Id, def.Id);
            Assert.AreEqual(row.Name, def.Name);
            Assert.AreEqual(row.Desc, def.Desc);
            Assert.AreEqual(row.Icon, def.Icon);
            Assert.AreEqual((int)row.Quality, def.Quality);
            Assert.AreEqual((int)row.Type, def.Type);
            Assert.AreEqual(row.Param, def.Param);
            Assert.AreEqual(row.UseEffect, def.UseEffect);
            Assert.AreEqual(row.UseValue, def.UseValue);
            Assert.AreEqual(row.UseNum, def.UseNum);
            Assert.AreEqual(row.UseLevel, def.UseLevel);
            Assert.AreEqual(row.Stacking, def.Stacking);
        }

        // ───────────────────────── 礼包抽样（G1–G4） ─────────────────────────

        [Test]
        public void G1_RollRandom_Deterministic_SameSeedSameSequence()
        {
            ItemConfigMgr.InitForTest(SampleItems(), SampleRandomPool());
            var pool = ItemConfigMgr.GetGiftRandom(6001);

            var seq1 = new List<int>();
            var rng1 = new System.Random(12345);
            for (int i = 0; i < 50; i++) seq1.Add(GiftOpener.RollRandom(pool, rng1).ItemId);

            var seq2 = new List<int>();
            var rng2 = new System.Random(12345);
            for (int i = 0; i < 50; i++) seq2.Add(GiftOpener.RollRandom(pool, rng2).ItemId);

            CollectionAssert.AreEqual(seq1, seq2, "同种子+同奖池抽样序列应完全相同");
        }

        [Test]
        public void G2_RollRandom_Distribution_LargeSample()
        {
            ItemConfigMgr.InitForTest(SampleItems(), SampleRandomPool());
            var pool = ItemConfigMgr.GetGiftRandom(6001);
            var rng = new System.Random(2024);
            const int N = 10000;
            var hits = new Dictionary<int, int>();
            for (int i = 0; i < N; i++)
            {
                int id = GiftOpener.RollRandom(pool, rng).ItemId;
                hits.TryGetValue(id, out var c);
                hits[id] = c + 1;
            }
            // 期望 {30001:50%, 30002:30%, 30004:15%, 30003:5%}，±3% 容差
            AssertFreq(hits, 30001, 0.50, N);
            AssertFreq(hits, 30002, 0.30, N);
            AssertFreq(hits, 30004, 0.15, N);
            AssertFreq(hits, 30003, 0.05, N);
        }

        private static void AssertFreq(Dictionary<int, int> hits, int id, double expected, int n)
        {
            hits.TryGetValue(id, out var c);
            double freq = (double)c / n;
            Assert.That(freq, Is.EqualTo(expected).Within(0.03), $"item {id} 频率 {freq:P1} 应落 {expected:P0}±3%");
        }

        [Test]
        public void G3_RollRandom_Boundaries()
        {
            // 空池 → null
            Assert.IsNull(GiftOpener.RollRandom(new List<GiftEntry>(), new System.Random(1)));
            Assert.IsNull(GiftOpener.RollRandom(null, new System.Random(1)));

            // 单项 → 必中
            var single = new List<GiftEntry> { new GiftEntry { ItemId = 7, Rate = 10 } };
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(7, GiftOpener.RollRandom(single, new System.Random(i)).ItemId);

            // 全 0 权重 → 退化取首项
            var allZero = new List<GiftEntry>
            { new GiftEntry { ItemId = 1, Rate = 0 }, new GiftEntry { ItemId = 2, Rate = 0 } };
            Assert.AreEqual(1, GiftOpener.RollRandom(allZero, new System.Random(5)).ItemId);

            // 负权重当 0：仅正权重项可被抽中
            var withNeg = new List<GiftEntry>
            { new GiftEntry { ItemId = 1, Rate = -100 }, new GiftEntry { ItemId = 2, Rate = 10 } };
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(2, GiftOpener.RollRandom(withNeg, new System.Random(i)).ItemId, "负权重项不应被抽中");

            // times≤0 当 1 次
            ItemConfigMgr.InitForTest(SampleItems(), SampleRandomPool());
            Assert.AreEqual(1, GiftOpener.OpenRandom(6001, 0, new System.Random(1)).Count);
            Assert.AreEqual(1, GiftOpener.OpenRandom(6001, -3, new System.Random(1)).Count);
            Assert.AreEqual(3, GiftOpener.OpenRandom(6001, 3, new System.Random(1)).Count);
        }

        [Test]
        public void G4_ListSelectable_ReturnsAllInOrder()
        {
            ItemConfigMgr.InitForTest(SampleItems(), null, SampleSelectPool());
            var list = GiftOpener.ListSelectable(5001);
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(30001, list[0].ItemId);
            Assert.AreEqual(30002, list[1].ItemId);
            Assert.AreEqual(30004, list[2].ItemId);
        }

        // ───────────────────────── UseEffect 解析（U1–U5） ─────────────────────────

        [Test]
        public void U1_Resolve_Numeric()
        {
            var p = ItemGrant.Resolve(ExpItem(), 1);
            Assert.AreEqual(GrantKind.Numeric, p.Kind);
            Assert.AreEqual(1, p.TargetId);     // num_id=1 经验
            Assert.AreEqual(5000, p.Amount);
        }

        [Test]
        public void U2_Resolve_Pattern_WithLevel()
        {
            var p = ItemGrant.Resolve(PatternItem(), 2); // use_num=2 × count=2 = 4
            Assert.AreEqual(GrantKind.Pattern, p.Kind);
            Assert.AreEqual(1, p.TargetId);   // Butterfly key
            Assert.AreEqual(4, p.Amount);
            Assert.AreEqual(2, p.Level);
        }

        [Test]
        public void U3_Resolve_Gifts()
        {
            var sel = ItemGrant.Resolve(GiftSelectItem(), 1);
            Assert.AreEqual(GrantKind.GiftSelect, sel.Kind);
            Assert.AreEqual(5001, sel.TargetId);

            var rnd = ItemGrant.Resolve(GiftRandomItem(), 1);
            Assert.AreEqual(GrantKind.GiftRandom, rnd.Kind);
            Assert.AreEqual(6001, rnd.TargetId);
            Assert.AreEqual(1, rnd.Times);      // = param
        }

        [Test]
        public void U4_Resolve_None_PureHold()
        {
            var p = ItemGrant.Resolve(PlainMaterial(), 3);
            Assert.AreEqual(GrantKind.None, p.Kind);
        }

        [Test]
        public void U5_ApplyPattern_LandsOnAddDirect()
        {
            var state = new MergeOrderState();
            state.Reset();
            int before = state.InventoryCount(MergeElement.Butterfly, 2)
                       + state.InventoryCount(MergeElement.Butterfly, 3);

            var p = ItemGrant.Resolve(PatternItem(), 2); // Butterfly Lv2 ×4（use_num=2 × count=2）
            ItemGrant.ApplyPattern(state, p);

            // AddDirect 经既有 AddToInventory 级联（4合1）：注入 4 个 Lv2 恰好满 MergeCount，
            // 沿单链向上合 1 个 Lv3，Lv2 清零（Lv3 仅 1 个 < MergeCount，不再继续）。
            // 本测验「注入落到既有收集区」，不复刻级联算法细节：断言总持有量随注入增加即可。
            Assert.AreEqual(0, state.InventoryCount(MergeElement.Butterfly, 2), "注入 4 个 Lv2 满 MergeCount，清零升级");
            Assert.AreEqual(1, state.InventoryCount(MergeElement.Butterfly, 3), "单链向上合出 1 个 Lv3");
            Assert.Greater(state.InventoryCount(MergeElement.Butterfly, 2)
                         + state.InventoryCount(MergeElement.Butterfly, 3), before, "注入应反映到收集区");
        }

        [Test]
        public void U5b_ApplyNumeric_LandsOnExistingFields()
        {
            var state = new MergeOrderState();
            state.Reset();

            // 经验：state.Exp += 5000
            int expBefore = state.Exp;
            Assert.IsTrue(ItemGrant.ApplyNumeric(state, ItemGrant.Resolve(ExpItem(), 1)));
            Assert.AreEqual(expBefore + 5000, state.Exp);

            // 钻石(num_id=3)无既有字段 → 返 false，不落
            var diamondItem = new ItemDef { UseEffect = 1, UseValue = 3, UseNum = 10 };
            Assert.IsFalse(ItemGrant.ApplyNumeric(state, ItemGrant.Resolve(diamondItem, 1)));
        }

        // ───────────────────────── 背包（B1–B5） ─────────────────────────

        [Test]
        public void B1_Stackable_AccumulatesOneSlot()
        {
            ItemConfigMgr.InitForTest(SampleItems()); // 30003 stacking=1
            var bag = new ItemBag();
            Assert.AreEqual(10, bag.Add(30003, 10));
            Assert.AreEqual(5, bag.Add(30003, 5));
            Assert.AreEqual(15, bag.Count(30003));
            Assert.AreEqual(1, bag.SlotUsed, "可叠同 id 占 1 格");
        }

        [Test]
        public void B2_StackCap999_AndFormat()
        {
            ItemConfigMgr.InitForTest(SampleItems());
            var bag = new ItemBag();
            int placed = bag.Add(30003, 1500);  // 夹 999
            Assert.AreEqual(999, placed, "实际放入 999");
            Assert.AreEqual(999, bag.Count(30003));
            Assert.AreEqual(0, bag.Add(30003, 100), "已满 999 再放返 0");

            Assert.AreEqual("999+", ItemBag.FormatCount(1500));
            Assert.AreEqual("999+", ItemBag.FormatCount(999));
            Assert.AreEqual("998", ItemBag.FormatCount(998));
        }

        [Test]
        public void B3_NonStackable_OccupiesPerSlot()
        {
            ItemConfigMgr.InitForTest(SampleItems()); // 30008 stacking=0
            var bag = new ItemBag();
            Assert.AreEqual(3, bag.Add(30008, 3));
            Assert.AreEqual(3, bag.SlotUsed, "不可叠 3 个占 3 格");
            Assert.AreEqual(3, bag.Count(30008));
        }

        [Test]
        public void B4_SlotCap100_Rejects()
        {
            ItemConfigMgr.InitForTest(SampleItems());
            var bag = new ItemBag();
            // 不可叠占满 100 格
            Assert.AreEqual(100, bag.Add(30008, 100));
            Assert.AreEqual(100, bag.SlotUsed);
            // 再放不可叠返 0
            Assert.AreEqual(0, bag.Add(30008, 5));
            // 再放新可叠 id（需 1 新格）返 0
            Assert.AreEqual(0, bag.Add(30003, 1));
            Assert.AreEqual(100, bag.SlotUsed, "拒绝后已有不变");
            Assert.AreEqual(100, bag.Count(30008));
        }

        [Test]
        public void B5_Remove_FreesSlot()
        {
            ItemConfigMgr.InitForTest(SampleItems());
            var bag = new ItemBag();
            bag.Add(30003, 5);   // 可叠 1 格
            bag.Add(30008, 2);   // 不可叠 2 格
            Assert.AreEqual(3, bag.SlotUsed);

            Assert.IsTrue(bag.Remove(30003, 5)); // 移除到 0
            Assert.AreEqual(0, bag.Count(30003), "id 消失");
            Assert.AreEqual(2, bag.SlotUsed, "释放可叠那格");

            Assert.IsTrue(bag.Remove(30008, 1));
            Assert.AreEqual(1, bag.SlotUsed);
            Assert.IsFalse(bag.Remove(30008, 5), "持有不足返 false 不改动");
            Assert.AreEqual(1, bag.Count(30008));
        }

        // ───────────────────────── 隔离（Z1，纯逻辑无 ConfigSystem） ─────────────────────────

        [Test]
        public void Z1_PureLogic_NoConfigSystem()
        {
            // InitForTest 注入后，全链 G/U/B 不触发 ConfigSystem / YooAsset。
            // 本测试能在 EditMode（无 Unity 运行时资源模块）跑通本身即证：未访问 ConfigSystem.Instance.Tables。
            ItemConfigMgr.InitForTest(SampleItems(), SampleRandomPool(), SampleSelectPool());
            Assert.AreEqual(GrantKind.Numeric, ItemGrant.Resolve(ItemConfigMgr.GetItem(30001), 1).Kind);
            Assert.IsNotNull(GiftOpener.RollRandom(ItemConfigMgr.GetGiftRandom(6001), new System.Random(1)));
            var bag = new ItemBag();
            Assert.AreEqual(5, bag.Add(30003, 5));
        }
    }
}
