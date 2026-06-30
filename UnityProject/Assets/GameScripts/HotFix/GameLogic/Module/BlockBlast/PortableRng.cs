using System;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 随机源抽象。生成核心仅通过此接口取随机,与具体实现解耦,使同一份生成逻辑
    /// 能在不同运行时(Unity Mono/IL2CPP 与 .NET)上跑出逐位一致的序列。
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>返回 [0,1) 的浮点数。</summary>
        double NextDouble();

        /// <summary>返回 [minInclusive, maxExclusive) 的整数。语义与 System.Random.Next(min,max) 等价。</summary>
        int Next(int minInclusive, int maxExclusive);
    }

    /// <summary>
    /// xorshift128+ 整数运算确定性 PRNG。状态推进与取值全程整数位运算,
    /// 不依赖各运行时的浮点/库实现,故同 seed 在 Unity Mono/IL2CPP 与 .NET 上序列一致。
    /// 这是跨运行时一致性的必备仪器:消解 System.Random 跨运行时实现差异(隐患 c3),
    /// 否则上层算法的枚举序/double 分支差异(c1/c2)会被 c3 噪声淹没,无法单独测量。
    /// </summary>
    public sealed class XorShift128PlusRng : IRandomSource
    {
        private ulong _s0;
        private ulong _s1;

        public XorShift128PlusRng(long seed)
        {
            // SplitMix64 把单一 seed 扩散成两个非零状态字,避免低质量初值导致前几步退化。
            ulong z = unchecked((ulong)seed + 0x9E3779B97F4A7C15UL);
            _s0 = SplitMix64(ref z);
            _s1 = SplitMix64(ref z);
            // xorshift128+ 要求状态非全零。
            if (_s0 == 0 && _s1 == 0) _s1 = 0x9E3779B97F4A7C15UL;
        }

        private static ulong SplitMix64(ref ulong z)
        {
            z = unchecked(z + 0x9E3779B97F4A7C15UL);
            ulong r = z;
            r = unchecked((r ^ (r >> 30)) * 0xBF58476D1CE4E5B9UL);
            r = unchecked((r ^ (r >> 27)) * 0x94D049BB133111EBUL);
            return r ^ (r >> 31);
        }

        /// <summary>推进状态,返回一个 64 位无符号随机字。</summary>
        private ulong NextULong()
        {
            ulong x = _s0;
            ulong y = _s1;
            _s0 = y;
            x ^= x << 23;
            x ^= x >> 17;
            x ^= y ^ (y >> 26);
            _s1 = x;
            return unchecked(x + y);
        }

        /// <summary>
        /// 取 53 位尾数构造 [0,1) 双精度。用整数右移取高 53 位再乘 2^-53,
        /// 全程无运行时相关的浮点库调用,结果逐位确定。
        /// </summary>
        public double NextDouble()
        {
            // 高 53 位 → [0, 2^53),乘 1/2^53 → [0,1)。
            ulong bits = NextULong() >> 11;
            return bits * (1.0 / 9007199254740992.0); // 2^53
        }

        /// <summary>
        /// [minInclusive, maxExclusive) 整数。用无偏拒绝采样取模,避免取模偏置;
        /// 等价语义对齐 System.Random.Next(min,max)(min&gt;=max 时返回 min)。
        /// </summary>
        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            ulong range = (ulong)((long)maxExclusive - minInclusive);
            // 无偏:丢弃落在最后一段不完整桶里的值。
            ulong limit = ulong.MaxValue - (ulong.MaxValue % range);
            ulong r;
            do { r = NextULong(); } while (r >= limit);
            return (int)((long)minInclusive + (long)(r % range));
        }
    }
}
