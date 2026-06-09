using System.Collections.Generic;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EditorTools.HtmlToUGUI
{
    /// <summary>
    /// html-to-ugui 管线 Step 3：把烘焙器输出的 UIDataNode JSON 还原成 UGUI 节点树。
    /// 菜单：Tools/UI Architecture/HTML to UGUI Baker。
    /// 文本/按钮/输入框/下拉框走 TMP；滚动/滑条用内置 DefaultControls；开关 Label 换成 TMP。
    /// </summary>
    public class HtmlToUGUIBaker : EditorWindow
    {
        enum SourceMode { PasteJson, JsonFile }

        [SerializeField] Canvas _canvas;
        [SerializeField] SourceMode _mode = SourceMode.PasteJson;
        [SerializeField] string _json = "";
        [SerializeField] string _filePath = "";
        Vector2 _scroll;

        [MenuItem("Tools/UI Architecture/HTML to UGUI Baker")]
        static void Open()
        {
            var w = GetWindow<HtmlToUGUIBaker>("HTML→UGUI Baker");
            w.minSize = new Vector2(380, 420);
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "把 bake_html_to_json.py 输出的 JSON 粘进来，选目标 Canvas，点生成。\n" +
                "text/button/input/dropdown 用 TMP，外观用 Unity 内置 UI 皮肤。",
                MessageType.Info);

            _canvas = (Canvas)EditorGUILayout.ObjectField("目标 Canvas", _canvas, typeof(Canvas), true);
            if (_canvas == null)
                EditorGUILayout.HelpBox("未指定 Canvas：生成时会自动在场景里找一个，没有则新建。", MessageType.None);

            _mode = (SourceMode)EditorGUILayout.EnumPopup("来源", _mode);

            if (_mode == SourceMode.PasteJson)
            {
                EditorGUILayout.LabelField("JSON 文本");
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(180));
                _json = EditorGUILayout.TextArea(_json, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                _filePath = EditorGUILayout.TextField("JSON 文件", _filePath);
                if (GUILayout.Button("…", GUILayout.Width(28)))
                {
                    var p = EditorUtility.OpenFilePanel("选择 JSON", Application.dataPath, "json");
                    if (!string.IsNullOrEmpty(p)) _filePath = p;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling))
            {
                if (GUILayout.Button("执行烘焙生成", GUILayout.Height(36)))
                    DoBake();
            }
        }

        void DoBake()
        {
            string json = _json;
            if (_mode == SourceMode.JsonFile)
            {
                if (!System.IO.File.Exists(_filePath))
                {
                    EditorUtility.DisplayDialog("HTML→UGUI", "JSON 文件不存在：\n" + _filePath, "OK");
                    return;
                }
                json = System.IO.File.ReadAllText(_filePath);
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                EditorUtility.DisplayDialog("HTML→UGUI", "JSON 为空。", "OK");
                return;
            }

            Canvas canvas = _canvas != null ? _canvas : ResolveCanvas();
            try
            {
                var go = Bake(json, canvas.transform as RectTransform);
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
                Debug.Log($"[HtmlToUGUIBaker] 生成完成：{go.name}", go);
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("HTML→UGUI 失败", ex.Message, "OK");
                Debug.LogException(ex);
            }
        }

        // ───────────────────────── 核心：JSON → UGUI ─────────────────────────

        /// <summary>把 UIDataNode JSON 还原到 parent 下，返回生成的根 GameObject。可供窗口或自动化调用。</summary>
        public static GameObject Bake(string json, RectTransform parent)
        {
            var root = JsonConvert.DeserializeObject<UIDataNode>(json);
            if (root == null) throw new System.Exception("JSON 解析为空，检查格式。");
            if (parent == null) throw new System.Exception("parent(Canvas) 为空。");

            EnsureResources();
            var go = BuildNode(root, parent, 0, 0);
            Undo.RegisterCreatedObjectUndo(go, "Bake HTML to UGUI");
            EditorUtility.SetDirty(go);
            return go;
        }

        static GameObject BuildNode(UIDataNode n, RectTransform parent, int parentAbsX, int parentAbsY)
        {
            RectTransform holder;
            GameObject go = CreateByType(n, out holder);
            go.name = string.IsNullOrEmpty(n.name) ? n.type : n.name;

            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(n.width, n.height);
            rt.anchoredPosition = new Vector2(n.x - parentAbsX, -(n.y - parentAbsY));

            ApplyVisuals(go, n);

            if (holder == null) holder = rt;
            if (n.children != null)
                foreach (var c in n.children)
                    if (c != null) BuildNode(c, holder, n.x, n.y);

            return go;
        }

        static GameObject CreateByType(UIDataNode n, out RectTransform holder)
        {
            holder = null;
            switch (n.type)
            {
                case "image": return NewGraphic(n.name);
                case "text":  return NewText(n.name);
                case "button": return TMP_DefaultControls.CreateButton(_tmpRes);
                case "input": return TMP_DefaultControls.CreateInputField(_tmpRes);
                case "dropdown": return TMP_DefaultControls.CreateDropdown(_tmpRes);
                case "slider": return DefaultControls.CreateSlider(_res);
                case "toggle": return DefaultControls.CreateToggle(_res);
                case "scroll":
                {
                    var sv = DefaultControls.CreateScrollView(_res);
                    var sr = sv.GetComponent<ScrollRect>();
                    if (sr != null && sr.content != null) holder = sr.content;
                    return sv;
                }
                case "div":
                default: return NewGraphic(n.name); // div = 带 Image 的容器
            }
        }

        static void ApplyVisuals(GameObject go, UIDataNode n)
        {
            switch (n.type)
            {
                case "div":
                case "image":
                {
                    var img = go.GetComponent<Image>();
                    var col = ParseColor(n.color, new Color(1, 1, 1, 0));
                    img.color = col;
                    // 透明容器自动剔除射线，避免挡住下层交互
                    img.raycastTarget = col.a > 0.001f && n.type == "image";
                    break;
                }
                case "text":
                {
                    var t = go.GetComponent<TextMeshProUGUI>();
                    t.text = n.text ?? "";
                    if (n.fontSize > 0) t.fontSize = n.fontSize;
                    t.color = ParseColor(n.fontColor, Color.white);
                    t.alignment = MapAlign(n.textAlign);
                    t.enableWordWrapping = false;
                    t.overflowMode = TextOverflowModes.Overflow;
                    break;
                }
                case "button":
                {
                    var img = go.GetComponent<Image>();
                    if (img != null) img.color = ParseColor(n.color, new Color(0.85f, 0.85f, 0.85f, 1));
                    var label = go.GetComponentInChildren<TMP_Text>(true);
                    if (label != null) StyleLabel(label, n, Color.black);
                    break;
                }
                case "input":
                {
                    var img = go.GetComponent<Image>();
                    if (img != null) img.color = ParseColor(n.color, Color.white);
                    var inp = go.GetComponent<TMP_InputField>();
                    if (inp != null)
                    {
                        if (inp.placeholder is TMP_Text ph) StyleLabel(ph, n, new Color(0.5f, 0.5f, 0.5f, 1), n.text);
                        if (inp.textComponent != null && n.fontSize > 0) inp.textComponent.fontSize = n.fontSize;
                    }
                    break;
                }
                case "dropdown":
                {
                    var img = go.GetComponent<Image>();
                    if (img != null) img.color = ParseColor(n.color, Color.white);
                    var dd = go.GetComponent<TMP_Dropdown>();
                    if (dd != null)
                    {
                        dd.ClearOptions();
                        if (n.options != null && n.options.Count > 0) dd.AddOptions(n.options);
                        if (dd.captionText != null && n.fontSize > 0) dd.captionText.fontSize = n.fontSize;
                    }
                    break;
                }
                case "toggle":
                {
                    var tg = go.GetComponent<Toggle>();
                    if (tg != null) tg.isOn = n.isChecked;
                    // 内置 Toggle 的 Label 是 legacy Text，Unity6 无 legacy 字体会隐形 → 换成 TMP
                    var labelTr = go.transform.Find("Label");
                    if (labelTr != null)
                    {
                        var old = labelTr.GetComponent<Text>();
                        if (old != null) Object.DestroyImmediate(old);
                        var tmp = labelTr.gameObject.GetComponent<TextMeshProUGUI>();
                        if (tmp == null) tmp = labelTr.gameObject.AddComponent<TextMeshProUGUI>();
                        StyleLabel(tmp, n, Color.black);
                    }
                    break;
                }
                case "slider":
                {
                    var sl = go.GetComponent<Slider>();
                    if (sl != null)
                    {
                        sl.minValue = 0; sl.maxValue = 1;
                        sl.value = Mathf.Clamp01(n.value);
                        sl.direction = n.dir == "h" || string.IsNullOrEmpty(n.dir)
                            ? Slider.Direction.LeftToRight : Slider.Direction.BottomToTop;
                    }
                    break;
                }
                case "scroll":
                {
                    var img = go.GetComponent<Image>();
                    if (img != null) img.color = ParseColor(n.color, new Color(0.2f, 0.2f, 0.2f, 1));
                    var sr = go.GetComponent<ScrollRect>();
                    if (sr != null)
                    {
                        bool horizontal = n.dir == "h";
                        sr.horizontal = horizontal;
                        sr.vertical = !horizontal;
                    }
                    break;
                }
            }
        }

        static void StyleLabel(TMP_Text label, UIDataNode n, Color defColor, string overrideText = null)
        {
            label.text = overrideText ?? n.text ?? "";
            if (n.fontSize > 0) label.fontSize = n.fontSize;
            label.color = ParseColor(n.fontColor, defColor);
            label.alignment = MapAlign(n.textAlign);
        }

        // ───────────────────────── 工具 ─────────────────────────

        static GameObject NewGraphic(string name)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "image" : name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            return go;
        }

        static GameObject NewText(string name)
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? "text" : name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            return go;
        }

        static TextAlignmentOptions MapAlign(string a)
        {
            switch ((a ?? "").ToLowerInvariant())
            {
                case "left":
                case "start": return TextAlignmentOptions.Left;
                case "right":
                case "end": return TextAlignmentOptions.Right;
                default: return TextAlignmentOptions.Center;
            }
        }

        static Color ParseColor(string s, Color fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            return ColorUtility.TryParseHtmlString(s, out var c) ? c : fallback;
        }

        static Canvas ResolveCanvas()
        {
            var c = Object.FindFirstObjectByType<Canvas>();
            if (c != null) return c;
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
            }
            return canvas;
        }

        // 内置 UI 皮肤资源（legacy + TMP 两套 Resources 共用同一组 sprite）
        static bool _resReady;
        static DefaultControls.Resources _res;
        static TMP_DefaultControls.Resources _tmpRes;

        static void EnsureResources()
        {
            if (_resReady) return;
            Sprite S(string p) => AssetDatabase.GetBuiltinExtraResource<Sprite>(p);
            var std = S("UI/Skin/UISprite.psd");
            var bg = S("UI/Skin/Background.psd");
            var input = S("UI/Skin/InputFieldBackground.psd");
            var knob = S("UI/Skin/Knob.psd");
            var check = S("UI/Skin/Checkmark.psd");
            var ddArrow = S("UI/Skin/DropdownArrow.psd");
            var mask = S("UI/Skin/UIMask.psd");

            _res = new DefaultControls.Resources
            {
                standard = std, background = bg, inputField = input,
                knob = knob, checkmark = check, dropdown = ddArrow, mask = mask
            };
            _tmpRes = new TMP_DefaultControls.Resources
            {
                standard = std, background = bg, inputField = input,
                knob = knob, checkmark = check, dropdown = ddArrow, mask = mask
            };
            _resReady = true;
        }
    }

    /// <summary>对应 json-schema.md 的 UIDataNode；字段名与 JSON 完全一致，Newtonsoft 直接映射。</summary>
    [System.Serializable]
    public class UIDataNode
    {
        public string name;
        public string type;
        public string dir = "v";
        public float value = 0.5f;
        public bool isChecked;
        public List<string> options;
        public int x;
        public int y;
        public int width;
        public int height;
        public string color = "#FFFFFF00";
        public string fontColor = "#FFFFFF";
        public int fontSize = 24;
        public string textAlign = "center";
        public string text = "";
        public List<UIDataNode> children;
    }
}
