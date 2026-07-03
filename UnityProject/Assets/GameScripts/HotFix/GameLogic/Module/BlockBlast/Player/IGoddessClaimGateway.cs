using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 女神满档领取 RPC 接缝(女神系统·客户端段)。把网络层(<c>FantasyClient.FantasyNetwork.Session</c> + 协议消息)
    /// 抽到接缝后,使领取编排逻辑可 EditMode 单测(沿 <see cref="IWishGateway"/> 范式)。
    /// </summary>
    /// <remarks>
    /// 生产实现 = <see cref="GoddessClaimGatewayProd"/>(同目录,#if FANTASY_UNITY 包裹,内部调 Session.C2G_GoddessClaimRequest);
    /// 测试实现 = 桩(返预设 <see cref="GoddessClaimResult"/>,记调用次数)。返 <see cref="UniTask{T}"/> 不暴露 Fantasy.Async.FTask。
    /// 失败(网络断 / 超时 / 异常 / 空响应)以 <see cref="GoddessClaimResult.Rejected"/> 返、<b>不抛异常</b>。
    /// 请求空载荷:身份 / 满档判定 / 奖励内容全部由服务端从会话 + PlayerDoc + 订单态 + 奖励表自派生(客户端不预领、不上报奖励)。
    /// </remarks>
    public interface IGoddessClaimGateway
    {
        /// <summary>发起女神满档领取请求(空载荷)。服务端原子做完满档校验 / 清零 / 组装奖励,响应回带元素类型 + 各档等级/数量。</summary>
        UniTask<GoddessClaimResult> ClaimAsync();
    }
}
