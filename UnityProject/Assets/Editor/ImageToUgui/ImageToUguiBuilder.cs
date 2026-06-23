using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using EditorTools.HtmlToUGUI;

namespace EditorTools.ImageToUgui
{
    /// <summary>
    /// 截图 → UGUI 视觉预制体管线的收尾装配。
    /// 输入 UIDataNode JSON（与 html-to-ugui 同一契约），复用 HtmlToUGUIBaker.Bake 搭节点，
    /// 再把 TMP 文本换成项目实际使用的 legacy Text + LegacyRuntime（渲染中文、与项目 UI 组件一致），
    /// 最后存成 .prefab 并在当前场景留一个链接实例。配合 unityMCP execute_code 调用。
    /// </summary>
    public static class ImageToUguiBuilder
    {
        const string PrefabDir = "Assets/AssetRaw/UI/Prefabs";

        /// <summary>JSON → 节点树 → 项目化文本 → 存 prefab，并在当前场景留链接实例。返回状态文本。</summary>
        public static string BuildPrefab(string json, string prefabName, int designW, int designH)
        {
            if (string.IsNullOrWhiteSpace(json)) return "ERROR: json 为空";
            if (string.IsNullOrWhiteSpace(prefabName)) return "ERROR: prefabName 为空";

            var canvas = NewStageCanvas(prefabName + "_Canvas", designW, designH, RenderMode.ScreenSpaceOverlay, null);
            var root = HtmlToUGUIBaker.Bake(json, canvas.transform as RectTransform);
            int converted = Projectize(root);

            EnsureFolder(PrefabDir);
            string path = PrefabDir + "/" + prefabName + ".prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);
            AssetDatabase.SaveAssets();

            bool ok = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            return $"prefab={path} saved={ok} textProjectized={converted} sceneInstance={root.name}";
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

        /// <summary>
        /// TMP_Text → legacy Text + LegacyRuntime。LegacyRuntime 走 OS 动态回退，能渲染中文，
        /// 且与项目现有 UI（m_text_* 全是 legacy Text + LegacyRuntime）一致。
        /// 跳过 TMP_InputField / TMP_Dropdown 内部的 TMP 文本——销毁会破坏控件。
        /// </summary>
        static int Projectize(GameObject root)
        {
            var legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int n = 0;
            var list = new List<TMP_Text>(root.GetComponentsInChildren<TMP_Text>(true));
            foreach (var tmp in list)
            {
                if (tmp == null) continue;
                if (IsInsideTmpControl(tmp.transform, root.transform)) continue;

                var go = tmp.gameObject;
                string txt = tmp.text;
                float fs = tmp.fontSize;
                Color col = tmp.color;
                var align = tmp.alignment;
                Object.DestroyImmediate(tmp);

                var t = go.GetComponent<Text>();
                if (t == null) t = go.AddComponent<Text>();
                t.font = legacy;
                t.text = txt;
                t.fontSize = Mathf.RoundToInt(fs <= 0 ? 24 : fs);
                t.color = col;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.alignment = MapAnchor(align);
                n++;
            }
            return n;
        }

        static bool IsInsideTmpControl(Transform t, Transform root)
        {
            for (var p = t; p != null; p = p.parent)
            {
                if (p.GetComponent<TMP_InputField>() != null || p.GetComponent<TMP_Dropdown>() != null) return true;
                if (p == root) break;
            }
            return false;
        }

        static TextAnchor MapAnchor(TextAlignmentOptions a)
        {
            switch (a)
            {
                case TextAlignmentOptions.Left:
                case TextAlignmentOptions.TopLeft:
                case TextAlignmentOptions.BottomLeft:
                case TextAlignmentOptions.MidlineLeft:
                    return TextAnchor.MiddleLeft;
                case TextAlignmentOptions.Right:
                case TextAlignmentOptions.TopRight:
                case TextAlignmentOptions.BottomRight:
                case TextAlignmentOptions.MidlineRight:
                    return TextAnchor.MiddleRight;
                default:
                    return TextAnchor.MiddleCenter;
            }
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
