using System.Collections.Generic;
using GameLogic.Redeem;

namespace GameLogic.Config
{
    /// <summary>
    /// 通用兑换码底层配置管理器（设计 20 §3.2）。
    /// 桥接 Luban 生成的 <c>GameConfig.redeem.TbRedeemCode / TbRedeemReward</c> → POCO
    /// （<see cref="RedeemCodeDef"/> / <see cref="RedeemReward"/>），让业务侧不直接依赖 Luban 类型；
    /// 按规整后的码查定义，奖励子表按 code 聚合。仿 <see cref="ItemConfigMgr"/>。
    /// </summary>
    /// <remarks>
    /// 加法式：注册表只持有元数据，不读写 <c>MergeOrderState</c>；发放落点由 <c>RedeemService</c> 接道具系统既有逻辑。
    /// 加载分两路：运行期 <see cref="EnsureLoaded"/> 走 <c>ConfigSystem</c>（YooAsset，需 Unity 运行时）；
    /// EditMode 单测经 <see cref="InitForTest"/> 注入，绕 ConfigSystem（纯逻辑可测）。
    /// 字典 key 用<b>规整后（大写）</b>的码，与 <see cref="RedeemService.Normalize"/> 比对口径一致。
    /// </remarks>
    public static class RedeemConfigMgr
    {
        private static Dictionary<string, RedeemCodeDef> _codes; // 规整码 → 定义

        // ── 单行桥接：Luban 行 → POCO ─────────────────────────────

        /// <summary>码主表行 → POCO（不含奖励，奖励由子表聚合）。Code 规整为大写。</summary>
        public static RedeemCodeDef ToDef(GameConfig.RedeemCode row)
        {
            return new RedeemCodeDef
            {
                Code = RedeemService.Normalize(row.Code),
                Name = row.Name,
                OncePerPlayer = row.OncePerPlayer,
                ExpireTime = row.ExpireTime,
                Rewards = new List<RedeemReward>(),
            };
        }

        /// <summary>奖励子表行 → POCO 奖励项。</summary>
        public static RedeemReward ToReward(GameConfig.RedeemReward row)
            => new RedeemReward { ItemId = row.ItemId, Num = row.Num };

        // ── 运行期加载（经 ConfigSystem / YooAsset）────────────────

        /// <summary>
        /// 运行期建缓存：经 <c>ConfigSystem</c>（YooAsset，需 Unity 运行时）。首次访问时灌入。
        /// 已灌（含 InitForTest 注入）则直接返回。先读码主表建定义，再遍历奖励子表按 code 聚合。
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_codes != null) return;
            var tables = ConfigSystem.Instance.Tables;

            _codes = new Dictionary<string, RedeemCodeDef>(tables.TbRedeemCode.DataList.Count);
            foreach (var row in tables.TbRedeemCode.DataList)
            {
                var def = ToDef(row);
                _codes[def.Code] = def;
            }

            // 奖励子表按规整后的 code 聚合进对应定义
            foreach (var row in tables.TbRedeemReward.DataList)
            {
                var code = RedeemService.Normalize(row.Code);
                if (_codes.TryGetValue(code, out var def))
                {
                    def.Rewards.Add(ToReward(row));
                }
            }
        }

        // ── 查询 ───────────────────────────────────────────────

        /// <summary>按规整后的码查定义；查不到返 null（不抛）。</summary>
        public static RedeemCodeDef Get(string normalizedCode)
        {
            EnsureLoaded();
            return _codes.TryGetValue(normalizedCode, out var d) ? d : null;
        }

        // ── 测试注入 / 隔离 ─────────────────────────────────────

        /// <summary>
        /// 测试注入口：绕开 ConfigSystem，直接灌 POCO 列表（EditMode / 纯 C# 单测用）。
        /// 灌入后 <see cref="EnsureLoaded"/> 不再触发 ConfigSystem。key 用规整码（对每个 def 的 Code 再规整一次保口径）。
        /// </summary>
        public static void InitForTest(IEnumerable<RedeemCodeDef> codes)
        {
            _codes = new Dictionary<string, RedeemCodeDef>();
            if (codes != null)
            {
                foreach (var d in codes)
                {
                    var key = RedeemService.Normalize(d.Code);
                    d.Code = key; // 统一存规整码，与运行期口径一致
                    _codes[key] = d;
                }
            }
        }

        /// <summary>清空缓存（测试隔离用，下次查询会重新走 EnsureLoaded）。</summary>
        public static void ResetForTest()
        {
            _codes = null;
        }
    }
}
