using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 属性变更 RPC 接缝(设计 38 §四)。把网络层(<c>FantasyClient.FantasyNetwork.Session</c> + 协议消息)
    /// 抽到接缝后,使 <see cref="PlayerAttrService"/> 保持纯逻辑可 EditMode 单测。
    /// </summary>
    /// <remarks>
    /// 生产实现 = <c>RpcGatewayProd</c>(同目录,#if FANTASY_UNITY 包裹,内部调 Session.C2G_PropertyChangeRequest);
    /// 测试实现 = 桩(EditMode 单测里返预设 <see cref="ChangeResult"/>,记录调用次数与参数)。
    /// 返 <see cref="UniTask{T}"/> 而非 Fantasy.Async.FTask:接口面不暴露网络库类型,沿 RemoteRankSource 范式(memory「跨框架通用异步与网络库异步」)。
    /// </remarks>
    public interface IRpcGateway
    {
        /// <summary>
        /// 发起属性变更请求。请求负载仅含 type + delta + reason 三字段(沿 37 反作弊红线:客户端绝对值禁报)。
        /// 失败时(网络断 / 超时 / 异常)以 <see cref="ChangeResult.Rejected"/>(NetworkDown / ServiceUnavailable)返,
        /// <b>不抛异常</b>(沿 RemoteRankSource 范式)。
        /// </summary>
        UniTask<ChangeResult> SendChangeRequestAsync(AttrType type, long delta, string reason);
    }
}
