using Cysharp.Threading.Tasks;
#if FANTASY_UNITY
using Fantasy; // 协议消息 + NetworkProtocolHelper 扩展方法 C2G_ActivityIncrement 所在命名空间
#endif

namespace GameLogic.Activity
{
    /// <summary>
    /// 活动累加数据源生产实现(设计 48 §3.2)。
    /// 经 <c>FantasyClient.FantasyNetwork.Session</c> 发 <c>C2G_ActivityIncrement</c> 同步等响应,
    /// 把协议 <c>G2C_ActivityIncrementResponse</c> 反序列化为客户端 <see cref="ActivityIncrementResult"/>。
    /// </summary>
    /// <remarks>
    /// 降级(沿 38 RpcGatewayProd + 46 RemoteAttrLedgerSource 范式):未连接 / 未登录 / 发不出 / 超时 /
    /// 任何往返失败 → 返 <see cref="ActivityIncrementCode.NetworkDown"/>(客户端层);
    /// 服务端返非 Success 码 → 透传对应 <see cref="ActivityIncrementCode"/>;
    /// 响应 null → <see cref="ActivityIncrementCode.ServiceUnavailable"/>;<b>不抛异常</b>。
    /// 程序集边界:网络层 FantasyClient / Fantasy.Unity 受 FANTASY_UNITY 约束;
    /// 该 define 关闭的平台无网络可用,本类同样降级返 NetworkDown,使 GameLogic 在任何平台都可编译。
    /// 把「协议码 → 客户端枚举」的纯转换抽成非 FANTASY_UNITY-gated 静态方法
    /// (<see cref="MapResultCode"/>),可 EditMode 直测;FANTASY_UNITY-gated 的 <see cref="MapResponse"/>
    /// (读 Fantasy 协议字段)只薄薄调它,字段读取交真往返核(E1)。
    /// </remarks>
    public sealed class RemoteActivityIncrementSource : IActivityIncrementSource
    {
        public async UniTask<ActivityIncrementResult> IncrementAsync(int activityId, int delta)
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
            {
                return ActivityIncrementResult.NetworkDown; // 未连接:不发请求、不抛
            }
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
            {
                return ActivityIncrementResult.NetworkDown; // 未登录:身份从会话取,未登录则不发
            }

            G2C_ActivityIncrementResponse response;
            try
            {
                // FTask 自带 awaiter,可在 async UniTask 体内直接 await(memory「跨框架通用异步与网络库异步」)
                TEngine.Log.Info($"[Fantasy] 发送活动自增 activityId={activityId} delta={delta}");
                response = await session.C2G_ActivityIncrement(activityId, delta);
            }
            catch
            {
                return ActivityIncrementResult.NetworkDown; // 发不出 / 超时 / 往返异常:降级,不抛
            }

            if (response == null)
            {
                return ActivityIncrementResult.ServiceUnavailable;
            }

            return MapResponse(response);
#else
            // FANTASY_UNITY 关闭(无网络平台):降级返 NetworkDown,不抛
            await UniTask.CompletedTask;
            return ActivityIncrementResult.NetworkDown;
#endif
        }

        /// <summary>
        /// 协议结果码(int) → 客户端 <see cref="ActivityIncrementCode"/>;未知码按 ServiceUnavailable 兜底,不崩。
        /// 协议层无 NetworkDown(NetworkDown 是客户端独占,RPC 到不了服务端则在调用方层提早返)。
        /// </summary>
        public static ActivityIncrementCode MapResultCode(int rawCode)
        {
            switch (rawCode)
            {
                case 0: return ActivityIncrementCode.Success;            // ActivityIncrementResultCode.Success
                case 1: return ActivityIncrementCode.InvalidRequest;     // InvalidRequest
                case 2: return ActivityIncrementCode.NotCumulative;      // NotCumulative
                case 3: return ActivityIncrementCode.ServiceUnavailable; // ServiceUnavailable
                default: return ActivityIncrementCode.ServiceUnavailable;
            }
        }

#if FANTASY_UNITY
        /// <summary>把协议响应转客户端 <see cref="ActivityIncrementResult"/>(薄壳,读 Fantasy 协议字段后调纯转换)。</summary>
        private static ActivityIncrementResult MapResponse(G2C_ActivityIncrementResponse response)
        {
            var code = MapResultCode((int)response.ResultCode);
            if (code != ActivityIncrementCode.Success)
            {
                // 失败分支:counter / 达标位均为默认(协议响应虽可能有值,客户端按设计 48 §3.1 统一忽略)
                return new ActivityIncrementResult(code);
            }
            return new ActivityIncrementResult(
                ActivityIncrementCode.Success,
                response.CurrentCounter,
                response.TargetReached);
        }
#endif
    }
}
