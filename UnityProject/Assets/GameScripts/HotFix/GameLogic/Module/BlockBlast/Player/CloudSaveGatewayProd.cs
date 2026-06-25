using Cysharp.Threading.Tasks;
#if FANTASY_UNITY
using Fantasy; // 协议消息 + NetworkProtocolHelper 扩展方法 C2G_CloudSaveUploadRequest/DownloadRequest 所在命名空间
#endif

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 云存档 RPC 接缝生产实现(P3 全栈迁移·客户端段)。经 <c>FantasyClient.FantasyNetwork.Session</c>
    /// 发 <c>C2G_CloudSaveUploadRequest</c> / <c>C2G_CloudSaveDownloadRequest</c> 同步等响应,把服务端
    /// <c>CloudSave*ResultCode</c> 转客户端 <see cref="CloudUploadCode"/> / <see cref="CloudDownloadCode"/>。
    /// </summary>
    /// <remarks>
    /// 降级(沿 <see cref="RpcGatewayProd"/> 范式):未连 / 未登 / 发不出 / 超时 / 任何往返失败 → ServiceUnavailable,
    /// <b>不抛异常</b>(由 <see cref="CloudSaveSync"/> 据码决定是否动本地)。
    /// 程序集边界:网络层受 FANTASY_UNITY 约束;该 define 关闭的平台无网络,本类同样降级返 ServiceUnavailable,
    /// 使 GameLogic 在任何平台都可编译。
    /// 跨边界复制:Fantasy 协议响应对象用完即回池,字段(ServerVersion / ServerBlob)在 await 返回后立即复制进
    /// 值类型结果再返回,避免引用悬空。
    /// </remarks>
    public sealed class CloudSaveGatewayProd : ICloudSaveGateway
    {
        public async UniTask<CloudUploadResult> UploadAsync(long version, byte[] blob)
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
                return new CloudUploadResult(CloudUploadCode.ServiceUnavailable, 0, null);
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
                return new CloudUploadResult(CloudUploadCode.NotLoggedIn, 0, null);

            G2C_CloudSaveUploadResponse response;
            try
            {
                TEngine.Log.Info($"[Fantasy] 发送云存档上传 version={version} blobBytes={blob?.Length ?? 0}");
                response = await session.C2G_CloudSaveUploadRequest(version, blob);
            }
            catch
            {
                return new CloudUploadResult(CloudUploadCode.ServiceUnavailable, 0, null);
            }
            if (response == null)
                return new CloudUploadResult(CloudUploadCode.ServiceUnavailable, 0, null);

            // 立即复制出值类型(协议对象随 await 后回池)。
            return new CloudUploadResult(MapUpload(response.ResultCode), response.ServerVersion, response.ServerBlob);
#else
            await UniTask.CompletedTask;
            return new CloudUploadResult(CloudUploadCode.ServiceUnavailable, 0, null);
#endif
        }

        public async UniTask<CloudDownloadResult> DownloadAsync()
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
                return new CloudDownloadResult(CloudDownloadCode.ServiceUnavailable, 0, null);
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
                return new CloudDownloadResult(CloudDownloadCode.NotLoggedIn, 0, null);

            G2C_CloudSaveDownloadResponse response;
            try
            {
                TEngine.Log.Info($"[Fantasy] 发送云存档下载");
                response = await session.C2G_CloudSaveDownloadRequest();
            }
            catch
            {
                return new CloudDownloadResult(CloudDownloadCode.ServiceUnavailable, 0, null);
            }
            if (response == null)
                return new CloudDownloadResult(CloudDownloadCode.ServiceUnavailable, 0, null);

            return new CloudDownloadResult(MapDownload(response.ResultCode), response.ServerVersion, response.ServerBlob);
#else
            await UniTask.CompletedTask;
            return new CloudDownloadResult(CloudDownloadCode.ServiceUnavailable, 0, null);
#endif
        }

#if FANTASY_UNITY
        private static CloudUploadCode MapUpload(CloudSaveUploadResultCode code)
        {
            switch (code)
            {
                case CloudSaveUploadResultCode.Accepted:           return CloudUploadCode.Accepted;
                case CloudSaveUploadResultCode.Stale:              return CloudUploadCode.Stale;
                case CloudSaveUploadResultCode.BlobTooLarge:       return CloudUploadCode.BlobTooLarge;
                case CloudSaveUploadResultCode.NotLoggedIn:        return CloudUploadCode.NotLoggedIn;
                case CloudSaveUploadResultCode.InvalidRequest:     return CloudUploadCode.InvalidRequest;
                case CloudSaveUploadResultCode.ServiceUnavailable: return CloudUploadCode.ServiceUnavailable;
                default:                                           return CloudUploadCode.ServiceUnavailable;
            }
        }

        private static CloudDownloadCode MapDownload(CloudSaveDownloadResultCode code)
        {
            switch (code)
            {
                case CloudSaveDownloadResultCode.Success:            return CloudDownloadCode.Success;
                case CloudSaveDownloadResultCode.NoSnapshot:         return CloudDownloadCode.NoSnapshot;
                case CloudSaveDownloadResultCode.NotLoggedIn:        return CloudDownloadCode.NotLoggedIn;
                case CloudSaveDownloadResultCode.ServiceUnavailable: return CloudDownloadCode.ServiceUnavailable;
                default:                                            return CloudDownloadCode.ServiceUnavailable;
            }
        }
#endif
    }
}
