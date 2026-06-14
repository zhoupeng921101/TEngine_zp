namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 玩家账号等级 / 经验曲线（设计 18 §3.4）。纯函数：玩家等级是 <see cref="PlayerInfo.Exp"/> 的函数。
    /// </summary>
    /// <remarks>
    /// 独立第三条进度线：不复用 <c>MergeOrderState.Exp</c>/<c>GuardianLevel</c>（守护者等级语义是「神庙主线进度」，
    /// 只由修神庙产经验，与「玩家账号活跃度」不同）。两条线互不读写。
    /// 每级所需经验线性递增（最小可用，可后续换表）：第 L→L+1 级门槛 = BASE_EXP + (L-1)*STEP_EXP。
    /// </remarks>
    public static class PlayerLevelConfig
    {
        /// <summary>1→2 级所需经验。</summary>
        public const int BASE_EXP = 100;
        /// <summary>每升一级，下一级门槛多 50。</summary>
        public const int STEP_EXP = 50;
        /// <summary>等级上限（封顶后经验仍累计但等级不再涨）。</summary>
        public const int MAX_LEVEL = 60;

        /// <summary>
        /// 升到第 <paramref name="level"/> 级（level≥1）所需的「累计」经验门槛。
        /// CumExp(1)=0；CumExp(L)=Σ_{k=1..L-1}(BASE_EXP + (k-1)*STEP_EXP)。
        /// 闭式：CumExp(L) = (L-1)*BASE_EXP + STEP_EXP*(L-1)*(L-2)/2。
        /// </summary>
        public static int CumExp(int level)
        {
            if (level <= 1) return 0;
            int n = level - 1;                       // 跨过的等级数
            return n * BASE_EXP + STEP_EXP * n * (n - 1) / 2;
        }

        /// <summary>累计经验 → 当前等级（夹在 [1, MAX_LEVEL]）。</summary>
        public static int LevelFor(int exp)
        {
            if (exp <= 0) return 1;
            int level = 1;
            while (level < MAX_LEVEL && exp >= CumExp(level + 1)) level++;
            return level;
        }

        /// <summary>当前级已积累经验（经验槽用）。</summary>
        public static int ExpIntoLevel(int exp)
        {
            int e = exp < 0 ? 0 : exp;
            return e - CumExp(LevelFor(e));
        }

        /// <summary>升下一级还差多少（经验槽用；封顶返 0）。</summary>
        public static int ExpToNext(int exp)
        {
            int e = exp < 0 ? 0 : exp;
            int level = LevelFor(e);
            return level >= MAX_LEVEL ? 0 : CumExp(level + 1) - e;
        }
    }

    /// <summary>
    /// 加经验服务（设计 18 §3.4）。只增不减：负数夹 0。
    /// 「玩什么加多少经验」是经济接线 + 运营数据，本轮不接（O7），只给容器 + 换算 + 加经验接口。
    /// </summary>
    public static class PlayerExpService
    {
        /// <summary>给玩家加经验（<paramref name="amount"/> ≤0 视作 0，不减）。返回加后的总经验。</summary>
        public static int AddExp(PlayerInfo p, int amount)
        {
            if (amount > 0) p.Exp += amount;
            return p.Exp;
        }
    }

    /// <summary>
    /// 等级奖励预览数据结构占位（设计 18 §3.4，O7）。「每级给什么」是经济数据，本轮不接，
    /// 留接口 + 空实现，真实奖励表延后。接入时实现 <see cref="RewardFor"/> 查表即可，调用方不返工。
    /// </summary>
    public interface ILevelRewardProvider
    {
        /// <summary>查第 <paramref name="level"/> 级的奖励预览（本轮空实现返 null = 该级无预览）。</summary>
        LevelReward? RewardFor(int level);
    }

    /// <summary>单条等级奖励（占位结构，真实奖励内容延后 O7）。</summary>
    public readonly struct LevelReward
    {
        /// <summary>奖励所属等级。</summary>
        public readonly int Level;
        /// <summary>奖励道具/货币 id（占位，真实奖励表延后）。</summary>
        public readonly int RewardId;
        /// <summary>奖励数量（占位）。</summary>
        public readonly int Amount;

        public LevelReward(int level, int rewardId, int amount)
        {
            Level = level;
            RewardId = rewardId;
            Amount = amount;
        }
    }

    /// <summary>等级奖励 Provider 空实现（O7 占位）：任何等级返 null，接入真实奖励表时替换。</summary>
    public sealed class EmptyLevelRewardProvider : ILevelRewardProvider
    {
        public LevelReward? RewardFor(int level) => null;
    }
}
