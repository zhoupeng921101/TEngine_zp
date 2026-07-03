using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 玩法 UI / 特效预制与字体的启动期预载器（持有已加载资源引用，供运行时零同步加载实例化）。
    ///
    /// WebGL 约束：YooAsset 禁止同步加载未驻留 bundle，运行时调用同步资源加载 API 会被无条件拒绝并报
    /// "WebGL platform not support sync load method !"。客户端运行时代码因此禁用一切同步资源加载 API。
    ///
    /// 机制：启动期用异步 <c>LoadAssetAsync&lt;GameObject&gt;</c> / <c>LoadAssetAsync&lt;Font&gt;</c> 把玩法窗
    /// 高频复用的 widget / 特效预制与字体加载进来，并把返回的资源对象引用持有在本类的字典中（作模板常驻、
    /// 不 UnloadAsset）。之后运行时取用走 <see cref="Instantiate"/>（<c>UnityEngine.Object.Instantiate</c>
    /// 已加载预制，是实例化而非资源加载，WebGL 合法）/ <see cref="GetFont"/>（直接返回缓存字体引用）。
    /// 全程零同步资源 LOAD，且实例化同步、无弹出延迟。
    ///
    /// 模板资源随 app / 玩法生命周期常驻（不 UnloadAsset）：归还引用计数会让其可被自动释放、缓存失效。
    /// Instantiate 出来的实例由各自 GameObject 生命周期销毁，不依赖 AssetsReference 的 per-instance
    /// 引用计数（模板由本类持有常驻），故实例 Destroy 不影响模板存活，无泄漏。
    /// </summary>
    public static class UIPreloader
    {
        private static IResourceModule _resource;

        private static IResourceModule Resource => _resource ??= ModuleSystem.GetModule<IResourceModule>();

        /// <summary>location → 已加载预制模板引用（常驻）。</summary>
        private static readonly Dictionary<string, GameObject> PrefabCache = new();

        /// <summary>location → 已加载字体引用（常驻）。</summary>
        private static readonly Dictionary<string, Font> FontCache = new();

        /// <summary>
        /// 玩法窗内被实例化的 widget / 特效预制定位名（== AssetRaw/UI/Widgets、AssetRaw/Effects 下文件名）。
        /// 新增一个被玩法窗 CreateWidgetByType / Instantiate 的预制，须同步加入此清单，否则运行时取用会
        /// 拿不到模板（记 Error 返 null），暴露漏预载。
        /// </summary>
        private static readonly string[] GameplayPrefabLocations =
        {
            "BlockWidget",
            "ElementWidget",
            "OrderCardWidget",
            "CountdownWidget",
            "SynthTokenWidget",
            "ClearBurstFx",
        };

        /// <summary>
        /// 被代码创建文本取用的字体定位名（== AssetRaw/Fonts 下文件名）。
        /// UGuiFactory 代码创建 Text 时取本类缓存字体，新增一种被取用的字体须同步加入此清单。
        /// </summary>
        private static readonly string[] FontLocations =
        {
            "GBK",
        };

        /// <summary>
        /// 异步预载全部玩法 widget / 特效预制并持有模板引用。
        /// 必须在打开 UIMergeOrderPanel 之前 await 完成。逐项失败不阻断（记 Error，尽力放行其余）。
        /// 幂等：已缓存的 location 跳过（清档软重启重跑时去重）。
        /// </summary>
        public static async UniTask PreloadGameplayWidgetsAsync()
        {
            foreach (var location in GameplayPrefabLocations)
            {
                if (PrefabCache.ContainsKey(location))
                {
                    continue;
                }

                try
                {
                    // 用非实例化的 LoadAssetAsync<GameObject> 加载预制资源并持有引用作模板（驻留、不 UnloadAsset）。
                    var prefab = await Resource.LoadAssetAsync<GameObject>(location);
                    if (prefab == null)
                    {
                        Log.Error($"[UIPreloader] 预载预制失败（返回 null）：{location}。运行时取用将拿不到模板。");
                        continue;
                    }

                    PrefabCache[location] = prefab;
                }
                catch (System.Exception e)
                {
                    Log.Error($"[UIPreloader] 预载预制异常：{location}。{e}");
                }
            }
        }

        /// <summary>
        /// 异步预载全部被代码取用的字体并持有引用。
        /// 必须在首个 UGuiFactory 文本创建之前 await 完成。逐项失败不阻断（记 Error，尽力放行其余）。
        /// 幂等：已缓存的 location 跳过。
        /// </summary>
        public static async UniTask PreloadFontsAsync()
        {
            foreach (var location in FontLocations)
            {
                if (FontCache.ContainsKey(location))
                {
                    continue;
                }

                try
                {
                    // 用非实例化的 LoadAssetAsync<Font> 加载字体资源并持有引用（驻留、不 UnloadAsset）。
                    var font = await Resource.LoadAssetAsync<Font>(location);
                    if (font == null)
                    {
                        Log.Error($"[UIPreloader] 预载字体失败（返回 null）：{location}。文本将回退内置字体（无中文字形）。");
                        continue;
                    }

                    FontCache[location] = font;
                }
                catch (System.Exception e)
                {
                    Log.Error($"[UIPreloader] 预载字体异常：{location}。{e}");
                }
            }
        }

        /// <summary>
        /// 取已预载的预制模板。未命中返回 null（暴露漏预载，不静默触发同步加载）。
        /// </summary>
        public static GameObject GetPrefab(string location)
        {
            if (PrefabCache.TryGetValue(location, out var prefab) && prefab != null)
            {
                return prefab;
            }

            return null;
        }

        /// <summary>
        /// 实例化已预载的预制模板到指定父节点（<c>UnityEngine.Object.Instantiate</c>，同步、零资源 LOAD）。
        /// 模板未预载则记 Error 返 null——调用方须把该 location 加入预载清单，不在此回退同步加载。
        /// </summary>
        /// <param name="location">预制定位名（须在预载清单内）。</param>
        /// <param name="parent">实例父节点。</param>
        /// <returns>实例化的 GameObject；模板缺失返回 null。</returns>
        public static GameObject Instantiate(string location, Transform parent = null)
        {
            var prefab = GetPrefab(location);
            if (prefab == null)
            {
                Log.Error($"[UIPreloader] Instantiate 失败：预制 '{location}' 未预载。请将其加入 UIPreloader.GameplayPrefabLocations。");
                return null;
            }

            return UnityEngine.Object.Instantiate(prefab, parent);
        }

        /// <summary>
        /// 取已预载的字体。未命中返回 null（调用方自行兜底内置字体）。
        /// </summary>
        public static Font GetFont(string location)
        {
            if (FontCache.TryGetValue(location, out var font) && font != null)
            {
                return font;
            }

            return null;
        }
    }
}
