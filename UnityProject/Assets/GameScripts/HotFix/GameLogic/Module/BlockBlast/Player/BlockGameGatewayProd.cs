using System.Collections.Generic;
using Cysharp.Threading.Tasks;
#if FANTASY_UNITY
using Fantasy;
#endif

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 服务端权威发牌环路 RPC 的生产实现(经 <c>FantasyClient.FantasyNetwork.Session</c> 发协议)。
    /// 沿 RpcGatewayProd / EnterMainGameGatewayProd 范式:发请求 → await → 空检查 → 跨边界拷贝字段 → 映射结果码。
    /// 协议对象在 await 后会被对象池回收,故响应字段(列表 / BlockGenState)在返回前逐一拷成中立类型。
    /// </summary>
    public sealed class BlockGameGatewayProd : IBlockGameGateway
    {
        public async UniTask<GameStartResult> GameStartAsync()
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
                return GameStartResult.Fail(DealResultCode.NetworkDown);
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
                return GameStartResult.Fail(DealResultCode.NotLoggedIn);

            G2C_GameStartResponse response;
            try { response = await session.C2G_GameStartRequest(); }
            catch { return GameStartResult.Fail(DealResultCode.ServiceUnavailable); }
            if (response == null) return GameStartResult.Fail(DealResultCode.ServiceUnavailable);

            return new GameStartResult(
                DealResultCode.Ok,
                response.GameId,
                response.Seed,
                CopyInts(response.InitialTrio),
                response.Step,
                CopyGen(response.GeneratorState),
                response.Resumed,
                response.Score,
                CopyInts(response.Board));
#else
            await UniTask.CompletedTask;
            return GameStartResult.Fail(DealResultCode.ServiceUnavailable);
#endif
        }

        public async UniTask<PlaceResult> PlaceAsync(long gameId, int baseStep, int candidateIndex, int posX, int posY)
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
                return PlaceResult.Fail(DealResultCode.NetworkDown);
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
                return PlaceResult.Fail(DealResultCode.NotLoggedIn);

            G2C_PlaceResponse response;
            try { response = await session.C2G_PlaceRequest(gameId, baseStep, candidateIndex, posX, posY); }
            catch { return PlaceResult.Fail(DealResultCode.ServiceUnavailable); }
            if (response == null) return PlaceResult.Fail(DealResultCode.ServiceUnavailable);

            return new PlaceResult(
                MapPlaceCode(response.ResultCode),
                response.Step,
                response.Score,
                response.EliminatedLines,
                response.NewCandidate,
                CopyInts(response.Board),
                CopyGen(response.GeneratorState),
                response.GameOver,
                response.FinalScore,
                response.BestScore);
#else
            await UniTask.CompletedTask;
            return PlaceResult.Fail(DealResultCode.ServiceUnavailable);
#endif
        }

        public async UniTask<SnapshotResult> GameSnapshotAsync(long gameId)
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
                return SnapshotResult.Fail(DealResultCode.NetworkDown);
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
                return SnapshotResult.Fail(DealResultCode.NotLoggedIn);

            G2C_GameSnapshotResponse response;
            try { response = await session.C2G_GameSnapshotRequest(gameId); }
            catch { return SnapshotResult.Fail(DealResultCode.ServiceUnavailable); }
            if (response == null) return SnapshotResult.Fail(DealResultCode.ServiceUnavailable);

            return new SnapshotResult(
                MapSnapshotCode(response.ResultCode),
                CopyInts(response.Board),
                response.Score,
                response.Step,
                CopyInts(response.CandidateQueue),
                CopyGen(response.GeneratorState));
#else
            await UniTask.CompletedTask;
            return SnapshotResult.Fail(DealResultCode.ServiceUnavailable);
#endif
        }

#if FANTASY_UNITY
        /// <summary>跨边界拷贝 int 列表(协议列表 await 后会被池回收)。</summary>
        private static List<int> CopyInts(List<int> src)
        {
            var dst = new List<int>(src != null ? src.Count : 0);
            if (src != null) dst.AddRange(src);
            return dst;
        }

        /// <summary>跨边界拷贝发牌器状态向量(含候选队列 + PRNG 游标 + LastAlgo/LastTierId)。</summary>
        private static GenStateView CopyGen(BlockGenState gen)
        {
            if (gen == null) return null;
            return new GenStateView(
                CopyInts(gen.CandidateQueue),
                gen.DynamicWeight,
                gen.PreDynamicWeight,
                gen.RefillIndex,
                gen.BcInWindow,
                gen.BcCooldown,
                // 协议以 int64 承载 ulong 位型;按位还原(unchecked 强转,不丢高位)。
                unchecked((ulong)gen.RngS0),
                unchecked((ulong)gen.RngS1),
                gen.LastAlgo,
                gen.LastTierId);
        }

        private static DealResultCode MapPlaceCode(PlaceResultCode code)
        {
            switch (code)
            {
                case PlaceResultCode.StepAdvanced: return DealResultCode.Ok;
                case PlaceResultCode.IdempotentReplay: return DealResultCode.IdempotentReplay;
                case PlaceResultCode.StepAhead: return DealResultCode.StepAhead;
                case PlaceResultCode.IllegalPlacement: return DealResultCode.IllegalPlacement;
                case PlaceResultCode.GameNotFound: return DealResultCode.GameNotFound;
                case PlaceResultCode.NotLoggedIn: return DealResultCode.NotLoggedIn;
                default: return DealResultCode.ServiceUnavailable;
            }
        }

        private static DealResultCode MapSnapshotCode(GameSnapshotResultCode code)
        {
            switch (code)
            {
                case GameSnapshotResultCode.Ok: return DealResultCode.Ok;
                case GameSnapshotResultCode.GameNotFound: return DealResultCode.GameNotFound;
                case GameSnapshotResultCode.NotLoggedIn: return DealResultCode.NotLoggedIn;
                default: return DealResultCode.ServiceUnavailable;
            }
        }
#endif
    }
}
