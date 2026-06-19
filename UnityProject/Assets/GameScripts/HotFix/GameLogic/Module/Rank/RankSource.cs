using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
#if FANTASY_UNITY
using Fantasy; // 协议消息类型 + NetworkProtocolHelper 扩展方法 C2G_RankSubmitScoreRequest/C2G_RankQueryRequest 所在命名空间
#endif

namespace GameLogic.Rank
{
    /// <summary>
    /// 排名数据源接缝（设计 22 §3.6 / 设计 31 §六）。
    /// 给一个榜 id，返回「榜上有哪些参赛记录」（原始未排序），由 <see cref="RankService"/> 排序产出名次。
    /// 离线注 <see cref="LocalRankSource"/>（本机 + 陪榜，客户端排序）；上后端注 <see cref="RemoteRankSource"/>（远程已排好，短路本地排序）。
    /// </summary>
    public interface IRankSource
    {
        /// <summary>取该榜的参赛记录（未排序）。含本机一条（IsSelf=true）+ 若干陪榜。</summary>
        IReadOnlyList<RankEntry> Fetch(int rankId);
    }

    /// <summary>
    /// 远程数据源能力（设计 31 §六 / CV1-CV2）：服务端权威排序，客户端短路本地排序。
    /// 远程源已由服务端排好序 + 算好名次，故不走 <see cref="IRankSource.Fetch"/> 的「返原始记录 → 客户端排序」路径，
    /// 而是直接产出已排序的查询快照（<see cref="RankBoard"/>），由 <see cref="RankService.GetBoardAsync"/> 取用。
    /// </summary>
    /// <remarks>
    /// 两条源对服务层产出同一快照结构（<see cref="RankBoard"/>），只是名次由谁算不同：
    /// 本地源 = 客户端排序；远程源 = 服务端排好、客户端短路排序（设计 31 §六）。
    /// 降级（设计 31 §四 / CV1）：断服 / 超时 / 服务不可用时返回 null，由服务层回退本地源（不阻断玩法、不伪造全服名次）。
    /// </remarks>
    public interface IRemoteRankSource : IRankSource
    {
        /// <summary>
        /// 上报一次成绩（玩法结束提交，设计 31 §3.1）：发上报协议、await 裁决。
        /// 身份从会话取、请求不自报账号（CV3 / SV8）。断服 / 超时 / 不可用返
        /// <see cref="RankSubmitOutcome.ServiceUnavailable"/>（不抛）；调用方降级走本地源。
        /// </summary>
        UniTask<RankSubmitOutcome> SubmitScoreAsync(int rankId, long score);

        /// <summary>
        /// 查一个榜（设计 31 §3.4）：发查榜协议、await 服务端已排好的前 N 名 + 自己名次，
        /// 转成设计 22 查询快照（<see cref="RankBoard"/>，短路本地排序，CV2）。
        /// 断服 / 超时 / 服务不可用 / 榜不存在 → 返 null（不抛），调用方回退本地源（CV1）。
        /// </summary>
        UniTask<RankBoard> QueryBoardAsync(int rankId);
    }

    /// <summary>
    /// 上报裁决结果（设计 31 §3.2）：结果码 + 服务端当前最佳成绩。
    /// </summary>
    public enum RankSubmitCode
    {
        /// <summary>够入榜要求且高于已存最佳 → 服务端已刷新（BestScore = 本次成绩）。</summary>
        BestRefreshed = 0,
        /// <summary>够入榜要求但不高于已存最佳 → 接受但不更新（BestScore = 已存最佳）。</summary>
        BestNotRefreshed = 1,
        /// <summary>低于该榜入榜要求 → 不进榜、不写存储。</summary>
        BelowEnterRequirement = 2,
        /// <summary>榜 id 在服务端查不到（不崩，以结果码回包）。</summary>
        RankNotFound = 3,
        /// <summary>服务不可用 / 断服 / 超时 → 不阻断玩法，稍后重连可重报（设计 31 §四）。</summary>
        ServiceUnavailable = 4,
    }

    /// <summary>
    /// 一次上报的结果（设计 31 §3.2）：结果码 + 服务端当前最佳成绩（便于显示「你的最佳分」）。
    /// </summary>
    public readonly struct RankSubmitOutcome
    {
        /// <summary>裁决结果码。</summary>
        public readonly RankSubmitCode Code;
        /// <summary>服务端当前最佳成绩（无成绩 = 0）。</summary>
        public readonly long BestScore;

        public RankSubmitOutcome(RankSubmitCode code, long bestScore)
        {
            Code = code;
            BestScore = bestScore;
        }

        /// <summary>服务不可用快捷构造（最佳分 0）。</summary>
        public static RankSubmitOutcome ServiceUnavailable => new RankSubmitOutcome(RankSubmitCode.ServiceUnavailable, 0L);
    }

