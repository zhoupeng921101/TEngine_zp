using System;
using GameLogic.BlockBlast.Player;

namespace GameLogic.AttrLedger
{
    /// <summary>
    /// 客户端 ledger 文案与时间格式工具(设计 46 §3.4 / §3.5)。
    /// 全部纯函数,无状态、不抛、便于 EditMode 单测。
    /// </summary>
    /// <remarks>
    /// 文案硬编码中文(设计 46 §3.4 / O5):去变现下 i18n 系统暂无;Tier 2+ 上 i18n 表后改 i18n 查表。
    /// </remarks>
    public static class AttrLedgerFormat
    {
        /// <summary>
        /// source 整数枚举 → 中文文案(设计 46 §3.4,覆盖 44 §3.3 已登记 10 档 + default「其他」兜底)。
        /// </summary>
        public static string FormatSource(AttrChangeSource s)
        {
            switch (s)
            {
                case AttrChangeSource.ChangeNameSpend:  return "改名扣钻";
                case AttrChangeSource.MailClaim:        return "邮件领奖";
                case AttrChangeSource.RedeemCode:       return "兑换码";
                case AttrChangeSource.RankSettleReward: return "排行榜奖励";
                case AttrChangeSource.ActivityReward:   return "活动奖励";
                case AttrChangeSource.GameplayConsume:  return "玩法消费";
                case AttrChangeSource.ShopPurchase:     return "商店购买";
                case AttrChangeSource.AdminGrant:       return "管理员发放";
                case AttrChangeSource.Refund:           return "退款";
                case AttrChangeSource.Unknown:
                default:                                return "其他"; // 未来扩 source 或 Unknown 都落此分支
            }
        }

        /// <summary>
        /// 属性种类 → 中文文案(用于无图标兜底 / 占位渲染)。
        /// </summary>
        public static string FormatKind(AttrType type)
        {
            switch (type)
            {
                case AttrType.Coin:    return "金币";
                case AttrType.Diamond: return "钻石";
                case AttrType.Stamina: return "体力";
                default:               return "?";
            }
        }

        /// <summary>
        /// Unix ms UTC → 显示文案(设计 46 §3.5,本地时区 + 相对时间混合)。
        /// </summary>
        /// <param name="timestampMs">Unix 毫秒 UTC(同 44 §3.1 Timestamp)。</param>
        /// <param name="nowUtc">当前 UTC 时间(入参化便于单测;生产传 <see cref="DateTime.UtcNow"/>)。</param>
        /// <remarks>
        /// 分支(设计 46 §3.5 表):
        /// - &lt; 60s → 「刚刚」;60s ~ 60min → 「N 分钟前」;60min ~ 24h → 「N 小时前」;24h ~ 7d → 「N 天前」;
        /// - ≥ 7d 或负数(未来时间戳)→ 本地时区 yyyy-MM-dd HH:mm(降级显示)。
        /// 未来时间戳(timestampMs &gt; nowUtc 对应毫秒)= 客户端 / 服务端时钟漂移,不抛,直接走绝对时间分支。
        /// </remarks>
        public static string FormatRelativeTime(long timestampMs, DateTime nowUtc)
        {
            // 时间戳 → UTC DateTime(Unix epoch 起)
            DateTime tsUtc;
            try
            {
                tsUtc = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime;
            }
            catch
            {
                // 极端越界(timestampMs 超出 DateTimeOffset 允许范围)→ 兜底显原始毫秒
                return timestampMs.ToString();
            }

            var delta = nowUtc - tsUtc;

            // 未来时间戳(负 delta)或 ≥ 7 天 → 走绝对时间分支(本地时区)
            if (delta.TotalSeconds < 0 || delta.TotalDays >= 7)
            {
                return tsUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            }

            if (delta.TotalSeconds < 60)  return "刚刚";
            if (delta.TotalMinutes < 60)  return $"{(int)delta.TotalMinutes} 分钟前";
            if (delta.TotalHours < 24)    return $"{(int)delta.TotalHours} 小时前";
            return $"{(int)delta.TotalDays} 天前";
        }
    }
}
