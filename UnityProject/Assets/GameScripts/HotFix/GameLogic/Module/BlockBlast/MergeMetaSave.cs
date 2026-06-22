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

        // ── 元层进度（设计 14 §3.1 表「是」的 11 项）──────────────
        // 完成单数与本局得分不进盘：二者是单局瞬态，由 MergeOrderState.Reset 每局清零，跨会话累计会污染本局通关判定与结算显示（设计 14 O2 降级、设计 29 L139）。
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
        public int highScore;        // 经典遗产「最高分」（设计 29 §5.4）：跨会话长期指标，与元层进度同时机落盘/加载。旧档缺此字段 JsonUtility 给缺省 0

        // ── 每日字段（配跨天重置 §3.6）──────────────────────────
        public int wishUsedToday;            // 今日已用祈愿次数
        public string lastWishResetDate;     // 上次祈愿重置日期 yyyy-MM-dd（本地日期）

        // ── 体力进盘 + 离线时基恢复（无尽模型，设计 14 §3.7 / 设计 49 §3.2；CurrentVersion 不升，旧档缺省 0）──
        // 体力从「不进盘」改为「进盘」：无尽模型下「关掉游戏等体力恢复」是设计要的脱困路径（离线恢复 = 预期行为，非漏洞）。
        // 旧档缺这两字段 → JsonUtility 给缺省 0：energy=0 由 ImportMeta 夹回起始体力；lastEnergyRegenTime=0 = 尚无记录，
        // 加载后 ApplyTimeRegen(now) 以 now 初始化、本次不补（设计 49 §3.2 崩法三）。
        public int energy;                   // 当前体力（无尽节流资源，进盘）
        public long lastEnergyRegenTime;     // 上次时基恢复结算时刻（Unix 秒，本地时钟；0 = 尚无记录）

        // ── 方块皮肤态（设计 50 §六；做法同 18 §3.8 平铺，既有字段不动，CurrentVersion 不升）──
        // 皮肤态是「已达成全清」的只增成就标记（单色不退回彩色，设计 50 §三 规则 4），跨会话单调累积——
        // 语义与元层进度（goddessLevel/highScore 等只增字段）同类，故并入元层 DTO 落盘（与 goddessLevel 同时机加载/落盘）。
        // 旧档缺这两字段 → JsonUtility 给缺省（false/0）→ BlockSkinState.Import 判为彩色态 + 未选（= 初始态，符合「初始 = 彩色」）。
        public bool skinMono;                // 是否单色态（false = 彩色态，含缺省）
        public int skinMonoId;               // 单色态当前在用 sprite 编号（彩色态 = BlockSkinState.Unselected）

        // ── 玩家信息字段（设计 18 §3.8 做法 a 平铺；既有字段一行不改，CurrentVersion 不升）──
        // 旧档缺这些字段 → JsonUtility 给缺省（""/0/null）；保底夹值在 PlayerInfo.ImportFromMeta（§3.8 注）。
        public string playerId;              // 玩家本地唯一 id
        public string playerName;            // 当前昵称
        public int playerRenameCount;        // 已改名次数
        public int playerExp;                // 玩家账号经验（独立第三进度线，不复用 exp/守护者）
        public int curAvatarId;              // 当前佩戴头像 id
        public int curFrameId;               // 当前佩戴头像框 id
        public int[] unlockedAvatarIds;      // 已解锁头像 id 集合（JsonUtility 序列化 int[]）
        public int[] unlockedFrameIds;       // 已解锁头像框 id 集合
    }
}
