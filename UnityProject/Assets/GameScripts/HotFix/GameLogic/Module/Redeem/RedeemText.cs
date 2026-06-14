namespace GameLogic.Redeem
{
    /// <summary>
    /// 兑换结果文案 textId（占位常量，设计 20 §3.7 / §七 O6）。
    /// 真实多语言查表延后（同 num/item/reward/settings 现状）；本轮给<b>互不相同的非 0 占位常量</b>，
    /// 上方注释写中文占位文案供 UI 参考。真实文本表建成后替换为多语言 id。
    /// </summary>
    public static class RedeemText
    {
        /// <summary>「兑换成功，奖励已发放」。</summary>
        public const int Success           = 110601;
        /// <summary>「请输入兑换码」。</summary>
        public const int EmptyInput        = 110602;
        /// <summary>「兑换码无效」。</summary>
        public const int NotFound          = 110603;
        /// <summary>「该兑换码已使用过」。</summary>
        public const int AlreadyRedeemed   = 110604;
        /// <summary>「兑换码已过期」。</summary>
        public const int Expired           = 110605;
        /// <summary>「兑换服务暂不可用」。</summary>
        public const int SourceUnavailable = 110606;

        /// <summary>结果码 → 文案 textId。</summary>
        public static int TextIdFor(RedeemResult r) => r switch
        {
            RedeemResult.Success           => Success,
            RedeemResult.EmptyInput        => EmptyInput,
            RedeemResult.NotFound          => NotFound,
            RedeemResult.AlreadyRedeemed   => AlreadyRedeemed,
            RedeemResult.Expired           => Expired,
            RedeemResult.SourceUnavailable => SourceUnavailable,
            _                              => 0,
        };
    }
}
