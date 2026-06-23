using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 融合主玩法「局内态」存档的序列化 / 落盘 / 读盘（2026-06-22 决定：真无尽局内态续存）。
    ///
    /// 与 <see cref="MergeMetaPersistence"/> 同口径分两层：
    /// - 同步纯逻辑（可单测、不碰磁盘）：<see cref="Serialize"/> / <see cref="Deserialize"/>。
    /// - 异步 IO 外壳（满足「禁同步 IO」红线）：<see cref="SaveAsync"/>；读路径 <see cref="Load"/> 走同步
    ///   （PlayerPrefs 非阻塞内存读，与 <c>BlockGameState.Load</c> / <c>MergeMetaPersistence.Load</c> 同口径），
    ///   使 <c>ResetForMergeOrder</c> 可保持同步、不被迫改 async。
    ///
    /// 独立存储键，与元层键 / Classic 局内键互不干扰：换会话只读本键，缺则等价首次（走 Reset 缺省）。
    /// </summary>
    public static class MergeIngamePersistence
    {
        /// <summary>存档结构当前版本。破坏性结构变更才升；未来档（version &gt; 当前）一律视作无档走缺省。</summary>
        public const int CurrentVersion = 1;

        /// <summary>持久化存储键。</summary>
        public const string StorageKey = "block_blast_merge_ingame_v1";

        /// <summary>DTO → JSON。null 入参视作空档返回空串。</summary>
        public static string Serialize(MergeIngameSave dto)
        {
            if (dto == null) return string.Empty;
            return UnityEngine.JsonUtility.ToJson(dto);
        }

        /// <summary>JSON → DTO。空 / null / 非法 JSON → null（视作无存档，调用方走缺省）。</summary>
        public static MergeIngameSave Deserialize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            try { return UnityEngine.JsonUtility.FromJson<MergeIngameSave>(raw); }
            catch { return null; }
        }

        /// <summary>
        /// 落盘（序列化 + 写存储）。经 <see cref="Persistence.Provider"/> 写入，失败吞掉不抛、不阻断玩法。
        /// 返回 UniTask 满足异步红线。
        /// </summary>
        public static UniTask SaveAsync(MergeIngameSave dto)
        {
            try
            {
                string json = Serialize(dto);
                if (!string.IsNullOrEmpty(json)) Persistence.Provider.Set(StorageKey, json);
            }
            catch { /* ignore：落盘失败不阻断玩法 */ }
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 同步读盘（读存储 + 反序列化 + 版本校验），返回可用 DTO 或 null（无档 / 加载失败 / 未来档 → 缺省重置）。
        /// </summary>
        public static MergeIngameSave Load()
        {
            try
            {
                if (!Persistence.Provider.TryGet(StorageKey, out string raw) || string.IsNullOrEmpty(raw))
                    return null;
                var dto = Deserialize(raw);
                if (dto == null) return null;
                if (dto.version > CurrentVersion) return null; // 未来档：不冒险用错位数据
                return dto;
            }
            catch { return null; }
        }

        /// <summary>清档（重开新游戏 / 调试用）。</summary>
        public static void Clear()
        {
            try { Persistence.Provider.Remove(StorageKey); }
            catch { /* ignore */ }
        }
    }
}
