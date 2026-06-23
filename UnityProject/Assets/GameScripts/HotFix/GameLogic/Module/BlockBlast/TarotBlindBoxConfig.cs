using System.Collections.Generic;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 盲盒掷出的奖项类型（设计 12 §3.1）。开一个盲盒掷出恰好 1 项（非三选一）。
    /// </summary>
    public enum BlindBoxRewardKind
    {
        PatternLow = 0,  // Lv1 图案 ×1
        PatternMid = 1,  // Lv2 图案 ×1
        Energy = 2,      // 体力 +BoxEnergyGain
        PatternHigh = 3, // 封顶等级（Lv MaxLevel）图案 ×1（高阶物）
        NeededHigh = 4,  // 当前订单缺口的最高等级图案 ×1（无缺口降级 PatternHigh）
    }

    /// <summary>
    /// 一次开盒掷出的奖励（已应用 NeededHigh 降级保底，落点信息齐全可直接发放）。
    /// 纯数据值类型，无副作用：发放由 <see cref="MergeOrderState.OpenBlindBox"/> 按此施加。
    /// </summary>
    public readonly struct BlindBoxReward
    {
        /// <summary>掷出的奖项（NeededHigh 无缺口时已降级为 PatternHigh）。</summary>
        public readonly BlindBoxRewardKind Kind;
        /// <summary>图案归属类型（Energy 项为 None）。</summary>
        public readonly MergeElement PatternType;
        /// <summary>图案等级（Energy 项为 0；图案项 1~MaxLevel）。</summary>
        public readonly int PatternLevel;
        /// <summary>图案数量（图案项恒 1；Energy 项为 0）。</summary>
        public readonly int PatternCount;
        /// <summary>体力增量（Energy 项 = BoxEnergyGain；图案项为 0）。</summary>
        public readonly int EnergyGain;

        public BlindBoxReward(BlindBoxRewardKind kind, MergeElement patternType,
            int patternLevel, int patternCount, int energyGain)
        {
            Kind = kind;
            PatternType = patternType;
            PatternLevel = patternLevel;
            PatternCount = patternCount;
            EnergyGain = energyGain;
        }

        /// <summary>是否为图案类奖励（落点走 AddDirect）。</summary>
        public bool IsPattern => PatternType != MergeElement.None && PatternLevel >= 1 && PatternCount > 0;
        /// <summary>是否为体力类奖励（落点走 RefundEnergy）。</summary>
        public bool IsEnergy => EnergyGain > 0;
    }

    /// <summary>
    /// 神秘塔罗盲盒奖池配置（设计 12 §三）。静态类，仿 <see cref="MergeOrderConfig"/> / <see cref="ChestSystem"/>
    /// 风格，不接 Luban。用 <see cref="RandomSource"/> 做确定性加权抽样（可单测，与 ChestSystem.RollRewards 同源）。
    ///
    /// 双重保底（§3.2）：
    /// · 下限保底——权重表无 0 价值项，任意 seed 必产有效奖（最低 1 个 Lv1 图案或体力）；
    /// · NeededHigh 降级——抽中 NeededHigh 但当前无订单缺口 → 降级为通用 PatternHigh（封顶 Lv MaxLevel），不浪费大奖。
    /// </summary>
    public static class TarotBlindBoxConfig
    {
        // ── 奖池权重（设计 12 §3.1，和 = 100，占比即权重便于读）─────────────
        // 索引 = BlindBoxRewardKind 枚举值。调大某项即提高该项出率。
        /// <summary>奖池权重：Low40 / Mid22 / Energy20 / High10 / NeededHigh8（和 100）。</summary>
        public static readonly int[] BoxRewardWeights = { 40, 22, 20, 10, 8 };

        // ── 产物常量（设计 12 §三默认拍板）─────────────────────────────
        /// <summary>体力档：Energy 奖项补回的体力（受 EnergyCap 约束，不溢出）。</summary>
        public const int BoxEnergyGain = 10;

        /// <summary>连消解锁阈值：ComboChain 跨到此值那一手发 1 盲盒（用 == 单次跨阈，不重复发）。</summary>
        public const int BoxComboThreshold = 4;

        /// <summary>特殊订单附赠：加急单交付附赠盲盒数。</summary>
        public const int BoxPerExpress = 1;
        /// <summary>特殊订单附赠：剧情单交付附赠盲盒数（主线奖励更重）。</summary>
        public const int BoxPerStory = 2;
        /// <summary>特殊订单附赠：黄金时段单交付附赠盲盒数。</summary>
        public const int BoxPerGolden = 1;

        /// <summary>持有计数软上限旋钮（本轮不设硬上限，默认 99 备用，设计 12 §七 O2）。</summary>
        public const int BoxHoldCap = 99;

        /// <summary>各 Kind 图案的固定产物等级（Energy 项 0）。索引 = BlindBoxRewardKind。</summary>
        private static readonly int[] PatternLevelOf = { 1, 2, 0, MergeOrderConfig.MaxLevel, MergeOrderConfig.MaxLevel };

        /// <summary>
        /// 加权抽样掷出恰好 1 项奖励（设计 12 §3.2）。用 RandomSource 确定性随机，可单测。
        /// 已应用 NeededHigh 降级保底：抽中 NeededHigh 但 state 无订单缺口 → 返回降级后的 PatternHigh。
        /// 返回的 BlindBoxReward 落点信息齐全（图案归属类型/等级或体力增量），调用方按此直接发放。
        /// </summary>
        public static BlindBoxReward RollReward(MergeOrderState state)
        {
            int total = 0;
            for (int i = 0; i < BoxRewardWeights.Length; i++) total += BoxRewardWeights[i];

            int r = RandomSource.Range(0, total); // [0, total)
            int acc = 0;
            int kindIdx = BoxRewardWeights.Length - 1; // 兜底落末项（理论不触达，r<total 必落区间）
            for (int i = 0; i < BoxRewardWeights.Length; i++)
            {
                acc += BoxRewardWeights[i];
                if (r < acc) { kindIdx = i; break; }
            }

            return Materialize((BlindBoxRewardKind)kindIdx, state);
        }

        /// <summary>
        /// 把抽中的 Kind 落成具体奖励（图案归属类型/等级、或体力增量）。
        /// NeededHigh 在此查 state 缺口；无缺口降级 PatternHigh。图案归属类型取 NeededTypes 之一（轮转），空回退 Star。
        /// </summary>
        private static BlindBoxReward Materialize(BlindBoxRewardKind kind, MergeOrderState state)
        {
            switch (kind)
            {
                case BlindBoxRewardKind.Energy:
                    return new BlindBoxReward(BlindBoxRewardKind.Energy, MergeElement.None, 0, 0, BoxEnergyGain);

                case BlindBoxRewardKind.NeededHigh:
                {
                    // 缺口里「最高等级」那一项的(类型,等级)；无缺口 → 降级 PatternHigh（封顶 Lv MaxLevel 通用）。
                    if (TryPickNeededHigh(state, out var type, out var level))
                        return new BlindBoxReward(BlindBoxRewardKind.NeededHigh, type, level, 1, 0);
                    // 降级保底：当作 PatternHigh 走通用封顶等级
                    var fallbackType = PickPatternType(state);
                    return new BlindBoxReward(BlindBoxRewardKind.PatternHigh, fallbackType, MergeOrderConfig.MaxLevel, 1, 0);
                }

                default: // PatternLow / PatternMid / PatternHigh
                {
                    int lvl = PatternLevelOf[(int)kind];
                    var type = PickPatternType(state);
                    return new BlindBoxReward(kind, type, lvl, 1, 0);
                }
            }
        }

        /// <summary>
        /// 取当前订单缺口中「最高等级」的那一项 (类型,等级)。缺口 = 激活订单里尚未满足的项
        /// （含特殊槽订单）。无任何缺口返回 false（调用方据此降级）。
        /// </summary>
        private static bool TryPickNeededHigh(MergeOrderState state, out MergeElement type, out int level)
        {
            type = MergeElement.None;
            level = 0;
            if (state == null) return false;

            int bestLevel = 0;
            var bestType = MergeElement.None;

            // 普通激活订单：取尚未满足（库存不够）的订单中等级最高者。
            var orders = state.ActiveOrders;
            if (orders != null)
            {
                for (int i = 0; i < orders.Length; i++)
                {
                    var o = orders[i];
                    if (!o.IsValid) continue;
                    if (state.InventoryCount(o.Type, o.Level) >= o.Count) continue; // 已可满足 → 非缺口
                    if (o.Level > bestLevel) { bestLevel = o.Level; bestType = o.Type; }
                }
            }

            // 特殊槽订单同样计入缺口（设计 12 §3.1「当前订单缺口」含特殊单）。
            if (state.SpecialTrack != null && state.SpecialTrack.HasOccupied)
            {
                var so = state.SpecialTrack.Occupied.Req;
                if (so.IsValid && state.InventoryCount(so.Type, so.Level) < so.Count && so.Level > bestLevel)
                {
                    bestLevel = so.Level;
                    bestType = so.Type;
                }
            }

            if (bestLevel >= 1 && bestType != MergeElement.None)
            {
                type = bestType;
                level = bestLevel;
                return true;
            }
            return false;
        }

        /// <summary>图案归属类型：取当前所需类型之一（NeededTypes 轮转取首项），空则回退 Star。</summary>
        private static MergeElement PickPatternType(MergeOrderState state)
        {
            var needed = state != null ? state.NeededTypes() : null;
            if (needed != null && needed.Count > 0) return needed[0];
            return MergeElement.Star;
        }
    }
}
