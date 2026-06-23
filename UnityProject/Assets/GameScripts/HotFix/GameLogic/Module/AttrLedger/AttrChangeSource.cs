namespace GameLogic.AttrLedger
{
    /// <summary>
    /// 玩家属性变更来源枚举(客户端段,设计 46 §3.4 + 44 §3.3)。
    /// 整数值与服务端 <c>Fantasy.AttrChangeSource</c> 同源(44 已登记 10 档),客户端独立定义防引服务端命名空间。
    /// </summary>
    /// <remarks>
    /// 协议层 <c>AttrLedgerEntry.Source</c> 是 int(沿 45 §3.2 协议生成物),反序列化时按整数转本枚举;
    /// 未知整数 → <see cref="Unknown"/>(0)兜底,显文案「其他」(设计 46 §3.4 default 分支)。
    /// </remarks>
    public enum AttrChangeSource
    {
        /// <summary>未知 / 兜底(服务端 source 映射未命中或客户端不识别的整数)。</summary>
        Unknown = 0,
        /// <summary>改名扣钻(38 PlayerRenameService → 37 ChangeProperty)。</summary>
        ChangeNameSpend = 1,
        /// <summary>邮件领奖(32 MailClaim → 37 ChangeProperty)。</summary>
        MailClaim = 2,
        /// <summary>兑换码奖励(30 RedeemCode → 37 ChangeProperty)。</summary>
        RedeemCode = 3,
        /// <summary>排行榜结算奖励(33 RankSettleReward → 37 ChangeProperty)。</summary>
        RankSettleReward = 4,
        /// <summary>活动奖励(39/40/43 Activity → 37 ChangeProperty)。</summary>
        ActivityReward = 5,
        /// <summary>玩法消费(Tier 2+ 玩法阶段占位)。</summary>
        GameplayConsume = 6,
        /// <summary>商店购买(Tier 2+ 商店阶段占位)。</summary>
        ShopPurchase = 7,
        /// <summary>管理员发放(Tier 2+ GM 阶段占位)。</summary>
        AdminGrant = 8,
        /// <summary>退款(Tier 2+ 退款阶段占位)。</summary>
        Refund = 9,
    }
}
