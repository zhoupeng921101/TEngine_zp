namespace GameLogic.Activity
{
    /// <summary>
    /// 活动累加结果码(设计 48 §3.1,5 档;比 47 §3.3 服务端多一档 <see cref="NetworkDown"/>,
    /// 沿 46 <c>AttrLedgerQueryCode</c> + 38 <c>ChangeReject</c> 范式区分「服务端裁决」与「客户端断网」)。
    /// </summary>
    /// <remarks>
    /// 整数值与协议 <c>ActivityIncrementResultCode</c> 一一对位(0..3),
    /// <see cref="NetworkDown"/> = 4 是客户端独占(RPC 到不了服务端时调用方层提早返,协议层无此值)。
    /// 客户端处理见设计 48 §3.8 错误码表(Success/Info、InvalidRequest+NotCumulative/Error、
    /// ServiceUnavailable+NetworkDown/Warning;任一返码均不弹 UI,沿 EVENT 无 UI 反馈同范式)。
    /// </remarks>
    public enum ActivityIncrementCode
    {
        /// <summary>累加成功(counter 已写;若 TargetReached=true 服务端已投活动邮件)。</summary>
        Success = 0,
        /// <summary>参数非法(activityId 不在配 / delta ≤ 0 / 字段缺)— 客户端 bug 或配置缺。</summary>
        InvalidRequest = 1,
        /// <summary>活动 type ≠ Cumulative — 客户端 bug 或配置缺(防客户端用此 RPC 推 Login 类活动 counter)。</summary>
        NotCumulative = 2,
        /// <summary>服务端不可用(MongoDB 不可达 / 活动配置未加载)— 本次累计丢弃(沿设计 48 §3.3 不本地缓存)。</summary>
        ServiceUnavailable = 3,
        /// <summary>客户端断网(Session 未建立 / 未登录 / RPC 抛异常 / 超时)— 本次累计丢弃。</summary>
        NetworkDown = 4,
    }
}
