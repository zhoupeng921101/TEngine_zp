using System;
using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 祈愿(每日限领体力)服务端权威编排器(祈愿服务端权威·客户端段)。玩法入口(祈愿按钮)调 <see cref="WishAsync"/>:
    /// 经 <see cref="IWishGateway"/> 发 <c>C2G_WishForEnergyRequest</c> → 等响应 → 用回带的权威 SoulPower/Energy/WishUsedToday
    /// <b>应用为权威</b>,返回 <see cref="WishRpcResult"/> 供 UI 分支提示。
    /// </summary>
    /// <remarks>
    /// 【非乐观,等响应】祈愿每日限领(默认 3 次)、低频,不做客户端乐观预扣:本地不产生 Soul/Energy 净变化,
    /// <see cref="MetaCurrencySync"/> 的「当前值 - 基线」净 delta 落盘边界自然看不到这笔,免去双扣灵力 / 双减体力
    /// (无需 exclude-optimistic-spend 那套「扣减瞬间抬基线」——那是针对客户端确有本地乐观扣的场景)。
    ///
    /// 【应用为权威】成功时把服务端回带的 Soul/Energy 经 <see cref="MetaCurrencySync.ApplyDeltaPush"/> set 本地字段 + 基线
    /// (绝对值覆盖、幂等,与登录快照 / delta-push 同口径);WishUsedToday 直接 set 到 <see cref="MergeOrderState.WishUsedToday"/>
    /// (投影,非本地累加)。DailyLimitReached / NotEnoughSoul 回带了服务端当前权威值也一并对齐,防两端漂移。
    /// NetworkDown / ServiceUnavailable / NotLoggedIn 不动投影(响应不可信),仅供 UI 提示重试 / 重登。
    ///
    /// 纯逻辑(不依赖 UnityEngine):经注入的 <see cref="IWishGateway"/> 发请求、经注入的 <see cref="MetaCurrencySync"/>
    /// 落权威值,故可 EditMode 单测(注桩 gateway + 真实/桩 MetaCurrencySync)。
    /// </remarks>
    public sealed class WishService
    {
        private readonly IWishGateway _gateway;
        private readonly MetaCurrencySync _currency;

        /// <summary>一次祈愿往返进行中标志:防重入(玩法按钮连点),在途时直接返回上次「进行中」占位结果。</summary>
        private bool _inFlight;

        public WishService(IWishGateway gateway, MetaCurrencySync currency)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _currency = currency; // 允许 null(无对账器时仅 set state 投影,不落基线)
        }

        /// <summary>
        /// 发起一次祈愿并按响应把权威值应用到投影(<paramref name="state"/> 为当前玩法态,可为 null 仅走对账器基线)。
        /// 返回 <see cref="WishRpcResult"/>:Success 已对齐 Soul/Energy/WishUsedToday;拒绝码供 UI 提示。
        /// 在途重入直接返 ServiceUnavailable 占位(不重复发请求、不叠 delta)。
        /// </summary>
        public async UniTask<WishRpcResult> WishAsync(MergeOrderState state)
        {
            if (_inFlight) return WishRpcResult.Rejected(WishOutcome.ServiceUnavailable);
            _inFlight = true;
            try
            {
                var result = await _gateway.WishAsync();
                ApplyAuthoritative(state, result);
                return result;
            }
            finally
            {
                _inFlight = false;
            }
        }

        /// <summary>
        /// 把响应回带的权威值应用到投影(仅在响应可信的码下):Soul/Energy 经对账器 <see cref="MetaCurrencySync.ApplyDeltaPush"/>
        /// set 字段 + 基线(绝对值覆盖);WishUsedToday set 到 state 投影。响应不可信码(NetworkDown/ServiceUnavailable/NotLoggedIn)
        /// 不动投影。抽独立方法便于单测直接喂构造结果断言应用逻辑,不必真跑 RPC。
        /// </summary>
        public void ApplyAuthoritative(MergeOrderState state, WishRpcResult result)
        {
            // 仅这三码回带了服务端当前权威 Soul/Energy/WishUsedToday(见 WishGatewayProd.MapResponse):可信,对齐投影。
            bool trustworthy = result.Outcome == WishOutcome.Success
                               || result.Outcome == WishOutcome.DailyLimitReached
                               || result.Outcome == WishOutcome.NotEnoughSoul;
            if (!trustworthy) return;

            // Soul/Energy:经对账器 set 本地字段 + 基线(state 为 null 时对账器只 set 基线)。绝对值覆盖、幂等。
            if (_currency != null)
            {
                _currency.ApplyDeltaPush(state, AttrType.SoulPower, result.SoulPower);
                _currency.ApplyDeltaPush(state, AttrType.Energy, result.Energy);
            }
            else if (state != null)
            {
                // 无对账器(不接网络的降级/单测):直接 set 字段投影,不涉基线。
                state.Soul = (int)result.SoulPower;
                state.Energy = (int)result.Energy;
            }

            // 今日已用祈愿次数:投影,直接 set(非本地累加;跨天重置由服务端做,客户端只读)。
            if (state != null) state.WishUsedToday = result.WishUsedToday;
        }
    }
}
