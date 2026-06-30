using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using GameLogic.BlockBlast;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 方块皮肤切换（设计 50）单测，对应验收 §八 A 组 A1–A9：
    /// 状态机（初始彩色 / 首次转单色 / 后续换色 / 不退彩色）、防相邻重复、候选池域正确（不含留空编号）、
    /// 续存往返保真、缺字段 / 非法值保底、退化兜底、皮肤切换不污染全清结算。
    ///
    /// 全部锚在纯逻辑（BlockSkinState / BlockSkinCatalog / MergeMetaSave）+ 确定性 RandomSource，
    /// 不依赖 Unity 运行期 / YooAsset / 磁盘。RandomSource.SetSeed 注入确定性序列。
    /// </summary>
    [TestFixture]
    public class BlockSkinStateTests
    {
        // 构造测试候选池（小池，便于断言；模拟「真实存在集」语义）。
        private static readonly IReadOnlyList<int> TestPool = new[] { 7, 14, 29, 51, 247, 477 };

        [SetUp]
        public void SetUp()
        {
            // InMemory Provider 隔离真实 PlayerPrefs（整合用例触达 ResetForMergeOrder → Load）。
            Persistence.Provider = new InMemoryPersistenceProvider();
            RandomSource.SetSeed(20260622);
            // DynamicWeightDiff 现为 BlockGameState 的逐局实例字段,随 BlockGameState 释放,无独立单例可释。
            if (BlockGameState.IsValid) BlockGameState.Instance.Release();
        }

        [TearDown]
        public void TearDown()
        {
            if (BlockGameState.IsValid) BlockGameState.Instance.Release();
        }

        // ───────────── A1 初始 = 彩色 ─────────────
        [Test]
        public void A1_Initial_IsColored_Unselected()
        {
            var s = new BlockSkinState();
            Assert.AreEqual(SkinMode.Colored, s.Mode, "A1: 初始皮肤模式 = 彩色");
            Assert.AreEqual(BlockSkinState.Unselected, s.MonoId, "A1: 初始当前单色标识 = 未选");
            Assert.IsFalse(s.IsMono, "A1: 初始非单色态");
        }

        // ───────────── A2 首次全清转单色 ─────────────
        [Test]
        public void A2_FirstAllClear_TurnsMono_FromPool()
        {
            var s = new BlockSkinState();
            bool changed = s.OnAllClear(TestPool);

            Assert.IsTrue(changed, "A2: 首次全清后单色标识发生变化（彩色→单色）");
            Assert.AreEqual(SkinMode.Mono, s.Mode, "A2: 首次全清后皮肤模式 = 单色");
            Assert.AreNotEqual(BlockSkinState.Unselected, s.MonoId, "A2: 单色标识非未选");
            CollectionAssert.Contains((ICollection)ToList(TestPool), s.MonoId, "A2: 单色标识 ∈ 候选池");
        }

        // ───────────── A3 后续全清换色且不连续重复 ─────────────
        [Test]
        public void A3_SubsequentAllClear_ChangesAndNeverAdjacentRepeat()
        {
            var s = new BlockSkinState();
            s.OnAllClear(TestPool);          // 首次
            int prev = s.MonoId;

            // 单次后续全清：新标识 ≠ 上一张且 ∈ 池。
            s.OnAllClear(TestPool);
            Assert.AreNotEqual(prev, s.MonoId, "A3: 后续全清新标识 ≠ 上一张");
            CollectionAssert.Contains((ICollection)ToList(TestPool), s.MonoId, "A3: 新标识 ∈ 池");

            // 连续多次全清：任意相邻两次两两不等。
            int last = s.MonoId;
            for (int i = 0; i < 200; i++)
            {
                s.OnAllClear(TestPool);
                Assert.AreNotEqual(last, s.MonoId, $"A3: 第 {i} 次后续全清与上一张相邻重复");
                last = s.MonoId;
            }
        }

        // ───────────── A4 候选池域正确（采样全部 ∈ 真实集，不含留空编号）─────────────
        [Test]
        public void A4_RealCatalog_SamplesAllInExistingSet_NoGaps()
        {
            var s = new BlockSkinState();
            var existing = new HashSet<int>(BlockSkinCatalog.MonoIds);

            // 用真实候选池足量采样，全部落在真实存在集内。
            for (int i = 0; i < 5000; i++)
            {
                s.OnAllClear(BlockSkinCatalog.MonoIds);
                Assert.IsTrue(existing.Contains(s.MonoId),
                    $"A4: 采样标识 {s.MonoId} 不在真实存在集（疑似选到留空编号）");
            }

            // 显式断言越界编号不在候选池（有效区间 1..374，区间外均无对应文件）。
            foreach (int gap in new[] { 0, -1, 375, 400, 478, 999 })
                Assert.IsFalse(BlockSkinCatalog.Contains(gap), $"A4: 越界编号 {gap} 不应在候选池");

            // 真实候选池规模与真实文件集一致（374 张）。
            Assert.AreEqual(374, BlockSkinCatalog.MonoIds.Count, "A4: 候选池规模 = 真实文件集 374 张");
        }

        // ───────────── A5 续存往返保真 ─────────────
        [Test]
        public void A5_Persistence_RoundTrip_Mono_Fidelity()
        {
            var src = new BlockSkinState();
            src.OnAllClear(TestPool);  // 进入单色态某张
            int srcId = src.MonoId;

            // Export → Serialize → Deserialize → Import（同设计 14 整盘往返口径）。
            var dto = new MergeMetaSave { version = MergeMetaPersistence.CurrentVersion };
            src.Export(dto);
            string json = MergeMetaPersistence.Serialize(dto);
            var back = MergeMetaPersistence.Deserialize(json);
            Assert.IsNotNull(back, "A5: 往返反序列化非 null");

            var dst = new BlockSkinState();
            dst.Import(back, TestPool);

            Assert.AreEqual(SkinMode.Mono, dst.Mode, "A5: 往返后皮肤模式 = 单色");
            Assert.AreEqual(srcId, dst.MonoId, "A5: 往返后当前单色标识逐一相等");
        }

        [Test]
        public void A5b_Persistence_RoundTrip_Colored_Fidelity()
        {
            var src = new BlockSkinState(); // 彩色态
            var dto = new MergeMetaSave { version = MergeMetaPersistence.CurrentVersion };
            src.Export(dto);
            var dst = new BlockSkinState();
            dst.OnAllClear(TestPool);       // 先弄脏为单色,验 Import 会覆盖回彩色
            dst.Import(dto, TestPool);

            Assert.AreEqual(SkinMode.Colored, dst.Mode, "A5b: 彩色态往返后仍彩色");
            Assert.AreEqual(BlockSkinState.Unselected, dst.MonoId, "A5b: 彩色态往返后未选");
        }

        // ───────────── A6 缺字段 / 非法值保底 ─────────────
        [Test]
        public void A6a_MissingFields_FallbackToColored()
        {
            // 旧档 / 全新档:无皮肤态字段 → JsonUtility 给缺省（skinMono=false, skinMonoId=0）。
            var dto = new MergeMetaSave { version = MergeMetaPersistence.CurrentVersion };
            // 不写 skinMono/skinMonoId（保持缺省）
            var s = new BlockSkinState();
            s.OnAllClear(TestPool);          // 先弄脏,验 Import 覆盖
            s.Import(dto, TestPool);

            Assert.AreEqual(SkinMode.Colored, s.Mode, "A6a: 缺字段 → 彩色态");
            Assert.AreEqual(BlockSkinState.Unselected, s.MonoId, "A6a: 缺字段 → 未选");
        }

        [Test]
        public void A6b_IllegalMonoId_FallbackToMonoReroll()
        {
            // 单色态但当前标识指向候选池外编号（旧档 / 篡改 / 资源缺失）。
            var dto = new MergeMetaSave
            {
                version = MergeMetaPersistence.CurrentVersion,
                skinMono = true,
                skinMonoId = 99999, // 候选池外
            };
            var s = new BlockSkinState();
            s.Import(dto, TestPool);

            Assert.AreEqual(SkinMode.Mono, s.Mode, "A6b: 非法标识默认兜底 = 单色态（保「已达成全清」语义）");
            CollectionAssert.Contains((ICollection)ToList(TestPool), s.MonoId, "A6b: 兜底重随机选的标识 ∈ 候选池");
            Assert.AreNotEqual(99999, s.MonoId, "A6b: 不保留非法标识");
        }

        [Test]
        public void A6b_IllegalMonoId_EmptyPool_FallbackToColored()
        {
            // 边界:单色态非法标识 + 候选池异常为空 → 退回彩色 + 未选（不抛、不留空标识）。
            var dto = new MergeMetaSave
            {
                version = MergeMetaPersistence.CurrentVersion,
                skinMono = true,
                skinMonoId = 99999,
            };
            var s = new BlockSkinState();
            s.Import(dto, new int[0]);
            Assert.AreEqual(SkinMode.Colored, s.Mode, "A6b-empty: 池空兜底退回彩色");
            Assert.AreEqual(BlockSkinState.Unselected, s.MonoId, "A6b-empty: 池空兜底未选");
        }

        [Test]
        public void A6_NullDto_KeepsCurrent()
        {
            var s = new BlockSkinState();
            s.OnAllClear(TestPool);
            int before = s.MonoId;
            s.Import(null, TestPool);  // null DTO 不动现状
            Assert.AreEqual(SkinMode.Mono, s.Mode, "A6: null DTO 保持现状模式");
            Assert.AreEqual(before, s.MonoId, "A6: null DTO 保持现状标识");
        }

        // ───────────── A7 单色不退回彩色 ─────────────
        [Test]
        public void A7_OnceMono_NeverRevertsToColored()
        {
            var s = new BlockSkinState();
            s.OnAllClear(TestPool);
            for (int i = 0; i < 100; i++)
            {
                s.OnAllClear(TestPool);
                Assert.AreEqual(SkinMode.Mono, s.Mode, $"A7: 第 {i} 次全清后仍单色,无路径退回彩色");
            }
        }

        // ───────────── A8 候选池退化兜底（仅 1 张）─────────────
        [Test]
        public void A8_SingleCandidate_KeepsCurrent_NoException()
        {
            var single = new[] { 42 };
            var s = new BlockSkinState();

            s.OnAllClear(single);                       // 首次:池仅 1 张 → 选中 42
            Assert.AreEqual(SkinMode.Mono, s.Mode, "A8: 首次全清转单色");
            Assert.AreEqual(42, s.MonoId, "A8: 单张池选中那张");

            // 后续全清:排除当前后可选集为空 → 保持当前不变(不抛/不死循环)。
            Assert.DoesNotThrow(() => s.OnAllClear(single), "A8: 单张池后续全清不抛异常");
            Assert.AreEqual(42, s.MonoId, "A8: 单张池后续全清保持当前不变");
        }

        [Test]
        public void A8b_EmptyPool_NoChange_NoException()
        {
            var s = new BlockSkinState();
            Assert.DoesNotThrow(() => s.OnAllClear(new int[0]), "A8b: 空池全清不抛");
            Assert.AreEqual(SkinMode.Colored, s.Mode, "A8b: 空池全清保持彩色(无张可转)");
            Assert.DoesNotThrow(() => s.OnAllClear(null), "A8b: null 池全清不抛");
        }

        // ───────────── A9 皮肤切换不污染全清结算 ─────────────
        // A9 锚在结构隔离:OnAllClear 只动皮肤态字段,不接触 MergeOrderState 任何结算字段。
        // 此处构造性验证:对 MergeOrderState 触发皮肤切换,断言其结算相关字段不被皮肤逻辑改动。
        [Test]
        public void A9_SkinSwitch_DoesNotTouchSettlementFields()
        {
            var m = new MergeOrderState();
            m.Reset();
            // 记录结算相关字段快照
            int goddessRating = m.GoddessRating;
            int goddessLevel = m.GoddessLevel;
            int blindBox = m.BlindBoxCount;
            bool armed = m.AllClearArmed;
            int combo = m.ComboChain;
            int soul = m.Soul;
            int energy = m.Energy;

            // 仅触发皮肤切换(全清的纯视觉附加,不走 ClearSettlement.Settle)。
            m.Skin.OnAllClear(BlockSkinCatalog.MonoIds);

            Assert.AreEqual(goddessRating, m.GoddessRating, "A9: 皮肤切换不改女神好评条");
            Assert.AreEqual(goddessLevel, m.GoddessLevel, "A9: 皮肤切换不改女神等级");
            Assert.AreEqual(blindBox, m.BlindBoxCount, "A9: 皮肤切换不改盲盒计数");
            Assert.AreEqual(armed, m.AllClearArmed, "A9: 皮肤切换不改全清武装位");
            Assert.AreEqual(combo, m.ComboChain, "A9: 皮肤切换不改连消链");
            Assert.AreEqual(soul, m.Soul, "A9: 皮肤切换不改灵力");
            Assert.AreEqual(energy, m.Energy, "A9: 皮肤切换不改体力");
            // 反向:皮肤态确实推进了。
            Assert.AreEqual(SkinMode.Mono, m.Skin.Mode, "A9: 皮肤态本身已转单色");
        }

        // ───────────── 整合:MergeOrderState 经 ExportMeta/ImportMeta 续存皮肤态 ─────────────
        [Test]
        public void Integration_MergeOrderState_PersistsSkinViaMeta()
        {
            var src = new MergeOrderState();
            src.Reset();
            src.Skin.OnAllClear(BlockSkinCatalog.MonoIds); // 单色态某张
            int srcId = src.Skin.MonoId;

            var dto = src.ExportMeta("2026-06-22");
            string json = MergeMetaPersistence.Serialize(dto);
            var back = MergeMetaPersistence.Deserialize(json);

            var dst = new MergeOrderState();
            dst.Reset();                       // 缺省彩色
            dst.ImportMeta(back, "2026-06-22");

            Assert.AreEqual(SkinMode.Mono, dst.Skin.Mode, "整合: ExportMeta/ImportMeta 续存皮肤模式");
            Assert.AreEqual(srcId, dst.Skin.MonoId, "整合: 续存当前单色标识保真");
        }

        // ───────────── 彩色态贴图映射（设计 50 §二：彩色态按类型贴 default_skin 纹理）─────────────
        // colorIdx(=BlockColor 0..7 Blue/Green/Yellow/Orange/Red/Steel/Teal/Purple)→ blocks_main_<编号> 1:1。
        [Test]
        public void ColoredSpriteName_MapsEachColorIdxToExpectedDefaultSkin()
        {
            // 期望映射(boss 看图比色已定),编号与 default_skin 实际文件集(1/6/14/15/16/18/19/21)一一对应。
            var expected = new[]
            {
                (0, "blocks_main_14"), // Blue
                (1, "blocks_main_15"), // Green
                (2, "blocks_main_16"), // Yellow
                (3, "blocks_main_21"), // Orange
                (4, "blocks_main_1"),  // Red
                (5, "blocks_main_6"),  // Steel
                (6, "blocks_main_18"), // Teal
                (7, "blocks_main_19"), // Purple
            };
            foreach (var (idx, name) in expected)
                Assert.AreEqual(name, BlockSkinCatalog.ColoredSpriteName(idx), $"colorIdx {idx} 应映射到 {name}");
        }

        // 越界 colorIdx 取首项保底、不抛(与 BlockLayout.ColorOf 越界归 0 同口径)。
        [Test]
        public void ColoredSpriteName_OutOfRange_FallsBackToFirst_NoException()
        {
            string first = BlockSkinCatalog.ColoredSpriteName(0);
            Assert.DoesNotThrow(() => BlockSkinCatalog.ColoredSpriteName(-1));
            Assert.DoesNotThrow(() => BlockSkinCatalog.ColoredSpriteName(8));
            Assert.DoesNotThrow(() => BlockSkinCatalog.ColoredSpriteName(int.MaxValue));
            Assert.AreEqual(first, BlockSkinCatalog.ColoredSpriteName(-1), "负越界取首项");
            Assert.AreEqual(first, BlockSkinCatalog.ColoredSpriteName(8), "上越界取首项");
        }

        // 帮助:IReadOnlyList<int> → List(CollectionAssert 需 ICollection)。
        private static List<int> ToList(IReadOnlyList<int> src)
        {
            var list = new List<int>(src.Count);
            foreach (var x in src) list.Add(x);
            return list;
        }
    }
}
