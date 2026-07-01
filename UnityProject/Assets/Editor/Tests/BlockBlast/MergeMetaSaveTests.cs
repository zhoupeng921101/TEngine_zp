using System;
using NUnit.Framework;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 跨会话磁盘存档（设计 14）单测：序列化往返保真、bool[12] 往返、无存档缺省、缺字段补缺、
    /// 版本迁移 / 未来版降级重置、非法截断兜底、祈愿今日已用次数作投影往返、
    /// 旧路径零回归、MergeOrderState 仍纯逻辑。
    /// 全部锚在同步纯方法（ExportMeta/ImportMeta/Serialize/Deserialize/Migrate）+ InMemory Provider，
    /// 不依赖真实磁盘 / 不依赖 UniTask 运行。SetUp 仿 TempleRepairTests（InMemory Provider）。
    ///
    /// 祈愿每日重置已迁服务端权威(祈愿服务端权威·客户端段):客户端不再本地按日期跨天归零 wishUsedToday,
    /// 它降为投影(缓存往返保真、由登录快照覆盖为权威当日值),故原「跨天/同日/缺日期本地重置」用例已删。
    /// </summary>
    [TestFixture]
    public class MergeMetaSaveTests
    {
        private const string Today = "2026-06-14";
        private const string Yesterday = "2026-06-13";

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

        // 构造一个所有进盘字段都是非缺省值的 state（用于往返保真）。
        private static MergeOrderState NonDefaultState()
        {
            var m = FreshState();
            m.Soul = 137;
            m.Piety = 2480;
            m.Exp = 1730;
            m.UnlockedChapter = 4;
            m.NextRepairIndex = 3;
            m.TempleRepaired[0] = true;
            m.TempleRepaired[1] = true;
            m.TempleRepaired[2] = true;
            m.TempleDecorated[0] = true;
            m.TempleDecorated[2] = true;
            m.BlindBoxCount = 5;
            m.GoddessRating = 2;
            m.GoddessLevel = 3;
            m.WishUsedToday = 1;
            return m;
        }

        // ───────────── A1 序列化往返保真（全元字段非缺省）─────────────

        [Test]
        public void A1_RoundTrip_AllMetaFields_Equal_SameDay()
        {
            var src = NonDefaultState();

            // Export → Serialize → Deserialize → ImportMeta 到新 state（同一天，无跨天干扰）。
            var dto = src.ExportMeta();
            string json = MergeMetaPersistence.Serialize(dto);
            var back = MergeMetaPersistence.Deserialize(json);
            Assert.IsNotNull(back, "往返反序列化不应为 null");

            var dst = FreshState();
            dst.ImportMeta(back);

            // §3.1「是」的 11 项逐一相等。
            Assert.AreEqual(src.Soul, dst.Soul, "Soul");
            Assert.AreEqual(src.Piety, dst.Piety, "Piety");
            Assert.AreEqual(src.Exp, dst.Exp, "Exp");
            Assert.AreEqual(src.UnlockedChapter, dst.UnlockedChapter, "UnlockedChapter");
            Assert.AreEqual(src.NextRepairIndex, dst.NextRepairIndex, "NextRepairIndex");
            Assert.AreEqual(src.BlindBoxCount, dst.BlindBoxCount, "BlindBoxCount");
            Assert.AreEqual(src.GoddessRating, dst.GoddessRating, "GoddessRating");
            Assert.AreEqual(src.GoddessLevel, dst.GoddessLevel, "GoddessLevel");
            Assert.AreEqual(src.WishUsedToday, dst.WishUsedToday, "WishUsedToday(同日不重置)");
            // 神庙两数组逐位相等（A2 另测，这里一并核对）。
            CollectionAssert.AreEqual(src.TempleRepaired, dst.TempleRepaired, "TempleRepaired");
            CollectionAssert.AreEqual(src.TempleDecorated, dst.TempleDecorated, "TempleDecorated");
        }

        // ───────────── A2 bool[12] 往返保真 ─────────────

        [Test]
        public void A2_BoolArrays_RoundTrip_Length12_BitwiseEqual()
        {
            var src = FreshState();
            // 设若干 true（含首位、末位、中段），覆盖边界。
            int[] repairedTrue = { 0, 5, 11 };
            int[] decoratedTrue = { 1, 2, 11 };
            foreach (int i in repairedTrue) src.TempleRepaired[i] = true;
            foreach (int i in decoratedTrue) src.TempleDecorated[i] = true;

            var dto = src.ExportMeta();
            string json = MergeMetaPersistence.Serialize(dto);
            var back = MergeMetaPersistence.Deserialize(json);
            var dst = FreshState();
            dst.ImportMeta(back);

            Assert.AreEqual(TempleConfig.HallCount, dst.TempleRepaired.Length, "TempleRepaired 长度仍 12");
            Assert.AreEqual(TempleConfig.HallCount, dst.TempleDecorated.Length, "TempleDecorated 长度仍 12");
            for (int i = 0; i < TempleConfig.HallCount; i++)
            {
                Assert.AreEqual(src.TempleRepaired[i], dst.TempleRepaired[i], $"TempleRepaired[{i}] 逐位相等");
                Assert.AreEqual(src.TempleDecorated[i], dst.TempleDecorated[i], $"TempleDecorated[{i}] 逐位相等");
            }
        }

        // ───────────── A3 无存档 → 缺省（首次游玩）─────────────

        [Test]
        public void A3_NoSave_Deserialize_ReturnsNull()
        {
            Assert.IsNull(MergeMetaPersistence.Deserialize(null), "null → null");
            Assert.IsNull(MergeMetaPersistence.Deserialize(""), "空串 → null");
        }

        [Test]
        public void A3_ImportNull_KeepsResetDefaults()
        {
            var m = FreshState();
            // ImportMeta(null) 应保持 Reset 缺省（等价首次）。
            m.ImportMeta(null);
            Assert.AreEqual(0, m.Piety, "Piety=0");
            Assert.AreEqual(0, m.Soul, "Soul=0");
            Assert.AreEqual(0, m.Exp, "Exp=0");
            Assert.AreEqual(0, m.UnlockedChapter, "UnlockedChapter=0");
            Assert.AreEqual(0, m.NextRepairIndex, "NextRepairIndex=0");
            Assert.AreEqual(1, m.GoddessLevel, "GoddessLevel=1(从 1 起)");
            Assert.IsFalse(m.IsTempleAllRepaired, "神庙全未修");
            for (int i = 0; i < TempleConfig.HallCount; i++)
                Assert.IsFalse(m.TempleRepaired[i], $"第 {i} 厅未修");
        }

        // ───────────── A4 缺字段补缺（version 匹配但 JSON 缺字段）─────────────

        [Test]
        public void A4_MissingFields_RebuiltDefaults_NoThrow()
        {
            // 手造缺 templeRepaired/templeDecorated/goddessLevel 的 JSON（仅含 version + piety）。
            string json = "{\"version\":1,\"piety\":500}";
            var dto = MergeMetaPersistence.Deserialize(json);
            Assert.IsNotNull(dto, "合法 JSON 不应 null");
            // JsonUtility 对缺失数组给 null、缺失 int 给 0。
            Assert.IsNull(dto.templeRepaired, "缺字段 → null（JsonUtility 行为）");
            Assert.AreEqual(0, dto.goddessLevel, "缺字段 → 0");

            var m = FreshState();
            Assert.DoesNotThrow(() => m.ImportMeta(dto), "ImportMeta 不抛");
            Assert.AreEqual(TempleConfig.HallCount, m.TempleRepaired.Length, "数组重建长 12");
            Assert.AreEqual(TempleConfig.HallCount, m.TempleDecorated.Length, "装饰数组重建长 12");
            for (int i = 0; i < TempleConfig.HallCount; i++)
            {
                Assert.IsFalse(m.TempleRepaired[i], "重建数组全 false");
                Assert.IsFalse(m.TempleDecorated[i], "重建装饰数组全 false");
            }
            Assert.AreEqual(1, m.GoddessLevel, "goddessLevel<1 → 夹到 1");
            Assert.AreEqual(500, m.Piety, "已有字段照常导入");
        }

        // ───────────── A5 旧版本迁移 ─────────────

        [Test]
        public void A5_OldVersion_Migrate_FieldsBackfilled_VersionNormalized()
        {
            // version=0（< CurrentVersion），缺数组、goddessLevel 缺省。
            string json = "{\"version\":0,\"piety\":120,\"goddessLevel\":0}";
            var dto = MergeMetaPersistence.Deserialize(json);
            Assert.AreEqual(0, dto.version, "读出 version=0");

            bool usable = MergeMetaPersistence.Migrate(dto);
            Assert.IsTrue(usable, "旧档可迁移");
            Assert.AreEqual(MergeMetaPersistence.CurrentVersion, dto.version, "version 归一为 CurrentVersion");

            var m = FreshState();
            Assert.DoesNotThrow(() => m.ImportMeta(dto));
            Assert.AreEqual(120, m.Piety, "字段照常导入");
            Assert.AreEqual(1, m.GoddessLevel, "goddessLevel 补缺合理(夹到 1)");
            Assert.AreEqual(TempleConfig.HallCount, m.TempleRepaired.Length, "数组补缺长 12");
        }

        // ───────────── A6 未来版本降级重置 ─────────────

        [Test]
        public void A6_FutureVersion_Migrate_ReturnsFalse_LoadGivesNull()
        {
            string json = "{\"version\":99,\"piety\":9999}";
            var dto = MergeMetaPersistence.Deserialize(json);
            Assert.AreEqual(99, dto.version, "读出 version=99");
            Assert.IsFalse(MergeMetaPersistence.Migrate(dto), "未来档 Migrate → false(不冒险用错位数据)");

            // 经存储层 Load 路径:未来档 → 返回 null(调用方走缺省重置,等价首次)。
            Persistence.Provider.Set(MergeMetaPersistence.StorageKey, json);
            var loaded = MergeMetaPersistence.Load();
            Assert.IsNull(loaded, "未来档 Load → null → 缺省重置");
        }

        // ───────────── A7 非法 / 截断档兜底 ─────────────

        [Test]
        public void A7_GarbageJson_Deserialize_ReturnsNull()
        {
            Assert.IsNull(MergeMetaPersistence.Deserialize("not a json {{{"), "非 JSON → null");
            Assert.IsNull(MergeMetaPersistence.Deserialize("12345"), "纯数字非对象 JSON → null 或被吞");
        }

        [Test]
        public void A7_OutOfRangeFields_ClampedToInvariants_NoThrow()
        {
            // nextRepairIndex=99(越界)、goddessLevel=0(<1)、神庙数组长度错(给 3 元素)。
            var dto = new MergeMetaSave
            {
                version = MergeMetaPersistence.CurrentVersion,
                nextRepairIndex = 99,
                goddessLevel = 0,
                templeRepaired = new bool[] { true, false, true }, // 长度 3 ≠ 12
                templeDecorated = null,
            };

            var m = FreshState();
            Assert.DoesNotThrow(() => m.ImportMeta(dto), "越界输入 ImportMeta 不抛");
            Assert.AreEqual(TempleConfig.HallCount, m.NextRepairIndex, "nextRepairIndex 夹到 [0,HallCount]");
            Assert.IsTrue(m.NextRepairIndex >= 0 && m.NextRepairIndex <= TempleConfig.HallCount, "index 落在合法区间");
            Assert.AreEqual(1, m.GoddessLevel, "goddessLevel 夹到 ≥1");
            Assert.AreEqual(TempleConfig.HallCount, m.TempleRepaired.Length, "长度错的数组重建为长 12");
            for (int i = 0; i < TempleConfig.HallCount; i++)
                Assert.IsFalse(m.TempleRepaired[i], "重建数组全 false(不带脏数据进玩法)");
        }

        [Test]
        public void A7_NegativeIndex_ClampedToZero()
        {
            var dto = new MergeMetaSave
            {
                version = MergeMetaPersistence.CurrentVersion,
                nextRepairIndex = -5,
                goddessLevel = 2,
            };
            var m = FreshState();
            m.ImportMeta(dto);
            Assert.AreEqual(0, m.NextRepairIndex, "负 index 夹到 0");
        }

        // ───────────── A8 祈愿今日已用次数作投影:ImportMeta 读缓存值原样、不按日期本地重置 ─────────────
        // 祈愿每日重置已迁服务端权威(祈愿服务端权威·客户端段):客户端 wishUsedToday 降为投影,
        // ImportMeta 无论缓存日期是昨天/今天/缺失,一律读缓存值原样(权威当日值由登录快照覆盖),不再本地跨天归零。

        [Test]
        public void A8_Wish_ReadAsProjection_StaleDate_NoLocalReset()
        {
            // 缓存日期是「昨天」(旧客户端会本地重置为 0),现在应原样读出 3(不本地重置)。
            var dto = new MergeMetaSave
            {
                version = MergeMetaPersistence.CurrentVersion,
                wishUsedToday = 3,
                lastWishResetDate = Yesterday,
                goddessLevel = 1,
            };
            var m = FreshState();
            m.ImportMeta(dto);
            Assert.AreEqual(3, m.WishUsedToday, "投影:昨天日期的缓存值原样读出,不本地跨天归零");
        }

        [Test]
        public void A8_Wish_ReadAsProjection_SameDate_KeptAsIs()
        {
            var dto = new MergeMetaSave
            {
                version = MergeMetaPersistence.CurrentVersion,
                wishUsedToday = 2,
                lastWishResetDate = Today,
                goddessLevel = 1,
            };
            var m = FreshState();
            m.ImportMeta(dto);
            Assert.AreEqual(2, m.WishUsedToday, "投影:今日缓存值原样读出");
        }

        [Test]
        public void A8_Wish_ReadAsProjection_MissingDate_KeptAsIs()
        {
            var dto = new MergeMetaSave
            {
                version = MergeMetaPersistence.CurrentVersion,
                wishUsedToday = 3,
                lastWishResetDate = null, // 旧档无日期
                goddessLevel = 1,
            };
            var m = FreshState();
            m.ImportMeta(dto);
            Assert.AreEqual(3, m.WishUsedToday, "投影:缺日期的缓存值原样读出,不本地重置");
        }

        [Test]
        public void A8_Wish_NegativeProjection_ClampedToZero()
        {
            var dto = new MergeMetaSave
            {
                version = MergeMetaPersistence.CurrentVersion,
                wishUsedToday = -5, // 篡改
                goddessLevel = 1,
            };
            var m = FreshState();
            m.ImportMeta(dto);
            Assert.AreEqual(0, m.WishUsedToday, "投影:负值(篡改)夹到 0");
        }

        // ───────────── A12 旧路径零回归（含存储层往返）─────────────

        [Test]
        public void A12_FreshLoad_NoSave_EquivalentToFirstPlay()
        {
            // Provider 空(SetUp 注入的 InMemory 无键)→ Load 返回 null → ResetForMergeOrder 保持 Reset 缺省。
            var loaded = MergeMetaPersistence.Load();
            Assert.IsNull(loaded, "无存档 Load → null");

            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board); // 内部 Load 无档 → 元层缺省
            var m = s.MergeState;
            Assert.AreEqual(0, m.Piety, "无存档时进入 merge-order:Piety 仍 0(等价首次)");
            Assert.AreEqual(1, m.GoddessLevel, "GoddessLevel 仍 1");
            Assert.AreEqual(0, m.NextRepairIndex, "从第 0 厅起");
            s.Release();
        }

        [Test]
        public void A12_StorageLayer_RoundTrip_ViaProvider()
        {
            // 经存储层(Set → Load)往返:写一份 DTO 到 Provider,Load 读回应等值(同日)。
            var src = NonDefaultState();
            var dto = src.ExportMeta();
            Persistence.Provider.Set(MergeMetaPersistence.StorageKey, MergeMetaPersistence.Serialize(dto));

            var loaded = MergeMetaPersistence.Load();
            Assert.IsNotNull(loaded, "Load 读回非 null");
            var dst = FreshState();
            dst.ImportMeta(loaded);
            Assert.AreEqual(src.Piety, dst.Piety, "经 Provider 往返 Piety 保真");
            Assert.AreEqual(src.BlindBoxCount, dst.BlindBoxCount, "经 Provider 往返盲盒计数保真");
            CollectionAssert.AreEqual(src.TempleRepaired, dst.TempleRepaired, "经 Provider 往返神庙数组保真");
        }

        [Test]
        public void A12_LoadedSave_OverwritesMetaOnEnter()
        {
            // 写一份存档 → 进入 merge-order 应被 ImportMeta 覆盖(元层来自存档,非缺省)。
            var src = NonDefaultState();
            var dto = src.ExportMeta();
            Persistence.Provider.Set(MergeMetaPersistence.StorageKey, MergeMetaPersistence.Serialize(dto));

            var s = BlockGameState.Instance;
            var board = new BinaryBoard();
            s.ResetForMergeOrder(board);
            var m = s.MergeState;
            Assert.AreEqual(src.Piety, m.Piety, "进入 merge-order:元层 Piety 来自存档");
            Assert.AreEqual(src.GoddessLevel, m.GoddessLevel, "女神等级来自存档");
            Assert.AreEqual(src.NextRepairIndex, m.NextRepairIndex, "修复进度来自存档");
            // 无尽模型(设计 14 §3.7):体力进盘。此存档(NonDefaultState)lastEnergyRegenTime=0(未显式设),
            // ImportMeta 判为「尚无记录」→ 体力夹回起始值(不信缺省 0)。真实无尽档(lastEnergyRegenTime>0)
            // 的体力续存 + 离线补算另由 A17/A18 专测。
            Assert.AreEqual(MergeOrderConfig.EnergyStart, m.Energy, "无记录档(lastEnergyRegenTime=0):体力夹回起始值");
            s.Release();
        }

        // ───────────── A13 MergeOrderState 仍纯逻辑 ─────────────

        [Test]
        public void A13_ExportImport_AreSyncPureMethods()
        {
            // 纯 C# 单测里直接 new + 同步调用 ExportMeta/ImportMeta,无需 async、不碰磁盘。
            var m = new MergeOrderState();
            m.Reset();
            m.Piety = 42;
            var dto = m.ExportMeta();          // 同步返回 DTO
            Assert.IsNotNull(dto);
            Assert.AreEqual(42, dto.piety);

            var m2 = new MergeOrderState();
            m2.Reset();
            m2.ImportMeta(dto);              // 同步覆盖
            Assert.AreEqual(42, m2.Piety, "纯同步往返成立(不依赖 UniTask 运行)");
        }

        // 完成单数 / 本局得分本阶段(无尽增量①)仍不进盘:上一会话的 CompletedOrders/TotalScore 经 ExportMeta/ImportMeta
        // 不得带到下一会话(整盘续存属增量②,本阶段不碰;无尽模型删通关,故不再有「秒结算」风险,但分层不变)。
        [Test]
        public void CompletedOrdersAndTotalScore_DoNotCarryAcrossSessions()
        {
            // 上一会话:完成单数 / 本局得分非 0。
            var prev = FreshState();
            prev.CompletedOrders = 5;
            prev.TotalScore = 12345;
            prev.Piety = 99;                                          // 真元层字段,须随档保留作对照

            // 导出 → 序列化往返 → 下一会话 Reset 后 ImportMeta(模拟 ResetForMergeOrder 链)。
            var dto = prev.ExportMeta();
            string json = MergeMetaPersistence.Serialize(dto);
            var back = MergeMetaPersistence.Deserialize(json);

            var next = FreshState();          // Reset 已置 CompletedOrders=0/TotalScore=0
            next.ImportMeta(back);

            Assert.AreEqual(0, next.CompletedOrders, "完成单数本阶段不进盘,新会话须为 0");
            Assert.AreEqual(0, next.TotalScore, "本局得分本阶段不进盘,新会话须为 0");
            Assert.AreEqual(99, next.Piety, "真元层字段仍随档保留(虔诚币)");
        }

        // 旧档兼容:JSON 含已移除的 completedOrders/totalScore 字段,JsonUtility 忽略多余字段,加载不抛、不串味。
        [Test]
        public void LegacyJson_WithRemovedFields_LoadsWithoutError()
        {
            // 手工拼旧档 JSON:含已删字段 + 一个真元层字段。
            const string legacyJson =
                "{\"version\":1,\"piety\":77,\"completedOrders\":8,\"totalScore\":55555}";

            MergeMetaSave back = null;
            Assert.DoesNotThrow(() => back = MergeMetaPersistence.Deserialize(legacyJson), "旧档反序列化不应抛");
            Assert.IsNotNull(back, "旧档应正常反序列化");

            var m = FreshState();
            Assert.DoesNotThrow(() => m.ImportMeta(back), "旧档 ImportMeta 不应抛");
            Assert.AreEqual(77, m.Piety, "真元层字段从旧档读出");
            Assert.AreEqual(0, m.CompletedOrders, "旧档里的 completedOrders 被忽略,新局为 0");
            Assert.AreEqual(0, m.TotalScore, "旧档里的 totalScore 被忽略,新局为 0");
        }

        [Test]
        public void A13_DirtyFlag_RequestAndClear()
        {
            var m = FreshState();
            Assert.IsFalse(m.IsSaveDirty, "开局未脏");
            m.RequestSave();
            Assert.IsTrue(m.IsSaveDirty, "标脏后为真");
            m.ClearSaveDirty();
            Assert.IsFalse(m.IsSaveDirty, "清脏后为假");
        }

        // ───────────── 时机层补全:全清结算改元层 → 须标脏落盘（设计 14 §3.4 ①）─────────────
        // 窗口 PlaceAndResolve 用谓词 metaChangedBySettle = AllClearRewarded || GoddessLeveledUp || BlindBoxGained>0
        // 判定「本手结算是否改了进盘字段(女神/盲盒)」,为真则落盘。下面直接对 ClearSettlement.Settle 验证
        // 该谓词与「持久化字段确实变了」一致——纯同步,不经窗口、不依赖 UniTask。

        [Test]
        public void Settle_AllClear_ChangesPersistedMeta_AndPredicateIsTrue()
        {
            var m = FreshState();
            m.AllClearArmed = true;          // 武装全清(本手可发奖)
            m.GoddessRating = MergeOrderConfig.GoddessRatingGoal - 1; // 再推进 1 即升档
            int boxBefore = m.BlindBoxCount;
            int levelBefore = m.GoddessLevel;

            // boardEmptyAfter=true + 已武装 → 走全清分支:AdvanceGoddess + AddBlindBox(均改进盘字段)。
            var settle = ClearSettlement.Settle(m, lines: 2, clearedCells: 18, boardEmptyAfter: true,
                milestoneType: MergeElement.Butterfly);

            bool metaChangedBySettle = settle.AllClearRewarded || settle.GoddessLeveledUp || settle.BlindBoxGained > 0;
            Assert.IsTrue(metaChangedBySettle, "全清结算 → 谓词须为真(应触发标脏落盘)");
            Assert.IsTrue(settle.AllClearRewarded, "全清发奖位为真");
            Assert.IsTrue(settle.GoddessLeveledUp, "好评条满档 → 本手升档");

            // 持久化字段确实变了:盲盒 +1、女神升 1 档(rating 归零)。
            Assert.AreEqual(boxBefore + 1, m.BlindBoxCount, "全清发盲盒 → 进盘 blindBoxCount 改变");
            Assert.AreEqual(levelBefore + 1, m.GoddessLevel, "全清推女神升档 → 进盘 goddessLevel 改变");
        }

        [Test]
        public void Settle_NoClear_DoesNotChangePersistedMeta_AndPredicateIsFalse()
        {
            var m = FreshState();
            m.AllClearArmed = true;
            int boxBefore = m.BlindBoxCount;
            int levelBefore = m.GoddessLevel;
            int ratingBefore = m.GoddessRating;

            // lines<=0 → 无消除分支:不发盲盒、不推女神。谓词须为假(无需落盘)。
            var settle = ClearSettlement.Settle(m, lines: 0, clearedCells: 0, boardEmptyAfter: false,
                milestoneType: MergeElement.None);

            bool metaChangedBySettle = settle.AllClearRewarded || settle.GoddessLeveledUp || settle.BlindBoxGained > 0;
            Assert.IsFalse(metaChangedBySettle, "无消除结算 → 谓词须为假(不触发落盘)");
            Assert.AreEqual(boxBefore, m.BlindBoxCount, "无消除 → 盲盒计数不变");
            Assert.AreEqual(levelBefore, m.GoddessLevel, "无消除 → 女神等级不变");
            Assert.AreEqual(ratingBefore, m.GoddessRating, "无消除 → 好评条不变");
        }
    }
}
