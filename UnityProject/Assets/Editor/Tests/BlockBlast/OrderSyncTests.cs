using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Player;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 订单投影器 <see cref="OrderSync"/> 交付对账 EditMode 单测(下行 delta-push 自推除冗·客户端段)。
    /// 交付奖励 Energy/Piety 改由交付响应回带权威绝对余额、经注入的 <see cref="MetaCurrencySync"/> 对账应用
    /// (取代原对发起方的 delta-push)。断言:Success 且余额 >=0 时 set 到该值;-1 哨兵时不 set。
    /// 纯逻辑、不连网 — 桩 <see cref="IOrderRpcGateway"/> 注入交付响应,桩 <see cref="IRpcGateway"/> 供 MetaCurrencySync 构造。
    /// 同步驱动 UniTask 用 GetAwaiter().GetResult()(桩 await CompletedTask 同步完成)。
    /// </summary>
    [TestFixture]
    public class OrderSyncTests
    {
        // 桩交付 gateway:返预设 OrderDeliverResult,记录调用次数。
        private sealed class StubOrderGateway : IOrderRpcGateway
        {
            public OrderDeliverResult Result;
            public int Calls;

            public async UniTask<OrderDeliverResult> DeliverAsync(int slot)
            {
                Calls++;
                await UniTask.CompletedTask;
                return Result;
            }
        }

        // 桩属性 gateway:仅供 MetaCurrencySync 构造(交付对账走 ApplyDeltaPush,不触网络)。
        private sealed class NoopRpcGateway : IRpcGateway
        {
            public async UniTask<ChangeResult> SendChangeRequestAsync(AttrType type, long delta, string reason)
            {
                await UniTask.CompletedTask;
                return ChangeResult.Ok(0);
            }

            public async UniTask<IReadOnlyList<BatchChangeResultItem>> SendBatchChangeRequestAsync(
                IReadOnlyList<BatchChangeItem> items, string reason)
            {
                await UniTask.CompletedTask;
                return new List<BatchChangeResultItem>();
            }
        }

        private static OrderSync NewSync(StubOrderGateway gw)
            => new OrderSync(gw, new MetaCurrencySync(new NoopRpcGateway()));

        // ── OSD1:交付成功 + 两余额有效 → 体力 + 虔诚币 set 到权威绝对值 ──
        [Test]
        public void OSD1_DeliverSuccess_AppliesBothBalances()
        {
            var gw = new StubOrderGateway { Result = new OrderDeliverResult(DeliverCode.Success, null, 55L, 1590L) };
            var sync = NewSync(gw);
            var state = new MergeOrderState { Energy = 10, Piety = 3 };

            sync.RequestDeliver(state, 0, default(Order)).GetAwaiter().GetResult();

            Assert.AreEqual(55, state.Energy, "Energy 应对账到响应 EnergyBalance");
            Assert.AreEqual(1590, state.Piety, "Piety 应对账到响应 PietyBalance");
        }

        // ── OSD2:交付成功但两余额为 -1 哨兵 → 不 set(靠快照 / 其它推送对齐) ──
        [Test]
        public void OSD2_DeliverSuccess_SentinelBalances_DoesNotSet()
        {
            var gw = new StubOrderGateway { Result = new OrderDeliverResult(DeliverCode.Success, null, -1L, -1L) };
            var sync = NewSync(gw);
            var state = new MergeOrderState { Energy = 10, Piety = 3 };

            sync.RequestDeliver(state, 0, default(Order)).GetAwaiter().GetResult();

            Assert.AreEqual(10, state.Energy, "哨兵 -1:Energy 不应被 set");
            Assert.AreEqual(3, state.Piety, "哨兵 -1:Piety 不应被 set");
        }

        // ── OSD3:一有效一哨兵(Energy 有效、Piety -1)→ 只 Energy 对账 ──
        [Test]
        public void OSD3_DeliverSuccess_MixedBalances_AppliesValidOnly()
        {
            var gw = new StubOrderGateway { Result = new OrderDeliverResult(DeliverCode.Success, null, 42L, -1L) };
            var sync = NewSync(gw);
            var state = new MergeOrderState { Energy = 10, Piety = 3 };

            sync.RequestDeliver(state, 0, default(Order)).GetAwaiter().GetResult();

            Assert.AreEqual(42, state.Energy, "Energy 有效余额应 set");
            Assert.AreEqual(3, state.Piety, "Piety 哨兵 -1 不应 set");
        }

        // ── OSD4:零余额(0)是合法权威值(非哨兵)→ set 到 0 ──
        [Test]
        public void OSD4_DeliverSuccess_ZeroBalance_IsAppliedNotSkipped()
        {
            var gw = new StubOrderGateway { Result = new OrderDeliverResult(DeliverCode.Success, null, 0L, 0L) };
            var sync = NewSync(gw);
            var state = new MergeOrderState { Energy = 10, Piety = 3 };

            sync.RequestDeliver(state, 0, default(Order)).GetAwaiter().GetResult();

            Assert.AreEqual(0, state.Energy, "余额 0 是合法权威值(>=0),应 set");
            Assert.AreEqual(0, state.Piety, "余额 0 是合法权威值(>=0),应 set");
        }

        // ── OSD5:交付成功回带碎片权威余额 → 对齐注入背包计数(塔罗收集·碎片对账) ──
        [Test]
        public void OSD5_DeliverSuccess_FragmentBalance_AppliedToBag()
        {
            var gw = new StubOrderGateway
            {
                Result = new OrderDeliverResult(DeliverCode.Success, null, -1L, -1L,
                    fragmentItemId: 31001, fragmentReward: 1L, fragmentBalance: 4L)
            };
            var bag = new Item.ItemBag();
            var sync = new OrderSync(gw, new MetaCurrencySync(new NoopRpcGateway()), bag);
            var state = new MergeOrderState();

            sync.RequestDeliver(state, 0, default(Order)).GetAwaiter().GetResult();

            Assert.AreEqual(4, bag.Count(31001), "碎片计数应对账到响应 FragmentBalance");
        }

        // ── OSD6:碎片余额 -1 哨兵 → 背包不动(靠下次进主游戏快照对齐) ──
        [Test]
        public void OSD6_DeliverSuccess_FragmentSentinel_DoesNotTouchBag()
        {
            var gw = new StubOrderGateway
            {
                Result = new OrderDeliverResult(DeliverCode.Success, null, -1L, -1L,
                    fragmentItemId: 31001, fragmentReward: 0L, fragmentBalance: -1L)
            };
            var bag = new Item.ItemBag();
            bag.SetAuthoritativeCount(31001, 7L); // 既有投影
            var sync = new OrderSync(gw, new MetaCurrencySync(new NoopRpcGateway()), bag);
            var state = new MergeOrderState();

            sync.RequestDeliver(state, 0, default(Order)).GetAwaiter().GetResult();

            Assert.AreEqual(7, bag.Count(31001), "哨兵 -1:背包计数不应被改动");
        }
    }
}
