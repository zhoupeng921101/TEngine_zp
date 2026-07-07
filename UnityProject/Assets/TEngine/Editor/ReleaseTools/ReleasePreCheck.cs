using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using UnityEditor;
using YooAsset;
using YooAsset.Editor;

namespace TEngine
{
    /// <summary>
    /// 发布前自检:把打包/发布环节反复踩到的失败模式(WebGL 首包缺内置目录、必崩拷贝组合、
    /// 部署遗漏/半量、版本不一致)固化成可核对的检查项。分本地(配置 + 构建产物)与线上(CDN 可达性 + 版本一致)两组。
    /// </summary>
    public static class ReleasePreCheck
    {
        public enum CheckStatus { Pass, Warn, Fail }

        public class CheckItem
        {
            public string Name;
            public CheckStatus Status;
            public string Message;
            public string Fix;

            public CheckItem(string name, CheckStatus status, string message, string fix = null)
            {
                Name = name;
                Status = status;
                Message = message;
                Fix = fix;
            }
        }

        private const string PackageName = "DefaultPackage";
        private const string BuildinCatalogFileName = "BuildinCatalog.bytes"; // 对应 YooAsset DefaultBuildinFileSystemDefine(internal,跨程序集用字面量)

        // ---------- 本地检查(配置 + 构建产物,不联网) ----------

        public static List<CheckItem> RunLocalChecks(BuildConfig config)
        {
            var items = new List<CheckItem>();
            bool isWebGL = config.BuildTarget == BuildTarget.WebGL;
            var loadWay = Settings.UpdateSetting.GetLoadResWayWebGL();
            bool webglRemote = isWebGL && loadWay == LoadResWayWebGL.Remote;

            items.Add(new CheckItem("平台 / 加载模式", CheckStatus.Pass,
                $"目标平台 {config.BuildTarget}" + (isWebGL ? $",WebGL 加载方式 {loadWay}" : "")));

            CheckCopyOption(config, webglRemote, items);
            CheckMinimalPackage(config, isWebGL, items);
            CheckHostScheme(isWebGL, items);
            CheckFileNameStyle(config, webglRemote, items);
            CheckByTagsHitDll(config, isWebGL, items);

            CheckBuildinCatalog(isWebGL, items);
            CheckFirstPackageDll(isWebGL, items);
            CheckManifestTriple(items);
            CheckPlayerOutput(config, isWebGL, items);

            return items;
        }

        private static void CheckCopyOption(BuildConfig config, bool webglRemote, List<CheckItem> items)
        {
            if (!webglRemote)
                return;

            bool none = config.BuildinFileCopyOption == EBuildinFileCopyOption.None;
            bool byTags = config.BuildinFileCopyOption == EBuildinFileCopyOption.ClearAndCopyByTags
                          || config.BuildinFileCopyOption == EBuildinFileCopyOption.OnlyCopyByTags;
            bool byTagsEmpty = byTags && string.IsNullOrWhiteSpace(config.BuildinFileCopyParams);

            if (none || byTagsEmpty)
            {
                items.Add(new CheckItem("内置文件拷贝", CheckStatus.Fail,
                    none ? "WebGL+Remote 下拷贝档为 None:首包不会有 BuildinCatalog,运行时资源初始化必失败。"
                         : "WebGL+Remote 下选了 ByTags 但『首包保留Tag』为空:等同 None,运行时必失败。",
                    none ? "改用 ClearAndCopyByTags 并填首包保留Tag(如 buildin),或 ClearAndCopyAll。"
                         : "在高级设置填『首包保留Tag』(如 buildin)。"));
            }
            else
            {
                items.Add(new CheckItem("内置文件拷贝", CheckStatus.Pass,
                    $"{config.BuildinFileCopyOption}" +
                    (string.IsNullOrWhiteSpace(config.BuildinFileCopyParams) ? "" : $"(Tag: {config.BuildinFileCopyParams})")));
            }
        }

        private static void CheckMinimalPackage(BuildConfig config, bool isWebGL, List<CheckItem> items)
        {
            if (config.MinimalPackage && isWebGL)
            {
                items.Add(new CheckItem("最小包模式", CheckStatus.Warn,
                    "WebGL 上最小包(后删 .bundle 不重建 catalog)会产坏包:被删 bundle 仍被内置文件系统认领而 404,不回落 CDN。",
                    "WebGL 首包瘦身改用 内置文件拷贝=ClearAndCopyByTags + 首包保留Tag,不要用最小包模式。"));
            }
        }

        private static void CheckHostScheme(bool isWebGL, List<CheckItem> items)
        {
            if (!isWebGL)
                return;

            string host = Settings.UpdateSetting.GetResDownLoadPath();
            if (host != null && host.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new CheckItem("资源地址协议", CheckStatus.Fail,
                    $"资源地址是 http:// —— WebGL 页面通常是 https,浏览器会以 mixed-content 拦截 http 资源。\n{host}",
                    "把 UpdateSetting.ResDownLoadPath 改为 https://。"));
            }
            else
            {
                items.Add(new CheckItem("资源地址协议", CheckStatus.Pass, host));
            }
        }

