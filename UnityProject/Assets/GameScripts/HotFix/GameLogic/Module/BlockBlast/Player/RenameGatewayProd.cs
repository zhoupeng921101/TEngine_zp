using Cysharp.Threading.Tasks;
#if FANTASY_UNITY
using Fantasy; // 协议消息 + NetworkProtocolHelper 扩展方法 C2G_RenameRequest 所在命名空间
#endif

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 改名 RPC 接缝生产实现(改名服务端权威·客户端段)。经 <c>FantasyClient.FantasyNetwork.Session</c>
    /// 发 <c>C2G_RenameRequest(newNickname)</c> 同步等响应,把服务端 <c>RenameResultCode</c> + 权威值
    /// (Nickname/RenameCount/Diamond)转 <see cref="RenameRpcResult"/>。
    /// </summary>
    /// <remarks>
    /// 降级(沿 <see cref="RpcGatewayProd"/> 范式):未连接 / 未登录 / 发不出 / 超时 / 空响应 → 以
    /// <see cref="RenameRpcResult.Rejected"/>(NetworkDown / NotLoggedIn / ServiceUnavailable)返、<b>不抛异常</b>,
    /// 由调用方据 Outcome 决定是否对齐视图。程序集边界:网络层受 FANTASY_UNITY 约束,该 define 关闭的平台
    /// 无网络可用,本类同样降级返 ServiceUnavailable,使 GameLogic 在任何平台都可编译。
    /// </remarks>
    public sealed class RenameGatewayProd : IRenameGateway
    {
        public async UniTask<RenameRpcResult> SendRenameAsync(string newNickname)
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
            {
                return RenameRpcResult.Rejected(RenameOutcome.NetworkDown); // 未连接:不发请求、不抛
            }
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
            {
                return RenameRpcResult.Rejected(RenameOutcome.NotLoggedIn);
            }

            G2C_RenameResponse response;
            try
            {
                // FTask 自带 awaiter,可在 async UniTask 体内直接 await(memory「跨框架通用异步与网络库异步」)
                response = await session.C2G_RenameRequest(newNickname);
            }
            catch
            {
                return RenameRpcResult.Rejected(RenameOutcome.ServiceUnavailable);
            }

            if (response == null)
            {
                return RenameRpcResult.Rejected(RenameOutcome.ServiceUnavailable);
            }

            return MapResponse(response);
#else
            // FANTASY_UNITY 关闭(无网络平台):降级为服务不可用,不抛
            await UniTask.CompletedTask;
            return RenameRpcResult.Rejected(RenameOutcome.ServiceUnavailable);
#endif
        }

#if FANTASY_UNITY
        /// <summary>把协议响应转客户端 <see cref="RenameRpcResult"/>(含服务端权威 Nickname/RenameCount/Diamond)。</summary>
        private static RenameRpcResult MapResponse(G2C_RenameResponse response)
        {
            switch (response.ResultCode)
            {
                case RenameResultCode.Success:
                    return RenameRpcResult.Ok(response.Nickname, response.RenameCount, response.Diamond);
                case RenameResultCode.InvalidName:
                    return RenameRpcResult.Rejected(RenameOutcome.InvalidName, response.Nickname, response.RenameCount, response.Diamond);
                case RenameResultCode.NotEnoughDiamond:
                    return RenameRpcResult.Rejected(RenameOutcome.NotEnoughDiamond, response.Nickname, response.RenameCount, response.Diamond);
                case RenameResultCode.NotLoggedIn:
                    return RenameRpcResult.Rejected(RenameOutcome.NotLoggedIn);
                case RenameResultCode.ServiceUnavailable:
                    return RenameRpcResult.Rejected(RenameOutcome.ServiceUnavailable);
                default:
                    return RenameRpcResult.Rejected(RenameOutcome.ServiceUnavailable); // 未知码兜底
            }
        }
#endif
    }
}
