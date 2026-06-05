using System;
using System.Collections.Generic;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 数据层共享随机源。测试可通过 SetSeed/SetRng 注入确定性序列；
    /// 业务逻辑默认使用基于时间种子的 System.Random。
    /// 显式避免 UnityEngine.Random 以保持数据层无 Unity 依赖。
    /// </summary>
    public static class RandomSource
    {
        private static Random _rng = new Random();

        public static void SetSeed(int seed) => _rng = new Random(seed);
        public static void SetRng(Random rng) => _rng = rng ?? throw new ArgumentNullException(nameof(rng));

        /// <summary>返回 [0,1) 之间的浮点数。</summary>
        public static double NextDouble() => _rng.NextDouble();

        /// <summary>返回 [minInclusive, maxExclusive) 之间的整数。</summary>
        public static int Range(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);

        /// <summary>返回 [0, count) 之间的整数索引。</summary>
        public static int Index(int count) => _rng.Next(0, count);

        /// <summary>从列表中随机抽一个元素。</summary>
        public static T Pick<T>(IReadOnlyList<T> list) => list[_rng.Next(0, list.Count)];

        /// <summary>原地 Fisher-Yates 洗牌。</summary>
        public static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
