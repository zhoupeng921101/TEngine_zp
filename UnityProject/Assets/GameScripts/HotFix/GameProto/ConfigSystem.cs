using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Luban;
using GameConfig;
using TEngine;
using UnityEngine;

/// <summary>
/// 配置加载器。
///
/// WebGL 约束：YooAsset 在 WebGL 下禁止同步加载未驻留 bundle（报 "WebGL platform not support sync load method !"）。
/// 故配置二进制改为启动期异步预载到内存字节缓存（<see cref="PreloadAsync"/>），之后 Luban <see cref="Tables"/>
/// 的懒加载 loader（<see cref="LoadByteBuf"/>）直接从内存缓存读 byte[]，不再触发任何 bundle 加载。
/// 非 WebGL 平台（编辑器 / Standalone / Android / iOS）：未预载时 loader 回退同步加载（这些平台同步合法），行为等价。
/// </summary>
public class ConfigSystem
{
    private static ConfigSystem _instance;

    public static ConfigSystem Instance => _instance ??= new ConfigSystem();

    private bool _init = false;

    private Tables _tables;

    /// <summary>
    /// 配置二进制内存缓存（location → 原始 bytes）。由 <see cref="PreloadAsync"/> 在启动期异步灌入，
    /// <see cref="LoadByteBuf"/> 优先从此读，使 <see cref="Tables"/> 的懒加载不触发 bundle 同步加载。
    /// </summary>
    private readonly Dictionary<string, byte[]> _byteCache = new Dictionary<string, byte[]>();

    /// <summary>
    /// 全部 Luban 表二进制的资源定位名（== AssetRaw/Configs/bytes 下文件名，去扩展名）。
    /// 与 <see cref="Tables"/>（Luban 生成，各 TbXxx 调 defaultLoader 的字符串）一一对应：
    /// 新增 / 删除一张表，须同步增删此处一项（Tables.cs 由工具生成、不可手改，故在此维护预载清单）。
    /// </summary>
    private static readonly string[] ConfigLocations =
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
    };

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

    private IResourceModule ResourceModule => _resourceModule ??= ModuleSystem.GetModule<IResourceModule>();

    /// <summary>
    /// 同步构建 Tables（保留供非 WebGL 平台 / 已预载后的同步构建）。
    /// Tables 各表为懒加载：构造仅设 loader，首次访问某表才经 <see cref="LoadByteBuf"/> 取字节。
    /// </summary>
    public void Load()
    {
        _tables = new Tables(LoadByteBuf);
        _init = true;
    }

    /// <summary>
    /// 启动期异步预载全部配置二进制到内存缓存，并以缓存为数据源构建 <see cref="Tables"/>。
    /// 必须在任何 <see cref="Tables"/> 访问（含 GlobalConfigMgr / 各 ConfigMgr）之前 await 完成，
    /// 否则 WebGL 上首次表访问会落回同步 bundle 加载而失败。
    ///
    /// 加载失败不静默吞默认值：逐项缺失记 Error 并跳过（该表首次访问时仍会落回同步加载暴露问题），
    /// 不再制造"全表静默退默认"的假象路径。
    /// </summary>
    public async UniTask PreloadAsync()
    {
        foreach (var location in ConfigLocations)
        {
            if (_byteCache.ContainsKey(location))
            {
                continue;
            }

            try
            {
                var textAsset = await ResourceModule.LoadAssetAsync<TextAsset>(location);
                if (textAsset == null)
                {
                    Log.Error($"[ConfigSystem] 预载配置失败（返回 null）：{location}。WebGL 上该表访问将报同步加载错。");
                    continue;
                }

                _byteCache[location] = textAsset.bytes;
                // TextAsset 句柄已被资源模块对象池持有（LoadAssetAsync 注册 spawned），bytes 已拷入缓存，
                // 此处归还引用计数避免长期占用（缓存持 byte[]，不依赖 TextAsset 存活）。
                ResourceModule.UnloadAsset(textAsset);
            }
            catch (Exception e)
            {
                Log.Error($"[ConfigSystem] 预载配置异常：{location}。{e}");
            }
        }

        // 以已灌满的内存缓存为数据源构建 Tables（懒加载 loader 命中缓存，不触发 bundle 加载）。
        Load();
    }

    /// <summary>
    /// 加载二进制配置：取内存缓存（预载后命中，零资源加载）。
    /// 运行时唯一数据源是预载缓存；客户端运行时禁用同步资源加载 API，故缓存未命中即抛
    /// （说明该表未被预载，应补进 <see cref="ConfigLocations"/>）。
    /// 仅编辑器（含 EditMode 单测，不走启动预载流程）保留同步回退，便于无预载直接读配置。
    /// </summary>
    /// <param name="file">资源定位名（去扩展名的文件名）。</param>
    /// <returns>ByteBuf。</returns>
    private ByteBuf LoadByteBuf(string file)
    {
        if (_byteCache.TryGetValue(file, out var cached))
        {
            return new ByteBuf(cached);
        }

#if UNITY_EDITOR
        // 编辑器 / EditMode 单测专用回退：测试不跑启动期预载，直接同步读配置。运行时（含 WebGL）零同步加载。
        TextAsset textAsset = ResourceModule.LoadAsset<TextAsset>(file);
        if (textAsset == null)
        {
            throw new GameFrameworkException($"[ConfigSystem] 配置加载失败：{file}（资源不存在）。");
        }

        return new ByteBuf(textAsset.bytes);
#else
        throw new GameFrameworkException($"[ConfigSystem] 配置 {file} 未预载，运行时禁用同步加载。请将其加入 ConfigSystem.ConfigLocations 由启动期异步预载。");
#endif
    }
}
