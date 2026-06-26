using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 玩法 UI / 特效预制启动期预载器。
    ///
    /// WebGL 约束：YooAsset 禁止同步加载未驻留 bundle。玩法窗（MergeOrderWindow）在 OnCreate / 渲染流程里
    /// 高频用同步 <c>LoadGameObject</c>（经 <see cref="UIBaseMono.CreateWidgetByType{T}"/> → CreateWidgetByPath）
    /// 实例化 widget，这些路径不能逐用改异步（滚动 / 落子 / 飞行每帧实例化会丢帧）。
    ///
    /// 机制：启动期先异步 <c>LoadAssetAsync&lt;GameObject&gt;</c> 把这些 widget / 特效预制载入资源模块对象池
    /// （注册为 spawned，恒驻留不被自动释放）；之后同步 <c>LoadGameObject(location)</c> 命中对象池缓存、直接
    /// <c>Instantiate</c> 缓存预制，不再触发 bundle 同步加载。零同步 bundle 加载且无弹出延迟。
    ///
    /// 预制必须随 app / 玩法生命周期常驻（不 UnloadAsset）：它是后续同步实例化的模板，归还引用计数会让其
    /// 可被自动释放、缓存失效。
    /// </summary>
    public static class UIPreloader
    {
        private static IResourceModule _resource;

        private static IResourceModule Resource => _resource ??= ModuleSystem.GetModule<IResourceModule>();

        /// <summary>
        /// 玩法窗内被同步实例化的 widget / 特效预制定位名（== AssetRaw/UI/Widgets、AssetRaw/Effects 下文件名）。
        /// 新增一个被玩法窗同步 LoadGameObject / CreateWidgetByType 的预制，须同步加入此清单，否则 WebGL 上首次
        /// 实例化会落回同步 bundle 加载而报错。
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
        /// 被代码同步取用的字体定位名（== AssetRaw/Fonts 下文件名）。
        /// UGuiFactory 代码创建 Text 时同步 LoadAsset&lt;Font&gt;(location) 取字体，WebGL 禁同步 bundle 加载，
        /// 故须启动期预载进资源池驻留。新增一种被同步取用的字体，须同步加入此清单。
        /// </summary>
        private static readonly string[] FontLocations =
        {
            "GBK",
        };

        /// <summary>
        /// 异步预载全部玩法 widget / 特效预制到资源模块对象池（驻留）。
        /// 必须在打开 MergeOrderWindow 之前 await 完成。逐项失败不阻断（记 Error，尽力放行其余）。
        /// </summary>
        public static async UniTask PreloadGameplayWidgetsAsync()
        {
            foreach (var location in GameplayPrefabLocations)
            {
                try
                {
                    // 注意：用非实例化的 LoadAssetAsync<GameObject>（载入预制资源并注册进对象池、驻留），
                    // 不用 LoadGameObjectAsync（那会立即实例化一个无用实例）。返回值故意丢弃：预制留在池中作模板。
                    var prefab = await Resource.LoadAssetAsync<GameObject>(location);
                    if (prefab == null)
                    {
                        Log.Error($"[UIPreloader] 预载预制失败（返回 null）：{location}。WebGL 上该预制同步实例化将报错。");
                    }
                }
                catch (System.Exception e)
                {
                    Log.Error($"[UIPreloader] 预载预制异常：{location}。{e}");
                }
            }
        }

        /// <summary>
        /// 异步预载全部被同步取用的字体到资源模块对象池（驻留）。
        /// 必须在首个 UGuiFactory 文本创建之前 await 完成。逐项失败不阻断（记 Error，尽力放行其余）。
        /// </summary>
        public static async UniTask PreloadFontsAsync()
        {
            foreach (var location in FontLocations)
            {
                try
                {
                    // 用非实例化的 LoadAssetAsync<Font>（载入字体资源并注册进对象池、驻留），返回值故意丢弃、不 UnloadAsset：
                    // 字体须留池作后续同步 LoadAsset<Font>(location) 取用的缓存，归还引用计数会让其可被自动释放、缓存失效。
                    var font = await Resource.LoadAssetAsync<Font>(location);
                    if (font == null)
                    {
                        Log.Error($"[UIPreloader] 预载字体失败（返回 null）：{location}。WebGL 上该字体同步取用将报错、回退内置字体。");
                    }
                }
                catch (System.Exception e)
                {
                    Log.Error($"[UIPreloader] 预载字体异常：{location}。{e}");
                }
            }
        }
    }
}
