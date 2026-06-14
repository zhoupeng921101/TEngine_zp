using System;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 跨会话磁盘存档数据传输对象（设计 14 §3.2）。
    /// 扁平 [Serializable]，JsonUtility 友好：bool[] 直接可序列化、Dictionary 不进盘故无需拍平。
    /// 只承载 <see cref="MergeOrderState"/> 的元层进度（跨局累积、长期语义），不含局内瞬态（棋盘/手牌/订单/合成区/悔棋栈）。
    /// 版本字段随档落盘，加载时按 <see cref="MergeMetaPersistence.CurrentVersion"/> 决策迁移/重置（§3.5）。
    /// 与悔棋快照 <see cref="MergeOrderState"/>.Snapshot 是两条独立轨，互不调用。
    /// </summary>
    [Serializable]
    public sealed class MergeMetaSave
    {
        /// <summary>存档结构版本，当前 = <see cref="MergeMetaPersistence.CurrentVersion"/>。</summary>
        public int version;

        // ── 元层进度（设计 14 §3.1 表「是」的 13 项）──────────────
        public int soul;             // 灵力（软货币）
        public int piety;            // 虔诚币（长期主线货币）
        public int exp;              // 累积经验（守护者等级是其纯函数）
        public int unlockedChapter;  // 已解锁剧情章节数
        public int nextRepairIndex;  // 下一座待修神庙序号
        public bool[] templeRepaired;   // 各厅是否已修（长度 = TempleConfig.HallCount）
        public bool[] templeDecorated;  // 各厅是否已装饰
        public int blindBoxCount;    // 盲盒持有计数
        public int goddessRating;    // 当前档好评条计数
        public int goddessLevel;     // 女神好感等级（从 1 起）
        public int completedOrders;  // 累计完成单数
        public int totalScore;       // O2：默认进盘当累计总分

        // ── 每日字段（配跨天重置 §3.6）──────────────────────────
        public int wishUsedToday;            // 今日已用祈愿次数
        public string lastWishResetDate;     // 上次祈愿重置日期 yyyy-MM-dd（本地日期）
    }
}
