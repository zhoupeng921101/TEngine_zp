using Cysharp.Threading.Tasks;
#if FANTASY_UNITY
using Fantasy; // 协议消息 + NetworkProtocolHelper 扩展方法 C2G_SetProfileStateRequest 所在命名空间
#endif

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 档案状态 SET 上报 RPC 接缝生产实现(皮肤/神庙装饰服务端权威·客户端段)。经 <c>FantasyClient.FantasyNetwork.Session</c>
    /// 发 <c>C2G_SetProfileStateRequest(skinMono, skinMonoId, templeDecorated)</c> 等响应,把服务端
    /// <c>SetProfileStateResultCode</c> 转 <see cref="ProfileStateResult"/>。
    /// </summary>
    /// <remarks>
    /// 降级(沿 <see cref="RenameGatewayProd"/> / <see cref="CosmeticGatewayProd"/> 范式):未连接 / 未登录 / 发不出 / 超时 /
    /// 空响应 → 以 <see cref="ProfileStateResult.Rejected"/>(NetworkDown / NotLoggedIn / ServiceUnavailable)返、<b>不抛异常</b>。
    /// 程序集边界:网络层受 FANTASY_UNITY 约束,该 define 关闭的平台无网络可用,本类同样降级返 ServiceUnavailable,
    /// 使 GameLogic 在任何平台都可编译。
    /// </remarks>
    public sealed class ProfileStateGatewayProd : IProfileStateGateway
    {
        public async UniTask<ProfileStateResult> SetProfileStateAsync(int skinMono, int skinMonoId, int templeDecoratedCount)
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
            {
                return ProfileStateResult.Rejected(ProfileStateOutcome.NetworkDown); // 未连接:不发请求、不抛
            }
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
            {
                return ProfileStateResult.Rejected(ProfileStateOutcome.NotLoggedIn);
            }

            G2C_SetProfileStateResponse response;
            try
            {
                // FTask 自带 awaiter,可在 async UniTask 体内直接 await(memory「跨框架通用异步与网络库异步」)。
                // 协议 TempleDecorated 为 long,厅数标量(小值)传入即可。
                response = await session.C2G_SetProfileStateRequest(skinMono, skinMonoId, templeDecoratedCount);
            }
            catch
            {
                return ProfileStateResult.Rejected(ProfileStateOutcome.ServiceUnavailable);
            }

            if (response == null)
            {
                return ProfileStateResult.Rejected(ProfileStateOutcome.ServiceUnavailable);
            }

            return MapResponse(response);
#else
            // FANTASY_UNITY 关闭(无网络平台):降级为服务不可用,不抛
            await UniTask.CompletedTask;
            return ProfileStateResult.Rejected(ProfileStateOutcome.ServiceUnavailable);
#endif
        }

#if FANTASY_UNITY
        /// <summary>把协议响应结果码转客户端 <see cref="ProfileStateResult"/>(fire-and-forget,不取回带权威三态)。</summary>
        private static ProfileStateResult MapResponse(G2C_SetProfileStateResponse response)
        {
            switch ((SetProfileStateResultCode)response.ResultCode)
            {
                case SetProfileStateResultCode.Success:
                    return ProfileStateResult.Ok();
                case SetProfileStateResultCode.InvalidRequest:
                    return ProfileStateResult.Rejected(ProfileStateOutcome.InvalidRequest);
                case SetProfileStateResultCode.NotLoggedIn:
                    return ProfileStateResult.Rejected(ProfileStateOutcome.NotLoggedIn);
                case SetProfileStateResultCode.ServiceUnavailable:
                    return ProfileStateResult.Rejected(ProfileStateOutcome.ServiceUnavailable);
                default:
                    return ProfileStateResult.Rejected(ProfileStateOutcome.ServiceUnavailable); // 未知码兜底
            }
        }
#endif
    }
}
