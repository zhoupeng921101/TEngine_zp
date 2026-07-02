using Cysharp.Threading.Tasks;
using NUnit.Framework;
using GameLogic.BlockBlast.Item;
using GameLogic.BlockBlast.Player;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 塔罗收集投影 <see cref="TarotCollection"/> + 背包权威对齐入口(塔罗收集·客户端段)EditMode 单测。
    /// 服务端是唯一事实源:合成经桩 <see cref="ITarotRpcGateway"/> 裁决,断言各结果码下投影(已合成集合 + 背包碎片计数)
    /// 与响应权威值对齐、失败不脏本地。纯逻辑、不连网;同步驱动 UniTask 用 GetAwaiter().GetResult()。
    /// </summary>
    [TestFixture]
    public class TarotCollectionTests
    {
        private sealed class StubTarotGateway : ITarotRpcGateway
        {
            public TarotSynthesizeResult Result;
            public int Calls;

            public async UniTask<TarotSynthesizeResult> SynthesizeAsync(int cardId)
            {
                Calls++;
                await UniTask.CompletedTask;
                return Result;
            }
        }

        // ── TB1:背包整份权威覆盖 — 清掉服务端未含的旧 id、落入快照值 ──
        [Test]
        public void TB1_BagSnapshot_OverwritesAndClearsStale()
        {
            var bag = new ItemBag();
            bag.SetAuthoritativeCount(31001, 3L);
            bag.SetAuthoritativeCount(31002, 5L); // 服务端快照未含 → 应被清

            bag.ApplyAuthoritativeSnapshot(new[] { (31001, 9L), (31003, 1L) });

            Assert.AreEqual(9, bag.Count(31001), "快照值应覆盖旧计数");
            Assert.AreEqual(0, bag.Count(31002), "服务端未含的 id 应被清除,投影不残留脏数据");
            Assert.AreEqual(1, bag.Count(31003), "快照新 id 应落入");
        }

        // ── TB2:单条权威 set 到 0 → 该 id 移除(释放格子) ──
        [Test]
        public void TB2_BagSetAuthoritativeZero_RemovesEntry()
        {
            var bag = new ItemBag();
            bag.SetAuthoritativeCount(31001, 10L);

            bag.SetAuthoritativeCount(31001, 0L);

            Assert.AreEqual(0, bag.Count(31001));
            Assert.AreEqual(0, bag.SlotUsed, "计数归零应释放格子");
        }

        // ── TC1:合成成功 → 已合成集合整份覆盖(含新牌)+ 碎片余额对齐背包 ──
        [Test]
        public void TC1_SynthesizeSuccess_AppliesCollectedAndBalance()
        {
            var bag = new ItemBag();
            bag.SetAuthoritativeCount(31003, 12L);
            var gw = new StubTarotGateway
            {
                Result = new TarotSynthesizeResult(TarotSynthCode.Success, cardId: 3,
                    fragmentItemId: 31003, fragmentBalance: 2L, collectedIds: new[] { 1, 3 })
            };
            var tarot = new TarotCollection(gw, bag);

            var result = tarot.SynthesizeAsync(3).GetAwaiter().GetResult();

            Assert.AreEqual(TarotSynthCode.Success, result.Code);
            Assert.IsTrue(tarot.IsCollected(3), "新合成的牌应进投影");
            Assert.IsTrue(tarot.IsCollected(1), "响应全集里的既有牌应保留");
            Assert.AreEqual(2, bag.Count(31003), "碎片计数应对齐到扣减后权威余额");
        }

        // ── TC2:AlreadyCollected → 不扣碎片,但响应全集照常对齐投影(把漏掉的牌补进来) ──
        [Test]
        public void TC2_AlreadyCollected_AlignsProjectionWithoutSpending()
        {
            var bag = new ItemBag();
            bag.SetAuthoritativeCount(31003, 12L);
            var gw = new StubTarotGateway
            {
                Result = new TarotSynthesizeResult(TarotSynthCode.AlreadyCollected, cardId: 3,
                    fragmentItemId: 31003, fragmentBalance: 12L, collectedIds: new[] { 3 })
            };
            var tarot = new TarotCollection(gw, bag);

            var result = tarot.SynthesizeAsync(3).GetAwaiter().GetResult();

            Assert.AreEqual(TarotSynthCode.AlreadyCollected, result.Code);
            Assert.IsTrue(tarot.IsCollected(3), "本地投影缺失的已合成牌应由响应全集补齐");
            Assert.AreEqual(12, bag.Count(31003), "未扣碎片:余额维持权威值");
        }

        // ── TC3:NotEnoughFragments → 集合不变、背包按响应权威余额对齐(非本地扣减) ──
        [Test]
        public void TC3_NotEnough_LeavesCollectionUntouched()
        {
            var bag = new ItemBag();
            bag.SetAuthoritativeCount(31003, 9L);
            var gw = new StubTarotGateway
            {
                Result = new TarotSynthesizeResult(TarotSynthCode.NotEnoughFragments, cardId: 3,
                    fragmentItemId: 31003, fragmentBalance: 9L, collectedIds: System.Array.Empty<int>())
            };
            var tarot = new TarotCollection(gw, bag);

            var result = tarot.SynthesizeAsync(3).GetAwaiter().GetResult();

            Assert.AreEqual(TarotSynthCode.NotEnoughFragments, result.Code);
            Assert.IsFalse(tarot.IsCollected(3), "失败不得置牌");
            Assert.AreEqual(9, bag.Count(31003), "失败不得动碎片计数(权威余额未变)");
        }

        // ── TC4:网络断(NetworkDown,无权威回带:全集 null + 余额 -1)→ 本地投影零改动 ──
        [Test]
        public void TC4_NetworkDown_NoLocalMutation()
        {
            var bag = new ItemBag();
            bag.SetAuthoritativeCount(31003, 10L);
            var gw = new StubTarotGateway
            {
                Result = new TarotSynthesizeResult(TarotSynthCode.NetworkDown, cardId: 3)
            };
            var tarot = new TarotCollection(gw, bag);
            tarot.ApplyCollectedSnapshot(new[] { 1 });

            tarot.SynthesizeAsync(3).GetAwaiter().GetResult();

            Assert.IsTrue(tarot.IsCollected(1), "全集 null(未取到权威值):既有投影保留");
            Assert.IsFalse(tarot.IsCollected(3));
            Assert.AreEqual(10, bag.Count(31003), "余额 -1 哨兵:背包不动");
        }

        // ── TC5:快照整份覆盖语义 — 空数组 = 权威空收集,清空投影;null = 未取到,保留 ──
        [Test]
        public void TC5_CollectedSnapshot_EmptyClears_NullKeeps()
        {
            var tarot = new TarotCollection(new StubTarotGateway());
            tarot.ApplyCollectedSnapshot(new[] { 1, 2 });

            tarot.ApplyCollectedSnapshot(null);
            Assert.IsTrue(tarot.IsCollected(1), "null:未取到权威值,保留投影");

            tarot.ApplyCollectedSnapshot(System.Array.Empty<int>());
            Assert.IsFalse(tarot.IsCollected(1), "空数组:权威空收集,应清空");
            Assert.IsFalse(tarot.IsCollected(2));
        }
    }
}
