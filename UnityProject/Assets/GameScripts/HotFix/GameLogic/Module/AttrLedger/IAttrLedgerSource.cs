using Cysharp.Threading.Tasks;
using GameLogic.BlockBlast.Player;

namespace GameLogic.AttrLedger
{
    /// <summary>
    /// 远程 ledger 数据源接缝(设计 46 §3.2,沿 32 IRemoteMailSource + 38 IRpcGateway 范式)。
    /// 把网络层(<c>FantasyClient.FantasyNetwork.Session</c> + 协议消息)抽到接缝后,
    /// 使 <see cref="RemoteAttrLedgerService"/> 保持纯逻辑可 EditMode 单测(注桩 <c>FakeAttrLedgerSource</c> 验各分支)。
    /// </summary>
    /// <remarks>
    /// 生产实现 = <see cref="RemoteAttrLedgerSource"/>(同目录,#if FANTASY_UNITY 包裹,内部调
    /// <c>Session.C2G_QueryAttrLedger(kind, sinceTs, limit)</c>);测试实现 = 桩(EditMode 单测返预设 <see cref="AttrLedgerPage"/>)。
    /// 返 <see cref="UniTask{T}"/> 而非 Fantasy.Async.FTask:接口面不暴露网络库类型,沿 RemoteMailSource 范式
    /// (memory「跨框架通用异步与网络库异步」)。
    /// 降级语义:所有失败分支(未连接 / 未登录 / RPC 异常 / 服务端 ServiceUnavailable)<b>不抛</b>,
    /// 返结果码不为 Success 的 <see cref="AttrLedgerPage"/>,由调用方据 <see cref="AttrLedgerPage.Code"/> 决定文案兜底。
    /// </remarks>
    public interface IAttrLedgerSource
    {
        /// <summary>
        /// 拉一页 ledger 流水(设计 46 §3.1)。
        /// </summary>
        /// <param name="kind">属性种类过滤(null = 不过滤;非 null = 按 Coin / Diamond / Stamina 过滤)。</param>
        /// <param name="sinceTs">时间下界(只返 Timestamp &gt; sinceTs 的行;0 = 不过滤);本子单首屏传 0 拿最新 N 条。</param>
        /// <param name="limit">单次最多返回行数(服务端钳制 [0, 100],本子单首屏 50 = 设计 46 §4.1 O2)。</param>
        /// <returns>结果包(失败时 <see cref="AttrLedgerPage.Code"/> != Success + <see cref="AttrLedgerPage.Entries"/> 空)。</returns>
        UniTask<AttrLedgerPage> FetchPageAsync(AttrType? kind, long sinceTs, int limit);
    }
}
