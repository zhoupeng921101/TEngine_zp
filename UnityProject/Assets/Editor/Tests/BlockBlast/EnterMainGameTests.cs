using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Player;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 进主游戏编排 <see cref="EnterMainGameSync"/> EditMode 单测(全栈协议改动·客户端段)。
    /// 纯逻辑、不连网 — 用 <see cref="InMemoryPersistenceProvider"/> 隔离本地存储、桩 <see cref="IEnterMainGameGateway"/> 注入响应,断言:
    /// 进主游戏响应把云存档冲突解决落地 + 置 Ready;每次进入复位 Ready 再重对齐(决策②);失败(ServiceUnavailable)保留本地仍置 Ready 不卡死。
    /// 同步驱动 UniTask 用 GetAwaiter().GetResult()(桩 await CompletedTask 同步完成,沿 CloudSaveTests 范式)。
    /// </summary>
    [TestFixture]
    public class EnterMainGameTests
    {
        private InMemoryPersistenceProvider _store;

        [SetUp]
        public void SetUp()
        {
            _store = new InMemoryPersistenceProvider();
            Persistence.Provider = _store;
        }

        private const string ClassicKey = "block_blast_save_v1";

        private static string ReadKey(InMemoryPersistenceProvider s, string key)
            => s.TryGet(key, out var v) ? v : null;

        private static byte[] BlobWithClassicScore(int score)
        {
            var payload = new CloudSavePayload { payloadVersion = 1, classicJson = "{\"score\":" + score + "}", metaJson = "" };
            return Encoding.UTF8.GetBytes(UnityEngine.JsonUtility.ToJson(payload));
        }

        // ── 桩 gateway:可预设进主游戏响应(可按调用次序返不同响应),记录调用次数 ──
        private sealed class StubEnterGateway : IEnterMainGameGateway
        {
            public readonly List<EnterMainGameResult> Queue = new();
            public EnterMainGameResult Fallback = EnterMainGameResult.Unavailable();
            public int Calls;

            public async UniTask<EnterMainGameResult> EnterAsync()
            {
                int idx = Calls;
                Calls++;
                await UniTask.CompletedTask;
                return idx < Queue.Count ? Queue[idx] : Fallback;
            }
        }

        private static EnterMainGameSync NewSync(StubEnterGateway gw, out CloudSaveSync cloud)
        {
            var orderSync = new OrderSync(new StubOrderGateway());
            cloud = new CloudSaveSync(new StubCloudGateway(), () => 0);
            return new EnterMainGameSync(gw, orderSync, cloud);
        }

        // 进主游戏路径不经这两个 gateway 发请求(下载/订单已同包回带),但 OrderSync/CloudSaveSync 构造需要接缝,给惰性桩占位。
        private sealed class StubOrderGateway : IOrderRpcGateway
        {
            public async UniTask<OrderDeliverResult> DeliverAsync(int slot)
            { await UniTask.CompletedTask; return new OrderDeliverResult(DeliverCode.ServiceUnavailable, null); }
        }
        private sealed class StubCloudGateway : ICloudSaveGateway
        {
            public async UniTask<CloudUploadResult> UploadAsync(long version, byte[] blob)
            { await UniTask.CompletedTask; return new CloudUploadResult(CloudUploadCode.ServiceUnavailable, 0, null); }
            public async UniTask<CloudDownloadResult> DownloadAsync()
            { await UniTask.CompletedTask; return new CloudDownloadResult(CloudDownloadCode.ServiceUnavailable, 0, null); }
        }

        // ── E1:进主游戏响应 Success 且 ServerVersion>本地 → 应用云存档 blob + 置 Ready ──
        [Test]
        public void E1_Enter_SuccessNewerVersion_AppliesCloudAndReady()
        {
            var gw = new StubEnterGateway();
            gw.Queue.Add(new EnterMainGameResult(null, CloudDownloadCode.Success, 5, BlobWithClassicScore(77)));

            var sync = NewSync(gw, out var cloud);
            sync.EnterAsync().GetAwaiter().GetResult();

            Assert.AreEqual(1, gw.Calls);
            Assert.IsTrue(cloud.IsReady, "响应应用后置 Ready");
            Assert.AreEqual(5, cloud.LocalVersion, "本地 version 推进到 ServerVersion");
            Assert.AreEqual("{\"score\":77}", ReadKey(_store, ClassicKey), "应用了进主游戏回带的云存档 blob");
        }

        // ── E2:失败(ServiceUnavailable)→ 保留本地 + 仍置 Ready(不卡死进游戏)──
        [Test]
        public void E2_Enter_Unavailable_KeepsLocalButReady()
        {
            _store.Set(CloudSaveSync.VersionKey, "10");
            _store.Set(ClassicKey, "{\"score\":1}");

            var gw = new StubEnterGateway { Fallback = EnterMainGameResult.Unavailable() };
            var sync = NewSync(gw, out var cloud);
            sync.EnterAsync().GetAwaiter().GetResult();

            Assert.IsTrue(cloud.IsReady, "失败也置 Ready,按本地兜底进游戏");
            Assert.AreEqual(10, cloud.LocalVersion, "本地 version 不变");
            Assert.AreEqual("{\"score\":1}", ReadKey(_store, ClassicKey), "本地未被覆盖");
        }

        // ── E3:决策② — 每次进入都重新请求,复位 Ready 后取最新响应再对齐 ──
        [Test]
        public void E3_Enter_EachEntry_ReRequestsAndRealigns()
        {
            var gw = new StubEnterGateway();
            // 第一次进入:ServerVersion=3,classic=100。
            gw.Queue.Add(new EnterMainGameResult(null, CloudDownloadCode.Success, 3, BlobWithClassicScore(100)));
            // 第二次进入:ServerVersion=8,classic=200(更新,模拟另一端写过)。
            gw.Queue.Add(new EnterMainGameResult(null, CloudDownloadCode.Success, 8, BlobWithClassicScore(200)));

            var sync = NewSync(gw, out var cloud);

            sync.EnterAsync().GetAwaiter().GetResult();
            Assert.AreEqual(3, cloud.LocalVersion);
            Assert.AreEqual("{\"score\":100}", ReadKey(_store, ClassicKey));

            // 第二次进入:必须重新发请求(Calls=2),且复位 Ready 后取到 v8 覆盖。
            sync.EnterAsync().GetAwaiter().GetResult();
            Assert.AreEqual(2, gw.Calls, "每次进入都重发请求,不复用上次缓存(决策②)");
            Assert.IsTrue(cloud.IsReady);
            Assert.AreEqual(8, cloud.LocalVersion, "重对齐到最新 ServerVersion");
            Assert.AreEqual("{\"score\":200}", ReadKey(_store, ClassicKey), "应用了第二次进入的最新 blob");
        }

        // ── E4:进入前 Ready 被复位(响应落地前 WhenReady 重新挂起,落地后完成)──
        [Test]
        public void E4_Enter_ResetsReadyBeforeApply()
        {
            var gw = new StubEnterGateway();
            gw.Queue.Add(new EnterMainGameResult(null, CloudDownloadCode.NoSnapshot, 0, null));
            var sync = NewSync(gw, out var cloud);

            // 先跑一次置 Ready。
            sync.EnterAsync().GetAwaiter().GetResult();
            Assert.IsTrue(cloud.IsReady);

            // 手动复位验证语义:复位后未就绪,WhenReady 挂起。
            cloud.ResetReadyForReentry();
            Assert.IsFalse(cloud.IsReady, "复位后不就绪");
            Assert.IsFalse(cloud.WhenReady().Status.IsCompleted(), "复位后 WhenReady 应重新挂起");

            // 再进入应用响应后重新置位。
            gw.Queue.Add(new EnterMainGameResult(null, CloudDownloadCode.NoSnapshot, 0, null));
            sync.EnterAsync().GetAwaiter().GetResult();
            Assert.IsTrue(cloud.IsReady, "再进入响应落地后重新置 Ready");
        }
    }
}
