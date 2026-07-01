using System.Collections.Generic;
using Cysharp.Threading.Tasks;
#if FANTASY_UNITY
using Fantasy; // 协议消息 + NetworkProtocolHelper 扩展方法 C2G_PropertyChangeRequest / C2G_PropertyBatchChangeRequest 所在命名空间
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

        public async UniTask<IReadOnlyList<BatchChangeResultItem>> SendBatchChangeRequestAsync(
            IReadOnlyList<BatchChangeItem> items, string reason)
        {
#if FANTASY_UNITY
            if (items == null || items.Count == 0) return System.Array.Empty<BatchChangeResultItem>();

            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
            {
                return System.Array.Empty<BatchChangeResultItem>(); // 未连接:整批不发,调用方留待下次边界重报
            }
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
            {
                return System.Array.Empty<BatchChangeResultItem>();
            }

            var protoItems = new List<PropertyChangeItem>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                protoItems.Add(new PropertyChangeItem { Type = (PropertyType)items[i].Type, Delta = items[i].Delta });
            }

            G2C_PropertyBatchChangeResponse response;
            try
            {
                response = await session.C2G_PropertyBatchChangeRequest(protoItems, reason);
            }
            catch
            {
                return System.Array.Empty<BatchChangeResultItem>();
            }

            if (response == null || response.Results == null)
            {
                return System.Array.Empty<BatchChangeResultItem>();
            }

            var results = new List<BatchChangeResultItem>(response.Results.Count);
            for (int i = 0; i < response.Results.Count; i++)
            {
                var r = response.Results[i];
                results.Add(new BatchChangeResultItem((AttrType)r.Type, MapCode(r.ResultCode), r.NewAmount));
            }
            return results;
#else
            await UniTask.CompletedTask;
            return System.Array.Empty<BatchChangeResultItem>();
#endif
        }

#if FANTASY_UNITY
        /// <summary>
        /// 把协议响应转客户端 <see cref="ChangeResult"/>(纯转换,理论可拆出可 EditMode 测,但因含 Fantasy 类型不便 — 留给真往返核 E2/E3)。
        /// NewAmount 在成功 / NotEnough / OverLimit 下是服务端实际余额,其它码下服务端本就回 0,直接透传。
        /// </summary>
        private static ChangeResult MapResponse(G2C_PropertyChangeResponse response)
        {
            var reject = MapCode(response.ResultCode);
            return reject == ChangeReject.None
                ? ChangeResult.Ok(response.NewAmount)
                : ChangeResult.Rejected(reject, response.NewAmount);
        }

        /// <summary>服务端属性变更结果码 → 客户端拒因(单条 / 批量共用;UnknownType 与 InvalidRequest 合并为 TypeUnknown,未知码兜底 ServiceUnavailable)。</summary>
        private static ChangeReject MapCode(PropertyChangeResultCode code)
        {
            switch (code)
            {
                case PropertyChangeResultCode.Success:            return ChangeReject.None;
                case PropertyChangeResultCode.NotEnough:          return ChangeReject.NotEnoughBalance;
                case PropertyChangeResultCode.OverLimit:          return ChangeReject.TypeUpperOverflow;
                case PropertyChangeResultCode.NotLoggedIn:        return ChangeReject.NotLoggedIn;
                case PropertyChangeResultCode.UnknownType:
                case PropertyChangeResultCode.InvalidRequest:     return ChangeReject.TypeUnknown;
                case PropertyChangeResultCode.ServiceUnavailable: return ChangeReject.ServiceUnavailable;
                default:                                          return ChangeReject.ServiceUnavailable;
            }
        }
#endif
    }
}
