using System;
using System.Collections.Generic;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 数据层共享随机源。测试可通过 SetSeed/SetRng 注入确定性序列；
    /// 业务逻辑默认使用基于时间种子的 System.Random。
    /// 显式避免 UnityEngine.Random 以保持数据层无 Unity 依赖。
    ///
    /// 取随机统一经 <see cref="IRandomSource"/> 抽象,使生成逻辑能切换到跨运行时确定的
    /// 可移植 PRNG(<see cref="XorShift128PlusRng"/>),用于服务端权威发牌的跨端一致性验证。
    /// </summary>
    public static class RandomSource
    {
        /// <summary>把 System.Random 适配成 IRandomSource,保留既有默认路径不变。</summary>
        private sealed class SystemRandomSource : IRandomSource
        {
            private readonly Random _rng;
            public SystemRandomSource(Random rng) => _rng = rng;
            public double NextDouble() => _rng.NextDouble();
            public int Next(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);
        }

        private static IRandomSource _source = new SystemRandomSource(new Random());

        public static void SetSeed(int seed) => _source = new SystemRandomSource(new Random(seed));

        /// <summary>注入 System.Random(保留原有测试路径)。</summary>
        public static void SetRng(Random rng)
            => _source = new SystemRandomSource(rng ?? throw new ArgumentNullException(nameof(rng)));

        /// <summary>注入任意随机源抽象(如跨运行时确定的可移植 PRNG)。</summary>
        public static void SetRng(IRandomSource source)
            => _source = source ?? throw new ArgumentNullException(nameof(source));

        /// <summary>返回 [0,1) 之间的浮点数。</summary>
        public static double NextDouble() => _source.NextDouble();

        /// <summary>返回 [minInclusive, maxExclusive) 之间的整数。</summary>
        public static int Range(int minInclusive, int maxExclusive) => _source.Next(minInclusive, maxExclusive);

        /// <summary>返回 [0, count) 之间的整数索引。</summary>
        public static int Index(int count) => _source.Next(0, count);

        /// <summary>从列表中随机抽一个元素。</summary>
        public static T Pick<T>(IReadOnlyList<T> list) => list[_source.Next(0, list.Count)];

        /// <summary>原地 Fisher-Yates 洗牌。</summary>
        public static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _source.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    /// <summary>
    /// <see cref="IRandomSource"/> 上的取值便捷方法。生成核心把随机源作为参数显式贯穿(逐局实例,
    /// 无进程级全局可变状态),这些方法让被贯穿的实例复用与静态 <see cref="RandomSource"/> 逐位等价的
    /// Index/Pick/Shuffle 算法,确保去单例化后发牌序列零回归。
    /// </summary>
    public static class RandomSourceExtensions
    {
        /// <summary>返回 [0, count) 之间的整数索引。</summary>
        public static int Index(this IRandomSource source, int count) => source.Next(0, count);

        /// <summary>从列表中随机抽一个元素。</summary>
        public static T Pick<T>(this IRandomSource source, IReadOnlyList<T> list)
            => list[source.Next(0, list.Count)];

        /// <summary>原地 Fisher-Yates 洗牌。</summary>
        public static void Shuffle<T>(this IRandomSource source, IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = source.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
