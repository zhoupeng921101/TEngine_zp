using NUnit.Framework;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Player;

namespace GameLogic.BlockBlast.Tests
{
    /// <summary>
    /// <see cref="PlayerDataLocalReset.ClearAll"/> EditMode 单测(清档·客户端段)。纯逻辑,
    /// 经 <see cref="InMemoryPersistenceProvider"/> 隔离本地存储。断言:
    /// - 全部玩法 player-data 键被删(尤其局内棋盘 blob 缓存,否则清档重登后旧盘面复活);
    /// - 不在删除集内的「外部键」(代表设备偏好 / 连接配置等走别处存储的白名单类比)不被误删。
    /// </summary>
    [TestFixture]
    public class PlayerDataLocalResetTests
    {
        private InMemoryPersistenceProvider _store;

        // 与 PlayerDataLocalReset 的删除集对齐(此处复述键值,与实现独立校验,
        // 任一侧改键名而未同步,本测试即红,作防漂移闸)。
        private static readonly string[] PlayerDataKeys =
        {
            "block_blast_merge_meta_v1",   // 元层(货币/进度/档案/经典最高分)
            "block_blast_save_v1",         // 经典棋盘
            "block_blast_dynamic_v1",      // 动态权重
            "block_blast_merge_ingame_v1", // 历史遗留:已退役的融合局内 blob(防御性清)
            "block_blast_cloud_version_v1",// 历史遗留:已退役的云存档本地 version(防御性清)
            "Mail.Inbox",                  // 邮件
            "Rank.Progress",               // 排行榜进度
        };

        [SetUp]
        public void SetUp()
        {
            _store = new InMemoryPersistenceProvider();
            Persistence.Provider = _store;
        }

        [Test]
        public void ClearAll_RemovesAllPlayerDataKeys()
        {
            foreach (var key in PlayerDataKeys)
                _store.Set(key, "non-empty-payload");

            PlayerDataLocalReset.ClearAll();

            foreach (var key in PlayerDataKeys)
                Assert.IsFalse(_store.TryGet(key, out _), $"清档后玩法键应被删除: {key}");
        }

        [Test]
        public void ClearAll_PreservesForeignKey()
        {
            // 模拟一个不属玩法 player-data 的外部键(设备偏好 / 连接配置走别的存储栈,这里以本 Provider 上的
            // 任意非删除集键代表「不该被 ClearAll 触及」)。
            const string foreignKey = "some_other_subsystem_key";
            _store.Set(foreignKey, "keep-me");
            foreach (var key in PlayerDataKeys)
                _store.Set(key, "payload");

            PlayerDataLocalReset.ClearAll();

            Assert.IsTrue(_store.TryGet(foreignKey, out var v), "非删除集的外部键不应被 ClearAll 误删");
            Assert.AreEqual("keep-me", v);
        }

        [Test]
        public void ClearAll_IsIdempotent_OnEmptyStore()
        {
            // 空存储(新装 / 已清过)再清一次不抛、无副作用。
            Assert.DoesNotThrow(() => PlayerDataLocalReset.ClearAll());
            Assert.AreEqual(0, _store.Count);
        }
    }
}
