using System.Collections.Generic;
using GameLogic.BlockBlast.Algorithms;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 全局可调配置（v1 静态默认，无运行时面板）。
    /// 对应源 GameConfig.ts，但砍掉 ConfigPanel/监听器/persist。
    /// </summary>
    public static class GameConfigBB
    {
        /// <summary>动态调度激活分数门槛。</summary>
        public const int ActivationScore = 1000;

        /// <summary>
        /// 是否显示「不可放置」的红色 ghost 预览。
        /// 开启：拖拽全程显示落点 footprint——可放置绿色、不可放置红色，使玩家所见即所落、失败可见。
        /// </summary>
        public static bool ShowInvalidGhost = true;

        /// <summary>每种算法的调权因子（首次/换向 vs 同向连续）。</summary>
        public static readonly IReadOnlyDictionary<AlgorithmKind, WeightFactor> FactorList = new Dictionary<AlgorithmKind, WeightFactor>
        {
            { AlgorithmKind.Fill,              new WeightFactor( 40,  20) },
            { AlgorithmKind.RandomNoDie,       new WeightFactor( 15,   5) },
            { AlgorithmKind.Add3,              new WeightFactor(-25, -10) },
            { AlgorithmKind.EasyDiff,          new WeightFactor( 30,  12) },
            { AlgorithmKind.Diff,              new WeightFactor(-40, -15) },
            { AlgorithmKind.StraightDeathDiff, new WeightFactor(-80, -30) },
            { AlgorithmKind.ClearAll,          new WeightFactor( 60,  30) },
            { AlgorithmKind.AllCombination,    new WeightFactor( 35,  15) },
        };

        /// <summary>每种算法的采样次数（算法内部已硬编码默认；保留此入口给 P2 接 Luban）。</summary>
        public static readonly IReadOnlyDictionary<AlgorithmKind, int> AlgorithmSamples = new Dictionary<AlgorithmKind, int>
        {
            { AlgorithmKind.Fill,              80 },
            { AlgorithmKind.RandomNoDie,       50 },
            { AlgorithmKind.Add3,              60 },
            { AlgorithmKind.EasyDiff,          80 },
            { AlgorithmKind.Diff,              160 },
            { AlgorithmKind.StraightDeathDiff, 320 },
            { AlgorithmKind.ClearAll,          120 },
            { AlgorithmKind.AllCombination,    150 },
        };

        /// <summary>新玩家首发 3 个形状 ID。</summary>
        public static readonly int[] FirstHand = { 9, 39, 24 };

        public static WeightFactor FactorFor(AlgorithmKind algo)
            => FactorList.TryGetValue(algo, out var f) ? f : new WeightFactor(0, 0);

        public static int SamplesFor(AlgorithmKind algo)
            => AlgorithmSamples.TryGetValue(algo, out var n) ? n : 50;
    }
}
