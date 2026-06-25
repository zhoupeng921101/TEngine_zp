using System;
using System.Text;
using UnityEngine;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 云存档载荷 ↔ 本地存储 的纯逻辑编解码(P3 全栈迁移·客户端段)。无 IO 之外的网络依赖,经
    /// <see cref="Persistence.Provider"/> 读写本地键,可 EditMode 单测(注 InMemory provider)。
    ///
    /// 职责:
    /// - <see cref="BuildBlob"/>:把本地「可移植切片」(局内态 / Classic / dynamic / 元层非货币非身份字段)
    ///   组装成 <see cref="CloudSavePayload"/> 再转 bytes。组装时把元层货币四项 + playerId 清零/清空(绝不入 blob)。
    /// - <see cref="ApplyBlob"/>:把下载到的 bytes 解回 <see cref="CloudSavePayload"/>,按键写回本地存储。
    ///   元层应用时<b>保留本地现有货币 + playerId</b>(从本地既有元档读出后只覆盖非货币非身份字段),
    ///   避免用 blob 里的(已清零)货币/身份冲掉 P2/P0 权威值。
    /// </summary>
    public static class CloudSaveCodec
    {
        /// <summary>当前载荷结构版本。</summary>
        public const int PayloadVersion = 1;

        /// <summary>元层本地存储键(= <see cref="MergeMetaPersistence.StorageKey"/>)。</summary>
        private const string MetaKey = MergeMetaPersistence.StorageKey;
        /// <summary>融合局内态本地存储键(= <see cref="MergeIngamePersistence.StorageKey"/>)。</summary>
        private const string MergeIngameKey = MergeIngamePersistence.StorageKey;
        /// <summary>Classic 局内态本地存储键。</summary>
        private const string ClassicKey = "block_blast_save_v1";
        /// <summary>dynamicWeight 本地存储键。</summary>
        private const string DynamicKey = "block_blast_dynamic_v1";

        /// <summary>
        /// 从本地存储组装一份云存档 blob(bytes)。无任何可移植切片时仍返回一份合法(空切片)载荷的 bytes
        /// (上传空档无害;version 单调推进由 <see cref="CloudSaveSync"/> 决策)。失败返回 null(调用方不上传)。
        /// </summary>
        public static byte[] BuildBlob()
        {
            try
            {
                var payload = new CloudSavePayload
                {
                    payloadVersion = PayloadVersion,
                    mergeIngameJson = ReadKey(MergeIngameKey),
                    classicJson = ReadKey(ClassicKey),
                    dynamicJson = ReadKey(DynamicKey),
                    metaJson = BuildSanitizedMetaJson(),
                };
                string json = JsonUtility.ToJson(payload);
                if (string.IsNullOrEmpty(json)) return null;
                return Encoding.UTF8.GetBytes(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 把下载到的 blob(bytes)应用到本地存储:按键写回局内态 / Classic / dynamic;元层只覆盖非货币非身份字段,
        /// 保留本地现有货币 + playerId(权威值不被 blob 冲掉)。bytes 为 null/空/非法 → 返回 false、不动本地。
        /// </summary>
        public static bool ApplyBlob(byte[] blob)
        {
            if (blob == null || blob.Length == 0) return false;
            CloudSavePayload payload;
            try
            {
                string json = Encoding.UTF8.GetString(blob);
                if (string.IsNullOrEmpty(json)) return false;
                payload = JsonUtility.FromJson<CloudSavePayload>(json);
            }
            catch
            {
                return false;
            }
            if (payload == null) return false;

            try
            {
                // 局内态 / Classic / dynamic:原样写回(各自本地读盘逻辑负责 Deserialize/夹值)。空串 = 该切片缺,不动本地。
                WriteKeyIfPresent(MergeIngameKey, payload.mergeIngameJson);
                WriteKeyIfPresent(ClassicKey, payload.classicJson);
                WriteKeyIfPresent(DynamicKey, payload.dynamicJson);

                // 元层:合并应用,保留本地货币 + playerId。
                ApplyMetaPreservingCurrencyAndIdentity(payload.metaJson);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ── 元层 sanitize / merge ────────────────────────────────

        /// <summary>
        /// 读本地元档 → 清零货币四项 + 清空 playerId → 序列化为 metaJson。无本地元档返回空串(无元层切片)。
        /// 清零而非删字段:DTO 是固定结构,JsonUtility 无「省略字段」概念;清零使下载端合并时这些值被忽略
        /// (合并逻辑只取非货币非身份字段)。
        /// </summary>
        private static string BuildSanitizedMetaJson()
        {
            string raw = ReadKey(MetaKey);
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var dto = MergeMetaPersistence.Deserialize(raw);
            if (dto == null) return string.Empty;
            StripCurrencyAndIdentity(dto);
            return MergeMetaPersistence.Serialize(dto);
        }

        /// <summary>
        /// 把 metaJson(发送端已 strip 过货币/身份)合并进本地元档:以本地现有元档为底(保留货币 + playerId),
        /// 只用 blob 元层覆盖「非货币非身份」字段,再落盘。本地无元档时新建一份(货币缺省 0 + 空 playerId,
        /// 由后续 P2 快照 / P0 登录补权威值)。metaJson 空 → 不动本地元档。
        /// </summary>
        private static void ApplyMetaPreservingCurrencyAndIdentity(string metaJson)
        {
            if (string.IsNullOrEmpty(metaJson)) return;
            var incoming = MergeMetaPersistence.Deserialize(metaJson);
            if (incoming == null) return;

            // 本地底档:保留其货币 + playerId 作为权威基准。
            var local = MergeMetaPersistence.Deserialize(ReadKey(MetaKey))
                        ?? new MergeMetaSave { version = MergeMetaPersistence.CurrentVersion };

            // 把 incoming 的非货币非身份字段拷到 local(local 的货币/playerId 不动)。
            CopyNonCurrencyNonIdentity(from: incoming, to: local);
            local.version = MergeMetaPersistence.CurrentVersion;

            // 直接经 Provider 落盘(不走 SaveAsync:避免触发 OnSaved 货币上报钩子——此处是下载回灌,非玩法事件边界)。
            string json = MergeMetaPersistence.Serialize(local);
            if (!string.IsNullOrEmpty(json)) Persistence.Provider.Set(MetaKey, json);
        }

        /// <summary>把货币四项清零 + playerId 清空(绝不入 blob)。其余字段不动。</summary>
        private static void StripCurrencyAndIdentity(MergeMetaSave dto)
        {
            // P2 服务端权威货币:soul/piety/exp/energy + lastEnergyRegenTime(体力时基依据,连带清)。
            dto.soul = 0;
            dto.piety = 0;
            dto.exp = 0;
            dto.energy = 0;
            dto.lastEnergyRegenTime = 0;
            // P0 服务端权威身份。
            dto.playerId = null;
        }

        /// <summary>
        /// 把 <paramref name="from"/> 的「非货币非身份」字段逐项拷到 <paramref name="to"/>。
        /// 显式枚举字段(不反射、不整体覆盖):新增元层字段需在此登记,使「哪些进云存档」一目了然、可审计。
        /// 不拷:soul/piety/exp/energy/lastEnergyRegenTime(货币)、playerId(身份)。
        /// </summary>
        private static void CopyNonCurrencyNonIdentity(MergeMetaSave from, MergeMetaSave to)
        {
            // ── 解锁/进度标志 ──
            to.unlockedChapter = from.unlockedChapter;
            to.nextRepairIndex = from.nextRepairIndex;
            to.templeRepaired = from.templeRepaired;
            to.templeDecorated = from.templeDecorated;
            to.blindBoxCount = from.blindBoxCount;
            to.goddessRating = from.goddessRating;
            to.goddessLevel = from.goddessLevel;
            to.highScore = from.highScore;
            // ── 每日态 ──
            to.wishUsedToday = from.wishUsedToday;
            to.lastWishResetDate = from.lastWishResetDate;
            // ── 皮肤态 ──
            to.skinMono = from.skinMono;
            to.skinMonoId = from.skinMonoId;
            // ── 玩家档案(非身份:昵称/头像/解锁集/账号经验/改名次数)──
            to.playerName = from.playerName;
            to.playerRenameCount = from.playerRenameCount;
            to.playerExp = from.playerExp;
            to.curAvatarId = from.curAvatarId;
            to.curFrameId = from.curFrameId;
            to.unlockedAvatarIds = from.unlockedAvatarIds;
            to.unlockedFrameIds = from.unlockedFrameIds;
        }

        // ── 本地存储读写小工具 ──────────────────────────────────

        private static string ReadKey(string key)
        {
            return Persistence.Provider.TryGet(key, out string v) && !string.IsNullOrEmpty(v) ? v : string.Empty;
        }

        private static void WriteKeyIfPresent(string key, string json)
        {
            if (!string.IsNullOrEmpty(json)) Persistence.Provider.Set(key, json);
        }
    }
}
