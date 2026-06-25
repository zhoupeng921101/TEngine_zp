using Cysharp.Threading.Tasks;
#if FANTASY_UNITY
using Fantasy; // 协议消息 + NetworkProtocolHelper 扩展方法 C2G_PropertyChangeRequest 所在命名空间
#endif

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 属性变更 RPC 接缝生产实现(设计 38 §四 + §五接线)。经 <c>FantasyClient.FantasyNetwork.Session</c>
    /// 发 <c>C2G_PropertyChangeRequest</c> 同步等响应,把服务端 <c>PropertyChangeResultCode</c> 转 <see cref="ChangeReject"/>。
    /// </summary>
    /// <remarks>
    /// 降级(沿 RemoteRankSource 范式):未连接 / 发不出 / 超时 / 任何往返失败 → 返 <see cref="ChangeReject.NetworkDown"/> 或
    /// <see cref="ChangeReject.ServiceUnavailable"/>,<b>不抛异常</b>(由 <see cref="PlayerAttrService"/> 据 Reason 决定是否动视图)。
    /// 与改名「不本地放行」语义一致(沿 30 / 36 / 38 §读前必看):服务不可用 → 客户端不冒进。
    /// 程序集边界:网络层 FantasyClient / Fantasy.Unity 受 FANTASY_UNITY 约束;
    /// 该 define 关闭的平台无网络可用,本类同样降级返 ServiceUnavailable,使 GameLogic 在任何平台都可编译。
    /// </remarks>
    public sealed class RpcGatewayProd : IRpcGateway
    {
        public async UniTask<ChangeResult> SendChangeRequestAsync(AttrType type, long delta, string reason)
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
            {
                return ChangeResult.Rejected(ChangeReject.NetworkDown); // 未连接:不发请求、不抛
            }
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
            {
                return ChangeResult.Rejected(ChangeReject.NotLoggedIn);
            }

            G2C_PropertyChangeResponse response;
            try
            {
                // FTask 自带 awaiter,可在 async UniTask 体内直接 await(memory「跨框架通用异步与网络库异步」)
                TEngine.Log.Info($"[Fantasy] 发送属性变更 Type={type} Delta={delta} Reason={reason}");
                response = await session.C2G_PropertyChangeRequest((PropertyType)type, delta, reason);
            }
            catch
            {
                return ChangeResult.Rejected(ChangeReject.ServiceUnavailable);
            }

            if (response == null)
            {
                return ChangeResult.Rejected(ChangeReject.ServiceUnavailable);
            }

            return MapResponse(response);
#else
            // FANTASY_UNITY 关闭(无网络平台):降级为服务不可用,不抛
            await UniTask.CompletedTask;
            return ChangeResult.Rejected(ChangeReject.ServiceUnavailable);
#endif
        }

#if FANTASY_UNITY
        /// <summary>
        /// 把协议响应转客户端 <see cref="ChangeResult"/>(纯转换,理论可拆出可 EditMode 测,但因含 Fantasy 类型不便 — 留给真往返核 E2/E3)。
        /// </summary>
        private static ChangeResult MapResponse(G2C_PropertyChangeResponse response)
        {
            switch (response.ResultCode)
            {
                case PropertyChangeResultCode.Success:
                    return ChangeResult.Ok(response.NewAmount);
                case PropertyChangeResultCode.NotEnough:
                    return ChangeResult.Rejected(ChangeReject.NotEnoughBalance, response.NewAmount);
                case PropertyChangeResultCode.OverLimit:
                    return ChangeResult.Rejected(ChangeReject.TypeUpperOverflow, response.NewAmount);
                case PropertyChangeResultCode.NotLoggedIn:
                    return ChangeResult.Rejected(ChangeReject.NotLoggedIn);
                case PropertyChangeResultCode.UnknownType:
                case PropertyChangeResultCode.InvalidRequest:
                    return ChangeResult.Rejected(ChangeReject.TypeUnknown);
                case PropertyChangeResultCode.ServiceUnavailable:
                    return ChangeResult.Rejected(ChangeReject.ServiceUnavailable);
                default:
                    return ChangeResult.Rejected(ChangeReject.ServiceUnavailable); // 未知码兜底
            }
        }
#endif
    }
}
