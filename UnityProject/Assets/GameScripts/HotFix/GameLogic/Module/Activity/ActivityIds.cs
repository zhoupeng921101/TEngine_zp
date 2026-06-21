namespace GameLogic.Activity
{
    /// <summary>
    /// 活动 id 常量集中(设计 48 §3.5 / O7;本子单单点 = 累计游戏 N 局 = 5,首次集中以避免散落魔数)。
    /// 加同类 Cumulative 活动时按 activity.xlsx 行新增常量,业务出口调
    /// <c>GameContext.Activity.IncrementAndLogAsync(ActivityIds.Xxx, 1).Forget()</c>。
    /// </summary>
    public static class ActivityIds
    {
        /// <summary>累计游戏 N 局(activity.xlsx activity_id=5,target=100,详 47 §3.4)。</summary>
        public const int AccumulatePlayCount = 5;
    }
}
