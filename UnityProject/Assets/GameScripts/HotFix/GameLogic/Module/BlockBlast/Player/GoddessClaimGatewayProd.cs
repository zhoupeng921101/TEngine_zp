using System.Collections.Generic;
using Cysharp.Threading.Tasks;
#if FANTASY_UNITY
using Fantasy; // 协议消息 + NetworkProtocolHelper 扩展方法 C2G_GoddessClaimRequest 所在命名空间
#endif

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 女神领取 RPC 接缝生产实现(女神系统·客户端段)。经 <c>FantasyClient.FantasyNetwork.Session</c>
    /// 发 <c>C2G_GoddessClaimRequest(0)</c> 同步等响应,把服务端 <c>GoddessClaimResultCode</c> + 奖励 payload
    /// (ElementType + Rewards[Level,Count])转框架中立 <see cref="GoddessClaimResult"/>。
    /// </summary>
    /// <remarks>
    /// 降级(沿 <see cref="WishGatewayProd"/> 范式):未连接 / 未登录 / 发不出 / 超时 / 空响应 → 以
    /// <see cref="GoddessClaimResult.Rejected"/>(NetworkDown / NotLoggedIn / ServiceUnavailable)返、<b>不抛异常</b>。
    /// 程序集边界:网络层受 FANTASY_UNITY 约束,该 define 关闭的平台无网络可用,本类同样降级返 ServiceUnavailable,
    /// 使 GameLogic 在任何平台都可编译。
    /// </remarks>
    public sealed class GoddessClaimGatewayProd : IGoddessClaimGateway
    {
        public async UniTask<GoddessClaimResult> ClaimAsync()
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
            {
                return GoddessClaimResult.Rejected(GoddessClaimOutcome.NetworkDown); // 未连接:不发请求、不抛
            }
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
            {
                return GoddessClaimResult.Rejected(GoddessClaimOutcome.NotLoggedIn);
            }

            G2C_GoddessClaimResponse response;
            try
            {
                // FTask 自带 awaiter,可在 async UniTask 体内直接 await。Reserved 占位恒填 0(领取内容全由服务端裁定)。
                response = await session.C2G_GoddessClaimRequest(0);
            }
            catch
            {
                return GoddessClaimResult.Rejected(GoddessClaimOutcome.ServiceUnavailable);
            }

            if (response == null)
            {
                return GoddessClaimResult.Rejected(GoddessClaimOutcome.ServiceUnavailable);
            }

            return MapResponse(response);
#else
            // FANTASY_UNITY 关闭(无网络平台):降级为服务不可用,不抛
            await UniTask.CompletedTask;
            return GoddessClaimResult.Rejected(GoddessClaimOutcome.ServiceUnavailable);
#endif
        }

#if FANTASY_UNITY
        /// <summary>把协议响应转客户端 <see cref="GoddessClaimResult"/>(成功时含发放元素类型 + 各档等级/数量)。</summary>
        private static GoddessClaimResult MapResponse(G2C_GoddessClaimResponse response)
        {
            switch ((GoddessClaimResultCode)response.ResultCode)
            {
                case GoddessClaimResultCode.Success:
                {
                    var list = new List<GoddessRewardEntry>();
                    if (response.Rewards != null)
                    {
                        foreach (var r in response.Rewards)
                        {
                            if (r != null) list.Add(new GoddessRewardEntry(r.Level, r.Count));
                        }
                    }
                    return GoddessClaimResult.Ok(response.ElementType, list);
                }
                case GoddessClaimResultCode.NotFull:
                    return GoddessClaimResult.Rejected(GoddessClaimOutcome.NotFull);
                case GoddessClaimResultCode.NotLoggedIn:
                    return GoddessClaimResult.Rejected(GoddessClaimOutcome.NotLoggedIn);
                case GoddessClaimResultCode.ServiceUnavailable:
                    return GoddessClaimResult.Rejected(GoddessClaimOutcome.ServiceUnavailable);
                default:
                    return GoddessClaimResult.Rejected(GoddessClaimOutcome.ServiceUnavailable); // 未知码兜底
            }
        }
#endif
    }
}
