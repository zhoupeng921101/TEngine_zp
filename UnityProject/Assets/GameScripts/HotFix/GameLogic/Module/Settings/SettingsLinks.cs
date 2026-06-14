namespace GameLogic.Settings
{
    /// <summary>
    /// 延后项的钩子常量与 stub（设计 19 §3.6 / §七）。spec 列了多个「跳转到其它系统」入口，多数系统未建。
    /// 本轮按四档处置，不投机性建未来用不上的接口：
    /// 占位 URL 常量（协议/隐私）/ stub 常量（客服，spec 自身标待定）/ TODO 注释钩子（兑换码/新手关，依赖未建系统）/ 不做（快捷登录）。
    /// </summary>
    public static class SettingsLinks
    {
        // 用户协议 / 隐私政策：占位 URL 常量。UI 接时用 Application.OpenURL（设计 19 §七 O3）。
        /// <summary>用户协议 URL（占位，替换真实 URL 见 O3）。</summary>
        public const string UserAgreementUrl = "https://example.com/terms";

        /// <summary>隐私政策 URL（占位，替换真实 URL 见 O3）。</summary>
        public const string PrivacyPolicyUrl = "https://example.com/privacy";

        // 联系客服：spec 标「待定」，形态未定（邮箱/网页/工单），留 stub 常量（设计 19 §七 O2）。
        /// <summary>联系客服（stub，spec 形态待定后补，见 O2）。</summary>
        public const string ContactSupport = "";

        // 新手说明 → 新手关卡（O5）/ 兑换码入口（O4）：UI 接到真实系统时填。
        // 不投机性建接口，仅留 TODO 钩子：
        //   - OpenTutorial()   依赖未建的新手 / 教学关
        //   - OpenRedeemCode() 兑换码系统（设计 20）已建数据逻辑层：服务入口
        //       new GameLogic.Redeem.RedeemService(
        //           new GameLogic.Redeem.LocalConfigRedeemValidator(),
        //           new GameLogic.Redeem.PersistenceRedeemStore())
        //         .Redeem(玩家输入码, MergeOrderState, System.Random)
        //       UI 投放时建输入窗口 + 结果弹窗，按 RedeemOutcome.Result/TextId 提示、Granted 转 RewardView（设计 17）展示。
        //       本轮只接服务入口注释，不建窗口（设计 20 §七 O7）。
        //
        // 快捷登录（O8）= 不做：离线无账号系统（同设计 18 账号绑定 out），不留钩子。
    }
}
