using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using EditorTools.Ugui;

namespace EditorTools.ImageToUgui
{
    /// <summary>
    /// 截图/效果图 → UGUI 视觉预制体管线的收尾装配。
    /// 输入描述 JSON(与 html-to-ugui 同一契约)，复用 UguiBaker.Bake 搭节点树(文本/图片占位)，
    /// 存成 .prefab 并在当前场景留一个链接实例。配合 unityMCP execute_code 调用。
    /// 真按钮/滑条等控件由人在 Unity 里手动转，不在此阶段生成。
    /// </summary>
    public static class ImageToUguiBuilder
    {
        const string PrefabDir = "Assets/AssetRaw/UI/Prefabs";

        /// <summary>JSON → 节点树 → 存 prefab，并在当前场景留链接实例。返回状态文本。</summary>
        public static string BuildPrefab(string json, string prefabName, int designW, int designH)
        {
            if (string.IsNullOrWhiteSpace(json)) return "ERROR: json 为空";
            if (string.IsNullOrWhiteSpace(prefabName)) return "ERROR: prefabName 为空";

            var canvas = NewStageCanvas(prefabName + "_Canvas", designW, designH, RenderMode.ScreenSpaceOverlay, null);
            var root = UguiBaker.Bake(json, canvas.transform as RectTransform);

            EnsureFolder(PrefabDir);
            string path = PrefabDir + "/" + prefabName + ".prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
            AssetDatabase.SaveAssets();

            bool ok = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            return $"prefab={path} saved={ok} sceneInstance={root.name}" + LintGrouping(json);
        }

        // ───────────────────────── 结构 lint(分组兜底) ─────────────────────────

        /// <summary>
        /// 机械兜底,catch「该成组却平铺」的漏容器——不依赖作者是否记得先分组。
        /// 判据:某父节点的直接子里,存在 ≥3 个「尺寸相近 + 共线(同行或同列)」的对等项,且它们只是该父的一部分
        /// (父下还有别的子节点)→ 疑似漏了一个包裹容器,应把这簇对等项收进独立容器,而非与其它节点平铺在同一父下。
        /// 若这簇就是父的全部子节点,则父本身已是它们的容器,不告警(正确分组不误报)。
        /// 只提示不修改;分组的设计意图由作者定夺。返回追加到 BuildPrefab 状态串的提示文本(无提示则空串)。
        /// </summary>
        static string LintGrouping(string json)
        {
            UIDataNode root;
            try { root = JsonConvert.DeserializeObject<UIDataNode>(json); }
            catch { return ""; }
            if (root == null) return "";

            var hints = new List<string>();
            WalkForGrouping(root, hints);
            if (hints.Count == 0) return "";

            var sb = new StringBuilder();
            sb.Append("\n[分组提示] 疑似漏容器(该成组却平铺),按需包裹后重烤:");
            foreach (var h in hints)
            {
                sb.Append("\n  - ").Append(h);
                Debug.LogWarning("[ImageToUgui] " + h);
            }
            return sb.ToString();
        }

        static void WalkForGrouping(UIDataNode node, List<string> hints)
        {
            var kids = node.children;
            if (kids != null)
            {
                foreach (var cluster in FindPeerClusters(kids))
                {
                    // 严格子集(cluster < kids)才算漏容器;cluster 就是全部子 → 父已是容器
                    if (cluster.Count >= 3 && cluster.Count < kids.Count)
                    {
                        string names = string.Join("、", cluster.Select(c => "「" + (string.IsNullOrEmpty(c.name) ? "?" : c.name) + "」"));
                        string parent = string.IsNullOrEmpty(node.name) ? "根" : "「" + node.name + "」";
                        string axis = IsRow(cluster) ? "行" : "列";
                        hints.Add($"{parent} 下的 {names} 尺寸相近且排成一{axis},像一组对等项——建议收进一个独立容器,而非与其它节点平铺在 {parent} 下。");
                    }
                }
                foreach (var c in kids)
                    if (c != null) WalkForGrouping(c, hints);
            }
        }

