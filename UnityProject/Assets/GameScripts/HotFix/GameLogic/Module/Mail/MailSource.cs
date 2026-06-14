namespace GameLogic.Mail
{
    /// <summary>
    /// 服务器 / 运营来源接缝（设计 21 §3.7）。
    /// 未来真实服务器 / 运营后台推送邮件（后台全服发 / 定时邮件 / 区服多选 = 服务器侧能力）经此进收件箱。
    /// </summary>
    /// <remarks>
    /// 与对外收件 API <see cref="IMailService"/> 区分：<see cref="IMailService.Send"/> 是<b>游戏内系统</b>发奖入口
    /// （排行榜 / 活动 / 补偿，离线可用，本轮真做）；<see cref="IMailSource"/> 是<b>外部服务器 / 运营</b>推送入口
    /// （离线不可用，stub）。本工程无网络模块、方向去变现，本轮 <see cref="InertMailSource"/> 离线 inert，不连网。
    /// </remarks>
    public interface IMailSource
    {
        /// <summary>拉取待派发邮件（未来：从服务器拉全服 / 私人 / 定时邮件）。离线返空。</summary>
        System.Collections.Generic.IReadOnlyList<MailDraft> Pull();
    }

    /// <summary>
    /// 离线 stub：无服务器，返空、不连网（设计 21 §3.7 / §七 O1）。区服离线视单一本地区服（无多区概念，O2）。
    /// TODO: 未来上后端时实现一次（拉服务器邮件 → 转 <see cref="MailDraft"/> → <see cref="IMailService.Send"/>），服务层零改动。
    /// </summary>
    public sealed class InertMailSource : IMailSource
    {
        public System.Collections.Generic.IReadOnlyList<MailDraft> Pull()
            => System.Array.Empty<MailDraft>(); // 不抛、不连网
    }
}
