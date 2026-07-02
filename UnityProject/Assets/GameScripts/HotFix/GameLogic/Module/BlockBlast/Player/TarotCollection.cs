using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameLogic.BlockBlast.Item;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 塔罗牌合成裁决结果码(框架中立,映射服务端 <c>Fantasy.TarotSynthesizeResultCode</c> + 客户端网络层失败码)。
    /// </summary>
    public enum TarotSynthCode
    {
        /// <summary>成功:服务端已扣碎片、置已合成,回带权威碎片余额 + 收集全集。</summary>
        Success = 0,
        /// <summary>会话未挂账号(登录链路异常)→ 客户端重登。</summary>
        NotLoggedIn = 1,
        /// <summary>牌 id 不在 TbTarotCard 表。</summary>
        UnknownCard = 2,
        /// <summary>该牌已合成(服务端幂等拒绝,不扣碎片)。</summary>
        AlreadyCollected = 3,
        /// <summary>碎片不足。</summary>
        NotEnoughFragments = 4,
        /// <summary>服务不可用(MongoDB 不可达 / 配置缺失)。</summary>
        ServiceUnavailable = 5,
        /// <summary>网络断 / 超时 / RPC 异常(客户端层失败,服务端无对应码)。</summary>
        NetworkDown = 6,
    }

    /// <summary>
    /// 合成裁决结果(框架中立,对应协议 <c>G2C_TarotSynthesizeResponse</c>)。
    /// <see cref="FragmentBalance"/> = 扣减后该碎片权威持有量(-1 哨兵 = 未取到权威值,不据此 set);
    /// <see cref="CollectedIds"/> = 已合成牌 id 权威全集(整份覆盖;null = 本次未取到,保留本地投影)。
    /// </summary>
    public readonly struct TarotSynthesizeResult
    {
        public readonly TarotSynthCode Code;
        public readonly int CardId;
        public readonly int FragmentItemId;
        public readonly long FragmentBalance;
        public readonly int[] CollectedIds;

        public TarotSynthesizeResult(TarotSynthCode code, int cardId,
            int fragmentItemId = 0, long fragmentBalance = -1L, int[] collectedIds = null)
        {
            Code = code;
            CardId = cardId;
            FragmentItemId = fragmentItemId;
            FragmentBalance = fragmentBalance;
            CollectedIds = collectedIds;
        }
    }

    /// <summary>
    /// 塔罗合成 RPC 接缝(塔罗收集·客户端段)。把网络层(<c>FantasyClient.FantasyNetwork.Session</c> +
    /// <c>C2G_TarotSynthesizeRequest</c>)抽到接缝后,使 <see cref="TarotCollection"/> 保持纯逻辑、
    /// 可 EditMode 单测(沿 <see cref="IOrderRpcGateway"/> 范式)。
    /// </summary>
    /// <remarks>
    /// 生产实现 = <c>TarotRpcGatewayProd</c>(同目录,#if FANTASY_UNITY 包裹);测试实现 = 桩(返预设结果)。
    /// 任何往返失败(未连 / 未登 / 超时 / 异常)以 <see cref="TarotSynthCode.NetworkDown"/> /
    /// <see cref="TarotSynthCode.ServiceUnavailable"/> 归一,<b>不抛异常</b>。
    /// </remarks>
    public interface ITarotRpcGateway
    {
        /// <summary>发起合成请求(仅带牌 id,身份从会话取,碎片消耗由服务端按表自算)。失败以失败码返、不抛。</summary>
        UniTask<TarotSynthesizeResult> SynthesizeAsync(int cardId);
    }

    /// <summary>
    /// 塔罗牌收集投影(塔罗收集系统·客户端段,与既有 TarotBlindBox 盲盒是两个系统)。
    ///
    /// 服务端是唯一事实源(PlayerDoc.CollectedTarotIds + ItemHoldings 碎片持有),本类只持投影:
    /// 进主游戏响应整份覆盖已合成集合(经 <see cref="ApplyCollectedSnapshot"/>);
    /// 合成经 RPC 裁决(<see cref="SynthesizeAsync"/>),响应回带的权威全集 + 碎片余额落投影与背包。
    /// 碎片进度不在本类冗余存放——由 UI 用「牌 → 碎片 id(TbTarotCard 表)」查注入的 <see cref="ItemBag"/> 现算。
    ///
    /// 纯逻辑(经注入的 <see cref="ITarotRpcGateway"/> 发请求),可 EditMode 单测(沿 <see cref="OrderSync"/> 范式)。
    /// </summary>
    public sealed class TarotCollection
    {
        private readonly ITarotRpcGateway _gateway;
        private readonly ItemBag _bag;
        private readonly HashSet<int> _collected = new HashSet<int>();

        /// <summary>已合成塔罗牌 id 投影(只读;来源 = 进主游戏快照 / 合成响应整份覆盖)。</summary>
        public IReadOnlyCollection<int> CollectedIds => _collected;

        /// <param name="gateway">合成 RPC 接缝(必填)。</param>
        /// <param name="bag">背包投影(可空;非空时合成响应的碎片权威余额一并对齐到背包)。</param>
        public TarotCollection(ITarotRpcGateway gateway, ItemBag bag = null)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _bag = bag;
        }

        /// <summary>该牌是否已合成(投影)。</summary>
        public bool IsCollected(int cardId) => _collected.Contains(cardId);

        /// <summary>
        /// 用服务端权威全集整份覆盖已合成集合(进主游戏响应 / 合成响应回带)。ids 为 null → 不动(未取到权威值,保留投影)。
        /// </summary>
        public void ApplyCollectedSnapshot(IEnumerable<int> ids)
        {
            if (ids == null) return;
            _collected.Clear();
            foreach (var id in ids)
            {
                if (id > 0) _collected.Add(id);
            }
        }

        /// <summary>
        /// 发起合成(UI 合成按钮调)。响应落投影:回带的权威全集非 null 时整份覆盖已合成集合
        /// (读到玩家文档的结果码——Success/AlreadyCollected/NotEnoughFragments——携带权威态,供本地对齐,
        /// 如 AlreadyCollected 时把漏掉的牌补进投影;null = 降级路径未取到,保留既有投影);
        /// 碎片余额 ≥ 0 时对齐背包计数(-1 哨兵不 set)。返回结果交 UI 按码表现。
        /// </summary>
        public async UniTask<TarotSynthesizeResult> SynthesizeAsync(int cardId)
        {
            var result = await _gateway.SynthesizeAsync(cardId);

            ApplyCollectedSnapshot(result.CollectedIds);
            if (_bag != null && result.FragmentItemId > 0 && result.FragmentBalance >= 0)
            {
                _bag.SetAuthoritativeCount(result.FragmentItemId, result.FragmentBalance);
            }
            return result;
        }
    }
}
