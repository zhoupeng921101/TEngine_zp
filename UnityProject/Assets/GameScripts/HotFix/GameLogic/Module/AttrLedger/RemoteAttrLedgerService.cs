using System;
using Cysharp.Threading.Tasks;
using GameLogic.BlockBlast.Player;

namespace GameLogic.AttrLedger
{
    /// <summary>
    /// 远程 ledger 服务(编排层,设计 46 §3.3,沿 32 RemoteMailService 范式)。
    /// 持 <see cref="IAttrLedgerSource"/> 接缝,提供 <see cref="FetchPageAsync"/> 转发 + 错误码归一。
    /// </summary>
    /// <remarks>
    /// 本子单当前与接缝层 1:1 转发,但保留服务层便于 Tier 2+ 加交叉编排 / 缓存策略 / 重试。
    /// <b>不持本地状态</b>(无缓存、无副本):每次调真请求,守 44 §5.4 服务端独占审计完整性
    /// (客户端不应有第二份 ledger,沿设计 46 §3.3 关键约束 + PV14 ⑤)。
    /// 异步:经一次 RPC 往返,方法 <c>async UniTask</c>(框架通用异步类型,与网络层 FTask 解耦);不阻塞、不抛异常
    /// (失败返对应 <see cref="AttrLedgerQueryCode"/>)。
    /// </remarks>
    public sealed class RemoteAttrLedgerService
    {
        private readonly IAttrLedgerSource _source;

        public RemoteAttrLedgerService(IAttrLedgerSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>
        /// 拉一页 ledger 流水(设计 46 §3.3 API):
        /// 转发到注入的 <see cref="IAttrLedgerSource"/>;失败时返结构化结果码,不抛。
        /// </summary>
        /// <param name="kind">属性种类过滤(null = 不过滤,UI 全部 tab 传此);非 null = 按 Coin / Diamond / Stamina 过滤。</param>
        /// <param name="sinceTs">时间下界(只返 Timestamp &gt; sinceTs 的行;0 = 不过滤;本子单首屏 0)。</param>
        /// <param name="limit">单次最多返回行数(客户端不钳制,服务端 45 §3.1 钳制上限 100;本子单首屏 50)。</param>
        public UniTask<AttrLedgerPage> FetchPageAsync(AttrType? kind, long sinceTs, int limit)
        {
            return _source.FetchPageAsync(kind, sinceTs, limit);
        }
    }
}
