namespace GameLogic.Rank
{
    /// <summary>
    /// 榜上一名（数据源产出 + 服务排序后回填名次，设计 22 §3.3）。
    /// </summary>
    public sealed class RankEntry
    {
        /// <summary>玩家展示名 textId（本地源：陪榜 = 配置占位名；本人 = PlayerInfo 名占位）。远程源用 <see cref="RemoteName"/>。</summary>
        public int PlayerNameTextId;
        /// <summary>
        /// 远程源服务端回的展示名（账号标识占位，设计 31 §3.5 / O5；本地源为 null）。
        /// UI 渲染优先用此（非空时）；客户端有本地昵称则替换。本地源走 <see cref="PlayerNameTextId"/> 占位。
        /// </summary>
        public string RemoteName;
        /// <summary>成绩。</summary>
        public long Score;
        /// <summary>达到该分的时间（并列时早者靠前，§3.3.2）。</summary>
        public long AchievedTicks;
        /// <summary>是否本机玩家（由数据源标记）。</summary>
        public bool IsSelf;
        /// <summary>名次（服务排序后回填，1 起）。</summary>
        public int Rank;
    }

    /// <summary>
    /// 一个榜的查询快照（设计 22 §3.3）。
    /// </summary>
    public sealed class RankBoard
    {
        /// <summary>榜 id。</summary>
        public int Id;
        /// <summary>已排序、已截展示上限的条目。</summary>
        public System.Collections.Generic.List<RankEntry> Entries;
        /// <summary>本机玩家在榜的条目（未入榜则 null）。</summary>
        public RankEntry Self;
        /// <summary>本人名次（未入榜 = 0）。</summary>
        public int SelfRank;
        /// <summary>本人成绩（未入榜 = 当前最佳，可能 &lt; condition）。</summary>
        public long SelfScore;
    }

    /// <summary>领取结果码（每日 / 点赞奖，设计 22 §3.9）。</summary>
    public enum RankClaimStatus
    {
        /// <summary>领取成功（奖励经邮件下发）。</summary>
        Success,
        /// <summary>未入榜（无名次）。</summary>
        NotRanked,
        /// <summary>该名次档 / 该榜无对应奖励。</summary>
        NoReward,
        /// <summary>今日已领过。</summary>
        AlreadyClaimedToday,
    }

    /// <summary>
    /// 领取结果（设计 22 §3.9）：状态 + 结果文案 textId。
    /// </summary>
    public readonly struct RankClaimResult
    {
        /// <summary>结果码。</summary>
        public readonly RankClaimStatus Status;
        /// <summary>结果文案 textId（占位，§3.9）。</summary>
        public readonly int TextId;

        public RankClaimResult(RankClaimStatus status, int textId)
        {
            Status = status;
            TextId = textId;
        }
    }

    /// <summary>
    /// 结果文案 textId（占位常量，设计 22 §3.9）。真实多语言查表延后（O6，同 num/item/reward/settings/redeem/mail）。
    /// 验收断言各返互不相同的非 0 值（同 19/20/21 做法）。
    /// </summary>
    public static class RankText
    {
        /// <summary>「领取成功」。</summary>
        public const int ClaimSuccess = 110801;
        /// <summary>「未入榜」。</summary>
        public const int NotRanked = 110802;
        /// <summary>「无可领奖励」。</summary>
        public const int NoReward = 110803;
        /// <summary>「今日已领取」。</summary>
        public const int AlreadyClaimedToday = 110804;
        /// <summary>结算邮件发件人 textId。</summary>
        public const int SettleSender = 110805;
        /// <summary>结算邮件标题（模板缺省兜底）textId。</summary>
        public const int SettleTitle = 110806;
        /// <summary>每日奖邮件标题 textId。</summary>
        public const int DailyMailTitle = 110807;
        /// <summary>点赞奖邮件标题 textId。</summary>
        public const int PraiseMailTitle = 110808;

        /// <summary>结果码 → 文案 textId。</summary>
        public static int TextIdFor(RankClaimStatus s)
        {
            switch (s)
            {
                case RankClaimStatus.Success: return ClaimSuccess;
                case RankClaimStatus.NotRanked: return NotRanked;
                case RankClaimStatus.NoReward: return NoReward;
                case RankClaimStatus.AlreadyClaimedToday: return AlreadyClaimedToday;
                default: return 0;
            }
        }
    }
}
