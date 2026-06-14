namespace GameLogic.Rank
{
    /// <summary>
    /// 结算时机类型（spec valid_type，设计 22 §3.1 / §3.5）。
    /// </summary>
    public enum RankValidType
    {
        /// <summary>无结算，持续开启，永不结算（只查榜不发结算奖）。</summary>
        Always = 0,
        /// <summary>开服第 X 天后结算（valid_val = 天数）；一次性。</summary>
        OpenDays = 1,
        /// <summary>指定时间结算（valid_val = Unix 秒）；一次性。</summary>
        FixedTime = 2,
        /// <summary>周循环，星期 X 结算（valid_val = 1..7，1=周一）；每周一次。</summary>
        Weekly = 3,
    }

    /// <summary>
    /// 一个名次奖励档（spec 配置表一行，设计 22 §3.2）。
    /// 同 <see cref="RankDef.Id"/> 的多行聚合成一个榜的多个档（各档按 <see cref="RankMin"/> 升序）。
    /// </summary>
    public sealed class RankRewardTier
    {
        /// <summary>名次区间下界（含）。</summary>
        public int RankMin;
        /// <summary>名次区间上界（含）。</summary>
        public int RankMax;
        /// <summary>实发奖励库 id（道具系统 16 gift_random index）；0 = 无奖。</summary>
        public int RewardPoolId;
        /// <summary>UI 预览库 id；0 = 同 <see cref="RewardPoolId"/>。</summary>
        public int ShowRewardPoolId;
        /// <summary>本档每日奖励库 id；0 = 无每日奖。</summary>
        public int DailyRewardPoolId;
    }

    /// <summary>
    /// 一个榜的定义（同 id 多行聚合，设计 22 §3.2）。
    /// 榜级字段（name/group/method/condition/praise/valid_*/mail/上限）取该 id 首行；
    /// 各行的名次档进 <see cref="Tiers"/>（按 RankMin 升序）。
    /// </summary>
    public sealed class RankDef
    {
        /// <summary>榜唯一 id（spec Id）。</summary>
        public int Id;
        /// <summary>名称 textId（占位，§3.9 / O6）。</summary>
        public int NameTextId;
        /// <summary>排行榜组（UI 分页签分组）。</summary>
        public int Group;
        /// <summary>所属玩法类型（分数来自哪个维度；本轮枚举占位，O3）。</summary>
        public int Method;
        /// <summary>入榜要求（最低入榜分；成绩 &lt; 此值不进榜）。</summary>
        public long Condition;
        /// <summary>点赞奖励库 id；0 = 无点赞按钮（榜级，不分档）。</summary>
        public int PraiseRewardPoolId;
        /// <summary>结算时机类型（spec valid_type）。</summary>
        public RankValidType ValidType;
        /// <summary>结算时机参数（配合 <see cref="ValidType"/>，spec valid_val）。</summary>
        public long ValidVal;
        /// <summary>结算邮件模板 id（设计 21 MailDef.Id）。</summary>
        public int MailDefId;
        /// <summary>入榜上限（参与排名 / 结算名额）。</summary>
        public int CountMax;
        /// <summary>展示上限（List 返回条数）。</summary>
        public int ShowMax;
        /// <summary>名次档（按 RankMin 升序）。</summary>
        public System.Collections.Generic.List<RankRewardTier> Tiers;

        /// <summary>
        /// 按名次查中奖档；无匹配返 null（名次未落入任何区间 → 无奖）。
        /// </summary>
        public RankRewardTier TierForRank(int rank)
        {
            if (Tiers == null) return null;
            for (int i = 0; i < Tiers.Count; i++)
                if (rank >= Tiers[i].RankMin && rank <= Tiers[i].RankMax) return Tiers[i];
            return null;
        }
    }
}
