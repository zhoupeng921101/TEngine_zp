using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using GameLogic.BlockBlast.Player;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 头像/框修饰编排器 <see cref="CosmeticService"/> EditMode 单测(头像服务端权威·客户端段)。
    /// 纯逻辑、不连网 — 用桩 <see cref="ICosmeticGateway"/> 注入响应,断言:
    /// 换装乐观 set + 对账(Success 覆盖 / NotUnlocked 回退到服务端当前 / 断网回滚到旧值)、
    /// 解锁上报(Success 覆盖本地集合 / 失败不动)、bootstrap 只报差集(服务端已含不重报 + 默认 id 兜底)。
    /// 同步驱动 UniTask 用 GetAwaiter().GetResult()(桩 await CompletedTask 同步完成,沿 MetaCurrencySyncTests 范式)。
    /// </summary>
    [TestFixture]
    public class CosmeticServiceTests
    {
        // ── 桩 gateway:可预设换装 / 解锁响应,记录每次调用 ──
        private sealed class StubGateway : ICosmeticGateway
        {
            public EquipCosmeticResult EquipResult = EquipCosmeticResult.Ok(0, 0);
            public UnlockCosmeticResult UnlockResult = UnlockCosmeticResult.Ok(1, System.Array.Empty<int>());
            public UnlockCosmeticBatchResult BatchResult = UnlockCosmeticBatchResult.Ok(System.Array.Empty<int>(), System.Array.Empty<int>());
            public readonly List<(int kind, int id)> EquipCalls = new();
            public readonly List<(int kind, int id)> UnlockCalls = new();
            // 按 id 预设 unlock 响应(单条 ReportUnlock 场景);未预设则用 UnlockResult。
            public readonly Dictionary<int, UnlockCosmeticResult> UnlockById = new();
            // 批量解锁(bootstrap)记录:调用次数 + 携带项。
            public int BatchCallCount;
            public readonly List<(int kind, int id)> BatchItems = new();

            public async UniTask<EquipCosmeticResult> EquipAsync(int kind, int id)
            {
                EquipCalls.Add((kind, id));
                await UniTask.CompletedTask;
                return EquipResult;
            }

            public async UniTask<UnlockCosmeticResult> UnlockAsync(int kind, int id)
            {
                UnlockCalls.Add((kind, id));
                await UniTask.CompletedTask;
                return UnlockById.TryGetValue(id, out var r) ? r : UnlockResult;
            }

            public async UniTask<UnlockCosmeticBatchResult> UnlockBatchAsync(IReadOnlyList<(int kind, int id)> items)
            {
                BatchCallCount++;
                if (items != null) BatchItems.AddRange(items);
                await UniTask.CompletedTask;
                return BatchResult;
            }
        }

        private static PlayerInfo NewPlayer(int curAvatar = PlayerInfo.DefaultAvatarId, int curFrame = PlayerInfo.DefaultFrameId,
            int[] avatars = null, int[] frames = null)
        {
            return new PlayerInfo
            {
                Id = "t", Name = "t", Exp = 0,
                CurrentAvatarId = curAvatar, CurrentFrameId = curFrame,
                UnlockedAvatarIds = avatars ?? new[] { PlayerInfo.DefaultAvatarId },
                UnlockedFrameIds = frames ?? new[] { PlayerInfo.DefaultFrameId },
            };
        }

        private static AvatarEntry Entry(int id, int type, int cond = UnlockCond.Level, int param = 1)
            => new AvatarEntry { Id = id, Type = type, UnlockCond = cond, UnlockParam = param };

        // ── E1:构造空 gateway 抛 ──
        [Test]
        public void E1_Constructor_NullGateway_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new CosmeticService(null));
        }

        // ── E2:换装成功 → 乐观 set 后用服务端回带当前 id 覆盖 ──
        [Test]
        public void E2_Equip_Success_OverwritesFromResponse()
        {
            var gw = new StubGateway { EquipResult = EquipCosmeticResult.Ok(7, 101) };
            var svc = new CosmeticService(gw);
            var p = NewPlayer();

            var r = svc.EquipAsync(p, Entry(7, AvatarType.Avatar)).GetAwaiter().GetResult();

            Assert.IsTrue(r.Success);
            Assert.AreEqual(1, gw.EquipCalls.Count);
            Assert.AreEqual((AvatarType.Avatar, 7), gw.EquipCalls[0]);
            Assert.AreEqual(7, p.CurrentAvatarId, "成功用服务端回带值覆盖");
            Assert.AreEqual(101, p.CurrentFrameId);
        }

        // ── E3:换装 NotUnlocked → 回退到服务端当前 id(非旧本地值) ──
        [Test]
        public void E3_Equip_NotUnlocked_RollsBackToServerCurrent()
        {
            // 服务端当前佩戴头像 = 3(权威),客户端尝试换到未解锁的 9。
            var gw = new StubGateway { EquipResult = EquipCosmeticResult.Rejected(EquipCosmeticOutcome.NotUnlocked, 3, 101) };
            var svc = new CosmeticService(gw);
            var p = NewPlayer(curAvatar: 3);

            var r = svc.EquipAsync(p, Entry(9, AvatarType.Avatar)).GetAwaiter().GetResult();

            Assert.IsFalse(r.Success);
            Assert.AreEqual(EquipCosmeticOutcome.NotUnlocked, r.Outcome);
            Assert.AreEqual(3, p.CurrentAvatarId, "乐观 set 到 9 后被服务端当前值 3 回退");
        }

        // ── E4:换装断网 → 回滚到发前旧值(响应不可信) ──
        [Test]
        public void E4_Equip_NetworkDown_RollsBackToPrev()
        {
            var gw = new StubGateway { EquipResult = EquipCosmeticResult.Rejected(EquipCosmeticOutcome.NetworkDown) };
            var svc = new CosmeticService(gw);
            var p = NewPlayer(curAvatar: 5);

            var r = svc.EquipAsync(p, Entry(9, AvatarType.Avatar)).GetAwaiter().GetResult();

            Assert.IsFalse(r.Success);
            Assert.AreEqual(5, p.CurrentAvatarId, "断网响应不可信 → 回滚到发前旧值 5");
        }

        // ── E5:换装框(kind=Frame)乐观 set 走 CurrentFrameId ──
        [Test]
        public void E5_Equip_Frame_Success()
        {
            var gw = new StubGateway { EquipResult = EquipCosmeticResult.Ok(1, 102) };
            var svc = new CosmeticService(gw);
            var p = NewPlayer();

            svc.EquipAsync(p, Entry(102, AvatarType.Frame)).GetAwaiter().GetResult();

            Assert.AreEqual((AvatarType.Frame, 102), gw.EquipCalls[0]);
            Assert.AreEqual(102, p.CurrentFrameId);
        }

        // ── E6:解锁上报成功 → 用回带集合覆盖本地对应集合 ──
        [Test]
        public void E6_ReportUnlock_Success_OverwritesSet()
        {
            var gw = new StubGateway { UnlockResult = UnlockCosmeticResult.Ok(AvatarType.Avatar, new[] { 1, 4 }) };
            var svc = new CosmeticService(gw);
            var p = NewPlayer();

            var r = svc.ReportUnlockAsync(p, AvatarType.Avatar, 4).GetAwaiter().GetResult();

            Assert.IsTrue(r.Success);
            Assert.AreEqual((AvatarType.Avatar, 4), gw.UnlockCalls[0]);
            CollectionAssert.AreEquivalent(new[] { 1, 4 }, p.UnlockedAvatarIds, "用服务端回带集合覆盖");
        }

        // ── E7:解锁上报失败 → 不动本地集合 ──
        [Test]
        public void E7_ReportUnlock_Failure_LeavesSet()
        {
            var gw = new StubGateway { UnlockResult = UnlockCosmeticResult.Rejected(AvatarType.Avatar, UnlockCosmeticOutcome.ServiceUnavailable) };
            var svc = new CosmeticService(gw);
            var p = NewPlayer(avatars: new[] { 1 });

            svc.ReportUnlockAsync(p, AvatarType.Avatar, 4).GetAwaiter().GetResult();

            CollectionAssert.AreEquivalent(new[] { 1 }, p.UnlockedAvatarIds, "失败不动本地集合");
        }

        // ── E8:解锁上报非法 id / kind 直接跳过、不发 RPC ──
        [Test]
        public void E8_ReportUnlock_InvalidInput_NoRpc()
        {
            var gw = new StubGateway();
            var svc = new CosmeticService(gw);
            var p = NewPlayer();

            svc.ReportUnlockAsync(p, AvatarType.Avatar, 0).GetAwaiter().GetResult();  // id ≤ 0
            svc.ReportUnlockAsync(p, 99, 5).GetAwaiter().GetResult();                 // kind 非法

            Assert.AreEqual(0, gw.UnlockCalls.Count, "非法输入不发 RPC");
        }

        // ── E9:bootstrap 只报差集(服务端已含默认 id → 只报缺的等级项) ──
        [Test]
        public void E9_Bootstrap_ReportsOnlyMissing()
        {
            // 服务端快照已含默认头像 1 + 框 101;客户端等级 5,配置里 id 2(≥5)应解锁但服务端缺。
            var gw = new StubGateway
            {
                UnlockResult = UnlockCosmeticResult.Ok(AvatarType.Avatar, new[] { 1, 2 })
            };
            gw.UnlockById[2] = UnlockCosmeticResult.Ok(AvatarType.Avatar, new[] { 1, 2 });
            var svc = new CosmeticService(gw);
            var p = NewPlayer(avatars: new[] { 1 }, frames: new[] { 101 });
            p.Exp = PlayerLevelConfig.CumExp(5); // Level=5

            var all = new List<AvatarEntry>
            {
                Entry(1, AvatarType.Avatar, UnlockCond.Level, 1),   // 已含,不报
                Entry(2, AvatarType.Avatar, UnlockCond.Level, 5),   // 达标且缺 → 报
                Entry(50, AvatarType.Avatar, UnlockCond.Level, 50), // 未达标 → 不报
                Entry(3, AvatarType.Avatar, UnlockCond.Event, 9001),// EVENT → 不报
                Entry(101, AvatarType.Frame, UnlockCond.Level, 1),  // 已含,不报
            };

            int reported = svc.BootstrapUnlocksAsync(p, all).GetAwaiter().GetResult();

            Assert.AreEqual(1, reported, "只报差集里的 id 2");
            Assert.AreEqual(1, gw.BatchCallCount, "差集合成一次批量上报");
            CollectionAssert.AreEquivalent(new[] { (AvatarType.Avatar, 2) }, gw.BatchItems, "批量只含差集 id 2");
        }

        // ── E10:bootstrap 首登服务端空集 → 补报默认头像 1 + 框 101 ──
        [Test]
        public void E10_Bootstrap_EmptyServerSet_ReportsDefaults()
        {
            var gw = new StubGateway
            {
                UnlockResult = UnlockCosmeticResult.Ok(AvatarType.Avatar, new[] { 1 })
            };
            var svc = new CosmeticService(gw);
            // 服务端首登空解锁集(snapshot 覆盖后本地也空)。
            var p = NewPlayer(avatars: System.Array.Empty<int>(), frames: System.Array.Empty<int>());
            p.Exp = 0; // Level=1

            int reported = svc.BootstrapUnlocksAsync(p, null).GetAwaiter().GetResult();

            Assert.AreEqual(2, reported, "补报默认头像 1 + 框 101");
            Assert.AreEqual(1, gw.BatchCallCount, "两 kind 差集合成一次批量上报");
            CollectionAssert.Contains(gw.BatchItems, (AvatarType.Avatar, PlayerInfo.DefaultAvatarId));
            CollectionAssert.Contains(gw.BatchItems, (AvatarType.Frame, PlayerInfo.DefaultFrameId));
        }

        // ── E11:bootstrap 服务端已含全部应解锁 → 零上报 ──
        [Test]
        public void E11_Bootstrap_AllPresent_NoReport()
        {
            var gw = new StubGateway();
            var svc = new CosmeticService(gw);
            var p = NewPlayer(avatars: new[] { 1 }, frames: new[] { 101 });
            p.Exp = 0; // Level=1,只有默认应解锁

            int reported = svc.BootstrapUnlocksAsync(p, null).GetAwaiter().GetResult();

            Assert.AreEqual(0, reported, "应解锁集已全在服务端 → 零上报");
            Assert.AreEqual(0, gw.BatchCallCount, "无差集 → 不发批量请求");
        }
    }
}
