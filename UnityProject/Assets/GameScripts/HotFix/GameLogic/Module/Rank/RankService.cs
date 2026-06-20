using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameLogic.Rank
{
    /// <summary>
    /// 配置访问接缝（设计 22 §3.3）：默认包 <c>RankConfigMgr</c>（运行期），测试可注 fake 绕 ConfigSystem。
    /// </summary>
    public interface IRankConfigSource
    {
        /// <summary>按榜 id 查；查不到返 null（不抛）。</summary>
        RankDef GetRank(int id);
        /// <summary>全部榜（供登录遍历检查结算 / 红点）。</summary>
        IReadOnlyCollection<RankDef> All();
    }

    /// <summary>
    /// 默认配置源：包既有 <c>RankConfigMgr</c>（运行期走 ConfigSystem / YooAsset，设计 22 §3.3）。
    /// </summary>
    public sealed class RankConfigMgrSource : IRankConfigSource
    {
        public RankDef GetRank(int id) => GameLogic.Config.RankConfigMgr.GetRank(id);
        public IReadOnlyCollection<RankDef> All() => GameLogic.Config.RankConfigMgr.All();
    }

    /// <summary>
    /// 排行榜服务（设计 22 §3.3–§3.7）：查榜 / 排序并列 / 每日 + 点赞领取 / 红点 getter。
    /// 纯逻辑可单测:注入数据源 / 持久化 / 邮件服务 / 时钟（每日 + 点赞跨天用） / 配置源。
    /// 结算编排上移服务端（设计 33），客户端本服务不提供结算检查 / 结算时机判定 / 上次结算时间 / 已结标记任何对外表面。
    /// </summary>
    /// <remarks>
    /// 加法式:每日 / 点赞发奖经注入 <see cref="GameLogic.Mail.IMailService.Send"/>（设计 21，复用不另造发奖），
    /// 排名层不碰 <c>MergeOrderState</c> / <c>ItemGrant</c> / <c>GiftOpener</c>（奖励经邮件领取链展开）。
    /// 元层进度（本机最佳 / 每日 + 点赞领取日期）经注入 <see cref="IRankPersistence"/> 落盘，复用既有 Provider。
    /// 结算奖经设计 33 服务端发奖入口投玩家邮箱（玩家走设计 32 客户端段领取链取奖），本服务不本地组结算邮件草稿、不本地发结算奖。
    /// </remarks>
    public sealed class RankService
    {
        private readonly IRankConfigSource _cfg;
        private readonly IRankSource _source;
        private readonly IRemoteRankSource _remote; // 可选远程源（设计 31）；null = 纯本地（离线）
        private readonly IRankPersistence _persist;
        private readonly GameLogic.Mail.IMailService _mail;
        private RankProgressSave _progress;

        /// <summary>时钟（注入;默认系统时钟）。每日 + 点赞跨天重置 / 提交成绩刷新 AchievedTicks 用它。</summary>
        public Func<DateTime> NowProvider = () => DateTime.Now;

        /// <summary>
        /// 构造排行榜服务。<paramref name="cfg"/> 为 null 时默认包 <c>RankConfigMgr</c>。
        /// </summary>
        /// <param name="source">
        /// 本地数据源（<see cref="LocalRankSource"/>，本机 + 陪榜，客户端排序）。
        /// 同时作为远程不可用时的<b>降级回退源</b>（设计 31 §四 / CV1），故离线 / 在线均须注入。
        /// </param>
        /// <param name="remote">
        /// 可选远程源（<see cref="RemoteRankSource"/>，服务端权威排序，设计 31）。
        /// 非 null → <see cref="GetBoardAsync"/> / <see cref="SubmitScoreAsync"/> 优先走 RPC、短路本地排序（CV2），
        /// 断服 / 超时回退本地源（CV1）；null → 纯离线，异步入口直接走本地同步路径。
        /// </param>
        public RankService(IRankSource source, IRankPersistence persist,
                           GameLogic.Mail.IMailService mail, IRankConfigSource cfg = null,
                           IRemoteRankSource remote = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _persist = persist ?? throw new ArgumentNullException(nameof(persist));
            _mail = mail ?? throw new ArgumentNullException(nameof(mail));
            _cfg = cfg ?? new RankConfigMgrSource();
            _remote = remote;
            _progress = _persist.Load() ?? new RankProgressSave();
            if (_progress.boards == null) _progress.boards = new List<RankBoardProgress>();
        }

        // ── 本机最佳成绩（供数据源 selfProvider / 提交）────────────

        /// <summary>
        /// 取本机在某榜的成绩快照（供生产 <see cref="LocalRankSource"/> 的 selfProvider 闭包引用）。
        /// 展示名 textId 占位（O6）；未提交过返 (0,0,占位名)。
        /// </summary>
        public (long score, long ticks, int nameTextId) GetMyBest(int rankId)
        {
            var p = FindProgress(rankId, create: false);
            return (p?.bestScore ?? 0L, p?.bestAchievedTicks ?? 0L, RankText.SettleSender);
        }

        /// <summary>
        /// 提交本机一次成绩（取较大者更新最佳，落盘）。供玩法结束时调（设计 22 §3.3）。
        /// 成绩夹 ≥0；更高分时刷新 AchievedTicks 为当前注入时钟。
        /// </summary>
        public void SubmitScore(int rankId, long score)
        {
            if (score < 0) score = 0;
            var p = FindProgress(rankId, create: true);
            if (score > p.bestScore)
            {
                p.bestScore = score;
                p.bestAchievedTicks = NowProvider().Ticks;
            }
            _persist.Save(_progress);
        }

        // ── 查榜 + 排序并列（设计 22 §3.3 / §3.3.2）────────────────

        /// <summary>
        /// 查一个榜：取数据源原始记录 → 过滤入榜要求 → 排序回填名次 → 截入榜上限 / 展示上限 → 标本人。
        /// 榜不存在返 null（不抛）。
        /// </summary>
        public RankBoard GetBoard(int rankId)
        {
            var def = _cfg.GetRank(rankId);
            if (def == null) return null;

            var raw = _source.Fetch(rankId) ?? Array.Empty<RankEntry>();

            // 本机原始成绩（用于「未入榜时返当前最佳」），在过滤前留存
            long selfRawScore = 0;
            bool selfPresent = false;
            for (int i = 0; i < raw.Count; i++)
            {
                if (raw[i] != null && raw[i].IsSelf)
                {
                    selfRawScore = raw[i].Score;
                    selfPresent = true;
                    break;
                }
            }

            // 过滤入榜要求：Score < Condition 不进榜
            var eligible = new List<RankEntry>();
            for (int i = 0; i < raw.Count; i++)
            {
                var e = raw[i];
                if (e == null) continue;
                if (e.Score < def.Condition) continue;
                eligible.Add(e);
            }

            // 排序：分数降序 → 同分按 AchievedTicks 升序（早者靠前），稳定可复现
            eligible.Sort(CompareForRank);

            // 入榜上限：只取前 CountMax 名参与名次（CountMax<=0 视作不限）
            int countMax = def.CountMax > 0 ? def.CountMax : eligible.Count;
            int ranked = Math.Min(countMax, eligible.Count);

            // 回填名次（1 起，顺序名次：同分也各占唯一名次，O4）
            for (int i = 0; i < ranked; i++) eligible[i].Rank = i + 1;

            // 标本人
            RankEntry self = null;
            int selfRank = 0;
            for (int i = 0; i < ranked; i++)
            {
                if (eligible[i].IsSelf) { self = eligible[i]; selfRank = eligible[i].Rank; break; }
            }

            // 展示上限：Entries 截前 ShowMax 条（ShowMax<=0 视作不限）
            int showMax = def.ShowMax > 0 ? def.ShowMax : ranked;
            int show = Math.Min(showMax, ranked);
            var entries = new List<RankEntry>(show);
            for (int i = 0; i < show; i++) entries.Add(eligible[i]);

            return new RankBoard
            {
                Id = rankId,
                Entries = entries,
                Self = self,
                SelfRank = selfRank,
                SelfScore = self != null ? self.Score : (selfPresent ? selfRawScore : 0L),
            };
        }

        /// <summary>
        /// 查本人名次（轻量，与 <see cref="GetBoard"/> 的 SelfRank 一致）。未入榜返 (0, 当前成绩)。
        /// </summary>
        public (int rank, long score) GetMyRank(int rankId)
        {
            var board = GetBoard(rankId);
            if (board == null) return (0, 0);
            return (board.SelfRank, board.SelfScore);
        }

        // ── 远程数据源入口（设计 31，全服权威榜 + 降级回退本地源）──────

        /// <summary>
        /// 查一个榜（设计 31 §3.4 / CV1-CV2）：有远程源 → 发查榜 RPC，取服务端已排好的前 N 名 + 自己名次
        /// （短路本地排序，CV2）；断服 / 超时 / 服务不可用 / 榜不存在 → 回退本地源同步 <see cref="GetBoard"/>
        /// （不阻断玩法、不伪造全服名次，设计 31 §四）。无远程源（离线）→ 直接走本地同步路径。
        /// </summary>
        /// <remarks>服务层对外仍产出同一查询快照（<see cref="RankBoard"/>），调用方无需关心名次由本地还是服务端算。</remarks>
        public async UniTask<RankBoard> GetBoardAsync(int rankId)
        {
            if (_remote != null)
            {
                var remoteBoard = await _remote.QueryBoardAsync(rankId); // 失败返 null（不抛）
                if (remoteBoard != null) return remoteBoard;             // 服务端已排好，短路本地排序（CV2）
                // null = 断服 / 超时 / 服务不可用 / 榜不存在 → 回退本地源（CV1）
            }
            return GetBoard(rankId); // 本地源：本机 + 陪榜 → 客户端排序（同步路径零改动）
        }

        /// <summary>
        /// 上报一次成绩（设计 31 §3.1 / CV1）：有远程源 → 发上报 RPC（取最优由服务端裁定）；
        /// 无论远程成功 / 失败，<b>始终</b>同步更新本地最佳（成绩已在本地，断服时本地源仍记本机最佳，设计 31 §四）。
        /// 身份从会话取、不自报账号（CV3）。返回服务端裁决结果（断服 = ServiceUnavailable，不阻断玩法）。
        /// </summary>
        /// <remarks>
        /// 本地落盘与远程上报并行不冲突：本地最佳是离线源的本机记录（设计 22），远程最佳是服务端权威。
        /// 远程不可用时本地仍保住「本机历史最佳 + 陪榜榜」，重连后可重报（取最优、重报同分不掉名次）。
        /// </remarks>
        public async UniTask<RankSubmitOutcome> SubmitScoreAsync(int rankId, long score)
        {
            SubmitScore(rankId, score); // 本地最佳始终更新（断服降级时本地源仍记本机最佳，设计 31 §四）
            if (_remote == null)
            {
                return new RankSubmitOutcome(RankSubmitCode.ServiceUnavailable, GetMyBest(rankId).score); // 离线：无远程裁决
            }
            return await _remote.SubmitScoreAsync(rankId, score); // 失败返 ServiceUnavailable（不抛，不阻断玩法）
        }

        /// <summary>名次排序比较：分数降序；同分按 AchievedTicks 升序（早者靠前）。</summary>
        private static int CompareForRank(RankEntry a, RankEntry b)
        {
            int byScore = b.Score.CompareTo(a.Score); // 降序
            if (byScore != 0) return byScore;
            return a.AchievedTicks.CompareTo(b.AchievedTicks); // 升序（早者靠前）
        }

        // ── 结算编排（设计 22 §3.5）已上移服务端（设计 33）──────────
        // 本服务不暴露「结算检查 / 结算时机判定 / 上次结算时间 / 已结标记」对外表面;
        // 结算到点 / 算名次 / 按名次档查奖 / 发结算邮件 / 周期幂等全程在服务端,
        // 客户端在结算上无动作。玩家在邮箱里见结算奖,经设计 32 客户端段领取链取奖。

        // ── 每日 / 点赞领取（设计 22 §3.4.2，跨天重置，经邮件发）───

        /// <summary>
        /// 领今日每日奖（按本人当前名次档的 DailyRewardPoolId）。
        /// 当天已领 → AlreadyClaimedToday；未入榜 → NotRanked；无每日奖 → NoReward。成功经邮件发奖。
        /// </summary>
        public RankClaimResult ClaimDaily(int rankId)
        {
            var def = _cfg.GetRank(rankId);
            if (def == null) return Fail(RankClaimStatus.NotRanked);

            var today = NowProvider().Date;
            if (AlreadyClaimedDailyToday(rankId, today)) return Fail(RankClaimStatus.AlreadyClaimedToday);

            var (rank, _) = GetMyRank(rankId);
            if (rank == 0) return Fail(RankClaimStatus.NotRanked);

            var tier = def.TierForRank(rank);
            if (tier == null || tier.DailyRewardPoolId == 0) return Fail(RankClaimStatus.NoReward);

            SendRewardMail(def, tier.DailyRewardPoolId, RankText.DailyMailTitle);
            MarkDailyClaimed(rankId, today);
            _persist.Save(_progress);
            return Ok();
        }

        /// <summary>
        /// 领今日点赞奖（榜级 PraiseRewardPoolId，不分档）。
        /// PraiseRewardPoolId==0 → NoReward（无点赞按钮）；当天已领 → AlreadyClaimedToday。每天一次，经邮件发。
        /// </summary>
        public RankClaimResult ClaimPraise(int rankId)
        {
            var def = _cfg.GetRank(rankId);
            if (def == null) return Fail(RankClaimStatus.NoReward);
            if (def.PraiseRewardPoolId == 0) return Fail(RankClaimStatus.NoReward);

            var today = NowProvider().Date;
            if (AlreadyClaimedPraiseToday(rankId, today)) return Fail(RankClaimStatus.AlreadyClaimedToday);

            SendRewardMail(def, def.PraiseRewardPoolId, RankText.PraiseMailTitle);
            MarkPraiseClaimed(rankId, today);
            _persist.Save(_progress);
            return Ok();
        }

        /// <summary>组草稿挂奖励库 id，经 21 IMailService.Send 发本机邮件（每日 / 点赞经此渠道,O5;结算奖经设计 33 服务端发,不走本方法）。</summary>
        private void SendRewardMail(RankDef def, int rewardPoolId, int titleTextId)
        {
            GameLogic.Mail.MailDraft draft = null;
            if (def.MailDefId != 0)
                draft = GameLogic.Mail.MailDraft.FromTemplate(def.MailDefId, RankText.SettleSender);
            if (draft == null)
                draft = new GameLogic.Mail.MailDraft { SenderTextId = RankText.SettleSender, TitleTextId = titleTextId };
            draft.RewardPoolId = rewardPoolId;
            _mail.Send(draft);
        }

        // ── 红点 getter（设计 22 §3.7）────────────────────────────

        /// <summary>
        /// 排行榜 icon 红点:任一榜「今日每日奖可领」或「今日点赞可领」即亮。
        /// 结算奖红点交[邮件红点 21]接管(结算奖经设计 33 服务端发到玩家邮箱,「有未领结算奖」由邮件红点统一表达,
        /// 本红点不感知「到点未结算」状态分支以免与邮件红点同源亮两次)。
        /// </summary>
        public bool HasClaimable
        {
            get
            {
                var today = NowProvider().Date;
                foreach (var def in _cfg.All())
                {
                    if (def == null) continue;
                    var (rank, _) = GetMyRank(def.Id);
                    if (rank > 0)
                    {
                        var tier = def.TierForRank(rank);
                        if (tier != null && tier.DailyRewardPoolId != 0 && !AlreadyClaimedDailyToday(def.Id, today))
                            return true;
                    }
                    if (def.PraiseRewardPoolId != 0 && !AlreadyClaimedPraiseToday(def.Id, today))
                        return true;
                }
                return false;
            }
        }

        // ── 进度访问内部工具 ──────────────────────────────────────

        private RankBoardProgress FindProgress(int rankId, bool create)
        {
            for (int i = 0; i < _progress.boards.Count; i++)
                if (_progress.boards[i].rankId == rankId) return _progress.boards[i];
            if (!create) return null;
            var p = new RankBoardProgress { rankId = rankId };
            _progress.boards.Add(p);
            return p;
        }

        private bool AlreadyClaimedDailyToday(int rankId, DateTime today)
        {
            var p = FindProgress(rankId, create: false);
            return p != null && p.dailyClaimDateBin == today.Ticks;
        }

        private void MarkDailyClaimed(int rankId, DateTime today)
        {
            var p = FindProgress(rankId, create: true);
            p.dailyClaimDateBin = today.Ticks;
        }

        private bool AlreadyClaimedPraiseToday(int rankId, DateTime today)
        {
            var p = FindProgress(rankId, create: false);
            return p != null && p.praiseClaimDateBin == today.Ticks;
        }

        private void MarkPraiseClaimed(int rankId, DateTime today)
        {
            var p = FindProgress(rankId, create: true);
            p.praiseClaimDateBin = today.Ticks;
        }

        private static RankClaimResult Ok()
            => new RankClaimResult(RankClaimStatus.Success, RankText.ClaimSuccess);

        private static RankClaimResult Fail(RankClaimStatus s)
            => new RankClaimResult(s, RankText.TextIdFor(s));
    }
}
