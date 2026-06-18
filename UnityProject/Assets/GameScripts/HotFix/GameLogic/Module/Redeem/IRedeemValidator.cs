using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameLogic.Redeem
{
    /// <summary>
    /// 服务端裁决结果码（设计 30 §3.2，与协议 <c>Fantasy.RedeemResultCode</c> 一一对应）。
    /// 兑换权威在服务端：码是否有效 / 是否兑过 / 是否过期 / 全局是否超量，全由服务端裁定回包。
    /// </summary>
    public enum RedeemVerdict
    {
        /// <summary>成功并已授权发奖（结果含奖励列表）。</summary>
        Success,
        /// <summary>码不存在或规整后为空（服务端兜底）。</summary>
        InvalidCode,
        /// <summary>本账号已兑过此码（结果不含奖励）。</summary>
        AlreadyRedeemed,
        /// <summary>码已过期（服务端时钟判定）。</summary>
        Expired,
        /// <summary>全局限量已满。</summary>
        LimitReached,
        /// <summary>服务不可用 / 发不出 / 超时：不发奖、码保持可兑、提示重试（设计 30 §四）。</summary>
        ServiceUnavailable,
    }

    /// <summary>单个奖励项（道具 id × 数量），与协议 <c>Fantasy.RedeemRewardItem</c> 对应。</summary>
    public readonly struct RedeemRewardItem
    {
        /// <summary>道具 id（指向 <c>item.TbItemDef</c>，设计 16）。</summary>
        public readonly int ItemId;
        /// <summary>数量。</summary>
        public readonly int Count;

        public RedeemRewardItem(int itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }

    /// <summary>
    /// 服务端裁决结果（设计 30 §3.2）：结果码 + 成功时的奖励列表（道具 id × 数量）。
    /// 客户端不再持本地码表 / 本地去重——「码有效吗、兑过没、能换什么」完全由服务端裁定，本结构只承载回包。
    /// </summary>
    public readonly struct ValidationResult
    {
        /// <summary>服务端裁决码。</summary>
        public readonly RedeemVerdict Verdict;
        /// <summary>成功时的奖励列表（道具 id × 数量）；失败时为空集合（非 null，设计 30 §3.2）。</summary>
        public readonly IReadOnlyList<RedeemRewardItem> Rewards;

        public ValidationResult(RedeemVerdict verdict, IReadOnlyList<RedeemRewardItem> rewards)
        {
            Verdict = verdict;
            Rewards = rewards ?? Array.Empty<RedeemRewardItem>();
        }

        /// <summary>失败结果（无奖励）的便捷构造。</summary>
        public static ValidationResult Fail(RedeemVerdict verdict)
            => new ValidationResult(verdict, Array.Empty<RedeemRewardItem>());
    }

    /// <summary>
    /// 兑换裁决器接缝（设计 30 §一 · 权威上移服务端）。
    /// 把「码能否兑、兑了什么」这件服务端权威的事抽象成<b>异步</b>接口，使 <see cref="RedeemService"/>
    /// 只依赖接口、不依赖裁决来源——生产注 <see cref="RemoteRedeemValidator"/>（发 RPC 收服务端裁决），
    /// 单测注桩裁决器验客户端各结果码分支（CV6）。
    /// </summary>
    /// <remarks>
    /// 异步:裁决须经一次联网往返,接口返框架通用 <c>UniTask</c>(TEngine 异步红线的统一可等待类型,
    /// 与网络层 Fantasy 的 <c>FTask</c> 解耦——FantasyClient 受 <c>FANTASY_UNITY</c> 约束,
    /// 接口不依赖该 define,使 GameLogic 在任何平台都可编译;FTask↔UniTask 桥接封在 <see cref="RemoteRedeemValidator"/> 内)。
    /// 客户端<b>无</b>本地放行路径:断服只回 <see cref="RedeemVerdict.ServiceUnavailable"/>,绝不本地裁定成功(设计 30 §四 / CV4)。
    /// </remarks>
    public interface IRedeemValidator
    {
        /// <summary>
        /// 提交码字符串裁决。返回服务端裁决结果(结果码 + 成功时奖励列表)。
        /// 码的规整(trim + 大写)权威在服务端,客户端原样提交;空输入短路由上层处理,不进本接口。
        /// </summary>
        UniTask<ValidationResult> ValidateAsync(string code);
    }
}
