using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using GameLogic.AttrLedger;
using GameLogic.BlockBlast.Player;
using GameLogic.UI;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 我的流水 · 客户端段测试(设计 46 PV6 / PV7 / PV10 / PV11 EditMode 部分)。
    /// 注桩 <see cref="IAttrLedgerSource"/> 覆盖各分支:成功列表 / 空列表 / ServiceUnavailable / NetworkDown(异常 catch)/
    /// source 未知值 / 时间格式各分支。真往返 E1-E4 由 boss 后续 test-only 增量起服联调。
    /// </summary>
    /// <remarks>
    /// 异步同步驱动:桩只 <c>await UniTask.CompletedTask</c>,<c>UniTask&lt;T&gt;.GetAwaiter().GetResult()</c> 即同步完成
    /// (同 mail / redeem / rank 客户端段做法)。测试<b>不</b>引 Fantasy 协议类型(test asmdef 无 Fantasy.Unity 引用)。
    /// </remarks>
    [TestFixture]
    public class AttrLedgerClientTests
    {
        // ── 桩远程源:可编程响应 + 抛异常 ─────────────────────────────

        /// <summary>桩:返预置 page 或抛异常;记录 FetchPage 调用入参(核 kind/sinceTs/limit 透传 + 字段映射)。</summary>
        private sealed class FakeAttrLedgerSource : IAttrLedgerSource
        {
            public AttrLedgerPage PageToReturn = AttrLedgerPage.ServiceUnavailable;
            public Exception ToThrow;

            public int FetchCalls;
            public AttrType? LastKind;
            public long LastSinceTs;
            public int LastLimit;

            public async UniTask<AttrLedgerPage> FetchPageAsync(AttrType? kind, long sinceTs, int limit)
            {
                FetchCalls++;
                LastKind = kind;
                LastSinceTs = sinceTs;
                LastLimit = limit;
                await UniTask.CompletedTask;
                if (ToThrow != null) throw ToThrow;
                return PageToReturn;
            }
        }

        private static AttrLedgerEntry Entry(long ts, AttrType kind, long before, long after,
            AttrChangeSource source = AttrChangeSource.Unknown, string raw = "")
            => new AttrLedgerEntry
            {
                Timestamp = ts, Kind = kind, BalanceBefore = before, BalanceAfter = after,
                Delta = after - before, Source = source, ReasonRaw = raw,
            };

        // ════════════ PV4 service 编排:成功列表透传 ════════════

        [Test] // PV4 等价:服务端下发列表 → service 原样返
        public void Service_FetchPage_ReturnsEntries()
        {
            var stub = new FakeAttrLedgerSource
            {
                PageToReturn = new AttrLedgerPage
                {
                    Code = AttrLedgerQueryCode.Success,
                    Entries = new[]
                    {
                        Entry(3000, AttrType.Diamond, 100, 50, AttrChangeSource.ChangeNameSpend, "player_rename"),
                        Entry(2000, AttrType.Coin, 1000, 1500, AttrChangeSource.RankSettleReward, "rank_1"),
                    },
                    HasMore = false,
                },
            };
            var svc = new RemoteAttrLedgerService(stub);

            var page = svc.FetchPageAsync(null, 0L, 50).GetAwaiter().GetResult();

            Assert.AreEqual(AttrLedgerQueryCode.Success, page.Code, "Success 透传");
            Assert.AreEqual(2, page.Entries.Count, "2 行透传");
            Assert.AreEqual(AttrType.Diamond, page.Entries[0].Kind, "首行 Kind");
            Assert.AreEqual(-50L, page.Entries[0].Delta, "delta 透传");
            Assert.AreEqual(AttrChangeSource.ChangeNameSpend, page.Entries[0].Source);
        }

        [Test] // 入参透传(kind / sinceTs / limit 原样到接缝)
        public void Service_FetchPage_PassesParams()
        {
            var stub = new FakeAttrLedgerSource { PageToReturn = new AttrLedgerPage { Code = AttrLedgerQueryCode.Success } };
            var svc = new RemoteAttrLedgerService(stub);

            svc.FetchPageAsync(AttrType.Diamond, 12345L, 50).GetAwaiter().GetResult();

            Assert.AreEqual(AttrType.Diamond, stub.LastKind);
            Assert.AreEqual(12345L, stub.LastSinceTs);
            Assert.AreEqual(50, stub.LastLimit);
        }

        [Test] // PV3:空列表(Success + Entries 空)
        public void Service_FetchPage_EmptyList()
        {
            var stub = new FakeAttrLedgerSource
            {
                PageToReturn = new AttrLedgerPage { Code = AttrLedgerQueryCode.Success, Entries = Array.Empty<AttrLedgerEntry>() },
            };
            var svc = new RemoteAttrLedgerService(stub);

            var page = svc.FetchPageAsync(null, 0L, 50).GetAwaiter().GetResult();

            Assert.AreEqual(AttrLedgerQueryCode.Success, page.Code);
            Assert.AreEqual(0, page.Entries.Count, "空账号 → 0 行");
        }

        // ════════════ PV9 / PV10:错误码透传 ════════════

        [Test] // PV9:ServiceUnavailable 透传
        public void Service_FetchPage_ServiceUnavailable()
        {
            var stub = new FakeAttrLedgerSource { PageToReturn = AttrLedgerPage.ServiceUnavailable };
            var svc = new RemoteAttrLedgerService(stub);

            var page = svc.FetchPageAsync(null, 0L, 50).GetAwaiter().GetResult();

            Assert.AreEqual(AttrLedgerQueryCode.ServiceUnavailable, page.Code);
            Assert.AreEqual(0, page.Entries.Count, "失败时 Entries 空");
            Assert.IsFalse(page.HasMore);
        }

        [Test] // PV10:NetworkDown 透传(协议层无此码,客户端独占)
        public void Service_FetchPage_NetworkDown()
        {
            var stub = new FakeAttrLedgerSource { PageToReturn = AttrLedgerPage.NetworkDown };
            var svc = new RemoteAttrLedgerService(stub);

            var page = svc.FetchPageAsync(null, 0L, 50).GetAwaiter().GetResult();

            Assert.AreEqual(AttrLedgerQueryCode.NetworkDown, page.Code);
            Assert.AreEqual(0, page.Entries.Count);
        }

        // ════════════ PV6:source 未知值 → 「其他」 ════════════

        [Test] // PV6:Source=Unknown(0) → 「其他」
        public void FormatSource_Unknown_ReturnsOther()
        {
            Assert.AreEqual("其他", AttrLedgerFormat.FormatSource(AttrChangeSource.Unknown));
        }

        [Test] // PV6:Source=(AttrChangeSource)99(未来扩值)→ 「其他」(default 兜底)
        public void FormatSource_UnknownEnumValue_ReturnsOther()
        {
            Assert.AreEqual("其他", AttrLedgerFormat.FormatSource((AttrChangeSource)99));
        }

        [Test] // PV6:10 档已登记 source 全有对应文案,无 null / 空字符串 / 抛
        public void FormatSource_AllRegistered_HaveText()
        {
            var pairs = new (AttrChangeSource src, string text)[]
            {
                (AttrChangeSource.ChangeNameSpend,  "改名扣钻"),
                (AttrChangeSource.MailClaim,        "邮件领奖"),
                (AttrChangeSource.RedeemCode,       "兑换码"),
                (AttrChangeSource.RankSettleReward, "排行榜奖励"),
                (AttrChangeSource.ActivityReward,   "活动奖励"),
                (AttrChangeSource.GameplayConsume,  "玩法消费"),
                (AttrChangeSource.ShopPurchase,     "商店购买"),
                (AttrChangeSource.AdminGrant,       "管理员发放"),
                (AttrChangeSource.Refund,           "退款"),
            };
            foreach (var (src, text) in pairs)
                Assert.AreEqual(text, AttrLedgerFormat.FormatSource(src), $"{src} 文案应为「{text}」");
        }

        // ════════════ PV7:时间格式各分支 ════════════

        [Test] // PV7-①:< 60s → 「刚刚」
        public void FormatRelativeTime_Recent_JustNow()
        {
            var now = new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);
            long ts30sAgo = new DateTimeOffset(now.AddSeconds(-30)).ToUnixTimeMilliseconds();
            Assert.AreEqual("刚刚", AttrLedgerFormat.FormatRelativeTime(ts30sAgo, now));
        }

        [Test] // PV7-②:5min → 「5 分钟前」
        public void FormatRelativeTime_FiveMinutes()
        {
            var now = new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);
            long ts = new DateTimeOffset(now.AddMinutes(-5)).ToUnixTimeMilliseconds();
            Assert.AreEqual("5 分钟前", AttrLedgerFormat.FormatRelativeTime(ts, now));
        }

        [Test] // PV7-③:2h → 「2 小时前」
        public void FormatRelativeTime_TwoHours()
        {
            var now = new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);
            long ts = new DateTimeOffset(now.AddHours(-2)).ToUnixTimeMilliseconds();
            Assert.AreEqual("2 小时前", AttrLedgerFormat.FormatRelativeTime(ts, now));
        }

        [Test] // PV7-④:3d → 「3 天前」
        public void FormatRelativeTime_ThreeDays()
        {
            var now = new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);
            long ts = new DateTimeOffset(now.AddDays(-3)).ToUnixTimeMilliseconds();
            Assert.AreEqual("3 天前", AttrLedgerFormat.FormatRelativeTime(ts, now));
        }

        [Test] // PV7-⑤:10d → 「2026-XX-XX HH:mm」(绝对时间格式,本地时区)
        public void FormatRelativeTime_TenDays_AbsoluteFormat()
        {
            var now = new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);
            long ts = new DateTimeOffset(now.AddDays(-10)).ToUnixTimeMilliseconds();
            var s = AttrLedgerFormat.FormatRelativeTime(ts, now);
            // 仅核格式形态:含年份-月份-日 + 空格 + 时:分(本地时区,不固定具体值)
            StringAssert.IsMatch(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$", s);
        }

        [Test] // PV7-⑥:未来时间戳(now + 1d)→ 不抛 + 走绝对时间分支(降级)
        public void FormatRelativeTime_Future_DoesNotThrow_AbsoluteFormat()
        {
            var now = new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);
            long tsFuture = new DateTimeOffset(now.AddDays(1)).ToUnixTimeMilliseconds();
            string s = null;
            Assert.DoesNotThrow(() => s = AttrLedgerFormat.FormatRelativeTime(tsFuture, now));
            StringAssert.IsMatch(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$", s);
        }

        [Test] // PV7-⑦:很远的过去(7 年前)→ 绝对时间
        public void FormatRelativeTime_LongAgo_AbsoluteFormat()
        {
            var now = new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);
            long ts = new DateTimeOffset(now.AddDays(-365 * 7)).ToUnixTimeMilliseconds();
            var s = AttrLedgerFormat.FormatRelativeTime(ts, now);
            StringAssert.IsMatch(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$", s);
        }

        // ════════════ Source 层:KindToProtocolInt + MapResultCode 纯函数 ════════════

        [Test] // kind null → 0(协议 sentinel「不过滤」)
        public void KindToProtocolInt_Null_ReturnsZero()
        {
            Assert.AreEqual(0, RemoteAttrLedgerSource.KindToProtocolInt(null));
        }

        [Test] // Coin/Diamond/Stamina → 1/2/3(协议层错开一位,沿 AttrLedgerQueryHelper)
        public void KindToProtocolInt_KnownKinds()
        {
            Assert.AreEqual(1, RemoteAttrLedgerSource.KindToProtocolInt(AttrType.Coin));
            Assert.AreEqual(2, RemoteAttrLedgerSource.KindToProtocolInt(AttrType.Diamond));
            Assert.AreEqual(3, RemoteAttrLedgerSource.KindToProtocolInt(AttrType.Stamina));
        }

        [Test] // All → 0(防御兜底,协议层不接受 All)
        public void KindToProtocolInt_All_ReturnsZero()
        {
            Assert.AreEqual(0, RemoteAttrLedgerSource.KindToProtocolInt(AttrType.All));
        }

        [Test] // 协议结果码 0/1/2 一一映射;未知码兜底 ServiceUnavailable
        public void MapResultCode_AllBranches()
        {
            Assert.AreEqual(AttrLedgerQueryCode.Success, RemoteAttrLedgerSource.MapResultCode(0));
            Assert.AreEqual(AttrLedgerQueryCode.InvalidRequest, RemoteAttrLedgerSource.MapResultCode(1));
            Assert.AreEqual(AttrLedgerQueryCode.ServiceUnavailable, RemoteAttrLedgerSource.MapResultCode(2));
            Assert.AreEqual(AttrLedgerQueryCode.ServiceUnavailable, RemoteAttrLedgerSource.MapResultCode(99));
        }

        // ════════════ Window 行 VM 映射(PV4 + PV6 + PV7 单测锚点)════════════

        [Test] // 行 VM 映射:全字段一一对位,正 delta 加「+」前缀
        public void BuildRowVM_AllFields()
        {
            var now = new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);
            long ts5MinAgo = new DateTimeOffset(now.AddMinutes(-5)).ToUnixTimeMilliseconds();

            var entry = Entry(ts5MinAgo, AttrType.Coin, 1000, 1500, AttrChangeSource.RankSettleReward, "rank_1");
            var vm = UIPlayerAttrLedgerPanel.BuildRowVM(entry, now);

            Assert.AreEqual("5 分钟前", vm.TimeText);
            Assert.AreEqual(AttrType.Coin, vm.Kind);
            Assert.AreEqual("金币", vm.KindText);
            Assert.AreEqual("+500", vm.DeltaText, "正 delta 有 + 前缀");
            Assert.AreEqual("排行榜奖励", vm.SourceText);
            Assert.AreEqual("1000 → 1500", vm.BalanceText);
        }

        [Test] // 负 delta 无 + 前缀(.ToString() 自带 - 号)
        public void BuildRowVM_NegativeDelta_NoPlusPrefix()
        {
            var now = new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);
            var entry = Entry(new DateTimeOffset(now.AddSeconds(-30)).ToUnixTimeMilliseconds(),
                AttrType.Diamond, 100, 50, AttrChangeSource.ChangeNameSpend, "player_rename");
            var vm = UIPlayerAttrLedgerPanel.BuildRowVM(entry, now);

            Assert.AreEqual("刚刚", vm.TimeText);
            Assert.AreEqual("钻石", vm.KindText);
            Assert.AreEqual("-50", vm.DeltaText, "负 delta 无 + 前缀,.ToString() 自带 -");
            Assert.AreEqual("改名扣钻", vm.SourceText);
            Assert.AreEqual("100 → 50", vm.BalanceText);
        }

        [Test] // PV6:未知 source → 行 VM 用「其他」(default 分支)
        public void BuildRowVM_UnknownSource_OtherText()
        {
            var now = new DateTime(2026, 6, 21, 12, 0, 0, DateTimeKind.Utc);
            var entry = Entry(new DateTimeOffset(now.AddMinutes(-1)).ToUnixTimeMilliseconds(),
                AttrType.Stamina, 5, 0, (AttrChangeSource)99, "future_source");
            var vm = UIPlayerAttrLedgerPanel.BuildRowVM(entry, now);

            Assert.AreEqual("其他", vm.SourceText, "未来扩 source 整数 → default「其他」兜底");
            Assert.AreEqual("体力", vm.KindText);
            Assert.AreEqual("-5", vm.DeltaText);
        }

        [Test] // null entry 不抛(行 VM 走兜底)
        public void BuildRowVM_NullEntry_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                var vm = UIPlayerAttrLedgerPanel.BuildRowVM(null, DateTime.UtcNow);
            });
        }

        // ════════════ GameContext.InitAttrLedgerWith(测试注入) ════════════

        [Test] // 测试注入接缝:替换 AttrLedger 后,GameContext.AttrLedger 走桩源
        public void GameContext_InitAttrLedgerWith_ReplacesSvc()
        {
            var stub = new FakeAttrLedgerSource
            {
                PageToReturn = new AttrLedgerPage { Code = AttrLedgerQueryCode.Success, Entries = Array.Empty<AttrLedgerEntry>() },
            };
            GameContext.Instance.InitAttrLedgerWith(stub);

            var page = GameContext.Instance.AttrLedger.FetchPageAsync(AttrType.Coin, 0L, 50)
                .GetAwaiter().GetResult();

            Assert.AreEqual(AttrLedgerQueryCode.Success, page.Code);
            Assert.AreEqual(1, stub.FetchCalls);
            Assert.AreEqual(AttrType.Coin, stub.LastKind);
        }

        // ════════════ AttrLedgerPage 工厂方法 ════════════

        [Test]
        public void AttrLedgerPage_FactoryHelpers()
        {
            var sa = AttrLedgerPage.ServiceUnavailable;
            Assert.AreEqual(AttrLedgerQueryCode.ServiceUnavailable, sa.Code);
            Assert.AreEqual(0, sa.Entries.Count);
            Assert.IsFalse(sa.HasMore);

            var nd = AttrLedgerPage.NetworkDown;
            Assert.AreEqual(AttrLedgerQueryCode.NetworkDown, nd.Code);

            var ir = AttrLedgerPage.InvalidRequest;
            Assert.AreEqual(AttrLedgerQueryCode.InvalidRequest, ir.Code);
        }
    }
}