    /// <summary>
    /// 离线本地源（可跑可测，设计 22 §3.6）：本机最佳成绩（注入 / 来自持久化）+ 配置陪榜成绩 → 一组参赛记录。
    /// 「陪榜」= 配置 / 注入的基准成绩（NPC 名 textId 占位 + 分数），使单机也有一份可排序的榜。
    /// 不引入随机生成 NPC（表现层 / 运营内容，O2）；不连网。
    /// </summary>
    public sealed class LocalRankSource : IRankSource
    {
        private readonly Func<int, (long score, long ticks, int nameTextId)> _selfProvider; // 本机成绩
        private readonly Func<int, IReadOnlyList<RankEntry>> _filler;                        // 陪榜（基准分）

        /// <summary>
        /// 构造离线本地源。
        /// </summary>
        /// <param name="selfProvider">按榜 id 取本机成绩（分数 / 达到时间 / 展示名 textId）。</param>
        /// <param name="filler">按榜 id 取陪榜记录（基准分；可为 null = 无陪榜）。</param>
        public LocalRankSource(
            Func<int, (long score, long ticks, int nameTextId)> selfProvider,
            Func<int, IReadOnlyList<RankEntry>> filler = null)
        {
            _selfProvider = selfProvider ?? throw new ArgumentNullException(nameof(selfProvider));
            _filler = filler;
        }

        public IReadOnlyList<RankEntry> Fetch(int rankId)
        {
            var list = new List<RankEntry>();
            var self = _selfProvider(rankId);
            list.Add(new RankEntry
            {
                IsSelf = true,
                Score = self.score,
                AchievedTicks = self.ticks,
                PlayerNameTextId = self.nameTextId,
            });
            if (_filler != null)
            {
                var fill = _filler(rankId);
                if (fill != null) list.AddRange(fill);
            }
            return list;
        }
    }

    /// <summary>
    /// 远程数据源（设计 31，兑现设计 22 §3.6 / O1 的远程源占位）：经联网会话发上报 / 查榜 RPC，
    /// 由服务端权威排序 + 算名次，客户端短路本地排序（CV2）。不再返空占位。
    /// </summary>
    /// <remarks>
    /// 身份从会话取，请求<b>不</b>携带账号 id（设计 31 §3.1 / §3.4 / CV3 / SV8）——协议请求只有榜 id（+ 上报的分数）。
    /// 异步红线：接口返框架通用 <c>UniTask</c>；网络往返用 Fantasy <c>FTask</c>，二者经 await 桥接（FTask 自带 awaiter）。
    /// 降级（设计 31 §四 / CV1）：未连接 / 发不出 / 超时 / 任何往返失败 / 榜不存在 → 上报返
    /// <see cref="RankSubmitCode.ServiceUnavailable"/>、查榜返 null（<b>不抛异常</b>），由 <see cref="RankService"/> 回退本地源、不阻断玩法、不伪造全服名次。
    /// 与兑换码「不本地放行」区别：排行榜本地回退无超发风险（结算发奖不在本增量）。
    /// 程序集边界：网络层 <c>FantasyClient</c> / <c>Fantasy.Unity</c> 受 <c>FANTASY_UNITY</c> 约束；
    /// 该 define 关闭的平台无网络可用，本类同样降级，使 GameLogic 在任何平台都可编译。
    /// <para>
    /// <see cref="IRankSource.Fetch"/> 对远程源返空：远程参赛记录在服务端、客户端无完整数据，不走「返原始记录 → 客户端排序」路径，
    /// 故 Fetch 不连网、不抛、返空（远程榜数据经 <see cref="QueryBoardAsync"/> 的已排序快照取，而非 Fetch）。
    /// 服务端排序用服务端时钟单位（Unix ms），客户端<b>不</b>用本地 Ticks 重排远程结果（服务端段决策）。
    /// </para>
    /// </remarks>
    public sealed class RemoteRankSource : IRemoteRankSource
    {
        /// <summary>远程参赛记录在服务端、不走客户端排序路径，故 Fetch 返空（不连网、不抛）。</summary>
        public IReadOnlyList<RankEntry> Fetch(int rankId) => Array.Empty<RankEntry>();

