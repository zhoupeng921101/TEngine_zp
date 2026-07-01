using Cysharp.Threading.Tasks;
#if FANTASY_UNITY
using Fantasy; // 协议消息 + NetworkProtocolHelper 扩展方法 C2G_WishForEnergyRequest 所在命名空间
#endif

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 祈愿 RPC 接缝生产实现(祈愿服务端权威·客户端段)。经 <c>FantasyClient.FantasyNetwork.Session</c>
    /// 发 <c>C2G_WishForEnergyRequest()</c>(空载荷)同步等响应,把服务端 <c>WishForEnergyResultCode</c> + 权威值
    /// (SoulPower/Energy/WishUsedToday/WishDailyLimit)转 <see cref="WishRpcResult"/>。
    /// </summary>
    /// <remarks>
    /// 降级(沿 <see cref="RenameGatewayProd"/> 范式):未连接 / 未登录 / 发不出 / 超时 / 空响应 → 以
    /// <see cref="WishRpcResult.Rejected"/>(NetworkDown / NotLoggedIn / ServiceUnavailable)返、<b>不抛异常</b>,
    /// 由调用方据 Outcome 决定是否对齐投影。程序集边界:网络层受 FANTASY_UNITY 约束,该 define 关闭的平台
    /// 无网络可用,本类同样降级返 ServiceUnavailable,使 GameLogic 在任何平台都可编译。
    /// </remarks>
    public sealed class WishGatewayProd : IWishGateway
    {
        public async UniTask<WishRpcResult> WishAsync()
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
            {
                return WishRpcResult.Rejected(WishOutcome.NetworkDown); // 未连接:不发请求、不抛
            }
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
            {
                return WishRpcResult.Rejected(WishOutcome.NotLoggedIn);
            }

            G2C_WishForEnergyResponse response;
            try
            {
                // FTask 自带 awaiter,可在 async UniTask 体内直接 await(memory「跨框架通用异步与网络库异步」)。
                // 用无载荷重载(NetworkProtocolHelper 内部 Create + 用完回池)。
                response = await session.C2G_WishForEnergyRequest();
            }
            catch
            {
                return WishRpcResult.Rejected(WishOutcome.ServiceUnavailable);
            }

            if (response == null)
            {
                return WishRpcResult.Rejected(WishOutcome.ServiceUnavailable);
            }

            return MapResponse(response);
#else
            // FANTASY_UNITY 关闭(无网络平台):降级为服务不可用,不抛
            await UniTask.CompletedTask;
            return WishRpcResult.Rejected(WishOutcome.ServiceUnavailable);
#endif
        }

#if FANTASY_UNITY
        /// <summary>把协议响应转客户端 <see cref="WishRpcResult"/>(含服务端权威 SoulPower/Energy/WishUsedToday/WishDailyLimit)。</summary>
        private static WishRpcResult MapResponse(G2C_WishForEnergyResponse response)
        {
            switch ((WishForEnergyResultCode)response.ResultCode)
            {
                case WishForEnergyResultCode.Success:
                    return WishRpcResult.Ok(response.SoulPower, response.Energy, response.WishUsedToday, response.WishDailyLimit);
                case WishForEnergyResultCode.DailyLimitReached:
                    return WishRpcResult.Rejected(WishOutcome.DailyLimitReached, response.SoulPower, response.Energy, response.WishUsedToday, response.WishDailyLimit);
                case WishForEnergyResultCode.NotEnoughSoul:
                    return WishRpcResult.Rejected(WishOutcome.NotEnoughSoul, response.SoulPower, response.Energy, response.WishUsedToday, response.WishDailyLimit);
                case WishForEnergyResultCode.NotLoggedIn:
                    return WishRpcResult.Rejected(WishOutcome.NotLoggedIn);
                case WishForEnergyResultCode.ServiceUnavailable:
                    return WishRpcResult.Rejected(WishOutcome.ServiceUnavailable);
                default:
                    return WishRpcResult.Rejected(WishOutcome.ServiceUnavailable); // 未知码兜底
            }
        }
#endif
    }
}
