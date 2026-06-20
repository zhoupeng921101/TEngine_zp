namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 玩家元层属性类型(设计 38 §四)。
    /// 与服务端协议 <c>Fantasy.PropertyType</c>(Coin=0/Diamond=1/Stamina=2)一一映射;
    /// 客户端额外用 <see cref="All"/> 表 ApplySnapshot 触发的「全量刷新」事件,All 不入协议字段。
    /// </summary>
    /// <remarks>
    /// 别名取舍:服务端枚举叫 Coin / Diamond / Stamina;客户端历史叫 Gold / Diamond / Energy(BlockGameState.MergeState.Energy 局内态)。
    /// 本枚举对齐协议命名(Coin / Diamond / Stamina),避免两套命名漂移;PlayerAttrService 字段保协议命名,UI 文案侧本地化层处理。
    /// </remarks>
    public enum AttrType
    {
        /// <summary>全量(仅 ApplySnapshot 触发 OnAttrChanged 用,不入协议)。</summary>
        All = -1,
        /// <summary>金币(协议 PropertyType.Coin = 0)。</summary>
        Coin = 0,
        /// <summary>钻石(协议 PropertyType.Diamond = 1)。</summary>
        Diamond = 1,
        /// <summary>体力(协议 PropertyType.Stamina = 2)。</summary>
        Stamina = 2,
    }
}
