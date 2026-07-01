using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 头像/框修饰 RPC 接缝(头像服务端权威·客户端段)。把网络层(<c>FantasyClient.FantasyNetwork.Session</c> + 协议消息)
    /// 抽到接缝后,使换装 / 解锁上报编排逻辑可 EditMode 单测(沿 <see cref="IRenameGateway"/> / <see cref="IRpcGateway"/> 范式)。
    /// </summary>
    /// <remarks>
    /// 生产实现 = <see cref="CosmeticGatewayProd"/>(同目录,#if FANTASY_UNITY 包裹,内部调 Session.C2G_EquipCosmeticRequest /
    /// Session.C2G_UnlockCosmeticRequest);测试实现 = 桩(返预设结果,记调用参数)。返 <see cref="UniTask{T}"/> 不暴露 Fantasy.Async.FTask。
    /// 失败(网络断 / 超时 / 异常 / 空响应)以 Rejected(NetworkDown / ServiceUnavailable)返、<b>不抛异常</b>。
    /// 请求负载仅含 kind + id(身份服务端从会话取;佩戴合法性 / 解锁 sanity 服务端裁定,客户端不报权威集合)。
    /// </remarks>
    public interface ICosmeticGateway
    {
        /// <summary>发起换装请求。上报 kind(<see cref="AvatarType"/>:1 头像 / 2 框)+ 目标佩戴 id;服务端校验已解锁才切,响应回带最新当前 id。</summary>
        UniTask<EquipCosmeticResult> EquipAsync(int kind, int id);

        /// <summary>发起解锁上报请求。上报 kind + 待解锁 id;服务端 $addToSet 幂等 + sanity,响应回带更新后集合。</summary>
        UniTask<UnlockCosmeticResult> UnlockAsync(int kind, int id);

        /// <summary>
        /// 批量解锁上报:一次携带多项 (kind, id),服务端频率闸对整批只检一次、过闸后逐项 sanity + $addToSet 幂等,
        /// 回带两个 kind 的最终解锁集。用于登录 bootstrap 补齐多解锁,把 N 条单发收敛成 1 条往返。
        /// 失败(网络断 / 未登录 / 服务不可用 / 整批被闸挡)以 Rejected 返、<b>不抛异常</b>;空 items 视作无需上报,返 Rejected(调用方不对齐)。
        /// </summary>
        UniTask<UnlockCosmeticBatchResult> UnlockBatchAsync(IReadOnlyList<(int kind, int id)> items);
    }
}
