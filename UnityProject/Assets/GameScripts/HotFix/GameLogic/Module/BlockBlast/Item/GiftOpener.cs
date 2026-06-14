using System;
using System.Collections.Generic;
using GameLogic.Config;

namespace GameLogic.BlockBlast.Item
{
    /// <summary>
    /// 礼包开启（设计 16 §3.6）：吃注册表 + 注入随机数的纯逻辑。
    /// 随机礼包按 rate 权重抽；自选礼包返回候选列表交 UI 选。
    /// </summary>
    /// <remarks>
    /// 随机数从外部注入（<see cref="System.Random"/>），使抽样可用固定种子复现——
    /// 「权重抽样确定性」验收（G1）的地基。本类不写 MergeOrderState，可纯逻辑单测。
    /// </remarks>
    public static class GiftOpener
    {
        /// <summary>
        /// 按 rate 权重抽 1 项（确定可测）。
        /// 边界：空池返 null；全 0 权重退化取首项；负权重当 0（设计 16 §3.6 边界表）。
        /// </summary>
        public static GiftEntry RollRandom(IReadOnlyList<GiftEntry> pool, System.Random rng)
        {
            if (pool == null || pool.Count == 0) return null;

            int total = 0;
            for (int i = 0; i < pool.Count; i++) total += Math.Max(0, pool[i].Rate); // 负权重当 0
            if (total <= 0) return pool[0];                                           // 全 0 退化取首项（不抛）

            int r = rng.Next(total);                                                  // [0, total)
            int acc = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                acc += Math.Max(0, pool[i].Rate);
                if (r < acc) return pool[i];
            }
            return pool[pool.Count - 1];                                              // 边界兜底
        }

        /// <summary>
        /// 开 N 次随机礼包（times = 抽几次），每次独立抽（放回）。
        /// times≤0 当 1 次（设计 16 §3.6）。池为空返回空列表。
        /// </summary>
        public static List<GiftEntry> OpenRandom(int index, int times, System.Random rng)
        {
            var pool = ItemConfigMgr.GetGiftRandom(index);
            var result = new List<GiftEntry>();
            int n = Math.Max(1, times);
            for (int i = 0; i < n; i++)
            {
                var e = RollRandom(pool, rng);
                if (e != null) result.Add(e);
            }
            return result;
        }

        /// <summary>自选礼包：返回该 index 的全部候选（不抽，UI 选）。查无返空集合。</summary>
        public static IReadOnlyList<GiftEntry> ListSelectable(int index)
            => ItemConfigMgr.GetGiftSelect(index);
    }
}
