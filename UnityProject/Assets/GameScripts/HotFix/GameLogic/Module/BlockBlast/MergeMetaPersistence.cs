using System;
using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 跨会话磁盘存档的序列化 / 落盘 / 读盘 / 版本迁移 / 跨天重置（设计 14 §3.2–§3.6）。
    ///
    /// 分两层（设计 14 §3.3）：
    /// - 同步纯逻辑（不碰磁盘、可单测）：<see cref="Serialize"/> / <see cref="Deserialize"/> / <see cref="Migrate"/> /
    ///   <see cref="ApplyDailyReset"/>。断言全落在 string / DTO 上，不依赖真实文件 / 不依赖 UniTask 运行。
    /// - 异步 IO 外壳（生产侧落盘，满足 CLAUDE.md「禁同步 IO」红线）：<see cref="SaveAsync"/> / <see cref="LoadAsync"/>。
    ///   内部经 <see cref="Persistence.Provider"/>（生产 PlayerPrefs / 测试 InMemory）读写，沿用工程既有持久化接缝，不引入新存储栈。
    ///
    /// 存储键带版本后缀 _v1（同 block_blast_save_v1 / block_blast_dynamic_v1 口径）：与 DTO 内 <see cref="MergeMetaSave.version"/>
    /// 是两个层级——小改字段升 version 走迁移，破坏性大改才换 _v2 键。
    /// </summary>
    public static class MergeMetaPersistence
    {
        /// <summary>存档结构当前版本。新增字段不必升版（JsonUtility 自动给缺省 + ImportMeta 逐字段保底）；语义破坏性变更才升。</summary>
        public const int CurrentVersion = 1;

        /// <summary>持久化存储键（PlayerPrefs 键 / 沙盒文件名前缀）。</summary>
        public const string StorageKey = "block_blast_merge_meta_v1";

        /// <summary>本地日期字符串格式（§3.6）。</summary>
        public const string DateFormat = "yyyy-MM-dd";

        // ── 同步纯逻辑层（可单测，不碰磁盘）────────────────────────

        /// <summary>DTO → JSON 字符串。null 入参视作空档返回空串。</summary>
        public static string Serialize(MergeMetaSave dto)
        {
            if (dto == null) return string.Empty;
            return UnityEngine.JsonUtility.ToJson(dto);
        }

        /// <summary>
        /// JSON 字符串 → DTO。空 / null / 非法 JSON → 返回 null（视作无存档，调用方走缺省）。
        /// 不在此处补缺字段或迁移——那是 <see cref="Migrate"/> / ImportMeta 的职责，保持本方法单一。
        /// </summary>
        public static MergeMetaSave Deserialize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            try
            {
                return UnityEngine.JsonUtility.FromJson<MergeMetaSave>(raw);
            }
            catch
            {
                return null; // 非 JSON / 截断 → 当无存档
            }
        }

        /// <summary>
        /// 按版本迁移 DTO（§3.5），返回是否「可用」：
        /// - version == CurrentVersion：直接可用（true）。
        /// - version &lt; CurrentVersion（旧档）：逐字段补缺由 ImportMeta 承接，此处仅把 version 归一为 CurrentVersion，返回 true。
        /// - version &gt; CurrentVersion（未来档，降级运行）：无法理解新字段，返回 false（调用方走缺省重置，等价首次游玩）。
        /// 入参 null 返回 false。本方法可能就地修改 dto.version。
        /// </summary>
        public static bool Migrate(MergeMetaSave dto)
        {
            if (dto == null) return false;
            if (dto.version > CurrentVersion) return false;   // 未来档：不冒险用错位数据
            dto.version = CurrentVersion;                     // 旧档 / 当前档：归一版本（字段保底交 ImportMeta）
            return true;
        }

        /// <summary>
        /// 跨天重置祈愿（§3.6，纯逻辑，便于单测注入 today）：
        /// - 缺日期字段（旧档 / 篡改）→ 视作需重置：WishUsedToday=0、日期设为 today（宽松：宁可多给一次每日额度）。
        /// - lastWishResetDate != today → 跨天：WishUsedToday=0、日期更新为 today。
        /// - lastWishResetDate == today → 同日：沿用 dto.wishUsedToday，仅确保日期为 today。
        /// 就地修改 dto；入参 null 直接返回。today 为本地日期 yyyy-MM-dd。
        /// </summary>
        public static void ApplyDailyReset(MergeMetaSave dto, string today)
        {
            if (dto == null) return;
            if (string.IsNullOrEmpty(dto.lastWishResetDate) || dto.lastWishResetDate != today)
            {
                dto.wishUsedToday = 0;
            }
            dto.lastWishResetDate = today;
        }

        /// <summary>当前本地日期字符串（生产用；测试用构造日期绕开真实时钟，见 §3.6）。</summary>
        public static string Today() => DateTime.Now.ToString(DateFormat);

        // ── 异步 IO 外壳（生产侧落盘，满足红线）────────────────────

        /// <summary>
        /// 落盘（序列化 + 写存储）。version 由调用方在 ExportMeta 时已置 CurrentVersion。
        /// 经 <see cref="Persistence.Provider"/> 写入（PlayerPrefs 非阻塞 / InMemory）。失败吞掉不抛
        /// （仿 BlockGameState.Save 的 try-catch 兜底），不阻断玩法。返回 UniTask 以满足异步红线。
        /// </summary>
        public static UniTask SaveAsync(MergeMetaSave dto)
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
        /// 读盘（读存储 + 反序列化 + 迁移 + 跨天重置），返回可用 DTO 或 null（无存档 / 加载失败 → 调用方走缺省）。
        /// 经 <see cref="Persistence.Provider"/> 读取。失败吞掉返回 null（仿 BlockGameState.Load）。
        /// today 默认取本地日期；测试可注入。返回 UniTask 以满足异步红线。
        /// </summary>
        public static UniTask<MergeMetaSave> LoadAsync(string today = null)
        {
            try
            {
                if (!Persistence.Provider.TryGet(StorageKey, out string raw) || string.IsNullOrEmpty(raw))
                    return UniTask.FromResult<MergeMetaSave>(null);

                var dto = Deserialize(raw);
                if (dto == null) return UniTask.FromResult<MergeMetaSave>(null);
                if (!Migrate(dto)) return UniTask.FromResult<MergeMetaSave>(null); // 未来档 → 缺省重置

                ApplyDailyReset(dto, today ?? Today());
                return UniTask.FromResult(dto);
            }
            catch
            {
                return UniTask.FromResult<MergeMetaSave>(null);
            }
        }

        /// <summary>
        /// 同步读盘（读存储 + 反序列化 + 迁移 + 跨天重置），返回可用 DTO 或 null。与 <see cref="LoadAsync"/> 共享纯管线。
        ///
        /// 加载路径走同步:经 <see cref="Persistence.Provider"/> 读取(生产 PlayerPrefs 为非阻塞内存级读、不触「禁阻塞 IO」红线,
        /// 与既有 <c>BlockGameState.Load</c> / <c>DynamicWeightDiff.Load</c> 同口径)。落盘(写)路径用 <see cref="SaveAsync"/> 异步外壳。
        /// 这样 <c>ResetForMergeOrder</c> 可保持同步、不被迫改成 async,而写盘仍满足异步红线。
        /// </summary>
        public static MergeMetaSave Load(string today = null)
        {
            try
            {
                if (!Persistence.Provider.TryGet(StorageKey, out string raw) || string.IsNullOrEmpty(raw))
                    return null;

                var dto = Deserialize(raw);
                if (dto == null) return null;
                if (!Migrate(dto)) return null; // 未来档 → 缺省重置

                ApplyDailyReset(dto, today ?? Today());
                return dto;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>清档（调试 / 重开新游戏用）。</summary>
        public static void Clear()
        {
            try { Persistence.Provider.Remove(StorageKey); }
            catch { /* ignore */ }
        }
    }
}
