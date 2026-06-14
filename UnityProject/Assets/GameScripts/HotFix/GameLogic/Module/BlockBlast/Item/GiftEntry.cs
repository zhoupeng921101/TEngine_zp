namespace GameLogic.BlockBlast.Item
{
    /// <summary>
    /// 礼包池单项的运行期 POCO，隔离 Luban 生成类型 <c>GameConfig.GiftRandom</c> / <c>GameConfig.GiftSelect</c>
    /// （两表结构相同，复用同一 POCO，设计 16 §3.4）。
    /// </summary>
    /// <remarks>
    /// 随机礼包里 <see cref="Rate"/> 是权重（抽中概率 = rate / 同 index 全部 rate 之和）；
    /// 自选礼包里 <see cref="Rate"/> 仅作展示排序 / 保留字段（自选不抽，玩家手选）。
    /// <see cref="ItemId"/> 指向 <c>item.TbItemDef</c> 的 id：礼包奖品本身也是道具，
    /// 开出后该道具自己的 use_effect 决定落点（§3.7）。
    /// </remarks>
    public sealed class GiftEntry
    {
        /// <summary>奖品道具 id（指向 item.TbItemDef）。</summary>
        public int ItemId;
        /// <summary>该奖品数量。</summary>
        public int Num;
        /// <summary>权重（随机礼包用）/ 排序保留（自选礼包用）。</summary>
        public int Rate;
    }
}
