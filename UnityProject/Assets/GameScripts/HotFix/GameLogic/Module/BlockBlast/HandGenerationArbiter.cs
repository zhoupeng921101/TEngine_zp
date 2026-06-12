using GameLogic.BlockBlast.Algorithms;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 智能生成发牌的运行上下文（设计 11 §八）。计数器跨手累积，每次落子后由 <see cref="OnPlaced"/> 推进。
    /// 纯数据 + 计数推进，棋盘读取在仲裁器内做。
    /// </summary>
    public sealed class HandGenContext
    {
        /// <summary>连续未消除落子数（R2 防卡死触发量）。</summary>
        public int NoClearStreak;

        /// <summary>清盘后剩余的「连发异形」配额（R1 增难触发量，全清后武装 AntiStreakCount）。</summary>
        public int AntiStreak;

        public void Reset()
        {
            NoClearStreak = 0;
            AntiStreak = 0;
        }

        /// <summary>
        /// 每次落子后推进计数器（设计 11 §8.1 OnPlaced）。
        /// 无消除 → NoClearStreak+1；有消除 → 归 0。全清 → AntiStreak 武装为 AntiStreakCount。
        /// </summary>
        public void OnPlaced(int clearedLines, bool isAllClear)
        {
            if (clearedLines == 0) NoClearStreak += 1;
            else NoClearStreak = 0;
            if (isAllClear) AntiStreak = MergeOrderConfig.AntiStreakCount;
        }
    }

    /// <summary>
    /// 智能生成仲裁器（设计 11 §八）：R1–R3 上层仲裁套在现状 dynamicWeight(R4) 之外。
    ///
    /// 优先级链（高→低）：
    ///   P0 R2 防卡死         —— noClearStreak ≥ 阈值，玩家受苦，最高
    ///   P1 R1 清盘后增难      —— antiStreak &gt; 0，全清后连发异形
    ///   P2 R4 清盘引导        —— 剩余非空格 ≤ 阈值，促全清
    ///   P3 R3 高阶引导        —— 存在多消机会，促高多消
    ///   P4 常规 dynamicWeight —— 都不触发，回落现状发牌（行为零改变）
    ///
    /// 裁决铁律：任一手最终只采纳一条规则；高优先级独占，低优先级让位。
    /// P0 与 P1 永不同时（R2 要能消、R1 要难消，直接矛盾）——P0 优先时 antiStreak 此手不递减。
    ///
    /// 本仲裁器只「决定用哪种算法发牌」，不改 dynamicWeight 本身：
    /// <see cref="Decide"/> 返回 (是否接管, 算法)。返回「不接管」即回落 dynamicWeight，
    /// 保证四规则都不触发时现状行为逐字节不变。算法到 trio 的产出复用 BlockAlgorithms。
    /// </summary>
    public static class HandGenerationArbiter
    {
        public readonly struct Decision
        {
            /// <summary>true=本仲裁器接管发牌（用 Algo）；false=回落 dynamicWeight(R4) 现状发牌。</summary>
            public readonly bool Override;
            /// <summary>接管时采用的算法（仅 Override==true 有意义）。</summary>
            public readonly AlgorithmKind Algo;
            /// <summary>命中的规则标签（调试/测试可读）。</summary>
            public readonly string Rule;

            public Decision(bool over, AlgorithmKind algo, string rule)
            {
                Override = over;
                Algo = algo;
                Rule = rule;
            }
        }

        /// <summary>回落标记（不接管）。</summary>
        public static readonly Decision Fallthrough = new Decision(false, default, "P4_DynamicWeight");

        /// <summary>
        /// 仲裁单手发牌（设计 11 §8.1 SelectBlockForSlot 的逐组版：本作发整组 trio，逐组仲裁）。
        /// 不在此推进计数器（计数推进归 <see cref="HandGenContext.OnPlaced"/>，每次落子触发）。
        /// </summary>
        public static Decision Decide(BinaryBoard board, HandGenContext ctx)
        {
            // —— P0 R2 防卡死（最高，无条件优先）——
            if (ctx.NoClearStreak >= MergeOrderConfig.NoClearThreshold)
                return new Decision(true, AlgorithmKind.Fill, "P0_NoDie");

            // —— P1 R1 清盘后增难 ——
            if (ctx.AntiStreak > 0)
            {
                ctx.AntiStreak -= 1;                 // 消耗一个异形配额
                return new Decision(true, AlgorithmKind.Diff, "P1_AntiStreak");
            }

            // —— P2 R4 清盘引导 ——
            if (FilledCount(board) <= MergeOrderConfig.ClearGuideThreshold && !board.IsEmpty())
                return new Decision(true, AlgorithmKind.ClearAll, "P2_ClearGuide");

            // —— P3 R3 高阶消除引导 ——
            // 存在多消机会的近似判据：有近完成行（缺 1~3 格），补上即可达成多消。
            if (HasMultiClearOpportunity(board))
                return new Decision(true, AlgorithmKind.AllCombination, "P3_MultiGuide");

            // —— P4 回落现状 dynamicWeight ——
            return Fallthrough;
        }

        private static int FilledCount(BinaryBoard board)
        {
            int n = 0;
            for (int r = 0; r < BinaryBoard.RowCount; r++)
            {
                int row = board.RowBinary[r];
                while (row != 0) { n += row & 1; row >>= 1; }
            }
            return n;
        }

        // 近似「再放某形状即可三消+」：统计近完成行(缺1~3)+近完成列(缺1~3)，合计 ≥ MultiGuideMinLines。
        private static bool HasMultiClearOpportunity(BinaryBoard board)
        {
            int near = 0;
            // 行
            for (int r = 0; r < BinaryBoard.RowCount; r++)
            {
                int missing = BinaryBoard.ColCount - PopCount(board.RowBinary[r]);
                if (missing >= 1 && missing <= 3) near++;
            }
            // 列
            for (int c = 0; c < BinaryBoard.ColCount; c++)
            {
                int mask = 1 << (BinaryBoard.ColCount - c - 1);
                int filled = 0;
                for (int r = 0; r < BinaryBoard.RowCount; r++)
                    if ((board.RowBinary[r] & mask) != 0) filled++;
                int missing = BinaryBoard.RowCount - filled;
                if (missing >= 1 && missing <= 3) near++;
            }
            return near >= MergeOrderConfig.MultiGuideMinLines;
        }

        private static int PopCount(int x)
        {
            int n = 0;
            while (x != 0) { n += x & 1; x >>= 1; }
            return n;
        }
    }
}
