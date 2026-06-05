using System;
using System.Collections.Generic;

namespace GameLogic.BlockBlast.Algorithms
{
    /// <summary>
    /// 8 种动态权重算法。对应原游戏 com.block.juggle main_bundle.js DynamicWeightDiff。
    /// </summary>
    public enum AlgorithmKind
    {
        Fill = 0,              // 填空消除 (tiankongxiaochu2)
        RandomNoDie = 1,       // 随机无死亡 (suijiwusiwang)
        Add3 = 2,              // 熵增算法 (shangzeng3)
        EasyDiff = 3,          // 简单难题
        Diff = 4,              // 困难难题
        StraightDeathDiff = 5, // 直觉/死亡难题
        ClearAll = 6,          // 清盘 Plus
        AllCombination = 7,    // 全组合填空消除
    }

    public static class AlgorithmNames
    {
        /// <summary>算法 → 名称（与源项目 ALGORITHM_NAME 对齐，供 GameConfig 查 factor）。</summary>
        public static readonly IReadOnlyDictionary<AlgorithmKind, string> Map = new Dictionary<AlgorithmKind, string>
        {
            { AlgorithmKind.Fill,              "FILL" },
            { AlgorithmKind.RandomNoDie,       "RANDOM_NO_DIE" },
            { AlgorithmKind.Add3,              "ADD3" },
            { AlgorithmKind.EasyDiff,          "EASY_DIFF" },
            { AlgorithmKind.Diff,              "DIFF" },
            { AlgorithmKind.StraightDeathDiff, "STRAIGHT_DEATH_DIFF" },
            { AlgorithmKind.ClearAll,          "CLEAR_ALL" },
            { AlgorithmKind.AllCombination,    "ALL_COMBINATION" },
        };

        public static string Of(AlgorithmKind k) => Map.TryGetValue(k, out var n) ? n : k.ToString();
    }

    /// <summary>weightcfg 中 odds 字段顺序（必须与 AlgorithmKind 0..7 一一对应）。</summary>
    public static class OddsFields
    {
        public static readonly string[] Order = new[]
        {
            "FillBlankOdds",   // 0 Fill
            "RandomOdds",      // 1 RandomNoDie
            "EntropyOdds",     // 2 Add3
            "EasyOdds",        // 3 EasyDiff
            "HardOdds",        // 4 Diff
            "IntuitionOdds",   // 5 StraightDeathDiff
            "Clearboard",      // 6 ClearAll
            "Allunite",        // 7 AllCombination
        };
    }

    /// <summary>算法激活的分数下限（对应原游戏 isCondition: score &gt; 1000）。</summary>
    public static class AlgorithmConstants
    {
        public const int DynamicActivationScore = 1000;
    }
}
