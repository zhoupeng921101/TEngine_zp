using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Item;
using GameLogic.Config;

namespace GameLogic.Mail
{
    /// <summary>
    /// 服务端校验领奖结果（客户端编排层，设计 32 §3.4）：结果码 + 文案 textId + 成功时本地落地的产出列表。
    /// </summary>
    /// <remarks>
    /// 与设计 21 本地领奖 <see cref="ClaimResult"/> 平行：本结构走<b>服务端校验</b>路径（设计 32），
    /// 多一个「服务不可用」分支（领奖断服不本地放行，设计 32 §四 / CV3）。
    /// </remarks>
    public readonly struct MailClaimDisplay
    {
        /// <summary>裁决结果码。</summary>
        public readonly MailClaimCode Code;
        /// <summary>结果文案 textId（占位，复用 <see cref="MailText"/>）。</summary>
        public readonly int TextId;
        /// <summary>成功时本地落地的产出（复用 16 <see cref="GrantPayload"/>）；失败 / 断服为空集合（非 null）。</summary>
        public readonly IReadOnlyList<GrantPayload> Granted;

        public MailClaimDisplay(MailClaimCode code, int textId, IReadOnlyList<GrantPayload> granted)
        {
            Code = code;
            TextId = textId;
            Granted = granted ?? Array.Empty<GrantPayload>();
        }
    }

    /// <summary>
    /// 远程邮件服务（编排层，设计 32 §一）：把「拉应收列表」「领奖走服务端校验 + 仅成功本地落地」串起来。
    /// 注入 <see cref="IRemoteMailSource"/>（生产 <see cref="RemoteMailSource"/> / 单测桩）。
    /// </summary>
    /// <remarks>
    /// 权威全在服务端（设计 32 读前必看 / §四 / CV1-CV3）：
    /// <list type="bullet">
    /// <item><b>拉列表</b>：远程下发该账号应收 + 未过期邮件；断服 / 超时 → 空载（空列表），不阻断玩法、无本地运营邮件可伪造；</item>
    /// <item><b>领奖</b>：发领取请求收服务端裁决（防重 + 抽奖 + 过期），<b>仅成功时</b>按服务端奖励列表本地落地（复用 16
    /// <see cref="ItemGrant.GrantOnAcquire"/>，客户端不本地抽奖）；失败分支（含服务不可用）一律不发奖（CV2 / CV3 / CV7）。</item>
    /// </list>
    /// 客户端<b>无本地放行</b>领奖路径（设计 21 §3.4.2 本地抽奖 / 本地防重退出权威，设计 32 §六）：断服领取只进
    /// 「服务不可用」降级、邮件保持可领，不会本地成功发奖（CV3，同 [设计 30] 兑换码「不本地放行」）。
    /// 异步：经一次 RPC 往返，方法 <c>async UniTask</c>（框架通用异步类型，与网络层 FTask 解耦）；不阻塞、不抛异常（失败返对应结果码）。
    /// </remarks>
    public sealed class RemoteMailService
    {
        private readonly IRemoteMailSource _remote;

        public RemoteMailService(IRemoteMailSource remote)
        {
            _remote = remote ?? throw new ArgumentNullException(nameof(remote));
        }

        /// <summary>
        /// 拉该账号应收且未过期的邮件列表（设计 32 §3.1 / §3.2 / CV1）。
        /// 断服 / 超时 / 不可用 → 返空列表（不阻断玩法、不抛）；收件箱表现层据此画列表 / 红点（排序 / 已读由表现层处理）。
        /// </summary>
        public async UniTask<IReadOnlyList<MailListEntry>> PullInboxAsync()
        {
            var snapshot = await _remote.FetchInboxAsync();
            // null（断服 / 超时 / 服务不可用）→ 空列表降级（设计 32 §四，列表只是显示、无安全后果）。
            if (snapshot == null || snapshot.Mails == null) return Array.Empty<MailListEntry>();
            return snapshot.Mails;
        }

        /// <summary>
        /// 领取一封邮件（设计 32 §3.3 / §3.4 / CV2 / CV3）：发领取请求 → 服务端裁决 →
        /// <b>仅 <see cref="MailClaimCode.Success"/></b> 时按服务端奖励列表本地落地（复用 16）；其余分支（含服务不可用）不发奖。
        /// </summary>
        /// <param name="mailId">要领取的那封邮件标识（来自 <see cref="PullInboxAsync"/> 下发列表）。</param>
        /// <param name="state">发奖落点（可 null：仅产出结构、不落实际系统，纯解析路径，供单测）。</param>
        /// <param name="rng">随机礼包展开用（可 null：不展开随机礼包）。</param>
        public async UniTask<MailClaimDisplay> ClaimAsync(string mailId, MergeOrderState state, Random rng)
        {
            var outcome = await _remote.ClaimAsync(mailId);

            if (outcome.Code != MailClaimCode.Success)
            {
                // 失败分支（邮件不存在 / 无奖励 / 已领过 / 已过期 / 服务不可用）：不发奖、出对应文案（CV2 / CV3）。
                return Fail(outcome.Code);
            }

            // 成功：仅此分支按服务端奖励列表本地落地（复用 16 既有落点，客户端不本地抽奖，CV2 / CV7）。
            var granted = GrantRewards(outcome.Rewards, state, rng);
            return new MailClaimDisplay(MailClaimCode.Success, MailText.ClaimSuccess, granted);
        }

        /// <summary>
        /// 本地落地：遍历服务端裁定的奖励列表，每项查 <see cref="ItemConfigMgr.GetItem"/> 拿道具定义，
        /// 用既有 <see cref="ItemGrant.GrantOnAcquire"/> 落 <c>MergeOrderState</c>（automatic=1 立即结算 / 0 进背包）。
        /// 汇总产出供 17 奖励展示；<c>state==null</c> 时只产出结构不落（纯解析，供单测）。
        /// <b>不</b>本地抽奖（奖励已由服务端抽好，设计 32 读前必看第 2 条 / CV7）：只对服务端给的 item id × count 落点。
        /// </summary>
        private static IReadOnlyList<GrantPayload> GrantRewards(
            IReadOnlyList<MailRewardLine> rewards, MergeOrderState state, Random rng)
        {
            var all = new List<GrantPayload>();
            if (rewards == null) return all;
            foreach (var r in rewards)
            {
                var itemDef = ItemConfigMgr.GetItem(r.ItemId);              // 既有道具元数据（设计 16）
                var produced = ItemGrant.GrantOnAcquire(itemDef, r.Count, state, rng); // 既有，自动落点
                all.AddRange(produced);
            }
            return all;
        }

        private static MailClaimDisplay Fail(MailClaimCode code)
            => new MailClaimDisplay(code, TextIdFor(code), Array.Empty<GrantPayload>());

        /// <summary>结果码 → 文案 textId（复用 <see cref="MailText"/>；服务不可用占位常量）。</summary>
        private static int TextIdFor(MailClaimCode code)
        {
            switch (code)
            {
                case MailClaimCode.Success:            return MailText.ClaimSuccess;
                case MailClaimCode.NoReward:           return MailText.ClaimNoReward;
                case MailClaimCode.AlreadyClaimed:     return MailText.AlreadyClaimed;
                case MailClaimCode.Expired:            return MailText.ClaimExpired;
                case MailClaimCode.MailNotFound:       return MailText.MailNotFound;
                case MailClaimCode.ServiceUnavailable: return MailText.ServiceUnavailable;
                default:                               return 0;
            }
        }
    }
}
