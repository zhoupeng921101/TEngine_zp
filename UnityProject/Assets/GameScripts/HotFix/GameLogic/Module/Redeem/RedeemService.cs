using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Item;
using GameLogic.Config;

namespace GameLogic.Redeem
{
    /// <summary>兑换结果码(客户端侧,设计 30 §3.2)。</summary>
    public enum RedeemResult
    {
        /// <summary>兑换成功。</summary>
        Success,
        /// <summary>空 / 纯空白输入:客户端提交前短路,不发请求(设计 30 §3.1 / CV5)。</summary>
        EmptyInput,
        /// <summary>码无效(服务端裁定不存在 / 规整后为空)。</summary>
        InvalidCode,
        /// <summary>本账号已兑过此码(服务端裁定)。</summary>
        AlreadyRedeemed,
        /// <summary>码已过期(服务端时钟裁定)。</summary>
        Expired,
        /// <summary>全局限量已满(服务端新能力,设计 30 §五)。</summary>
        LimitReached,
        /// <summary>服务不可用 / 发不出 / 超时:不发奖、码可重试(设计 30 §四 / CV3)。</summary>
        ServiceUnavailable,
    }

    /// <summary>
    /// 兑换结果结构(设计 30 §3.2):结果码 + 文案 textId + 成功时的奖励产出列表。
    /// </summary>
    public readonly struct RedeemOutcome
    {
        /// <summary>结果码。</summary>
        public readonly RedeemResult Result;
        /// <summary>结果提示文案 textId(占位,设计 30 §八 O6)。</summary>
        public readonly int TextId;
        /// <summary>成功时的奖励产出(复用 16 <see cref="GrantPayload"/>);失败时为空集合(非 null)。</summary>
        public readonly IReadOnlyList<GrantPayload> Granted;

        public RedeemOutcome(RedeemResult result, int textId, IReadOnlyList<GrantPayload> granted)
        {
            Result = result;
            TextId = textId;
            Granted = granted ?? Array.Empty<GrantPayload>();
        }
    }

    /// <summary>
    /// 兑换服务(编排层,设计 30 §一)。把「空输入短路 → 发请求收服务端裁决 → 仅成功时本地发奖 → 出结果」串起来,
    /// 返 <see cref="RedeemOutcome"/>。注入裁决器(生产 <see cref="RemoteRedeemValidator"/> / 单测桩)。
    /// </summary>
    /// <remarks>
    /// 权威全在服务端:码是否有效 / 兑过 / 过期 / 全局是否超量,均由服务端裁定回包,客户端<b>无本地放行</b>(设计 30 §四 / CV4)。
    /// 异步:裁决经一次 RPC 往返,本方法 <c>async UniTask</c>(框架通用异步类型,与网络层 FTask 解耦);不阻塞、不抛异常(失败返对应结果码)。
    /// 发奖<b>不新造逻辑</b>,按服务端奖励列表复用 16 道具系统 <see cref="ItemGrant.GrantOnAcquire"/>(设计 30 §一 发奖落地条 / CV1);
    /// <b>仅成功时发奖</b>,失败分支(含服务不可用)一律不发奖(CV2/CV3)。
    /// 客户端不再持本地码表 / 本地去重 / 本地校验器——去重与限量由服务端记录裁定(设计 30 §六)。
    /// </remarks>
    public sealed class RedeemService
    {
        private readonly IRedeemValidator _validator;

        public RedeemService(IRedeemValidator validator)
        {
            _validator = validator;
        }

        /// <summary>
        /// 显示层规整(仅去首尾空白):用于客户端空输入短路判断。
        /// <b>裁决口径的规整(trim + 大写)权威在服务端</b>(设计 30 §3.1),客户端按原样提交码字符串;
        /// 此处仅为判空与显示整洁,不作裁决依据。
        /// </summary>
        public static string TrimForDisplay(string raw)
            => string.IsNullOrWhiteSpace(raw) ? "" : raw.Trim();

        /// <summary>
        /// 兑换一个码。流程:空输入短路(不发请求)→ 发裁决请求 → 按服务端结果码分发;仅成功时按服务端奖励列表本地发奖。
        /// </summary>
        /// <param name="raw">玩家原始输入(客户端只判空 / 显示 trim,规整权威在服务端)。</param>
        /// <param name="state">发奖落点(可 null:仅产出结构、不落实际系统,纯解析路径)。</param>
        /// <param name="rng">随机礼包展开用(可 null:不展开随机礼包)。</param>
        public async UniTask<RedeemOutcome> RedeemAsync(string raw, MergeOrderState state, Random rng)
        {
            // 空 / 纯空白:客户端提交前短路,不发请求(省一次往返,纯 UX,设计 30 §3.1 / CV5)。
            if (string.IsNullOrWhiteSpace(raw))
            {
                await UniTask.CompletedTask;
                return Fail(RedeemResult.EmptyInput);
            }

            // 发裁决请求(原样提交,规整权威在服务端)。裁决器内部已兜底:断服 / 超时 → ServiceUnavailable,不抛。
            var verdict = await _validator.ValidateAsync(raw);

            if (verdict.Verdict != RedeemVerdict.Success)
            {
                // 失败分支(含服务不可用):不发奖,出对应文案(CV2/CV3)。
                return Fail(MapResult(verdict.Verdict));
            }

            // 成功:仅此分支按服务端奖励列表本地发奖(复用 16 既有落点,CV1)。
            var granted = GrantRewards(verdict.Rewards, state, rng);
            return new RedeemOutcome(RedeemResult.Success, RedeemText.Success, granted);
        }

        /// <summary>
        /// 发奖:遍历服务端裁定的奖励列表,每项查 <see cref="ItemConfigMgr.GetItem"/> 拿道具定义,
        /// 用既有 <see cref="ItemGrant.GrantOnAcquire"/> 落 <c>MergeOrderState</c>(automatic=1 立即结算 / 0 进背包)。
        /// 汇总产出供 17 奖励展示;<c>state==null</c> 时只产出结构不落(纯解析,供单测)。
        /// </summary>
        private static IReadOnlyList<GrantPayload> GrantRewards(
            IReadOnlyList<RedeemRewardItem> rewards, MergeOrderState state, Random rng)
        {
            var all = new List<GrantPayload>();
            if (rewards == null) return all;
            foreach (var r in rewards)
            {
                var itemDef = ItemConfigMgr.GetItem(r.ItemId);          // 既有道具元数据(设计 16)
                var produced = ItemGrant.GrantOnAcquire(itemDef, r.Count, state, rng); // 既有,自动落点
                all.AddRange(produced);
            }
            return all;
        }

        /// <summary>裁决码 → 客户端结果码(一一对应)。</summary>
        private static RedeemResult MapResult(RedeemVerdict verdict)
        {
            switch (verdict)
            {
                case RedeemVerdict.Success:            return RedeemResult.Success;
                case RedeemVerdict.InvalidCode:        return RedeemResult.InvalidCode;
                case RedeemVerdict.AlreadyRedeemed:    return RedeemResult.AlreadyRedeemed;
                case RedeemVerdict.Expired:            return RedeemResult.Expired;
                case RedeemVerdict.LimitReached:       return RedeemResult.LimitReached;
                case RedeemVerdict.ServiceUnavailable: return RedeemResult.ServiceUnavailable;
                default:                               return RedeemResult.ServiceUnavailable;
            }
        }

        private static RedeemOutcome Fail(RedeemResult result)
            => new RedeemOutcome(result, RedeemText.TextIdFor(result), Array.Empty<GrantPayload>());
    }
}
