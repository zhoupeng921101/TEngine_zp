using System;
using System.Collections.Generic;
using NUnit.Framework;
using Cysharp.Threading.Tasks;
using GameLogic.Redeem;
using GameLogic.Config;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Item;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 兑换码客户端上后端测试(设计 30 §8.2,CV1-CV9 中可纯逻辑单测者)。
    /// 权威全在服务端:客户端不持码表 / 不持本地去重,兑换成功与否完全取决于注入的桩裁决器回包(CV6)。
    /// 桩裁决器实现 <see cref="IRedeemValidator"/>、按预设 <see cref="RedeemVerdict"/> 同步返回(UniTask 即时完成),
    /// 驱动 <see cref="RedeemService.RedeemAsync"/> 验客户端六类结果码分发 + 仅成功发奖。
    /// </summary>
    [TestFixture]
    public class RedeemCodeSystemTests
    {
        // 测试道具:用 InitForTest 注入,自控 UseNum=1 使产出 Amount == 奖励 count。
        private const int ItemExp = 30001;     // use_effect=1 → Numeric, use_value=Exp(1)
        private const int ItemEnergy = 30002;  // use_effect=1 → Numeric, use_value=Energy(4)
        private const int ItemPattern = 30004; // use_effect=2 → Pattern

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

        [SetUp]
        public void SetUp() => ItemConfigMgr.ResetForTest();

        [TearDown]
        public void TearDown() => ItemConfigMgr.ResetForTest();

        /// <summary>同步驱动 UniTask&lt;T&gt;:桩裁决器只 await UniTask.CompletedTask,全程同步完成,即时取结果。</summary>
        private static T RunSync<T>(UniTask<T> task) => task.GetAwaiter().GetResult();

        /// <summary>
        /// 桩裁决器:按预设 <see cref="RedeemVerdict"/> + 奖励列表同步回应,并记录被调用次数与最近一次提交的码字符串
        /// (用于断言「空输入不发请求」「客户端原样提交、不自报账号」)。
        /// </summary>
        private sealed class StubValidator : IRedeemValidator
        {
            private readonly RedeemVerdict _verdict;
            private readonly IReadOnlyList<RedeemRewardItem> _rewards;

            public int CallCount { get; private set; }
            public string LastCode { get; private set; }

            public StubValidator(RedeemVerdict verdict, IReadOnlyList<RedeemRewardItem> rewards = null)
            {
                _verdict = verdict;
                _rewards = rewards ?? Array.Empty<RedeemRewardItem>();
            }

            public async UniTask<ValidationResult> ValidateAsync(string code)
            {
                CallCount++;
                LastCode = code;
                await UniTask.CompletedTask;
                return _verdict == RedeemVerdict.Success
                    ? new ValidationResult(RedeemVerdict.Success, _rewards)
                    : ValidationResult.Fail(_verdict);
            }
        }

        private static RedeemRewardItem[] Rewards(params (int id, int count)[] items)
        {
            var list = new RedeemRewardItem[items.Length];
            for (int i = 0; i < items.Length; i++)
                list[i] = new RedeemRewardItem(items[i].id, items[i].count);
            return list;
        }

        // ── CV5:空输入客户端短路,不发请求 ─────────────────────────

        [Test] // CV5:空 / 纯空白 / null → EmptyInput,且裁决器未被调用(不发请求)
        public void CV5_EmptyOrWhitespace_ShortCircuit_NoRequest()
        {
            foreach (var raw in new[] { "", "   ", null })
            {
                var stub = new StubValidator(RedeemVerdict.Success, Rewards((ItemExp, 1)));
                var service = new RedeemService(stub);
                var outcome = RunSync(service.RedeemAsync(raw, null, null));
                Assert.AreEqual(RedeemResult.EmptyInput, outcome.Result, $"输入[{raw ?? "null"}]应短路 EmptyInput");
                Assert.AreEqual(0, stub.CallCount, "空输入不应发请求");
                Assert.AreEqual(0, outcome.Granted.Count, "空输入不发奖");
            }
        }

        // ── CV1:成功按服务端奖励列表本地发奖(复用 16 落点)─────────

        [Test] // CV1:成功 + 货币奖励 → state 对应字段增加正确数量(复用 16 ItemGrant 落点)
        public void CV1_Success_CurrencyReward_LandsOnState()
        {
            SeedItems();
            var stub = new StubValidator(RedeemVerdict.Success, Rewards((ItemExp, 250)));
            var service = new RedeemService(stub);
            var state = new MergeOrderState();

            var outcome = RunSync(service.RedeemAsync("WELCOME2026", state, null));
            Assert.AreEqual(RedeemResult.Success, outcome.Result);
            Assert.AreEqual(250, state.Exp, "Exp 应 = 奖励 count(UseNum=1 × count=250)");
            Assert.Greater(outcome.Granted.Count, 0, "Granted 含产出供 17 展示");
        }

        [Test] // CV1:一码多奖 → 多落点 + Granted 列表对应服务端奖励列表
        public void CV1_Success_MultiReward_AllLand()
        {
            SeedItems();
            var stub = new StubValidator(RedeemVerdict.Success,
                Rewards((ItemExp, 10), (ItemEnergy, 5), (ItemPattern, 1)));
            var service = new RedeemService(stub);
            var state = new MergeOrderState();

            var outcome = RunSync(service.RedeemAsync("MULTIGIFT", state, null));
            Assert.AreEqual(RedeemResult.Success, outcome.Result);
            Assert.AreEqual(3, outcome.Granted.Count, "三项 automatic=1 立即结算");
            Assert.AreEqual(10, state.Exp);
            Assert.AreEqual(5, state.Energy);
            Assert.AreEqual(1, state.InventoryCount((MergeElement)100, 1));
        }

        [Test] // CV1:state==null 仍 Success 且 Granted 含产出结构(纯解析路径,不抛)
        public void CV1_Success_NullState_StillGrantsStructure()
        {
            SeedItems();
            var stub = new StubValidator(RedeemVerdict.Success, Rewards((ItemExp, 7)));
            var service = new RedeemService(stub);

            RedeemOutcome outcome = default;
            Assert.DoesNotThrow(() => outcome = RunSync(service.RedeemAsync("EXP", null, null)));
            Assert.AreEqual(RedeemResult.Success, outcome.Result);
            Assert.AreEqual(1, outcome.Granted.Count);
            Assert.AreEqual(GrantKind.Numeric, outcome.Granted[0].Kind);
            Assert.AreEqual(7, outcome.Granted[0].Amount);
        }

        // ── CV2:四类失败不发奖,文案对应 ──────────────────────────

        [Test] // CV2:已兑过 / 码无效 / 已过期 / 全局满 → 不发奖,结果码对应,且(即便给了奖励列表)不落地
        public void CV2_FourFailures_NoGrant_CorrectResult()
        {
            SeedItems();
            var map = new (RedeemVerdict v, RedeemResult r)[]
            {
                (RedeemVerdict.AlreadyRedeemed, RedeemResult.AlreadyRedeemed),
                (RedeemVerdict.InvalidCode,     RedeemResult.InvalidCode),
                (RedeemVerdict.Expired,         RedeemResult.Expired),
                (RedeemVerdict.LimitReached,    RedeemResult.LimitReached),
            };
            foreach (var (v, r) in map)
            {
                // 即便桩裁决器附带奖励(异常服务),失败分支也不读它、不发奖。
                var stub = new StubValidator(v, Rewards((ItemExp, 999)));
                var service = new RedeemService(stub);
                var state = new MergeOrderState();
                var outcome = RunSync(service.RedeemAsync("ANYCODE", state, null));
                Assert.AreEqual(r, outcome.Result, $"{v} 应映射 {r}");
                Assert.AreEqual(0, outcome.Granted.Count, $"{v} 不发奖");
                Assert.AreEqual(0, state.Exp, $"{v} 不落地任何奖励");
            }
        }

        // ── CV3:服务不可用不发奖、文案与码无效有别、码可重试 ───────

        [Test] // CV3:ServiceUnavailable → 不发奖,文案与 InvalidCode 不同
        public void CV3_ServiceUnavailable_NoGrant_TextDiffersFromInvalid()
        {
            SeedItems();
            var stub = new StubValidator(RedeemVerdict.ServiceUnavailable);
            var service = new RedeemService(stub);
            var state = new MergeOrderState();

            var outcome = RunSync(service.RedeemAsync("CODE", state, null));
            Assert.AreEqual(RedeemResult.ServiceUnavailable, outcome.Result);
            Assert.AreEqual(0, outcome.Granted.Count);
            Assert.AreEqual(0, state.Exp);
            Assert.AreNotEqual(
                RedeemText.TextIdFor(RedeemResult.ServiceUnavailable),
                RedeemText.TextIdFor(RedeemResult.InvalidCode),
                "服务不可用与码无效文案须有别(误判会让玩家以为好码失效)");
        }

        // ── CV4 / CV6:无离线兜底,远程裁决器断服只降级、绝不本地放行 ──

        [Test] // CV4:RemoteRedeemValidator 在无会话(断服)时返 ServiceUnavailable,绝不返 Success
        public void CV4_RemoteValidator_NoSession_ServiceUnavailable_NeverLocalSuccess()
        {
            // 测试环境未连接 Fantasy 会话(Session==null):远程裁决器须降级,不本地放行。
            var validator = new RemoteRedeemValidator();
            ValidationResult result = default;
            Assert.DoesNotThrow(() => result = RunSync(validator.ValidateAsync("ANYCODE")));
            Assert.AreEqual(RedeemVerdict.ServiceUnavailable, result.Verdict,
                "断服只降级,不本地裁定成功(权威已上移服务端)");
            Assert.AreEqual(0, result.Rewards.Count);
        }

        [Test] // CV4:经服务层,断服(远程裁决器)→ 不本地发奖
        public void CV4_Service_WithRemoteValidator_NoSession_NoLocalGrant()
        {
            SeedItems();
            var service = new RedeemService(new RemoteRedeemValidator());
            var state = new MergeOrderState();
            var outcome = RunSync(service.RedeemAsync("WELCOME2026", state, null));
            Assert.AreEqual(RedeemResult.ServiceUnavailable, outcome.Result);
            Assert.AreEqual(0, outcome.Granted.Count, "断服不本地发奖");
            Assert.AreEqual(0, state.Exp);
        }

        // ── CV6:成功与否完全取决于回包(同码不同裁决得不同结果)────

        [Test] // CV6:同一码,裁决器回 Success vs AlreadyRedeemed → 客户端结果随回包变(客户端不持去重权威)
        public void CV6_OutcomeFollowsResponse_NotLocalState()
        {
            SeedItems();
            var ok = RunSync(new RedeemService(new StubValidator(RedeemVerdict.Success, Rewards((ItemExp, 1))))
                .RedeemAsync("SAME", new MergeOrderState(), null));
            var dup = RunSync(new RedeemService(new StubValidator(RedeemVerdict.AlreadyRedeemed))
                .RedeemAsync("SAME", new MergeOrderState(), null));
            Assert.AreEqual(RedeemResult.Success, ok.Result);
            Assert.AreEqual(RedeemResult.AlreadyRedeemed, dup.Result);
        }

        // ── CV8:客户端原样提交码字符串(请求不含自报账号——协议只有 Code 字段)──

        [Test] // CV8:服务层把玩家原始输入原样交裁决器提交(规整权威在服务端,客户端不预规整、不自报账号)
        public void CV8_SubmitsRawCode_NoClientSideAccount()
        {
            var stub = new StubValidator(RedeemVerdict.InvalidCode);
            var service = new RedeemService(stub);
            RunSync(service.RedeemAsync("  abc  ", null, null));
            Assert.AreEqual("  abc  ", stub.LastCode, "客户端原样提交,规整(trim+大写)权威在服务端");
            // 协议 C2G_RedeemCodeRequest 仅 Code 字段、无账号字段(身份由会话承载,CV8/SV9 对称)——编译期即保证。
        }

        // ── 文案:七类结果码 textId 非 0 且互不相同 ────────────────

        [Test] // 七类结果码 TextIdFor 返非 0 且互不相同(CV2 四失败互异 + CV3 不可用与无效有别)
        public void TextIds_AllDistinct_NonZero()
        {
            var ids = new HashSet<int>();
            foreach (RedeemResult r in Enum.GetValues(typeof(RedeemResult)))
            {
                int id = RedeemText.TextIdFor(r);
                Assert.AreNotEqual(0, id, $"{r} textId 不应为 0");
                Assert.IsTrue(ids.Add(id), $"{r} textId 与其它重复");
            }
            Assert.AreEqual(7, ids.Count);
        }
    }
}