        /// <summary>在一批兄弟节点里找「对等项簇」(尺寸相近 + 共线)。贪心:每个未归簇的种子取其 行/列 中更大的一簇。</summary>
        static List<List<UIDataNode>> FindPeerClusters(List<UIDataNode> kids)
        {
            var valid = kids.Where(k => k != null && k.width > 0 && k.height > 0).ToList();
            var used = new HashSet<UIDataNode>();
            var result = new List<List<UIDataNode>>();

            foreach (var seed in valid)
            {
                if (used.Contains(seed)) continue;
                var row = valid.Where(k => !used.Contains(k) && IsPeer(seed, k) && SameRow(seed, k)).ToList();
                var col = valid.Where(k => !used.Contains(k) && IsPeer(seed, k) && SameCol(seed, k)).ToList();
                var pick = row.Count >= col.Count ? row : col;
                if (pick.Count >= 3)
                {
                    foreach (var p in pick) used.Add(p);
                    result.Add(pick);
                }
            }
            return result;
        }

        // 尺寸相近:宽、高各自 min/max 比 ≥ 0.75
        static bool IsPeer(UIDataNode a, UIDataNode b)
        {
            float wr = (float)Mathf.Min(a.width, b.width) / Mathf.Max(a.width, b.width);
            float hr = (float)Mathf.Min(a.height, b.height) / Mathf.Max(a.height, b.height);
            return wr >= 0.75f && hr >= 0.75f;
        }

        // 同行:y 中心差 ≤ 较矮者高度的一半
        static bool SameRow(UIDataNode a, UIDataNode b)
        {
            float ca = a.y + a.height * 0.5f, cb = b.y + b.height * 0.5f;
            return Mathf.Abs(ca - cb) <= 0.5f * Mathf.Min(a.height, b.height);
        }

        // 同列:x 中心差 ≤ 较窄者宽度的一半
        static bool SameCol(UIDataNode a, UIDataNode b)
        {
            float ca = a.x + a.width * 0.5f, cb = b.x + b.width * 0.5f;
            return Mathf.Abs(ca - cb) <= 0.5f * Mathf.Min(a.width, b.width);
        }

        // 簇是行还是列:x 中心跨度 > y 中心跨度 → 行
        static bool IsRow(List<UIDataNode> c)
        {
            float xMin = c.Min(n => n.x + n.width * 0.5f), xMax = c.Max(n => n.x + n.width * 0.5f);
            float yMin = c.Min(n => n.y + n.height * 0.5f), yMax = c.Max(n => n.y + n.height * 0.5f);
            return (xMax - xMin) >= (yMax - yMin);
        }

        /// <summary>从已存盘 prefab 离屏渲一张 PNG 留证。自包含，不依赖场景状态。</summary>
        public static string Screenshot(string prefabPath, string outPng, int w, int h)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return "ERROR: prefab 不存在 " + prefabPath;

            var camGO = new GameObject("__ImageToUguiShotCam", typeof(Camera));
            var cam = camGO.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            camGO.transform.position = new Vector3(0, 0, -100);

            var canvas = NewStageCanvas("__ImageToUguiShotCanvas", w, h, RenderMode.ScreenSpaceCamera, cam);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.SetParent(canvas.transform, false);

            Canvas.ForceUpdateCanvases();

            var rt = new RenderTexture(w, h, 24);
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = prevActive;

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPng));
            System.IO.File.WriteAllBytes(outPng, tex.EncodeToPNG());
            long bytes = new System.IO.FileInfo(outPng).Length;

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(canvas.gameObject);
            Object.DestroyImmediate(camGO);
            AssetDatabase.Refresh();
            return $"png={outPng} bytes={bytes} size={w}x{h}";
        }

        // ───────────────────────── 内部 ─────────────────────────

        static Canvas NewStageCanvas(string name, int w, int h, RenderMode mode, Camera cam)
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);

            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = mode;
            if (mode == RenderMode.ScreenSpaceCamera && cam != null)
            {
                canvas.worldCamera = cam;
                canvas.planeDistance = 100;
            }
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(w, h);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        static void EnsureFolder(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            var parent = System.IO.Path.GetDirectoryName(dir).Replace('\\', '/');
            var leaf = System.IO.Path.GetFileName(dir);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
