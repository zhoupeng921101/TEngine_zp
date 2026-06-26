using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace GameLogic.EditorTools
{
    /// <summary>
    /// 批量把指定目录下 prefab 内「引用了 UI 图集贴图」的 UnityEngine.UI.Image 组件改写为 GameLogic.ExImage，
    /// 并自动绑定其贴图所属的 SpriteAtlas。改写仅替换 m_Script 并补 spriteAtlas 字段，Source Image 与其余字段/引用全部保留。
    ///
    /// 判定来源：Assets/AssetArt/Atlas/ 下所有 .spriteatlasv2 的 packables（贴图 guid → 所属图集映射）。
    /// 只有当 Image 的 m_Sprite 贴图 guid 命中映射时才改写；一张贴图被多图集打包时记冲突日志并跳过。
    ///
    /// Scan 与 Apply 各自独立 LoadPrefabContents、各自重新判定每个组件——不跨两次加载传递组件身份。
    /// 原因：LoadPrefabContents 出来的对象无稳定 localFileId（TryGetGUIDAndLocalFileIdentifier 返回 false/0），
    /// InstanceID 又随加载变动，跨加载按 id 重定位组件必失败。Dry-Run 列表仅供用户预览，Apply 以当前 prefab 现状重判。
    /// </summary>
    public class ExImageConverterWindow : EditorWindow
    {
        private const string AtlasDir = "Assets/AssetArt/Atlas";

        private DefaultAsset _scanFolder;
        private string _scanPath = "Assets";

        private Vector2 _scroll;
        private string _summary = string.Empty;

        // 贴图 guid -> 所属图集集合（>1 即冲突）。
        private Dictionary<string, HashSet<string>> _textureGuidToAtlasGuids;
        // 图集 guid -> SpriteAtlas 资源。
        private Dictionary<string, SpriteAtlas> _atlasByGuid;

        private readonly List<PreviewItem> _pending = new List<PreviewItem>();
        private readonly List<string> _skips = new List<string>();

        /// <summary>一条预览记录：仅用于 Dry-Run 展示，不携带跨加载的组件句柄。</summary>
        private class PreviewItem
        {
            public string PrefabPath;
            public string NodePath;
            public string SpriteName;
            public string AtlasName;
        }

        /// <summary>单个组件的判定结果。</summary>
        private enum Decision
        {
            NotImage,       // 非纯 Image（含已是 ExImage）→ 静默跳过
            NoSprite,       // 无 sprite → 静默跳过
            NotAtlased,     // sprite 贴图不在任何图集 → 静默跳过
            Conflict,       // 贴图被多图集打包 → 记冲突、跳过
            FromOtherPrefab,// 来自其它 prefab 源（变体继承 / 嵌套实例）→ 记跳过，应在源 prefab 上转换
            AtlasMissing,   // 命中图集但资源加载失败 → 记跳过
            Convert         // 改写
        }

        [MenuItem("GameLogic/UI/Image 转 ExImage（批量绑图集）")]
        private static void Open()
        {
            var win = GetWindow<ExImageConverterWindow>("Image → ExImage");
            win.minSize = new Vector2(640, 420);
            win.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "把扫描目录下 prefab 中引用了 UI 图集贴图的 Image 改写为 ExImage 并绑定图集。\n" +
                "仅补图集引用，保留 Source Image，零视觉变化。先扫描预览，确认后再 Apply。",
                MessageType.Info);

            DrawFolderField();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("扫描（Dry-Run 预览）", GUILayout.Height(28)))
                {
                    Scan();
                }

                using (new EditorGUI.DisabledScope(_pending.Count == 0))
                {
                    GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
                    if (GUILayout.Button($"Apply（改写 {_pending.Count} 处）", GUILayout.Height(28)))
                    {
                        Apply();
                    }
                    GUI.backgroundColor = Color.white;
                }
            }

            if (!string.IsNullOrEmpty(_summary))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(_summary, EditorStyles.wordWrappedLabel);
            }

            DrawPreviewList();
        }

        private void DrawFolderField()
        {
            EditorGUILayout.LabelField("扫描根目录", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                var picked = (DefaultAsset)EditorGUILayout.ObjectField(_scanFolder, typeof(DefaultAsset), false);
                if (picked != _scanFolder)
                {
                    _scanFolder = picked;
                    if (picked != null)
                    {
                        var p = AssetDatabase.GetAssetPath(picked);
                        if (AssetDatabase.IsValidFolder(p))
                        {
                            _scanPath = p;
                        }
                    }
                }

                if (GUILayout.Button("选择文件夹", GUILayout.Width(90)))
                {
                    var abs = EditorUtility.OpenFolderPanel("选择扫描根目录", _scanPath, string.Empty);
                    if (!string.IsNullOrEmpty(abs))
                    {
                        var rel = AbsoluteToAssetsRelative(abs);
                        if (rel != null && AssetDatabase.IsValidFolder(rel))
                        {
                            _scanPath = rel;
                            _scanFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(rel);
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("无效目录", "请选择 Assets 下的文件夹。", "OK");
                        }
                    }
                }
            }

            EditorGUILayout.LabelField("当前路径", _scanPath);
        }

        private void DrawPreviewList()
        {
            if (_pending.Count == 0 && _skips.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"将改写 {_pending.Count} 处，跳过/冲突 {_skips.Count} 处", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            foreach (var c in _pending)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(Path.GetFileName(c.PrefabPath) + "  ·  " + c.NodePath, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"sprite: {c.SpriteName}    →  图集: {c.AtlasName}");
                    EditorGUILayout.LabelField(c.PrefabPath, EditorStyles.miniLabel);
                }
            }

            if (_skips.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("跳过 / 冲突明细", EditorStyles.boldLabel);
                foreach (var s in _skips)
                {
                    EditorGUILayout.LabelField("· " + s, EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------ 扫描（Dry-Run）

        private void Scan()
        {
            _pending.Clear();
            _skips.Clear();
            _summary = string.Empty;

            if (!AssetDatabase.IsValidFolder(_scanPath))
            {
                _summary = $"扫描目录无效：{_scanPath}";
                return;
            }

            BuildAtlasMap();
            if (_textureGuidToAtlasGuids.Count == 0)
            {
                _summary = $"在 {AtlasDir} 未解析到任何图集贴图，终止。";
                return;
            }

            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { _scanPath });
            int hitImages = 0;

            try
            {
                for (int i = 0; i < prefabGuids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                    EditorUtility.DisplayProgressBar("扫描 prefab", path, (float)i / Mathf.Max(1, prefabGuids.Length));
                    hitImages += ScanPrefab(path);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            _summary =
                $"扫描完成：{prefabGuids.Length} 个 prefab，命中图集 Image {hitImages} 处，" +
                $"待改写 {_pending.Count} 处，跳过/冲突 {_skips.Count} 处。";
            Debug.Log("[ExImageConverter] " + _summary);
        }

        /// <summary>只读扫描单个 prefab，填充预览/跳过列表，返回命中图集的 Image 数。</summary>
        private int ScanPrefab(string prefabPath)
        {
            int hit = 0;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                return 0;
            }

            try
            {
                foreach (var img in root.GetComponentsInChildren<Image>(true))
                {
                    var decision = Evaluate(img, out string atlasGuid, out SpriteAtlas atlas, out string note);
                    if (decision == Decision.NotImage || decision == Decision.NoSprite || decision == Decision.NotAtlased)
                    {
                        continue;
                    }

                    hit++;
                    string nodePath = GetNodePath(root.transform, img.transform);
                    string spriteName = img.sprite != null ? img.sprite.name : "<none>";

                    switch (decision)
                    {
                        case Decision.Convert:
                            _pending.Add(new PreviewItem
                            {
                                PrefabPath = prefabPath,
                                NodePath = nodePath,
                                SpriteName = spriteName,
                                AtlasName = atlas.name,
                            });
                            break;
                        case Decision.Conflict:
                            _skips.Add($"[冲突] {Path.GetFileName(prefabPath)} · {nodePath} · sprite={spriteName} · {note}");
                            Debug.LogWarning($"[ExImageConverter] 贴图被多图集打包，跳过：{prefabPath} · {nodePath} · {note}");
                            break;
                        case Decision.FromOtherPrefab:
                            _skips.Add($"[跳过] {Path.GetFileName(prefabPath)} · {nodePath} · 来自其它 prefab 源（变体继承 / 嵌套实例），应在源 prefab 上转换");
                            break;
                        case Decision.AtlasMissing:
                            _skips.Add($"[跳过] {Path.GetFileName(prefabPath)} · {nodePath} · {note}");
                            break;
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return hit;
        }

        // ------------------------------------------------------------------ 应用

        private void Apply()
        {
            if (!AssetDatabase.IsValidFolder(_scanPath))
            {
                return;
            }

            // 以最新图集映射为准重建，再按预览列表涉及的 prefab 逐个重判改写。
            BuildAtlasMap();
            var exScript = MonoScriptFor(typeof(ExImage));
            if (exScript == null)
            {
                _summary = "致命：找不到 ExImage 的 MonoScript，终止 Apply。";
                Debug.LogError("[ExImageConverter] " + _summary);
                return;
            }

            var prefabPaths = _pending.Select(p => p.PrefabPath).Distinct().ToList();
            int changed = 0;
            int prefabCount = 0;
            var failed = new List<string>();

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < prefabPaths.Count; i++)
                {
                    var prefabPath = prefabPaths[i];
                    EditorUtility.DisplayProgressBar("改写 prefab", prefabPath, (float)i / Mathf.Max(1, prefabPaths.Count));
                    changed += ApplyToPrefab(prefabPath, exScript, ref prefabCount, failed);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            _summary = $"Apply 完成：改写 {changed} 处，涉及 {prefabCount} 个 prefab，失败 {failed.Count} 处。";
            Debug.Log("[ExImageConverter] " + _summary);
            foreach (var f in failed)
            {
                Debug.LogWarning("[ExImageConverter] 失败：" + f);
            }

            // 改写后重扫，刷新预览（已转的不再出现，验证幂等）。
            Scan();
        }

        /// <summary>对单个 prefab 在一次加载内完成：重判每个组件 + 就地改写需转的，返回本 prefab 改写数。</summary>
        private int ApplyToPrefab(string prefabPath, MonoScript exScript, ref int prefabCount, List<string> failed)
        {
            int changed = 0;
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                failed.Add(prefabPath + "（加载失败）");
                return 0;
            }
            bool dirty = false;

            try
            {
                // 先收集需改写的目标 GameObject（改写会令 Image 句柄失效，故不在遍历中边改边引用旧句柄）。
                var targets = new List<KeyValuePair<GameObject, SpriteAtlas>>();
                foreach (var img in root.GetComponentsInChildren<Image>(true))
                {
                    var decision = Evaluate(img, out _, out SpriteAtlas atlas, out _);
                    if (decision == Decision.Convert)
                    {
                        targets.Add(new KeyValuePair<GameObject, SpriteAtlas>(img.gameObject, atlas));
                    }
                }

                foreach (var kv in targets)
                {
                    var go = kv.Key;
                    var img = go.GetComponent<Image>();
                    if (img == null || img is ExImage)
                    {
                        continue;
                    }
                    if (SwapToExImage(img, exScript, kv.Value))
                    {
                        changed++;
                        dirty = true;
                    }
                    else
                    {
                        failed.Add($"{prefabPath} · {GetNodePath(root.transform, go.transform)}（改写失败）");
                    }
                }

                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    prefabCount++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return changed;
        }

        /// <summary>
        /// 把一个 Image 组件原地改写为 ExImage：只换 m_Script 并补 spriteAtlas，所有继承字段与外部引用保留。
        /// 严禁删旧组件再 AddComponent（会断引用、丢字段）。
        ///
        /// 关键：改 m_Script 并 Apply 后，传入的 Image 托管引用会失效（底层 native 对象已被重解释为 ExImage，
        /// 旧 Image wrapper 变 null）。因此不能继续用 img 写 spriteAtlas，必须从其 GameObject 重新 GetComponent
        /// 取到变身后的 ExImage 组件再写。GameObject 引用在改写前后保持有效。
        /// </summary>
        private static bool SwapToExImage(Image img, MonoScript exScript, SpriteAtlas atlas)
        {
            var go = img.gameObject;

            var so = new SerializedObject(img);
            var scriptProp = so.FindProperty("m_Script");
            if (scriptProp == null)
            {
                return false;
            }
            scriptProp.objectReferenceValue = exScript;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 重新取变身后的 ExImage（img 引用此刻可能已失效）。
            var ex = go.GetComponent<ExImage>();
            if (ex == null)
            {
                return false;
            }

            var so2 = new SerializedObject(ex);
            var atlasProp = so2.FindProperty("spriteAtlas");
            if (atlasProp == null)
            {
                return false;
            }
            atlasProp.objectReferenceValue = atlas;
            so2.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(ex);
            return true;
        }

        /// <summary>对单个 Image 组件作判定（Scan 与 Apply 共用，保证两段一致）。</summary>
        private Decision Evaluate(Image img, out string atlasGuid, out SpriteAtlas atlas, out string note)
        {
            atlasGuid = null;
            atlas = null;
            note = null;

            // 幂等 + 只处理纯 UnityEngine.UI.Image。ExImage 是 Image 子类，GetType() 判等已涵盖。
            if (img.GetType() != typeof(Image))
            {
                return Decision.NotImage;
            }

            var sprite = img.sprite;
            if (sprite == null)
            {
                return Decision.NoSprite;
            }

            if (!TryGetTextureGuid(sprite, out string texGuid))
            {
                return Decision.NoSprite;
            }

            if (!_textureGuidToAtlasGuids.TryGetValue(texGuid, out var atlasGuids))
            {
                return Decision.NotAtlased;
            }

            if (atlasGuids.Count > 1)
            {
                note = "被多图集打包(" + string.Join(", ", atlasGuids.Select(AssetName)) + ")";
                return Decision.Conflict;
            }

            // 变体/嵌套安全：组件若来自其它 prefab 源（变体继承的 base 组件，或本 prefab 内嵌的 prefab 实例的组件），
            // GetCorrespondingObjectFromSource 非 null。这类组件的脚本类型不能作为 override 在本资产改写——Unity 不支持，
            // SaveAsPrefabAsset 会破坏覆盖关系/产生孤儿。统一跳过，留待该源 prefab 自身被扫描时在其资产上转换。
            if (PrefabUtility.GetCorrespondingObjectFromSource(img) != null)
            {
                return Decision.FromOtherPrefab;
            }

            atlasGuid = atlasGuids.First();
            _atlasByGuid.TryGetValue(atlasGuid, out atlas);
            if (atlas == null)
            {
                note = $"图集资源加载失败 guid={atlasGuid}";
                return Decision.AtlasMissing;
            }

            return Decision.Convert;
        }

        // ------------------------------------------------------------------ 图集映射

        private void BuildAtlasMap()
        {
            _textureGuidToAtlasGuids = new Dictionary<string, HashSet<string>>();
            _atlasByGuid = new Dictionary<string, SpriteAtlas>();

            if (!AssetDatabase.IsValidFolder(AtlasDir))
            {
                Debug.LogWarning($"[ExImageConverter] 图集目录不存在：{AtlasDir}");
                return;
            }

            var atlasGuids = AssetDatabase.FindAssets("t:SpriteAtlasAsset", new[] { AtlasDir })
                .Concat(AssetDatabase.FindAssets("t:SpriteAtlas", new[] { AtlasDir }))
                .Distinct();

            foreach (var atlasGuid in atlasGuids)
            {
                var atlasPath = AssetDatabase.GUIDToAssetPath(atlasGuid);
                if (string.IsNullOrEmpty(atlasPath) ||
                    (!atlasPath.EndsWith(".spriteatlasv2") && !atlasPath.EndsWith(".spriteatlas")))
                {
                    continue;
                }

                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                if (atlas == null)
                {
                    continue;
                }
                _atlasByGuid[atlasGuid] = atlas;

                foreach (var packable in CollectPackableTextureGuids(atlas))
                {
                    if (!_textureGuidToAtlasGuids.TryGetValue(packable, out var set))
                    {
                        set = new HashSet<string>();
                        _textureGuidToAtlasGuids[packable] = set;
                    }
                    set.Add(atlasGuid);
                }
            }

            Debug.Log($"[ExImageConverter] 解析图集 {_atlasByGuid.Count} 个，覆盖贴图 {_textureGuidToAtlasGuids.Count} 张。");
        }

        /// <summary>
        /// 取一个图集的全部 packable 贴图 guid。packable 可为单张贴图，也可为文件夹（递归其下贴图）。
        /// 用 SpriteAtlasExtensions.GetPackables 读 UnityEngine.Object，再回到 guid。
        /// </summary>
        private static IEnumerable<string> CollectPackableTextureGuids(SpriteAtlas atlas)
        {
            var result = new HashSet<string>();
            UnityEngine.Object[] packables;
            try
            {
                packables = SpriteAtlasExtensions.GetPackables(atlas);
            }
            catch
            {
                packables = null;
            }

            if (packables == null)
            {
                return result;
            }

            foreach (var obj in packables)
            {
                if (obj == null)
                {
                    continue;
                }
                var p = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(p))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(p))
                {
                    foreach (var tg in AssetDatabase.FindAssets("t:Texture2D", new[] { p }))
                    {
                        result.Add(tg);
                    }
                }
                else
                {
                    var g = AssetDatabase.AssetPathToGUID(p);
                    if (!string.IsNullOrEmpty(g))
                    {
                        result.Add(g);
                    }
                }
            }

            return result;
        }

        // ------------------------------------------------------------------ 工具

        /// <summary>从 sprite 资源回到其所属贴图（主资产）的 guid。</summary>
        private static bool TryGetTextureGuid(Sprite sprite, out string guid)
        {
            guid = null;
            var path = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            guid = AssetDatabase.AssetPathToGUID(path);
            return !string.IsNullOrEmpty(guid);
        }

        private static string GetNodePath(Transform root, Transform t)
        {
            var stack = new Stack<string>();
            while (t != null && t != root)
            {
                stack.Push(t.name);
                t = t.parent;
            }
            stack.Push(root.name);
            return string.Join("/", stack.ToArray());
        }

        private static string AssetName(string guid)
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(p) ? guid : Path.GetFileNameWithoutExtension(p);
        }

        private static MonoScript MonoScriptFor(System.Type type)
        {
            foreach (var ms in MonoImporter.GetAllRuntimeMonoScripts())
            {
                if (ms != null && ms.GetClass() == type)
                {
                    return ms;
                }
            }
            return null;
        }

        private static string AbsoluteToAssetsRelative(string abs)
        {
            abs = abs.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/'); // .../Assets
            if (abs == dataPath)
            {
                return "Assets";
            }
            if (abs.StartsWith(dataPath + "/"))
            {
                return "Assets" + abs.Substring(dataPath.Length);
            }
            return null;
        }
    }
}
