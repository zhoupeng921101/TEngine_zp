using System.Collections.Generic;
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

        /// <summary>
        /// 批量发起属性变更:一次 RPC 携带多项 (type, delta),服务端逐项独立裁决(非全或无)。
        /// 用于一次玩法事件同时改多个属性(如全清同时改女神 / 盲盒 / 货币),把 N 条单发收敛成 1 条往返。
        /// 返回逐项结果(调用方按 <see cref="BatchChangeResultItem.Type"/> 匹配);整批失败(网络断 / 服务不可用 /
        /// 未登录 / 空入参)返空列表——调用方对未返回项不动视图 / 基线,下次边界重报(沿单条链路失败语义)。<b>不抛异常</b>。
        /// </summary>
        UniTask<IReadOnlyList<BatchChangeResultItem>> SendBatchChangeRequestAsync(
            IReadOnlyList<BatchChangeItem> items, string reason);
    }
}
