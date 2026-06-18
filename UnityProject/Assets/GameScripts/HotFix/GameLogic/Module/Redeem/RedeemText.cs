namespace GameLogic.Redeem
{
    /// <summary>
    /// 兑换结果文案 textId(占位常量,设计 30 §八 O6)。
    /// 真实多语言查表延后(同 num/item/reward/settings 现状);本轮给<b>互不相同的非 0 占位常量</b>,
    /// 上方注释写中文占位文案供 UI 参考。真实文本表建成后替换为多语言 id。
    /// </summary>
    /// <remarks>
    /// 各结果码文案<b>互不相同</b>(CV2:四类失败文案互异;CV3:服务不可用与码无效文案有别——
    /// 前者鼓励重试、码可兑,后者告知码本身不行,误判会让玩家以为好码失效,设计 30 §四)。
    /// </remarks>
    public static class RedeemText
    {
        /// <summary>「兑换成功,奖励已发放」。</summary>
        public const int Success            = 110601;
        /// <summary>「请输入兑换码」。</summary>
        public const int EmptyInput         = 110602;
        /// <summary>「兑换码无效」。</summary>
        public const int InvalidCode        = 110603;
        /// <summary>「该兑换码已使用过」。</summary>
        public const int AlreadyRedeemed    = 110604;
        /// <summary>「兑换码已过期」。</summary>
        public const int Expired            = 110605;
        /// <summary>「兑换码已被领完」(全局限量已满)。</summary>
        public const int LimitReached       = 110606;
        /// <summary>「兑换服务暂不可用,请稍后重试」。</summary>
        public const int ServiceUnavailable = 110607;

        /// <summary>结果码 → 文案 textId。</summary>
        public static int TextIdFor(RedeemResult r) => r switch
        {
            RedeemResult.Success            => Success,
            RedeemResult.EmptyInput         => EmptyInput,
            RedeemResult.InvalidCode        => InvalidCode,
            RedeemResult.AlreadyRedeemed    => AlreadyRedeemed,
            RedeemResult.Expired            => Expired,
            RedeemResult.LimitReached       => LimitReached,
            RedeemResult.ServiceUnavailable => ServiceUnavailable,
            _                               => 0,
        };
    }
}
