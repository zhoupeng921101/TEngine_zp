using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using GameLogic.BlockBlast.Player;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 玩家元层属性服务 EditMode 单测(设计 38 §9.1 CV1-CV7)。
    /// 纯逻辑、不依赖 UnityEngine / 不连网 — 用桩 <see cref="IRpcGateway"/> 注入响应,断言视图与事件。
    /// 同步驱动 UniTask 用 GetAwaiter().GetResult()(桩只 await CompletedTask 即同步完成,沿 memory「EditMode 同步驱动 UniTask」)。
    /// </summary>
    [TestFixture]
    public class PlayerAttrServiceTests
    {
        // ── 桩 RPC 接缝 ──

        private sealed class StubGateway : IRpcGateway
        {
            public ChangeResult NextResult { get; set; } = ChangeResult.Ok(0);
            public int CallCount { get; private set; }
            public AttrType LastType { get; private set; }
            public long LastDelta { get; private set; }
            public string LastReason { get; private set; }

            public async UniTask<ChangeResult> SendChangeRequestAsync(AttrType type, long delta, string reason)
            {
                CallCount++;
                LastType = type;
                LastDelta = delta;
                LastReason = reason;
                await UniTask.CompletedTask;
                return NextResult;
            }

            // PlayerAttrService(金币/钻石/体力)只走单条链路,不发批量;桩满足接口即可。
            public async UniTask<IReadOnlyList<BatchChangeResultItem>> SendBatchChangeRequestAsync(
                IReadOnlyList<BatchChangeItem> items, string reason)
            {
                await UniTask.CompletedTask;
                return System.Array.Empty<BatchChangeResultItem>();
            }
        }

        // ── CV1:ApplySnapshot 初值 + IsReady + 事件 ──

        [Test]
        public void CV1_ApplySnapshot_SetsAllAndIsReadyAndFiresAllOnce()
        {
            var svc = new PlayerAttrService(new StubGateway());
            Assert.IsFalse(svc.IsReady, "初始 IsReady=false");
            Assert.AreEqual(0L, svc.Coin);
            Assert.AreEqual(0L, svc.Diamond);
            Assert.AreEqual(0L, svc.Stamina);

            int fireCount = 0;
            AttrType lastType = AttrType.Coin;
            svc.OnAttrChanged += (t, _, __) => { fireCount++; lastType = t; };

            svc.ApplySnapshot(100, 200, 5);

            Assert.IsTrue(svc.IsReady, "ApplySnapshot 后 IsReady=true");
            Assert.AreEqual(100L, svc.Coin);
            Assert.AreEqual(200L, svc.Diamond);
            Assert.AreEqual(5L, svc.Stamina);
            Assert.AreEqual(1, fireCount, "ApplySnapshot 应触发 OnAttrChanged 一次");
            Assert.AreEqual(AttrType.All, lastType, "事件 type 应是 All");
        }

        // ── CV2:ApplyDeltaPush 按 type set + 不动其它字段 ──

        [Test]
        public void CV2_ApplyDeltaPush_OnlyTouchesTargetType()
        {
            var svc = new PlayerAttrService(new StubGateway());
            svc.ApplySnapshot(100, 200, 5);

            int fireCount = 0;
            AttrType lastType = AttrType.Coin;
            long lastValue = -1;
            string lastReason = null;
            svc.OnAttrChanged += (t, v, r) => { fireCount++; lastType = t; lastValue = v; lastReason = r; };

            svc.ApplyDeltaPush(AttrType.Diamond, 150L, "shop");

            Assert.AreEqual(150L, svc.Diamond);
            Assert.AreEqual(100L, svc.Coin, "其它字段不动");
            Assert.AreEqual(5L, svc.Stamina, "其它字段不动");
            Assert.AreEqual(1, fireCount);
            Assert.AreEqual(AttrType.Diamond, lastType);
            Assert.AreEqual(150L, lastValue);
            Assert.AreEqual("shop", lastReason);
        }

        // ── CV3:ApplyChangeResponse 与 ApplyDeltaPush 同口径(幂等) ──

        [Test]
        public void CV3_ApplyChangeResponse_IdempotentWithDeltaPush()
        {
            var svc = new PlayerAttrService(new StubGateway());
            svc.ApplyChangeResponse(AttrType.Coin, 50, "test");
            Assert.AreEqual(50L, svc.Coin);

            svc.ApplyDeltaPush(AttrType.Coin, 50, "push");      // 同值再来一次(模拟推送在响应后到)
            Assert.AreEqual(50L, svc.Coin, "重复同值幂等");

            svc.ApplyChangeResponse(AttrType.Coin, 80, "test2"); // 真值变化
            Assert.AreEqual(80L, svc.Coin);
        }

        // ── CV4:TryChangeAsync 成功 → 视图刷 + 事件 ──

        [Test]
        public void CV4_TryChangeAsync_Success_RefreshesViewAndFires()
        {
            var gw = new StubGateway { NextResult = ChangeResult.Ok(50) };
            var svc = new PlayerAttrService(gw);
            svc.ApplySnapshot(0, 100, 0);

            int fireCount = 0;
            svc.OnAttrChanged += (t, v, r) => { if (t == AttrType.Diamond) fireCount++; };

            var result = svc.TryChangeAsync(AttrType.Diamond, -50, "spend").GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(ChangeReject.None, result.Reason);
            Assert.AreEqual(50L, result.NewBalance);
            Assert.AreEqual(50L, svc.Diamond, "本地视图应刷为响应 NewBalance");
            Assert.AreEqual(1, gw.CallCount);
            Assert.AreEqual(AttrType.Diamond, gw.LastType);
            Assert.AreEqual(-50L, gw.LastDelta);
            Assert.AreEqual("spend", gw.LastReason);
            Assert.AreEqual(1, fireCount, "成功应触发 OnAttrChanged 一次");
        }

        // ── CV5:TryChangeAsync 余额不足 → 视图按响应实际余额刷(不冒进) ──

        [Test]
        public void CV5_TryChangeAsync_NotEnough_RefreshesViewToActualBalance()
        {
            var gw = new StubGateway { NextResult = ChangeResult.Rejected(ChangeReject.NotEnoughBalance, newBalance: 30) };
            var svc = new PlayerAttrService(gw);
            svc.ApplySnapshot(0, 200, 0);   // 视图说 200,服务端真值 30(漂移)

            int fireCount = 0;
            svc.OnAttrChanged += (t, _, __) => { if (t == AttrType.Diamond) fireCount++; };

            var result = svc.TryChangeAsync(AttrType.Diamond, -100, "spend").GetAwaiter().GetResult();

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ChangeReject.NotEnoughBalance, result.Reason);
            Assert.AreEqual(30L, result.NewBalance);
            Assert.AreEqual(30L, svc.Diamond, "余额不足响应应刷视图为真值");
            Assert.AreEqual(1, fireCount);
        }

        // ── CV6:TryChangeAsync 服务不可用 → 视图不变(不冒进、不漂移) ──

        [Test]
        public void CV6_TryChangeAsync_ServiceUnavailable_ViewUnchanged()
        {
            var gw = new StubGateway { NextResult = ChangeResult.Rejected(ChangeReject.ServiceUnavailable) };
            var svc = new PlayerAttrService(gw);
            svc.ApplySnapshot(0, 200, 0);

            int fireCount = 0;
            svc.OnAttrChanged += (t, _, __) => { if (t == AttrType.Diamond) fireCount++; };

            var result = svc.TryChangeAsync(AttrType.Diamond, -100, "spend").GetAwaiter().GetResult();

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ChangeReject.ServiceUnavailable, result.Reason);
            Assert.AreEqual(200L, svc.Diamond, "服务不可用时视图不动(沿 38 §SV5/§7.5)");
            Assert.AreEqual(0, fireCount, "失败(非 NotEnough/OverLimit)不触发事件");
        }

        // ── CV7:TryChangeAsync 网络断 → 视图不变 ──

        [Test]
        public void CV7_TryChangeAsync_NetworkDown_ViewUnchanged()
        {
            var gw = new StubGateway { NextResult = ChangeResult.Rejected(ChangeReject.NetworkDown) };
            var svc = new PlayerAttrService(gw);
            svc.ApplySnapshot(0, 200, 0);

            var result = svc.TryChangeAsync(AttrType.Diamond, -100, "spend").GetAwaiter().GetResult();

            Assert.AreEqual(ChangeReject.NetworkDown, result.Reason);
            Assert.AreEqual(200L, svc.Diamond);
        }

        // ── 额外:All 不入协议,直接拒不发 RPC(防御性) ──

        [Test]
        public void TryChangeAsync_TypeAll_RejectedWithoutRpc()
        {
            var gw = new StubGateway { NextResult = ChangeResult.Ok(123) };
            var svc = new PlayerAttrService(gw);

            var result = svc.TryChangeAsync(AttrType.All, 10, "x").GetAwaiter().GetResult();

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ChangeReject.TypeUnknown, result.Reason);
            Assert.AreEqual(0, gw.CallCount, "AttrType.All 不应触发 RPC");
        }

        // ── 额外:构造空 gateway 抛 ──

        [Test]
        public void Constructor_NullGateway_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new PlayerAttrService(null));
        }
    }
}
