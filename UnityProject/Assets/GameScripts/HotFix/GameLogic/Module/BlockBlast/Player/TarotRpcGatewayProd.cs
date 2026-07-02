using Cysharp.Threading.Tasks;
#if FANTASY_UNITY
using Fantasy; // 协议消息 + NetworkProtocolHelper 扩展方法 C2G_TarotSynthesizeRequest 所在命名空间
#endif

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 塔罗合成 RPC 接缝生产实现(塔罗收集·客户端段)。经 <c>FantasyClient.FantasyNetwork.Session</c>
    /// 发 <c>C2G_TarotSynthesizeRequest</c> 同步等响应,把服务端 <c>TarotSynthesizeResultCode</c> +
    /// 权威全集/碎片余额转框架中立的 <see cref="TarotSynthesizeResult"/>(沿 <see cref="OrderRpcGatewayProd"/> 范式)。
    /// </summary>
    /// <remarks>
    /// 降级:未连接 / 未登录 / 发不出 / 超时 / 任何往返失败 → <see cref="TarotSynthCode.NetworkDown"/> 或
    /// <see cref="TarotSynthCode.ServiceUnavailable"/>,<b>不抛异常</b>。
    /// 程序集边界:网络层受 FANTASY_UNITY 约束;该 define 关闭的平台无网络,本类同样降级,使 GameLogic 任何平台可编译。
    /// 跨边界复制:协议响应对象用完即回池,CollectedTarotIds 拷成独立数组再返回,避免引用悬空。
    /// </remarks>
    public sealed class TarotRpcGatewayProd : ITarotRpcGateway
    {
        public async UniTask<TarotSynthesizeResult> SynthesizeAsync(int cardId)
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
            {
                return new TarotSynthesizeResult(TarotSynthCode.NetworkDown, cardId);
            }
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
            {
                return new TarotSynthesizeResult(TarotSynthCode.NotLoggedIn, cardId);
            }

            G2C_TarotSynthesizeResponse response;
            try
            {
                response = await session.C2G_TarotSynthesizeRequest(cardId);
            }
            catch
            {
                return new TarotSynthesizeResult(TarotSynthCode.ServiceUnavailable, cardId);
            }
            if (response == null)
            {
                return new TarotSynthesizeResult(TarotSynthCode.ServiceUnavailable, cardId);
            }

            // 立即复制出独立数组(协议对象随 await 后回池)。
            // CollectedValid=false = 服务端降级路径的空占位(未读到玩家文档)→ 转 null,
            // TarotCollection.ApplyCollectedSnapshot 对 null 保留既有投影(空数组才是权威空集,照常覆盖)。
            int[] collected = response.CollectedValid
                ? (response.CollectedTarotIds != null ? response.CollectedTarotIds.ToArray() : System.Array.Empty<int>())
                : null;
            return new TarotSynthesizeResult(MapCode(response.ResultCode), response.CardId,
                response.FragmentItemId, response.FragmentBalance, collected);
#else
            // FANTASY_UNITY 关闭(无网络平台):降级为服务不可用,不抛
            await UniTask.CompletedTask;
            return new TarotSynthesizeResult(TarotSynthCode.ServiceUnavailable, cardId);
#endif
        }

#if FANTASY_UNITY
        /// <summary>把协议裁决码转框架中立 <see cref="TarotSynthCode"/>(枚举值一一对齐,未知码兜底 ServiceUnavailable)。</summary>
        private static TarotSynthCode MapCode(TarotSynthesizeResultCode code)
        {
            switch (code)
            {
                case TarotSynthesizeResultCode.Success:            return TarotSynthCode.Success;
                case TarotSynthesizeResultCode.NotLoggedIn:        return TarotSynthCode.NotLoggedIn;
                case TarotSynthesizeResultCode.UnknownCard:        return TarotSynthCode.UnknownCard;
                case TarotSynthesizeResultCode.AlreadyCollected:   return TarotSynthCode.AlreadyCollected;
                case TarotSynthesizeResultCode.NotEnoughFragments: return TarotSynthCode.NotEnoughFragments;
                case TarotSynthesizeResultCode.ServiceUnavailable: return TarotSynthCode.ServiceUnavailable;
                default:                                           return TarotSynthCode.ServiceUnavailable;
            }
        }
#endif
    }
}
