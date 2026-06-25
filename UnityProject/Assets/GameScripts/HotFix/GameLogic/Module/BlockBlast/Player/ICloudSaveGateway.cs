using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>上传裁决码(客户端镜像 <c>Fantasy.CloudSaveUploadResultCode</c>,接口面不暴露网络库类型)。</summary>
    public enum CloudUploadCode
    {
        Accepted = 0,           // 接受并已覆盖,ServerVersion = 本次 version
        Stale = 1,              // 上传 version <= 已存,ServerBlob 回带服务端权威值,客户端据此合并
        BlobTooLarge = 2,       // blob 超上限(1MB)
        NotLoggedIn = 3,        // 会话未挂身份
        InvalidRequest = 4,     // version<=0 / blob 空 等
        ServiceUnavailable = 5, // 服务不可用 / 网络断 / 超时
    }

    /// <summary>下载裁决码(客户端镜像 <c>Fantasy.CloudSaveDownloadResultCode</c>)。</summary>
    public enum CloudDownloadCode
    {
        Success = 0,            // 取到存档,ServerVersion / ServerBlob 有效
        NoSnapshot = 1,         // 服务端无该 playerId 存档(全新账号),ServerVersion=0
        NotLoggedIn = 2,        // 会话未挂身份
        ServiceUnavailable = 3, // 服务不可用 / 网络断 / 超时
    }

    /// <summary>上传响应快照(已脱离 Fantasy 对象池,字段可安全跨边界持有)。</summary>
    public readonly struct CloudUploadResult
    {
        public readonly CloudUploadCode Code;
        public readonly long ServerVersion;
        public readonly byte[] ServerBlob; // 仅 Stale 时非空
        public CloudUploadResult(CloudUploadCode code, long serverVersion, byte[] serverBlob)
        {
            Code = code; ServerVersion = serverVersion; ServerBlob = serverBlob;
        }
    }

    /// <summary>下载响应快照。</summary>
    public readonly struct CloudDownloadResult
    {
        public readonly CloudDownloadCode Code;
        public readonly long ServerVersion;
        public readonly byte[] ServerBlob; // 仅 Success 时非空
        public CloudDownloadResult(CloudDownloadCode code, long serverVersion, byte[] serverBlob)
        {
            Code = code; ServerVersion = serverVersion; ServerBlob = serverBlob;
        }
    }

    /// <summary>
    /// 云存档 RPC 接缝(P3 全栈迁移·客户端段)。把网络层(<c>FantasyClient.FantasyNetwork.Session</c> + 协议消息)
    /// 抽到接缝后,使 <see cref="CloudSaveSync"/> 保持纯逻辑可 EditMode 单测(沿 <see cref="IRpcGateway"/> 范式)。
    /// </summary>
    /// <remarks>
    /// 生产实现 = <see cref="CloudSaveGatewayProd"/>(同目录,#if FANTASY_UNITY 包裹);测试实现 = 桩(返预设结果)。
    /// 返 <see cref="UniTask{T}"/> 而非 Fantasy.Async.FTask:接口面不暴露网络库类型(沿 RemoteRankSource / IRpcGateway 范式)。
    /// 任何往返失败(未连 / 超时 / 异常)以 ServiceUnavailable 归一,<b>不抛异常</b>。
    /// </remarks>
    public interface ICloudSaveGateway
    {
        /// <summary>上传一份存档快照(version + blob)。身份从会话取,不携带 playerId。</summary>
        UniTask<CloudUploadResult> UploadAsync(long version, byte[] blob);

        /// <summary>拉取本账号当前存档快照(身份从会话取)。</summary>
        UniTask<CloudDownloadResult> DownloadAsync();
    }
}
