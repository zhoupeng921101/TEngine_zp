using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
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
    /// 2. 窗口逻辑（W3/W5）——bare new 一个 <see cref="UIPlayerInfoPanel"/>（不 Activate、不加载 prefab，
    ///    UI 组件字段全 null，窗口方法对 null 字段已做空守卫），反射调私有方法断言：
    ///    改名走服务端权威 RPC（<see cref="IRenameGateway"/>）—— 观察 PlayerAttrService.Nickname/RenameCount/Diamond
    ///    按服务端响应对齐、本地不扣钻不写名；占位点击不抛。窗口真实视觉绑定 / OnRefresh 文本刷新（W2/W4）属 V 组 Play 实测。
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
            if (_bareGo != null) { UnityEngine.Object.DestroyImmediate(_bareGo); _bareGo = null; }
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

        // ═══════════════════════ W3：改名走服务端权威 RPC（窗口经 IRenameGateway）═══════════════════════
        //
        // 验收 = 窗口 OnRenameSubmit：① 本地先校验(空/超长/屏蔽字)明显非法直接拒、不发 RPC；
        // ② 合法则发 C2G_Rename（仅上报新昵称）；③ 据响应对齐 PlayerAttrService.Nickname/RenameCount/Diamond。
        // 客户端不本地扣钻、不本地写名。用桩 IRenameGateway 注各分支响应，观察 PlayerAttrService 视图与 RPC 调用参数。

        // UIPlayerInfoPanel 迁 MonoBehaviour 体系（UIPanelMono）后不能 new 构造：挂到临时 GameObject 上。
        // 不 Setup / 不加载 prefab，UI 组件字段（[SerializeField]）全 null，窗口方法对 null 字段已做空守卫。
        // 建出的 GameObject 在 TearDown 销毁。
        private GameObject _bareGo;

        private UIPlayerInfoPanel NewBareWindow()
        {
            if (_bareGo != null) UnityEngine.Object.DestroyImmediate(_bareGo);
            _bareGo = new GameObject("PlayerInfoWindow_BareTest");
            return _bareGo.AddComponent<UIPlayerInfoPanel>();
        }

        private static void InvokeRenameSubmit(UIPlayerInfoPanel w, string newName)
        {
            var m = typeof(UIPlayerInfoPanel).GetMethod("OnRenameSubmit",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(m, "应有私有方法 OnRenameSubmit");
            m.Invoke(w, new object[] { newName });
        }

        /// <summary>装配一个已 Ready 的 PlayerAttr(经快照)+ 指定改名桩,供 W3 用。</summary>
        private static FakeRenameGateway ArrangeRename(RenameRpcResult stub, long initialDiamond = 0L, string initialNickname = "旧昵称", int initialRenameCount = 0)
        {
            GameContext.Instance.InitPlayerFromMeta(null, new System.Random(1));
            // PlayerAttr 用默认 RpcGatewayProd 无妨(改名不再经它);但需先应用一次快照置 IsReady + 初始视图。
            var attr = GameContext.Instance.PlayerAttr;
            attr.ApplyProfile("acc", initialNickname, 1, 0L, initialRenameCount, 1);
            attr.ApplySnapshotFull(0, initialDiamond, 0, 0, 0, 0, 0);   // 末尾置 IsReady=true
            var gw = new FakeRenameGateway(stub);
            GameContext.Instance.InitRenameWith(gw);
            return gw;
        }

        [Test]
        public void W3_FirstRename_RpcSuccess_AppliesAuthoritative()
        {
            // 首次改名免费:服务端返 Success(RenameCount 1, Diamond 不变 500)。客户端用响应覆盖视图。
            var gw = ArrangeRename(RenameRpcResult.Ok("新昵称", renameCount: 1, diamond: 500),
                initialDiamond: 500, initialNickname: "旧昵称", initialRenameCount: 0);

            InvokeRenameSubmit(NewBareWindow(), "新昵称");

            Assert.AreEqual(1, gw.CallCount, "应发一次改名 RPC");
            Assert.AreEqual("新昵称", gw.LastNickname, "RPC 应上报新昵称");
            var attr = GameContext.Instance.PlayerAttr;
            Assert.AreEqual("新昵称", attr.Nickname, "成功后昵称按响应覆盖");
            Assert.AreEqual(1, attr.RenameCount, "成功后 RenameCount 按响应覆盖");
            Assert.AreEqual(500L, attr.Diamond, "免费首改钻石不变(按响应对齐)");
        }

        [Test]
        public void W3_SecondRename_RpcSuccess_ChargesViaServer()
        {
            // 二次改名:服务端扣 100 → 返 Success(RenameCount 3, Diamond 900)。客户端不本地扣钻,按响应覆盖。
            var gw = ArrangeRename(RenameRpcResult.Ok("付费昵称", renameCount: 3, diamond: 900),
                initialDiamond: 1000, initialNickname: "旧昵称", initialRenameCount: 2);

            InvokeRenameSubmit(NewBareWindow(), "付费昵称");

            Assert.AreEqual(1, gw.CallCount, "应发一次改名 RPC");
            var attr = GameContext.Instance.PlayerAttr;
            Assert.AreEqual("付费昵称", attr.Nickname, "成功后昵称覆盖");
            Assert.AreEqual(3, attr.RenameCount, "成功后 RenameCount 覆盖");
            Assert.AreEqual(900L, attr.Diamond, "钻石余额按服务端响应覆盖(服务端扣费,客户端不自扣)");
        }

        [Test]
        public void W3_RpcNotEnoughDiamond_Rejected_AlignsAuthoritative()
        {
            // 服务端拒(钻不足)→ 昵称不改,但响应回带当前权威值(昵称/次数/余额)对齐视图。
            var gw = ArrangeRename(RenameRpcResult.Rejected(RenameOutcome.NotEnoughDiamond, "旧昵称", renameCount: 2, diamond: 50),
                initialDiamond: 50, initialNickname: "旧昵称", initialRenameCount: 2);

            InvokeRenameSubmit(NewBareWindow(), "改不动");

            Assert.AreEqual(1, gw.CallCount, "应发一次 RPC");
            var attr = GameContext.Instance.PlayerAttr;
            Assert.AreEqual("旧昵称", attr.Nickname, "钻不足昵称不变(对齐服务端当前值)");
            Assert.AreEqual(2, attr.RenameCount, "钻不足次数不变");
            Assert.AreEqual(50L, attr.Diamond, "钻不足响应仍对齐余额");
        }

        [Test]
        public void W3_RpcServiceUnavailable_NoViewChange()
        {
            // 服务不可用(未收响应)→ 昵称/次数/余额均不动(值不可信,不冒进)。
            var gw = ArrangeRename(RenameRpcResult.Rejected(RenameOutcome.ServiceUnavailable),
                initialDiamond: 300, initialNickname: "旧昵称", initialRenameCount: 1);

            InvokeRenameSubmit(NewBareWindow(), "想改名");

            Assert.AreEqual(1, gw.CallCount, "应发一次 RPC");
            var attr = GameContext.Instance.PlayerAttr;
            Assert.AreEqual("旧昵称", attr.Nickname, "服务不可用昵称不动");
            Assert.AreEqual(1, attr.RenameCount, "服务不可用次数不动");
            Assert.AreEqual(300L, attr.Diamond, "服务不可用余额不动(值不可信)");
        }

        [Test]
        public void W3_LocalInvalidName_DoesNotCallRpc()
        {
            // 本地校验(空/超长)明显非法 → 直接拒、不发 RPC(减一次无谓往返)。
            var gw = ArrangeRename(RenameRpcResult.Ok("x", 1, 0), initialNickname: "旧昵称");

            InvokeRenameSubmit(NewBareWindow(), "   ");   // 全空白 = 空名

            Assert.AreEqual(0, gw.CallCount, "本地非法名不应发 RPC");
            Assert.AreEqual("旧昵称", GameContext.Instance.PlayerAttr.Nickname, "本地拒后昵称不变");
        }

        [Test]
        public void W3_TooLongName_DoesNotCallRpc()
        {
            var gw = ArrangeRename(RenameRpcResult.Ok("x", 1, 0), initialNickname: "旧昵称");
            string tooLong = new string('字', PlayerRenameService.MaxLen + 1);

            InvokeRenameSubmit(NewBareWindow(), tooLong);

            Assert.AreEqual(0, gw.CallCount, "本地超长名不应发 RPC");
            Assert.AreEqual("旧昵称", GameContext.Instance.PlayerAttr.Nickname, "本地拒后昵称不变");
        }

        /// <summary>
        /// 测试用桩 <see cref="IRenameGateway"/>:返预设响应 + 记录调用参数。
        /// 桩 await CompletedTask 即同步完成 UniTask(memory「跨框架通用异步与网络库异步」)。
        /// </summary>
        private sealed class FakeRenameGateway : IRenameGateway
        {
            private readonly RenameRpcResult _result;
            public int CallCount { get; private set; }
            public string LastNickname { get; private set; }

            public FakeRenameGateway(RenameRpcResult result) { _result = result; }

            public async UniTask<RenameRpcResult> SendRenameAsync(string newNickname)
            {
                CallCount++;
                LastNickname = newNickname;
                await UniTask.CompletedTask;
                return _result;
            }
        }

        // ═══════════════════════ W5：占位项点击不抛 ═══════════════════════

        private static void InvokeNoArgPrivate(UIPlayerInfoPanel w, string method)
        {
            var m = typeof(UIPlayerInfoPanel).GetMethod(method,
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
            var m = typeof(UIPlayerInfoPanel).GetMethod("StableColorFor",
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
