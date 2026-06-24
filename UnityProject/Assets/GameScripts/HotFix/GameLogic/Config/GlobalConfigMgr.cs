using System.Collections.Generic;
using System.Globalization;
using TEngine;

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
        public const int ClearToolEnergyCost = 5; // 消除道具(清行列)消耗体力

        private static Dictionary<int, string> _cache; // 懒加载缓存：id → value(原始字符串)

        /// <summary>
        /// 运行期建缓存：经 <c>ConfigSystem</c>（YooAsset，需 Unity 运行时）。首次访问时灌入。
        /// 已灌（含 InitForTest 注入）则直接返回。
        /// </summary>
        /// <remarks>
        /// ConfigSystem 不可用（EditMode 单测无 ResourceModule / 资源未就绪）时灌空缓存而非抛：
        /// 取值方一律走调用方给的默认值（缺键即默认），使 MergeOrderConfig 等符号化引用在纯逻辑单测里不崩。
        /// 需要确定性表值的单测改用 <see cref="InitForTest"/> 显式注入（绕开本路径）。
        /// </remarks>
        public static void EnsureLoaded()
        {
            if (_cache != null) return;
            try
            {
                var table = ConfigSystem.Instance.Tables.TbGlobal;
                _cache = new Dictionary<int, string>(table.DataList.Count);
                foreach (var row in table.DataList)
                {
                    _cache[row.Id] = row.Value;
                }
            }
            catch (System.Exception e)
            {
                // 配置不可用：灌空缓存，取值方回退默认（不抛，不崩单测/早期调用）。
                // EditMode 单测/早期调用无 ConfigSystem 时也会进此分支（属预期回退），故记 Warning 仅供生产期排查，不上抛。
                Log.Warning($"[GlobalConfigMgr] global 配置表加载失败，已回退默认值。{e}");
                _cache = new Dictionary<int, string>();
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
        public static int EnergyRecoverCapValue => GetInt(EnergyRecoverCap, 30);
        public static int ClearToolEnergyCostValue => GetInt(ClearToolEnergyCost, 5);

        // ── 体力恢复（"点数#间隔秒"，id=3）──
        // value 格式 "amount#interval"：每 interval 秒恢复 amount 点体力。
        // 向后兼容：bare int（旧值 "360"）解析为 amount=1、interval=该值，老数据/单测不炸。
        // 缺键 / 整段非法 → amount=1、interval=360（与 global.xlsx 旧初值一致）。
        // 局部非法（如 "1#" / "#10" / "a#10"）：缺失或非法的那一半各自回退（amount→1，interval→360），另一半仍取合法部分。

        /// <summary>体力恢复默认每次点数（解析失败 / 缺键回退）。</summary>
        public const int EnergyRecoverAmountDefault = 1;
        /// <summary>体力恢复默认间隔秒（解析失败 / 缺键回退，与旧 "360" 一致）。</summary>
        public const int EnergyRecoverIntervalDefault = 360;

        /// <summary>每次恢复体力点数（"amount#interval" 的 amount；bare int / 缺键 → 默认 1）。</summary>
        public static int EnergyRecoverAmount => ParseEnergyRecover().amount;
        /// <summary>体力恢复间隔秒（"amount#interval" 的 interval；bare int 取该值；缺键 → 默认 360）。</summary>
        public static int EnergyRecoverIntervalSeconds => ParseEnergyRecover().interval;

        /// <summary>
        /// 解析体力恢复配置（id=3）为 (amount, interval)。
        /// 接受三种形态：① "amount#interval"（新格式）；② bare int（旧格式，作 interval、amount=1）；③ 缺键 / 空。
        /// 任一半解析失败独立回退到对应默认（amount→1，interval→360），不互相牵连、不抛。
        /// </summary>
        private static (int amount, int interval) ParseEnergyRecover()
        {
            string raw = GetString(EnergyRecoverSeconds, null);
            if (string.IsNullOrEmpty(raw))
                return (EnergyRecoverAmountDefault, EnergyRecoverIntervalDefault);

            int sep = raw.IndexOf('#');
            if (sep < 0)
            {
                // bare int（旧格式）：作间隔秒，amount=1。整段非法则全回退默认。
                return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv)
                    ? (EnergyRecoverAmountDefault, iv)
                    : (EnergyRecoverAmountDefault, EnergyRecoverIntervalDefault);
            }

            string amountStr = raw.Substring(0, sep);
            string intervalStr = raw.Substring(sep + 1);
            int amount = int.TryParse(amountStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var a)
                ? a : EnergyRecoverAmountDefault;
            int interval = int.TryParse(intervalStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
                ? i : EnergyRecoverIntervalDefault;
            return (amount, interval);
        }

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
