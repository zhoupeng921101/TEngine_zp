namespace GameLogic.BlockBlast.Item
{
    /// <summary>
    /// 道具定义的运行期 POCO，隔离 Luban 生成类型 <c>GameConfig.ItemDef</c>。
    /// 业务侧只认本类型，仿 <see cref="GameLogic.BlockBlast.Numeric.NumericEntry"/>（设计 16 §3.5）。
    /// </summary>
    /// <remarks>
    /// 字段全部 group=c,s（客户端目标保留），故可全量映射，无 helper 兜底缺字段。
    /// Quality/Type 存枚举底层 int（业务侧不依赖 Luban 枚举类型）。
    /// Term/TermPrompt/TermTime/Compensate/CompensateEmail/JumpList 为限时·补偿·跳转字段，
    /// 本轮进表不接逻辑（设计 16 §七 O5/O7 stub）。
    /// </remarks>
    public sealed class ItemDef
    {
        /// <summary>道具唯一 id，主键。</summary>
        public int Id;
        /// <summary>道具名（多语言文本 id）。</summary>
        public int Name;
        /// <summary>道具描述（多语言文本 id）。</summary>
        public int Desc;
        /// <summary>图标资源名（占位，无美术）。</summary>
        public string Icon;
        /// <summary>品质底层值 1–6（EItemQuality）。</summary>
        public int Quality;
        /// <summary>icon 特效 id / 路径（占位，无美术）。</summary>
        public string Light;
        /// <summary>获得时是否自动使用（0 否 / 1 是）。</summary>
        public int Automatic;
        /// <summary>类型底层值（EItemType：1 货币 / 2 材料 / 3 功能材料 / 5 自选礼包 / 6 随机礼包）。</summary>
        public int Type;
        /// <summary>参数：自选 / 随机礼包奖励数量 / 开启次数（UseEffect=3/4 时用）。</summary>
        public int Param;
        /// <summary>使用效果：0 无 / 1 num / 2 图案 / 3 自选礼包 id / 4 随机礼包 id。</summary>
        public int UseEffect;
        /// <summary>使用效果目标 id：=1 num_id / =2 图案 key / =3 自选 index / =4 随机 index。</summary>
        public int UseValue;
        /// <summary>使用效果数量：=1 货币数量 / =2 图案数量。</summary>
        public int UseNum;
        /// <summary>图案等级（=2 时 1–3；其余 0）。</summary>
        public int UseLevel;
        /// <summary>可叠放（0 否 / 1 是；叠加上限 999）。</summary>
        public int Stacking;
        /// <summary>限时（0 非限时 / 1 指定日期 / 2 指定时长）。本轮 stub。</summary>
        public int Term;
        /// <summary>限时提示（文本 id）。本轮 stub。</summary>
        public int TermPrompt;
        /// <summary>限时时间（日期串 / 时长秒）。本轮 stub。</summary>
        public string TermTime;
        /// <summary>到期补偿（Reward 表 id）。本轮 stub。</summary>
        public int Compensate;
        /// <summary>补偿邮件（邮件 id）。本轮 stub。</summary>
        public int CompensateEmail;
        /// <summary>获取跳转列表。本轮进表不接 UI。</summary>
        public int[] JumpList;
    }
}
