using Cysharp.Threading.Tasks;
#if FANTASY_UNITY
using Fantasy; // 协议消息 + NetworkProtocolHelper 扩展方法 C2G_EquipCosmeticRequest / C2G_UnlockCosmeticRequest 所在命名空间
#endif

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 头像/框修饰 RPC 接缝生产实现(头像服务端权威·客户端段)。经 <c>FantasyClient.FantasyNetwork.Session</c>
    /// 发 <c>C2G_EquipCosmeticRequest(kind,id)</c> / <c>C2G_UnlockCosmeticRequest(kind,id)</c> 同步等响应,
    /// 把服务端结果码 + 权威值转 <see cref="EquipCosmeticResult"/> / <see cref="UnlockCosmeticResult"/>。
    /// </summary>
    /// <remarks>
    /// 降级(沿 <see cref="RenameGatewayProd"/> 范式):未连接 / 未登录 / 发不出 / 超时 / 空响应 → 以
    /// Rejected(NetworkDown / NotLoggedIn / ServiceUnavailable)返、<b>不抛异常</b>,由调用方据 Outcome 决定是否对齐视图。
    /// 程序集边界:网络层受 FANTASY_UNITY 约束,该 define 关闭的平台无网络可用,本类降级返 ServiceUnavailable,
    /// 使 GameLogic 在任何平台都可编译。
    /// </remarks>
    public sealed class CosmeticGatewayProd : ICosmeticGateway
    {
        public async UniTask<EquipCosmeticResult> EquipAsync(int kind, int id)
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
            {
                return EquipCosmeticResult.Rejected(EquipCosmeticOutcome.NetworkDown);
            }
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
            {
                return EquipCosmeticResult.Rejected(EquipCosmeticOutcome.NotLoggedIn);
            }

            G2C_EquipCosmeticResponse response;
            try
            {
                response = await session.C2G_EquipCosmeticRequest(kind, id);
            }
            catch
            {
                return EquipCosmeticResult.Rejected(EquipCosmeticOutcome.ServiceUnavailable);
            }

            if (response == null)
            {
                return EquipCosmeticResult.Rejected(EquipCosmeticOutcome.ServiceUnavailable);
            }

            return MapEquip(response);
#else
            await UniTask.CompletedTask;
            return EquipCosmeticResult.Rejected(EquipCosmeticOutcome.ServiceUnavailable);
#endif
        }

        public async UniTask<UnlockCosmeticResult> UnlockAsync(int kind, int id)
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
            {
                return UnlockCosmeticResult.Rejected(kind, UnlockCosmeticOutcome.NetworkDown);
            }
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
            {
                return UnlockCosmeticResult.Rejected(kind, UnlockCosmeticOutcome.NotLoggedIn);
            }

            G2C_UnlockCosmeticResponse response;
            try
            {
                response = await session.C2G_UnlockCosmeticRequest(kind, id);
            }
            catch
            {
                return UnlockCosmeticResult.Rejected(kind, UnlockCosmeticOutcome.ServiceUnavailable);
            }

            if (response == null)
            {
                return UnlockCosmeticResult.Rejected(kind, UnlockCosmeticOutcome.ServiceUnavailable);
            }

            return MapUnlock(kind, response);
#else
            await UniTask.CompletedTask;
            return UnlockCosmeticResult.Rejected(kind, UnlockCosmeticOutcome.ServiceUnavailable);
#endif
        }

#if FANTASY_UNITY
        /// <summary>把换装协议响应转客户端 <see cref="EquipCosmeticResult"/>(含服务端权威当前头像 / 框 id)。</summary>
        private static EquipCosmeticResult MapEquip(G2C_EquipCosmeticResponse response)
        {
            switch ((EquipCosmeticResultCode)response.ResultCode)
            {
                case EquipCosmeticResultCode.Success:
                    return EquipCosmeticResult.Ok(response.CurrentAvatarId, response.CurrentFrameId);
                case EquipCosmeticResultCode.NotUnlocked:
                    return EquipCosmeticResult.Rejected(EquipCosmeticOutcome.NotUnlocked, response.CurrentAvatarId, response.CurrentFrameId);
                case EquipCosmeticResultCode.InvalidKind:
                    return EquipCosmeticResult.Rejected(EquipCosmeticOutcome.InvalidKind, response.CurrentAvatarId, response.CurrentFrameId);
                case EquipCosmeticResultCode.NotLoggedIn:
                    return EquipCosmeticResult.Rejected(EquipCosmeticOutcome.NotLoggedIn);
                case EquipCosmeticResultCode.ServiceUnavailable:
                    return EquipCosmeticResult.Rejected(EquipCosmeticOutcome.ServiceUnavailable);
                default:
                    return EquipCosmeticResult.Rejected(EquipCosmeticOutcome.ServiceUnavailable); // 未知码兜底
            }
        }

        /// <summary>把解锁上报协议响应转客户端 <see cref="UnlockCosmeticResult"/>(含服务端更新后集合)。</summary>
        private static UnlockCosmeticResult MapUnlock(int kind, G2C_UnlockCosmeticResponse response)
        {
            switch ((UnlockCosmeticResultCode)response.ResultCode)
            {
                case UnlockCosmeticResultCode.Success:
                    // 复制为独立数组:Fantasy 协议对象用完即回池,跨边界须复制避免引用悬空。
                    return UnlockCosmeticResult.Ok(kind, CopyIds(response.UnlockedIds));
                case UnlockCosmeticResultCode.InvalidKind:
                    return UnlockCosmeticResult.Rejected(kind, UnlockCosmeticOutcome.InvalidKind);
                case UnlockCosmeticResultCode.InvalidId:
                    return UnlockCosmeticResult.Rejected(kind, UnlockCosmeticOutcome.InvalidId);
                case UnlockCosmeticResultCode.SetFull:
                    return UnlockCosmeticResult.Rejected(kind, UnlockCosmeticOutcome.SetFull);
                case UnlockCosmeticResultCode.RateLimited:
                    return UnlockCosmeticResult.Rejected(kind, UnlockCosmeticOutcome.RateLimited);
                case UnlockCosmeticResultCode.NotLoggedIn:
                    return UnlockCosmeticResult.Rejected(kind, UnlockCosmeticOutcome.NotLoggedIn);
                case UnlockCosmeticResultCode.ServiceUnavailable:
                    return UnlockCosmeticResult.Rejected(kind, UnlockCosmeticOutcome.ServiceUnavailable);
                default:
                    return UnlockCosmeticResult.Rejected(kind, UnlockCosmeticOutcome.ServiceUnavailable);
            }
        }

        private static int[] CopyIds(System.Collections.Generic.List<int> src)
        {
            if (src == null || src.Count == 0) return System.Array.Empty<int>();
            var dst = new int[src.Count];
            for (int i = 0; i < src.Count; i++) dst[i] = src[i];
            return dst;
        }
#endif
    }
}
