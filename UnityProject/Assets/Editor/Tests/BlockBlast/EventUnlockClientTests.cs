using System;
using NUnit.Framework;
using GameLogic;
using GameLogic.Config;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Item;
using GameLogic.BlockBlast.Player;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// EVENT 头像解锁通路客户端段验收测试（设计 41 §八 CV2-CV5）。
    /// 解析层 + 适配器 + 边界全纯逻辑：Resolve(EVENT) 产 EventUnlock 结构、ResolveAndApply 经 GrantOnAcquire
    /// 命中 GrantUnlock 落 UnlockedAvatarIds 幂等、entry/PlayerInfo 为 null 静默不抛。
    /// 配置层（CV1）由 Luban 重导后 .bytes 直读核（C 段断言，本类不重复）。
    /// 落盘触发（CV8）由调用方核（本类不模拟 GameContext.SavePlayer 整链）。
    /// </summary>
    [TestFixture]
    public class EventUnlockClientTests
    {
        /// <summary>EVENT 解锁道具 POCO（与 itemdef.xlsx id=30101 同值）。</summary>
        private static ItemDef EventItem(int useValue = 3) => new ItemDef
        {
            Id = 30101, Name = 110101, Desc = 210101, Icon = "icon_avatar_event",
            Quality = 4, Light = null, Automatic = 1, Type = 2 /* MATERIAL */, Param = 0,
            UseEffect = 5, UseValue = useValue, UseNum = 1, UseLevel = 0, Stacking = 0,
            Term = 0, TermPrompt = 0, TermTime = null, Compensate = 0, CompensateEmail = 0,
            JumpList = null,
        };

        /// <summary>EVENT 头像样例 entry（与 avatar.xlsx id=3 同值）。</summary>
        private static AvatarEntry StarAvatarEntry() => new AvatarEntry
        {
            Id = 3, Type = AvatarType.Avatar, Image = "avt_star",
            UnlockText = 300003, UnlockCond = UnlockCond.Event, UnlockParam = 9001,
        };

        /// <summary>构造初始 PlayerInfo（默认头像 1 / 框 101 + 经验 0）。</summary>
        private static PlayerInfo NewPlayer() => new PlayerInfo
        {
            Id = "id_evt", Name = "PlayerEvt", RenameCount = 0, Exp = 0,
            CurrentAvatarId = PlayerInfo.DefaultAvatarId,
            CurrentFrameId = PlayerInfo.DefaultFrameId,
            UnlockedAvatarIds = new[] { PlayerInfo.DefaultAvatarId },
            UnlockedFrameIds = new[] { PlayerInfo.DefaultFrameId },
        };

        [SetUp]
        public void Setup()
        {
            AvatarConfigMgr.ResetForTest();
            ItemConfigMgr.ResetForTest();
            if (GameContext.IsValid) GameContext.Instance.Release();
        }

        [TearDown]
        public void TearDown()
        {
            AvatarConfigMgr.ResetForTest();
            ItemConfigMgr.ResetForTest();
            if (GameContext.IsValid) GameContext.Instance.Release();
        }

        // ───────────────────────── CV2：Resolve EVENT 分支产出 ─────────────────────────

        [Test]
        public void CV2_Resolve_EventUnlock_ProducesPayload()
        {
            var p = ItemGrant.Resolve(EventItem(useValue: 3), 1);
            Assert.AreEqual(GrantKind.EventUnlock, p.Kind, "case 5 应产 EventUnlock");
            Assert.AreEqual(3, p.TargetId, "TargetId = use_value = avatar id");
            Assert.AreEqual(1, p.Amount, "Amount = count（适配器忽略，但语义存）");
            Assert.AreEqual(0, p.Level);
            Assert.AreEqual(0, p.Times);
        }

        // ───────────────────────── CV3：ResolveAndApply 命中 GrantUnlock + 幂等 ─────────────────────────

        [Test]
        public void CV3_ResolveAndApply_EventUnlock_GrantsAvatarIdempotent()
        {
            // 灌：① AvatarConfigMgr 注 avt_star（id=3）② GameContext 实例化并注入 PlayerInfo
            AvatarConfigMgr.InitForTest(new[] { StarAvatarEntry() });

            // 把 player 注入 GameContext（沿 GameContextTests 范式 + 设计 41 §3.5：适配器走 GameContext.Instance.Player）。
            // 构造一份「不含 avt_star id=3」的 DTO（仅默认头像 1），经 ImportFromMeta 重建到 GameContext。
            // 注意 ImportFromMeta.NormalizeSet 会保证集合含默认头像 id；本测试聚焦「3 是否被 EVENT 适配器加入」。
            var dto = new MergeMetaSave { version = MergeMetaPersistence.CurrentVersion };
            NewPlayer().ExportToMeta(dto);
            GameContext.Instance.InitPlayerFromMeta(dto, new Random(0));
            var pInCtx = GameContext.Instance.Player;
            Assert.IsNotNull(pInCtx);
            // 前置：清掉可能由 PlayerPrefs 历史污染带入的 3（手动覆盖集合，确保起点干净）
            pInCtx.UnlockedAvatarIds = new[] { PlayerInfo.DefaultAvatarId };
            Assert.IsFalse(AvatarUnlockService.IsUnlocked(pInCtx, StarAvatarEntry()),
                "前置：avt_star 应未解锁（集合无 3，UnlockCond=Event 无法等级判）");

            // 触发 EVENT：经 GrantOnAcquire → ResolveAndApply → ApplyEventUnlock → AvatarUnlockService.GrantUnlock
            var def = EventItem(useValue: 3);
            var produced = ItemGrant.GrantOnAcquire(def, 1, state: null, rng: null);

            Assert.AreEqual(1, produced.Count, "EVENT 适配器记 1 个 EventUnlock 产出结构");
            Assert.AreEqual(GrantKind.EventUnlock, produced[0].Kind);
            Assert.IsTrue(ItemGrant.ContainsEventUnlock(produced),
                "ContainsEventUnlock 帮助方法应识别 EVENT 产出（CV8 D3 触发 SavePlayer 的判据）");

            // 断言落入集合
            Assert.IsNotNull(pInCtx.UnlockedAvatarIds);
            Assert.Contains(3, pInCtx.UnlockedAvatarIds, "avt_star id=3 应已写入 UnlockedAvatarIds");
            Assert.IsTrue(AvatarUnlockService.IsUnlocked(pInCtx, StarAvatarEntry()),
                "IsUnlocked(avt_star) 应返 true");

            // 幂等：再触发一次仍只含 3，长度不增
            int lenBefore = pInCtx.UnlockedAvatarIds.Length;
            var produced2 = ItemGrant.GrantOnAcquire(def, 1, state: null, rng: null);
            Assert.IsTrue(ItemGrant.ContainsEventUnlock(produced2));
            Assert.AreEqual(lenBefore, pInCtx.UnlockedAvatarIds.Length, "重复触发 EVENT 应幂等不重复加");
            Assert.Contains(3, pInCtx.UnlockedAvatarIds);
        }

        // ───────────────────────── CV4：适配器静默 · entry 不存在 ─────────────────────────

        [Test]
        public void CV4_EventUnlock_EntryNotFound_SilentNoThrow()
        {
            // 灌：AvatarConfigMgr 不注入任何 entry（GetAvatar 任何 id 返 null）+ GameContext 注 player
            AvatarConfigMgr.InitForTest(Array.Empty<AvatarEntry>());
            var dto = new MergeMetaSave { version = MergeMetaPersistence.CurrentVersion };
            NewPlayer().ExportToMeta(dto);
            GameContext.Instance.InitPlayerFromMeta(dto, new Random(0));
            var pInCtx = GameContext.Instance.Player;
            int[] before = pInCtx.UnlockedAvatarIds;

            // EVENT 指向 use_value=999（avatar 表无）→ GetAvatar 返 null → ApplyEventUnlock 静默
            Assert.DoesNotThrow(() =>
            {
                var produced = ItemGrant.GrantOnAcquire(EventItem(useValue: 999), 1, null, null);
                Assert.AreEqual(1, produced.Count, "仍记 EVENT 产出结构（CV4 produced 含 EVENT）");
                Assert.AreEqual(GrantKind.EventUnlock, produced[0].Kind);
                Assert.AreEqual(999, produced[0].TargetId);
            });
            // UnlockedAvatarIds 不变
            Assert.AreSame(before, pInCtx.UnlockedAvatarIds, "entry 不存在 → 不写集合（引用未变）");
        }

        // ───────────────────────── CV5：适配器静默 · PlayerInfo 为 null ─────────────────────────

        [Test]
        public void CV5_EventUnlock_PlayerNull_SilentNoThrow()
        {
            AvatarConfigMgr.InitForTest(new[] { StarAvatarEntry() });
            // 故意不实例化 GameContext（GameContext.IsValid = false）→ ApplyEventUnlock 第一步 IsValid 检查短路
            Assert.IsFalse(GameContext.IsValid);
            Assert.DoesNotThrow(() =>
            {
                var produced = ItemGrant.GrantOnAcquire(EventItem(useValue: 3), 1, null, null);
                Assert.AreEqual(1, produced.Count, "仍记 EVENT 产出结构（CV5 produced 含 EVENT）");
                Assert.AreEqual(GrantKind.EventUnlock, produced[0].Kind);
            });
        }

        // ───────────────────────── CV6 辅助：Resolve 既有五档零回归 ─────────────────────────

        [Test]
        public void CV6Aux_Resolve_ExistingCases_Unchanged()
        {
            // case 1 Numeric
            var num = ItemGrant.Resolve(new ItemDef { UseEffect = 1, UseValue = 1, UseNum = 5000 }, 1);
            Assert.AreEqual(GrantKind.Numeric, num.Kind);
            // case 2 Pattern
            var pat = ItemGrant.Resolve(new ItemDef { UseEffect = 2, UseValue = 100, UseNum = 2, UseLevel = 2 }, 2);
            Assert.AreEqual(GrantKind.Pattern, pat.Kind);
            // case 3 GiftSelect
            var sel = ItemGrant.Resolve(new ItemDef { UseEffect = 3, UseValue = 5001, Param = 1 }, 1);
            Assert.AreEqual(GrantKind.GiftSelect, sel.Kind);
            // case 4 GiftRandom
            var rnd = ItemGrant.Resolve(new ItemDef { UseEffect = 4, UseValue = 6001, Param = 1 }, 1);
            Assert.AreEqual(GrantKind.GiftRandom, rnd.Kind);
            // default None
            var none = ItemGrant.Resolve(new ItemDef { UseEffect = 0, Id = 30003 }, 3);
            Assert.AreEqual(GrantKind.None, none.Kind);
        }

        // ───────────────────────── ContainsEventUnlock helper 边界 ─────────────────────────

        [Test]
        public void ContainsEventUnlock_NullOrEmpty_ReturnsFalse()
        {
            Assert.IsFalse(ItemGrant.ContainsEventUnlock(null));
            Assert.IsFalse(ItemGrant.ContainsEventUnlock(Array.Empty<GrantPayload>()));
        }

        [Test]
        public void ContainsEventUnlock_MixedList_DetectsEvent()
        {
            var list = new System.Collections.Generic.List<GrantPayload>
            {
                new GrantPayload(GrantKind.Numeric, 1, 100, 0, 0),
                new GrantPayload(GrantKind.EventUnlock, 3, 1, 0, 0),
                new GrantPayload(GrantKind.None, 0, 1, 0, 0),
            };
            Assert.IsTrue(ItemGrant.ContainsEventUnlock(list));
        }

        [Test]
        public void ContainsEventUnlock_NoEvent_ReturnsFalse()
        {
            var list = new System.Collections.Generic.List<GrantPayload>
            {
                new GrantPayload(GrantKind.Numeric, 1, 100, 0, 0),
                new GrantPayload(GrantKind.None, 0, 1, 0, 0),
            };
            Assert.IsFalse(ItemGrant.ContainsEventUnlock(list));
        }
    }
}
