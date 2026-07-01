using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Player;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 进主游戏编排 <see cref="EnterMainGameSync"/> EditMode 单测(全栈协议改动·客户端段)。
    /// 进主游戏响应现只回带订单快照(云存档通道已整体退役,局内 cosmetic + 合成叠加层改经 C2G_GameStart/GameSnapshot 的 SliceJson 收发)。
    /// 纯逻辑、不连网 — 桩 <see cref="IEnterMainGameGateway"/> 注入响应、经 <see cref="OrderSync"/> + 开窗 <see cref="MergeOrderState"/> 观测订单落地。
    /// 断言:响应回带的订单快照喂 OrderSync 并覆盖活态订单;每次进入都重发请求(决策②)、取最新响应;失败(Unavailable → 空快照)保留本地订单不清空。
    /// 同步驱动 UniTask 用 GetAwaiter().GetResult()(桩 await CompletedTask 同步完成)。
    /// </summary>
    [TestFixture]
    public class EnterMainGameTests
    {
        // ── 桩 gateway:可预设进主游戏响应(可按调用次序返不同响应),记录调用次数 ──
        private sealed class StubEnterGateway : IEnterMainGameGateway
        {
            public readonly List<EnterMainGameResult> Queue = new();
            public EnterMainGameResult Fallback = EnterMainGameResult.Unavailable();
            public int Calls;

            public async UniTask<EnterMainGameResult> EnterAsync()
            {
                int idx = Calls;
                Calls++;
                await UniTask.CompletedTask;
                return idx < Queue.Count ? Queue[idx] : Fallback;
            }
        }

        // 交付 RPC 不在本编排路径触发,给惰性桩占位(OrderSync 构造需接缝)。
        private sealed class StubOrderGateway : IOrderRpcGateway
        {
            public async UniTask<OrderDeliverResult> DeliverAsync(int slot)
            { await UniTask.CompletedTask; return new OrderDeliverResult(DeliverCode.ServiceUnavailable, null); }
        }

        // 单槽订单快照:一条 (type, level, count) 有效订单,其余槽空。
        private static OrderSnapshotData SnapshotWith(MergeElement type, int level, int count)
        {
            var orders = new List<OrderItemData> { new OrderItemData((int)type, level, count) };
            return new OrderSnapshotData(orders, orderCursor: 0, lastOrderRefreshMs: 0,
                orderRefreshIntervalSec: 0, orderRewardEnergy: 0);
        }

        // 组一套 OrderSync + 已开窗活态,供观测进主游戏回带的订单是否落地到活态。
        private static (EnterMainGameSync sync, MergeOrderState state) NewSyncWithOpenState(StubEnterGateway gw)
        {
            var orderSync = new OrderSync(new StubOrderGateway());
            var state = new MergeOrderState();
            orderSync.OnMergeStateReady(state); // 开窗:切服务端权威 + 记活态,后续 OnSnapshotPush 直接覆盖活态
            return (new EnterMainGameSync(gw, orderSync), state);
        }

        // ── E1:进主游戏响应回带订单快照 → 喂 OrderSync 覆盖活态订单 ──
        [Test]
        public void E1_Enter_SnapshotApplied_OverwritesActiveOrders()
        {
            var gw = new StubEnterGateway();
            gw.Queue.Add(new EnterMainGameResult(SnapshotWith(MergeElement.Chalice, level: 2, count: 3)));

            var (sync, state) = NewSyncWithOpenState(gw);
            sync.EnterAsync().GetAwaiter().GetResult();

            Assert.AreEqual(1, gw.Calls);
            Assert.AreEqual(1, state.ActiveOrders.Length, "回带快照的单槽订单覆盖活态");
            Assert.AreEqual(MergeElement.Chalice, state.ActiveOrders[0].Type);
            Assert.AreEqual(2, state.ActiveOrders[0].Level);
            Assert.AreEqual(3, state.ActiveOrders[0].Count);
        }

        // ── E2:失败(Unavailable → 空快照)→ 不喂 OrderSync,保留本地已有订单不清空 ──
        [Test]
        public void E2_Enter_Unavailable_KeepsLocalOrders()
        {
            var gw = new StubEnterGateway();
            var (sync, state) = NewSyncWithOpenState(gw);

            // 先经一次成功回带铺底本地订单。
            gw.Queue.Add(new EnterMainGameResult(SnapshotWith(MergeElement.Star, level: 1, count: 5)));
            sync.EnterAsync().GetAwaiter().GetResult();
            Assert.AreEqual(MergeElement.Star, state.ActiveOrders[0].Type, "首次回带铺底");

            // 再进入返回 Unavailable(空快照):OrderSnapshot=null → 不喂 OrderSync,活态订单保留不变。
            gw.Fallback = EnterMainGameResult.Unavailable();
            sync.EnterAsync().GetAwaiter().GetResult();

            Assert.AreEqual(2, gw.Calls, "每次进入都重发请求");
            Assert.AreEqual(MergeElement.Star, state.ActiveOrders[0].Type, "服务不可用不清空本地订单");
            Assert.AreEqual(5, state.ActiveOrders[0].Count);
        }

        // ── E3:决策② — 每次进入都重新请求,取最新响应覆盖活态 ──
        [Test]
        public void E3_Enter_EachEntry_ReRequestsAndRealigns()
        {
            var gw = new StubEnterGateway();
            gw.Queue.Add(new EnterMainGameResult(SnapshotWith(MergeElement.Butterfly, level: 1, count: 2)));
            gw.Queue.Add(new EnterMainGameResult(SnapshotWith(MergeElement.Scroll, level: 3, count: 7)));

            var (sync, state) = NewSyncWithOpenState(gw);

            sync.EnterAsync().GetAwaiter().GetResult();
            Assert.AreEqual(MergeElement.Butterfly, state.ActiveOrders[0].Type);

            sync.EnterAsync().GetAwaiter().GetResult();
            Assert.AreEqual(2, gw.Calls, "每次进入都重发请求,不复用上次缓存(决策②)");
            Assert.AreEqual(MergeElement.Scroll, state.ActiveOrders[0].Type, "重对齐到最新回带快照");
            Assert.AreEqual(3, state.ActiveOrders[0].Level);
            Assert.AreEqual(7, state.ActiveOrders[0].Count);
        }

        // ── E4:窗未开时回带快照缓存到 OrderSync,开窗后应用 ──
        [Test]
        public void E4_Enter_BeforeWindowOpen_AppliesOnOpen()
        {
            var gw = new StubEnterGateway();
            gw.Queue.Add(new EnterMainGameResult(SnapshotWith(MergeElement.Chalice, level: 1, count: 4)));

            var orderSync = new OrderSync(new StubOrderGateway());
            var sync = new EnterMainGameSync(gw, orderSync);

            // 先进主游戏(窗未开):快照缓存到 OrderSync 的 _pending。
            sync.EnterAsync().GetAwaiter().GetResult();

            // 后开窗:应用已缓存快照到活态。
            var state = new MergeOrderState();
            orderSync.OnMergeStateReady(state);

            Assert.AreEqual(MergeElement.Chalice, state.ActiveOrders[0].Type, "开窗应用进入时缓存的快照");
            Assert.AreEqual(4, state.ActiveOrders[0].Count);
        }
    }
}
