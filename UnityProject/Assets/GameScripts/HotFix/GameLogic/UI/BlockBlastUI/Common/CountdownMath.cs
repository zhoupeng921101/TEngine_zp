namespace GameLogic
{
    /// <summary>
    /// 倒计时剩余秒的纯计算 helper（体力时基恢复 / 订单整批刷新等共用）。
    /// 纯函数、注入 now，不依赖真实时钟；由 CountdownWidget 展示层与各宿主窗口共用，避免各自内联同一套防御逻辑。
    /// </summary>
    public static class CountdownMath
    {
        /// <summary>
        /// 计算到下一次结算的剩余秒。
        /// <paramref name="periodic"/>=true（周期循环，如体力）：剩余 = interval - 已过秒 % interval（每满间隔重置）。
        /// <paramref name="periodic"/>=false（一次性到点，如订单）：剩余 = interval - 已过秒。
        /// 防御：interval ≤ 0（配置错）→ 返 0；lastTime ≤ 0（尚无记录 / 首次）或 now &lt; lastTime（时钟回拨）→ 显整周期 interval（不出负数）。
        /// </summary>
        public static int Remain(long now, long lastTime, int interval, bool periodic)
        {
            if (interval <= 0) return 0;
            if (lastTime <= 0 || now < lastTime) return interval; // 尚无记录 / 回拨：显整周期，避免负数错乱
            long elapsed = now - lastTime;
            long remain = periodic ? interval - (elapsed % interval) : interval - elapsed;
            if (remain < 0) remain = 0;       // 一次性：已过点（轮询将刷新，此刻先显 0）
            if (remain > interval) remain = interval;
            return (int)remain;
        }
    }
}
