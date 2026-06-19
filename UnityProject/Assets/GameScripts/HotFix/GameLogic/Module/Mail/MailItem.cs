namespace GameLogic.Mail
{
    /// <summary>
    /// 收件草稿：对外 <see cref="IMailService.Send"/> 的入参（设计 21 §3.3）。
    /// 可由模板 id 构造（<see cref="FromTemplate"/>），也可直接填字段（游戏内系统自定义发奖）。
    /// </summary>
    public sealed class MailDraft
    {
        /// <summary>发件人 textId（系统 / 运营 / 排行榜… 占位）。</summary>
        public int SenderTextId;
        /// <summary>标题 textId。</summary>
        public int TitleTextId;
        /// <summary>内容 textId。</summary>
        public int BodyTextId;
        /// <summary>有效期（天）；&lt;=0 → 收件时用全局 RetainDays 兜底。</summary>
        public int ExpireDays;
        /// <summary>奖励随机库 id；0 = 无奖励。</summary>
        public int RewardPoolId;

        /// <summary>
        /// 按模板 id 建草稿：从 <c>MailConfigMgr.GetMail</c> 复制字段（设计 21 §3.3）。
        /// 模板不存在返 null（不抛），调用方决定是否发。
        /// </summary>
        public static MailDraft FromTemplate(int mailDefId, int senderTextId)
        {
            var def = GameLogic.Config.MailConfigMgr.GetMail(mailDefId);
            if (def == null) return null;
            return new MailDraft
            {
                SenderTextId = senderTextId,
                TitleTextId  = def.TitleTextId,
                BodyTextId   = def.BodyTextId,
                ExpireDays   = def.ExpireDays,
                RewardPoolId = def.RewardPoolId,
            };
        }
    }

    /// <summary>
    /// 收件箱里的一封邮件（运行期模型 + 可序列化，设计 21 §3.3）。
    /// 字段全部 <c>JsonUtility</c> 友好（无属性入盘、无 Unity 引用类型）；
    /// 红点 / 可删 / 有奖用 getter 表达，不进盘（JsonUtility 不序列化属性）。
    /// </summary>
    [System.Serializable]
    public sealed class MailItem
    {
        /// <summary>收件箱内唯一 id（服务分配，自增/时间戳，§七 O8）。</summary>
        public long Id;
        /// <summary>发件人 textId。</summary>
        public int SenderTextId;
        /// <summary>标题 textId。</summary>
        public int TitleTextId;
        /// <summary>内容 textId。</summary>
        public int BodyTextId;
        /// <summary>收件时间（<c>DateTime.Ticks</c>，JsonUtility 友好；过期判定还原比较，§3.3）。</summary>
        public long SendTimeTicks;
        /// <summary>有效期（天）；收件时已从草稿折算成自身天数（&lt;=0 表示用全局兜底）。</summary>
        public int ExpireDays;
        /// <summary>奖励随机库 id；0 = 无奖励。</summary>
        public int RewardPoolId;
        /// <summary>已读。</summary>
        public bool Read;
        /// <summary>奖励已领（无奖励邮件 <see cref="RewardPoolId"/>==0 视同已结清，但本字段仍按是否领过算）。</summary>
        public bool Claimed;

        /// <summary>是否带奖励附件。</summary>
        public bool HasReward => RewardPoolId != 0;
        /// <summary>红点：未读 OR 有奖励未领（设计 21 §3.6）。</summary>
        public bool HasRedDot => !Read || (HasReward && !Claimed);
        /// <summary>删除前置：已读 &amp;&amp;（无奖励 ‖ 奖励已领）（设计 21 §2.1，spec「已读且奖励已领或无奖励」）。</summary>
        public bool CanDelete => Read && (!HasReward || Claimed);
    }

    /// <summary>领取结果码（设计 21 §3.4.2）。</summary>
    public enum ClaimStatus
    {
        /// <summary>领取成功（含一键）。</summary>
        Success,
        /// <summary>邮件不存在。</summary>
        NotFound,
        /// <summary>该邮件无奖励。</summary>
        NoReward,
        /// <summary>奖励已领取过。</summary>
        AlreadyClaimed,
        /// <summary>邮件已过期。</summary>
        Expired,
    }

    /// <summary>
    /// 领取结果（设计 21 §3.4.2）：状态 + 结果文案 textId + 成功时的产出列表（供 UI 转 17 RewardView 展示）。
    /// </summary>
    public readonly struct ClaimResult
    {
        /// <summary>结果码。</summary>
        public readonly ClaimStatus Status;
        /// <summary>结果文案 textId（占位，§3.8）。</summary>
        public readonly int TextId;
        /// <summary>成功时的发奖产出列表（复用 16 <c>GrantPayload</c>）；失败时为空集合（非 null）。</summary>
        public readonly System.Collections.Generic.IReadOnlyList<GameLogic.BlockBlast.Item.GrantPayload> Granted;

        public ClaimResult(
            ClaimStatus status, int textId,
            System.Collections.Generic.IReadOnlyList<GameLogic.BlockBlast.Item.GrantPayload> granted)
        {
            Status = status;
            TextId = textId;
            Granted = granted ?? System.Array.Empty<GameLogic.BlockBlast.Item.GrantPayload>();
        }
    }

    /// <summary>
    /// 结果文案 textId（占位常量，设计 21 §3.8）。真实多语言查表延后（§七 O5，同 num/item/reward/settings/redeem）。
    /// 验收断言各返互不相同的非 0 值（同 19/20 做法）。
    /// </summary>
    public static class MailText
    {
        /// <summary>「奖励已领取」。</summary>
        public const int ClaimSuccess   = 110701;
        /// <summary>「该邮件无奖励」。</summary>
        public const int ClaimNoReward  = 110702;
        /// <summary>「奖励已领取过」。</summary>
        public const int AlreadyClaimed = 110703;
        /// <summary>「邮件已过期」。</summary>
        public const int ClaimExpired   = 110704;
        /// <summary>「邮件不存在」。</summary>
        public const int MailNotFound   = 110705;
        /// <summary>「邮件服务暂不可用，请稍后重试」（设计 32 §四服务端化新增分支：与上方各「此邮件不能领」文案有别——
        /// 前者鼓励重试、邮件仍可领，后者告知此邮件本身不能领，误判会让玩家以为好邮件废了）。</summary>
        public const int ServiceUnavailable = 110706;

        /// <summary>结果码 → 文案 textId。</summary>
        public static int TextIdFor(ClaimStatus s) => s switch
        {
            ClaimStatus.Success        => ClaimSuccess,
            ClaimStatus.NoReward       => ClaimNoReward,
            ClaimStatus.AlreadyClaimed => AlreadyClaimed,
            ClaimStatus.Expired        => ClaimExpired,
            ClaimStatus.NotFound       => MailNotFound,
            _                          => 0,
        };
    }
}
