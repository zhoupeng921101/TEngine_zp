using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GameLogic.Redeem;
using GameLogic.Config;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Item;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 通用兑换码系统测试（设计 20 §六，23 条验收点）。
    /// 纯逻辑全 EditMode 可测（POCO + 注入隔离）；Luban 直读条按工具链可达性。
    /// 配置经 <see cref="RedeemConfigMgr.InitForTest"/> 注入、去重经 <see cref="InMemoryRedeemStore"/> /
    /// <see cref="InMemoryPersistenceProvider"/>、发奖落点 state 可 null 走纯解析、道具经 <see cref="ItemConfigMgr.InitForTest"/>。
    /// </summary>
    [TestFixture]
    public class RedeemCodeSystemTests
    {
        // 测试道具：用 InitForTest 注入，自控 UseNum=1 使产出 Amount == 奖励 num。
        private const int ItemExp = 30001;    // use_effect=1 → Numeric, use_value=Exp(1)
        private const int ItemEnergy = 30002; // use_effect=1 → Numeric, use_value=Energy(4)
        private const int ItemPattern = 30004;// use_effect=2 → Pattern

        private static ItemDef NumericItem(int id, int numId)
            => new ItemDef { Id = id, Automatic = 1, UseEffect = 1, UseValue = numId, UseNum = 1, UseLevel = 0 };

        private static ItemDef PatternItem(int id)
            => new ItemDef { Id = id, Automatic = 1, UseEffect = 2, UseValue = 100, UseNum = 1, UseLevel = 1 };

        private void SeedItems()
        {
            ItemConfigMgr.InitForTest(new[]
            {
                NumericItem(ItemExp, NumericConfigMgr.Exp),
                NumericItem(ItemEnergy, NumericConfigMgr.Energy),
                PatternItem(ItemPattern),
            });
        }

        private static RedeemCodeDef Code(string code, int once = 1, string expire = null, params (int id, int num)[] rewards)
        {
            var def = new RedeemCodeDef { Code = code, OncePerPlayer = once, ExpireTime = expire };
            foreach (var (id, num) in rewards)
                def.Rewards.Add(new RedeemReward { ItemId = id, Num = num });
            return def;
        }

        [SetUp]
        public void SetUp()
        {
            RedeemConfigMgr.ResetForTest();
            ItemConfigMgr.ResetForTest();
        }

        [TearDown]
        public void TearDown()
        {
            RedeemConfigMgr.ResetForTest();
            ItemConfigMgr.ResetForTest();
        }

        // ── 配置 C ──────────────────────────────────────────────

        [Test] // C1：InitForTest 灌入后 Get 返对应 def，奖励按 code 聚合（一码多奖）
        public void C1_ConfigMgr_GetReturnsDef_RewardsAggregated()
        {
            RedeemConfigMgr.InitForTest(new[]
            {
                Code("MULTI", rewards: new[] { (ItemExp, 100), (ItemEnergy, 5), (ItemPattern, 1) }),
            });
            var def = RedeemConfigMgr.Get("MULTI");
            Assert.IsNotNull(def);
            Assert.AreEqual(3, def.Rewards.Count);
            Assert.AreEqual(ItemExp, def.Rewards[0].ItemId);
            Assert.AreEqual(100, def.Rewards[0].Num);
        }

        [Test] // C2：key 用规整（大写）码；查不存在返 null（不抛）
        public void C2_ConfigMgr_KeyNormalizedUpper_MissReturnsNull()
        {
            RedeemConfigMgr.InitForTest(new[] { Code("abc", rewards: new[] { (ItemExp, 1) }) });
            // InitForTest 内部把 Code 规整成大写存
            Assert.IsNotNull(RedeemConfigMgr.Get("ABC"));
            Assert.IsNull(RedeemConfigMgr.Get("NOPE"));
        }

        [Test] // C3：Luban 直读 redeem_tbredeemcode.bytes 行数 > 0 且字段映射正确（工具链不可达列 BLOCKED）
        public void C3_LubanDirectRead_RedeemCodeBytes_HasRows()
        {
            const string codePath = "Assets/AssetRaw/Configs/bytes/redeem_tbredeemcode.bytes";
            const string rewardPath = "Assets/AssetRaw/Configs/bytes/redeem_tbredeemreward.bytes";
            var taCode = AssetDatabase.LoadAssetAtPath<TextAsset>(codePath);
            var taReward = AssetDatabase.LoadAssetAtPath<TextAsset>(rewardPath);
            Assert.IsNotNull(taCode, $"找不到 {codePath}，请先运行 Luban 生成");
            Assert.IsNotNull(taReward, $"找不到 {rewardPath}，请先运行 Luban 生成");

            var tbCode = new GameConfig.redeem.TbRedeemCode(new Luban.ByteBuf(taCode.bytes));
            var tbReward = new GameConfig.redeem.TbRedeemReward(new Luban.ByteBuf(taReward.bytes));
            Assert.Greater(tbCode.DataList.Count, 0);
            Assert.Greater(tbReward.DataList.Count, 0);

            // 字段映射正确：WELCOME2026 码存在，once=1
            var welcome = tbCode.DataList.Find(r => r.Code == "WELCOME2026");
            Assert.IsNotNull(welcome, "redeemcode.bytes 缺 WELCOME2026 行");
            Assert.AreEqual(1, welcome.OncePerPlayer);
            // 奖励子表 MULTIGIFT 多奖项聚合
            int multiCount = tbReward.DataList.FindAll(r => r.Code == "MULTIGIFT").Count;
            Assert.AreEqual(3, multiCount, "MULTIGIFT 应有 3 个奖励项");
        }

        // ── 校验 V ──────────────────────────────────────────────

        [Test] // V1：Local 命中 Valid+Def / 未命中 NotFound+null
        public void V1_LocalValidator_HitValid_MissNotFound()
        {
            RedeemConfigMgr.InitForTest(new[] { Code("HIT", rewards: new[] { (ItemExp, 1) }) });
            var validator = new LocalConfigRedeemValidator();

            var hit = validator.Validate("HIT");
            Assert.AreEqual(ValidationStatus.Valid, hit.Status);
            Assert.IsNotNull(hit.Def);

            var miss = validator.Validate("MISS");
            Assert.AreEqual(ValidationStatus.NotFound, miss.Status);
            Assert.IsNull(miss.Def);
        }

        [Test] // V2：Remote stub 返 SourceUnavailable 不抛
        public void V2_RemoteValidator_ReturnsSourceUnavailable_NoThrow()
        {
            var validator = new RemoteRedeemValidator();
            ValidationResult r = default;
            Assert.DoesNotThrow(() => r = validator.Validate("ANYTHING"));
            Assert.AreEqual(ValidationStatus.SourceUnavailable, r.Status);
            Assert.IsNull(r.Def);
        }

        [Test] // V3：Service 注 Remote → 返 SourceUnavailable（换注入零改动服务层）
        public void V3_Service_WithRemoteValidator_ReturnsSourceUnavailable()
        {
            SeedItems();
            RedeemConfigMgr.InitForTest(new[] { Code("HIT", rewards: new[] { (ItemExp, 1) }) });
            var service = new RedeemService(new RemoteRedeemValidator(), new InMemoryRedeemStore());
            var outcome = service.Redeem("HIT", null, null);
            Assert.AreEqual(RedeemResult.SourceUnavailable, outcome.Result);
        }

        // ── 去重 D ──────────────────────────────────────────────

        [Test] // D1：InMemory 标记往返
        public void D1_InMemoryStore_MarkRoundtrip()
        {
            var store = new InMemoryRedeemStore();
            Assert.IsFalse(store.HasRedeemed("X"));
            store.MarkRedeemed("X");
            Assert.IsTrue(store.HasRedeemed("X"));
            Assert.IsFalse(store.HasRedeemed("Y"));
        }

        [Test] // D2：Persistence 注 InMemoryProvider 跨实例往返保真 + 空串/无键 → 空集合不抛
        public void D2_PersistenceStore_CrossInstanceRoundtrip_DefensiveDeserialize()
        {
            var provider = new InMemoryPersistenceProvider();
            Persistence.Provider = provider;

            var store1 = new PersistenceRedeemStore();
            store1.MarkRedeemed("CODE1");
            // 新实例复用同 provider（模拟重启）
            var store2 = new PersistenceRedeemStore();
            Assert.IsTrue(store2.HasRedeemed("CODE1"), "跨实例往返应保真");

            // 空串 → 空集合不抛
            provider.Set(PersistenceRedeemStore.Key, "");
            var store3 = new PersistenceRedeemStore();
            Assert.DoesNotThrow(() => store3.HasRedeemed("CODE1"));
            Assert.IsFalse(store3.HasRedeemed("CODE1"));

            // 无键 → 空集合不抛
            provider.Remove(PersistenceRedeemStore.Key);
            var store4 = new PersistenceRedeemStore();
            Assert.DoesNotThrow(() => store4.HasRedeemed("CODE1"));
            Assert.IsFalse(store4.HasRedeemed("CODE1"));
        }

        [Test] // D3：once=0 重复兑成功不进集合；once=1 第二次 AlreadyRedeemed
        public void D3_OncePerPlayer_RepeatSemantics()
        {
            SeedItems();
            RedeemConfigMgr.InitForTest(new[]
            {
                Code("REPEAT", once: 0, rewards: new[] { (ItemExp, 1) }),
                Code("ONCE", once: 1, rewards: new[] { (ItemExp, 1) }),
            });
            var store = new InMemoryRedeemStore();
            var service = new RedeemService(new LocalConfigRedeemValidator(), store);

            Assert.AreEqual(RedeemResult.Success, service.Redeem("REPEAT", null, null).Result);
            Assert.AreEqual(RedeemResult.Success, service.Redeem("REPEAT", null, null).Result);
            Assert.IsFalse(store.HasRedeemed("REPEAT"), "once=0 不进去重集合");

            Assert.AreEqual(RedeemResult.Success, service.Redeem("ONCE", null, null).Result);
            Assert.AreEqual(RedeemResult.AlreadyRedeemed, service.Redeem("ONCE", null, null).Result);
        }

        // ── 服务 S ──────────────────────────────────────────────

        [Test] // S1：空 / 纯空白 → EmptyInput（最先短路，不查表）
        public void S1_EmptyOrWhitespace_EmptyInput()
        {
            var service = new RedeemService(new LocalConfigRedeemValidator(), new InMemoryRedeemStore());
            Assert.AreEqual(RedeemResult.EmptyInput, service.Redeem("", null, null).Result);
            Assert.AreEqual(RedeemResult.EmptyInput, service.Redeem("   ", null, null).Result);
            Assert.AreEqual(RedeemResult.EmptyInput, service.Redeem(null, null, null).Result);
        }

        [Test] // S2：" abc " 经 Normalize → trim+大写 命中存为 ABC
        public void S2_Normalize_TrimUpper_Hits()
        {
            SeedItems();
            RedeemConfigMgr.InitForTest(new[] { Code("ABC", rewards: new[] { (ItemExp, 1) }) });
            var service = new RedeemService(new LocalConfigRedeemValidator(), new InMemoryRedeemStore());
            Assert.AreEqual(RedeemResult.Success, service.Redeem("  abc  ", null, null).Result);
            Assert.AreEqual("ABC", RedeemService.Normalize(" abc "));
        }

        [Test] // S3：有效码首次 Success+Granted 非空；once=1 再兑 AlreadyRedeemed 且不重复发奖
        public void S3_FirstSuccess_SecondAlreadyRedeemed_NoDoubleGrant()
        {
            SeedItems();
            RedeemConfigMgr.InitForTest(new[] { Code("GIFT", once: 1, rewards: new[] { (ItemExp, 100) }) });
            var service = new RedeemService(new LocalConfigRedeemValidator(), new InMemoryRedeemStore());
            var state = new MergeOrderState();

            var first = service.Redeem("GIFT", state, null);
            Assert.AreEqual(RedeemResult.Success, first.Result);
            Assert.Greater(first.Granted.Count, 0);
            int expAfterFirst = state.Exp;
            Assert.AreEqual(100, expAfterFirst);

            var second = service.Redeem("GIFT", state, null);
            Assert.AreEqual(RedeemResult.AlreadyRedeemed, second.Result);
            Assert.AreEqual(0, second.Granted.Count, "第二次不发奖");
            Assert.AreEqual(expAfterFirst, state.Exp, "Exp 不应再增（无重复发奖）");
        }

        [Test] // S4：过期码 → Expired；空 ExpireTime 不判过期
        public void S4_Expired_VsNoExpire()
        {
            SeedItems();
            RedeemConfigMgr.InitForTest(new[]
            {
                Code("OLD", expire: "2020-01-01", rewards: new[] { (ItemExp, 1) }),
                Code("NOEXP", expire: "", rewards: new[] { (ItemExp, 1) }),
            });
            var service = new RedeemService(new LocalConfigRedeemValidator(), new InMemoryRedeemStore())
            {
                NowProvider = () => new DateTime(2026, 6, 14),
            };
            Assert.AreEqual(RedeemResult.Expired, service.Redeem("OLD", null, null).Result);
            Assert.AreEqual(RedeemResult.Success, service.Redeem("NOEXP", null, null).Result);
        }

        [Test] // S5：失败分支不发奖、不写去重集合；六类结果码 TextIdFor 返非 0 且互不相同
        public void S5_FailBranches_NoGrant_NoDedupe_DistinctTextIds()
        {
            SeedItems();
            RedeemConfigMgr.InitForTest(new[]
            {
                Code("OLD", expire: "2020-01-01", rewards: new[] { (ItemExp, 1) }),
            });
            var store = new InMemoryRedeemStore();
            var service = new RedeemService(new LocalConfigRedeemValidator(), store)
            {
                NowProvider = () => new DateTime(2026, 6, 14),
            };

            // NotFound：不发奖、不写去重
            var nf = service.Redeem("NOPE", new MergeOrderState(), null);
            Assert.AreEqual(RedeemResult.NotFound, nf.Result);
            Assert.AreEqual(0, nf.Granted.Count);
            Assert.IsFalse(store.HasRedeemed("NOPE"));

            // Expired：不发奖、不写去重
            var ex = service.Redeem("OLD", new MergeOrderState(), null);
            Assert.AreEqual(RedeemResult.Expired, ex.Result);
            Assert.AreEqual(0, ex.Granted.Count);
            Assert.IsFalse(store.HasRedeemed("OLD"));

            // 六类 textId 非 0 且互不相同
            var ids = new HashSet<int>();
            foreach (RedeemResult r in Enum.GetValues(typeof(RedeemResult)))
            {
                int id = RedeemText.TextIdFor(r);
                Assert.AreNotEqual(0, id, $"{r} textId 不应为 0");
                Assert.IsTrue(ids.Add(id), $"{r} textId 与其它重复");
            }
            Assert.AreEqual(6, ids.Count);
        }

        // ── 发奖 G ──────────────────────────────────────────────

        [Test] // G1：货币道具 → state 对应字段增加正确数量（复用 16 落点）
        public void G1_CurrencyReward_LandsOnState()
        {
            SeedItems();
            RedeemConfigMgr.InitForTest(new[] { Code("EXP", rewards: new[] { (ItemExp, 250) }) });
            var service = new RedeemService(new LocalConfigRedeemValidator(), new InMemoryRedeemStore());
            var state = new MergeOrderState();

            var outcome = service.Redeem("EXP", state, null);
            Assert.AreEqual(RedeemResult.Success, outcome.Result);
            Assert.AreEqual(250, state.Exp, "Exp 应 = 奖励 num（UseNum=1 × count=250）");
        }

        [Test] // G2：一码多奖 Granted 列表对应配置
        public void G2_MultiReward_GrantedMatchesConfig()
        {
            SeedItems();
            RedeemConfigMgr.InitForTest(new[]
            {
                Code("MULTI", rewards: new[] { (ItemExp, 10), (ItemEnergy, 5), (ItemPattern, 1) }),
            });
            var service = new RedeemService(new LocalConfigRedeemValidator(), new InMemoryRedeemStore());
            var state = new MergeOrderState();

            var outcome = service.Redeem("MULTI", state, null);
            Assert.AreEqual(RedeemResult.Success, outcome.Result);
            // 三项均 automatic=1 立即结算：2 货币 + 1 图案 → 3 产出
            Assert.AreEqual(3, outcome.Granted.Count);
            Assert.AreEqual(10, state.Exp);
            Assert.AreEqual(5, state.Energy);
            // 图案落收集区（Lv1 注入 5? 不：count=1 → 1 个 Lv1）
            Assert.AreEqual(1, state.InventoryCount((MergeElement)100, 1));
        }

        [Test] // G3：state==null 仍 Success 且 Granted 含产出结构（纯解析路径，不抛）
        public void G3_NullState_StillSuccess_GrantedHasStructure()
        {
            SeedItems();
            RedeemConfigMgr.InitForTest(new[] { Code("EXP", rewards: new[] { (ItemExp, 7) }) });
            var service = new RedeemService(new LocalConfigRedeemValidator(), new InMemoryRedeemStore());

            RedeemOutcome outcome = default;
            Assert.DoesNotThrow(() => outcome = service.Redeem("EXP", null, null));
            Assert.AreEqual(RedeemResult.Success, outcome.Result);
            Assert.AreEqual(1, outcome.Granted.Count);
            Assert.AreEqual(GrantKind.Numeric, outcome.Granted[0].Kind);
            Assert.AreEqual(7, outcome.Granted[0].Amount);
        }
    }
}
