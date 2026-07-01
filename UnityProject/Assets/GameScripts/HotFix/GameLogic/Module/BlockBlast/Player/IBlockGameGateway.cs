using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 服务端权威发牌环路的 RPC 接缝(GameStart / Place / GameSnapshot)。
    /// 把网络层(<c>FantasyClient.FantasyNetwork.Session</c> + 协议消息)抽到接缝后,
    /// 使 <see cref="ServerDealSync"/> 保持纯逻辑可 EditMode 单测(沿 IRpcGateway / IOrderRpcGateway 范式)。
    /// </summary>
    /// <remarks>
    /// 生产实现 = <see cref="BlockGameGatewayProd"/>(#if FANTASY_UNITY 包裹,内部调 Session.C2G_*);
    /// 测试实现 = 桩(返预设结果、记调用)。返 <see cref="UniTask{T}"/> 不暴露 Fantasy.Async.FTask。
    /// 失败(网络断 / 超时 / 异常 / 空响应)以 Code != Ok 返、<b>不抛异常</b>。
    /// </remarks>
    public interface IBlockGameGateway
    {
        /// <summary>开新局:服务端签发 gameId + seed + 首批 trio + step=0 + 发牌器初态。空请求。</summary>
        UniTask<GameStartResult> GameStartAsync();

        /// <summary>
        /// 落子:只上报玩家输入(候选槽位 + 落点),形状服务端权威。
        /// baseStep = 客户端当前权威步号,服务端按 ==/&lt;/&gt; 分三分支(推进 / 幂等 / 超前)。
        /// </summary>
        UniTask<PlaceResult> PlaceAsync(long gameId, int baseStep, int candidateIndex, int posX, int posY);

        /// <summary>取本局权威快照(恢复 / 重连用):board / score / step / 候选队列 / 发牌器态。</summary>
        UniTask<SnapshotResult> GameSnapshotAsync(long gameId);

        /// <summary>
        /// 消除道具:只上报玩家输入(gameId + baseStep + 目标格 posX/posY),服务端权威扣体力 + 清整行整列 + 推进 Step。
        /// baseStep = 客户端预测推进前的权威步号,服务端按 ==/&lt;/&gt; 分三分支(执行 / 幂等 / 超前)。
        /// 响应回带最新权威态(board/step/genState)+ 体力绝对值(NewEnergy),供宿主对账 + 体力校正。
        /// </summary>
        UniTask<ClearToolResult> ClearToolAsync(long gameId, int baseStep, int posX, int posY);
    }
}
