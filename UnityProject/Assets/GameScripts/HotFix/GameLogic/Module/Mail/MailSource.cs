using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
#if FANTASY_UNITY
using Fantasy; // 协议消息类型 + NetworkProtocolHelper 扩展方法 C2G_MailListRequest/C2G_MailClaimRequest 所在命名空间
#endif

namespace GameLogic.Mail
{
    /// <summary>
    /// 服务器 / 运营来源接缝（设计 21 §3.7 / 设计 32 §六）。
    /// 运营推送邮件（后台全服发 / 定时邮件 / 系统定向）的来源：服务端权威存运营邮件，客户端经此拉取应收列表。
    /// </summary>
    /// <remarks>
    /// 与对外收件 API <see cref="IMailService"/> 区分：<see cref="IMailService.Send"/> 是<b>客户端进程内系统</b>发奖入口
    /// （排行榜客户端结算 / 活动 / 补偿，离线可用，设计 21 §3.3）；<see cref="IMailSource"/> 是<b>外部服务器 / 运营</b>推送来源
    /// （设计 32 服务端权威，经联网会话拉取）。本地降级 / 离线注 <see cref="InertMailSource"/>（返空、不连网）。
    /// </remarks>
    public interface IMailSource
    {
        /// <summary>同步拉取待派发邮件（离线 / 无网络层时返空）。</summary>
        System.Collections.Generic.IReadOnlyList<MailDraft> Pull();
    }

    /// <summary>
    /// 离线 / 降级 stub：无服务器可达时返空、不连网（设计 21 §3.7 / §七 O1，设计 32 §四列表降级）。
    /// 区服离线视单一本地区服（无多区概念）。无运营邮件可伪造（运营来源唯一在服务端，设计 32 读前必看第 1 条）。
    /// </summary>
    public sealed class InertMailSource : IMailSource
    {
        public System.Collections.Generic.IReadOnlyList<MailDraft> Pull()
            => System.Array.Empty<MailDraft>(); // 不抛、不连网
    }

    /// <summary>
    /// 服务端下发的一封邮件（拉列表响应转成的客户端模型，设计 32 §3.2）。
    /// 不含奖励明细（奖励领取时才由服务端抽奖，见设计 32 §3.2 注）；收件箱表现层据此画列表 / 红点 / 领取按钮。
    /// </summary>
    public sealed class MailListEntry
    {
        /// <summary>邮件标识（领取时回传定位；广播 / 定向同一标识空间，客户端无需区分）。</summary>
        public string MailId;
        /// <summary>发件人 textId（多语言占位）。</summary>
        public int SenderTextId;
        /// <summary>标题 textId。</summary>
        public int TitleTextId;
        /// <summary>正文 textId。</summary>
        public int ContentTextId;
        /// <summary>收件 / 发件时间（服务端 Unix 毫秒）。</summary>
        public long SendUnixMs;
        /// <summary>是否有附件（服务端据附件库 id != 0 标记；客户端画领取按钮 / 红点）。</summary>
        public bool HasReward;
        /// <summary>该账号对此邮件的领取态（服务端权威：已领 = true / 未领 = false）。</summary>
        public bool Claimed;
    }

    /// <summary>
    /// 一次拉列表的快照（设计 32 §3.2）：该账号应收、未过期的邮件（过期已由服务端时钟滤）。
    /// 断服 / 不可用时 <see cref="RemoteMailService.PullInboxAsync"/> 返空列表（不阻断玩法，设计 32 §四）。
    /// </summary>
    public sealed class MailInboxSnapshot
    {
        /// <summary>该账号应收且未过期的邮件（按服务端下发顺序，排序 / 已读态由客户端表现层处理）。</summary>
        public IReadOnlyList<MailListEntry> Mails;

        public MailInboxSnapshot(IReadOnlyList<MailListEntry> mails)
        {
            Mails = mails ?? Array.Empty<MailListEntry>();
        }
    }

    /// <summary>
    /// 服务端领奖裁决结果码（客户端侧，设计 32 §3.4）。与协议 <c>MailClaimResultCode</c> 一一对应，
    /// 但定义在 GameLogic 内（不暴露 Fantasy 类型于公开签名，跨平台可编译）。
    /// </summary>
    public enum MailClaimCode
    {
        /// <summary>成功并已授权发奖（响应附奖励列表；库 id 未登记时奖励列表为空但仍成功）。</summary>
        Success = 0,
        /// <summary>按邮件标识 + 会话账号定位不到这封邮件。</summary>
        MailNotFound = 1,
        /// <summary>该邮件无附件（附件库 id = 0，纯通知邮件）；不发奖。</summary>
        NoReward = 2,
        /// <summary>本账号已领过此邮件（响应不附奖励列表）。</summary>
        AlreadyClaimed = 3,
        /// <summary>邮件有有效期且服务端时钟已超过（过期判定用服务端时钟）。</summary>
        Expired = 4,
        /// <summary>服务不可用 / 断服 / 超时；客户端提示重试、邮件保持可领、<b>不本地放行</b>（设计 32 §四）。</summary>
        ServiceUnavailable = 5,
    }

