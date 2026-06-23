using System.Collections.Generic;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 单次落子的结算结果（设计 11 §5.5 流水线产出）。供窗口刷新弹字/分数/收集区/女神/全清表现。
    /// 纯数据，无副作用——副作用已在 <see cref="ClearSettlement.Settle"/> 内对 MergeOrderState 施加。
    /// </summary>
    public readonly struct SettlementResult
    {
        /// <summary>本次落子被清的行列总数（多消数）。0 = 无消除。</summary>
        public readonly int Lines;
        /// <summary>基础消除得分（未乘连消，用于元素产出与全清判定）。</summary>
        public readonly int BaseScore;
        /// <summary>显示分 = 基础分 × 连消倍率（仅展示用，不驱动经济）。</summary>
        public readonly int DisplayScore;
        /// <summary>结算后连消链长（1 起；本手无消除则为 1）。</summary>
        public readonly int ComboChain;
        /// <summary>多消即时弹字（无消除为空串）。</summary>
        public readonly string MultiLabel;
        /// <summary>本手是否发生全清且发奖（武装位曾为 true）。</summary>
        public readonly bool AllClearRewarded;
        /// <summary>全清发奖触发的女神升档（仅 AllClearRewarded 时有意义）。</summary>
        public readonly bool GoddessLeveledUp;
        /// <summary>得分驱动的基础 Lv1 元素产出数（已入预算队列的 k）。</summary>
        public readonly int BaseElementsK;
        /// <summary>本手获得的盲盒数（全清解锁 + 连消阈值解锁，设计 12 §3.4）。供窗口弹「+1 🔮」提示。</summary>
        public readonly int BlindBoxGained;

        public SettlementResult(int lines, int baseScore, int displayScore, int comboChain,
            string multiLabel, bool allClearRewarded, bool goddessLeveledUp, int baseElementsK,
            int blindBoxGained)
        {
            Lines = lines;
            BaseScore = baseScore;
            DisplayScore = displayScore;
            ComboChain = comboChain;
            MultiLabel = multiLabel;
            AllClearRewarded = allClearRewarded;
            GoddessLeveledUp = goddessLeveledUp;
            BaseElementsK = baseElementsK;
            BlindBoxGained = blindBoxGained;
        }
    }

    /// <summary>
    /// 连消 / 多消 / 全清结算流水线（设计 11 §5.5，顺序固定，可单测）。
    ///
    /// 顺序铁律：连消倍率只乘【显示分】，绝不参与【元素产出】与【全清判定】。
    /// 这条隔离保证「会连消的玩家拿高分爽感」与「图案经济不被连消通胀」互不污染。
    ///
    /// 注意：本模块只负责落子后「判定消除之后」的结算副作用——
    /// 扣体力(§7)、IngestElement(逐 Lv1 入收集区)、RefundEnergy 由窗口在调用本模块前完成，
    /// 因为它们涉及棋盘/overlay 的实际清除。本模块接管：连消链推进、显示分、得分驱动产出入队、
    /// 多消里程碑直发、全清武装位 + 全清奖 + 女神推进。
    /// </summary>
    public static class ClearSettlement
    {
        /// <summary>
        /// 执行结算。<paramref name="lines"/>=本次落子清的行列总数（0=无消除），
        /// <paramref name="clearedCells"/>=被清格数（驱动 ClearScore），
        /// <paramref name="boardEmptyAfter"/>=消除后棋盘是否清空（全清判定）。
        /// 里程碑直发的图案类型用 <paramref name="milestoneType"/>（窗口取当前订单所需类型之一；
        /// 传 None 时回退 Star，保证产物有归属）。
        /// </summary>
        public static SettlementResult Settle(
            MergeOrderState m, int lines, int clearedCells, bool boardEmptyAfter,
            MergeElement milestoneType)
        {
            // —— 步 2：无消除分支 —— 连消链清零、全清武装位重置、结束 ——
            if (lines <= 0)
            {
                m.ComboChain = 1;            // 链断回基准
                m.AllClearArmed = true;      // 一次非全清落子重新武装全清奖
                return new SettlementResult(0, 0, 0, 1, string.Empty, false, false, 0, 0);
            }

            // —— 步 3：基础消除得分（复用 BlockScoring 单一信息源）——
            int baseScore = BlockScoring.ClearScore(clearedCells, lines);

            // —— 步 4：连消加成（仅作用显示分）——
            m.ComboChain += 1;
            int permille = MergeOrderConfig.ComboMultPermilleFor(m.ComboChain);
            int displayScore = baseScore * permille / 1000;

            // 盲盒解锁累加器（设计 12 §3.4）：本手获得的盲盒数（连消阈值 + 全清解锁两路）。
            int blindBoxGained = 0;

            // 连消阈值解锁：用 == 而非 >=，每条连消链跨过阈值那一手发一次（链更长不重复发，链断回 1 后重新计）。
            if (m.ComboChain == TarotBlindBoxConfig.BoxComboThreshold)
            {
                m.AddBlindBox(1);
                blindBoxGained += 1;
            }

            // —— 步 5：元素产出（用【未乘连消】的 baseScore）——
            int k = MergeOrderConfig.ElementsForScore(baseScore);
            m.EnqueueScoreElements(k); // 基础产出 k 个 Lv1 入预算队列（得分驱动，现状逻辑）

            // 多消里程碑加码：lines≥3 额外直发 Lv2/Lv3 进收集区（跳过合成）
            if (milestoneType == MergeElement.None) milestoneType = MergeElement.Star;
            foreach (var (level, count) in MergeOrderConfig.MultiClearMilestoneBonus(lines))
                m.AddDirect(milestoneType, level, count);

            // —— 步 6：全清判定（用棋盘空 + 武装位，与连消倍率无关）——
            bool allClearRewarded = false;
            bool goddessLeveledUp = false;
            if (boardEmptyAfter && m.AllClearArmed)
            {
                // 全清奖：1 个 Lv3 高级图案（设计 11 §5.4）
                m.AddDirect(milestoneType, MergeOrderConfig.AllClearRewardLevel,
                    MergeOrderConfig.AllClearRewardCount);
                // 推进女神好评条 +1（换背景统一归女神升级触发，设计 11 §十·去重）
                goddessLeveledUp = m.AdvanceGoddess();
                // 全清解锁盲盒 +1（设计 12 §3.4，复用全清武装位：连续第 2 次全清不发）
                m.AddBlindBox(1);
                blindBoxGained += 1;
                m.AllClearArmed = false; // 本次已发，连续第二次不再发
                allClearRewarded = true;
            }
            else if (!boardEmptyAfter)
            {
                m.AllClearArmed = true;  // 非全清落子 → 重新武装
            }
            // 注：boardEmptyAfter 且 !Armed（连续第二次全清）→ 不发奖、武装位保持 false。

            return new SettlementResult(
                lines, baseScore, displayScore, m.ComboChain,
                MergeOrderConfig.MultiClearLabelFor(lines),
                allClearRewarded, goddessLeveledUp, k, blindBoxGained);
        }
    }
}
