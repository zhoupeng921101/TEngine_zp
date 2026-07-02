using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>单条道具持有的框架中立投影(对应协议 <c>Fantasy.ItemHolding</c>:itemId → 权威持有量)。</summary>
    public readonly struct ItemHoldingData
    {
        public readonly int ItemId;
        public readonly long Count;

        public ItemHoldingData(int itemId, long count)
        {
            ItemId = itemId;
            Count = count;
        }
    }

    /// <summary>
    /// 进主游戏一次性原子响应快照(已脱离 Fantasy 对象池,字段可安全跨边界持有)。
    /// 由 <see cref="IEnterMainGameGateway.EnterAsync"/> 把协议 <c>G2C_EnterMainGameResponse</c> 拷成框架中立值类型。
    /// </summary>
    /// <remarks>
    /// 携带:订单当前激活快照(<see cref="OrderSnapshot"/>,喂 <see cref="OrderSync.OnSnapshotPush"/>)
    /// + 道具持有整份快照(<see cref="ItemHoldings"/>,整份覆盖 <c>ItemBag</c> 投影)
    /// + 已合成塔罗牌全集(<see cref="CollectedTarotIds"/>,整份覆盖 <c>TarotCollection</c> 投影)。
    /// 任何往返失败 → 三者均 null(调用方保留已有投影降级,不清空)。
    /// </remarks>
    public readonly struct EnterMainGameResult
    {
        /// <summary>订单当前激活快照(已转框架中立 DTO;服务不可用 / 无订单 → null,调用方按"无订单"降级)。</summary>
        public readonly OrderSnapshotData OrderSnapshot;

        /// <summary>道具持有整份快照(服务不可用 → null,调用方保留本地投影;空数组 = 权威空持有,照常覆盖清空)。</summary>
        public readonly ItemHoldingData[] ItemHoldings;

        /// <summary>已合成塔罗牌 id 全集(服务不可用 → null,调用方保留本地投影;空数组 = 权威空收集,照常覆盖)。</summary>
        public readonly int[] CollectedTarotIds;

        public EnterMainGameResult(OrderSnapshotData orderSnapshot,
            ItemHoldingData[] itemHoldings = null, int[] collectedTarotIds = null)
        {
            OrderSnapshot = orderSnapshot;
            ItemHoldings = itemHoldings;
            CollectedTarotIds = collectedTarotIds;
        }

        /// <summary>服务不可用兜底:无任何快照(调用方保留本地)。</summary>
        public static EnterMainGameResult Unavailable()
            => new EnterMainGameResult(null);
    }

    /// <summary>
    /// 进主游戏请求接缝(全栈协议改动·客户端段)。把网络层(<c>FantasyClient.FantasyNetwork.Session</c> + 协议消息
    /// <c>C2G_EnterMainGameRequest</c> / <c>G2C_EnterMainGameResponse</c>)抽到接缝后,使进主游戏编排(<see cref="EnterMainGameSync"/>)
    /// 保持纯逻辑可 EditMode 单测(沿 <see cref="IOrderRpcGateway"/> 范式)。
    /// </summary>
    /// <remarks>
    /// 生产实现 = <see cref="EnterMainGameGatewayProd"/>(同目录,#if FANTASY_UNITY 包裹);测试实现 = 桩(返预设结果)。
    /// 返 <see cref="UniTask{T}"/> 而非 Fantasy.Async.FTask:接口面不暴露网络库类型。
    /// 任何往返失败(未连 / 未登 / 超时 / 异常)以 <see cref="EnterMainGameResult.Unavailable"/> 归一,<b>不抛异常</b>。
    /// </remarks>
    public interface IEnterMainGameGateway
    {
        /// <summary>发一次进主游戏请求,取服务端一次性原子响应(订单快照)。身份从会话取,空请求。</summary>
        UniTask<EnterMainGameResult> EnterAsync();
    }
}
