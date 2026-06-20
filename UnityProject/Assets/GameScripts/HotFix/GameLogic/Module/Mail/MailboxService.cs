using System;
using System.Collections.Generic;
using GameLogic.BlockBlast.Item;

namespace GameLogic.Mail
{
    /// <summary>
    /// 对外收件接口（设计 21 §3.4）：游戏内任意系统（排行榜 / 活动 / 补偿）经此发奖。
    /// spec「为其他功能留邮件调用接口，用于奖励发放」——本轮真做（本地），下轮排行榜接此真实服务而非再 stub。
    /// </summary>
    public interface IMailService
    {
        /// <summary>收一封邮件进收件箱（收件后触发容量 / 过期清理）。返新邮件 id。</summary>
        long Send(MailDraft draft);
    }

    /// <summary>
    /// 收件箱服务（设计 21 §3.4–§3.6）：收件（对外 API）/ 列表（已读&gt;未读+时间）/ 标记已读 /
    /// 领取（单封 + 一键，复用 16 礼包库+落点）/ 删除已读 / 自动清理（过期 + 超量，注入时钟）/ 红点 getter。
    /// </summary>
    /// <remarks>
    /// 加法式：不新造发奖（复用 <see cref="GiftOpener.OpenRandom"/> → <see cref="ItemGrant.GrantOnAcquire"/>）、
    /// 不新造存储栈（持久化经注入 <see cref="IMailPersistence"/> 包既有 <c>Persistence.Provider</c>）。
    /// 纯逻辑可单测：注入 <see cref="NowProvider"/>（时钟）/ <see cref="RngProvider"/>（抽奖随机）/ <see cref="IMailPersistence"/>。
    /// 每个写操作（收件 / 领取 / 删除）落盘一次。
    /// </remarks>
    public sealed class MailboxService : IMailService
    {
        private readonly IMailPersistence _persist;
        private readonly List<MailItem> _inbox;
        private long _lastAssignedId; // 自增 id 单调保唯一（与时间戳基准叠加，§七 O8）

        /// <summary>时钟（注入；默认系统时钟）。有效期 / 过期清理 / 收件时间用它。</summary>
        public Func<DateTime> NowProvider = () => DateTime.Now;
        /// <summary>抽奖随机（注入；默认 new Random()）。复用 16 礼包库抽奖用。</summary>
        public Func<System.Random> RngProvider = () => new System.Random();

        public MailboxService(IMailPersistence persist)
        {
            _persist = persist ?? throw new ArgumentNullException(nameof(persist));
            _inbox = _persist.Load() ?? new List<MailItem>();
            // 恢复 id 基准：取已有最大 id，使重启后新邮件 id 不与旧邮件冲突
            for (int i = 0; i < _inbox.Count; i++)
                if (_inbox[i].Id > _lastAssignedId) _lastAssignedId = _inbox[i].Id;
        }

        // ── 收件（对外 IMailService.Send）─────────────────────────

        public long Send(MailDraft draft)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            var now = NowProvider();
            var m = new MailItem
            {
                Id            = NextId(now),
                SenderTextId  = draft.SenderTextId,
                TitleTextId   = draft.TitleTextId,
                BodyTextId    = draft.BodyTextId,
                SendTimeTicks = now.Ticks,
                ExpireDays    = draft.ExpireDays,
                RewardPoolId  = draft.RewardPoolId,
                Read          = false,
                Claimed       = false,
            };
            _inbox.Add(m);                  // 新邮件插尾
            CleanupExpired(now);
            CleanupOverflow();
            _persist.Save(_inbox);
            return m.Id;
        }

        /// <summary>分配收件箱内唯一 id：max(收件时间 Ticks, 上一个 id + 1)，单调递增防同 tick 连发冲突（§七 O8）。</summary>
        private long NextId(DateTime now)
        {
            long candidate = now.Ticks;
            if (candidate <= _lastAssignedId) candidate = _lastAssignedId + 1;
            _lastAssignedId = candidate;
            return candidate;
        }

        // ── 列表（已读>未读，组内时间降序）────────────────────────

        /// <summary>
        /// 列表：未读置顶（spec「已读&gt;未读」= 未读先看），组内按发件时间降序（新在前）。先清过期再返。
        /// </summary>
        public IReadOnlyList<MailItem> List()
        {
            CleanupExpired(NowProvider());
            var result = new List<MailItem>(_inbox);
            result.Sort(CompareForList);
            return result;
        }

