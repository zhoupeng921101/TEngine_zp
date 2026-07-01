using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 头像/框修饰客户端编排器(头像服务端权威·客户端段)。承接换装 / 解锁上报 / 登录 bootstrap 三条路径的客户端段:
    /// 本地投影 <see cref="PlayerInfo"/> 为唯一客户端载体(登录快照覆盖其当前佩戴 + 解锁集),所有变更经服务端裁定。
    /// </summary>
    /// <remarks>
    /// 纯逻辑(不依赖 UnityEngine):经注入的 <see cref="ICosmeticGateway"/> 发请求、直接读写传入的 <see cref="PlayerInfo"/>,
    /// 故可 EditMode 单测。
    ///
    /// 【换装】乐观 set(即时 UI)→ 发 <c>C2G_EquipCosmetic</c> → 对账:Success 用回带当前 id 覆盖(权威);
    /// NotUnlocked/InvalidKind 用回带当前 id 回滚乐观 set(响应回带的是服务端当前值);纯客户端失败(断网/服务不可用)
    /// 手动回滚到发前旧值(响应不可信)。
    ///
    /// 【解锁上报】发 <c>C2G_UnlockCosmetic</c> → Success 用回带集合覆盖本地对应集合(服务端权威);失败不动本地
    /// (等级实时判定仍能显示解锁态,下次 bootstrap / 事件重报补齐)。
    ///
    /// 【登录 bootstrap】snapshot 覆盖本地当前/解锁集后,把「客户端按等级算出的应解锁集」与「服务端快照集」做差,
    /// 对缺的每个 id 发一次 UnlockCosmetic 上报($addToSet 幂等,重登再报无害;只报差集,避免每登录全量重发)。
    ///
    /// 【解锁 vs 换装时序】换装校验「已解锁」用服务端解锁集。某 id 客户端刚算出解锁但 bootstrap 上报还在途时立刻换装,
    /// 可能被服务端判 NotUnlocked → 乐观 set 被回滚 + 提示(可接受)。本类不为消除该竞态做换装前强制上报——bootstrap
    /// 在登录快照到达时即触发,通常早于用户打开头像网格的手动换装。
    /// </remarks>
    public sealed class CosmeticService
    {
        private readonly ICosmeticGateway _gateway;

        public CosmeticService(ICosmeticGateway gateway)
        {
            _gateway = gateway ?? throw new System.ArgumentNullException(nameof(gateway));
        }

        /// <summary>
        /// 换装(乐观 set + 服务端对账)。<paramref name="p"/> 为本地投影,<paramref name="e"/> 为目标头像/框定义。
        /// 返回 RPC 结果供 UI 提示。本地校验(是否解锁)不在此做:服务端解锁集才是权威,客户端乐观 set 后由响应回滚。
        /// </summary>
        public async UniTask<EquipCosmeticResult> EquipAsync(PlayerInfo p, AvatarEntry e)
        {
            if (p == null || e == null)
                return EquipCosmeticResult.Rejected(EquipCosmeticOutcome.ServiceUnavailable);

            // 记发前旧值,供纯客户端失败(响应不可信)时手动回滚。
            int prevAvatar = p.CurrentAvatarId;
            int prevFrame = p.CurrentFrameId;

            // 乐观 set(即时 UI)。
            if (e.Type == AvatarType.Avatar) p.CurrentAvatarId = e.Id;
            else p.CurrentFrameId = e.Id;

            var result = await _gateway.EquipAsync(e.Type, e.Id);

            switch (result.Outcome)
            {
                case EquipCosmeticOutcome.Success:
                case EquipCosmeticOutcome.NotUnlocked:
                case EquipCosmeticOutcome.InvalidKind:
                    // 三码下响应回带了服务端当前权威 id → 直接覆盖(Success = 切换后;失败 = 回退到服务端当前值)。
                    p.CurrentAvatarId = result.CurrentAvatarId;
                    p.CurrentFrameId = result.CurrentFrameId;
                    break;
                default:
                    // NotLoggedIn / ServiceUnavailable / NetworkDown:响应值不可信 → 回滚到发前旧值。
                    p.CurrentAvatarId = prevAvatar;
                    p.CurrentFrameId = prevFrame;
                    break;
            }

            return result;
        }

        /// <summary>
        /// 上报一个新解锁 id(运行时新解锁:活动发放 / 等级升级产生)。Success 用回带集合覆盖本地对应集合;
        /// 失败不动本地(下次 bootstrap / 事件重报补齐)。id ≤ 0 或 kind 非法直接跳过、不发 RPC。
        /// </summary>
        public async UniTask<UnlockCosmeticResult> ReportUnlockAsync(PlayerInfo p, int kind, int id)
        {
            if (p == null || id <= 0 || (kind != AvatarType.Avatar && kind != AvatarType.Frame))
                return UnlockCosmeticResult.Rejected(kind, UnlockCosmeticOutcome.InvalidKind);

            var result = await _gateway.UnlockAsync(kind, id);
            if (result.Success)
                ApplyUnlockedSet(p, kind, result.UnlockedIds);
            return result;
        }

        /// <summary>
        /// 登录 bootstrap:把「客户端按等级算出的应解锁集」与「服务端快照集(已写入 <paramref name="p"/>)」做差,
        /// 对缺的 id 合成一批、一次 <c>C2G_UnlockCosmeticBatch</c> 上报($addToSet 幂等,重登再报无害;只报差集省流量)。
        /// Success 用回带的两个 kind 最终集覆盖本地投影。返回实际上报的 id 数(供日志/断言;0 = 无缺不发)。
        /// <paramref name="allEntries"/> 为全部头像/框定义(<c>AvatarConfigMgr.All()</c>);null/空则只兜默认 id。
        /// 默认头像/框(<see cref="PlayerInfo.DefaultAvatarId"/> / <see cref="PlayerInfo.DefaultFrameId"/>)也纳入应解锁集,
        /// 保证首登服务端空集时被补齐上报。
        /// </summary>
        public async UniTask<int> BootstrapUnlocksAsync(PlayerInfo p, IEnumerable<AvatarEntry> allEntries)
        {
            if (p == null) return 0;

            // 应解锁集 = 默认 id + 等级实时达标的 LEVEL 项(EVENT 项不算,靠活动发放时单独上报)。
            var wantAvatars = new List<int> { PlayerInfo.DefaultAvatarId };
            var wantFrames = new List<int> { PlayerInfo.DefaultFrameId };
            if (allEntries != null)
            {
                foreach (var e in allEntries)
                {
                    if (e == null) continue;
                    if (e.UnlockCond == UnlockCond.Level && p.Level >= e.UnlockParam)
                    {
                        if (e.Type == AvatarType.Avatar) AddDistinct(wantAvatars, e.Id);
                        else if (e.Type == AvatarType.Frame) AddDistinct(wantFrames, e.Id);
                    }
                }
            }

            // 收集两个 kind 里、服务端集合(本地投影)尚缺的 id,合成一批一次上报(N 条单发 → 1 条批量往返)。
            var missing = new List<(int kind, int id)>();
            CollectMissing(p, AvatarType.Avatar, wantAvatars, missing);
            CollectMissing(p, AvatarType.Frame, wantFrames, missing);
            if (missing.Count == 0) return 0; // 无缺:不发空请求

            var result = await _gateway.UnlockBatchAsync(missing);
            if (result.Success)
            {
                // 服务端回带两个 kind 的最终解锁集 → 覆盖本地投影(权威)。失败不动本地(下次 bootstrap 重报补齐)。
                ApplyUnlockedSet(p, AvatarType.Avatar, result.AvatarIds);
                ApplyUnlockedSet(p, AvatarType.Frame, result.FrameIds);
            }
            return missing.Count;
        }

        /// <summary>把 want 集里、服务端集合(本地投影)尚缺的 id 收进 <paramref name="into"/>(不发 RPC)。</summary>
        private static void CollectMissing(PlayerInfo p, int kind, List<int> want, List<(int kind, int id)> into)
        {
            for (int i = 0; i < want.Count; i++)
            {
                int id = want[i];
                if (SetContains(SetOf(p, kind), id)) continue; // 服务端已含,不重报
                into.Add((kind, id));
            }
        }

        // ── 本地投影集合读写 ────────────────────────────────────

        private static int[] SetOf(PlayerInfo p, int kind)
            => kind == AvatarType.Avatar ? p.UnlockedAvatarIds : p.UnlockedFrameIds;

        private static bool SetContains(int[] set, int id)
        {
            if (set == null) return false;
            for (int i = 0; i < set.Length; i++)
                if (set[i] == id) return true;
            return false;
        }

        /// <summary>用服务端回带集合覆盖本地对应集合(拷成独立数组)。</summary>
        private static void ApplyUnlockedSet(PlayerInfo p, int kind, IReadOnlyList<int> ids)
        {
            var arr = ToArray(ids);
            if (kind == AvatarType.Avatar) p.UnlockedAvatarIds = arr;
            else p.UnlockedFrameIds = arr;
        }

        private static int[] ToArray(IReadOnlyList<int> src)
        {
            if (src == null || src.Count == 0) return System.Array.Empty<int>();
            var dst = new int[src.Count];
            for (int i = 0; i < src.Count; i++) dst[i] = src[i];
            return dst;
        }

        private static void AddDistinct(List<int> list, int id)
        {
            if (!list.Contains(id)) list.Add(id);
        }
    }
}
