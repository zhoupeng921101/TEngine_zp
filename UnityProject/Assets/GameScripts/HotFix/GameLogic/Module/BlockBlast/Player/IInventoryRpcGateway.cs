using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 使用道具 RPC 接缝(背包系统·客户端段)。把网络层抽到接缝后,<see cref="InventoryService"/> 保持纯逻辑可 EditMode 单测。
    /// </summary>
    /// <remarks>
    /// 生产实现 = <see cref="InventoryRpcGatewayProd"/>(同目录,#if FANTASY_UNITY,内部调 Session.C2G_UseItem);
    /// 测试实现 = 桩(返预设 <see cref="UseItemResult"/> + 记录调用参数,断言 reqSeq 单调)。
    /// 返 <see cref="UniTask{T}"/> 不暴露网络库类型(沿 <see cref="IRpcGateway"/> / <see cref="ITarotRpcGateway"/> 范式)。
    /// </remarks>
    public interface IInventoryRpcGateway
    {
        /// <summary>
        /// 发起使用道具请求。负载 = itemId + count + reqSeq(身份从会话取,不携带账号)。
        /// reqSeq 由 <see cref="InventoryService"/> 全账号单调递增签发,服务端据此幂等去重。
        /// 失败(网络断 / 超时 / 异常)以 <see cref="UseItemCode.NetworkDown"/> / <see cref="UseItemCode.ServiceUnavailable"/> 返,<b>不抛异常</b>。
        /// </summary>
        UniTask<UseItemResult> SendUseItemAsync(int itemId, long count, long reqSeq);
    }
}
