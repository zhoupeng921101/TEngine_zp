using System;
using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 女神满档领取服务端权威编排器(女神系统·客户端段)。玩法入口(领取按钮)调 <see cref="ClaimAsync"/>:
    /// 经 <see cref="IGoddessClaimGateway"/> 发 <c>C2G_GoddessClaimRequest</c> → 等响应 → 成功时把奖励元素逐档
    /// <see cref="MergeOrderState.AddDirect"/> 入合成区 + 经 <see cref="MetaCurrencySync.ApplyDeltaPush"/> 对账
    /// GoddessRating=0(value + base),返回 <see cref="GoddessClaimResult"/> 供 UI 展示。
    /// </summary>
    /// <remarks>
    /// 【非乐观,等响应】领取低频、须服务端裁定满档(防未满领 / 重复领),不做客户端乐观预领:本地不预先清零 / 发奖,
    /// 一切以服务端响应为准。奖励元素落客户端合成区(局内 blob),由本地 <see cref="MergeOrderState.AddDirect"/> 执行——
    /// 服务端只裁定「能否领 + 发什么」,元素入库是客户端低危局内动作(同全清奖 AddDirect 落点口径)。
    ///
    /// 【对账】成功后 GoddessRating 经 <see cref="MetaCurrencySync.ApplyDeltaPush"/>(0) set value + base = 0,
    /// 与服务端 delta-push 同口径、幂等(服务端领取后亦推 GoddessRating=0,重复 set 无害);避免下次
    /// <see cref="MetaCurrencySync.ReportPending"/> 把「本地已 0、基线仍满档」算成负 delta 误报。
    ///
    /// 纯逻辑(不依赖 UnityEngine):经注入 <see cref="IGoddessClaimGateway"/> + <see cref="MetaCurrencySync"/>,可 EditMode 单测。
    /// </remarks>
    public sealed class GoddessClaimService
    {
        private readonly IGoddessClaimGateway _gateway;
        private readonly MetaCurrencySync _currency;

        /// <summary>一次领取往返进行中标志:防重入(领取按钮连点),在途时直接返回占位结果、不重复发。</summary>
        private bool _inFlight;

        public GoddessClaimService(IGoddessClaimGateway gateway, MetaCurrencySync currency)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _currency = currency; // 允许 null(无对账器时仅 set state 字段,不落基线)
        }

        /// <summary>
        /// 发起一次领取并按响应应用(<paramref name="state"/> 当前玩法态,可为 null)。返回 <see cref="GoddessClaimResult"/> 供 UI。
        /// 在途重入直接返 ServiceUnavailable 占位(不重复发请求)。
        /// </summary>
        public async UniTask<GoddessClaimResult> ClaimAsync(MergeOrderState state)
        {
            if (_inFlight) return GoddessClaimResult.Rejected(GoddessClaimOutcome.ServiceUnavailable);
            _inFlight = true;
            try
            {
                var result = await _gateway.ClaimAsync();
                ApplyAuthoritative(state, result);
                return result;
            }
            finally
            {
                _inFlight = false;
            }
        }

        /// <summary>
        /// 成功时把奖励元素逐档入合成区 + 对账 GoddessRating=0;非成功码不动 state(响应不可信 / 未满档,本地不清零、不发奖)。
        /// 抽独立方法便于单测直接喂构造结果断言应用逻辑,不必真跑 RPC。
        /// </summary>
        public void ApplyAuthoritative(MergeOrderState state, GoddessClaimResult result)
        {
            if (result.Outcome != GoddessClaimOutcome.Success) return;

            if (state != null)
            {
                var type = (MergeElement)result.ElementType;
                var rewards = result.Rewards;
                if (rewards != null)
                {
                    for (int i = 0; i < rewards.Count; i++)
                    {
                        // 元素入合成区(走级联合并,同全清奖 AddDirect 口径)。ElementType 非法(0/越界)时 AddDirect 内部 no-op。
                        state.AddDirect(type, rewards[i].Level, rewards[i].Count);
                    }
                }
            }

            // GoddessRating 清零对账:value + base 同置 0(与登录快照 / delta-push 同口径,幂等)。
            if (_currency != null)
            {
                _currency.ApplyDeltaPush(state, AttrType.GoddessRating, 0);
            }
            else if (state != null)
            {
                state.GoddessRating = 0;
            }
        }
    }
}
