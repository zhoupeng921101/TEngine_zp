using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 祈愿(每日限领体力)RPC 接缝(祈愿服务端权威·客户端段)。把网络层(<c>FantasyClient.FantasyNetwork.Session</c> +
    /// 协议消息)抽到接缝后,使祈愿编排逻辑可 EditMode 单测(沿 <see cref="IRenameGateway"/> / <see cref="IRpcGateway"/> 范式)。
    /// </summary>
    /// <remarks>
    /// 生产实现 = <see cref="WishGatewayProd"/>(同目录,#if FANTASY_UNITY 包裹,内部调 Session.C2G_WishForEnergyRequest);
    /// 测试实现 = 桩(返预设 <see cref="WishRpcResult"/>,记调用次数)。返 <see cref="UniTask{T}"/> 不暴露 Fantasy.Async.FTask。
    /// 失败(网络断 / 超时 / 异常 / 空响应)以 <see cref="WishRpcResult.Rejected"/> 返、<b>不抛异常</b>。
    /// 请求空载荷:身份 / 灵力 / 次数 / 每日重置全部由服务端从会话 + PlayerDoc 自派生(祈愿低频每日限领,客户端不预扣、不上报量)。
    /// </remarks>
    public interface IWishGateway
    {
        /// <summary>发起祈愿请求(空载荷)。服务端一次原子做完懒每日重置 / 次数闸 / 扣灵力 / 发体力(夹软上限) / 计数 +1,响应回带最新权威值。</summary>
        UniTask<WishRpcResult> WishAsync();
    }
}
