using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EditorTools.FontReplacer
{
    /// <summary>
    /// 一键替换字体工具：批量把指定文件夹下所有 prefab 内的 UGUI Text 组件 font 替换为目标字体。
    /// 只改 UnityEngine.UI.Text，不动 TMP(TMP_Text/TextMeshProUGUI)。
    /// 菜单：Tools/字体批量替换。
    ///
    /// 替换核心是可被 EditMode 直调的静态方法 <see cref="Scan"/> 与 <see cref="Replace"/>，
    /// 窗口只是 IMGUI 薄壳。仅编辑器程序集(不打包、不热更)。
    /// 写盘走 PrefabUtility.LoadPrefabContents + SaveAsPrefabAsset + UnloadPrefabContents，
    /// 在独立 prefab 内容副本上改后保存，避免直接改 asset 实例的脏状态问题。
    /// </summary>
    public class FontReplacerWindow : EditorWindow
    {
        [SerializeField] private Font _targetFont;
        [SerializeField] private DefaultAsset _targetFolder;
        private Vector2 _logScroll;
        private string _log = "";

        [MenuItem("Tools/字体批量替换")]
        private static void Open()
        {
            var w = GetWindow<FontReplacerWindow>("字体批量替换");
            w.minSize = new Vector2(420, 460);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "批量替换指定文件夹下所有 prefab 内 UGUI Text 的字体。\n仅改 UnityEngine.UI.Text，TMP 文本一律跳过。",
                MessageType.Info);

            EditorGUILayout.Space();
            _targetFont = (Font)EditorGUILayout.ObjectField("目标字体 (Font)", _targetFont, typeof(Font), false);
            _targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("目标文件夹", _targetFolder, typeof(DefaultAsset), false);

            string folderPath = GetFolderAssetPath();
            if (_targetFolder != null && string.IsNullOrEmpty(folderPath))
            {
                EditorGUILayout.HelpBox("选择的对象不是 Assets 下的文件夹，请重新选择一个文件夹。", MessageType.Warning);
            }
            else if (!string.IsNullOrEmpty(folderPath))
            {
                EditorGUILayout.LabelField("扫描范围", folderPath);
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("扫描预览", GUILayout.Height(28)))
                {
                    DoScan(folderPath);
                }

                using (new EditorGUI.DisabledScope(_targetFont == null))
                {
                    if (GUILayout.Button("一键替换", GUILayout.Height(28)))
                    {
                        DoReplace(folderPath);
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("日志", EditorStyles.boldLabel);
            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private string GetFolderAssetPath()
        {
            if (_targetFolder == null) return null;
            string path = AssetDatabase.GetAssetPath(_targetFolder);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path)) return null;
            return path;
        }

        private void DoScan(string folderPath)
        {
            if (!ValidateFolder(folderPath)) return;

            var report = Scan(folderPath, _targetFont);
            var sb = new StringBuilder();
            sb.AppendLine($"[扫描预览] 范围：{folderPath}");
            sb.AppendLine($"共扫描 {report.PrefabCount} 个 prefab，发现 {report.TextCount} 个 UGUI Text。");
            if (_targetFont != null)
                sb.AppendLine($"其中已是目标字体、无需替换的 Text：{report.AlreadyTargetCount} 个。");
            else
                sb.AppendLine("(未指定目标字体，仅统计 Text 数量；替换需先指定目标字体。)");
            sb.AppendLine();
            sb.AppendLine("受影响 prefab 列表：");
            foreach (var item in report.AffectedPrefabs)
                sb.AppendLine($"  - {item.AssetPath}  (Text: {item.TextCount})");
            if (report.AffectedPrefabs.Count == 0)
                sb.AppendLine("  (无含 UGUI Text 的 prefab)");

            _log = sb.ToString();
            Debug.Log(_log);
            _logScroll = Vector2.zero;
        }

        private void DoReplace(string folderPath)
        {
            if (_targetFont == null)
            {
                EditorUtility.DisplayDialog("无法替换", "请先指定目标字体 (Font)。", "确定");
                return;
            }
            if (!ValidateFolder(folderPath)) return;

            if (!EditorUtility.DisplayDialog("确认替换",
                    $"将把 {folderPath} 下所有 prefab 的 UGUI Text 字体替换为：{_targetFont.name}\n\n此操作会写盘修改 prefab，确定继续？",
                    "替换", "取消"))
            {
                return;
            }

            var report = Replace(folderPath, _targetFont);
            var sb = new StringBuilder();
            sb.AppendLine($"[一键替换] 范围：{folderPath}");
            sb.AppendLine($"目标字体：{_targetFont.name}");
            sb.AppendLine($"共扫描 {report.PrefabCount} 个 prefab，替换 {report.ReplacedTextCount} 个 Text，跳过 {report.SkippedTextCount} 个(已是目标字体)。");
            sb.AppendLine($"实际写盘修改的 prefab：{report.ModifiedPrefabCount} 个。");
            sb.AppendLine();
            sb.AppendLine("修改的 prefab 列表：");
            foreach (var item in report.ModifiedPrefabs)
                sb.AppendLine($"  - {item.AssetPath}  (替换 Text: {item.TextCount})");
            if (report.ModifiedPrefabs.Count == 0)
                sb.AppendLine("  (无 prefab 被修改)");
            if (report.FailedPrefabs.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("加载失败、已跳过的 prefab：");
                foreach (var p in report.FailedPrefabs)
                    sb.AppendLine($"  - {p}");
            }

            _log = sb.ToString();
            Debug.Log(_log);
            _logScroll = Vector2.zero;
        }

        private bool ValidateFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                EditorUtility.DisplayDialog("无法操作", "请先选择一个 Assets 下的有效文件夹。", "确定");
                return false;
            }
            return true;
        }

        // ───────────────────────── 可直调核心 ─────────────────────────

        /// <summary>单个 prefab 的命中统计。</summary>
        public class PrefabHit
        {
            public string AssetPath;
            public int TextCount;
        }

        /// <summary>扫描报告(不落盘)。</summary>
        public class ScanReport
        {
            public int PrefabCount;                 // 扫描到的 prefab 总数
            public int TextCount;                   // UGUI Text 总数
            public int AlreadyTargetCount;          // 已是目标字体的 Text 数(targetFont 为空时恒为 0)
            public List<PrefabHit> AffectedPrefabs = new List<PrefabHit>(); // 含 Text 的 prefab
        }

        /// <summary>替换报告。</summary>
        public class ReplaceReport
        {
            public int PrefabCount;                 // 扫描到的 prefab 总数
            public int ReplacedTextCount;           // 实际改了 font 的 Text 数
            public int SkippedTextCount;            // 已是目标字体、跳过的 Text 数
            public int ModifiedPrefabCount;         // 实际写盘的 prefab 数
            public List<PrefabHit> ModifiedPrefabs = new List<PrefabHit>(); // 实际写盘的 prefab
            public List<string> FailedPrefabs = new List<string>();         // 加载失败、跳过的 prefab
        }

        /// <summary>
        /// 扫描 folderAssetPath(Assets 相对路径)下所有 prefab，统计 UGUI Text 数量与受影响 prefab，不落盘。
        /// targetFont 为空时仍可统计 Text 总数，但不计 AlreadyTargetCount。
        /// </summary>
        public static ScanReport Scan(string folderAssetPath, Font targetFont)
        {
            var report = new ScanReport();
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderAssetPath });
            report.PrefabCount = guids.Length;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                // 只读不改，直接在 asset 上 GetComponentsInChildren(含 inactive)。
                var texts = prefab.GetComponentsInChildren<Text>(true);
                if (texts.Length == 0) continue;

                int already = 0;
                if (targetFont != null)
                {
                    foreach (var t in texts)
                        if (t.font == targetFont) already++;
                }

                report.TextCount += texts.Length;
                report.AlreadyTargetCount += already;
                report.AffectedPrefabs.Add(new PrefabHit { AssetPath = path, TextCount = texts.Length });
            }

            return report;
        }

        /// <summary>
        /// 把 folderAssetPath 下所有 prefab 内 UGUI Text 的 font 替换为 targetFont 并写盘。
        /// 已是目标字体的 Text 跳过、不计入修改。targetFont 为 null 时直接返回空报告(调用方应先校验)。
        /// </summary>
        public static ReplaceReport Replace(string folderAssetPath, Font targetFont)
        {
            var report = new ReplaceReport();
            if (targetFont == null) return report;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderAssetPath });
            report.PrefabCount = guids.Length;

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    EditorUtility.DisplayProgressBar("字体批量替换",
                        $"处理 {path}", (float)(i + 1) / Mathf.Max(1, guids.Length));

                    // 在独立内容副本上改，避免污染 asset 实例的脏状态。
                    GameObject contents = PrefabUtility.LoadPrefabContents(path);
                    if (contents == null)
                    {
                        report.FailedPrefabs.Add(path);
                        continue;
                    }

                    try
                    {
                        var texts = contents.GetComponentsInChildren<Text>(true);
                        int replacedInThis = 0;
                        foreach (var t in texts)
                        {
                            if (t.font == targetFont)
                            {
                                report.SkippedTextCount++;
                                continue;
                            }
                            t.font = targetFont;
                            EditorUtility.SetDirty(t);
                            replacedInThis++;
                        }

                        if (replacedInThis > 0)
                        {
                            PrefabUtility.SaveAsPrefabAsset(contents, path);
                            report.ReplacedTextCount += replacedInThis;
                            report.ModifiedPrefabCount++;
                            report.ModifiedPrefabs.Add(new PrefabHit { AssetPath = path, TextCount = replacedInThis });
                        }
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(contents);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return report;
        }
    }
}
