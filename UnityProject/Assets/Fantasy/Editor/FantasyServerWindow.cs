using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FantasyClient.Editor
{
    /// <summary>
    /// Fantasy 服务端配置 &amp; 一键启动工具（菜单 Fantasy/服务端配置）。
    /// 直接从 Unity 编辑器里启动/停止本仓库的示例服务器，免去手敲 dotnet 命令。
    /// 配置（仓库路径/运行模式/框架/构建配置）持久化在 EditorPrefs。
    /// </summary>
    public class FantasyServerWindow : EditorWindow
    {
        private const string PrefRoot = "Fantasy.Server.Root";
        private const string PrefMode = "Fantasy.Server.Mode";
        private const string PrefFramework = "Fantasy.Server.Framework";
        private const string PrefConfig = "Fantasy.Server.Config";

        // 示例服务器固定的客户端网关端口（仅用于状态显示）。
        private const int KcpPort = 20000;
        private const int WebSocketPort = 20001;

        private string _root;
        private string _mode;
        private string _framework;
        private string _config;

        // 状态缓存：每秒刷新一次，OnGUI 只读缓存，避免每次重绘都做昂贵的进程/端口查询导致卡顿。
        private double _nextStatusRefresh;
        private int _statusProcCount;
        private bool _statusKcpOnline;
        private bool _statusWsOnline;

        [MenuItem("Fantasy/服务端配置", false, 0)]
        public static void Open()
        {
            var window = GetWindow<FantasyServerWindow>(false, "Fantasy 服务端配置");
            window.minSize = new Vector2(480, 320);
            window.Show();
        }

        [MenuItem("Fantasy/启动服务端", false, 1)]
        private static void MenuStart() => StartServer();

        [MenuItem("Fantasy/停止服务端", false, 2)]
        private static void MenuStop() => StopServer();

        private void OnEnable()
        {
            _root = EditorPrefs.GetString(PrefRoot, DefaultRoot());
            _mode = EditorPrefs.GetString(PrefMode, "Develop");
            _framework = EditorPrefs.GetString(PrefFramework, "net9.0");
            _config = EditorPrefs.GetString(PrefConfig, "Debug");
            RefreshStatus();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("服务端配置", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                _root = EditorGUILayout.TextField("Fantasy 仓库根目录", _root);
                if (GUILayout.Button("浏览", GUILayout.Width(48)))
                {
                    var picked = EditorUtility.OpenFolderPanel("选择 Fantasy 仓库根目录", _root, string.Empty);
                    if (!string.IsNullOrEmpty(picked))
                    {
                        _root = picked;
                        GUI.changed = true;
                    }
                }
            }

            _mode = EditorGUILayout.TextField("运行模式 (--m)", _mode);
            _framework = EditorGUILayout.TextField("目标框架 (--framework)", _framework);
            _config = EditorGUILayout.TextField("构建配置 (-c)", _config);
            if (EditorGUI.EndChangeCheck())
            {
                Save();
            }

            var csproj = MainCsproj(_root);
            var ok = File.Exists(csproj);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                ok ? "服务端项目：" + csproj
                   : "找不到服务端项目：\n" + csproj + "\n请检查“Fantasy 仓库根目录”是否正确。",
                ok ? MessageType.None : MessageType.Error);

            EditorGUILayout.LabelField("启动命令预览", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(BuildCommand(_root, _config, _framework, _mode),
                EditorStyles.textArea, GUILayout.Height(64), GUILayout.ExpandWidth(true));

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!ok))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("启动服务端", GUILayout.Height(30)))
                {
                    Save();
                    StartServer();
                }

                if (GUILayout.Button("停止服务端", GUILayout.Height(30)))
                {
                    StopServer();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开 Fantasy.config"))
                {
                    OpenPath(ConfigFile(_root));
                }

                if (GUILayout.Button("打开服务端目录"))
                {
                    OpenPath(ok ? Path.GetDirectoryName(csproj) : _root);
                }
            }

            EditorGUILayout.Space();
            DrawStatus();
        }

        private void Update()
        {
            // 每秒重算一次状态并刷新窗口；OnGUI 本身只读缓存，所以重绘很轻。
            if (EditorApplication.timeSinceStartup < _nextStatusRefresh)
            {
                return;
            }

            _nextStatusRefresh = EditorApplication.timeSinceStartup + 1.0;
            RefreshStatus();
            Repaint();
        }

        // 昂贵查询只在这里、每秒一次地做；结果存字段供 OnGUI 读取。
        private void RefreshStatus()
        {
            _statusProcCount = RunningServerCount();
            _statusKcpOnline = IsUdpListening(KcpPort);
            _statusWsOnline = IsTcpListening(WebSocketPort);
        }

        private void DrawStatus()
        {
            EditorGUILayout.LabelField("运行状态", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("服务端进程 (Main)", _statusProcCount > 0 ? $"运行中 ×{_statusProcCount}" : "未运行");
            EditorGUILayout.LabelField($"Gate KCP {KcpPort}", _statusKcpOnline ? "在线" : "离线");
            EditorGUILayout.LabelField($"Gate WebSocket {WebSocketPort}", _statusWsOnline ? "在线" : "离线");
        }

        // 轻量计数：只用进程名，不碰昂贵的 MainModule（按路径精确过滤只在“停止”时才需要）。
        private static int RunningServerCount()
        {
            try
            {
                var procs = Process.GetProcessesByName("Main");
                var count = procs.Length;
                foreach (var p in procs)
                {
                    p.Dispose();
                }

                return count;
            }
            catch
            {
                return 0;
            }
        }

        private void Save()
        {
            EditorPrefs.SetString(PrefRoot, _root);
            EditorPrefs.SetString(PrefMode, _mode);
            EditorPrefs.SetString(PrefFramework, _framework);
            EditorPrefs.SetString(PrefConfig, _config);
        }

        // ---- 静态核心：菜单项与窗口按钮共用，统一从 EditorPrefs 读取 ----

        private static void StartServer()
        {
            var root = EditorPrefs.GetString(PrefRoot, DefaultRoot());
            var mode = EditorPrefs.GetString(PrefMode, "Develop");
            var framework = EditorPrefs.GetString(PrefFramework, "net9.0");
            var config = EditorPrefs.GetString(PrefConfig, "Debug");
            var csproj = MainCsproj(root);

            if (!File.Exists(csproj))
            {
                Debug.LogError("[Fantasy] 找不到服务端项目，无法启动：" + csproj);
                return;
            }

            try
            {
                ProcessStartInfo psi;
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    // cmd /k：开一个新控制台窗口跑服务器并保持打开，方便看实时日志/崩溃信息。
                    psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/k dotnet " + DotnetArgs(csproj, config, framework, mode),
                        WorkingDirectory = root,
                        UseShellExecute = true,
                    };
                }
                else
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = DotnetArgs(csproj, config, framework, mode),
                        WorkingDirectory = root,
                        UseShellExecute = true,
                    };
                }

                Process.Start(psi);
                Debug.Log("[Fantasy] 已请求启动服务端（在新终端窗口查看日志）：\n"
                          + BuildCommand(root, config, framework, mode));
            }
            catch (Exception e)
            {
                Debug.LogError("[Fantasy] 启动服务端失败：" + e.Message);
            }
        }

        private static void StopServer()
        {
            var root = EditorPrefs.GetString(PrefRoot, DefaultRoot());
            var killed = 0;
            foreach (var p in ServerProcesses(root))
            {
                try
                {
                    p.Kill();
                    p.WaitForExit(3000);
                    killed++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Fantasy] 停止服务端进程失败：" + e.Message);
                }
            }

            Debug.Log(killed > 0
                ? $"[Fantasy] 已停止 {killed} 个服务端进程。"
                : "[Fantasy] 没有检测到正在运行的服务端进程。");
        }

        private static string DotnetArgs(string csproj, string config, string framework, string mode)
            => $"run --project \"{csproj}\" -c {config} --framework {framework} -- --m {mode}";

        private static string BuildCommand(string root, string config, string framework, string mode)
            => "dotnet " + DotnetArgs(MainCsproj(root), config, framework, mode);

        private static string DefaultRoot()
        {
            // 默认取 UnityProject 同级目录的 Fantasy 仓库。
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            try
            {
                return Path.GetFullPath(Path.Combine(projectRoot, "..", "Fantasy"));
            }
            catch
            {
                return projectRoot;
            }
        }

        private static string MainCsproj(string root)
            => Path.Combine(root ?? string.Empty, "examples", "Server", "APP", "Main", "Main.csproj");

        private static string ConfigFile(string root)
            => Path.Combine(root ?? string.Empty, "examples", "Server", "APP", "Entity", "Fantasy.config");

        // 只挑能识别为本仓库服务端的 Main 进程（可执行路径在仓库根目录下）；路径读不到时也纳入（dev 下基本就是它）。
        private static Process[] ServerProcesses(string root)
        {
            Process[] all;
            try
            {
                all = Process.GetProcessesByName("Main");
            }
            catch
            {
                return Array.Empty<Process>();
            }

            var normRoot = string.IsNullOrEmpty(root) ? null : root.Replace('/', '\\');
            return all.Where(p =>
            {
                try
                {
                    var path = p.MainModule?.FileName?.Replace('/', '\\');
                    if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(normRoot))
                    {
                        return true;
                    }

                    return path.StartsWith(normRoot, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return true;
                }
            }).ToArray();
        }

        private static bool IsUdpListening(int port)
        {
            try
            {
                return IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveUdpListeners().Any(e => e.Port == port);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsTcpListening(int port)
        {
            try
            {
                return IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveTcpListeners().Any(e => e.Port == port);
            }
            catch
            {
                return false;
            }
        }

        private static void OpenPath(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || (!File.Exists(path) && !Directory.Exists(path)))
                {
                    Debug.LogWarning("[Fantasy] 路径不存在：" + path);
                    return;
                }

                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception e)
            {
                Debug.LogError("[Fantasy] 打开失败：" + e.Message);
            }
        }
    }
}