        /// <summary>
        /// 把服务端已排序的条目 + 名次 + 分数组装成设计 22 查询快照（短路本地排序，CV2）。
        /// 纯逻辑、无网络 / 无 FANTASY_UNITY 依赖（可 EditMode 单测）：条目顺序 / 名次<b>原样采用</b>服务端的（不重排、不重算）；
        /// 本人 = 名次等于 <paramref name="myRank"/> 的那条（名次唯一，同分各占唯一名次，§3.3.2）；
        /// 名次落在展示上限外（<paramref name="myRank"/>=0 或 &gt; 展示条数）→ self=null，SelfRank/SelfScore 仍按服务端权威回。
        /// </summary>
        public static RankBoard BuildBoardFromServer(int rankId, IReadOnlyList<RankEntry> sortedEntries, int myRank, long myScore)
        {
            var entries = new List<RankEntry>();
            RankEntry self = null;
            if (sortedEntries != null)
            {
                foreach (var e in sortedEntries)
                {
                    if (e == null) continue;
                    e.IsSelf = myRank > 0 && e.Rank == myRank; // 名次唯一 → 名次匹配即本人
                    if (e.IsSelf) self = e;
                    entries.Add(e);
                }
            }
            return new RankBoard
            {
                Id = rankId,
                Entries = entries,
                Self = self,        // 我若在展示条目内则标出（本人行高亮用；MyRank 为权威名次）
                SelfRank = myRank,  // 服务端算的名次（未入榜 = 0）
                SelfScore = myScore,
            };
        }

        public async UniTask<RankSubmitOutcome> SubmitScoreAsync(int rankId, long score)
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
            {
                return RankSubmitOutcome.ServiceUnavailable; // 未连接：不发请求、不抛，降级
            }

            Fantasy.G2C_RankSubmitScoreResponse response;
            try
            {
                // 发 RPC 并 await 回包：FTask 自带 awaiter，可在 async UniTask 体内直接 await。请求不自报账号（CV3）。
                response = await session.C2G_RankSubmitScoreRequest(rankId, score);
            }
            catch
            {
                return RankSubmitOutcome.ServiceUnavailable; // 发不出 / 超时 / 往返异常：降级，不抛
            }

            if (response == null)
            {
                return RankSubmitOutcome.ServiceUnavailable;
            }

            return new RankSubmitOutcome(MapSubmitCode(response.ResultCode), response.BestScore);
#else
            // FANTASY_UNITY 关闭（无网络平台）：降级为服务不可用。
            await UniTask.CompletedTask;
            return RankSubmitOutcome.ServiceUnavailable;
#endif
        }

        public async UniTask<RankBoard> QueryBoardAsync(int rankId)
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
            {
                return null; // 未连接 → 返 null，调用方回退本地源（CV1）
            }

            Fantasy.G2C_RankQueryResponse response;
            try
            {
                response = await session.C2G_RankQueryRequest(rankId); // 请求不自报账号（CV3）
            }
            catch
            {
                return null; // 发不出 / 超时 / 往返异常：降级回退本地源
            }

            if (response == null || response.ResultCode != Fantasy.RankQueryResultCode.Success)
            {
                // 服务不可用 / 榜不存在 → 回退本地源（CV1，本地源对榜不存在自身返 null）
                return null;
            }

            return MapBoard(rankId, response);
#else
            await UniTask.CompletedTask;
            return null;
#endif
        }

#if FANTASY_UNITY
        /// <summary>
        /// 把服务端已排序的查榜响应转成设计 22 查询快照（短路本地排序，CV2）：
        /// 服务端已排好序 + 算好名次 + 截展示上限，客户端直接采用，不重排、不重算名次。
        /// </summary>
        private static RankBoard MapBoard(int rankId, Fantasy.G2C_RankQueryResponse response)
        {
            var entries = new List<RankEntry>();
            if (response.Entries != null)
            {
                foreach (var item in response.Entries)
                {
                    if (item == null) continue;
                    entries.Add(new RankEntry
                    {
                        Rank = item.Rank,
                        Score = item.Score,
                        RemoteName = item.PlayerName, // 服务端展示名（账号占位，客户端有本地昵称则替换，O5）
                    });
                }
            }
            return BuildBoardFromServer(rankId, entries, response.MyRank, response.MyScore);
        }

        /// <summary>协议上报结果码 → 客户端结果码（一一对应）；未知码按服务不可用兜底，不崩。</summary>
        private static RankSubmitCode MapSubmitCode(Fantasy.RankSubmitResultCode code)
        {
            switch (code)
            {
                case Fantasy.RankSubmitResultCode.BestRefreshed:         return RankSubmitCode.BestRefreshed;
                case Fantasy.RankSubmitResultCode.BestNotRefreshed:      return RankSubmitCode.BestNotRefreshed;
                case Fantasy.RankSubmitResultCode.BelowEnterRequirement: return RankSubmitCode.BelowEnterRequirement;
                case Fantasy.RankSubmitResultCode.RankNotFound:          return RankSubmitCode.RankNotFound;
                case Fantasy.RankSubmitResultCode.ServiceUnavailable:    return RankSubmitCode.ServiceUnavailable;
                default:                                                 return RankSubmitCode.ServiceUnavailable;
            }
        }
#endif
    }
}
