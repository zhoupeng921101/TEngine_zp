using System;
using System.Collections.Generic;

namespace GameLogic.Rank
{
    /// <summary>
    /// 排名数据源接缝（设计 22 §3.6，标题「服务器接缝」的实义）。
    /// 给一个榜 id，返回「榜上有哪些参赛记录」（原始未排序），由 <see cref="RankService"/> 排序产出名次。
    /// 离线注 <see cref="LocalRankSource"/>（本机 + 陪榜）；未来上后端只换 <see cref="RemoteRankSource"/> 实现，服务层零改动。
    /// </summary>
    public interface IRankSource
    {
        /// <summary>取该榜的参赛记录（未排序）。含本机一条（IsSelf=true）+ 若干陪榜。</summary>
        IReadOnlyList<RankEntry> Fetch(int rankId);
    }

    /// <summary>
    /// 离线本地源（可跑可测，设计 22 §3.6）：本机最佳成绩（注入 / 来自持久化）+ 配置陪榜成绩 → 一组参赛记录。
    /// 「陪榜」= 配置 / 注入的基准成绩（NPC 名 textId 占位 + 分数），使单机也有一份可排序的榜。
    /// 不引入随机生成 NPC（表现层 / 运营内容，O2）；不连网。
    /// </summary>
    public sealed class LocalRankSource : IRankSource
    {
        private readonly Func<int, (long score, long ticks, int nameTextId)> _selfProvider; // 本机成绩
        private readonly Func<int, IReadOnlyList<RankEntry>> _filler;                        // 陪榜（基准分）

        /// <summary>
        /// 构造离线本地源。
        /// </summary>
        /// <param name="selfProvider">按榜 id 取本机成绩（分数 / 达到时间 / 展示名 textId）。</param>
        /// <param name="filler">按榜 id 取陪榜记录（基准分；可为 null = 无陪榜）。</param>
        public LocalRankSource(
            Func<int, (long score, long ticks, int nameTextId)> selfProvider,
            Func<int, IReadOnlyList<RankEntry>> filler = null)
        {
            _selfProvider = selfProvider ?? throw new ArgumentNullException(nameof(selfProvider));
            _filler = filler;
        }

        public IReadOnlyList<RankEntry> Fetch(int rankId)
        {
            var list = new List<RankEntry>();
            var self = _selfProvider(rankId);
            list.Add(new RankEntry
            {
                IsSelf = true,
                Score = self.score,
                AchievedTicks = self.ticks,
                PlayerNameTextId = self.nameTextId,
            });
            if (_filler != null)
            {
                var fill = _filler(rankId);
                if (fill != null) list.AddRange(fill);
            }
            return list;
        }
    }

    /// <summary>
    /// 远程 stub（设计 22 §3.6 / O1）：无服务器，返空、不连网、不抛。
    /// TODO: 未来上后端时实现一次（HTTP 拉全服榜 → 转 <see cref="RankEntry"/>），<see cref="RankService"/> 与排序层零改动。
    /// </summary>
    public sealed class RemoteRankSource : IRankSource
    {
        public IReadOnlyList<RankEntry> Fetch(int rankId)
            => Array.Empty<RankEntry>(); // 不抛、不连网
    }
}
