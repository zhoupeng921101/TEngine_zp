using Cysharp.Threading.Tasks;
#if FANTASY_UNITY
using Fantasy; // 协议消息 + NetworkProtocolHelper 扩展方法 C2G_EnterMainGameRequest 所在命名空间
#endif

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 进主游戏请求接缝生产实现(全栈协议改动·客户端段)。经 <c>FantasyClient.FantasyNetwork.Session</c>
    /// 发 <c>C2G_EnterMainGameRequest</c>(空请求,身份从会话取)同步等响应,把服务端一次性原子响应
    /// <c>G2C_EnterMainGameResponse</c>(订单快照 + 云存档同包回带)转框架中立 <see cref="EnterMainGameResult"/>。
    /// </summary>
    /// <remarks>
    /// 降级(沿 <see cref="CloudSaveGatewayProd"/> / <see cref="OrderRpcGatewayProd"/> 范式):未连 / 未登 / 发不出 / 超时 /
    /// 任何往返失败 → <see cref="EnterMainGameResult.Unavailable"/>,<b>不抛异常</b>(由 <see cref="EnterMainGameSync"/> 据码决定本地兜底)。
    /// 程序集边界:网络层受 FANTASY_UNITY 约束;该 define 关闭的平台无网络,本类同样降级返 Unavailable,使 GameLogic 在任何平台都可编译。
    /// 跨边界复制:Fantasy 协议响应对象用完即回池,订单快照经 <see cref="OrderRpcGatewayProd.ToSnapshot"/> 拷独立副本、
    /// 云存档 blob(byte[])在 await 返回后立即复制进值类型结果再返回,避免引用悬空。
    /// </remarks>
    public sealed class EnterMainGameGatewayProd : IEnterMainGameGateway
    {
        public async UniTask<EnterMainGameResult> EnterAsync()
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
                return EnterMainGameResult.Unavailable();
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
                return EnterMainGameResult.Unavailable();

            G2C_EnterMainGameResponse response;
            try
            {
                response = await session.C2G_EnterMainGameRequest();
            }
            catch
            {
                return EnterMainGameResult.Unavailable();
            }
            if (response == null)
                return EnterMainGameResult.Unavailable();

            // 立即复制出值类型(协议对象随 await 后回池):订单快照转独立 DTO,云存档 blob 直接持引用(协议 byte[] 不入池复用,可安全持有)。
            var order = OrderRpcGatewayProd.ToSnapshot(response.OrderSnapshot);
            return new EnterMainGameResult(order,
                MapDownload(response.CloudSaveResultCode), response.CloudSaveServerVersion, response.CloudSaveServerBlob);
#else
            await UniTask.CompletedTask;
            return EnterMainGameResult.Unavailable();
#endif
        }

#if FANTASY_UNITY
        /// <summary>把协议云存档下载码转客户端 <see cref="CloudDownloadCode"/>(枚举值一一对齐,未知码兜底 ServiceUnavailable)。</summary>
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
