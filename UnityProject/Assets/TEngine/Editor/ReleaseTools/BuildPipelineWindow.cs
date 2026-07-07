using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace TEngine
{
    public class BuildPipelineWindow : EditorWindow
    {
        private static readonly string[] PlatformNames = new string[]
        {
            "Windows 64-bit",
            "macOS",
            "Linux",
            "Android",
            "iOS",
            "WebGL",
        };

        private static readonly BuildTarget[] PlatformTargets = new BuildTarget[]
        {
            BuildTarget.StandaloneWindows64,
            BuildTarget.StandaloneOSX,
            BuildTarget.StandaloneLinux64,
            BuildTarget.Android,
            BuildTarget.iOS,
            BuildTarget.WebGL,
        };

        private static readonly string[] PipelineNames = new string[]
        {
            "ScriptableBuildPipeline (SBP)",
            "BuiltinBuildPipeline (内置)",
        };

        private static readonly string[] CompressNames = new string[]
        {
            "Uncompressed (不压缩)",
            "LZMA (高压缩)",
            "LZ4 (快速压缩)",
        };

        private static readonly string[] EncryptionNames = new string[]
        {
            "无加密",
            "文件偏移加密",
            "文件流加密",
        };

        private static readonly string[] CopyOptionNames = new string[]
        {
            "None (不拷贝)",
            "ClearAndCopyAll (清空后拷贝全部)",
            "ClearAndCopyByTags (清空后按Tag拷贝)",
            "OnlyCopyAll (仅拷贝全部)",
            "OnlyCopyByTags (仅按Tag拷贝)",
        };

        private static readonly string[] FileNameStyleNames = new string[]
        {
            "HashName (哈希名)",
            "BundleName (资源包名称)",
            "BundleName_HashName (资源包名称 + 哈希值名称)",
        };

        // 配置状态
        private BuildConfig _config;

        // UI 状态
        private Vector2 _scrollPosition;
        private bool _showBasicSettings = true;
        private bool _showMinimalPackageSettings = true;
        private bool _showAdvancedSettings;
        private bool _showDllSettings = true;
        private bool _showPlayerSettings;
        private bool _showBuildLog;
        private int _platformIndex;
        private int _playerPlatformIndex;

        // 构建日志
        private List<string> _buildLogs = new List<string>();
        private Vector2 _logScrollPosition;

        // 发布前自检
        private bool _showPreCheck = true;
        private List<ReleasePreCheck.CheckItem> _localCheckResults;
        private List<ReleasePreCheck.CheckItem> _remoteCheckResults;

        // 部署设置（构建后经 Git Bash + ssh/scp 增量上传；地址可在面板填写）
        private bool _showDeploySettings = true;
        private string _deployHost;
        private string _deployUser;
        private string _deployPort;
        private string _deployKey;
        private string _deployAbRemoteDir;
        private string _deployPlayerRemoteDir;

        // 上次部署 AB 时使用的资源版本号:用于在同版本号重复部署时提示确认(防资源改动却漏 bump)。
        private const string LastDeployedAbVersionKey = "TEngine_BP_LastDeployedAbVersion";

        [MenuItem("TEngine/Build/打包工具窗口", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<BuildPipelineWindow>("TEngine 打包工具");
            window.minSize = new Vector2(450, 600);
            window.Show();
        }

        private void OnEnable()
        {
            LoadSettings();
        }

        private void OnGUI()
        {
            if (_config == null)
                LoadSettings();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            {
                DrawHeader();
                DrawBasicSettings();
                DrawMinimalPackageSettings();
                DrawAdvancedSettings();
                DrawDllSettings();
                DrawPlayerSettings();
                DrawDeploySettings();
                DrawPreCheckSection();
                DrawActionButtons();
                DrawBuildLog();
            }
            EditorGUILayout.EndScrollView();
        }

        #region Header

        private void DrawHeader()
        {
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            var titleStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };

            EditorGUILayout.LabelField("TEngine 打包工具", titleStyle, GUILayout.Height(30));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // 刷新按钮
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("刷新", GUILayout.Width(60), GUILayout.Height(22)))
            {
                LoadSettings();
            }

            if (GUILayout.Button("重置默认", GUILayout.Width(80), GUILayout.Height(22)))
            {
                _config = BuildConfig.CreateDefault();
                SaveSettings();
                AddLog("已重置为默认配置");
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);
        }

        #endregion

        #region 基础设置

        private void DrawBasicSettings()
        {
            _showBasicSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showBasicSettings,
                new GUIContent("基础设置", "目标平台、构建管线、加密等核心参数"));

            if (_showBasicSettings)
            {
                EditorGUILayout.BeginVertical("HelpBox");
                {
                    // 目标平台
                    _platformIndex = EditorGUILayout.Popup("目标平台", _platformIndex, PlatformNames);
                    _config.BuildTarget = PlatformTargets[_platformIndex];

                    EditorGUILayout.Space(3);

                    // 构建管线
                    int pipelineIndex = _config.BuildPipeline == EBuildPipeline.BuiltinBuildPipeline ? 1 : 0;
                    pipelineIndex = EditorGUILayout.Popup("构建管线", pipelineIndex, PipelineNames);
                    _config.BuildPipeline = pipelineIndex == 1
                        ? EBuildPipeline.BuiltinBuildPipeline
                        : EBuildPipeline.ScriptableBuildPipeline;

                    // 压缩方式
                    _config.CompressOption = (ECompressOption)EditorGUILayout.Popup("压缩方式",
                        (int)_config.CompressOption, CompressNames);

                    // 加密方式
                    _config.EncryptionType = (EncryptionType)EditorGUILayout.Popup("加密方式",
                        (int)_config.EncryptionType, EncryptionNames);

                    EditorGUILayout.Space(3);

                    // 资源版本号
                    EditorGUILayout.BeginHorizontal();
                    _config.PackageVersion = EditorGUILayout.TextField("资源版本号", _config.PackageVersion);
                    if (GUILayout.Button("自动", GUILayout.Width(50)))
                    {
                        _config.PackageVersion = BuildConfig.GetDefaultPackageVersion();
                    }

                    EditorGUILayout.EndHorizontal();

                    // 输出目录
                    EditorGUILayout.BeginHorizontal();
                    _config.OutputRoot = EditorGUILayout.TextField("AB输出目录", _config.OutputRoot);
                    if (GUILayout.Button("浏览", GUILayout.Width(50)))
                    {
                        string selected = EditorUtility.OpenFolderPanel("选择输出目录", _config.OutputRoot, "");
                        if (!string.IsNullOrEmpty(selected))
                        {
                            string projectPath = PathGetRelative(Application.dataPath + "/../", selected);
                            _config.OutputRoot = string.IsNullOrEmpty(projectPath) ? selected : projectPath;
                        }
                    }

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(3);
                    EditorGUILayout.HelpBox("选择构建目标平台和基础参数。AB输出目录支持相对路径（相对于项目根目录）。", MessageType.Info);
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            GUILayout.Space(5);
        }

        #endregion

        #region 最小包设置

        private void DrawMinimalPackageSettings()
        {
            _showMinimalPackageSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showMinimalPackageSettings,
                new GUIContent("最小包设置", "删除 StreamingAssets 中的 .bundle 文件以减小首包体积"));

            if (_showMinimalPackageSettings)
            {
                EditorGUILayout.BeginVertical("HelpBox");
                {
                    _config.MinimalPackage = EditorGUILayout.ToggleLeft(
                        new GUIContent("启用最小包模式", "构建后删除 StreamingAssets 中的 .bundle 文件"),
                        _config.MinimalPackage);

                    if (_config.MinimalPackage)
                    {
                        EditorGUILayout.Space(3);
                        _config.RetainTags = EditorGUILayout.TextField(
                            new GUIContent("保留Tag(逗号分隔)", "带这些Tag的bundle不会被删除"),
                            _config.RetainTags);

                        EditorGUILayout.Space(3);

                        string tagInfo = string.IsNullOrWhiteSpace(_config.RetainTags)
                            ? "所有 .bundle 文件将被删除（仅保留清单）"
                            : $"保留带 [{_config.RetainTags}] Tag 的 bundle，其余删除";

                        EditorGUILayout.HelpBox(
                            $"最小包模式：删除 StreamingAssets 中所有 .bundle 文件，仅保留清单文件（.bytes/.hash/.version）。\n" +
                            $"当前: {tagInfo}\n\n" +
                            $"适用于 HostPlayMode 在线下载资源的场景，可大幅减小首包体积。",
                            MessageType.Info);
                    }
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            GUILayout.Space(5);
        }

        #endregion

        #region 高级设置

        private void DrawAdvancedSettings()
        {
            _showAdvancedSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showAdvancedSettings,
                new GUIContent("高级设置", "共享打包、依赖数据库、增量构建等"));

            if (_showAdvancedSettings)
            {
                EditorGUILayout.BeginVertical("HelpBox");
                {
                    _config.EnableSharePackRule = EditorGUILayout.ToggleLeft(
                        new GUIContent("启用共享资源打包", "自动提取共享资源到独立bundle"),
                        _config.EnableSharePackRule);

                    _config.UseAssetDependencyDB = EditorGUILayout.ToggleLeft(
                        new GUIContent("使用资源依赖数据库", "提高打包速度"),
                        _config.UseAssetDependencyDB);

                    _config.ClearBuildCache = EditorGUILayout.ToggleLeft(
                        new GUIContent("清理构建缓存(禁用增量构建)", "全量重新构建"),
                        _config.ClearBuildCache);

                    _config.VerifyBuildingResult = EditorGUILayout.ToggleLeft(
                        new GUIContent("验证构建结果", "构建后验证资源完整性"),
                        _config.VerifyBuildingResult);

                    EditorGUILayout.Space(3);

                    _config.BuildinFileCopyOption = (EBuildinFileCopyOption)EditorGUILayout.Popup(
                        "内置文件拷贝", (int)_config.BuildinFileCopyOption, CopyOptionNames);

                    // ByTags 拷贝需指定首包保留的 Tag(仅 ByTags 档位有效)
                    bool isByTags = _config.BuildinFileCopyOption == EBuildinFileCopyOption.ClearAndCopyByTags
                                    || _config.BuildinFileCopyOption == EBuildinFileCopyOption.OnlyCopyByTags;
                    if (isByTags)
                    {
                        _config.BuildinFileCopyParams = EditorGUILayout.TextField(
                            new GUIContent("首包保留Tag", "ByTags 拷贝:带这些 Tag 的 bundle 进首包(StreamingAssets),其余走 CDN。多个用分号分隔,如 buildin"),
                            _config.BuildinFileCopyParams);
                        EditorGUILayout.HelpBox(
                            string.IsNullOrWhiteSpace(_config.BuildinFileCopyParams)
                                ? "首包保留 Tag 为空:WebGL+Remote 下会被拦截(等同 None)。填入要留在首包的 Tag,如 buildin。"
                                : $"首包只保留带 [{_config.BuildinFileCopyParams}] Tag 的 bundle,其余从 CDN 加载。",
                            string.IsNullOrWhiteSpace(_config.BuildinFileCopyParams) ? MessageType.Warning : MessageType.Info);
                    }

                    _config.FileNameStyle = (EFileNameStyle)EditorGUILayout.Popup(
                        "文件名风格", (int)_config.FileNameStyle, FileNameStyleNames);

                    if (_config.BuildTarget == BuildTarget.WebGL
                        && _config.FileNameStyle == EFileNameStyle.BundleName)
                    {
                        EditorGUILayout.HelpBox(
                            "BundleName 给 bundle 固定文件名,内容变了 URL 不变,CDN 会用旧缓存冒充新文件、导致 CRC Mismatch。" +
                            "WebGL 部署请改用 BundleName_HashName 或 HashName(文件名带内容哈希)。",
                            MessageType.Warning);
                    }
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            GUILayout.Space(5);
        }

        #endregion

        #region 热更DLL设置

        private void DrawDllSettings()
        {
            _showDllSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showDllSettings,
                new GUIContent("热更DLL设置", "HybridCLR 热更程序集编译"));

            if (_showDllSettings)
            {
                EditorGUILayout.BeginVertical("HelpBox");
                {
                    _config.BuildHotFixDll = EditorGUILayout.ToggleLeft(
                        new GUIContent("构建前编译热更DLL", "执行 BuildDLLCommand.BuildAndCopyDlls"),
                        _config.BuildHotFixDll);
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            GUILayout.Space(5);
        }

        #endregion

        #region 打包Player设置

        private void DrawPlayerSettings()
        {
            _showPlayerSettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showPlayerSettings,
                new GUIContent("打包Player设置", "构建可执行程序"));

            if (_showPlayerSettings)
            {
                EditorGUILayout.BeginVertical("HelpBox");
                {
                    _config.BuildPlayer = EditorGUILayout.ToggleLeft(
                        new GUIContent("构建Player", "构建可执行程序(exe/apk/ipa)"),
                        _config.BuildPlayer);

                    if (_config.BuildPlayer)
                    {
                        EditorGUILayout.Space(3);

                        _playerPlatformIndex = EditorGUILayout.Popup("Player平台", _playerPlatformIndex, PlatformNames);
                        _config.PlayerPlatform = PlatformTargets[_playerPlatformIndex];

                        EditorGUILayout.BeginHorizontal();
                        _config.PlayerOutputPath = EditorGUILayout.TextField("输出路径", _config.PlayerOutputPath);
                        if (GUILayout.Button("浏览", GUILayout.Width(50)))
                        {
                            string selected = EditorUtility.SaveFilePanel("选择输出路径",
                                System.IO.Path.GetDirectoryName(_config.PlayerOutputPath),
                                System.IO.Path.GetFileName(_config.PlayerOutputPath), "");
                            if (!string.IsNullOrEmpty(selected))
                            {
                                _config.PlayerOutputPath = selected;
                            }
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            GUILayout.Space(5);
        }

        #endregion

        #region 部署设置

        private void DrawDeploySettings()
        {
            _showDeploySettings = EditorGUILayout.BeginFoldoutHeaderGroup(_showDeploySettings,
                new GUIContent("部署设置", "构建后经 Git Bash + ssh/scp 增量上传到远程服务器"));

            if (_showDeploySettings)
            {
                EditorGUILayout.BeginVertical("HelpBox");
                {
                    EditorGUILayout.HelpBox(
                        "默认连接参数取自 Fantasy 服务端部署脚本，仅作占位。请改成 AB / Player 实际部署服务器的地址、用户、私钥后再部署。",
                        MessageType.Info);

                    _deployHost = EditorGUILayout.TextField(
                        new GUIContent("服务器地址", "SSH 主机 IP 或 ~/.ssh/config 别名"), _deployHost);
                    _deployUser = EditorGUILayout.TextField(
                        new GUIContent("用户名", "SSH 用户名，默认 root"), _deployUser);
                    _deployPort = EditorGUILayout.TextField(
                        new GUIContent("SSH 端口", "默认 22"), _deployPort);

                    EditorGUILayout.BeginHorizontal();
                    _deployKey = EditorGUILayout.TextField(
                        new GUIContent("私钥路径", "SSH 私钥(.pem)文件路径；留空则用本机默认 ssh 配置"), _deployKey);
                    if (GUILayout.Button("浏览", GUILayout.Width(50)))
                    {
                        string selected = EditorUtility.OpenFilePanel("选择 SSH 私钥文件", "", "");
                        if (!string.IsNullOrEmpty(selected))
                        {
                            _deployKey = selected;
                        }
                    }

                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("上传地址（远程目录，可填写）", EditorStyles.boldLabel);
                    _deployAbRemoteDir = EditorGUILayout.TextField(
                        new GUIContent("AB 上传地址", "AssetBundle 远程目标目录"), _deployAbRemoteDir);
                    _deployPlayerRemoteDir = EditorGUILayout.TextField(
                        new GUIContent("Player 上传地址", "Player 远程目标目录"), _deployPlayerRemoteDir);
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            GUILayout.Space(5);
        }

        #endregion

        #region 操作按钮

        #region 发布前自检

        private void DrawPreCheckSection()
        {
            _showPreCheck = EditorGUILayout.BeginFoldoutHeaderGroup(_showPreCheck,
                new GUIContent("发布前自检", "把打包/发布反复踩的坑固化成检查项:本地(配置+产物)与线上(CDN 可达+版本一致)"));

            if (_showPreCheck)
            {
                EditorGUILayout.BeginVertical("HelpBox");
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("本地检查(配置+产物)", GUILayout.Height(26)))
                    {
                        _localCheckResults = ReleasePreCheck.RunLocalChecks(_config);
                    }

                    if (GUILayout.Button("线上检查(CDN)", GUILayout.Height(26)))
                    {
                        try
                        {
                            EditorUtility.DisplayProgressBar("发布前自检", "正在核对线上 CDN...", 0.5f);
                            _remoteCheckResults = ReleasePreCheck.RunRemoteChecks(_config);
                        }
                        finally
                        {
                            EditorUtility.ClearProgressBar();
                        }
                    }

                    EditorGUILayout.EndHorizontal();

                    DrawCheckResults("本地", _localCheckResults);
                    DrawCheckResults("线上", _remoteCheckResults);
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            GUILayout.Space(5);
        }

        private void DrawCheckResults(string group, List<ReleasePreCheck.CheckItem> results)
        {
            if (results == null)
                return;

            int fail = 0, warn = 0;
            foreach (var r in results)
            {
                if (r.Status == ReleasePreCheck.CheckStatus.Fail) fail++;
                else if (r.Status == ReleasePreCheck.CheckStatus.Warn) warn++;
            }

            EditorGUILayout.LabelField($"{group}检查:{results.Count} 项,失败 {fail},警告 {warn}", EditorStyles.boldLabel);

            foreach (var r in results)
            {
                var type = r.Status == ReleasePreCheck.CheckStatus.Fail ? MessageType.Error
                    : r.Status == ReleasePreCheck.CheckStatus.Warn ? MessageType.Warning
                    : MessageType.Info;
                string text = $"{r.Name}:{r.Message}";
                if (!string.IsNullOrEmpty(r.Fix))
                    text += $"\n→ 修复:{r.Fix}";
                EditorGUILayout.HelpBox(text, type);
            }
        }

        #endregion

        private void DrawActionButtons()
        {
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Space(5);

            // 主按钮行在
            EditorGUILayout.BeginHorizontal();
            {
                var abStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                };

                if (GUILayout.Button("构建 AssetBundle", abStyle, GUILayout.Height(35)))
                {
                    SaveSettings();
                    ExecuteBuild(buildPlayer: false);
                }

                if (GUILayout.Button("构建 Player", abStyle, GUILayout.Height(35)))
                {
                    SaveSettings();
                    ExecuteBuildPlayerOnly();
                }
            }
            EditorGUILayout.EndHorizontal();

            // 一键构建按钮
            var fullBuildStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.2f, 0.6f, 1f) },
            };

            if (GUILayout.Button("一键构建 (AB + Player)", fullBuildStyle, GUILayout.Height(38)))
            {
                SaveSettings();
                _config.BuildPlayer = true;
                ExecuteBuild(buildPlayer: true);
            }

            GUILayout.Space(5);

            // 一键部署按钮（构建 + 上传到远程）
            var deployStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.6f, 0.2f) },
            };

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("一键构建+部署 AB", deployStyle, GUILayout.Height(35)))
                {
                    SaveSettings();
                    ExecuteDeployAssetBundle();
                }

                if (GUILayout.Button("一键构建+部署 Player", deployStyle, GUILayout.Height(35)))
                {
                    SaveSettings();
                    ExecuteDeployPlayer();
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);
        }

        #endregion

        #region 构建日志

        private void DrawBuildLog()
        {
            _showBuildLog = EditorGUILayout.BeginFoldoutHeaderGroup(_showBuildLog,
                new GUIContent($"构建日志 ({_buildLogs.Count})", "构建过程的日志输出"));

            if (_showBuildLog)
            {
                EditorGUILayout.BeginVertical("HelpBox");
                {
                    if (GUILayout.Button("清空日志", GUILayout.Height(22)))
                    {
                        _buildLogs.Clear();
                    }

                    _logScrollPosition = EditorGUILayout.BeginScrollView(_logScrollPosition, GUILayout.Height(150));
                    {
                        foreach (var log in _buildLogs)
                        {
                            EditorGUILayout.SelectableLabel(log, EditorStyles.miniLabel,
                                GUILayout.Height(EditorStyles.miniLabel.CalcHeight(new GUIContent(log), position.width - 30)));
                        }
                    }
                    EditorGUILayout.EndScrollView();
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        #endregion

        #region 构建执行

        private void ExecuteBuild(bool buildPlayer)
        {
            _buildLogs.Clear();
            AddLog($"========== 开始构建 ==========");
            AddLog($"平台: {_config.BuildTarget} | 管线: {_config.BuildPipeline} | 最小包: {_config.MinimalPackage}");

            if (string.IsNullOrWhiteSpace(_config.PackageVersion))
            {
                _config.PackageVersion = BuildConfig.GetDefaultPackageVersion();
                AddLog($"版本号为空，自动生成: {_config.PackageVersion}");
            }

            try
            {
                // 注册日志回调
                Application.logMessageReceived += OnBuildLogReceived;

                if (buildPlayer)
                {
                    ReleaseTools.BuildWithConfig(_config, buildPlayer: true);
                }
                else
                {
                    // 仅构建AB，不走Player
                    var configCopy = CloneConfig(_config);
                    configCopy.BuildPlayer = false;
                    ReleaseTools.BuildWithConfig(configCopy, buildPlayer: false);
                }

                AddLog($"========== 构建完成 ==========");
            }
            catch (Exception e)
            {
                AddLog($"[错误] {e.Message}");
                Debug.LogException(e);
            }
            finally
            {
                Application.logMessageReceived -= OnBuildLogReceived;
            }

            // 自动滚动到底部并展开日志
            _showBuildLog = true;
            Repaint();
        }

        private void ExecuteBuildPlayerOnly()
        {
            _buildLogs.Clear();
            AddLog($"========== 仅构建 Player ==========");
            AddLog($"平台: {_config.PlayerPlatform} | 输出: {_config.PlayerOutputPath}");

            if (_config.PlayerPlatform == BuildTarget.WebGL && !CheckWebGLBuildinCatalogReady())
            {
                _showBuildLog = true;
                Repaint();
                return;
            }

            try
            {
                Application.logMessageReceived += OnBuildLogReceived;
                ReleaseTools.BuildImp(
                    BuildConfig.GetBuildTargetGroup(_config.PlayerPlatform),
                    _config.PlayerPlatform,
                    _config.PlayerOutputPath
                );
                AddLog($"========== Player 构建完成 ==========");
            }
            catch (Exception e)
            {
                AddLog($"[错误] {e.Message}");
                Debug.LogException(e);
            }
            finally
            {
                Application.logMessageReceived -= OnBuildLogReceived;
            }

            _showBuildLog = true;
            Repaint();
        }

        #endregion

        #region 部署执行

        private void ExecuteDeployAssetBundle()
        {
            _buildLogs.Clear();
            AddLog("========== 一键部署 AssetBundle ==========");

            if (string.IsNullOrWhiteSpace(_config.PackageVersion))
            {
                _config.PackageVersion = BuildConfig.GetDefaultPackageVersion();
                AddLog($"版本号为空，自动生成: {_config.PackageVersion}");
            }
            else if (_config.PackageVersion == EditorPrefs.GetString(LastDeployedAbVersionKey, ""))
            {
                // 版本号与上次部署相同:资源若有改动却未 bump,客户端会命中旧缓存 / 清单哈希不匹配。
                // 仅在重试上传同一份产物时才该沿用同版本,故此处要求显式确认,不静默复用。
                int choice = EditorUtility.DisplayDialogComplex(
                    "资源版本号确认",
                    $"当前资源版本号 [{_config.PackageVersion}] 与上次部署相同。\n\n" +
                    "若本次资源有改动,应先 bump 版本号——否则客户端可能命中旧缓存,或清单哈希不匹配导致加载失败。\n" +
                    "仅在重试上传同一份产物时才应沿用当前版本。",
                    "生成新版本号并继续", // 返回 0
                    "取消", // 返回 1
                    "仍用当前版本继续"); // 返回 2
                if (choice == 1)
                {
                    AddLog("已取消部署(资源版本号未确认)。");
                    _showBuildLog = true;
                    Repaint();
                    return;
                }

                if (choice == 0)
                {
                    _config.PackageVersion = BuildConfig.GetDefaultPackageVersion();
                    SaveSettings();
                    AddLog($"已生成新版本号: {_config.PackageVersion}");
                }
            }

            try
            {
                Application.logMessageReceived += OnBuildLogReceived;

                var cfg = CloneConfig(_config);
                cfg.BuildPlayer = false;
                ReleaseTools.BuildWithConfig(cfg, buildPlayer: false);
                AddLog("AB 构建完成，准备上传...");

                string localDir = ReleaseTools.GetAssetBundleOutputDirectory(cfg);
                RunDeployScript(localDir, _deployAbRemoteDir, "AB");

                // 记录本次部署版本号,供下次同版本号重复部署时提示确认。
                EditorPrefs.SetString(LastDeployedAbVersionKey, cfg.PackageVersion);
            }
            catch (Exception e)
            {
                AddLog($"[错误] {e.Message}");
                Debug.LogException(e);
            }
            finally
            {
                Application.logMessageReceived -= OnBuildLogReceived;
            }

            _showBuildLog = true;
            Repaint();
        }

        /// <summary>
        /// WebGL Player 构建前校验:StreamingAssets 内置目录 BuildinCatalog.bytes 必须存在。
        /// 缺失时 Player 会卡在内置文件系统初始化(读不到内置清单)而下载不到资源。
        /// 分开按钮流程下 Player 只打包当前 StreamingAssets、不重建 AB,顺序反了或漏打 AB 会静默出坏包,故此处拦截。
        /// </summary>
        private bool CheckWebGLBuildinCatalogReady()
        {
            // 文件名对应 YooAsset DefaultBuildinFileSystemDefine.BuildinCatalogBinaryFileName;
            // 该常量所在类为 internal 且在 YooAsset 程序集内,跨程序集不可引用,故此处用字面量。
            // GetStreamingAssetsRoot() 已含 yoo/package 子目录;包名固定 "DefaultPackage"(与 ReleaseTools 构建参数一致)。
            string catalogPath = System.IO.Path.Combine(
                AssetBundleBuilderHelper.GetStreamingAssetsRoot(), "DefaultPackage", "BuildinCatalog.bytes");
            if (System.IO.File.Exists(catalogPath))
            {
                return true;
            }

            string msg = "WebGL 内置目录 BuildinCatalog.bytes 缺失,Player 会因资源初始化失败而下载不到资源。\n" +
                         "请先执行『一键部署 AB』(『内置文件拷贝』需设为 ClearAndCopyAll)把内置目录拷进 StreamingAssets,再打 Player。\n\n" +
                         $"缺失路径: {catalogPath}";
            AddLog($"[错误] {msg}");
            EditorUtility.DisplayDialog("WebGL Player 构建中止", msg, "知道了");
            return false;
        }

        private void ExecuteDeployPlayer()
        {
            _buildLogs.Clear();
            AddLog("========== 一键部署 Player ==========");
            AddLog($"平台: {_config.PlayerPlatform} | 输出: {_config.PlayerOutputPath}");

            if (_config.PlayerPlatform == BuildTarget.WebGL && !CheckWebGLBuildinCatalogReady())
            {
                _showBuildLog = true;
                Repaint();
                return;
            }

            try
            {
                Application.logMessageReceived += OnBuildLogReceived;

                ReleaseTools.BuildImp(
                    BuildConfig.GetBuildTargetGroup(_config.PlayerPlatform),
                    _config.PlayerPlatform,
                    _config.PlayerOutputPath);
                AddLog("Player 构建完成，准备上传...");

                string localDir = _config.PlayerOutputPath;
                if (System.IO.File.Exists(localDir))
                {
                    localDir = System.IO.Path.GetDirectoryName(localDir);
                }

                RunDeployScript(localDir, _deployPlayerRemoteDir, "Player");
            }
            catch (Exception e)
            {
                AddLog($"[错误] {e.Message}");
                Debug.LogException(e);
            }
            finally
            {
                Application.logMessageReceived -= OnBuildLogReceived;
            }

            _showBuildLog = true;
            Repaint();
        }

        /// <summary>
        /// 调用 deploy/upload.sh（经 Git Bash），把本地产物目录增量上传到远程目录。
        /// 连接参数经环境变量注入子进程，私钥内容不进任何文件。
        /// </summary>
        private void RunDeployScript(string localDir, string remoteDir, string tag)
        {
            if (string.IsNullOrWhiteSpace(remoteDir))
            {
                AddLog($"[错误] {tag} 上传地址为空，已取消上传。");
                return;
            }

            if (string.IsNullOrWhiteSpace(_deployHost))
            {
                AddLog("[错误] 服务器地址为空，已取消上传。");
                return;
            }

            localDir = System.IO.Path.GetFullPath(localDir);
            if (!System.IO.Directory.Exists(localDir))
            {
                AddLog($"[错误] 本地产物目录不存在: {localDir}");
                return;
            }

            string bash = FindGitBash();
            if (bash == null)
            {
                AddLog("[错误] 未找到 Git Bash，请安装 Git for Windows: https://git-scm.com/download/win");
                return;
            }

            string scriptPath = System.IO.Path.GetFullPath(Application.dataPath + "/../deploy/upload.sh");
            if (!System.IO.File.Exists(scriptPath))
            {
                AddLog($"[错误] 上传脚本不存在: {scriptPath}");
                return;
            }

            string user = string.IsNullOrWhiteSpace(_deployUser) ? "root" : _deployUser;
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = bash,
                Arguments = $"-l \"{ToGitBashPath(scriptPath)}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
            };
            psi.EnvironmentVariables["LOCAL_DIR"] = ToGitBashPath(localDir);
            psi.EnvironmentVariables["REMOTE_DIR"] = remoteDir;
            psi.EnvironmentVariables["SERVER_HOST"] = _deployHost;
            psi.EnvironmentVariables["SERVER_USER"] = user;
            psi.EnvironmentVariables["SSH_PORT"] = string.IsNullOrWhiteSpace(_deployPort) ? "22" : _deployPort;
            psi.EnvironmentVariables["SSH_KEY"] = string.IsNullOrWhiteSpace(_deployKey) ? "" : ToGitBashPath(_deployKey);

            AddLog($"上传 {tag} → {user}@{_deployHost}:{remoteDir}");

            var buffer = new System.Collections.Concurrent.ConcurrentQueue<string>();
            using (var p = new System.Diagnostics.Process { StartInfo = psi })
            {
                p.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null) buffer.Enqueue(e.Data);
                };
                p.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null) buffer.Enqueue("[sh] " + e.Data);
                };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                p.WaitForExit();

                while (buffer.TryDequeue(out var line))
                {
                    AddLog(line);
                }

                AddLog(p.ExitCode == 0 ? $"==> {tag} 部署成功" : $"[错误] {tag} 部署失败 (exit={p.ExitCode})");
            }
        }

        private static string FindGitBash()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "git",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    if (p != null)
                    {
                        string output = p.StandardOutput.ReadToEnd();
                        p.WaitForExit();
                        foreach (var raw in output.Split('\n'))
                        {
                            string git = raw.Trim();
                            if (git.EndsWith("git.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                string gitRoot = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(git));
                                if (!string.IsNullOrEmpty(gitRoot))
                                {
                                    string bash = System.IO.Path.Combine(gitRoot, "bin", "bash.exe");
                                    if (System.IO.File.Exists(bash))
                                    {
                                        return bash;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // 忽略 where 调用异常，落到下面的候选路径
            }

            string[] candidates =
            {
                @"C:\Program Files\Git\bin\bash.exe",
                @"C:\Program Files (x86)\Git\bin\bash.exe",
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Programs\Git\bin\bash.exe",
            };
            foreach (var candidate in candidates)
            {
                if (System.IO.File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Windows 路径转 Git Bash 路径（D:\a\b → /d/a/b）；已是 /x/ 形式则原样返回。
        /// </summary>
        private static string ToGitBashPath(string winPath)
        {
            if (string.IsNullOrEmpty(winPath))
            {
                return winPath;
            }

            winPath = winPath.Replace('\\', '/');
            if (winPath.Length >= 2 && winPath[1] == ':')
            {
                char drive = char.ToLowerInvariant(winPath[0]);
                winPath = "/" + drive + winPath.Substring(2);
            }

            return winPath;
        }

        private void OnBuildLogReceived(string condition, string stackTrace, LogType type)
        {
            string prefix = type switch
            {
                LogType.Error => "[ERR]",
                LogType.Warning => "[WARN]",
                LogType.Assert => "[ASSERT]",
                _ => ""
            };

            if (!string.IsNullOrEmpty(prefix) || condition.StartsWith("[") || condition.Contains("构建") || condition.Contains("Build"))
            {
                AddLog($"{prefix}{condition}");
            }
        }

        private void AddLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            _buildLogs.Add($"[{timestamp}] {message}");
            _logScrollPosition = new Vector2(0, float.MaxValue);
        }

        #endregion

        #region 持久化

        private void LoadSettings()
        {
            _config = new BuildConfig();

            _platformIndex = EditorPrefs.GetInt("TEngine_BP_BuildTarget", -1);
            if (_platformIndex < 0 || _platformIndex >= PlatformTargets.Length)
            {
                _platformIndex = GetActivePlatformIndex();
            }

            _config.BuildTarget = PlatformTargets[_platformIndex];

            int pipelineIndex = EditorPrefs.GetInt("TEngine_BP_BuildPipeline", 0);
            _config.BuildPipeline = pipelineIndex == 1 ? EBuildPipeline.BuiltinBuildPipeline : EBuildPipeline.ScriptableBuildPipeline;

            _config.CompressOption = (ECompressOption)EditorPrefs.GetInt("TEngine_BP_CompressOption", 1);
            _config.EncryptionType = (EncryptionType)EditorPrefs.GetInt("TEngine_BP_EncryptionType", 0);

            _config.PackageVersion = EditorPrefs.GetString("TEngine_BP_PackageVersion", "");
            _config.OutputRoot = EditorPrefs.GetString("TEngine_BP_OutputRoot", "./Builds/");

            _config.MinimalPackage = EditorPrefs.GetBool("TEngine_BP_MinimalPackage", false);
            _config.RetainTags = EditorPrefs.GetString("TEngine_BP_RetainTags", "");

            _config.EnableSharePackRule = EditorPrefs.GetBool("TEngine_BP_EnableSharePack", true);
            _config.UseAssetDependencyDB = EditorPrefs.GetBool("TEngine_BP_UseDepDB", true);
            _config.ClearBuildCache = EditorPrefs.GetBool("TEngine_BP_ClearCache", false);
            _config.VerifyBuildingResult = EditorPrefs.GetBool("TEngine_BP_VerifyResult", true);
            // 默认 ClearAndCopyAll:WebGL 恒需 StreamingAssets 内置目录,None 会导致运行时资源初始化失败。
            _config.BuildinFileCopyOption = (EBuildinFileCopyOption)EditorPrefs.GetInt("TEngine_BP_CopyOption", (int)EBuildinFileCopyOption.ClearAndCopyAll);
            _config.BuildinFileCopyParams = EditorPrefs.GetString("TEngine_BP_CopyParams", "");
            // 默认 BundleName_HashName:文件名带内容哈希,内容变则 URL 变,规避 CDN 缓存导致的 CRC Mismatch。
            _config.FileNameStyle = (EFileNameStyle)EditorPrefs.GetInt("TEngine_BP_FileNameStyle", (int)EFileNameStyle.BundleName_HashName);

            _config.BuildHotFixDll = EditorPrefs.GetBool("TEngine_BP_BuildDll", true);

            _config.BuildPlayer = EditorPrefs.GetBool("TEngine_BP_BuildPlayer", false);

            _playerPlatformIndex = EditorPrefs.GetInt("TEngine_BP_PlayerPlatform", -1);
            if (_playerPlatformIndex < 0 || _playerPlatformIndex >= PlatformTargets.Length)
            {
                _playerPlatformIndex = GetActivePlatformIndex();
            }

            _config.PlayerPlatform = PlatformTargets[_playerPlatformIndex];

            _config.PlayerOutputPath = EditorPrefs.GetString("TEngine_BP_PlayerOutput",
                BuildConfig.GetDefaultPlayerOutputPath(_config.PlayerPlatform));

            _deployHost = EditorPrefs.GetString("TEngine_BP_DeployHost", "121.199.24.31");
            _deployUser = EditorPrefs.GetString("TEngine_BP_DeployUser", "root");
            _deployPort = EditorPrefs.GetString("TEngine_BP_DeployPort", "22");
            _deployKey = EditorPrefs.GetString("TEngine_BP_DeployKey", "/d/work/TEngine_block/Fantasy/蛙蛙.pem");
            _deployAbRemoteDir = EditorPrefs.GetString("TEngine_BP_DeployAbDir",
                "/workspace/lulukeji/tarot-block/cdn/tarot-block/WebGL");
            _deployPlayerRemoteDir = EditorPrefs.GetString("TEngine_BP_DeployPlayerDir",
                "/workspace/lulukeji/tarot-block/WebGL");
        }

        private void SaveSettings()
        {
            EditorPrefs.SetInt("TEngine_BP_BuildTarget", _platformIndex);
            EditorPrefs.SetInt("TEngine_BP_BuildPipeline", _config.BuildPipeline == EBuildPipeline.BuiltinBuildPipeline ? 1 : 0);
            EditorPrefs.SetInt("TEngine_BP_CompressOption", (int)_config.CompressOption);
            EditorPrefs.SetInt("TEngine_BP_EncryptionType", (int)_config.EncryptionType);
            EditorPrefs.SetString("TEngine_BP_PackageVersion", _config.PackageVersion);
            EditorPrefs.SetString("TEngine_BP_OutputRoot", _config.OutputRoot);
            EditorPrefs.SetBool("TEngine_BP_MinimalPackage", _config.MinimalPackage);
            EditorPrefs.SetString("TEngine_BP_RetainTags", _config.RetainTags);
            EditorPrefs.SetBool("TEngine_BP_EnableSharePack", _config.EnableSharePackRule);
            EditorPrefs.SetBool("TEngine_BP_UseDepDB", _config.UseAssetDependencyDB);
            EditorPrefs.SetBool("TEngine_BP_ClearCache", _config.ClearBuildCache);
            EditorPrefs.SetBool("TEngine_BP_VerifyResult", _config.VerifyBuildingResult);
            EditorPrefs.SetInt("TEngine_BP_CopyOption", (int)_config.BuildinFileCopyOption);
            EditorPrefs.SetString("TEngine_BP_CopyParams", _config.BuildinFileCopyParams ?? "");
            EditorPrefs.SetInt("TEngine_BP_FileNameStyle", (int)_config.FileNameStyle);
            EditorPrefs.SetBool("TEngine_BP_BuildDll", _config.BuildHotFixDll);
            EditorPrefs.SetBool("TEngine_BP_BuildPlayer", _config.BuildPlayer);
            EditorPrefs.SetInt("TEngine_BP_PlayerPlatform", _playerPlatformIndex);
            EditorPrefs.SetString("TEngine_BP_PlayerOutput", _config.PlayerOutputPath);

            EditorPrefs.SetString("TEngine_BP_DeployHost", _deployHost ?? "");
            EditorPrefs.SetString("TEngine_BP_DeployUser", _deployUser ?? "");
            EditorPrefs.SetString("TEngine_BP_DeployPort", _deployPort ?? "");
            EditorPrefs.SetString("TEngine_BP_DeployKey", _deployKey ?? "");
            EditorPrefs.SetString("TEngine_BP_DeployAbDir", _deployAbRemoteDir ?? "");
            EditorPrefs.SetString("TEngine_BP_DeployPlayerDir", _deployPlayerRemoteDir ?? "");
        }

        private int GetActivePlatformIndex()
        {
            BuildTarget active = EditorUserBuildSettings.activeBuildTarget;
            for (int i = 0; i < PlatformTargets.Length; i++)
            {
                if (PlatformTargets[i] == active)
                    return i;
            }

            return 0;
        }

        #endregion

        #region 工具方法

        private static string PathGetRelative(string relativeTo, string path)
        {
            try
            {
                var uri = new Uri(relativeTo + "/");
                var rel = Uri.UnescapeDataString(uri.MakeRelativeUri(new Uri(path)).ToString());
                return rel.Replace('/', '\\');
            }
            catch
            {
                return "";
            }
        }

        private static BuildConfig CloneConfig(BuildConfig source)
        {
            return new BuildConfig
            {
                BuildTarget = source.BuildTarget,
                BuildPipeline = source.BuildPipeline,
                CompressOption = source.CompressOption,
                EncryptionType = source.EncryptionType,
                PackageVersion = source.PackageVersion,
                OutputRoot = source.OutputRoot,
                MinimalPackage = source.MinimalPackage,
                RetainTags = source.RetainTags,
                EnableSharePackRule = source.EnableSharePackRule,
                UseAssetDependencyDB = source.UseAssetDependencyDB,
                ClearBuildCache = source.ClearBuildCache,
                VerifyBuildingResult = source.VerifyBuildingResult,
                BuildinFileCopyOption = source.BuildinFileCopyOption,
                BuildinFileCopyParams = source.BuildinFileCopyParams,
                FileNameStyle = source.FileNameStyle,
                BuildHotFixDll = source.BuildHotFixDll,
                BuildPlayer = source.BuildPlayer,
                PlayerPlatform = source.PlayerPlatform,
                PlayerOutputPath = source.PlayerOutputPath,
            };
        }

        #endregion
    }
}