    /// <summary>服务端裁定的一项奖励（道具 id × 数量，设计 32 §3.4）：服务端抽奖产物，客户端不申报。</summary>
    public readonly struct MailRewardLine
    {
        /// <summary>道具 id。</summary>
        public readonly int ItemId;
        /// <summary>数量。</summary>
        public readonly int Count;

        public MailRewardLine(int itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }

    /// <summary>
    /// 一次领奖的服务端裁决结果（设计 32 §3.4）：结果码 + 成功时奖励列表（道具 id × 数量）。
    /// 失败 / 断服时奖励列表为空集合（非 null）。
    /// </summary>
    public readonly struct MailClaimOutcome
    {
        /// <summary>裁决结果码。</summary>
        public readonly MailClaimCode Code;
        /// <summary>成功时服务端抽奖裁定的奖励列表；失败 / 断服时为空集合。</summary>
        public readonly IReadOnlyList<MailRewardLine> Rewards;

        public MailClaimOutcome(MailClaimCode code, IReadOnlyList<MailRewardLine> rewards)
        {
            Code = code;
            Rewards = rewards ?? Array.Empty<MailRewardLine>();
        }

        /// <summary>服务不可用快捷构造（无奖励，不本地放行）。</summary>
        public static MailClaimOutcome ServiceUnavailable
            => new MailClaimOutcome(MailClaimCode.ServiceUnavailable, Array.Empty<MailRewardLine>());

        /// <summary>非成功结果码快捷构造（无奖励列表）。</summary>
        public static MailClaimOutcome Fail(MailClaimCode code)
            => new MailClaimOutcome(code, Array.Empty<MailRewardLine>());
    }

    /// <summary>
    /// 远程邮件来源能力（设计 32 §三 / CV1-CV3）：服务端权威——拉应收列表 + 领奖裁决均经联网会话 RPC。
    /// </summary>
    /// <remarks>
    /// 身份从会话取、请求<b>不</b>携带账号 id（设计 32 §3.1 / §3.3 / CV5 / SV9）——协议请求只有（领取时的）邮件标识。
    /// 异步红线：接口返框架通用 <c>UniTask</c>；网络往返用 Fantasy <c>FTask</c>，二者经 await 桥接（FTask 自带 awaiter）。
    /// 降级（设计 32 §四 / CV1 / CV3）：
    /// <list type="bullet">
    /// <item>拉列表断服 / 超时 / 不可用 → 返 null（调用方空载、不阻断玩法，无本地运营邮件可伪造）；</item>
    /// <item>领奖断服 / 超时 / 不可用 → 返 <see cref="MailClaimCode.ServiceUnavailable"/>（<b>绝不本地放行</b>，邮件保持可领、可重试，区别于排行榜回退本地源）。</item>
    /// </list>
    /// </remarks>
    public interface IRemoteMailSource
    {
        /// <summary>
        /// 拉该账号应收（活跃广播 + 定向）且未过期的邮件列表（设计 32 §3.1 / §3.2）。
        /// 断服 / 超时 / 不可用 → 返 null（不抛），调用方空载不阻断玩法。
        /// </summary>
        UniTask<MailInboxSnapshot> FetchInboxAsync();

        /// <summary>
        /// 领取一封邮件（设计 32 §3.3）：发领取协议、await 服务端裁决（防重 + 抽奖 + 过期）。
        /// 成功 → 返结果码 + 服务端抽奖裁定的奖励列表；断服 / 超时 / 不可用 → 返
        /// <see cref="MailClaimCode.ServiceUnavailable"/>（不抛、<b>不本地放行</b>）。
        /// </summary>
        UniTask<MailClaimOutcome> ClaimAsync(string mailId);
    }

    /// <summary>
    /// 远程邮件来源（设计 32，兑现设计 21 §3.7 运营来源占位）：经联网会话发拉列表 / 领取 RPC，
    /// 服务端权威下发应收列表 + 领奖裁决（防重 + 抽奖 + 过期）。不再返空占位。
    /// </summary>
    /// <remarks>
    /// 程序集边界：网络层 <c>FantasyClient</c> / <c>Fantasy.Unity</c> 受 <c>FANTASY_UNITY</c> 约束；
    /// 该 define 关闭的平台无网络可用，本类同样降级（拉列表返 null、领取返 ServiceUnavailable），使 GameLogic 在任何平台都可编译。
    /// 把「服务端响应 → 客户端快照 / 裁决」的纯转换抽成非 FANTASY_UNITY-gated 静态方法（<see cref="BuildInboxFromEntries"/> /
    /// <see cref="MapClaimCode"/>），可 EditMode 直测；FANTASY_UNITY-gated 的 <c>MapInbox</c> / <c>MapClaim</c>（读 Fantasy 协议字段）只薄薄调它。
    /// </remarks>
    public sealed class RemoteMailSource : IRemoteMailSource
    {
        public async UniTask<MailInboxSnapshot> FetchInboxAsync()
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
            {
                return null; // 未连接：不发请求、不抛，返 null 由调用方空载（设计 32 §四）
            }

            Fantasy.G2C_MailListResponse response;
            try
            {
                // 发 RPC 并 await 回包：FTask 自带 awaiter，可在 async UniTask 体内直接 await。请求无业务字段、不自报账号（CV5）。
                TEngine.Log.Info($"[Fantasy] 发送邮件列表");
                response = await session.C2G_MailListRequest();
            }
            catch
            {
                return null; // 发不出 / 超时 / 往返异常：降级，不抛
            }

            if (response == null || response.ResultCode != Fantasy.MailClaimResultCode.Success)
            {
                // 服务不可用 → 返 null（调用方空载）。拉列表只有 Success / ServiceUnavailable 两态（设计 32 §3.2）。
                return null;
            }

            return MapInbox(response);
#else
            // FANTASY_UNITY 关闭（无网络平台）：降级为返 null，调用方空载。
            await UniTask.CompletedTask;
            return null;
#endif
        }

        public async UniTask<MailClaimOutcome> ClaimAsync(string mailId)
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
            {
                return MailClaimOutcome.ServiceUnavailable; // 未连接：不发请求、不本地放行（设计 32 §四）
            }

            Fantasy.G2C_MailClaimResponse response;
            try
            {
                // 发领取 RPC 并 await 裁决。请求只带邮件标识，身份从会话取、不自报账号（CV5）。
                TEngine.Log.Info($"[Fantasy] 发送邮件领取 mailId={mailId}");
                response = await session.C2G_MailClaimRequest(mailId);
            }
            catch
            {
                return MailClaimOutcome.ServiceUnavailable; // 发不出 / 超时 / 往返异常：不本地放行
            }

            if (response == null)
            {
                return MailClaimOutcome.ServiceUnavailable;
            }

            return MapClaim(response);
#else
            // FANTASY_UNITY 关闭（无网络平台）：领取绝不本地放行，降级为服务不可用。
            await UniTask.CompletedTask;
            return MailClaimOutcome.ServiceUnavailable;
#endif
        }

