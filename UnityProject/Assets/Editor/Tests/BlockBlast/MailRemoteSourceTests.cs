using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using GameLogic.Mail;
using GameLogic.Config;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Item;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 邮件上后端 · 客户端段测试（设计 32 §8.2 CV1-CV3 / CV5 / CV7）。
    /// 注桩 <see cref="IRemoteMailSource"/> 覆盖客户端分发逻辑各分支：列表渲染 / 领取成功本地落地 /
    /// 领取失败不放行 / 断服列表降级。真往返（E1）由 boss 后续 test-only 增量起服联调，本段不强求。
    /// </summary>
    /// <remarks>
    /// 异步同步驱动：桩只 <c>await UniTask.CompletedTask</c>，<c>UniTask&lt;T&gt;.GetAwaiter().GetResult()</c> 即同步完成
    /// （同 redeem / rank 客户端段做法）。本地落地复用 16：服务端给 item id × count → <c>ItemGrant.GrantOnAcquire</c>，
    /// 客户端<b>不</b>本地抽奖（区别于设计 21 本地领奖路径）。测试<b>不</b>引 Fantasy 协议类型（test asmdef 无 Fantasy.Unity 引用）。
    /// </remarks>
    [TestFixture]
    public class MailRemoteSourceTests
    {
        // ── 测试夹具：服务端直接给的道具（已抽好，客户端只落点）─────
        // 道具 30001（Exp，automatic=1 立即结算，UseNum=1 → 落 num=count）。
        private const int ItemExp = 30001;
        private const int RewardNum = 50;

        private static ItemDef NumericItem(int id, int numId)
            => new ItemDef { Id = id, Automatic = 1, UseEffect = 1, UseValue = numId, UseNum = 1, UseLevel = 0 };

        /// <summary>只灌道具元数据（无礼包库）：证客户端落地走「服务端给的 id × count 直接落点」，不本地抽奖。</summary>
        private static void SeedItem()
            => ItemConfigMgr.InitForTest(new[] { NumericItem(ItemExp, NumericConfigMgr.Exp) });

        [SetUp]
        public void SetUp() => ItemConfigMgr.ResetForTest();

        [TearDown]
        public void TearDown() => ItemConfigMgr.ResetForTest();

        // ── 桩远程源：可编程各分支响应 ─────────────────────────────

        /// <summary>桩：拉列表 / 领取均返预置响应；记录领取调用入参（核「请求只带邮件标识、不自报账号」）。</summary>
        private sealed class StubRemoteMailSource : IRemoteMailSource
        {
            public MailInboxSnapshot InboxToReturn;        // null = 断服 / 不可用（PullInboxAsync 应空载）
            public MailClaimOutcome ClaimToReturn = MailClaimOutcome.ServiceUnavailable;

            public int FetchCalls;
            public int ClaimCalls;
            public string LastClaimMailId;

            public async UniTask<MailInboxSnapshot> FetchInboxAsync()
            {
                FetchCalls++;
                await UniTask.CompletedTask;
                return InboxToReturn;
            }

            public async UniTask<MailClaimOutcome> ClaimAsync(string mailId)
            {
                ClaimCalls++;
                LastClaimMailId = mailId;
                await UniTask.CompletedTask;
                return ClaimToReturn;
            }
        }

        private static MailListEntry Entry(string id, bool hasReward, bool claimed, long sendMs = 1000)
            => new MailListEntry
            {
                MailId = id, SenderTextId = 9, TitleTextId = 1, ContentTextId = 2,
                SendUnixMs = sendMs, HasReward = hasReward, Claimed = claimed,
            };

        // ════════════ CV1：拉列表渲染 ════════════

        [Test] // CV1：服务端下发列表 → PullInboxAsync 原样返客户端模型（顺序/字段保真），表现层据此画收件箱
        public void CV1_PullInbox_RendersServerList()
        {
            var stub = new StubRemoteMailSource
            {
                InboxToReturn = new MailInboxSnapshot(new[]
                {
                    Entry("m1", hasReward: true,  claimed: false, sendMs: 3000),
                    Entry("m2", hasReward: false, claimed: false, sendMs: 2000),
                    Entry("m3", hasReward: true,  claimed: true,  sendMs: 1000),
                }),
            };
            var svc = new RemoteMailService(stub);

            var list = svc.PullInboxAsync().GetAwaiter().GetResult();

            Assert.AreEqual(3, list.Count, "下发 3 封应全渲染");
            Assert.AreEqual("m1", list[0].MailId, "顺序原样采用服务端的");
            Assert.IsTrue(list[0].HasReward && !list[0].Claimed, "m1 有奖未领");
            Assert.IsFalse(list[1].HasReward, "m2 无奖（纯通知）");
            Assert.IsTrue(list[2].Claimed, "m3 领取态已领（服务端权威）");
            Assert.AreEqual(1, stub.FetchCalls);
        }

        [Test] // CV1：服务端返空列表（无应收邮件）→ 空收件箱、不抛
        public void CV1_PullInbox_EmptyServerList_EmptyInbox()
        {
            var stub = new StubRemoteMailSource { InboxToReturn = new MailInboxSnapshot(Array.Empty<MailListEntry>()) };
            var svc = new RemoteMailService(stub);

            var list = svc.PullInboxAsync().GetAwaiter().GetResult();

            Assert.AreEqual(0, list.Count);
        }

        // ════════════ CV1/CV3：断服列表降级 ════════════

        [Test] // CV1：拉列表断服（远程返 null）→ 空载（空列表）、不阻断、不抛（设计 32 §四：列表只是显示无安全后果）
        public void CV1_PullInbox_ServiceUnavailable_DegradesToEmpty()
        {
            var stub = new StubRemoteMailSource { InboxToReturn = null }; // 断服 / 超时 / 不可用
            var svc = new RemoteMailService(stub);

            IReadOnlyList<MailListEntry> list = null;
            Assert.DoesNotThrow(() => { list = svc.PullInboxAsync().GetAwaiter().GetResult(); });
            Assert.IsNotNull(list, "降级返空集合非 null");
            Assert.AreEqual(0, list.Count, "断服空载，无本地运营邮件可伪造");
        }

        // ════════════ CV2：领取成功本地落地 ════════════

        [Test] // CV2：领取成功 → 按服务端奖励列表本地落地（复用 16）；产出含服务端给的 item×count；玩家进度增加
        public void CV2_Claim_Success_LandsServerRewardsLocally()
        {
            SeedItem();
            var stub = new StubRemoteMailSource
            {
                ClaimToReturn = new MailClaimOutcome(MailClaimCode.Success, new[]
                {
                    new MailRewardLine(ItemExp, RewardNum),
                }),
            };
            var svc = new RemoteMailService(stub);
            var state = new MergeOrderState();
            int expBefore = state.Exp;

            var result = svc.ClaimAsync("m1", state, new System.Random(1)).GetAwaiter().GetResult();

            Assert.AreEqual(MailClaimCode.Success, result.Code);
            Assert.AreEqual(MailText.ClaimSuccess, result.TextId);
            Assert.AreEqual(1, result.Granted.Count, "服务端一项奖励 → 一条产出");
            Assert.AreEqual(GrantKind.Numeric, result.Granted[0].Kind);
            Assert.AreEqual(RewardNum, result.Granted[0].Amount, "数量 = 服务端给的 count（客户端不抽奖、不虚增）");
            Assert.AreEqual(expBefore + RewardNum, state.Exp, "进度本地落地正确");
            Assert.AreEqual("m1", stub.LastClaimMailId, "请求只带邮件标识（不自报账号，CV5）");
        }

        [Test] // CV2：成功但服务端奖励列表为空（库 id 未登记，服务端抽空）→ 仍 Success、落地空、不抛
        public void CV2_Claim_SuccessEmptyRewards_StillSuccessNoLanding()
        {
            SeedItem();
            var stub = new StubRemoteMailSource
            {
                ClaimToReturn = new MailClaimOutcome(MailClaimCode.Success, Array.Empty<MailRewardLine>()),
            };
            var svc = new RemoteMailService(stub);
            var state = new MergeOrderState();
            int expBefore = state.Exp;

            var result = svc.ClaimAsync("m1", state, new System.Random(1)).GetAwaiter().GetResult();

            Assert.AreEqual(MailClaimCode.Success, result.Code);
            Assert.AreEqual(0, result.Granted.Count);
            Assert.AreEqual(expBefore, state.Exp, "无奖励不落地");
        }

        [Test] // CV2：服务端给的道具 id 在客户端未登记 → GrantOnAcquire 对 null def 返空（不崩、不落地）
        public void CV2_Claim_Success_UnregisteredItem_NoLandingNoThrow()
        {
            SeedItem(); // 只登记 30001
            var stub = new StubRemoteMailSource
            {
                ClaimToReturn = new MailClaimOutcome(MailClaimCode.Success, new[]
                {
                    new MailRewardLine(99999, 5), // 客户端未登记
                }),
            };
            var svc = new RemoteMailService(stub);
            var state = new MergeOrderState();
            int expBefore = state.Exp;

            MailClaimDisplay result = default;
            Assert.DoesNotThrow(() => { result = svc.ClaimAsync("m1", state, new System.Random(1)).GetAwaiter().GetResult(); });
            Assert.AreEqual(MailClaimCode.Success, result.Code);
            Assert.AreEqual(0, result.Granted.Count, "未登记道具 → 落地空");
            Assert.AreEqual(expBefore, state.Exp);
        }

        // ════════════ CV2/CV3：领取失败 / 断服不放行 ════════════

        [Test] // CV2/CV3：各失败结果码 → 不发奖、文案互不相同、进度不变（含服务不可用与「此邮件不能领」有别）
        public void CV2_Claim_FailBranches_NoLanding_DistinctText()
        {
            SeedItem();
            var fails = new[]
            {
                MailClaimCode.MailNotFound,
                MailClaimCode.NoReward,
                MailClaimCode.AlreadyClaimed,
                MailClaimCode.Expired,
                MailClaimCode.ServiceUnavailable,
            };

            var seenText = new HashSet<int>();
            foreach (var code in fails)
            {
                var stub = new StubRemoteMailSource { ClaimToReturn = MailClaimOutcome.Fail(code) };
                var svc = new RemoteMailService(stub);
                var state = new MergeOrderState();
                int expBefore = state.Exp;

                var result = svc.ClaimAsync("m1", state, new System.Random(1)).GetAwaiter().GetResult();

                Assert.AreEqual(code, result.Code, $"{code} 结果码透传");
                Assert.AreEqual(0, result.Granted.Count, $"{code} 不发奖");
                Assert.AreEqual(expBefore, state.Exp, $"{code} 进度不变（不本地放行）");
                Assert.AreNotEqual(0, result.TextId, $"{code} 文案非 0");
                Assert.IsTrue(seenText.Add(result.TextId), $"{code} 文案与其它分支互不相同");
            }
        }

        [Test] // CV3：领取断服（远程返 ServiceUnavailable）→ 绝不本地成功发奖、邮件保持可领（进度不变、Code=ServiceUnavailable）
        public void CV3_Claim_ServiceUnavailable_NeverLocalGrant()
        {
            SeedItem();
            var stub = new StubRemoteMailSource { ClaimToReturn = MailClaimOutcome.ServiceUnavailable };
            var svc = new RemoteMailService(stub);
            var state = new MergeOrderState();
            int expBefore = state.Exp;

            var result = svc.ClaimAsync("m1", state, new System.Random(1)).GetAwaiter().GetResult();

            Assert.AreEqual(MailClaimCode.ServiceUnavailable, result.Code);
            Assert.AreEqual(0, result.Granted.Count, "断服不发奖");
            Assert.AreEqual(expBefore, state.Exp, "断服绝不本地放行（区别于排行榜回退本地源）");
            Assert.AreEqual(MailText.ServiceUnavailable, result.TextId);
        }

        // ════════════ 纯转换 / 映射（非 FANTASY_UNITY-gated，可直测）════════════

        [Test] // BuildInboxFromEntries：null / 空条目跳过、顺序原样、不抛
        public void Build_FromEntries_SkipsNull_KeepsOrder()
        {
            var snap = RemoteMailSource.BuildInboxFromEntries(new[]
            {
                Entry("a", true, false),
                null,
                Entry("b", false, true),
            });
            Assert.AreEqual(2, snap.Mails.Count, "null 条目被跳过");
            Assert.AreEqual("a", snap.Mails[0].MailId);
            Assert.AreEqual("b", snap.Mails[1].MailId);
        }

        [Test] // BuildInboxFromEntries：null 入参 → 空快照不抛
        public void Build_FromEntries_NullInput_EmptyNoThrow()
        {
            MailInboxSnapshot snap = null;
            Assert.DoesNotThrow(() => { snap = RemoteMailSource.BuildInboxFromEntries(null); });
            Assert.IsNotNull(snap);
            Assert.AreEqual(0, snap.Mails.Count);
        }

        [Test] // MapClaimCode：协议结果码（0-5）一一对应；未知码（如 99）兜底服务不可用、不崩
        public void MapClaimCode_OneToOne_UnknownDefaultsServiceUnavailable()
        {
            Assert.AreEqual(MailClaimCode.Success,            RemoteMailSource.MapClaimCode(0));
            Assert.AreEqual(MailClaimCode.MailNotFound,       RemoteMailSource.MapClaimCode(1));
            Assert.AreEqual(MailClaimCode.NoReward,           RemoteMailSource.MapClaimCode(2));
            Assert.AreEqual(MailClaimCode.AlreadyClaimed,     RemoteMailSource.MapClaimCode(3));
            Assert.AreEqual(MailClaimCode.Expired,            RemoteMailSource.MapClaimCode(4));
            Assert.AreEqual(MailClaimCode.ServiceUnavailable, RemoteMailSource.MapClaimCode(5));
            Assert.AreEqual(MailClaimCode.ServiceUnavailable, RemoteMailSource.MapClaimCode(99));
        }

        // ════════════ 真 RemoteMailSource 无会话降级（CV1/CV3）════════════

        [Test] // 真 RemoteMailSource 在测试环境（无联网会话）：拉列表返 null、领取返 ServiceUnavailable，绝不抛
        public void RealRemote_NoSession_DegradesNotThrow()
        {
            var remote = new RemoteMailSource();

            MailInboxSnapshot inbox = null;
            MailClaimOutcome claim = default;
            Assert.DoesNotThrow(() => { inbox = remote.FetchInboxAsync().GetAwaiter().GetResult(); });
            Assert.DoesNotThrow(() => { claim = remote.ClaimAsync("m1").GetAwaiter().GetResult(); });

            Assert.IsNull(inbox, "无会话拉列表返 null（调用方空载）");
            Assert.AreEqual(MailClaimCode.ServiceUnavailable, claim.Code, "无会话领取返服务不可用（不本地放行）");
        }

        [Test] // 经 RemoteMailService 包真 RemoteMailSource：无会话 → 列表空载 + 领取不放行（端到端降级）
        public void RealRemote_ViaService_NoSession_DegradesSafely()
        {
            SeedItem();
            var svc = new RemoteMailService(new RemoteMailSource());
            var state = new MergeOrderState();
            int expBefore = state.Exp;

            IReadOnlyList<MailListEntry> list = null;
            MailClaimDisplay claim = default;
            Assert.DoesNotThrow(() => { list = svc.PullInboxAsync().GetAwaiter().GetResult(); });
            Assert.DoesNotThrow(() => { claim = svc.ClaimAsync("m1", state, new System.Random(1)).GetAwaiter().GetResult(); });

            Assert.AreEqual(0, list.Count, "无会话列表空载");
            Assert.AreEqual(MailClaimCode.ServiceUnavailable, claim.Code);
            Assert.AreEqual(expBefore, state.Exp, "无会话领取不本地放行");
        }
    }
}
