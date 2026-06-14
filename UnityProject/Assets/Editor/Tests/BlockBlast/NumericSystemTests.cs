using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameLogic.Config;
using GameLogic.BlockBlast.Numeric;
using GameLogic.BlockBlastUI;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 数值底层系统验收测试（设计 15 §六）。
    /// 格式化(F)/注册表(R) = 纯逻辑，不碰文件 / Unity 运行时；
    /// 配置表(C) = AssetDatabase 直读 num_tbnum.bytes（仿 WeightCfgLubanTests，绕 YooAsset）。
    /// </summary>
    [TestFixture]
    public class NumericSystemTests
    {
        private const string BytesPath = "Assets/AssetRaw/Configs/bytes/num_tbnum.bytes";

        // ───────────────────────── 格式化（F1–F9，纯逻辑无依赖） ─────────────────────────

        [Test] public void F1_Zero_AsRaw() => Assert.AreEqual("0", NumericFormat.Abbreviate(0));
        [Test] public void F2_999_AsRaw() => Assert.AreEqual("999", NumericFormat.Abbreviate(999));
        [Test] public void F3_1000_AsK_NoTrailingZero() => Assert.AreEqual("1K", NumericFormat.Abbreviate(1000));
        [Test] public void F4_1500_AsKOneDecimal() => Assert.AreEqual("1.5K", NumericFormat.Abbreviate(1500));
        [Test] public void F5_999999_TruncatedNoCarry() => Assert.AreEqual("999.9K", NumericFormat.Abbreviate(999999));
        [Test] public void F6_1000000_AsM() => Assert.AreEqual("1M", NumericFormat.Abbreviate(1000000));
        [Test] public void F7_1500000_AsMOneDecimal() => Assert.AreEqual("1.5M", NumericFormat.Abbreviate(1500000));
        [Test] public void F8_9999999_AsMTruncated() => Assert.AreEqual("9.9M", NumericFormat.Abbreviate(9999999));
        [Test] public void F9_HundredMillion_StillM() => Assert.AreEqual("100M", NumericFormat.Abbreviate(100000000));

        [Test]
        public void Format_NegativeKeepsSign()
        {
            Assert.AreEqual("-1.5K", NumericFormat.Abbreviate(-1500));
        }

        [Test]
        public void Format_1999_TruncatesNotRounds()
        {
            Assert.AreEqual("1.9K", NumericFormat.Abbreviate(1999));
        }

        // ───────────────────────── 配置表（C1–C3，AssetDatabase 直读 .bytes） ─────────────────────────

        private GameConfig.num.TbNum LoadTableFromBytes()
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(BytesPath);
            Assert.IsNotNull(ta, $"找不到 {BytesPath}，请先运行 Luban 导表");
            return new GameConfig.num.TbNum(new Luban.ByteBuf(ta.bytes));
        }

        [Test]
        public void C1_Table_Has4Rows()
        {
            var table = LoadTableFromBytes();
            Assert.AreEqual(4, table.DataList.Count);
        }

        [Test]
        public void C2_Piety_FieldsMatchSource()
        {
            var table = LoadTableFromBytes();
            var piety = table.GetOrDefault(2);
            Assert.IsNotNull(piety, "id=2 行应存在");
            Assert.AreEqual(2, (int)piety.NumType);        // PIETY=2
            Assert.AreEqual(3, piety.Quality);             // 紫
            Assert.AreEqual("icon_piety", piety.Icon);
            Assert.AreEqual(100002, piety.Name);
            Assert.AreEqual(200002, piety.Desc);
        }

        [Test]
        public void C3_NumTypes_Are1To4_IdsUnique()
        {
            var table = LoadTableFromBytes();
            var ids = new HashSet<int>();
            var types = new HashSet<int>();
            foreach (var row in table.DataList)
            {
                Assert.IsTrue(ids.Add(row.Id), $"重复 id {row.Id}");
                types.Add((int)row.NumType);
            }
            for (int i = 1; i <= 4; i++)
            {
                Assert.IsTrue(ids.Contains(i), $"缺少 id {i}");
                Assert.IsTrue(types.Contains(i), $"缺少 num_type {i}");
            }
            Assert.AreEqual(4, types.Count, "num_type 恰为 {1,2,3,4} 各一");
        }

        // ───────────────────────── 注册表 / helper（R1–R4，InitForTest 路径纯逻辑） ─────────────────────────

        /// <summary>仿真表里 4 行的 entry 列表（与 num.xlsx 样例同值）。</summary>
        private static List<NumericEntry> SampleEntries() => new List<NumericEntry>
        {
            new NumericEntry { NumId = 1, DescTextId = 200001, NameTextId = 100001, IconName = "icon_exp",     NumType = 1, Quality = 1 },
            new NumericEntry { NumId = 2, DescTextId = 200002, NameTextId = 100002, IconName = "icon_piety",   NumType = 2, Quality = 3 },
            new NumericEntry { NumId = 3, DescTextId = 200003, NameTextId = 100003, IconName = "icon_diamond", NumType = 3, Quality = 4 },
            new NumericEntry { NumId = 4, DescTextId = 200004, NameTextId = 100004, IconName = "icon_energy",  NumType = 4, Quality = 2 },
        };

        [SetUp]
        public void ResetMgr() => NumericConfigMgr.ResetForTest();

        [TearDown]
        public void CleanupMgr() => NumericConfigMgr.ResetForTest();

        [Test]
        public void R1_ToEntry_PreservesFields()
        {
            // 从真实 Luban 行转 POCO，逐字段比对（既验 ToEntry 也验 .bytes 对得上）。
            var table = LoadTableFromBytes();
            var row = table.GetOrDefault(2);
            var e = NumericConfigMgr.ToEntry(row);
            Assert.AreEqual(row.Id, e.NumId);
            Assert.AreEqual(row.Desc, e.DescTextId);
            Assert.AreEqual(row.Name, e.NameTextId);
            Assert.AreEqual(row.Icon, e.IconName);
            Assert.AreEqual((int)row.NumType, e.NumType);
            Assert.AreEqual(row.Quality, e.Quality);
        }

        [Test]
        public void R2_Get_HitAndMiss()
        {
            NumericConfigMgr.InitForTest(SampleEntries());
            var piety = NumericConfigMgr.Get(2);
            Assert.IsNotNull(piety);
            Assert.AreEqual(2, piety.NumId);
            Assert.AreEqual("icon_piety", piety.IconName);
            Assert.IsNull(NumericConfigMgr.Get(999), "未命中应返 null 不抛");
        }

        [Test]
        public void R3_GetByType_HitAndEmpty()
        {
            NumericConfigMgr.InitForTest(SampleEntries());
            var energy = NumericConfigMgr.GetByType(4);
            Assert.AreEqual(1, energy.Count, "体力(num_type=4)恰 1 条");
            Assert.AreEqual(4, energy[0].NumId);
            var none = NumericConfigMgr.GetByType(99);
            Assert.IsNotNull(none);
            Assert.AreEqual(0, none.Count, "未命中类型应返空集合不抛");
        }

        [Test]
        public void R4_Helper_FormatVertical()
        {
            NumericConfigMgr.InitForTest(SampleEntries());
            Assert.AreEqual("999.9K", NumericDisplay.Format(999999));
            StringAssert.Contains("1.5K", NumericDisplay.FormatWith(NumericConfigMgr.Piety, 1500));
            Assert.AreEqual("icon_piety", NumericDisplay.IconName(2));
        }

        // ───────────────────────── 边界（Z1，纯逻辑不依赖运行时） ─────────────────────────

        [Test]
        public void Z1_PureLogic_NoConfigSystem()
        {
            // InitForTest 注入后，Get/Format 完全不触发 ConfigSystem / YooAsset。
            // 本测试能在 EditMode（无 Unity 运行时资源模块）跑通本身即证明：未访问 ConfigSystem.Instance.Tables。
            NumericConfigMgr.InitForTest(SampleEntries());
            Assert.AreEqual("icon_exp", NumericConfigMgr.Get(1).IconName);
            Assert.AreEqual("1.5M", NumericFormat.Abbreviate(1500000));
            Assert.AreEqual(Color.white, NumericDisplay.QualityColor(1));
        }
    }
}