        private static void CheckFileNameStyle(BuildConfig config, bool webglRemote, List<CheckItem> items)
        {
            if (!webglRemote)
                return;

            // BundleName(无哈希)下 bundle URL 固定,内容变而 URL 不变,CDN/浏览器会用旧缓存冒充新文件 → CRC Mismatch。
            if (config.FileNameStyle == EFileNameStyle.BundleName)
            {
                items.Add(new CheckItem("文件名风格", CheckStatus.Fail,
                    "WebGL+Remote 下用 BundleName(无哈希):bundle URL 固定,重新部署时 CDN 会用旧缓存冒充新文件,导致 CRC Mismatch、资源加载失败。",
                    "改用 BundleName_HashName 或 HashName(文件名带内容哈希,内容变则 URL 变)。"));
            }
            else
            {
                items.Add(new CheckItem("文件名风格", CheckStatus.Pass, $"{config.FileNameStyle}(带内容哈希,规避 CDN 缓存)。"));
            }
        }

        private static void CheckByTagsHitDll(BuildConfig config, bool isWebGL, List<CheckItem> items)
        {
            bool byTags = config.BuildinFileCopyOption == EBuildinFileCopyOption.ClearAndCopyByTags
                          || config.BuildinFileCopyOption == EBuildinFileCopyOption.OnlyCopyByTags;
            if (!isWebGL || !byTags || string.IsNullOrWhiteSpace(config.BuildinFileCopyParams))
                return;

            var wantTags = config.BuildinFileCopyParams
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim()).ToList();

            string dllTags;
            try
            {
                var setting = AssetBundleCollectorSettingData.Setting;
                var pkg = setting.Packages.FirstOrDefault(p => p.PackageName == PackageName);
                var dllGroup = pkg?.Groups.FirstOrDefault(g => string.Equals(g.GroupName, "DLL", StringComparison.OrdinalIgnoreCase));
                dllTags = dllGroup?.AssetTags ?? "";
            }
            catch (Exception e)
            {
                items.Add(new CheckItem("首包覆盖 DLL", CheckStatus.Warn, $"无法读取采集器配置核对:{e.Message}"));
                return;
            }

