using System.Collections.Generic;
using GameLogic.Rank;

namespace GameLogic.Config
{
    /// <summary>
    /// 排行榜底层配置管理器（设计 22 §3.2）。
    /// 桥接 Luban 生成的 <c>GameConfig.rank.TbRank</c>（行类 <c>GameConfig.Rank</c>）→ POCO（<see cref="RankDef"/> / <see cref="RankRewardTier"/>），
    /// 让业务侧不直接依赖 Luban 类型。仿 <see cref="MailConfigMgr"/> / <see cref="ItemConfigMgr"/>。
    /// </summary>
    /// <remarks>
    /// 加法式：注册表只持有元数据，不读写 <c>MergeOrderState</c>。
    /// 加载分两路：运行期 <see cref="EnsureLoaded"/> 走 <c>ConfigSystem</c>（YooAsset，需 Unity 运行时）；
    /// EditMode 单测经 <see cref="InitForTest"/> 注入，绕 ConfigSystem（纯逻辑可测）。
    /// <b>按 id 聚合多行</b>：同 id 多行 = 一个榜的多个名次档；榜级字段取该 id 首遇行，各行的名次档进 Tiers（按 RankMin 升序）
    /// （同 16 礼包子项 / 20 兑换码奖励子表的「主+子聚合」做法）。
    /// </remarks>
    public static class RankConfigMgr
    {
        private static Dictionary<int, RankDef> _ranks;

        // ── 运行期加载（经 ConfigSystem / YooAsset，按 id 聚合）──────

        /// <summary>
        /// 运行期建缓存：经 <c>ConfigSystem</c>（YooAsset，需 Unity 运行时）。首次访问时灌入，已灌（含 InitForTest）则直接返回。
        /// 遍历 <c>TbRank.DataList</c>（行序 = RowId 升序），按 <c>Id</c> 聚合：首遇行定榜级字段，每行追加一个名次档，最后各榜 Tiers 按 RankMin 升序。
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_ranks != null) return;
            var table = ConfigSystem.Instance.Tables.TbRank;
            _ranks = Aggregate(table.DataList);
        }

        /// <summary>
        /// 按 id 聚合 Luban 行 → RankDef 字典（运行期 / 测试共用）。
        /// </summary>
        private static Dictionary<int, RankDef> Aggregate(IEnumerable<GameConfig.Rank> rows)
        {
            var map = new Dictionary<int, RankDef>();
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    if (!map.TryGetValue(row.Id, out var def))
                    {
                        // 首遇该 id：建榜，榜级字段取本行
                        def = new RankDef
                        {
                            Id = row.Id,
                            NameTextId = row.Name,
                            Group = row.RankGroup,
                            Method = row.RankMethod,
                            Condition = row.RankCondition,
                            PraiseRewardPoolId = row.RewardPraise,
                            ValidType = (RankValidType)row.ValidType,
                            ValidVal = row.ValidVal,
                            MailDefId = row.Mail,
                            CountMax = row.RankCountMax,
                            ShowMax = row.ShowCountMax,
                            Tiers = new List<RankRewardTier>(),
                        };
                        map[row.Id] = def;
                    }
                    // 每行追加一个名次档
                    def.Tiers.Add(new RankRewardTier
                    {
                        RankMin = row.RankMin,
                        RankMax = row.RankMax,
                        RewardPoolId = row.Reward,
                        ShowRewardPoolId = row.RankShowReward,
                        DailyRewardPoolId = row.RewardDaily,
                    });
                }
            }
            // 各榜名次档按 RankMin 升序（与行序无关，保 TierForRank 稳定）
            foreach (var def in map.Values)
                def.Tiers.Sort((a, b) => a.RankMin.CompareTo(b.RankMin));
            return map;
        }

        // ── 查询 ───────────────────────────────────────────────

        /// <summary>按榜 id 查；查不到返 null（不抛）。</summary>
        public static RankDef GetRank(int id)
        {
            EnsureLoaded();
            return _ranks.TryGetValue(id, out var d) ? d : null;
        }

        /// <summary>全部榜（供 UI 列分页 / 登录遍历检查结算 / 红点）。</summary>
        public static IReadOnlyCollection<RankDef> All()
        {
            EnsureLoaded();
            return _ranks.Values;
        }

        // ── 测试注入 / 隔离 ─────────────────────────────────────

        /// <summary>
        /// 测试注入口：绕开 ConfigSystem，直接灌已聚合好的 RankDef（EditMode / 纯 C# 单测用）。
        /// 灌入后 <see cref="EnsureLoaded"/> 不再触发 ConfigSystem。
        /// </summary>
        public static void InitForTest(IEnumerable<RankDef> ranks)
        {
            _ranks = new Dictionary<int, RankDef>();
            if (ranks != null)
                foreach (var d in ranks) _ranks[d.Id] = d;
        }

        /// <summary>清空缓存（测试隔离用，下次查询重新走 EnsureLoaded）。</summary>
        public static void ResetForTest()
        {
            _ranks = null;
        }
    }
}
