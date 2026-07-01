using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 改名 RPC 接缝(改名服务端权威·客户端段)。把网络层(<c>FantasyClient.FantasyNetwork.Session</c> + 协议消息)
    /// 抽到接缝后,使改名编排逻辑可 EditMode 单测(沿 <see cref="IRpcGateway"/> / <see cref="IBlockGameGateway"/> 范式)。
    /// </summary>
    /// <remarks>
    /// 生产实现 = <see cref="RenameGatewayProd"/>(同目录,#if FANTASY_UNITY 包裹,内部调 Session.C2G_RenameRequest);
    /// 测试实现 = 桩(返预设 <see cref="RenameRpcResult"/>,记调用参数)。返 <see cref="UniTask{T}"/> 不暴露 Fantasy.Async.FTask。
    /// 失败(网络断 / 超时 / 异常 / 空响应)以 <see cref="RenameRpcResult.Rejected"/> 返、<b>不抛异常</b>。
    /// 请求负载仅含新昵称(身份 / 费用 / 次数服务端从会话 + PlayerDoc 自派生,沿反作弊红线:客户端不报费用/次数)。
    /// </remarks>
    public interface IRenameGateway
    {
        /// <summary>发起改名请求。仅上报新昵称;服务端一次原子做完算费 / 扣钻 / 写名 / 计数,响应回带最新权威值。</summary>
        UniTask<RenameRpcResult> SendRenameAsync(string newNickname);
    }
}
