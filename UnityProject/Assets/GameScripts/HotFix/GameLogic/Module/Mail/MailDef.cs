namespace GameLogic.Mail
{
    /// <summary>
    /// 邮件模板（配置行 → POCO，设计 21 §3.2）。
    /// 隔离 Luban 生成类型 <c>GameConfig.Mail</c>，业务侧只认本 POCO。
    /// </summary>
    /// <remarks>
    /// 邮件是通用系统（非 BlockBlast 玩法专属），命名空间用 <see cref="GameLogic.Mail"/>（同 20 <c>GameLogic.Redeem</c> 做法）；
    /// 配置桥接 <c>MailConfigMgr</c> 归 <c>GameLogic.Config</c>（与既有 <c>ItemConfigMgr</c> 并列）。
    /// </remarks>
    public sealed class MailDef
    {
        /// <summary>邮件模板 id（主键）。</summary>
        public int Id;
        /// <summary>标题 textId（多语言占位，§七 O5）。</summary>
        public int TitleTextId;
        /// <summary>内容 textId（多语言占位）。</summary>
        public int BodyTextId;
        /// <summary>有效期（天）；&lt;=0 时用全局 <see cref="MailGlobalConfig.RetainDays"/> 兜底（§3.5.2）。</summary>
        public int ExpireDays;
        /// <summary>奖励随机库 id（gift_random index，设计 16）；0 = 无奖励。</summary>
        public int RewardPoolId;
    }

    /// <summary>
    /// 邮件全局配置（设计 21 §3.1）。表缺省时 <c>MailConfigMgr.Global</c> 返本默认值（spec 默认 100/30）。
    /// </summary>
    public sealed class MailGlobalConfig
    {
        /// <summary>收件箱总上限 N：超过按发件时间删最早（spec「总上限 N」，O4 取总条数口径）。</summary>
        public int MaxCount = 100;
        /// <summary>默认保留天数（spec「保留期默认一月」）；邮件自带 <see cref="MailDef.ExpireDays"/>&gt;0 时优先用邮件的。</summary>
        public int RetainDays = 30;
    }
}