        /// <summary>列表排序比较：未读(0)在已读(1)之前；同读态按 SendTimeTicks 降序（新在前）。</summary>
        private static int CompareForList(MailItem a, MailItem b)
        {
            int ra = a.Read ? 1 : 0;
            int rb = b.Read ? 1 : 0;
            if (ra != rb) return ra.CompareTo(rb);                 // 未读优先
            return b.SendTimeTicks.CompareTo(a.SendTimeTicks);     // 新在前（降序）
        }

        // ── 标记已读 ──────────────────────────────────────────────

        /// <summary>标记单封已读（幂等：已读再标不重复落盘）。</summary>
        public void MarkRead(long mailId)
        {
            var m = Find(mailId);
            if (m != null && !m.Read)
            {
                m.Read = true;
                _persist.Save(_inbox);
            }
        }

        // ── 删除已读（前置：已读 && (无奖励 || 已领)）──────────────

        /// <summary>删除一封邮件：仅当 <see cref="MailItem.CanDelete"/>（已读且无奖励/奖励已领）才删。否则返 false 不删。</summary>
        public bool DeleteRead(long mailId)
        {
            var m = Find(mailId);
            if (m == null || !m.CanDelete) return false; // 未读 / 有奖未领 → 拒绝
            _inbox.Remove(m);
            _persist.Save(_inbox);
            return true;
        }

        // ── 领取奖励（单封 Claim / 一键 ClaimAll）──────────────────

        /// <summary>
        /// 单封领取（设计 21 §3.4.2）：校验 → 抽奖励库 → 落点 → 标已领 + 已读 → 落盘。
        /// 顺序「抽奖落点成功 → 标已领 + 已读 → 落盘」防漏发 / 重复发；二次领返 <see cref="ClaimStatus.AlreadyClaimed"/> 不重发。
        /// <paramref name="state"/> 可 null 走纯解析路径（不落实际系统，仍产出结构，§3.4.3）。
        /// </summary>
        public ClaimResult Claim(long mailId, GameLogic.BlockBlast.MergeOrderState state)
        {
            var m = Find(mailId);
            if (m == null)                       return Fail(ClaimStatus.NotFound);
            if (!m.HasReward)                    return Fail(ClaimStatus.NoReward);
            if (m.Claimed)                       return Fail(ClaimStatus.AlreadyClaimed);
            if (IsExpired(m, NowProvider()))     return Fail(ClaimStatus.Expired);

            var granted = GrantPool(m.RewardPoolId, state); // 抽库 + 落点
            m.Claimed = true;
            m.Read = true;                                  // 领后标已领 + 已读
            _persist.Save(_inbox);
            // EVENT 解锁落盘（设计 41 §3.5 D3）：本地抽出的 produced 含 EVENT → 触发 SavePlayer 平铺 UnlockedAvatarIds。
            // IsValid 防 EVENT 触发时无谓 OnInit（EditMode 单测无 GameContext 实例时静默）。
            if (ItemGrant.ContainsEventUnlock(granted) && GameLogic.GameContext.IsValid)
                GameLogic.GameContext.Instance.SavePlayer();
            return new ClaimResult(ClaimStatus.Success, MailText.ClaimSuccess, granted);
        }

        /// <summary>
        /// 一键领取（设计 21 §3.4.2，spec「发所有未领奖→展示→全标已读」）：
        /// 所有「有奖励未领且未过期」邮件发奖并标已领；<b>全部邮件</b>（含无奖励/已领）标已读；汇总产出一次性返回。
        /// 已过期未领的不发（由清理移除）。
        /// </summary>
        public ClaimResult ClaimAll(GameLogic.BlockBlast.MergeOrderState state)
        {
            var now = NowProvider();
            CleanupExpired(now); // 先清过期，避免给已过期的发奖
            var all = new List<GrantPayload>();
            for (int i = 0; i < _inbox.Count; i++)
            {
                var m = _inbox[i];
                m.Read = true;   // 全标已读（spec）
                if (m.HasReward && !m.Claimed && !IsExpired(m, now))
                {
                    all.AddRange(GrantPool(m.RewardPoolId, state));
                    m.Claimed = true;
                }
            }
            _persist.Save(_inbox);
            // EVENT 解锁落盘（设计 41 §3.5 D3）：一键领取累积的 all 含 EVENT → 触发 SavePlayer。
            if (ItemGrant.ContainsEventUnlock(all) && GameLogic.GameContext.IsValid)
                GameLogic.GameContext.Instance.SavePlayer();
            return new ClaimResult(ClaimStatus.Success, MailText.ClaimSuccess, all);
        }

