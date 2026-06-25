using System.Collections.Generic;
using Cysharp.Threading.Tasks;
#if FANTASY_UNITY
using Fantasy; // 协议消息类型 + NetworkProtocolHelper 扩展方法 C2G_RedeemCodeRequest(this Session, string) 所在命名空间
#endif

namespace GameLogic.Redeem
{
    /// <summary>
    /// 远程兑换裁决器（设计 30 §一/§三）：经联网会话发 <c>C2G_RedeemCodeRequest</c>、await
    /// <c>G2C_RedeemCodeResponse</c> 裁决,把服务端 <c>RedeemResultCode</c> + 奖励列表映射成 <see cref="ValidationResult"/>。
    /// 这是<b>唯一</b>生产裁决器——客户端不再持本地码表 / 本地去重(设计 30 §六,本地权威路径已移除)。
    /// </summary>
    /// <remarks>
    /// 身份从会话取,请求<b>不</b>携带账号 id(设计 30 §3.1 / CV8)——协议 <c>C2G_RedeemCodeRequest</c> 只有 Code 字段。
    /// 异步红线:接口返框架通用 <c>UniTask</c>;网络往返用 Fantasy <c>FTask</c>,二者经 await 桥接(FTask 自带 awaiter),
    /// 不阻塞主线程(Scene 为 MainThread 模式,延续在主线程,无跨线程问题)。
    /// 降级(设计 30 §四 / CV3/CV4):未连接 / 发不出 / 超时 / 任何往返失败 → 返
    /// <see cref="RedeemVerdict.ServiceUnavailable"/>(<b>不抛异常</b>),由服务层提示重试、码保持可兑;<b>绝不本地放行</b>。
    /// 程序集边界:网络层 <c>FantasyClient</c> / <c>Fantasy.Unity</c> 受 <c>FANTASY_UNITY</c> 约束;
    /// 该 define 关闭的平台无网络可用,本类同样降级为 ServiceUnavailable,使 GameLogic 在任何平台都可编译。
    /// </remarks>
    public sealed class RemoteRedeemValidator : IRedeemValidator
    {
        public async UniTask<ValidationResult> ValidateAsync(string code)
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
            {
                // 未连接:不发请求、不本地放行,直接降级。
                return ValidationResult.Fail(RedeemVerdict.ServiceUnavailable);
            }

            Fantasy.G2C_RedeemCodeResponse response;
            try
            {
                // 发 RPC 并 await 回包:FTask 自带 awaiter,可在 async UniTask 体内直接 await。
                response = await session.C2G_RedeemCodeRequest(code);
            }
            catch
            {
                // 发不出 / 超时 / 往返异常:降级,不抛、不本地放行(设计 30 §四)。
                return ValidationResult.Fail(RedeemVerdict.ServiceUnavailable);
            }

            if (response == null)
            {
                return ValidationResult.Fail(RedeemVerdict.ServiceUnavailable);
            }

            return MapResponse(response);
#else
            // FANTASY_UNITY 关闭(无网络平台):降级为服务不可用,绝不本地放行。
            await UniTask.CompletedTask;
            return ValidationResult.Fail(RedeemVerdict.ServiceUnavailable);
#endif
        }

#if FANTASY_UNITY
        /// <summary>把服务端响应映射成裁决结果:结果码 + 成功时奖励列表(失败时奖励为空集合)。</summary>
        private static ValidationResult MapResponse(Fantasy.G2C_RedeemCodeResponse response)
        {
            var verdict = MapVerdict(response.ResultCode);
            if (verdict != RedeemVerdict.Success)
            {
                return ValidationResult.Fail(verdict);
            }

            var rewards = new List<RedeemRewardItem>();
            if (response.Rewards != null)
            {
                foreach (var r in response.Rewards)
                {
                    if (r == null) continue;
                    rewards.Add(new RedeemRewardItem(r.ItemId, r.Count));
                }
            }
            return new ValidationResult(RedeemVerdict.Success, rewards);
        }

        /// <summary>协议结果码 → 客户端裁决码(一一对应);未知码按服务不可用兜底,不崩。</summary>
        private static RedeemVerdict MapVerdict(Fantasy.RedeemResultCode code)
        {
            switch (code)
            {
                case Fantasy.RedeemResultCode.Success:            return RedeemVerdict.Success;
                case Fantasy.RedeemResultCode.InvalidCode:        return RedeemVerdict.InvalidCode;
                case Fantasy.RedeemResultCode.AlreadyRedeemed:    return RedeemVerdict.AlreadyRedeemed;
                case Fantasy.RedeemResultCode.Expired:            return RedeemVerdict.Expired;
                case Fantasy.RedeemResultCode.LimitReached:       return RedeemVerdict.LimitReached;
                case Fantasy.RedeemResultCode.ServiceUnavailable: return RedeemVerdict.ServiceUnavailable;
                default:                                          return RedeemVerdict.ServiceUnavailable;
            }
        }
#endif
    }
}
