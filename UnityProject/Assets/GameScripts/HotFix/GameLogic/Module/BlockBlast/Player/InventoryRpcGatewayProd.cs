using Cysharp.Threading.Tasks;
#if FANTASY_UNITY
using Fantasy; // 协议消息 + 扩展方法 C2G_UseItem 所在命名空间
#endif

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 使用道具 RPC 接缝生产实现(背包系统·客户端段)。经 <c>FantasyClient.FantasyNetwork.Session</c>
    /// 发 <c>C2G_UseItem</c> 同步等响应,把服务端 <c>UseItemResultCode</c> + 产出转框架中立 <see cref="UseItemResult"/>
    /// (沿 <see cref="TarotRpcGatewayProd"/> 范式)。
    /// </summary>
    /// <remarks>
    /// 降级:未连接 / 未登录 / 发不出 / 超时 / 任何往返失败 → <see cref="UseItemCode.NetworkDown"/> /
    /// <see cref="UseItemCode.ServiceUnavailable"/>,<b>不抛异常</b>。
    /// FANTASY_UNITY 关闭(无网络平台):同样降级,使 GameLogic 任何平台可编译。
    /// 跨边界复制:协议响应对象用完即回池,产出值即时读出为值类型再返回。
    /// </remarks>
    public sealed class InventoryRpcGatewayProd : IInventoryRpcGateway
    {
        public async UniTask<UseItemResult> SendUseItemAsync(int itemId, long count, long reqSeq)
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
            {
                return UseItemResult.Fail(UseItemCode.NetworkDown, itemId);
            }
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
            {
                return UseItemResult.Fail(UseItemCode.NotLoggedIn, itemId);
            }

            G2C_UseItemResponse response;
            try
            {
                response = await session.C2G_UseItem(itemId, count, reqSeq);
            }
            catch
            {
                return UseItemResult.Fail(UseItemCode.ServiceUnavailable, itemId);
            }
            if (response == null)
            {
                return UseItemResult.Fail(UseItemCode.ServiceUnavailable, itemId);
            }

            var code = MapCode(response.ResultCode);
            if (code != UseItemCode.Success)
            {
                return UseItemResult.Fail(code, itemId);
            }

            // 产出(本轮效果只有货币):取第一项 PropertyAmount → AttrType(枚举值与 PropertyType 1:1)。
            bool hasProduce = false;
            AttrType producedType = default;
            long producedAmount = 0;
            if (response.Produced != null && response.Produced.Count > 0)
            {
                var p = response.Produced[0];
                if (p != null)
                {
                    hasProduce = true;
                    producedType = (AttrType)(int)p.Type;
                    producedAmount = p.Amount;
                }
            }
            return new UseItemResult(UseItemCode.Success, response.ItemId, response.ConsumedCount,
                hasProduce, producedType, producedAmount);
#else
            await UniTask.CompletedTask;
            return UseItemResult.Fail(UseItemCode.ServiceUnavailable, itemId);
#endif
        }

#if FANTASY_UNITY
        /// <summary>协议裁决码 → 框架中立 <see cref="UseItemCode"/>(未知码兜底 ServiceUnavailable)。</summary>
        private static UseItemCode MapCode(UseItemResultCode code)
        {
            switch (code)
            {
                case UseItemResultCode.Success:            return UseItemCode.Success;
                case UseItemResultCode.NotLoggedIn:        return UseItemCode.NotLoggedIn;
                case UseItemResultCode.UnknownItem:        return UseItemCode.UnknownItem;
                case UseItemResultCode.NotEnough:          return UseItemCode.NotEnough;
                case UseItemResultCode.Expired:            return UseItemCode.Expired;
                case UseItemResultCode.NotUsable:          return UseItemCode.NotUsable;
                case UseItemResultCode.Duplicate:          return UseItemCode.Duplicate;
                case UseItemResultCode.InvalidRequest:     return UseItemCode.InvalidRequest;
                case UseItemResultCode.ServiceUnavailable: return UseItemCode.ServiceUnavailable;
                default:                                   return UseItemCode.ServiceUnavailable;
            }
        }
#endif
    }
}