        /// <summary>
        /// 发奖落点（复用 16，不新造，设计 21 §3.4.3）：奖励库 id → <see cref="GiftOpener.OpenRandom"/> 抽出
        /// <c>GiftEntry</c> 列表 → 每项查 <see cref="GameLogic.Config.ItemConfigMgr.GetItem"/> →
        /// <see cref="ItemGrant.GrantOnAcquire"/> 落 <c>MergeOrderState</c>，汇总 <c>GrantPayload</c> 列表返回。
        /// 池缺 / 空 / 道具未登记 → 空列表（不抛）；<paramref name="state"/>==null 时只产出结构、不落系统。
        /// </summary>
        private IReadOnlyList<GrantPayload> GrantPool(int rewardPoolId, GameLogic.BlockBlast.MergeOrderState state)
        {
            var all = new List<GrantPayload>();
            if (rewardPoolId == 0) return all; // 无奖励
            var rng = RngProvider() ?? new System.Random();
            var rolled = GiftOpener.OpenRandom(rewardPoolId, 1, rng); // 既有，抽一次
            foreach (var e in rolled)
            {
                var itemDef = GameLogic.Config.ItemConfigMgr.GetItem(e.ItemId); // 既有
                all.AddRange(ItemGrant.GrantOnAcquire(itemDef, e.Num, state, rng)); // 既有，自动落点
            }
            return all;
        }

        // ── 自动清理（过期 + 超量，注入时钟，设计 21 §3.5.2）─────────

        /// <summary>过期清理：每封有效期 = <c>ExpireDays&gt;0 ? ExpireDays : Global.RetainDays</c>；<c>SendTime+有效期 &lt; now</c> → 删。</summary>
        private void CleanupExpired(DateTime now)
        {
            int retain = GameLogic.Config.MailConfigMgr.Global.RetainDays;
            _inbox.RemoveAll(m =>
            {
                int days = m.ExpireDays > 0 ? m.ExpireDays : retain;
                return new DateTime(m.SendTimeTicks).AddDays(days) < now;
            });
        }

        /// <summary>超量清理：收件箱数 &gt; <c>Global.MaxCount</c> → 按 SendTimeTicks 升序删最早，留最新 N 封（O4 总条数口径）。</summary>
        private void CleanupOverflow()
        {
            int max = GameLogic.Config.MailConfigMgr.Global.MaxCount;
            if (max < 0) return;                 // 负上限视作不限（防误配清空收件箱）
            if (_inbox.Count <= max) return;
            _inbox.Sort((a, b) => a.SendTimeTicks.CompareTo(b.SendTimeTicks)); // 早→晚
            _inbox.RemoveRange(0, _inbox.Count - max);                         // 删最早的，留最新 max 封
        }

        /// <summary>单封过期判定（与 <see cref="CleanupExpired"/> 同口径）。</summary>
        private static bool IsExpired(MailItem m, DateTime now)
        {
            int retain = GameLogic.Config.MailConfigMgr.Global.RetainDays;
            int days = m.ExpireDays > 0 ? m.ExpireDays : retain;
            return new DateTime(m.SendTimeTicks).AddDays(days) < now;
        }

        // ── 红点 getter（未读 OR 有奖未领，设计 21 §3.6）────────────

        /// <summary>全局红点（主界面图标）：任一封有红点即亮。先清过期再判（过期邮件不亮红点）。</summary>
        public bool HasUnreadOrUnclaimed
        {
            get
            {
                CleanupExpired(NowProvider());
                for (int i = 0; i < _inbox.Count; i++)
                    if (_inbox[i].HasRedDot) return true;
                return false;
            }
        }

        /// <summary>未读数（供 UI 角标，可选）。</summary>
        public int UnreadCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _inbox.Count; i++) if (!_inbox[i].Read) n++;
                return n;
            }
        }

        /// <summary>有奖未领数（供 UI 角标，可选）。</summary>
        public int UnclaimedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _inbox.Count; i++) if (_inbox[i].HasReward && !_inbox[i].Claimed) n++;
                return n;
            }
        }

        // ── 内部工具 ──────────────────────────────────────────────

        /// <summary>按收件箱 id 查邮件；查不到返 null（不抛）。</summary>
        public MailItem Find(long mailId)
        {
            for (int i = 0; i < _inbox.Count; i++)
                if (_inbox[i].Id == mailId) return _inbox[i];
            return null;
        }

        /// <summary>当前收件箱条数（供测试 / 调用方查；不触发清理）。</summary>
        public int Count => _inbox.Count;

        private static ClaimResult Fail(ClaimStatus s) => new ClaimResult(s, MailText.TextIdFor(s), null);
    }
}
