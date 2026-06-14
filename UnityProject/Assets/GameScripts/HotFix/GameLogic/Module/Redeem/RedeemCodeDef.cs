using System.Collections.Generic;

namespace GameLogic.Redeem
{
    /// <summary>
    /// 单个奖励项的运行期 POCO（设计 20 §3.2）。
    /// 兑换码奖励 = 一组「道具 id × 数量」，发放复用道具系统既有落点（设计 16），
    /// 故本结构只声明「发哪个道具、发几个」，不重复定义奖励类型。
    /// </summary>
    public sealed class RedeemReward
    {
        /// <summary>奖励道具 id（指向 <c>item.TbItemDef</c>，设计 16）。</summary>
        public int ItemId;
        /// <summary>奖励数量（传给 <c>ItemGrant.GrantOnAcquire(def, num, ...)</c>）。</summary>
        public int Num;
    }

    /// <summary>
    /// 兑换码定义的运行期 POCO，隔离 Luban 生成类型 <c>GameConfig.RedeemCode</c> / <c>GameConfig.RedeemReward</c>。
    /// 业务侧（校验 / 服务）只认本类型，仿 <see cref="GameLogic.BlockBlast.Item.ItemDef"/>（设计 20 §3.2）。
    /// </summary>
    /// <remarks>
    /// <see cref="Code"/> 存<b>规整后（大写）</b>的码，与服务层 <c>RedeemService.Normalize</c> 比对口径一致；
    /// 奖励子表按 code 聚合到 <see cref="Rewards"/>。<see cref="ExpireTime"/> 空 = 不限时。
    /// </remarks>
    public sealed class RedeemCodeDef
    {
        /// <summary>兑换码（已规整 / 大写，主键）。</summary>
        public string Code;
        /// <summary>码名称 / 用途（多语言文本 id）。</summary>
        public int Name;
        /// <summary>每玩家是否仅一次：1 是（进去重集合）/ 0 否（可重复兑）。默认 1。</summary>
        public int OncePerPlayer = 1;
        /// <summary>过期时间（日期串，空 = 不限时）。判定经可注入 NowProvider（设计 20 §3.5 / §七 O4）。</summary>
        public string ExpireTime;
        /// <summary>该码发放的奖励项（按 code 聚合，可多项）。</summary>
        public List<RedeemReward> Rewards = new List<RedeemReward>();
    }
}
