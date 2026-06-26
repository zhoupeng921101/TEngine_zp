using GameLogic.Mail;
using GameLogic.Rank;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 清空本地玩法投影缓存(清档·客户端段)。服务端清档成功后调用:删掉所有「玩法 player-data」本地存储键,
    /// 使重连重登后客户端从已重置的服务端快照重建,而非从本地缓存复活旧数据(尤其棋盘 blob)。
    ///
    /// 删除集 = 经 <see cref="Persistence.Provider"/> 持有的、属于「玩家是谁 / 有什么 / 进度到哪」的投影:
    /// 元层档(货币/进度/档案/经典最高分)、融合棋盘、经典棋盘、动态权重、云存档本地 version、邮件、排行榜进度。
    /// 与 <see cref="CloudSaveCodec"/> 枚举的云存档键集对齐(云端可重建的本地切片),再加云 version 与邮件/排行榜。
    ///
    /// 不删(设备偏好 / 连接配置,见 data-authority.md 白名单):
    /// - 设置项(音量/语言/画质/操作)走 <c>TEngine.Utility.PlayerPrefs</c> 的 <c>Constant.Setting</c> 键,不经此 Provider。
    /// - 服务器连接配置(IP/端口/协议)由 <c>FantasyClient.FantasyNetworkConfig</c> 持有,不经此 Provider。
    /// 二者均不在删除集内,本类只触 <see cref="Persistence.Provider"/> 上的玩法键。
    /// </summary>
    public static class PlayerDataLocalReset
    {
        // 经典棋盘存档键。源:BlockGameState 私有 StorageKey / CloudSaveCodec.ClassicKey(私有 const,跨类不可引用,此处复述同值)。
        private const string ClassicBoardKey = "block_blast_save_v1";
        // 动态权重键。源:DynamicWeightDiff 私有 StorageKey / CloudSaveCodec.DynamicKey(同上)。
        private const string DynamicWeightKey = "block_blast_dynamic_v1";

        /// <summary>删掉全部玩法本地投影键。逐键独立 try,单键失败不阻断其余(同各持久化类 Clear 的容错口径)。</summary>
        public static void ClearAll()
        {
            // 元层 + 融合棋盘:各自已有 Clear() 入口(经 Provider.Remove 各自 StorageKey)。
            MergeMetaPersistence.Clear();
            MergeIngamePersistence.Clear();

            // 其余键无现成 Clear 入口,直接经 Provider 删。
            Remove(ClassicBoardKey);
            Remove(DynamicWeightKey);
            Remove(CloudSaveSync.VersionKey);
            Remove(MailPersistence.Key);
            Remove(RankPersistence.Key);
        }

        private static void Remove(string key)
        {
            try { Persistence.Provider.Remove(key); }
            catch { /* 单键删除失败不阻断其余(本地缓存删除非关键路径,服务端已是事实源) */ }
        }
    }
}
