using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Player;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 四货币对账器 <see cref="MetaCurrencySync"/> EditMode 单测(P2 全栈迁移·客户端段)。
    /// 纯逻辑、不依赖 UnityEngine / 不连网 — 用桩 <see cref="IRpcGateway"/> 注入响应,断言:
    /// 登录覆盖、聚合上报(每货币每事件一笔)、对账方向(NewAmount 覆盖)、delta=0 不发、
    /// 体力预测恢复排除、未 Ready 不报、服务不可用不漂移、推送覆盖。
    /// 同步驱动 UniTask 用 GetAwaiter().GetResult()(桩 await CompletedTask 同步完成,沿 PlayerAttrServiceTests 范式)。
    /// </summary>
    [TestFixture]
    public class MetaCurrencySyncTests
    {
        // ── 桩 RPC 接缝:可按 type 预设响应,记录每次调用 ──
        private sealed class StubGateway : IRpcGateway
        {
            // 按 type 预设响应;未预设的 type 默认 Ok(NewBalance = 该次请求的 delta 解析不出真值,故测试显式预设)。
            public readonly Dictionary<AttrType, ChangeResult> Results = new Dictionary<AttrType, ChangeResult>();
            public readonly List<(AttrType type, long delta, string reason)> Calls = new();
            public ChangeResult Default = ChangeResult.Ok(0);

            public async UniTask<ChangeResult> SendChangeRequestAsync(AttrType type, long delta, string reason)
            {
                Calls.Add((type, delta, reason));
                await UniTask.CompletedTask;
                return Results.TryGetValue(type, out var r) ? r : Default;
            }

            public int CountOf(AttrType type) => Calls.FindAll(c => c.type == type).Count;
        }

        private static MergeOrderState NewStateWith(int soul, int piety, int exp, int energy)
        {
            // 不调 Reset()(会读 MergeOrderConfig/配置表),直接设字段构造纯对账场景。
            return new MergeOrderState { Soul = soul, Piety = piety, Exp = exp, Energy = energy };
        }

        private static void Report(MetaCurrencySync sync, MergeOrderState s)
            => sync.ReportPending(s, "test").GetAwaiter().GetResult();

        // ── T1:构造空 gateway 抛 ──
        [Test]
        public void T1_Constructor_NullGateway_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new MetaCurrencySync(null));
        }

        // ── T2:未 ApplySnapshot(未 Ready)→ ReportPending 不发任何请求 ──
        [Test]
        public void T2_NotReady_NoReport()
        {
            var gw = new StubGateway();
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(10, 20, 30, 5);

            Report(sync, s);

            Assert.IsFalse(sync.IsReady);
            Assert.AreEqual(0, gw.Calls.Count, "未 Ready 不上报");
        }

        // ── T3:ApplySnapshot 覆盖本地视图 + 置 Ready + 对齐基线(随后无变化 → delta=0 不发) ──
        [Test]
        public void T3_ApplySnapshot_OverwritesViewAndBaseline()
        {
            var gw = new StubGateway();
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(999, 999, 999, 999); // 本地旧值

            sync.ApplySnapshot(s, 100, 200, 300, 25); // 服务端权威

            Assert.IsTrue(sync.IsReady);
            Assert.AreEqual(100, s.Soul, "登录覆盖本地 Soul 为服务端值");
            Assert.AreEqual(200, s.Piety);
            Assert.AreEqual(300, s.Exp);
            Assert.AreEqual(25, s.Energy);

            Report(sync, s); // 快照后无玩法变化 → 每货币 delta=0
            Assert.AreEqual(0, gw.Calls.Count, "基线对齐快照值,无产销则不发");
        }

        // ── T4:聚合上报 — 同一事件内多货币各净变化,每货币恰发一笔(不逐笔) ──
        [Test]
        public void T4_Aggregate_OneRequestPerCurrencyPerEvent()
        {
            var gw = new StubGateway();
            gw.Results[AttrType.SoulPower] = ChangeResult.Ok(115);
            gw.Results[AttrType.Piety] = ChangeResult.Ok(250);
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(100, 200, 300, 25);
            sync.ApplySnapshot(s, 100, 200, 300, 25);

            // 模拟一次玩法事件内级联:Soul 多笔累加后净 +15,Piety 净 +50,Exp/Energy 不变。
            s.Soul = 115;   // 等价多笔 +5/+5/+5 聚合
            s.Piety = 250;  // +50

            Report(sync, s);

            Assert.AreEqual(1, gw.CountOf(AttrType.SoulPower), "Soul 聚合成一笔");
            Assert.AreEqual(1, gw.CountOf(AttrType.Piety), "Piety 聚合成一笔");
            Assert.AreEqual(0, gw.CountOf(AttrType.GuardianExp), "Exp 无变化不发");
            Assert.AreEqual(0, gw.CountOf(AttrType.Energy), "Energy 无变化不发");

            // delta = 净变化量(非逐笔)
            var soulCall = gw.Calls.Find(c => c.type == AttrType.SoulPower);
            Assert.AreEqual(15L, soulCall.delta, "Soul 上报净 delta=+15");
            var pietyCall = gw.Calls.Find(c => c.type == AttrType.Piety);
            Assert.AreEqual(50L, pietyCall.delta, "Piety 上报净 delta=+50");
        }

        // ── T5:对账方向 — 成功响应 NewAmount 覆盖本地视图 + 基线(权威即使与乐观不同) ──
        [Test]
        public void T5_Reconcile_SuccessNewAmountOverwrites()
        {
            var gw = new StubGateway();
            gw.Results[AttrType.SoulPower] = ChangeResult.Ok(120); // 服务端权威 120(与乐观 115 不同)
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(100, 0, 0, 0);
            sync.ApplySnapshot(s, 100, 0, 0, 0);

            s.Soul = 115; // 乐观 +15
            Report(sync, s);

            Assert.AreEqual(120, s.Soul, "本地视图校正为服务端权威 NewAmount(哪怕与乐观不同)");

            // 再次上报:基线已对齐 120,无新变化 → 不再发。
            Report(sync, s);
            Assert.AreEqual(1, gw.CountOf(AttrType.SoulPower), "基线已对齐权威,无新变化不重发");
        }

        // ── T6:扣减不足 — NotEnough 把乐观值回滚到服务端权威实际余额 ──
        [Test]
        public void T6_Reconcile_NotEnough_RollsBackToActual()
        {
            var gw = new StubGateway();
            gw.Results[AttrType.Piety] = ChangeResult.Rejected(ChangeReject.NotEnoughBalance, newBalance: 30);
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(0, 50, 0, 0);
            sync.ApplySnapshot(s, 0, 50, 0, 0);

            s.Piety = 10; // 乐观扣 40(买东西),但服务端实际只有 30
            Report(sync, s);

            Assert.AreEqual(30, s.Piety, "扣减不足:本地校正回服务端权威实际余额");
        }

        // ── T7:服务不可用 — 视图与基线都不动,下次边界自然重报(未对账=未消费) ──
        [Test]
        public void T7_ServiceUnavailable_NoDriftAndRetries()
        {
            var gw = new StubGateway();
            gw.Results[AttrType.SoulPower] = ChangeResult.Rejected(ChangeReject.ServiceUnavailable);
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(100, 0, 0, 0);
            sync.ApplySnapshot(s, 100, 0, 0, 0);

            s.Soul = 115;
            Report(sync, s);
            Assert.AreEqual(115, s.Soul, "服务不可用:本地视图不动(不冒进、不漂移)");
            Assert.AreEqual(1, gw.CountOf(AttrType.SoulPower));

            // 下次边界:基线未推进,delta 仍 = 115-100 = 15,重报。
            Report(sync, s);
            Assert.AreEqual(2, gw.CountOf(AttrType.SoulPower), "未对账 → 下次边界重报");
            Assert.AreEqual(15L, gw.Calls[1].delta, "重报 delta 仍为 +15");
        }

        // ── T8:体力预测恢复排除 — RegenSinceReport 不计入上报 delta,仅玩法真实变更上报 ──
        [Test]
        public void T8_EnergyRegen_ExcludedFromReport()
        {
            var gw = new StubGateway();
            gw.Results[AttrType.Energy] = ChangeResult.Ok(18);
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(0, 0, 0, 20);
            sync.ApplySnapshot(s, 0, 0, 0, 20);

            // 玩法真实扣 5(落子)+ 本地预测恢复 +3(ApplyTimeRegen 累计到 RegenSinceReport):
            // 当前体力 20-5+3=18;待上报应仅 -5(扣),恢复 +3 排除。
            s.Energy = 18;
            s.RegenSinceReport = 3;
            Report(sync, s);

            Assert.AreEqual(1, gw.CountOf(AttrType.Energy));
            var call = gw.Calls.Find(c => c.type == AttrType.Energy);
            Assert.AreEqual(-5L, call.delta, "上报仅玩法真实扣 -5,预测恢复 +3 已排除");
            Assert.AreEqual(0, s.RegenSinceReport, "恢复量已并入基线并清零");
            Assert.AreEqual(18, s.Energy, "服务端权威 NewAmount 校正后体力 18");
        }

        // ── T9:纯恢复无玩法变更 — 不发体力请求(恢复全排除后 delta=0) ──
        [Test]
        public void T9_EnergyOnlyRegen_NoReport()
        {
            var gw = new StubGateway();
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(0, 0, 0, 20);
            sync.ApplySnapshot(s, 0, 0, 0, 20);

            // 仅预测恢复 +5,无玩法扣/奖:当前 25、RegenSinceReport=5 → 待上报 delta = (25-20) - 5 = 0。
            s.Energy = 25;
            s.RegenSinceReport = 5;
            Report(sync, s);

            Assert.AreEqual(0, gw.CountOf(AttrType.Energy), "纯预测恢复不上报(全排除后 delta=0)");
            Assert.AreEqual(25, s.Energy, "纯恢复不发请求,体力保持本地预测值");
        }

        // ── T10:推送覆盖 — ApplyDeltaPush 按 type set 本地字段 + 基线为权威值 ──
        [Test]
        public void T10_ApplyDeltaPush_OverwritesByType()
        {
            var gw = new StubGateway();
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(10, 20, 30, 5);
            sync.ApplySnapshot(s, 10, 20, 30, 5);

            sync.ApplyDeltaPush(s, AttrType.Piety, 999);
            Assert.AreEqual(999, s.Piety, "推送按 type 覆盖本地视图");
            Assert.AreEqual(10, s.Soul, "推送不动其它货币");

            // 推送已对齐基线 → 无玩法变化时不重报该货币。
            Report(sync, s);
            Assert.AreEqual(0, gw.CountOf(AttrType.Piety), "推送已对齐基线,无玩法变化不重报");

            // 非四货币 type(Coin)忽略,不崩。
            Assert.DoesNotThrow(() => sync.ApplyDeltaPush(s, AttrType.Coin, 123));
        }

        // ── T11:state 为 null(窗未开)— ApplySnapshot 仅记基线不崩、ReportPending 安全返回 ──
        [Test]
        public void T11_NullState_SafeNoThrow()
        {
            var gw = new StubGateway();
            var sync = new MetaCurrencySync(gw);

            Assert.DoesNotThrow(() => sync.ApplySnapshot(null, 100, 200, 300, 25));
            Assert.IsTrue(sync.IsReady, "state=null 仍记基线 + 置 Ready");
            Assert.DoesNotThrow(() => Report(sync, null));
            Assert.AreEqual(0, gw.Calls.Count, "state=null 不上报");
        }
    }
}
