using Cysharp.Threading.Tasks;

namespace GameLogic.Activity
{
    /// <summary>
    /// 活动累加数据源接缝(设计 48 §3.2,沿 46 <c>IAttrLedgerSource</c> + 38 <c>IRpcGateway</c> 范式)。
    /// 把网络层(<c>FantasyClient.FantasyNetwork.Session</c> + 协议消息)抽到接缝后,
    /// 使 <see cref="RemoteActivityService"/> 保持纯逻辑可 EditMode 单测(注桩 <c>FakeActivityIncrementSource</c>)。
    /// </summary>
    /// <remarks>
    /// 生产实现 = <see cref="RemoteActivityIncrementSource"/>(同目录,#if FANTASY_UNITY 包裹,内部调
    /// <c>session.C2G_ActivityIncrement(activityId, delta)</c>);测试实现 = 桩。
    /// 返 <see cref="UniTask{T}"/> 而非 Fantasy.Async.FTask:接口面不暴露网络库类型,沿 46 / 32 范式
    /// (memory「跨框架通用异步与网络库异步」)。
    /// 降级语义:所有失败分支(未连接 / 未登录 / RPC 异常 / 服务端 ServiceUnavailable)<b>不抛</b>,
    /// 返结果码不为 Success 的 <see cref="ActivityIncrementResult"/>,由调用方据 Code 落日志(沿设计 48 §3.8)。
    /// </remarks>
    public interface IActivityIncrementSource
    {
        /// <summary>
        /// 累加一次活动 counter(设计 48 §3.2)。
        /// </summary>
        /// <param name="activityId">目标活动 id(沿设计 39 §3.1 activity.xlsx.activity_id;不存在返 InvalidRequest)。</param>
        /// <param name="delta">本次累加增量(必须 > 0;服务端 47 §3.5 钳制上限 10000;客户端不前置钳制)。</param>
        /// <returns>结构化结果(失败时 <see cref="ActivityIncrementResult.Code"/> != Success,counter/达标位为默认值)。</returns>
        UniTask<ActivityIncrementResult> IncrementAsync(int activityId, int delta);
    }
}
