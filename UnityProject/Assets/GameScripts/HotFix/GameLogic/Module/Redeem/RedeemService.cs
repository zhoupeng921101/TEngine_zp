using System;
using System.Collections.Generic;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Item;

namespace GameLogic.Redeem
{
    /// <summary>兑换结果码（设计 20 §3.5）。</summary>
    public enum RedeemResult
    {
        /// <summary>兑换成功。</summary>
        Success,
        /// <summary>空 / 纯空白输入。</summary>
        EmptyInput,
        /// <summary>码不存在。</summary>
        NotFound,
        /// <summary>已兑换过（once_per_player=1）。</summary>
        AlreadyRedeemed,
        /// <summary>已过期。</summary>
        Expired,
        /// <summary>校验源不可用（远程 stub）。</summary>
        SourceUnavailable,
    }

    /// <summary>
    /// 兑换结果结构（设计 20 §3.5）：结果码 + 文案 textId + 成功时的奖励产出列表。
    /// </summary>
    public readonly struct RedeemOutcome
    {
        /// <summary>结果码。</summary>
        public readonly RedeemResult Result;
        /// <summary>结果提示文案 textId（占位，设计 20 §3.7）。</summary>
        public readonly int TextId;
        /// <summary>成功时的奖励产出（复用 16 <see cref="GrantPayload"/>）；失败时为空集合（非 null）。</summary>
        public readonly IReadOnlyList<GrantPayload> Granted;

        public RedeemOutcome(RedeemResult result, int textId, IReadOnlyList<GrantPayload> granted)
        {
            Result = result;
            TextId = textId;
            Granted = granted ?? Array.Empty<GrantPayload>();
        }
    }

    /// <summary>
    /// 兑换服务（编排层，设计 20 §3.5）。把「规整 → 校验 → 去重 → 发奖 → 出结果」串起来，
    /// 返 <see cref="RedeemOutcome"/>。注入校验器 + 去重存储；发奖落 <c>MergeOrderState</c>（可 null 走纯解析）。
    /// </summary>
    /// <remarks>
    /// 纯逻辑，无网络、不阻塞、不抛异常（失败返对应结果码）。
    /// 发奖<b>不新造逻辑</b>，复用 16 道具系统 <see cref="ItemGrant.GrantOnAcquire"/>（设计 20 §3.6）。
    /// 「记录已兑换」必须在<b>发奖成功之后</b>（失败不占名额，设计 20 §八 风险表）。
    /// </remarks>
    public sealed class RedeemService
    {
        private readonly IRedeemValidator _validator;
        private readonly IRedeemStore _store;

        /// <summary>当前时间提供者（可注入，默认系统时钟）。限时码到期判定用（设计 20 §七 O4）。</summary>
        public Func<DateTime> NowProvider = () => DateTime.Now;

        public RedeemService(IRedeemValidator validator, IRedeemStore store)
        {
            _validator = validator;
            _store = store;
        }

        /// <summary>规整：trim + 转大写（与配置表 key 口径一致）。空 / 纯空白返空串。</summary>
        public static string Normalize(string raw)
            => string.IsNullOrWhiteSpace(raw) ? "" : raw.Trim().ToUpperInvariant();

        /// <summary>
        /// 兑换一个码。结果码顺序：空输入 → SourceUnavailable → NotFound → Expired → AlreadyRedeemed → 发奖 → MarkRedeemed。
        /// </summary>
        /// <param name="raw">玩家原始输入（未规整）。</param>
        /// <param name="state">发奖落点（可 null：仅产出结构、不落实际系统，纯解析路径）。</param>
        /// <param name="rng">随机礼包展开用（可 null：不展开随机礼包）。</param>
        public RedeemOutcome Redeem(string raw, MergeOrderState state, Random rng)
        {
            var code = Normalize(raw);
            if (code.Length == 0) return Fail(RedeemResult.EmptyInput);

            var v = _validator.Validate(code);
            if (v.Status == ValidationStatus.SourceUnavailable) return Fail(RedeemResult.SourceUnavailable);
            if (v.Status == ValidationStatus.NotFound)          return Fail(RedeemResult.NotFound);

            var def = v.Def;
            if (IsExpired(def)) return Fail(RedeemResult.Expired);
            if (def.OncePerPlayer == 1 && _store.HasRedeemed(code)) return Fail(RedeemResult.AlreadyRedeemed);

            // 发奖：复用 16 道具系统落点（§3.6）
            var granted = GrantRewards(def, state, rng);

            // 成功后才记（失败不占名额）；仅去重 once_per_player=1 的码
            if (def.OncePerPlayer == 1) _store.MarkRedeemed(code);

            return new RedeemOutcome(RedeemResult.Success, RedeemText.Success, granted);
        }

        /// <summary>
        /// 发奖：遍历奖励项，每项查 <see cref="GameLogic.Config.ItemConfigMgr.GetItem"/> 拿道具定义，
        /// 用既有 <see cref="ItemGrant.GrantOnAcquire"/> 落 <c>MergeOrderState</c>（automatic=1 立即结算 / 0 进背包）。
        /// 汇总立即结算的产出供 UI 展示；<c>state==null</c> 时只产出结构不落（纯解析）。
        /// 本轮不接背包实例（设计 20 §七 O3）。
        /// </summary>
        private IReadOnlyList<GrantPayload> GrantRewards(RedeemCodeDef def, MergeOrderState state, Random rng)
        {
            var all = new List<GrantPayload>();
            foreach (var r in def.Rewards)
            {
                var itemDef = GameLogic.Config.ItemConfigMgr.GetItem(r.ItemId);   // 既有
                var produced = ItemGrant.GrantOnAcquire(itemDef, r.Num, state, rng); // 既有，自动落点
                all.AddRange(produced);
            }
            return all;
        }

        /// <summary>过期判定：ExpireTime 空 → 不过期；否则 parse 后与 NowProvider() 比（parse 失败按不过期处理，不崩）。</summary>
        private bool IsExpired(RedeemCodeDef def)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.ExpireTime)) return false;
            if (!DateTime.TryParse(def.ExpireTime, out var expire)) return false;
            return NowProvider() > expire;
        }

        private static RedeemOutcome Fail(RedeemResult result)
            => new RedeemOutcome(result, RedeemText.TextIdFor(result), Array.Empty<GrantPayload>());
    }
}
