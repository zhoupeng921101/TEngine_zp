using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using GameLogic;
using GameLogic.UI;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Player;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 个人信息窗表现层验收测试（设计 25 §9.1 H/W 组，EditMode 可测档）。
    ///
    /// 两类断言，都不依赖 Play / 真实 prefab：
    /// 1. GameContext 持有 PlayerInfo 往返（H2）——经测试注入入口 <see cref="GameContext.InitPlayerFromMeta"/>
    ///    灌 DTO / 无 DTO，断言字段。
    /// 2. 窗口逻辑（W3/W5）——bare new 一个 <see cref="PlayerInfoWindow"/>（不 Activate、不加载 prefab，
    ///    UI 组件字段全 null，窗口方法对 null 字段已做空守卫），反射调私有方法断言：
    ///    改名委托 <see cref="PlayerRenameService.TryRename"/>（观察 Player.Name/RenameCount 变化）、
    ///    占位点击不抛。窗口真实视觉绑定 / OnRefresh 文本刷新（W2/W4）属 V 组 Play 实测。
    ///
    /// 每例 TearDown Release GameContext，避免单例跨例污染（同 GameContextTests 口径）。
    /// </summary>
    [TestFixture]
    public class PlayerInfoWindowTests
    {
        private IPersistenceProvider _savedProvider;

        [SetUp]
        public void SetUp()
        {
            // 隔离持久化到内存，避免 SavePlayer 的 Load/SaveAsync 触真实编辑器 PlayerPrefs（不污染、可复现）。
            _savedProvider = Persistence.Provider;
            Persistence.Provider = new InMemoryPersistenceProvider();
        }

        [TearDown]
        public void TearDown()
        {
            if (GameContext.IsValid) GameContext.Instance.Release();
            Persistence.Provider = _savedProvider;
        }

        // ── 测试 DTO 构造（玩家字段已知值）──
        private static MergeMetaSave KnownDto() => new MergeMetaSave
        {
            version = MergeMetaPersistence.CurrentVersion,
            playerId = "test-id-abc",
            playerName = "测试名",
            playerRenameCount = 2,
            playerExp = 1234,
            curAvatarId = 2,
            curFrameId = 102,
            unlockedAvatarIds = new[] { 1, 2 },
            unlockedFrameIds = new[] { 101, 102 },
        };

        // ═══════════════════════ H2：GameContext 持有 PlayerInfo 往返 ═══════════════════════

        [Test]
        public void H2_InjectKnownDto_FieldsEqualDto()
        {
            GameContext.Instance.InitPlayerFromMeta(KnownDto(), new System.Random(1));
            var p = GameContext.Instance.Player;
            Assert.IsNotNull(p, "注入后 Player 应非空");
            Assert.AreEqual("test-id-abc", p.Id, "Id 应 == DTO");
            Assert.AreEqual("测试名", p.Name, "Name 应 == DTO");
            Assert.AreEqual(2, p.RenameCount, "RenameCount 应 == DTO");
            Assert.AreEqual(1234, p.Exp, "Exp 应 == DTO");
            Assert.AreEqual(2, p.CurrentAvatarId, "CurrentAvatarId 应 == DTO");
            Assert.AreEqual(102, p.CurrentFrameId, "CurrentFrameId 应 == DTO");
        }

        [Test]
        public void H2_NoDto_CreatesDefault()
        {
            GameContext.Instance.InitPlayerFromMeta(null, new System.Random(1));
            var p = GameContext.Instance.Player;
            Assert.IsNotNull(p, "无 DTO 也应建默认 Player");
            Assert.AreEqual(PlayerInfo.DefaultAvatarId, p.CurrentAvatarId, "无 DTO 头像应为默认 1");
            Assert.AreEqual(PlayerInfo.DefaultFrameId, p.CurrentFrameId, "无 DTO 框应为默认 101");
            Assert.AreEqual(0, p.RenameCount, "新建 RenameCount 应为 0");
            Assert.IsFalse(string.IsNullOrEmpty(p.Name), "新建应有系统生成名");
            Assert.IsFalse(string.IsNullOrEmpty(p.Id), "新建应有 id");
        }

        [Test]
        public void H2_Player_Persists_AcrossInstanceAccess()
        {
            GameContext.Instance.InitPlayerFromMeta(KnownDto(), new System.Random(1));
            var p1 = GameContext.Instance.Player;
            var p2 = GameContext.Instance.Player;
            Assert.AreSame(p1, p2, "多次访问应是同一 PlayerInfo 实例（持有，不每次新建）");
        }

        // ═══════════════════════ W3：改名贯通数据层（窗口委托 PlayerRenameService）═══════════════════════
        //
        // 验收 = 窗口的 OnRenameSubmit 确实委托 PlayerRenameService.TryRename、不自写改名逻辑。
        // 反射调 bare 窗口的私有 OnRenameSubmit，观察 GameContext.Player 的 Name/RenameCount 是否按
        // 数据层规则变化（数据层 R1–R5 规则本身已由 PlayerInfoTests 覆盖，此处只证「窗口走的是它」）。

        private static PlayerInfoWindow NewBareWindow() => new PlayerInfoWindow();

        private static void InvokeRenameSubmit(PlayerInfoWindow w, string newName)
        {
            var m = typeof(PlayerInfoWindow).GetMethod("OnRenameSubmit",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(m, "应有私有方法 OnRenameSubmit");
            m.Invoke(w, new object[] { newName });
        }

        [Test]
        public void W3_LegalName_RenamesAndCountsUp()
        {
            // 首次改名（RenameCount=0 → 免费），用无 DTO 默认 Player。
            GameContext.Instance.InitPlayerFromMeta(null, new System.Random(1));
            int before = GameContext.Instance.Player.RenameCount;

            InvokeRenameSubmit(NewBareWindow(), "新名字");

            Assert.AreEqual("新名字", GameContext.Instance.Player.Name, "合法名应改成功（窗口委托 TryRename）");
            Assert.AreEqual(before + 1, GameContext.Instance.Player.RenameCount, "成功后 RenameCount 应 +1");
        }

        [Test]
        public void W3_EmptyName_Rejected_NoChange()
        {
            GameContext.Instance.InitPlayerFromMeta(KnownDto(), new System.Random(1));
            string nameBefore = GameContext.Instance.Player.Name;
            int countBefore = GameContext.Instance.Player.RenameCount;

            InvokeRenameSubmit(NewBareWindow(), "   ");   // 全空白 = 空名

            Assert.AreEqual(nameBefore, GameContext.Instance.Player.Name, "空名应被拒、名字不变");
            Assert.AreEqual(countBefore, GameContext.Instance.Player.RenameCount, "被拒不应 +RenameCount");
        }

        [Test]
        public void W3_TooLongName_Rejected_NoChange()
        {
            GameContext.Instance.InitPlayerFromMeta(KnownDto(), new System.Random(1));
            string nameBefore = GameContext.Instance.Player.Name;

            string tooLong = new string('字', PlayerRenameService.MaxLen + 1);   // 超 16
            InvokeRenameSubmit(NewBareWindow(), tooLong);

            Assert.AreEqual(nameBefore, GameContext.Instance.Player.Name, "超长名应被拒、名字不变");
        }

        [Test]
        public void W3_EmptyWordList_DoesNotTriggerProfanity()
        {
            // 窗口注空词表（去变现/不阻塞 O6）→ 任何名都不该因屏蔽字被拒。
            GameContext.Instance.InitPlayerFromMeta(null, new System.Random(1));
            InvokeRenameSubmit(NewBareWindow(), "随便起个名");
            Assert.AreEqual("随便起个名", GameContext.Instance.Player.Name, "空词表下不应触发 Profanity 拒绝");
        }

        [Test]
        public void W3_NonFirstRename_DiamondSpendDefaultTrue_Succeeds()
        {
            // 窗口 trySpendDiamond 默认返 true（去变现 O8）→ 非首次改名（需计费）也应成功。
            var dto = KnownDto();   // RenameCount=2（非首次，需扣钻）
            GameContext.Instance.InitPlayerFromMeta(dto, new System.Random(1));
            int before = GameContext.Instance.Player.RenameCount;

            InvokeRenameSubmit(NewBareWindow(), "付费改名");

            Assert.AreEqual("付费改名", GameContext.Instance.Player.Name, "默认 trySpendDiamond=true 下非首次改名应成功");
            Assert.AreEqual(before + 1, GameContext.Instance.Player.RenameCount, "成功后 RenameCount 应 +1");
        }

        // ═══════════════════════ W5：占位项点击不抛 ═══════════════════════

        private static void InvokeNoArgPrivate(PlayerInfoWindow w, string method)
        {
            var m = typeof(PlayerInfoWindow).GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(m, $"应有私有方法 {method}");
            Assert.DoesNotThrow(() => m.Invoke(w, Array.Empty<object>()),
                $"{method} 占位点击不应抛异常");
        }

        [Test]
        public void W5_EditAvatarPlaceholder_DoesNotThrow()
            => InvokeNoArgPrivate(NewBareWindow(), "OnEditAvatar");

        [Test]
        public void W5_BirthdayPlaceholder_DoesNotThrow()
            => InvokeNoArgPrivate(NewBareWindow(), "OnBirthdayPlaceholder");

        [Test]
        public void W5_EnterRename_NoInputField_DoesNotThrow()
        {
            // bare 窗口 _inputName 为 null，OnEnterRename 应被空守卫挡住、不抛。
            GameContext.Instance.InitPlayerFromMeta(null, new System.Random(1));
            InvokeNoArgPrivate(NewBareWindow(), "OnEnterRename");
        }

        // ═══════════════════════ RefreshAvatar 占位取色：稳定 + 合法 ═══════════════════════

        [Test]
        public void StableColorFor_SameId_SameColor()
        {
            var m = typeof(PlayerInfoWindow).GetMethod("StableColorFor",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(m, "应有静态私有 StableColorFor");
            var c1 = (Color)m.Invoke(null, new object[] { 7 });
            var c2 = (Color)m.Invoke(null, new object[] { 7 });
            Assert.AreEqual(c1, c2, "同 id 占位色应稳定");
            var c3 = (Color)m.Invoke(null, new object[] { 8 });
            Assert.AreNotEqual(c1, c3, "不同 id 占位色应有区分度");
        }
    }
}
