using System;
using System.Collections.Generic;

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
    /// 排行榜服务（设计 22 §3.3–§3.7）：查榜 / 排序并列 / 结算编排 / 每日 + 点赞领取 / 红点 getter。
    /// 纯逻辑可单测：注入数据源 / 持久化 / 邮件服务 / 时钟 / 开服日期 / 配置源。
    /// </summary>
    /// <remarks>
    /// 加法式：结算 / 每日 / 点赞发奖一律经注入 <see cref="GameLogic.Mail.IMailService.Send"/>（设计 21，复用不另造发奖），
    /// 排名层不碰 <c>MergeOrderState</c> / <c>ItemGrant</c> / <c>GiftOpener</c>（奖励经邮件领取链展开）。
    /// 元层进度（本机最佳 / 上次结算 / 领取日期）经注入 <see cref="IRankPersistence"/> 落盘，复用既有 Provider。
    /// 无任何网络调用（离线还原方向，§3.6 / O1）。
    /// </remarks>
    public sealed class RankService
    {
        private readonly IRankConfigSource _cfg;
        private readonly IRankSource _source;
        private readonly IRankPersistence _persist;
        private readonly GameLogic.Mail.IMailService _mail;
        private RankProgressSave _progress;

        /// <summary>时钟（注入；默认系统时钟）。结算时机 / 跨天重置用它。</summary>
        public Func<DateTime> NowProvider = () => DateTime.Now;
        /// <summary>开服日期（注入；OpenDays 结算用）。</summary>
        public DateTime OpenDate = DateTime.MinValue;

        /// <summary>
        /// 构造排行榜服务。<paramref name="cfg"/> 为 null 时默认包 <c>RankConfigMgr</c>。
        /// </summary>
        public RankService(IRankSource source, IRankPersistence persist,
                           GameLogic.Mail.IMailService mail, IRankConfigSource cfg = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _persist = persist ?? throw new ArgumentNullException(nameof(persist));
            _mail = mail ?? throw new ArgumentNullException(nameof(mail));
            _cfg = cfg ?? new RankConfigMgrSource();
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

        /// <summary>名次排序比较：分数降序；同分按 AchievedTicks 升序（早者靠前）。</summary>
        private static int CompareForRank(RankEntry a, RankEntry b)
        {
            int byScore = b.Score.CompareTo(a.Score); // 降序
            if (byScore != 0) return byScore;
            return a.AchievedTicks.CompareTo(b.AchievedTicks); // 升序（早者靠前）
        }

        // ── 结算时机判定（设计 22 §3.5.1，valid_type 四档）────────

        /// <summary>
        /// 给定 now / 开服日期 / 上次结算时间，判该榜是否到结算点（且本周期未结过）。
        /// </summary>
        public bool IsSettleDue(RankDef def, DateTime now, DateTime openDate, DateTime? lastSettle)
        {
            if (def == null) return false;
            switch (def.ValidType)
            {
                case RankValidType.Always:
                    return false; // 无结算，持续开启，永不结算
                case RankValidType.OpenDays:
                    return now >= openDate.AddDays(def.ValidVal) && lastSettle == null; // 开服第 X 天后，一次性
                case RankValidType.FixedTime:
                {
                    var t = FromUnixSeconds(def.ValidVal);
                    return now >= t && lastSettle == null; // 指定时间，一次性
                }
                case RankValidType.Weekly:
                    return IsWeeklyDue(now, def.ValidVal, lastSettle); // 周循环，每周一次
                default:
                    return false;
            }
        }

        /// <summary>周循环判据：算 now 所在自然周的「星期 X 结算时刻」，到点且本周未结过 → due（设计 22 §3.5.1）。</summary>
        private static bool IsWeeklyDue(DateTime now, long weekdayVal, DateTime? lastSettle)
        {
            var thisWeekSettle = ThisWeekSettleTime(now, weekdayVal);
            if (now < thisWeekSettle) return false;
            return lastSettle == null || lastSettle.Value < thisWeekSettle;
        }

        /// <summary>本自然周内「星期 X（1=周一..7=周日）」当天 0 点（本轮精度到天，O4）。</summary>
        private static DateTime ThisWeekSettleTime(DateTime now, long weekdayVal)
        {
            int target = (int)weekdayVal;
            if (target < 1) target = 1;
            if (target > 7) target = 7;
            // ISO 周：周一为一周起点。DayOfWeek.Sunday==0，转成 1..7（周一=1..周日=7）
            int todayIso = ((int)now.DayOfWeek + 6) % 7 + 1;
            var monday = now.Date.AddDays(-(todayIso - 1));
            return monday.AddDays(target - 1); // 目标星期 X 当天 0 点
        }

        /// <summary>valid_val（Unix 秒，UTC 纪元）→ 本地 DateTime（FixedTime 用）。</summary>
        private static DateTime FromUnixSeconds(long seconds)
            => DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime;

        // ── 结算编排（设计 22 §3.5.2）─────────────────────────────

        /// <summary>
        /// 检查所有榜，对到点且未结的榜结算：算本机名次 → 查档奖 → 组结算邮件经 21 发 → 记已结算。
        /// 返回本次结算了哪些榜（供 UI 提示）。纯方法，调用方按需调（登录 / tick，O9）。
        /// </summary>
        public List<SettleResult> CheckAndSettle(DateTime now)
        {
            var results = new List<SettleResult>();
            bool dirty = false;
            foreach (var def in _cfg.All())
            {
                if (def == null) continue;
                var last = GetLastSettle(def.Id);
                if (!IsSettleDue(def, now, OpenDate, last)) continue;

                var (myRank, myScore) = GetMyRank(def.Id);
                var tier = myRank > 0 ? def.TierForRank(myRank) : null;
                int rewardPoolId = tier?.RewardPoolId ?? 0;

                if (tier != null && rewardPoolId != 0 && def.MailDefId != 0)
                {
                    var draft = GameLogic.Mail.MailDraft.FromTemplate(def.MailDefId, RankText.SettleSender)
                                ?? new GameLogic.Mail.MailDraft
                                {
                                    SenderTextId = RankText.SettleSender,
                                    TitleTextId = RankText.SettleTitle,
                                };
                    draft.RewardPoolId = rewardPoolId; // 名次档奖励挂结算邮件（spec「奖励写到邮件中」）
                    _mail.Send(draft);                 // → 设计 21，本地真实发奖入口
                }

                SetLastSettle(def.Id, now); // 记已结算（防重复结）
                dirty = true;
                results.Add(new SettleResult(def.Id, myRank, myScore, rewardPoolId));
            }
            if (dirty) _persist.Save(_progress);
            return results;
        }

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

        /// <summary>组草稿挂奖励库 id，经 21 IMailService.Send 发邮件（每日 / 点赞 / 结算统一渠道，O5）。</summary>
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
        /// 排行榜 icon 红点：任一榜「今日每日奖可领」或「今日点赞可领」或「到点未结算」即亮。
        /// 结算奖进邮箱后由邮件红点（21）接管，本红点不为已结算项亮（避免重复）。
        /// </summary>
        public bool HasClaimable
        {
            get
            {
                var now = NowProvider();
                var today = now.Date;
                foreach (var def in _cfg.All())
                {
                    if (def == null) continue;
                    if (IsSettleDue(def, now, OpenDate, GetLastSettle(def.Id))) return true; // 到点未结
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

        private DateTime? GetLastSettle(int rankId)
        {
            var p = FindProgress(rankId, create: false);
            if (p == null || p.lastSettleTicks == 0) return null;
            return new DateTime(p.lastSettleTicks);
        }

        private void SetLastSettle(int rankId, DateTime now)
        {
            var p = FindProgress(rankId, create: true);
            p.lastSettleTicks = now.Ticks;
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
