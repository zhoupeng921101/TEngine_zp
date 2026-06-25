using System;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 云存档载荷 DTO（P3 全栈迁移·客户端段）。把「低危可移植」的本地存档切片打包成一份扁平
    /// <see cref="UnityEngine.JsonUtility"/>-友好结构,再转 bytes 作为 <c>C2G_CloudSaveUploadRequest.Blob</c> 上行。
    ///
    /// 设计要点:
    /// - 复用既有本地存档的 JSON 字符串(不发明新格式):局内态 / Classic 局内态 / dynamicWeight 三键原样取其 JSON 串塞入,
    ///   下载时按键原样写回 <see cref="Persistence"/>,与现有本地读盘逻辑(各自 Deserialize)无缝对接。
    /// - 元层(<see cref="MergeMetaSave"/>)只搬「非货币非身份」字段:经 <see cref="CloudSaveCodec"/> 把货币四项
    ///   (soul/piety/exp/energy/lastEnergyRegenTime)与 playerId <b>清零/清空</b>后再序列化进 <see cref="MetaJson"/>。
    ///
    /// 【绝不入 blob】soul/piety/exp/energy/lastEnergyRegenTime(P2 服务端权威,走 PropertyChange/快照) +
    /// playerId(P0 服务端权威,登录签发)。装了会在下载回灌时用旧 blob 覆盖 P2/P0 刚下发的权威值,故必须排除。
    /// </summary>
    [Serializable]
    public sealed class CloudSavePayload
    {
        /// <summary>载荷结构版本(非云存档同步 version;后者是单调冲突解决用的本地 version,见 <see cref="CloudSaveSync"/>)。</summary>
        public int payloadVersion;

        /// <summary>融合局内态 JSON(键 block_blast_merge_ingame_v1 的原始串)。空 = 无该切片。</summary>
        public string mergeIngameJson;

        /// <summary>Classic 局内态 JSON(键 block_blast_save_v1 的原始串)。空 = 无该切片。</summary>
        public string classicJson;

        /// <summary>dynamicWeight JSON(键 block_blast_dynamic_v1 的原始串)。空 = 无该切片。</summary>
        public string dynamicJson;

        /// <summary>
        /// 元层「非货币非身份」字段 JSON(<see cref="MergeMetaSave"/> 序列化,但货币四项 + playerId 已被
        /// <see cref="CloudSaveCodec"/> 清零/清空)。空 = 无元层切片。
        /// </summary>
        public string metaJson;
    }
}
