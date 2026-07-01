namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 祈愿(每日限领体力)RPC 裁决结果码(客户端侧)。映射服务端 <c>Fantasy.WishForEnergyResultCode</c>
    /// + 客户端额外网络层失败码。灵力扣减 / 体力发放(夹软上限)/ 每日次数 +1 / 懒每日重置全部由服务端一次原子完成,
    /// 客户端不本地预扣、不本地补体力、不本地跨天重置——只按响应回带的权威值对齐投影。
    /// </summary>
    public enum WishOutcome
    {
        /// <summary>成功:服务端已扣灵力、已发体力(夹软上限)、WishUsedToday+1;响应回带最新权威值供客户端对账。</summary>
        Success = 0,
        /// <summary>今日祈愿次数已达上限(服务端懒重置后仍满),不扣不发。</summary>
        DailyLimitReached = 1,
        /// <summary>灵力不足,未发体力。</summary>
        NotEnoughSoul = 2,
        /// <summary>未登录(会话未挂账号)→ 客户端重登。</summary>
        NotLoggedIn = 3,
        /// <summary>服务不可用(未连接 / 超时 / 写库异常 / 空响应)。</summary>
        ServiceUnavailable = 4,
        /// <summary>网络断 / 未连接(客户端层,未发出请求)。</summary>
        NetworkDown = 5,
    }

    /// <summary>
    /// 祈愿 RPC 结果(客户端侧)。<see cref="SoulPower"/>/<see cref="Energy"/>/<see cref="WishUsedToday"/>/<see cref="WishDailyLimit"/>
    /// 在服务端返回的响应码下(Success / DailyLimitReached / NotEnoughSoul)是服务端当前权威值,可用于对齐投影 + 算今日剩余次数;
    /// 纯客户端失败(NetworkDown / ServiceUnavailable 未收到响应)时不可信(缺省 0)。
    /// </summary>
    public readonly struct WishRpcResult
    {
        /// <summary>是否祈愿成功(等价 <see cref="Outcome"/> = Success)。</summary>
        public readonly bool Success;
        /// <summary>结果码(成功时 = Success)。</summary>
        public readonly WishOutcome Outcome;
        /// <summary>服务端当前权威灵力(成功 = 扣后;失败回带 = 当前余额供对齐;不可信码下 = 0)。</summary>
        public readonly long SoulPower;
        /// <summary>服务端当前权威体力(成功 = 发后夹软上限;失败回带 = 当前值;不可信码下 = 0)。</summary>
        public readonly long Energy;
        /// <summary>服务端当前权威今日已用祈愿次数(成功 = +1 后;失败回带 = 当前值;不可信码下 = 0)。</summary>
        public readonly int WishUsedToday;
        /// <summary>服务端权威每日祈愿次数上限(供客户端算今日剩余 = 上限 - 已用;不可信码下 = 0)。</summary>
        public readonly int WishDailyLimit;

        public WishRpcResult(bool success, WishOutcome outcome, long soulPower, long energy,
            int wishUsedToday, int wishDailyLimit)
        {
            Success = success;
            Outcome = outcome;
            SoulPower = soulPower;
            Energy = energy;
            WishUsedToday = wishUsedToday;
            WishDailyLimit = wishDailyLimit;
        }

        /// <summary>成功:回带服务端最新灵力 / 体力 / 今日已用次数 / 每日上限。</summary>
        public static WishRpcResult Ok(long soulPower, long energy, int wishUsedToday, int wishDailyLimit)
            => new WishRpcResult(true, WishOutcome.Success, soulPower, energy, wishUsedToday, wishDailyLimit);

        /// <summary>
        /// 拒绝:回带服务端当前权威值(DailyLimitReached / NotEnoughSoul)供对齐;纯客户端失败(NetworkDown /
        /// ServiceUnavailable 未收响应 / NotLoggedIn)时缺省 0(调用方按 <paramref name="outcome"/> 决定是否对齐)。
        /// </summary>
        public static WishRpcResult Rejected(WishOutcome outcome, long soulPower = 0L, long energy = 0L,
            int wishUsedToday = 0, int wishDailyLimit = 0)
            => new WishRpcResult(false, outcome, soulPower, energy, wishUsedToday, wishDailyLimit);
    }
}
