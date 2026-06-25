namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 玩家元层属性类型(设计 38 §四 + P2 全栈迁移扩四货币)。
    /// 整数值与服务端协议 <c>Fantasy.PropertyType</c> 一一对齐(Coin=0/Diamond=1/Stamina=2/SoulPower=3/Piety=4/GuardianExp=5/Energy=6),
    /// 故 <c>(PropertyType)attrType</c> / <c>(AttrType)propertyType</c> 可直接互转;客户端额外用 <see cref="All"/> 表
    /// ApplySnapshot 触发的「全量刷新」事件,All 不入协议字段。
    /// </summary>
    /// <remarks>
    /// 别名取舍:服务端枚举叫 Coin / Diamond / Stamina;客户端历史叫 Gold / Diamond / Energy(BlockGameState.MergeState.Energy 局内态)。
    /// 本枚举对齐协议命名,避免两套命名漂移;PlayerAttrService 字段保协议命名,UI 文案侧本地化层处理。
    /// 四货币(SoulPower/Piety/GuardianExp/Energy)对应玩法元层 <c>MergeOrderState</c> 的 Soul/Piety/Exp/Energy,
    /// 经 <c>MetaCurrencySync</c> 把本地视图与服务端权威对账(P2 客户端段)。
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
        /// <summary>灵力(玩法软货币,协议 PropertyType.SoulPower = 3,对应 MergeOrderState.Soul)。</summary>
        SoulPower = 3,
        /// <summary>虔诚币(长期主线货币,协议 PropertyType.Piety = 4,对应 MergeOrderState.Piety)。</summary>
        Piety = 4,
        /// <summary>守护者累积经验(协议 PropertyType.GuardianExp = 5,对应 MergeOrderState.Exp)。</summary>
        GuardianExp = 5,
        /// <summary>玩法体力(带离线恢复,协议 PropertyType.Energy = 6,对应 MergeOrderState.Energy)。</summary>
        Energy = 6,
    }
}
