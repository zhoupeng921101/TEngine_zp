using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Algorithms;
using GameLogic.BlockBlast.Core;
using GameLogic.Config;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// Luban 权重表回归测试：直接从 AssetDatabase 读 .bytes（绕过 YooAsset），
    /// 验证 136 行反序列化 + 转换 WeightConfigEntry + 灌入 DynamicWeightDiff 全链路。
    /// </summary>
    [TestFixture]
    public class WeightCfgLubanTests
    {
        private const string BytesPath = "Assets/AssetRaw/Configs/bytes/block_tbweightcfg.bytes";

        private List<WeightConfigEntry> LoadEntriesFromBytes()
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(BytesPath);
            Assert.IsNotNull(ta, $"找不到 {BytesPath}，请先运行 Luban 生成");
            var table = new GameConfig.block.TbWeightCfg(new Luban.ByteBuf(ta.bytes));
            var list = new List<WeightConfigEntry>(table.DataList.Count);
            foreach (var row in table.DataList) list.Add(WeightCfgConfigMgr.ToEntry(row));
            return list;
        }

        [Test]
        public void WeightCfg_Has136Tiers()
        {
            var entries = LoadEntriesFromBytes();
            Assert.AreEqual(136, entries.Count);
        }

        [Test]
        public void WeightCfg_Row1_MatchesSourceData()
        {
            var entries = LoadEntriesFromBytes();
            var e1 = entries.Find(e => e.Id == 1);
            Assert.IsNotNull(e1);
            Assert.AreEqual(299, e1.FillBlankOdds);
            Assert.AreEqual(55, e1.RandomOdds);
            Assert.AreEqual(75, e1.EntropyOdds);
            Assert.AreEqual(0, e1.EasyOdds);
            Assert.AreEqual(203, e1.HardOdds);
            Assert.AreEqual(0, e1.IntuitionOdds);
            Assert.AreEqual(49, e1.Clearboard);
            Assert.AreEqual(319, e1.Allunite);
            Assert.AreEqual(1000, e1.HighScoreMin);
            Assert.AreEqual(3795, e1.HighScoreMax);
            Assert.AreEqual(-9999, e1.FactorLow);
            Assert.AreEqual(-410, e1.FactorHigh);
        }

        [Test]
        public void WeightCfg_AllIdsUniqueAndSequential()
        {
            var entries = LoadEntriesFromBytes();
            var seen = new HashSet<int>();
            foreach (var e in entries)
            {
                Assert.IsTrue(seen.Add(e.Id), $"重复 id {e.Id}");
            }
            for (int i = 1; i <= 136; i++)
                Assert.IsTrue(seen.Contains(i), $"缺少 id {i}");
        }

        [Test]
        public void DynamicWeightDiff_InitFromLubanTable_Works()
        {
            Persistence.Provider = new InMemoryPersistenceProvider();

            var entries = LoadEntriesFromBytes();
            // 去单例化后:用固定随机源 + 内存持久化 new 一个逐局调度器实例。
            var dyn = new DynamicWeightDiff(new XorShift128PlusRng(2026), new InMemoryPersistenceProvider());
            dyn.Init(entries);
            dyn.BeginGame();

            Assert.IsTrue(dyn.IsInitialized());
            var board = new BinaryBoard();
            var res = dyn.OfferTrio(board, 5000);
            Assert.AreEqual(3, res.Ids.Length);
            foreach (int id in res.Ids) Assert.IsNotNull(BlockShapeMap.Get(id));
        }
    }
}
