using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 档案状态(皮肤态 + 神庙装饰厅数)SET 上报 RPC 接缝(皮肤/神庙装饰服务端权威·客户端段)。
    /// 把网络层(<c>FantasyClient.FantasyNetwork.Session</c> + 协议消息)抽到接缝后,使上报编排逻辑可 EditMode 单测
    /// (沿 <see cref="ICosmeticGateway"/> / <see cref="IWishGateway"/> / <see cref="IRenameGateway"/> 范式)。
    /// </summary>
    /// <remarks>
    /// 生产实现 = <see cref="ProfileStateGatewayProd"/>(同目录,#if FANTASY_UNITY 包裹,内部调 Session.C2G_SetProfileStateRequest);
    /// 测试实现 = 桩(返预设 <see cref="ProfileStateResult"/>,记调用参数)。返 <see cref="UniTask{T}"/> 不暴露 Fantasy.Async.FTask。
    /// 失败(网络断 / 超时 / 异常 / 空响应)以 <see cref="ProfileStateResult.Rejected"/> 返、<b>不抛异常</b>。
    /// SET 语义:每次全量带三态(不带增量);低频低危 fire-and-forget,失败下次变更再报 / 登录快照对齐,客户端不据响应对账。
    /// </remarks>
    public interface IProfileStateGateway
    {
        /// <summary>
        /// 发起档案状态 SET 上报。全量带三态:是否单色(0/1)+ 当前单色 id(彩色态 -1)+ 已装饰厅数标量(前缀语义)。
        /// 服务端原子 $set + sanity。
        /// </summary>
        UniTask<ProfileStateResult> SetProfileStateAsync(int skinMono, int skinMonoId, int templeDecoratedCount);
    }
}
