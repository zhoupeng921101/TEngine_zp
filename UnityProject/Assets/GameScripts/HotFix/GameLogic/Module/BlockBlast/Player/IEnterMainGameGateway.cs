using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 进主游戏一次性原子响应快照(已脱离 Fantasy 对象池,字段可安全跨边界持有)。
    /// 由 <see cref="IEnterMainGameGateway.EnterAsync"/> 把协议 <c>G2C_EnterMainGameResponse</c> 拷成框架中立值类型。
    /// </summary>
    /// <remarks>
    /// 携带两块同包回带:① 订单当前激活快照(<see cref="OrderSnapshot"/>,喂 <see cref="OrderSync.OnSnapshotPush"/>);
    /// ② 云存档下载结果(<see cref="CloudCode"/> / <see cref="CloudServerVersion"/> / <see cref="CloudServerBlob"/>,
    /// 喂 <see cref="CloudSaveSync.ApplyDownloadResolution"/>)。
    /// 任何往返失败 → <see cref="CloudCode"/>=ServiceUnavailable + <see cref="OrderSnapshot"/>=null(调用方各自走本地兜底)。
    /// </remarks>
    public readonly struct EnterMainGameResult
    {
        /// <summary>订单当前激活快照(已转框架中立 DTO;服务不可用 / 无订单 → null,调用方按"无订单"降级)。</summary>
        public readonly OrderSnapshotData OrderSnapshot;

        /// <summary>云存档下载结果码(沿 <see cref="CloudDownloadCode"/>)。</summary>
        public readonly CloudDownloadCode CloudCode;

        /// <summary>云存档服务端权威版本号(NoSnapshot / 失败 = 0)。</summary>
        public readonly long CloudServerVersion;

        /// <summary>云存档服务端 blob(NoSnapshot / 失败 = null)。</summary>
        public readonly byte[] CloudServerBlob;

        public EnterMainGameResult(OrderSnapshotData orderSnapshot,
            CloudDownloadCode cloudCode, long cloudServerVersion, byte[] cloudServerBlob)
        {
            OrderSnapshot = orderSnapshot;
            CloudCode = cloudCode;
            CloudServerVersion = cloudServerVersion;
            CloudServerBlob = cloudServerBlob;
        }

        /// <summary>服务不可用兜底:无订单快照 + 云存档 ServiceUnavailable(调用方保留本地)。</summary>
        public static EnterMainGameResult Unavailable()
            => new EnterMainGameResult(null, CloudDownloadCode.ServiceUnavailable, 0, null);
    }

    /// <summary>
    /// 进主游戏请求接缝(全栈协议改动·客户端段)。把网络层(<c>FantasyClient.FantasyNetwork.Session</c> + 协议消息
    /// <c>C2G_EnterMainGameRequest</c> / <c>G2C_EnterMainGameResponse</c>)抽到接缝后,使进主游戏编排(<see cref="EnterMainGameSync"/>)
    /// 保持纯逻辑可 EditMode 单测(沿 <see cref="ICloudSaveGateway"/> / <see cref="IOrderRpcGateway"/> 范式)。
    /// </summary>
    /// <remarks>
    /// 生产实现 = <see cref="EnterMainGameGatewayProd"/>(同目录,#if FANTASY_UNITY 包裹);测试实现 = 桩(返预设结果)。
    /// 返 <see cref="UniTask{T}"/> 而非 Fantasy.Async.FTask:接口面不暴露网络库类型。
    /// 任何往返失败(未连 / 未登 / 超时 / 异常)以 <see cref="EnterMainGameResult.Unavailable"/> 归一,<b>不抛异常</b>。
    /// </remarks>
    public interface IEnterMainGameGateway
    {
        /// <summary>发一次进主游戏请求,取服务端一次性原子响应(订单快照 + 云存档同包回带)。身份从会话取,空请求。</summary>
        UniTask<EnterMainGameResult> EnterAsync();
    }
}
