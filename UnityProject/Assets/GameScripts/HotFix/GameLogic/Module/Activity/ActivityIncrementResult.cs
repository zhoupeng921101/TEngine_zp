namespace GameLogic.Activity
{
    /// <summary>
    /// 一次 Increment 的结构化结果(设计 48 §3.1;3 字段映射协议 <c>G2C_ActivityIncrementResponse</c>;
    /// 不含额外内部字段,服务端独占审计完整性沿 47 §四 不本地放行)。
    /// </summary>
    /// <remarks>
    /// 失败分支(<see cref="Code"/> != Success):<see cref="CurrentCounter"/>=0 + <see cref="TargetReached"/>=false。
    /// fire-and-forget 调用方(GameOver hook)据 <see cref="Code"/> 落日志即止,
    /// 不弹 UI、不重试、不缓存(沿设计 48 §3.7 无 UI 反馈 + §3.3 不本地缓存)。
    /// </remarks>
    public readonly struct ActivityIncrementResult
    {
        /// <summary>结果码(5 档,见 <see cref="ActivityIncrementCode"/>)。</summary>
        public readonly ActivityIncrementCode Code;
        /// <summary>写后 counter 值(仅 Code=Success 时有意义,其它码 0)。</summary>
        public readonly long CurrentCounter;
        /// <summary>本次是否首次达标 + 抢占成功 + 已投奖(仅 Code=Success 时有意义;true ↔ 服务端已投活动邮件)。</summary>
        public readonly bool TargetReached;

        public ActivityIncrementResult(ActivityIncrementCode code, long currentCounter = 0L, bool targetReached = false)
        {
            Code = code;
            CurrentCounter = currentCounter;
            TargetReached = targetReached;
        }

        /// <summary>服务端不可用快捷构造(无 counter / 无达标)。</summary>
        public static ActivityIncrementResult ServiceUnavailable
            => new ActivityIncrementResult(ActivityIncrementCode.ServiceUnavailable);

        /// <summary>客户端断网快捷构造(无 counter / 无达标)。</summary>
        public static ActivityIncrementResult NetworkDown
            => new ActivityIncrementResult(ActivityIncrementCode.NetworkDown);

        /// <summary>非法请求快捷构造(无 counter / 无达标)。</summary>
        public static ActivityIncrementResult InvalidRequest
            => new ActivityIncrementResult(ActivityIncrementCode.InvalidRequest);
    }
}
