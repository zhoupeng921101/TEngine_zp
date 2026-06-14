using System.Collections.Generic;
using GameLogic.BlockBlast.Numeric;

namespace GameLogic.Config
{
    /// <summary>
    /// 数值底层货币表配置管理器。
    /// 桥接 Luban 生成的 <c>GameConfig.num.TbNum</c> → POCO <see cref="NumericEntry"/>，
    /// 让业务侧不直接依赖 Luban 类型；按 num_id 查元数据。仿 <see cref="WeightCfgConfigMgr"/>。
    /// </summary>
    /// <remarks>
    /// 加法式：注册表只持有元数据，不读写 <c>MergeOrderState</c>；数量值仍由各现有字段持有。
    /// num_id ↔ 现有字段是「约定」（下方常量），非代码绑定（设计 15 §2.2 / §3.2）。
    /// </remarks>
    public static class NumericConfigMgr
    {
        // ── num_id ↔ 现有字段的约定常量（设计 15 §3.2 映射约定）──
        public const int Exp = 1;
        public const int Piety = 2;
        public const int Diamond = 3;
        public const int Energy = 4;

        private static Dictionary<int, NumericEntry> _cache; // 懒加载缓存

        /// <summary>单行转换：Luban Num → NumericEntry。</summary>
        public static NumericEntry ToEntry(GameConfig.Num row)
        {
            return new NumericEntry
            {
                NumId = row.Id,
                DescTextId = row.Desc,
                NameTextId = row.Name,
                IconName = row.Icon,
                NumType = (int)row.NumType,
                Quality = row.Quality,
            };
        }

        /// <summary>
        /// 运行期建缓存：经 <c>ConfigSystem</c>（YooAsset，需 Unity 运行时）。首次访问时灌入。
        /// 已灌（含 InitForTest 注入）则直接返回。
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_cache != null) return;
            var table = ConfigSystem.Instance.Tables.TbNum;
            _cache = new Dictionary<int, NumericEntry>(table.DataList.Count);
            foreach (var row in table.DataList)
            {
                _cache[row.Id] = ToEntry(row);
            }
        }

        /// <summary>按 num_id 查；查不到返 null（不抛）。</summary>
        public static NumericEntry Get(int numId)
        {
            EnsureLoaded();
            return _cache.TryGetValue(numId, out var e) ? e : null;
        }

        /// <summary>按 num_type 查；无匹配返空集合（不抛）。</summary>
        public static IReadOnlyList<NumericEntry> GetByType(int numType)
        {
            EnsureLoaded();
            var result = new List<NumericEntry>();
            foreach (var e in _cache.Values)
            {
                if (e.NumType == numType) result.Add(e);
            }
            return result;
        }

        /// <summary>
        /// 测试注入口：绕开 ConfigSystem，直接灌 entry 列表（EditMode / 纯 C# 单测用）。
        /// 灌入后 <see cref="EnsureLoaded"/> 不再触发 ConfigSystem。
        /// </summary>
        public static void InitForTest(IEnumerable<NumericEntry> entries)
        {
            _cache = new Dictionary<int, NumericEntry>();
            foreach (var e in entries)
            {
                _cache[e.NumId] = e;
            }
        }

        /// <summary>清空缓存（测试隔离用，下次 Get 会重新走 EnsureLoaded）。</summary>
        public static void ResetForTest()
        {
            _cache = null;
        }
    }
}
