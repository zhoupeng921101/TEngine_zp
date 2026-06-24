using System.Collections.Generic;
using System.Globalization;

namespace GameLogic.Config
{
    /// <summary>
    /// 全局零散参数配置管理器。
    /// 桥接 Luban 生成的 <c>GameConfig.global.TbGlobal</c>（键值型：id:int → value:string），
    /// 业务侧按命名常量取值，并按需解析为 int/float/bool，不直接依赖 Luban 类型。仿 <see cref="NumericConfigMgr"/>。
    /// </summary>
    /// <remarks>
    /// value 统一以字符串存储，解析失败或缺键时返回调用方给的默认值（不抛）。
    /// 新增参数：在 global.xlsx 加一行并导表，再在此处补一个 id 常量与（可选）便捷属性。
    /// </remarks>
    public static class GlobalConfigMgr
    {
        // ── 参数 id 常量（与 global.xlsx 主键约定一致）──
        public const int OrderCount = 1;          // 订单数量(同时可接订单上限)
        public const int OrderRefreshSeconds = 2; // 订单刷新时间(秒)
        public const int EnergyRecoverSeconds = 3;// 体力恢复时间(每点间隔秒)
        public const int EnergyRecoverCap = 4;    // 体力恢复上限

        private static Dictionary<int, string> _cache; // 懒加载缓存：id → value(原始字符串)

        /// <summary>
        /// 运行期建缓存：经 <c>ConfigSystem</c>（YooAsset，需 Unity 运行时）。首次访问时灌入。
        /// 已灌（含 InitForTest 注入）则直接返回。
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_cache != null) return;
            var table = ConfigSystem.Instance.Tables.TbGlobal;
            _cache = new Dictionary<int, string>(table.DataList.Count);
            foreach (var row in table.DataList)
            {
                _cache[row.Id] = row.Value;
            }
        }

        /// <summary>取原始字符串值；缺键返 <paramref name="defaultValue"/>。</summary>
        public static string GetString(int id, string defaultValue = "")
        {
            EnsureLoaded();
            return _cache.TryGetValue(id, out var v) ? v : defaultValue;
        }

        /// <summary>取 int 值；缺键或解析失败返 <paramref name="defaultValue"/>。</summary>
        public static int GetInt(int id, int defaultValue = 0)
        {
            EnsureLoaded();
            return _cache.TryGetValue(id, out var v)
                   && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)
                ? r : defaultValue;
        }

        /// <summary>取 float 值；缺键或解析失败返 <paramref name="defaultValue"/>。</summary>
        public static float GetFloat(int id, float defaultValue = 0f)
        {
            EnsureLoaded();
            return _cache.TryGetValue(id, out var v)
                   && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var r)
                ? r : defaultValue;
        }

        /// <summary>取 bool 值；接受 "true"/"false"（忽略大小写）与 "1"/"0"，否则返 <paramref name="defaultValue"/>。</summary>
        public static bool GetBool(int id, bool defaultValue = false)
        {
            EnsureLoaded();
            if (!_cache.TryGetValue(id, out var v)) return defaultValue;
            if (bool.TryParse(v, out var b)) return b;
            if (v == "1") return true;
            if (v == "0") return false;
            return defaultValue;
        }

        // ── 便捷属性（带兜底默认值，与 global.xlsx 初始值一致）──
        public static int OrderCountValue => GetInt(OrderCount, 3);
        public static int OrderRefreshSecondsValue => GetInt(OrderRefreshSeconds, 300);
        public static int EnergyRecoverSecondsValue => GetInt(EnergyRecoverSeconds, 360);
        public static int EnergyRecoverCapValue => GetInt(EnergyRecoverCap, 30);

        /// <summary>
        /// 测试注入口：绕开 ConfigSystem，直接灌 id→value 映射（EditMode / 纯 C# 单测用）。
        /// 灌入后 <see cref="EnsureLoaded"/> 不再触发 ConfigSystem。
        /// </summary>
        public static void InitForTest(IDictionary<int, string> values)
        {
            _cache = new Dictionary<int, string>();
            if (values != null)
                foreach (var kv in values) _cache[kv.Key] = kv.Value;
        }

        /// <summary>清空缓存（测试隔离用，下次取值会重新走 EnsureLoaded）。</summary>
        public static void ResetForTest()
        {
            _cache = null;
        }
    }
}
