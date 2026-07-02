using System.Collections.Generic;
using Cysharp.Threading.Tasks;
#if FANTASY_UNITY
using Fantasy; // 协议消息 + NetworkProtocolHelper 扩展方法 C2G_DeliverOrderRequest 所在命名空间
#endif

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 订单交付 RPC 接缝生产实现(P1 全栈迁移·客户端段 + §五接线)。经 <c>FantasyClient.FantasyNetwork.Session</c>
    /// 发 <c>C2G_DeliverOrderRequest</c> 同步等响应,把服务端 <c>DeliverOrderResultCode</c> + <c>MergeOrderSnapshot</c>
    /// 转框架中立的 <see cref="OrderDeliverResult"/>(沿 <see cref="RpcGatewayProd"/> 范式)。
    /// </summary>
    /// <remarks>
    /// 降级(沿 RemoteRankSource / RpcGatewayProd 范式):未连接 / 未登录 / 发不出 / 超时 / 任何往返失败 →
    /// 返 <see cref="DeliverCode.NetworkDown"/> 或 <see cref="DeliverCode.ServiceUnavailable"/>,<b>不抛异常</b>。
    /// 程序集边界:网络层 FantasyClient / Fantasy.Unity 受 FANTASY_UNITY 约束;该 define 关闭的平台无网络可用,
    /// 本类同样降级返 ServiceUnavailable,使 GameLogic 在任何平台都可编译。
    /// </remarks>
    public sealed class OrderRpcGatewayProd : IOrderRpcGateway
    {
        public async UniTask<OrderDeliverResult> DeliverAsync(int slot)
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
            {
                return new OrderDeliverResult(DeliverCode.NetworkDown, null); // 未连接:不发请求、不抛
            }
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
            {
                return new OrderDeliverResult(DeliverCode.NotLoggedIn, null);
            }

            G2C_DeliverOrderResponse response;
            try
            {
                // FTask 自带 awaiter,可在 async UniTask 体内直接 await(memory「跨框架通用异步与网络库异步」)
                response = await session.C2G_DeliverOrderRequest(slot);
            }
            catch
            {
                return new OrderDeliverResult(DeliverCode.ServiceUnavailable, null);
            }

            if (response == null)
            {
                return new OrderDeliverResult(DeliverCode.ServiceUnavailable, null);
            }

            // EnergyBalance / PietyBalance / FragmentBalance:交付后权威绝对余额(-1 哨兵时客户端不据此 set),
            // 供 OrderSync 对账应用体力/虔诚币/背包碎片计数。
            return new OrderDeliverResult(MapCode(response.ResultCode), ToSnapshot(response.Snapshot),
                response.EnergyBalance, response.PietyBalance,
                response.FragmentItemId, response.FragmentReward, response.FragmentBalance);
#else
            // FANTASY_UNITY 关闭(无网络平台):降级为服务不可用,不抛
            await UniTask.CompletedTask;
            return new OrderDeliverResult(DeliverCode.ServiceUnavailable, null);
#endif
        }

#if FANTASY_UNITY
        /// <summary>把协议裁决码转框架中立 <see cref="DeliverCode"/>(枚举值一一对齐,未知码兜底 ServiceUnavailable)。</summary>
        private static DeliverCode MapCode(DeliverOrderResultCode code)
        {
            switch (code)
            {
                case DeliverOrderResultCode.Success:            return DeliverCode.Success;
                case DeliverOrderResultCode.NotLoggedIn:        return DeliverCode.NotLoggedIn;
                case DeliverOrderResultCode.InvalidSlot:        return DeliverCode.InvalidSlot;
                case DeliverOrderResultCode.AlreadyDelivered:   return DeliverCode.AlreadyDelivered;
                case DeliverOrderResultCode.ServiceUnavailable: return DeliverCode.ServiceUnavailable;
                default:                                        return DeliverCode.ServiceUnavailable;
            }
        }

        /// <summary>
        /// 把协议 <c>MergeOrderSnapshot</c> 拷成框架中立 <see cref="OrderSnapshotData"/>(独立副本,
        /// 协议对象用完即回池,跨边界须复制避免引用悬空)。snapshot 为 null → 返 null(交逻辑层不应用)。
        /// </summary>
        internal static OrderSnapshotData ToSnapshot(MergeOrderSnapshot snapshot)
        {
            if (snapshot == null) return null;
            var orders = new List<OrderItemData>();
            if (snapshot.ActiveOrders != null)
            {
                foreach (var it in snapshot.ActiveOrders)
                {
                    if (it == null) continue;
                    orders.Add(new OrderItemData(it.Type, it.Level, it.Count,
                        it.EnergyReward, it.PietyReward, it.FragmentItemId, it.FragmentCount));
                }
            }
            return new OrderSnapshotData(orders, snapshot.OrderCursor,
                snapshot.LastOrderRefreshMs, snapshot.OrderRefreshIntervalSec);
        }
#endif
    }
}
