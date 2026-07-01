using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using GameLogic;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Core;
using GameLogic.BlockBlast.Player;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 档案状态服务端权威(皮肤态 + 神庙装饰厅数)客户端段单测(皮肤/神庙装饰服务端权威·客户端段 3b)。
    /// 纯逻辑、不连网 — 用桩 <see cref="IProfileStateGateway"/> 注入响应,断言:
    /// - 上报三态口径(SET 语义):彩色态 skinMono=0/id=-1、单色态 skinMono=1/id=当前;templeDecorated bool[] → 已装饰厅数标量;
    /// - null 态不发 RPC;
    /// - 快照读三态(<see cref="GameContext.ApplyServerProfileStateSnapshot"/>):templeDecorated 标量→前缀布尔数组、SkinMonoId=-1 视作未选(彩色态)。
    /// 同步驱动 UniTask 用 GetAwaiter().GetResult()(桩 await CompletedTask 同步完成,沿 MetaCurrencySyncTests 范式)。
    /// </summary>
    [TestFixture]
    public class ProfileStateSyncTests
    {
        private const int HallCount = TempleConfig.HallCount;

        // ── 桩 RPC 接缝:记录每次调用的三态,返回预设结果码 ──
        private sealed class StubGateway : IProfileStateGateway
        {
            public readonly List<(int skinMono, int skinMonoId, int templeDecoratedCount)> Calls = new();
            public ProfileStateResult Result = ProfileStateResult.Ok();

            public async UniTask<ProfileStateResult> SetProfileStateAsync(int skinMono, int skinMonoId, int templeDecoratedCount)
            {
                Calls.Add((skinMono, skinMonoId, templeDecoratedCount));
                await UniTask.CompletedTask;
                return Result;
            }
        }

        [SetUp]
        public void SetUp()
        {
            // InMemory Provider 隔离真实 PlayerPrefs(快照读用例走 MergeMetaPersistence.Load/Save)。
            Persistence.Provider = new InMemoryPersistenceProvider();
            RandomSource.SetSeed(20260701);
            if (BlockGameState.IsValid) BlockGameState.Instance.Release();
        }

        [TearDown]
        public void TearDown()
        {
            if (BlockGameState.IsValid) BlockGameState.Instance.Release();
            if (GameContext.IsValid) GameContext.Instance.Release();
        }

        private static ProfileStateResult Report(ProfileStateSync sync, MergeOrderState s)
            => sync.ReportAsync(s).GetAwaiter().GetResult();

        // ── P1:彩色态上报 skinMono=0 / id=-1 / 装饰=0 ──
        [Test]
        public void P1_Report_ColoredState_SkinMonoZero_IdUnselected()
        {
            var gw = new StubGateway();
            var sync = new ProfileStateSync(gw);
            var s = new MergeOrderState { TempleDecorated = new bool[HallCount] };
            // Skin 缺省 = 彩色态 + 未选。

            var r = Report(sync, s);

            Assert.IsTrue(r.Success);
            Assert.AreEqual(1, gw.Calls.Count);
            Assert.AreEqual(0, gw.Calls[0].skinMono, "彩色态 skinMono=0");
            Assert.AreEqual(BlockSkinState.Unselected, gw.Calls[0].skinMonoId, "彩色态 id=-1(未选)");
            Assert.AreEqual(0, gw.Calls[0].templeDecoratedCount, "无装饰 → 0");
        }

        // ── P2:单色态上报 skinMono=1 / id=当前 / 装饰厅数=true 项数 ──
        [Test]
        public void P2_Report_MonoState_And_DecoratedCount()
        {
            var gw = new StubGateway();
            var sync = new ProfileStateSync(gw);
            var s = new MergeOrderState { TempleDecorated = new bool[HallCount] };
            for (int i = 0; i < 5; i++) s.TempleDecorated[i] = true; // 前 5 厅已装饰
            // 转单色态:OnAllClear 从候选池选一张(种子确定性)。
            s.Skin.OnAllClear(BlockSkinCatalog.MonoIds);
            Assert.IsTrue(s.Skin.IsMono, "OnAllClear 后应为单色态");

            var r = Report(sync, s);

            Assert.IsTrue(r.Success);
            Assert.AreEqual(1, gw.Calls[0].skinMono, "单色态 skinMono=1");
            Assert.AreEqual(s.Skin.MonoId, gw.Calls[0].skinMonoId, "单色态 id=当前在用");
            Assert.AreEqual(5, gw.Calls[0].templeDecoratedCount, "装饰厅数 = true 项数 5");
        }

        // ── P3:null 态不发 RPC(守卫) ──
        [Test]
        public void P3_Report_NullState_NoRpc()
        {
            var gw = new StubGateway();
            var sync = new ProfileStateSync(gw);

            var r = Report(sync, null);

            Assert.IsFalse(r.Success);
            Assert.AreEqual(0, gw.Calls.Count, "null 态不发 RPC");
        }

        // ── P4:装饰厅数标量随 bool[] 前缀数变化(非满盘) ──
        [Test]
        public void P4_Report_DecoratedCount_TracksTrueEntries()
        {
            var gw = new StubGateway();
            var sync = new ProfileStateSync(gw);
            var s = new MergeOrderState { TempleDecorated = new bool[HallCount] };
            // 全 12 厅装饰。
            for (int i = 0; i < HallCount; i++) s.TempleDecorated[i] = true;

            Report(sync, s);
            Assert.AreEqual(HallCount, gw.Calls[0].templeDecoratedCount, "全装饰 → HallCount");
        }

        // ── S1:快照读单色态三态(标量→前缀数组 + 有效 id) ──
        [Test]
        public void S1_Snapshot_MonoState_ScalarToPrefixArray()
        {
            // 无活态(BlockGameState 已释),只走缓存 dto 写回路径。
            GameContext.Instance.ApplyServerProfileStateSnapshot(skinMono: 1, skinMonoId: 247, templeDecorated: 7);

            var dto = MergeMetaPersistence.Load();
            Assert.IsNotNull(dto);
            Assert.IsTrue(dto.skinMono, "服务端单色 → 缓存 skinMono=true");
            Assert.AreEqual(247, dto.skinMonoId, "缓存 skinMonoId = 服务端权威 id");
            // templeDecorated 标量 7 → 前 7 厅 true(前缀语义)。
            Assert.IsNotNull(dto.templeDecorated);
            Assert.AreEqual(HallCount, dto.templeDecorated.Length);
            for (int i = 0; i < HallCount; i++)
                Assert.AreEqual(i < 7, dto.templeDecorated[i], $"厅 {i} 装饰前缀");
        }

        // ── S2:快照读 SkinMonoId=-1 视作未选(彩色态) ──
        [Test]
        public void S2_Snapshot_ColoredState_IdUnselected()
        {
            GameContext.Instance.ApplyServerProfileStateSnapshot(skinMono: 0, skinMonoId: BlockSkinState.Unselected, templeDecorated: 0);

            var dto = MergeMetaPersistence.Load();
            Assert.IsNotNull(dto);
            Assert.IsFalse(dto.skinMono, "服务端彩色 → 缓存 skinMono=false");
            Assert.AreEqual(BlockSkinState.Unselected, dto.skinMonoId, "彩色态 id=-1(未选,勿当有效 id 0)");
            Assert.AreEqual(0, RepairedCount(dto.templeDecorated), "无装饰");
        }

        // ── S3:装饰标量夹到 [0, HallCount](防越界) ──
        [Test]
        public void S3_Snapshot_DecoratedScalar_ClampedToHallCount()
        {
            GameContext.Instance.ApplyServerProfileStateSnapshot(skinMono: 0, skinMonoId: -1, templeDecorated: 9999);

            var dto = MergeMetaPersistence.Load();
            Assert.AreEqual(HallCount, RepairedCount(dto.templeDecorated), "超量标量夹到 HallCount");
        }

        // ── S4:快照落地已开活态(MergeState)皮肤 + 装饰数组 ──
        [Test]
        public void S4_Snapshot_AppliesToLiveState()
        {
            // 开一局 merge-order 活态,使 ApplyServerProfileStateSnapshot 走活态覆盖分支。
            var live = BlockGameState.Instance;
            live.ResetForMergeOrder(new BinaryBoard());
            Assert.IsTrue(live.MergeOrderMode, "已进 merge-order 模式");
            var state = live.MergeState;
            Assert.IsNotNull(state);

            GameContext.Instance.ApplyServerProfileStateSnapshot(skinMono: 1, skinMonoId: 51, templeDecorated: 3);

            Assert.IsTrue(state.Skin.IsMono, "活态皮肤覆盖为单色");
            Assert.AreEqual(51, state.Skin.MonoId, "活态单色 id = 服务端权威");
            Assert.AreEqual(3, RepairedCount(state.TempleDecorated), "活态装饰前 3 厅");
        }

        private static int RepairedCount(bool[] arr)
        {
            if (arr == null) return 0;
            int n = 0;
            for (int i = 0; i < arr.Length; i++) if (arr[i]) n++;
            return n;
        }
    }
}
