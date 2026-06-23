using System;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 融合主玩法「局内态」跨会话磁盘存档 DTO（2026-06-22 决定：真无尽局内态续存）。
    /// 与元层存档 <see cref="MergeMetaSave"/> 分键分层：元层承载跨局长期累积（虔诚币/神庙/女神/皮肤/体力…），
    /// 本 DTO 承载「上次中断瞬间的对局现场」——盘面 / 元素层 / 手牌 / 合成区 / 订单 / 连消 / 得分，
    /// 使再入室等价于「从上次落子处继续」。扁平 [Serializable]，JsonUtility 友好（Dictionary/Queue/jagged 全拍平为 1D）。
    /// </summary>
    [Serializable]
    public sealed class MergeIngameSave
    {
        /// <summary>存档结构版本。</summary>
        public int version;

        // ── 盘面 / 元素层 / 手牌（住在 BlockGameState）──────────────
        /// <summary>8×8 棋盘颜色拍平（-1=空，0..7=BlockColor）。</summary>
        public int[] flatBoard;
        /// <summary>8×8 元素叠加层拍平（(int)MergeElement，None=0）。</summary>
        public int[] flatElements;
        /// <summary>手持 3 槽候选块（落子后置空）。</summary>
        public IngamePieceData[] hand;
        /// <summary>当前局得分（局内瞬态，随局内态续存）。</summary>
        public int score;
        /// <summary>视觉连击镜像（BlockGameState.Combo）。</summary>
        public int combo;

        // ── 合成区 / 订单 / 连消（住在 MergeOrderState）─────────────
        public int orderCursor;       // 循环订单池游标
        public int completedOrders;   // 已完成单数
        public int totalScore;        // 累计交付得分
        public int comboChain;        // 连消链长（≥1）
        public bool allClearArmed;    // 全清武装位
        public int needRotor;         // 所需类型轮转游标

        // 合成区库存（键 (类型,等级) → 数量，平行三数组）
        public int[] invType;
        public int[] invLevel;
        public int[] invCount;

        // 激活订单（长度 = MergeOrderConfig.ActiveOrders，平行三数组）
        public int[] ordType;
        public int[] ordLevel;
        public int[] ordCount;

        // 元素预算队列（FIFO 顺序，(int)MergeElement）
        public int[] pending;
    }

    /// <summary>一个手持候选块的可序列化镜像（含 merge-order 元素携带）。</summary>
    [Serializable]
    public sealed class IngamePieceData
    {
        public bool isNull;
        public int shapeId;
        public int color;
        public bool hasAlgo;
        public int algo;
        /// <summary>该块按填充格行优先顺序携带的元素（(int)MergeElement）；无则 null/空。</summary>
        public int[] elements;
    }
}
