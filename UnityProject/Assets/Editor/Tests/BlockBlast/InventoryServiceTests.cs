using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using GameLogic.BlockBlast.Player;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 背包批次轨投影 <see cref="InventoryService"/>(背包系统·客户端段)EditMode 单测。
    /// 服务端是唯一事实源:批次快照/推送整份覆盖投影(loaded 三态防误清)、批次倒计时基于服务端时间基准(不信本地墙钟)、
    /// 使用经桩 <see cref="IInventoryRpcGateway"/> 裁决(reqSeq 全账号单调 + 跨会话墙钟推高)。纯逻辑、不连网;同步驱动 UniTask 用 GetAwaiter().GetResult()。
    /// </summary>
    [TestFixture]
    public class InventoryServiceTests
    {
        private sealed class StubInvGateway : IInventoryRpcGateway
        {
            public UseItemResult Result;
            public readonly List<long> ReqSeqs = new List<long>();

            public async UniTask<UseItemResult> SendUseItemAsync(int itemId, long count, long reqSeq)
            {
                ReqSeqs.Add(reqSeq);
                await UniTask.CompletedTask;
                return Result;
            }
        }

        private static List<InventoryLot> Lots(params InventoryLot[] lots) => new List<InventoryLot>(lots);

        // ── IV1:loaded=true 整份覆盖 + 置 IsReady ──
        [Test]
        public void IV1_Snapshot_LoadedOverwrites()
        {
            long now = 1000;
            var svc = new InventoryService(new StubInvGateway(), () => now);

            svc.ApplyLotsSnapshot(Lots(new InventoryLot("a", 5001, 3, 0, 20000)), serverNowMs: 5000, loaded: true);

            Assert.IsTrue(svc.IsReady, "loaded=true 后应就绪");
            Assert.AreEqual(1, svc.Lots.Count);
            Assert.AreEqual(5001, svc.Lots[0].ItemId);
            Assert.AreEqual(3, svc.Lots[0].Count);
        }

        // ── IV2:loaded=false 保留既有投影不覆盖不清空(三态防误清) ──
        [Test]
        public void IV2_Snapshot_NotLoadedPreserves()
        {
            long now = 1000;
            var svc = new InventoryService(new StubInvGateway(), () => now);
            svc.ApplyLotsSnapshot(Lots(new InventoryLot("a", 5001, 3, 0, 20000)), serverNowMs: 5000, loaded: true);

            // 降级空占位:loaded=false + 空 lots,不应清空既有投影。
            svc.ApplyLotsSnapshot(Lots(), serverNowMs: 6000, loaded: false);

            Assert.AreEqual(1, svc.Lots.Count, "loaded=false 不覆盖,保留既有批次");
            Assert.AreEqual(5001, svc.Lots[0].ItemId);
        }

        // ── IV3:loaded=true 空集 = 权威空集,全清 ──
        [Test]
        public void IV3_Snapshot_LoadedEmptyClears()
        {
            long now = 1000;
            var svc = new InventoryService(new StubInvGateway(), () => now);
            svc.ApplyLotsSnapshot(Lots(new InventoryLot("a", 5001, 3, 0, 20000)), 5000, true);

            svc.ApplyLotsSnapshot(Lots(), serverNowMs: 6000, loaded: true);

            Assert.AreEqual(0, svc.Lots.Count, "loaded=true 空集是权威空集,应全清");
        }

        // ── IV4:批次倒计时基于服务端时间基准(offset),本地流逝推进过期,不用本地墙钟直判 ──
        [Test]
        public void IV4_Countdown_UsesServerTimeBase()
        {
            long now = 1000;
            var svc = new InventoryService(new StubInvGateway(), () => now);
            // 服务端时刻 100000,本地 1000 → offset=99000。批次 ExpireMs=105000。
            svc.ApplyLotsSnapshot(Lots(new InventoryLot("a", 5001, 1, 0, 105000)), serverNowMs: 100000, loaded: true);
            var lot = svc.Lots[0];

            // ServerNow = 1000 + 99000 = 100000;剩余 = 105000 - 100000 = 5000,未过期。
            Assert.AreEqual(5000, svc.RemainingMs(lot));
            Assert.IsFalse(svc.IsExpired(lot));

            // 本地流逝 6000ms → ServerNow=106000 > 105000,已过期。
            now = 7000;
            Assert.Less(svc.RemainingMs(lot), 0);
            Assert.IsTrue(svc.IsExpired(lot));
        }

        // ── IV5:同批次在不同 offset 的服务给出不同剩余(证明用服务端基准非本地墙钟) ──
        [Test]
        public void IV5_Countdown_OffsetIsolatesFromLocalWallClock()
        {
            long now = 0;
            var early = new InventoryService(new StubInvGateway(), () => now);
            var late = new InventoryService(new StubInvGateway(), () => now);
            var lot = new InventoryLot("a", 5001, 1, 0, 50000);

            early.ApplyLotsSnapshot(Lots(lot), serverNowMs: 10000, loaded: true); // offset=10000
            late.ApplyLotsSnapshot(Lots(lot), serverNowMs: 30000, loaded: true);  // offset=30000

            // 同一 lot、同一本地 now,但服务端基准不同 → 剩余不同(50000-10000 vs 50000-30000)。
            Assert.AreEqual(40000, early.RemainingMs(early.Lots[0]));
            Assert.AreEqual(20000, late.RemainingMs(late.Lots[0]));
        }

        // ── IV6:reqSeq 会话内严格递增,首个锚定墙钟 ──
        [Test]
        public void IV6_ReqSeq_MonotonicWithinSession()
        {
            long now = 5000;
            var gw = new StubInvGateway { Result = new UseItemResult(UseItemCode.Success, 5001, 1) };
            var svc = new InventoryService(gw, () => now);

            svc.TryUseAsync(5001, 1).GetAwaiter().GetResult();
            svc.TryUseAsync(5001, 1).GetAwaiter().GetResult();
            svc.TryUseAsync(5001, 1).GetAwaiter().GetResult();

            Assert.AreEqual(3, gw.ReqSeqs.Count);
            Assert.AreEqual(5000, gw.ReqSeqs[0], "首个 = max(1, 墙钟)");
            Assert.AreEqual(5001, gw.ReqSeqs[1]);
            Assert.AreEqual(5002, gw.ReqSeqs[2]);
            for (int i = 1; i < gw.ReqSeqs.Count; i++)
            {
                Assert.Greater(gw.ReqSeqs[i], gw.ReqSeqs[i - 1], "reqSeq 须严格递增");
            }
        }

        // ── IV7:跨会话(重登新实例)reqSeq 由墙钟跳到当前,高于上会话(防低序号被服务端误判 Duplicate) ──
        [Test]
        public void IV7_ReqSeq_CrossSessionJumpsByWallClock()
        {
            var gw = new StubInvGateway { Result = new UseItemResult(UseItemCode.Success, 5001, 1) };
            long s1 = 5000;
            var sess1 = new InventoryService(gw, () => s1);
            sess1.TryUseAsync(5001, 1).GetAwaiter().GetResult(); // seq=5000

            long s2 = 9000; // 重登:墙钟已前进
            var sess2 = new InventoryService(gw, () => s2);
            sess2.TryUseAsync(5001, 1).GetAwaiter().GetResult();

            Assert.AreEqual(9000, gw.ReqSeqs[gw.ReqSeqs.Count - 1]);
            Assert.Greater(gw.ReqSeqs[gw.ReqSeqs.Count - 1], gw.ReqSeqs[0], "新会话 reqSeq 须高于上会话");
        }

        // ── IV8:成功回执透传消耗量 + 产出货币 ──
        [Test]
        public void IV8_Use_SuccessPassesConsumedAndProduce()
        {
            long now = 1000;
            var gw = new StubInvGateway
            {
                Result = new UseItemResult(UseItemCode.Success, 5001, consumedCount: 2,
                    hasProduce: true, producedType: AttrType.Diamond, producedAmount: 20)
            };
            var svc = new InventoryService(gw, () => now);

            var r = svc.TryUseAsync(5001, 2).GetAwaiter().GetResult();

            Assert.AreEqual(UseItemCode.Success, r.Code);
            Assert.AreEqual(2, r.ConsumedCount);
            Assert.IsTrue(r.HasProduce);
            Assert.AreEqual(AttrType.Diamond, r.ProducedType);
            Assert.AreEqual(20, r.ProducedAmount);
        }

        // ── IV9:非法入参本地直拒,不发 RPC ──
        [Test]
        public void IV9_Use_InvalidRejectedWithoutRpc()
        {
            long now = 1000;
            var gw = new StubInvGateway { Result = new UseItemResult(UseItemCode.Success, 5001, 1) };
            var svc = new InventoryService(gw, () => now);

            var r0 = svc.TryUseAsync(0, 1).GetAwaiter().GetResult();
            var rNeg = svc.TryUseAsync(5001, 0).GetAwaiter().GetResult();

            Assert.AreEqual(UseItemCode.InvalidRequest, r0.Code);
            Assert.AreEqual(UseItemCode.InvalidRequest, rNeg.Code);
            Assert.AreEqual(0, gw.ReqSeqs.Count, "非法入参不应发 RPC");
        }

        // ── IV11:seed 幂等锚底 → 首个 reqSeq 严格大于服务端已处理值,即使本地墙钟低(消除时钟回拨误判) ──
        [Test]
        public void IV11_SeedReqSeq_FirstUseExceedsServerFloor()
        {
            long now = 3000; // 本地墙钟被回拨到低于服务端上次处理的 reqSeq(=8000)
            var gw = new StubInvGateway { Result = new UseItemResult(UseItemCode.Success, 5001, 1) };
            var svc = new InventoryService(gw, () => now);

            svc.SeedReqSeq(8000); // 登录快照下发的服务端 LastUseReqSeq
            svc.TryUseAsync(5001, 1).GetAwaiter().GetResult();

            Assert.AreEqual(8001, gw.ReqSeqs[0], "首个 reqSeq 应 = seed+1,严格大于服务端已处理值,不被墙钟(3000)拉低");
        }

        // ── IV12:SeedReqSeq 只增不减(推送传 0 / 低值为 no-op) ──
        [Test]
        public void IV12_SeedReqSeq_MonotonicNonDecreasing()
        {
            long now = 5000;
            var gw = new StubInvGateway { Result = new UseItemResult(UseItemCode.Success, 5001, 1) };
            var svc = new InventoryService(gw, () => now);

            svc.SeedReqSeq(9000);
            svc.SeedReqSeq(0);    // 推送 no-op
            svc.SeedReqSeq(100);  // 低值 no-op
            svc.TryUseAsync(5001, 1).GetAwaiter().GetResult();

            Assert.AreEqual(9001, gw.ReqSeqs[0], "seed 只增不减,低值/0 不回退");
        }

        // ── IV10:失败结果码原样透传(不脏投影) ──
        [Test]
        public void IV10_Use_FailureCodePassedThrough()
        {
            long now = 1000;
            var gw = new StubInvGateway { Result = UseItemResult.Fail(UseItemCode.Expired, 5001) };
            var svc = new InventoryService(gw, () => now);
            svc.ApplyLotsSnapshot(Lots(new InventoryLot("a", 5001, 1, 0, 20000)), 5000, true);

            var r = svc.TryUseAsync(5001, 1).GetAwaiter().GetResult();

            Assert.AreEqual(UseItemCode.Expired, r.Code);
            Assert.AreEqual(1, svc.Lots.Count, "失败不改投影(投影仅由权威快照/推送覆盖)");
        }
    }
}
