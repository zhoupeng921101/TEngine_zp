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
    /// 登录覆盖、批量聚合上报(一次事件多属性收敛成一次批量往返、每属性一项)、对账方向(NewAmount 覆盖)、delta=0 不发、
    /// 体力预测恢复排除、未 Ready 不报、服务不可用不漂移、推送覆盖;
    /// 消除道具 / 落子体力「服务端派生」防双扣(ExcludeEnergySpend 抬基线,零体力上报,同手 Soul/Piety/Exp 仍上报)。
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
            /// <summary>批量 RPC 调用次数(断言「一次玩法事件多属性收敛成一次批量往返」)。</summary>
            public int BatchCallCount;

            public async UniTask<ChangeResult> SendChangeRequestAsync(AttrType type, long delta, string reason)
            {
                Calls.Add((type, delta, reason));
                await UniTask.CompletedTask;
                return Results.TryGetValue(type, out var r) ? r : Default;
            }

            // 批量:每项记进同一 Calls(复用既有 CountOf / delta 断言),按 Results/Default 逐项回带结果。
            public async UniTask<IReadOnlyList<BatchChangeResultItem>> SendBatchChangeRequestAsync(
                IReadOnlyList<BatchChangeItem> items, string reason)
            {
                BatchCallCount++;
                var outp = new List<BatchChangeResultItem>(items.Count);
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    Calls.Add((it.Type, it.Delta, reason));
                    var r = Results.TryGetValue(it.Type, out var rr) ? rr : Default;
                    outp.Add(new BatchChangeResultItem(it.Type, r.Reason, r.NewBalance));
                }
                await UniTask.CompletedTask;
                return outp;
            }

            public int CountOf(AttrType type) => Calls.FindAll(c => c.type == type).Count;
        }

        private static MergeOrderState NewStateWith(int soul, int piety, int exp, int energy)
        {
            // 不调 Reset()(会读 MergeOrderConfig/配置表),直接设字段构造纯对账场景。
            return new MergeOrderState { Soul = soul, Piety = piety, Exp = exp, Energy = energy };
        }

        /// <summary>四货币快照便捷包装:六计数器初值一律 0(counter 场景由 SnapAll 显式给)。</summary>
        private static void Snap(MetaCurrencySync sync, MergeOrderState s, long soul, long piety, long exp, long energy)
            => sync.ApplySnapshot(s, soul, piety, exp, energy, 0, 0, 0, 0, 0, 0);

        /// <summary>全量快照:四货币 + 六计数器(女神等级/评级、章节、盲盒、神庙已修厅数、神庙游标)。</summary>
        private static void SnapAll(MetaCurrencySync sync, MergeOrderState s,
            long soul, long piety, long exp, long energy,
            long goddessLevel, long goddessRating, long unlockedChapter,
            long blindBoxCount, long templeRepaired, long nextRepairIndex)
            => sync.ApplySnapshot(s, soul, piety, exp, energy,
                goddessLevel, goddessRating, unlockedChapter, blindBoxCount, templeRepaired, nextRepairIndex);

        /// <summary>取活态神庙已修厅数(布尔数组 true 项数)。</summary>
        private static int RepairedCount(MergeOrderState s)
        {
            var arr = s.TempleRepaired;
            if (arr == null) return 0;
            int n = 0;
            for (int i = 0; i < arr.Length; i++) if (arr[i]) n++;
            return n;
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

            Snap(sync, s, 100, 200, 300, 25); // 服务端权威

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
            Snap(sync, s, 100, 200, 300, 25);

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

            Assert.AreEqual(1, gw.BatchCallCount, "同一事件多属性收敛成一次批量 RPC(而非逐属性单发)");
        }

        // ── T5:对账方向 — 成功响应 NewAmount 覆盖本地视图 + 基线(权威即使与乐观不同) ──
        [Test]
        public void T5_Reconcile_SuccessNewAmountOverwrites()
        {
            var gw = new StubGateway();
            gw.Results[AttrType.SoulPower] = ChangeResult.Ok(120); // 服务端权威 120(与乐观 115 不同)
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(100, 0, 0, 0);
            Snap(sync, s, 100, 0, 0, 0);

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
            Snap(sync, s, 0, 50, 0, 0);

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
            Snap(sync, s, 100, 0, 0, 0);

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
            Snap(sync, s, 0, 0, 0, 20);

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
            Snap(sync, s, 0, 0, 0, 20);

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
            Snap(sync, s, 10, 20, 30, 5);

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

            Assert.DoesNotThrow(() => Snap(sync, null, 100, 200, 300, 25));
            Assert.IsTrue(sync.IsReady, "state=null 仍记基线 + 置 Ready");
            Assert.DoesNotThrow(() => Report(sync, null));
            Assert.AreEqual(0, gw.Calls.Count, "state=null 不上报");
        }

        // ── T12:消除道具体力「服务端派生」防双扣 — 乐观扣 + ExcludeEnergySpend 抬基线 → ReportPending 不上报体力 ──
        [Test]
        public void T12_ClearToolSpend_ExcludedFromReport_NoDoubleCharge()
        {
            var gw = new StubGateway();
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(0, 0, 0, 20);
            Snap(sync, s, 0, 0, 0, 20);

            // 消除道具:本地乐观扣 5(SpendClearToolCost)+ 同步抬基线排除该笔(服务端已权威扣一次,客户端不得再报)。
            s.Energy = 15;                  // 乐观扣 5
            sync.ExcludeEnergySpend(-5);    // 基线一并降 5:待上报 delta = (15-20) - (-5) = 0

            Report(sync, s);

            Assert.AreEqual(0, gw.CountOf(AttrType.Energy), "消除道具乐观扣不应上报(服务端已扣,防双扣)");
            Assert.AreEqual(15, s.Energy, "本地体力保持乐观扣后值(HUD)");
        }

        // ── T13:落盘边界抢在 ClearTool RPC 响应之前跑(push 晚到)— 仍不双扣 ──
        [Test]
        public void T13_SaveBoundaryBeforeResponse_StillNoDoubleCharge()
        {
            var gw = new StubGateway();
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(0, 0, 0, 20);
            Snap(sync, s, 0, 0, 0, 20);

            // 时序:乐观扣 + 抬基线(同步瞬间)→ 落盘边界 ReportPending 先跑(响应 / delta-push 尚未到)。
            s.Energy = 15;
            sync.ExcludeEnergySpend(-5);
            Report(sync, s); // 抢跑:此刻不应把 -5 当净产出上报
            Assert.AreEqual(0, gw.CountOf(AttrType.Energy), "响应前落盘边界抢跑也不应上报体力(基线已抬平)");

            // 随后 ClearTool 响应 NewEnergy=15(服务端扣后余额)经 ApplyDeltaPush 到达:set 体力 + 基线到权威值(幂等对齐)。
            sync.ApplyDeltaPush(s, AttrType.Energy, 15);
            Assert.AreEqual(15, s.Energy, "响应后体力对齐服务端权威扣后余额");

            // 再一次落盘边界:基线已 = 15,无新变化 → 仍不发。
            Report(sync, s);
            Assert.AreEqual(0, gw.CountOf(AttrType.Energy), "响应到达对齐后仍不重报(总计零上报,无双扣)");
        }

        // ── T14:delta-push 晚到与响应对齐同值 — 幂等,不产生额外 delta ──
        [Test]
        public void T14_LateDeltaPush_Idempotent()
        {
            var gw = new StubGateway();
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(0, 0, 0, 20);
            Snap(sync, s, 0, 0, 0, 20);

            s.Energy = 15;
            sync.ExcludeEnergySpend(-5);
            sync.ApplyDeltaPush(s, AttrType.Energy, 15); // 响应 NewEnergy 先对齐
            sync.ApplyDeltaPush(s, AttrType.Energy, 15); // delta-push 晚到,同值,幂等

            Report(sync, s);
            Assert.AreEqual(0, gw.CountOf(AttrType.Energy), "响应 + 晚到 push 同值幂等,零上报");
            Assert.AreEqual(15, s.Energy);
        }

        // ── T16:落子体力「服务端派生」防双扣 — 不消行落子:乐观扣 PlaceCost + ExcludeEnergySpend 抬基线 → 体力零上报 ──
        // 且同一手若产 Soul/Piety/Exp(消行融合经济)仍被正常上报(本批只排除 Energy)。
        [Test]
        public void T16_PlaceSpend_ExcludedFromReport_SoulPietyExpStillReported()
        {
            var gw = new StubGateway();
            gw.Results[AttrType.SoulPower] = ChangeResult.Ok(112);
            gw.Results[AttrType.Piety] = ChangeResult.Ok(205);
            gw.Results[AttrType.GuardianExp] = ChangeResult.Ok(303);
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(100, 200, 300, 20);
            Snap(sync, s, 100, 200, 300, 20);

            // 一手落子(消行,融合经济产 Soul/Piety/Exp):
            // 体力 = eBefore(20) → 扣 PlaceCost 1(-1=19)→ 消行返 2(+2=21);净 +1。宿主捕获后 ExcludeEnergySpend(21-20=+1)。
            int eBefore = s.Energy;
            s.Energy = 19;            // SpendPlaceCost
            s.Energy = 21;            // RefundEnergy(2 lines)
            s.Soul = 112;             // 融合产出 +12
            s.Piety = 205;            // +5
            s.Exp = 303;              // +3
            sync.ExcludeEnergySpend(s.Energy - eBefore);   // 只动 _baseEnergy,不碰 Soul/Piety/Exp 基线

            Report(sync, s);

            Assert.AreEqual(0, gw.CountOf(AttrType.Energy), "落子体力服务端派生,客户端不上报(防双扣)");
            Assert.AreEqual(1, gw.CountOf(AttrType.SoulPower), "同一手 Soul 仍正常上报(本批只排除 Energy)");
            Assert.AreEqual(1, gw.CountOf(AttrType.Piety), "同一手 Piety 仍正常上报");
            Assert.AreEqual(1, gw.CountOf(AttrType.GuardianExp), "同一手 Exp 仍正常上报");
            Assert.AreEqual(12L, gw.Calls.Find(c => c.type == AttrType.SoulPower).delta, "Soul 上报净 +12");
            Assert.AreEqual(5L, gw.Calls.Find(c => c.type == AttrType.Piety).delta, "Piety 上报净 +5");
            Assert.AreEqual(3L, gw.Calls.Find(c => c.type == AttrType.GuardianExp).delta, "Exp 上报净 +3");
        }

        // ── T17:不消行普通落子 + 落盘边界抢在 Place 响应之前跑 — 仍不双扣、零体力上报 ──
        [Test]
        public void T17_PlaceSaveBoundaryBeforeResponse_StillNoDoubleCharge()
        {
            var gw = new StubGateway();
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(0, 0, 0, 20);
            Snap(sync, s, 0, 0, 0, 20);

            // 时序:乐观扣 PlaceCost(1)+ 抬基线(同步瞬间)→ 落盘边界 ReportPending 先跑(Place 响应 / delta-push 尚未到)。
            int eBefore = s.Energy;
            s.Energy = 19;                              // 不消行:仅扣 1
            sync.ExcludeEnergySpend(s.Energy - eBefore); // -1
            Report(sync, s);
            Assert.AreEqual(0, gw.CountOf(AttrType.Energy), "响应前落盘边界抢跑也不上报体力(基线已抬平)");

            // 随后 Place 响应 NewEnergy=19(服务端派生扣后余额)经 ApplyDeltaPush 到达:set 体力 + 基线到权威值。
            sync.ApplyDeltaPush(s, AttrType.Energy, 19);
            Assert.AreEqual(19, s.Energy, "响应后体力对齐服务端派生余额");

            Report(sync, s);
            Assert.AreEqual(0, gw.CountOf(AttrType.Energy), "对齐后仍不重报(总计零上报,无双扣)");
        }

        // ── T18:Place 响应 NewEnergy 与晚到 delta-push 同值 — 幂等,零上报 ──
        [Test]
        public void T18_PlaceLateDeltaPush_Idempotent()
        {
            var gw = new StubGateway();
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(0, 0, 0, 20);
            Snap(sync, s, 0, 0, 0, 20);

            // 消行落子:扣 1 返 2 → 净 +1,体力 21。宿主 ExcludeEnergySpend(+1)。
            int eBefore = s.Energy;
            s.Energy = 21;
            sync.ExcludeEnergySpend(s.Energy - eBefore);
            sync.ApplyDeltaPush(s, AttrType.Energy, 21); // 响应 NewEnergy 先对齐(服务端派生同值)
            sync.ApplyDeltaPush(s, AttrType.Energy, 21); // delta-push 晚到,同值,幂等

            Report(sync, s);
            Assert.AreEqual(0, gw.CountOf(AttrType.Energy), "响应 + 晚到 push 同值幂等,零上报");
            Assert.AreEqual(21, s.Energy);
        }

        // ── T15:体力不足被拒 — 撤销基线排除 + 回滚乐观扣,不误报、不双扣 ──
        [Test]
        public void T15_NotEnoughEnergy_RollbackExclusion_NoSpuriousReport()
        {
            var gw = new StubGateway();
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(0, 0, 0, 4); // 体力 4,不够一次消除道具(cost=5)
            Snap(sync, s, 0, 0, 0, 4);

            // 乐观扣 5(手感)+ 抬基线排除(此刻还不知会被拒)。
            s.Energy = -1;                  // 乐观扣后(极端:模拟乐观越界,实际 gate 会拦,这里测对账健壮)
            sync.ExcludeEnergySpend(-5);

            // 服务端拒(NotEnoughEnergy):宿主回滚——撤销基线排除(+5)+ 以服务端回带当前余额对齐体力 + 基线。
            sync.ExcludeEnergySpend(5);     // 撤销排除:基线回 4
            sync.ApplyDeltaPush(s, AttrType.Energy, 4); // 服务端当前余额(未扣):对齐体力 + 基线

            Assert.AreEqual(4, s.Energy, "拒绝后体力回滚到服务端权威余额");

            Report(sync, s);
            Assert.AreEqual(0, gw.CountOf(AttrType.Energy), "拒绝回滚后无残留虚假 delta,不上报");
        }

        // ══════════ 六元层计数器(云存档 blob 迁服务端权威第 1 批·客户端段)══════════

        // ── C1:全量快照覆盖六计数器本地视图 + 置 Ready + 对齐基线(随后无变化 → 不发) ──
        [Test]
        public void C1_SnapshotAll_OverwritesSixCounters_AndAlignsBaseline()
        {
            var gw = new StubGateway();
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(0, 0, 0, 0);
            // 本地旧值(应被快照覆盖)
            s.GoddessLevel = 9; s.GoddessRating = 9; s.UnlockedChapter = 9;
            s.BlindBoxCount = 9; s.NextRepairIndex = 9;
            s.TempleRepaired = new bool[GameLogic.BlockBlast.TempleConfig.HallCount];
            for (int i = 0; i < 9; i++) s.TempleRepaired[i] = true;

            // 服务端权威:女神等级 3 / 评级 2 / 章节 4 / 盲盒 5 / 已修厅数 6 / 修缮游标 6
            SnapAll(sync, s, 0, 0, 0, 0, 3, 2, 4, 5, 6, 6);

            Assert.IsTrue(sync.IsReady);
            Assert.AreEqual(3, s.GoddessLevel, "女神等级覆盖为服务端值");
            Assert.AreEqual(2, s.GoddessRating);
            Assert.AreEqual(4, s.UnlockedChapter);
            Assert.AreEqual(5, s.BlindBoxCount);
            Assert.AreEqual(6, s.NextRepairIndex);
            Assert.AreEqual(6, RepairedCount(s), "神庙已修厅数标量覆盖为服务端值(前 6 项 true)");

            Report(sync, s); // 快照后无玩法变化 → 六计数器 delta=0
            Assert.AreEqual(0, gw.Calls.Count, "基线对齐快照值,无产销则不发");
        }

        // ── C2:六计数器各净变化,每计数器恰发一笔(聚合上报,delta = 当前 - 基线)──
        [Test]
        public void C2_SixCounters_ReportNetDeltaPerCounter()
        {
            var gw = new StubGateway();
            gw.Results[AttrType.GoddessLevel]    = ChangeResult.Ok(4);
            gw.Results[AttrType.GoddessRating]   = ChangeResult.Ok(3);
            gw.Results[AttrType.UnlockedChapter] = ChangeResult.Ok(5);
            gw.Results[AttrType.BlindBoxCount]   = ChangeResult.Ok(7);
            gw.Results[AttrType.TempleRepaired]  = ChangeResult.Ok(2);
            gw.Results[AttrType.NextRepairIndex] = ChangeResult.Ok(2);
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(0, 0, 0, 0);
            SnapAll(sync, s, 0, 0, 0, 0, 3, 2, 4, 5, 1, 1);

            // 一次玩法事件:女神升 1 级、评级 +1、章节 +1、攒盒 +2、又修一厅(已修 1→2、游标 1→2)。
            s.GoddessLevel = 4;
            s.GoddessRating = 3;
            s.UnlockedChapter = 5;
            s.BlindBoxCount = 7;
            s.TempleRepaired[1] = true; // 已修 2
            s.NextRepairIndex = 2;

            Report(sync, s);

            Assert.AreEqual(1, gw.CountOf(AttrType.GoddessLevel));
            Assert.AreEqual(1, gw.CountOf(AttrType.GoddessRating));
            Assert.AreEqual(1, gw.CountOf(AttrType.UnlockedChapter));
            Assert.AreEqual(1, gw.CountOf(AttrType.BlindBoxCount));
            Assert.AreEqual(1, gw.CountOf(AttrType.TempleRepaired));
            Assert.AreEqual(1, gw.CountOf(AttrType.NextRepairIndex));
            Assert.AreEqual(1L, gw.Calls.Find(c => c.type == AttrType.GoddessLevel).delta, "女神等级净 +1");
            Assert.AreEqual(1L, gw.Calls.Find(c => c.type == AttrType.GoddessRating).delta);
            Assert.AreEqual(1L, gw.Calls.Find(c => c.type == AttrType.UnlockedChapter).delta);
            Assert.AreEqual(2L, gw.Calls.Find(c => c.type == AttrType.BlindBoxCount).delta, "盲盒净 +2");
            Assert.AreEqual(1L, gw.Calls.Find(c => c.type == AttrType.TempleRepaired).delta, "已修厅数净 +1");
            Assert.AreEqual(1L, gw.Calls.Find(c => c.type == AttrType.NextRepairIndex).delta, "修缮游标净 +1");
        }

        // ── C3:BlindBoxCount 可减(开盒)—— 净负 delta 正常上报,对账覆盖 ──
        [Test]
        public void C3_BlindBoxCount_CanDecrease_ReportsNegativeDelta()
        {
            var gw = new StubGateway();
            gw.Results[AttrType.BlindBoxCount] = ChangeResult.Ok(3); // 服务端权威扣后余额 3
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(0, 0, 0, 0);
            SnapAll(sync, s, 0, 0, 0, 0, 1, 0, 0, 5, 0, 0); // 盲盒基线 5

            s.BlindBoxCount = 3; // 开出 2 盒
            Report(sync, s);

            Assert.AreEqual(1, gw.CountOf(AttrType.BlindBoxCount));
            Assert.AreEqual(-2L, gw.Calls.Find(c => c.type == AttrType.BlindBoxCount).delta, "开盒净 -2 上报");
            Assert.AreEqual(3, s.BlindBoxCount, "对账采用服务端权威余额");

            Report(sync, s);
            Assert.AreEqual(1, gw.CountOf(AttrType.BlindBoxCount), "基线已对齐,无新变化不重报");
        }

        // ── C4:delta-push 覆盖六计数器之一(按 type set 本地字段 + 基线,幂等)──
        [Test]
        public void C4_ApplyDeltaPush_OverwritesCounterByType()
        {
            var gw = new StubGateway();
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(0, 0, 0, 0);
            SnapAll(sync, s, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0);

            sync.ApplyDeltaPush(s, AttrType.GoddessLevel, 7);
            Assert.AreEqual(7, s.GoddessLevel, "推送按 type 覆盖女神等级");

            // 神庙已修厅数:push 标量 → 布尔数组前 N 项 true。
            sync.ApplyDeltaPush(s, AttrType.TempleRepaired, 4);
            Assert.AreEqual(4, RepairedCount(s), "推送已修厅数标量 → 前 4 项 true");

            // 推送已对齐基线 → 无玩法变化不重报。
            Report(sync, s);
            Assert.AreEqual(0, gw.CountOf(AttrType.GoddessLevel), "推送已对齐基线,不重报");
            Assert.AreEqual(0, gw.CountOf(AttrType.TempleRepaired), "推送已对齐基线,不重报");
        }

        // ── C5:RebindBaseline 把六计数器基线重对齐到活态(开窗 ImportMeta 后,免首刀把缓存值当产出重报)──
        [Test]
        public void C5_RebindBaseline_RealignsSixCountersToState()
        {
            var gw = new StubGateway();
            var sync = new MetaCurrencySync(gw);

            // 登录窗未开:快照只记基线(女神等级 2 / 已修厅数 3),state=null。
            sync.ApplySnapshot(null, 0, 0, 0, 0, 2, 0, 0, 0, 3, 3);

            // 开窗新建 state,ImportMeta 从本地缓存读出计数器(与快照基线不同:女神 2、已修 3,缓存也是 2/3 一致场景)。
            var s = NewStateWith(0, 0, 0, 0);
            s.GoddessLevel = 2;
            s.NextRepairIndex = 3;
            s.TempleRepaired = new bool[GameLogic.BlockBlast.TempleConfig.HallCount];
            for (int i = 0; i < 3; i++) s.TempleRepaired[i] = true;

            sync.RebindBaseline(s);

            // 无玩法变化 → 首刀不发(基线已重对齐到活态)。
            Report(sync, s);
            Assert.AreEqual(0, gw.CountOf(AttrType.GoddessLevel), "重对齐后无产销不发");
            Assert.AreEqual(0, gw.CountOf(AttrType.TempleRepaired));
            Assert.AreEqual(0, gw.CountOf(AttrType.NextRepairIndex));

            // 之后真升一级 → 只上报本次开窗后的真实增量。
            gw.Results[AttrType.GoddessLevel] = ChangeResult.Ok(3);
            s.GoddessLevel = 3;
            Report(sync, s);
            Assert.AreEqual(1, gw.CountOf(AttrType.GoddessLevel));
            Assert.AreEqual(1L, gw.Calls.Find(c => c.type == AttrType.GoddessLevel).delta, "只报本次真实 +1");
        }

        // ── C6:纯快照(无产销)六计数器全 0 → 不发任何计数器请求 ──
        [Test]
        public void C6_SnapshotOnly_NoCounterReport()
        {
            var gw = new StubGateway();
            var sync = new MetaCurrencySync(gw);
            var s = NewStateWith(0, 0, 0, 0);
            SnapAll(sync, s, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0);

            Report(sync, s);
            Assert.AreEqual(0, gw.Calls.Count, "快照后无变化,四货币 + 六计数器均不发");
        }
    }
}