            var dllTagList = dllTags.Split(new[] { ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            bool hit = dllTagList.Any(wantTags.Contains);
            if (hit)
            {
                items.Add(new CheckItem("首包覆盖 DLL", CheckStatus.Pass,
                    $"DLL 组 Tag [{dllTags}] 命中首包保留 Tag,热更/AOT DLL 会进首包。"));
            }
            else
            {
                items.Add(new CheckItem("首包覆盖 DLL", CheckStatus.Warn,
                    $"DLL 组 Tag [{(string.IsNullOrEmpty(dllTags) ? "空" : dllTags)}] 未被首包保留 Tag [{config.BuildinFileCopyParams}] 命中:" +
                    "热更/AOT DLL 不进首包,启动要先从 CDN 下 DLL(慢且强依赖网络)。",
                    "给采集器 DLL 组打上首包保留 Tag(如 buildin),或把 DLL 组的 Tag 加进首包保留Tag。"));
            }
        }

        private static void CheckBuildinCatalog(bool isWebGL, List<CheckItem> items)
        {
            if (!isWebGL)
                return;

            string path = Path.Combine(AssetBundleBuilderHelper.GetStreamingAssetsRoot(), PackageName, BuildinCatalogFileName);
            if (File.Exists(path))
                items.Add(new CheckItem("内置目录 BuildinCatalog", CheckStatus.Pass, "StreamingAssets 存在内置目录。"));
            else
                items.Add(new CheckItem("内置目录 BuildinCatalog", CheckStatus.Fail,
                    $"缺失:{path}\nPlayer 运行时会卡在内置文件系统初始化(读不到内置清单)而下载不到资源。",
                    "先打一次 AB(内置文件拷贝需为 ClearAndCopyByTags/All)再打 Player。"));
        }

        private static void CheckFirstPackageDll(bool isWebGL, List<CheckItem> items)
        {
            if (!isWebGL)
                return;

            string dir = Path.Combine(AssetBundleBuilderHelper.GetStreamingAssetsRoot(), PackageName);
            if (!Directory.Exists(dir))
            {
                items.Add(new CheckItem("首包 DLL", CheckStatus.Warn, "StreamingAssets 首包目录不存在(尚未打 AB?)。"));
                return;
            }

            var bundles = Directory.GetFiles(dir, "*.bundle", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).ToList();
            bool hasLogic = bundles.Any(b => b.Contains("gamelogic_dll"));
            bool hasProto = bundles.Any(b => b.Contains("gameproto_dll"));
            if (hasLogic && hasProto)
                items.Add(new CheckItem("首包 DLL", CheckStatus.Pass, $"首包含 {bundles.Count} 个 bundle,含热更 DLL。"));
            else
                items.Add(new CheckItem("首包 DLL", CheckStatus.Warn,
                    $"首包 {bundles.Count} 个 bundle,未见热更 DLL(gamelogic/gameproto)。",
                    "确认 DLL 组已打首包保留 Tag,启动依赖这些 DLL。"));
        }

        private static void CheckManifestTriple(List<CheckItem> items)
        {
            string dir = Path.Combine(AssetBundleBuilderHelper.GetStreamingAssetsRoot(), PackageName);
            string versionFile = Path.Combine(dir, $"{PackageName}.version");
            if (!File.Exists(versionFile))
            {
                items.Add(new CheckItem("首包清单", CheckStatus.Warn, "未找到首包 .version(尚未打 AB?)。"));
                return;
            }

            string ver = File.ReadAllText(versionFile).Trim();
            string bytes = Path.Combine(dir, $"{PackageName}_{ver}.bytes");
            string hash = Path.Combine(dir, $"{PackageName}_{ver}.hash");
            if (File.Exists(bytes) && File.Exists(hash))
                items.Add(new CheckItem("首包清单", CheckStatus.Pass, $"版本 {ver},.version/.bytes/.hash 齐全。"));
            else
                items.Add(new CheckItem("首包清单", CheckStatus.Fail,
                    $"版本 {ver} 的 manifest 不齐:{(File.Exists(bytes) ? "" : ".bytes 缺 ")}{(File.Exists(hash) ? "" : ".hash 缺")}",
                    "重新打一次 AB。"));
        }

        private static void CheckPlayerOutput(BuildConfig config, bool isWebGL, List<CheckItem> items)
        {
            string outPath = config.PlayerOutputPath;
            if (string.IsNullOrEmpty(outPath))
                return;

            string dir = File.Exists(outPath) ? Path.GetDirectoryName(outPath) : outPath;
            if (!Directory.Exists(dir))
            {
                items.Add(new CheckItem("Player 产物", CheckStatus.Warn, $"Player 输出目录不存在(尚未打 Player?):{dir}"));
                return;
            }

            if (isWebGL)
            {
                bool hasIndex = File.Exists(Path.Combine(dir, "index.html"));
                bool hasBuild = Directory.Exists(Path.Combine(dir, "Build"));
                bool hasSa = Directory.Exists(Path.Combine(dir, "StreamingAssets"));
                if (hasIndex && hasBuild && hasSa)
                    items.Add(new CheckItem("Player 产物", CheckStatus.Pass, "index.html + Build/ + StreamingAssets/ 齐全。"));
                else
                    items.Add(new CheckItem("Player 产物", CheckStatus.Warn,
                        $"WebGL 产物不全:{(hasIndex ? "" : "缺 index.html ")}{(hasBuild ? "" : "缺 Build/ ")}{(hasSa ? "" : "缺 StreamingAssets/")}",
                        "重新打 Player;StreamingAssets 缺失说明打 Player 前没打 AB。"));
            }
            else
            {
                items.Add(new CheckItem("Player 产物", CheckStatus.Pass, $"输出目录存在:{dir}"));
            }
        }

        // ---------- 线上检查(CDN 可达性 + 版本一致,联网) ----------

        public static List<CheckItem> RunRemoteChecks(BuildConfig config)
        {
            var items = new List<CheckItem>();
            string cdnBase = Settings.UpdateSetting.GetResDownLoadPath();
            items.Add(new CheckItem("CDN 地址", CheckStatus.Pass, cdnBase));

            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
            {
                // 本地首包版本(作为一致性基准)
                string localVer = TryReadLocalVersion();

                // CDN 版本文件
                string cdnVerUrl = $"{cdnBase}/{PackageName}.version";
                var (verOk, verCode, verBody) = HttpGet(http, cdnVerUrl);
                if (!verOk)
                {
                    items.Add(new CheckItem("CDN 版本文件", CheckStatus.Fail,
                        $"{cdnVerUrl}\n{verCode}",
                        "AB 没上传或路径不对;点『一键部署 AB』把 AB 传到 CDN。"));
                    return items; // 版本拿不到,后续无从核对
                }

                string cdnVer = (verBody ?? "").Trim();
                items.Add(new CheckItem("CDN 版本文件", CheckStatus.Pass, $"版本 {cdnVer}"));

                // 版本一致性:本地首包 vs CDN
                if (!string.IsNullOrEmpty(localVer))
                {
                    if (localVer == cdnVer)
                        items.Add(new CheckItem("版本一致性", CheckStatus.Pass, $"首包与 CDN 均为 {cdnVer}。"));
                    else
                        items.Add(new CheckItem("版本一致性", CheckStatus.Warn,
                            $"首包版本 {localVer} ≠ CDN 版本 {cdnVer}:Player 内的 DLL bundle 可能与 CDN 清单不匹配(半量部署/漏传其一)。",
                            "AB 与 Player 要同一批打并成对部署:先『一键部署 AB』再『一键部署 Player』。"));
                }

                // CDN manifest 三件套
                string bytesUrl = $"{cdnBase}/{PackageName}_{cdnVer}.bytes";
                string hashUrl = $"{cdnBase}/{PackageName}_{cdnVer}.hash";
                var (bOk, bCode, _) = HttpHead(http, bytesUrl);
                var (hOk, hCode, _) = HttpHead(http, hashUrl);
                if (bOk && hOk)
                    items.Add(new CheckItem("CDN 清单文件", CheckStatus.Pass, ".bytes / .hash 均可达。"));
                else
                    items.Add(new CheckItem("CDN 清单文件", CheckStatus.Fail,
                        $"{(bOk ? "" : $"{bytesUrl} {bCode}\n")}{(hOk ? "" : $"{hashUrl} {hCode}")}",
                        "AB 上传不完整,重新『一键部署 AB』。"));

                // 抽样非首包 bundle(应在 CDN)
                CheckSampleRemoteBundles(http, config, cdnBase, items);
            }

            return items;
        }

        private static void CheckSampleRemoteBundles(HttpClient http, BuildConfig config, string cdnBase, List<CheckItem> items)
        {
            List<string> firstPackage = new List<string>();
            string saDir = Path.Combine(AssetBundleBuilderHelper.GetStreamingAssetsRoot(), PackageName);
            if (Directory.Exists(saDir))
                firstPackage = Directory.GetFiles(saDir, "*.bundle").Select(Path.GetFileName).ToList();

            string abDir = ReleaseTools.GetAssetBundleOutputDirectory(config);
            if (string.IsNullOrEmpty(abDir) || !Directory.Exists(abDir))
            {
                items.Add(new CheckItem("CDN 非首包抽样", CheckStatus.Warn, "找不到本地 AB 产物目录,跳过非首包抽样。"));
                return;
            }

            var remoteBundles = Directory.GetFiles(abDir, "*.bundle")
                .Select(Path.GetFileName)
                .Where(b => !firstPackage.Contains(b))
                .Take(3)
                .ToList();

            if (remoteBundles.Count == 0)
            {
                items.Add(new CheckItem("CDN 非首包抽样", CheckStatus.Warn, "没有非首包 bundle 可抽样(是否全量进了首包?)。"));
                return;
            }

            var missing = new List<string>();
            foreach (var b in remoteBundles)
            {
                var (ok, code, _) = HttpHead(http, $"{cdnBase}/{b}");
                if (!ok)
                    missing.Add($"{b} {code}");
            }

            if (missing.Count == 0)
                items.Add(new CheckItem("CDN 非首包抽样", CheckStatus.Pass,
                    $"抽样 {remoteBundles.Count} 个非首包 bundle 均可达。"));
            else
                items.Add(new CheckItem("CDN 非首包抽样", CheckStatus.Fail,
                    "非首包 bundle 在 CDN 缺失(非首屏资源会下载不到):\n" + string.Join("\n", missing),
                    "AB 上传不完整,重新『一键部署 AB』。"));
        }

        private static string TryReadLocalVersion()
        {
            try
            {
                string f = Path.Combine(AssetBundleBuilderHelper.GetStreamingAssetsRoot(), PackageName, $"{PackageName}.version");
                return File.Exists(f) ? File.ReadAllText(f).Trim() : null;
            }
            catch
            {
                return null;
            }
        }

        private static (bool ok, string code, string body) HttpGet(HttpClient http, string url)
        {
            try
            {
                var resp = http.GetAsync(url).GetAwaiter().GetResult();
                string body = resp.IsSuccessStatusCode ? resp.Content.ReadAsStringAsync().GetAwaiter().GetResult() : null;
                return (resp.IsSuccessStatusCode, $"HTTP {(int)resp.StatusCode}", body);
            }
            catch (Exception e)
            {
                return (false, $"请求失败:{e.Message}", null);
            }
        }

        private static (bool ok, string code, string body) HttpHead(HttpClient http, string url)
        {
            try
            {
                using (var req = new HttpRequestMessage(HttpMethod.Head, url))
                {
                    var resp = http.SendAsync(req).GetAwaiter().GetResult();
                    return (resp.IsSuccessStatusCode, $"HTTP {(int)resp.StatusCode}", null);
                }
            }
            catch (Exception e)
            {
                return (false, $"请求失败:{e.Message}", null);
            }
        }
    }
}
