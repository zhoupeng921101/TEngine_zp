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
            return $"prefab={path} saved={ok} sceneInstance={root.name}";
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
