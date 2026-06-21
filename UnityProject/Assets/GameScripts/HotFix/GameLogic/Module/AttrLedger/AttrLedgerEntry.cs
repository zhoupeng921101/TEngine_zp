using GameLogic.BlockBlast.Player;

namespace GameLogic.AttrLedger
{
    /// <summary>
    /// 玩家属性 ledger 单条流水(客户端 POCO,设计 46 §3.1)。
    /// 7 字段与 45 §3.2 协议响应 entry 一一映射,**不**含协议生成物中可能存在的内部字段;
    /// 反序列化由 <see cref="RemoteAttrLedgerSource"/> 完成,业务层(UI / 服务编排)只持本结构。
    /// </summary>
    public sealed class AttrLedgerEntry
    {
        /// <summary>应用端 Unix 毫秒 UTC(= 44 §3.1 <c>Timestamp</c> 同源)。</summary>
        public long Timestamp;
        /// <summary>属性种类(沿 38 已建 <see cref="AttrType"/>,Coin / Diamond / Stamina 三档)。</summary>
        public AttrType Kind;
        /// <summary>变更前余额(非负)。</summary>
        public long BalanceBefore;
        /// <summary>变更后余额(非负;invariant <c>BalanceAfter = BalanceBefore + Delta</c>)。</summary>
        public long BalanceAfter;
        /// <summary>相对变更量(有符号;正 = 收益、负 = 消费)。</summary>
        public long Delta;
        /// <summary>变更来源枚举码(本子单 <see cref="AttrChangeSource"/>,与 44 §3.3 一一映射)。</summary>
        public AttrChangeSource Source;
        /// <summary>原 reason 字符串(便于运营 ad-hoc 查子分类如 mailId / codeId / rankIdx;UI 不展示,运营查 mongo 用)。</summary>
        public string ReasonRaw;
    }
}
