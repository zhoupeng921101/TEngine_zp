using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Player;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// 云存档同步 <see cref="CloudSaveSync"/> + 编解码 <see cref="CloudSaveCodec"/> EditMode 单测(P3 全栈迁移·客户端段)。
    /// 纯逻辑、不连网 — 用 <see cref="InMemoryPersistenceProvider"/> 隔离本地存储、桩 <see cref="ICloudSaveGateway"/> 注入响应,断言:
    /// blob 排除货币+playerId、下载只覆盖非货币非身份字段、version 冲突高者胜、Stale 让位、上传节流、未 Ready 不传。
    /// 同步驱动 UniTask 用 GetAwaiter().GetResult()(桩 await CompletedTask 同步完成,沿 MetaCurrencySyncTests 范式)。
    /// </summary>
    [TestFixture]
    public class CloudSaveTests
    {
        private InMemoryPersistenceProvider _store;

        [SetUp]
        public void SetUp()
        {
            _store = new InMemoryPersistenceProvider();
            Persistence.Provider = _store;
        }

        // ── 桩 gateway:可预设上传/下载响应,记录调用 ──
        private sealed class StubGateway : ICloudSaveGateway
        {
            public CloudUploadResult UploadResult = new CloudUploadResult(CloudUploadCode.Accepted, 0, null);
            public CloudDownloadResult DownloadResult = new CloudDownloadResult(CloudDownloadCode.NoSnapshot, 0, null);
            public readonly List<(long version, byte[] blob)> Uploads = new();
            public int DownloadCalls;

            public async UniTask<CloudUploadResult> UploadAsync(long version, byte[] blob)
            {
                Uploads.Add((version, blob));
                await UniTask.CompletedTask;
                // Accepted 默认把 ServerVersion 设为本次 version(模拟服务端覆盖)。
                if (UploadResult.Code == CloudUploadCode.Accepted && UploadResult.ServerVersion == 0)
                    return new CloudUploadResult(CloudUploadCode.Accepted, version, null);
                return UploadResult;
            }

            public async UniTask<CloudDownloadResult> DownloadAsync()
            {
                DownloadCalls++;
                await UniTask.CompletedTask;
                return DownloadResult;
            }
        }

        // 手动毫秒时钟(节流断言用,绕开真实时钟)。
        private sealed class FakeClock { public long Now; public long Read() => Now; }

        private const string MetaKey = "block_blast_merge_meta_v1";
        private const string IngameKey = "block_blast_merge_ingame_v1";
        private const string ClassicKey = "block_blast_save_v1";
        private const string DynamicKey = "block_blast_dynamic_v1";

        private static string Read(InMemoryPersistenceProvider s, string key)
            => s.TryGet(key, out var v) ? v : null;

        // 构造一份含货币+playerId+非货币字段的本地元档 JSON。
        private static MergeMetaSave FullMeta()
        {
            return new MergeMetaSave
            {
                version = 1,
                soul = 111, piety = 222, exp = 333, energy = 44, lastEnergyRegenTime = 9999,
                playerId = "LOCAL_PID",
                highScore = 5000, blindBoxCount = 7, goddessLevel = 3, unlockedChapter = 2,
                playerName = "Hero", curAvatarId = 12,
            };
        }

        // ── C1:BuildBlob 排除货币 + playerId(blob 内元层这些字段被清零/清空)──
        [Test]
        public void C1_BuildBlob_ExcludesCurrencyAndIdentity()
        {
            _store.Set(MetaKey, MergeMetaPersistence.Serialize(FullMeta()));

            byte[] blob = CloudSaveCodec.BuildBlob();
            Assert.IsNotNull(blob);

            // 解出 payload → metaJson → DTO,断言货币/身份已清,非货币字段保留。
            var payload = UnityEngine.JsonUtility.FromJson<CloudSavePayload>(Encoding.UTF8.GetString(blob));
            var meta = MergeMetaPersistence.Deserialize(payload.metaJson);
            Assert.IsNotNull(meta);

            Assert.AreEqual(0, meta.soul, "soul 必须清零");
            Assert.AreEqual(0, meta.piety, "piety 必须清零");
            Assert.AreEqual(0, meta.exp, "exp 必须清零");
            Assert.AreEqual(0, meta.energy, "energy 必须清零");
            Assert.AreEqual(0, meta.lastEnergyRegenTime, "lastEnergyRegenTime 必须清零");
            Assert.IsTrue(string.IsNullOrEmpty(meta.playerId), "playerId 必须清空");

            // 非货币非身份字段保留。
            Assert.AreEqual(5000, meta.highScore);
            Assert.AreEqual(7, meta.blindBoxCount);
            Assert.AreEqual(3, meta.goddessLevel);
            Assert.AreEqual("Hero", meta.playerName);
            Assert.AreEqual(12, meta.curAvatarId);
        }

        // ── C2:ApplyBlob 保留本地货币 + playerId,只覆盖非货币字段 ──
        [Test]
        public void C2_ApplyBlob_PreservesLocalCurrencyAndIdentity()
        {
            // 本地底档:P2/P0 权威值。
            var local = new MergeMetaSave
            {
                version = 1, soul = 900, piety = 800, exp = 700, energy = 60, lastEnergyRegenTime = 12345,
                playerId = "AUTH_PID", highScore = 100, blindBoxCount = 1,
            };
            _store.Set(MetaKey, MergeMetaPersistence.Serialize(local));

            // 来档(blob):非货币字段更新(已 strip 货币/身份)。
            var incoming = FullMeta(); // highScore=5000 blindBox=7
            var payload = new CloudSavePayload
            {
                payloadVersion = 1,
                metaJson = MergeMetaPersistence.Serialize(StripForBlob(incoming)),
            };
            byte[] blob = Encoding.UTF8.GetBytes(UnityEngine.JsonUtility.ToJson(payload));

            Assert.IsTrue(CloudSaveCodec.ApplyBlob(blob));

            var after = MergeMetaPersistence.Deserialize(Read(_store, MetaKey));
            // 货币 + playerId 保留本地权威值(不被 blob 冲掉)。
            Assert.AreEqual(900, after.soul);
            Assert.AreEqual(800, after.piety);
            Assert.AreEqual(700, after.exp);
            Assert.AreEqual(60, after.energy);
            Assert.AreEqual(12345, after.lastEnergyRegenTime);
            Assert.AreEqual("AUTH_PID", after.playerId);
            // 非货币字段被 blob 覆盖。
            Assert.AreEqual(5000, after.highScore);
            Assert.AreEqual(7, after.blindBoxCount);
        }

        private static MergeMetaSave StripForBlob(MergeMetaSave m)
        {
            m.soul = 0; m.piety = 0; m.exp = 0; m.energy = 0; m.lastEnergyRegenTime = 0; m.playerId = null;
            return m;
        }

        // ── C3:其余切片原样回写本地键 ──
        [Test]
        public void C3_ApplyBlob_WritesIngameClassicDynamicKeys()
        {
            var payload = new CloudSavePayload
            {
                payloadVersion = 1,
                mergeIngameJson = "{\"version\":1}",
                classicJson = "{\"score\":42}",
                dynamicJson = "{\"dynamicWeight\":5}",
                metaJson = "",
            };
            byte[] blob = Encoding.UTF8.GetBytes(UnityEngine.JsonUtility.ToJson(payload));
            Assert.IsTrue(CloudSaveCodec.ApplyBlob(blob));

            Assert.AreEqual("{\"version\":1}", Read(_store, IngameKey));
            Assert.AreEqual("{\"score\":42}", Read(_store, ClassicKey));
            Assert.AreEqual("{\"dynamicWeight\":5}", Read(_store, DynamicKey));
        }

        // ── C4:下载 ServerVersion > 本地 → 应用 blob + 推进 version ──
        [Test]
        public void C4_Download_NewerServerVersion_Applies()
        {
            var gw = new StubGateway();
            var serverPayload = new CloudSavePayload { payloadVersion = 1, classicJson = "{\"score\":77}", metaJson = "" };
            byte[] serverBlob = Encoding.UTF8.GetBytes(UnityEngine.JsonUtility.ToJson(serverPayload));
            gw.DownloadResult = new CloudDownloadResult(CloudDownloadCode.Success, 5, serverBlob);

            var sync = new CloudSaveSync(gw, () => 0);
            sync.DownloadAndResolve().GetAwaiter().GetResult();

            Assert.IsTrue(sync.IsReady);
            Assert.AreEqual(5, sync.LocalVersion, "本地 version 推进到 ServerVersion");
            Assert.AreEqual("{\"score\":77}", Read(_store, ClassicKey), "应用了服务端 blob");
        }

        // ── C5:下载 ServerVersion <= 本地 → 保留本地,不覆盖 ──
        [Test]
        public void C5_Download_OlderServerVersion_KeepsLocal()
        {
            _store.Set(CloudSaveSync.VersionKey, "10");
            _store.Set(ClassicKey, "{\"score\":1}");

            var gw = new StubGateway();
            var serverPayload = new CloudSavePayload { payloadVersion = 1, classicJson = "{\"score\":999}", metaJson = "" };
            byte[] serverBlob = Encoding.UTF8.GetBytes(UnityEngine.JsonUtility.ToJson(serverPayload));
            gw.DownloadResult = new CloudDownloadResult(CloudDownloadCode.Success, 3, serverBlob); // 3 <= 10

            var sync = new CloudSaveSync(gw, () => 0);
            sync.DownloadAndResolve().GetAwaiter().GetResult();

            Assert.AreEqual(10, sync.LocalVersion, "本地 version 不变");
            Assert.AreEqual("{\"score\":1}", Read(_store, ClassicKey), "本地未被覆盖");
        }

        // ── C6:NoSnapshot / ServiceUnavailable → 用本地不崩,IsReady 仍置位 ──
        [Test]
        public void C6_Download_NoSnapshotOrUnavailable_Ready()
        {
            var gw = new StubGateway { DownloadResult = new CloudDownloadResult(CloudDownloadCode.ServiceUnavailable, 0, null) };
            var sync = new CloudSaveSync(gw, () => 0);
            sync.DownloadAndResolve().GetAwaiter().GetResult();
            Assert.IsTrue(sync.IsReady, "断服也置 Ready,用本地");
        }

        // ── C7:未 Ready(未下载)→ 不上传 ──
        [Test]
        public void C7_Upload_NotReady_NoUpload()
        {
            var gw = new StubGateway();
            var sync = new CloudSaveSync(gw, () => 0);
            sync.TryUploadThrottled().GetAwaiter().GetResult();
            Assert.AreEqual(0, gw.Uploads.Count, "未 Ready 不上传");
        }

        // ── C8:上传 version 单调 +1,Accepted 推进本地 version ──
        [Test]
        public void C8_Upload_Accepted_VersionMonotonic()
        {
            _store.Set(MetaKey, MergeMetaPersistence.Serialize(FullMeta()));
            var gw = new StubGateway { DownloadResult = new CloudDownloadResult(CloudDownloadCode.NoSnapshot, 0, null) };
            var clock = new FakeClock();
            var sync = new CloudSaveSync(gw, clock.Read);

            sync.DownloadAndResolve().GetAwaiter().GetResult(); // Ready,本地 version=0
            sync.UploadNow().GetAwaiter().GetResult();

            Assert.AreEqual(1, gw.Uploads.Count);
            Assert.AreEqual(1, gw.Uploads[0].version, "首传 version = 本地0 + 1");
            Assert.AreEqual(1, sync.LocalVersion, "Accepted 推进本地 version");
        }

        // ── C9:Stale → 采用回带 ServerBlob + 本地 version = ServerVersion(让位高 version)──
        [Test]
        public void C9_Upload_Stale_AdoptsServerBlobAndVersion()
        {
            _store.Set(MetaKey, MergeMetaPersistence.Serialize(FullMeta()));
            var gw = new StubGateway { DownloadResult = new CloudDownloadResult(CloudDownloadCode.NoSnapshot, 0, null) };
            var serverPayload = new CloudSavePayload { payloadVersion = 1, classicJson = "{\"score\":555}", metaJson = "" };
            byte[] serverBlob = Encoding.UTF8.GetBytes(UnityEngine.JsonUtility.ToJson(serverPayload));
            gw.UploadResult = new CloudUploadResult(CloudUploadCode.Stale, 9, serverBlob);

            var sync = new CloudSaveSync(gw, () => 0);
            sync.DownloadAndResolve().GetAwaiter().GetResult();
            sync.UploadNow().GetAwaiter().GetResult();

            Assert.AreEqual(9, sync.LocalVersion, "Stale 让位到服务端高 version");
            Assert.AreEqual("{\"score\":555}", Read(_store, ClassicKey), "采用了回带 ServerBlob");
        }

        // ── C10:节流 — 窗口内第二次 TryUploadThrottled 不立即发 ──
        [Test]
        public void C10_Upload_Throttled_WithinWindow_Skips()
        {
            _store.Set(MetaKey, MergeMetaPersistence.Serialize(FullMeta()));
            var gw = new StubGateway { DownloadResult = new CloudDownloadResult(CloudDownloadCode.NoSnapshot, 0, null) };
            var clock = new FakeClock { Now = 100_000 };
            var sync = new CloudSaveSync(gw, clock.Read);

            sync.DownloadAndResolve().GetAwaiter().GetResult();
            sync.TryUploadThrottled().GetAwaiter().GetResult(); // 首发
            Assert.AreEqual(1, gw.Uploads.Count);

            clock.Now += 1000; // 窗口内(< 10s)
            sync.TryUploadThrottled().GetAwaiter().GetResult();
            Assert.AreEqual(1, gw.Uploads.Count, "节流窗口内不重复发");

            clock.Now += CloudSaveSync.UploadThrottleMs; // 越过窗口
            sync.TryUploadThrottled().GetAwaiter().GetResult();
            Assert.AreEqual(2, gw.Uploads.Count, "窗口外放行");
        }
    }
}