        /// <summary>
        /// 把服务端下发的邮件条目组装成客户端收件箱快照（纯逻辑、无网络 / 无 FANTASY_UNITY 依赖，可 EditMode 直测）：
        /// 条目顺序<b>原样采用</b>服务端的（排序 / 已读态由客户端表现层处理）；null / 空条目跳过、不抛。
        /// </summary>
        public static MailInboxSnapshot BuildInboxFromEntries(IReadOnlyList<MailListEntry> entries)
        {
            var list = new List<MailListEntry>();
            if (entries != null)
            {
                foreach (var e in entries)
                {
                    if (e == null) continue;
                    list.Add(e);
                }
            }
            return new MailInboxSnapshot(list);
        }

        /// <summary>协议领奖结果码 → 客户端结果码（一一对应）；未知码按服务不可用兜底，不崩。</summary>
        public static MailClaimCode MapClaimCode(int rawCode)
        {
            switch (rawCode)
            {
                case 0: return MailClaimCode.Success;          // MailClaimResultCode.Success
                case 1: return MailClaimCode.MailNotFound;     // MailNotFound
                case 2: return MailClaimCode.NoReward;         // NoReward
                case 3: return MailClaimCode.AlreadyClaimed;   // AlreadyClaimed
                case 4: return MailClaimCode.Expired;          // Expired
                case 5: return MailClaimCode.ServiceUnavailable; // ServiceUnavailable
                default: return MailClaimCode.ServiceUnavailable;
            }
        }

#if FANTASY_UNITY
        /// <summary>把服务端拉列表响应转成客户端收件箱快照（薄壳，读 Fantasy 协议字段后调纯转换 <see cref="BuildInboxFromEntries"/>）。</summary>
        private static MailInboxSnapshot MapInbox(Fantasy.G2C_MailListResponse response)
        {
            var entries = new List<MailListEntry>();
            if (response.Mails != null)
            {
                foreach (var m in response.Mails)
                {
                    if (m == null) continue;
                    entries.Add(new MailListEntry
                    {
                        MailId        = m.MailId,
                        SenderTextId  = m.SenderTextId,
                        TitleTextId   = m.TitleTextId,
                        ContentTextId = m.ContentTextId,
                        SendUnixMs    = m.SendUnixMs,
                        HasReward     = m.HasReward,
                        Claimed       = m.Claimed,
                    });
                }
            }
            return BuildInboxFromEntries(entries);
        }

        /// <summary>把服务端领取响应转成客户端裁决结果（薄壳）：结果码映射 + 仅成功时收集奖励列表。</summary>
        private static MailClaimOutcome MapClaim(Fantasy.G2C_MailClaimResponse response)
        {
            var code = MapClaimCode((int)response.ResultCode);
            if (code != MailClaimCode.Success)
            {
                return MailClaimOutcome.Fail(code); // 失败分支不附奖励（服务端不授权发奖）
            }

            var rewards = new List<MailRewardLine>();
            if (response.Rewards != null)
            {
                foreach (var r in response.Rewards)
                {
                    if (r == null) continue;
                    rewards.Add(new MailRewardLine(r.ItemId, r.Count));
                }
            }
            return new MailClaimOutcome(MailClaimCode.Success, rewards);
        }
#endif
    }
}
