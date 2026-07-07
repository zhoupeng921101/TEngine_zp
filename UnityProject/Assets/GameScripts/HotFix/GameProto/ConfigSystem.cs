using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Luban;
using GameConfig;
using TEngine;
using UnityEngine;

/// <summary>
/// 配置加载器。
/// </summary>
public class ConfigSystem
{
    private static ConfigSystem _instance;

    public static ConfigSystem Instance => _instance ??= new ConfigSystem();

    private bool _init = false;

    private Tables _tables;

    public Tables Tables
    {
        get
        {
            if (!_init)
            {
                Load();
            }

            return _tables;
        }
    }

    private IResourceModule _resourceModule;

    private IResourceModule Resource => _resourceModule ??= ModuleSystem.GetModule<IResourceModule>();

    /// <summary>配置表资源名 → 已预载的原始字节(运行时 LoadByteBuf 命中即用,零同步加载)。</summary>
    private readonly Dictionary<string, byte[]> _byteCache = new();

    /// <summary>
    /// 全部配置表资源名(== <see cref="GameConfig.Tables"/> 各表 getter 传给 defaultLoader 的参数)。
    /// 与 Tables.cs 强耦合:新增 / 删除一张配置表须同步增删本清单对应项,否则运行时该表取字节走不到预载缓存
    ///(WebGL 原会落回同步加载报 "not support sync load")。
    /// </summary>
    private static readonly string[] TableAssetNames =
    {
        "block_tbweightcfg",
        "num_tbnum",
        "item_tbitemdef",
        "item_tbgiftrandom",
        "item_tbgiftselect",
        "avatar_tbavatar",
        "mail_tbmail",
        "mail_tbmailglobal",
        "rank_tbrank",
        "global_tbglobal",
        "block_tbmergeorder",
        "block_tbtarotcard",
        "audio_tbaudio",
        "block_tbgoddessreward",
    };

    /// <summary>
    /// 加载配置(建 Tables 懒加载 loader)。
    /// </summary>
    public void Load()
    {
        if (_init)
        {
            return; // 幂等:Tables 懒汉式(各表 getter 首访才读),重复 Load 只会重建 loader 壳、丢弃已懒载表并从缓存重载,徒增 churn。
        }

        _tables = new Tables(LoadByteBuf);
        _init = true;
    }

    /// <summary>
    /// 启动期异步预载全部配置表字节进缓存。WebGL 禁运行时同步加载未驻留 bundle,配置表须在首个消费者之前
    /// 异步预载:此方法必须在首个配置访问(如玩法窗打开)前 await 完成(接入 GameApp 入口闸 _preloadDone)。
    /// 逐项失败记 Error 不阻断;幂等(已缓存资源名跳过,软重启重跑可复用)。字节拷入缓存后立即 UnloadAsset——
    /// TextAsset 资源本身无需常驻,缓存持有的是拷出的 byte[]。
    /// </summary>
    public async UniTask PreloadAsync()
    {
        foreach (var name in TableAssetNames)
        {
            if (_byteCache.ContainsKey(name))
            {
                continue;
            }

            try
            {
                var textAsset = await Resource.LoadAssetAsync<TextAsset>(name);
                if (textAsset == null)
                {
                    Log.Error($"[ConfigSystem] 预载配置失败(返回 null):{name}。运行时该表取字节将失败。");
                    continue;
                }

                _byteCache[name] = textAsset.bytes;
                Resource.UnloadAsset(textAsset);
            }
            catch (System.Exception e)
            {
                Log.Error($"[ConfigSystem] 预载配置异常:{name}。{e}");
            }
        }
    }

    /// <summary>
    /// 加载二进制配置。命中预载字节缓存即用之(运行时唯一合法路径,零同步资源 LOAD)。
    /// </summary>
    /// <param name="file">FileName</param>
    /// <returns>ByteBuf</returns>
    private ByteBuf LoadByteBuf(string file)
    {
        if (_byteCache.TryGetValue(file, out var cached))
        {
            return new ByteBuf(cached);
        }

#if UNITY_EDITOR
        // 编辑器 / EditMode 单测:不跑启动预载流程,允许同步加载直接读(仅编辑器合法,WebGL 运行时禁此路径)。
        TextAsset textAsset = Resource.LoadAsset<TextAsset>(file);
        return new ByteBuf(textAsset.bytes);
#else
        // 运行时(含 WebGL)漏预载:不回退同步加载(WebGL 会报 "not support sync load")。抛异常暴露预载清单漏项。
        Log.Error($"[ConfigSystem] 配置 '{file}' 未预载,运行时禁止同步加载。请将其加入 ConfigSystem.TableAssetNames。");
        throw new System.InvalidOperationException($"[ConfigSystem] config '{file}' not preloaded");
#endif
    }
}
