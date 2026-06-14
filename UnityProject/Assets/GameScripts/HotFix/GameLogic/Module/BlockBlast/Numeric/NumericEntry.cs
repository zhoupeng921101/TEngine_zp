namespace GameLogic.BlockBlast.Numeric
{
    /// <summary>
    /// 数值底层货币的运行期 POCO，隔离 Luban 生成类型 <c>GameConfig.Num</c>。
    /// 业务侧只认本类型，仿 <see cref="GameLogic.BlockBlast.Algorithms.WeightConfigEntry"/>。
    /// </summary>
    /// <remarks>
    /// 字段对应 num 货币表（设计 15 §3.3）。func_name(group=s) 与 planner_notes(group=e)
    /// 不导出到客户端目标（client target groups=["c"]），客户端生成的 <c>GameConfig.Num</c>
    /// 不含这两字段，故 POCO 也不含——查不到时由 helper 兜底（用 num_id / name 文本 id）。
    /// </remarks>
    public sealed class NumericEntry
    {
        /// <summary>num_id，主键，= Luban Num.Id。</summary>
        public int NumId;
        /// <summary>用途描述文本 id，num_desc。</summary>
        public int DescTextId;
        /// <summary>名称文本 id，num_name。</summary>
        public int NameTextId;
        /// <summary>图标资源名，num_no（helper 用）。</summary>
        public string IconName;
        /// <summary>资源类型底层 int（1=经验…4=体力），num_type 的 ENumType 底层值。</summary>
        public int NumType;
        /// <summary>数值品质，quality（影响界面显示色）。</summary>
        public int Quality;
    }
}
