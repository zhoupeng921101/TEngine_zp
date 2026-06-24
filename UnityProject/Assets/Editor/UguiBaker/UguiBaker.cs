using Newtonsoft.Json;
using TEngine.Editor.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace EditorTools.Ugui
{
    /// <summary>
    /// 描述 JSON → UGUI 视觉占位节点树的后端。源无关：html→ugui 与 image→ugui 两条线共用。
    /// 规则：节点 text 非空 → 建 legacy Text(用可配置字体渲染中文)；否则 → 建 Image 占位。
    /// 不建任何控件——真按钮/滑条由人在 Unity 里手动转。
    /// </summary>
    public static class UguiBaker
    {
        /// <summary>把描述 JSON 还原到 parent 下，返回生成的根 GameObject。</summary>
        public static GameObject Bake(string json, RectTransform parent)
        {
            var root = JsonConvert.DeserializeObject<UIDataNode>(json);
            if (root == null) throw new System.Exception("JSON 解析为空，检查格式。");
            if (parent == null) throw new System.Exception("parent(Canvas) 为空。");

            // 整棵树共用一份字体，避免每个文本节点重复查资源
            var font = ScriptGeneratorSetting.GetDefaultUIFont();
            var go = BuildNode(root, parent, 0, 0, font);
            Undo.RegisterCreatedObjectUndo(go, "Bake UGUI");
            EditorUtility.SetDirty(go);
            return go;
        }

        static GameObject BuildNode(UIDataNode n, RectTransform parent, int parentAbsX, int parentAbsY, Font font)
        {
            bool isText = !string.IsNullOrEmpty(n.text);
            string nodeName = string.IsNullOrEmpty(n.name) ? (isText ? "文本" : "图片") : n.name;

            var go = isText
                ? new GameObject(nodeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text))
                : new GameObject(nodeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(n.width, n.height);
            rt.anchoredPosition = new Vector2(n.x - parentAbsX, -(n.y - parentAbsY));

            if (isText)
                ApplyText(go.GetComponent<Text>(), n, font);
            else
                ApplyImage(go.GetComponent<Image>(), n);

            if (n.children != null)
                foreach (var c in n.children)
                    if (c != null) BuildNode(c, rt, n.x, n.y, font);

            return go;
        }

        static void ApplyText(Text t, UIDataNode n, Font font)
        {
            t.font = font;
            t.text = n.text ?? "";
            t.fontSize = n.fontSize > 0 ? n.fontSize : 24;
            t.color = ParseColor(n.fontColor, Color.white);
            t.alignment = MapAlign(n.textAlign);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
        }

        static void ApplyImage(Image img, UIDataNode n)
        {
            var col = ParseColor(n.color, new Color(1, 1, 1, 0));
            img.color = col;
            // 全透明占位不挡射线，避免遮住下层节点
            img.raycastTarget = col.a > 0.001f;
        }

        // ───────────────────────── 工具 ─────────────────────────

        static TextAnchor MapAlign(string a)
        {
            switch ((a ?? "").ToLowerInvariant())
            {
                case "left":
                case "start": return TextAnchor.MiddleLeft;
                case "right":
                case "end": return TextAnchor.MiddleRight;
                default: return TextAnchor.MiddleCenter;
            }
        }

        static Color ParseColor(string s, Color fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            return ColorUtility.TryParseHtmlString(s, out var c) ? c : fallback;
        }
    }
}
