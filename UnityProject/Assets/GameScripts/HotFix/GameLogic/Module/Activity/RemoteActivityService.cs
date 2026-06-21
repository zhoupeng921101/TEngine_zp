using System;
using Cysharp.Threading.Tasks;
using TEngine;

namespace GameLogic.Activity
{
    /// <summary>
    /// 远程活动服务(编排层,设计 48 §3.3,沿 46 <c>RemoteAttrLedgerService</c> + 32 <c>RemoteMailService</c> 范式)。
    /// 持 <see cref="IActivityIncrementSource"/> 接缝,提供 <see cref="IncrementAsync"/> 转发 + 错误码归一。
    /// 另外提供 <see cref="IncrementAndLogAsync"/> 便利方法 — 业务出口(GameOver hook)统一调它,
    /// 据 Code 分级落日志,沿设计 48 §3.8 错误码客户端处理。
    /// </summary>
    /// <remarks>
    /// 本子单当前与接缝层 1:1 转发,但保留服务层便于 Tier 4+ 加交叉编排 / 批量上报 / 重试。
    /// <b>不持本地状态</b>(无缓存、无 pending 队列):每次调真请求,守 47 §四 服务端独占审计完整性
    /// (沿设计 48 §3.3「不本地放行」,与 30 兑换码 / 32 邮件领奖 / 46 ledger query 同口径)。
    /// 异步:经一次 RPC 往返,方法 <c>UniTask</c>(框架通用异步类型,与网络层 FTask 解耦);不阻塞、不抛
    /// (失败返对应 <see cref="ActivityIncrementCode"/>)。
    /// </remarks>
    public sealed class RemoteActivityService
    {
        private readonly IActivityIncrementSource _source;

        public RemoteActivityService(IActivityIncrementSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>
        /// 累加一次活动 counter(设计 48 §3.3 API);转发到注入的 <see cref="IActivityIncrementSource"/>。
        /// 失败时返结构化结果码,不抛。
        /// </summary>
        /// <param name="activityId">目标活动 id(沿 activity.xlsx.activity_id;不存在返 InvalidRequest)。</param>
        /// <param name="delta">本次累加增量(必须 > 0;服务端 47 §3.5 钳制上限 10000)。</param>
        public UniTask<ActivityIncrementResult> IncrementAsync(int activityId, int delta)
        {
            return _source.IncrementAsync(activityId, delta);
        }

        /// <summary>
        /// 业务出口便利:累加 + 按结果码分级落日志(设计 48 §3.8)。
        /// fire-and-forget 调用方(GameOver hook)统一 <c>.Forget()</c> 此入口,
        /// 避免在每处 hook 重复写日志分支。返码原样回传(便于单测断言)。
        /// </summary>
        /// <remarks>
        /// 日志级别:Success/Info(达标时附额外 Info)、InvalidRequest+NotCumulative/Error(代码 bug)、
        /// ServiceUnavailable+NetworkDown/Warning(环境抖动);任何返码均不弹 UI、不重试、不缓存
        /// (沿设计 48 §3.7 无 UI 反馈 + §3.3 不本地缓存)。
        /// </remarks>
        public async UniTask<ActivityIncrementResult> IncrementAndLogAsync(int activityId, int delta)
        {
            var result = await IncrementAsync(activityId, delta);
            LogResult(activityId, delta, result);
            return result;
        }

        /// <summary>纯函数 — 按返码分级落日志(EditMode 可不依赖单例直测;实现期间替换为可注入 ILogSink 不变签名)。</summary>
        public static void LogResult(int activityId, int delta, ActivityIncrementResult result)
        {
            switch (result.Code)
            {
                case ActivityIncrementCode.Success:
                    Log.Info($"[Activity] +{delta} → counter={result.CurrentCounter}, targetReached={result.TargetReached}");
                    if (result.TargetReached)
                    {
                        Log.Info($"[Activity] 累计达标 activity={activityId}");
                    }
                    break;
                case ActivityIncrementCode.InvalidRequest:
                    Log.Error($"[Activity] InvalidRequest activity={activityId} delta={delta} — 客户端 bug 或配置缺");
                    break;
                case ActivityIncrementCode.NotCumulative:
                    Log.Error($"[Activity] NotCumulative activity={activityId} — 客户端传错 id 或活动类型不符");
                    break;
                case ActivityIncrementCode.ServiceUnavailable:
                    Log.Warning($"[Activity] ServiceUnavailable activity={activityId} — 本次累计丢弃");
                    break;
                case ActivityIncrementCode.NetworkDown:
                    Log.Warning($"[Activity] NetworkDown activity={activityId} — 本次累计丢弃");
                    break;
            }
        }
    }
}
