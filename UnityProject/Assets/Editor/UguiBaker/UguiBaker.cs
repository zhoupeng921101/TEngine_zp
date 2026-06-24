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
    ///
    /// 锚点策略：据"子节点框相对父节点框"的几何自动推断 RectTransform 锚点，使产物随屏幕尺寸自适应
    /// (贴边的留在边、近似居中的随中心、占满父框的随父框拉伸)，而非死锚左上角。
    /// 设计分辨率(authoring 坐标基准)下版面与"死锚左上"等价，换尺寸时各元素各就各位。
    /// 节点可填 anchor 字段显式指定预设锚点，跳过推断；根节点(无父框)固定全屏拉伸。
    /// </summary>
    public static class UguiBaker
    {
        // ───────────── 推断阈值(便于调) ─────────────

        /// <summary>子尺寸 ≥ 父尺寸该比例,且两侧边距都较小 → 该轴判为拉伸。</summary>
        const float StretchSizeRatio = 0.8f;

        /// <summary>判拉伸时,单侧边距相对父尺寸的上限比例(超过则不算"贴合父框",不拉伸)。</summary>
        const float StretchMarginRatio = 0.15f;

        /// <summary>两侧边距差 ≤ 父尺寸该比例 → 该轴判为居中。</summary>
        const float CenterToleranceRatio = 0.06f;

        /// <summary>把描述 JSON 还原到 parent 下，返回生成的根 GameObject。</summary>
        public static GameObject Bake(string json, RectTransform parent)
        {
            var root = JsonConvert.DeserializeObject<UIDataNode>(json);
            if (root == null) throw new System.Exception("JSON 解析为空，检查格式。");
            if (parent == null) throw new System.Exception("parent(Canvas) 为空。");

            // 整棵树共用一份字体，避免每个文本节点重复查资源
            var font = ScriptGeneratorSetting.GetDefaultUIFont();
            // 根节点无设计父框：用画布(parent)的矩形尺寸当父框,并强制全屏拉伸
            var go = BuildNode(root, parent, null, font, isRoot: true);
            Undo.RegisterCreatedObjectUndo(go, "Bake UGUI");
            EditorUtility.SetDirty(go);
            return go;
        }

        /// <summary>设计空间的轴向区间(原点左上/上沿)。父框为 null 表示该节点是根。</summary>
        struct Box
        {
            public float x, y, w, h;
            public Box(float x, float y, float w, float h) { this.x = x; this.y = y; this.w = w; this.h = h; }
        }

        static GameObject BuildNode(UIDataNode n, RectTransform parentRt, Box? parentBox, Font font, bool isRoot)
        {
            bool isText = !string.IsNullOrEmpty(n.text);
            string nodeName = string.IsNullOrEmpty(n.name) ? (isText ? "文本" : "图片") : n.name;

            var go = isText
                ? new GameObject(nodeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text))
                : new GameObject(nodeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parentRt, false);

            // 父框：根节点用画布矩形尺寸(设计原点视作 0,0)；非根用父节点的设计框
            Box pBox = parentBox ?? new Box(0, 0, parentRt.rect.width, parentRt.rect.height);
            var self = new Box(n.x, n.y, n.width, n.height);

            if (isRoot)
                ApplyFullStretch(rt);
            else
                ApplyAdaptiveRect(rt, self, pBox, n.anchor);

            if (isText)
                ApplyText(go.GetComponent<Text>(), n, font);
            else
                ApplyImage(go.GetComponent<Image>(), n);

            if (n.children != null)
                foreach (var c in n.children)
                    if (c != null) BuildNode(c, rt, self, font, isRoot: false);

            return go;
        }

        // ───────────────────────── 锚点 ─────────────────────────

        enum AxisMode { Min, Center, Max, Stretch }

        static void ApplyFullStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 据子框相对父框的几何设置自适应锚点。横纵轴独立判级：拉伸/居中/锚 Min/锚 Max。
        /// 设计分辨率下与"死锚左上 + 固定 anchoredPosition/sizeDelta"像素级等价。
        /// </summary>
        static void ApplyAdaptiveRect(RectTransform rt, Box self, Box parent, string anchorOverride)
        {
            AxisMode modeX, modeYUnity;

            // 显式覆盖优先；非法值/空回退到推断。覆盖给的已是 Unity 纵轴语义(top=Max,bottom=Min)。
            if (TryParseAnchor(anchorOverride, out modeX, out modeYUnity))
            {
                ApplyAxes(rt, self, parent, modeX, modeYUnity);
                return;
            }

            // 设计空间："上"边距 = 子顶 - 父顶；横轴 Min=左
            float marginLeft = self.x - parent.x;
            float marginRight = (parent.x + parent.w) - (self.x + self.w);
            float marginTop = self.y - parent.y;
            float marginBottom = (parent.y + parent.h) - (self.y + self.h);

            modeX = DecideAxis(self.w, parent.w, marginLeft, marginRight);
            // 纵轴先在设计空间(原点上,Min=上端)判级,再翻到 Unity 纵轴语义(Min=下端)
            var modeYDesign = DecideAxis(self.h, parent.h, marginTop, marginBottom);
            modeYUnity = FlipAxisMode(modeYDesign);

            ApplyAxes(rt, self, parent, modeX, modeYUnity);
        }

        /// <summary>对单轴判级：拉伸 / 居中 / 锚靠近的一端(minMargin 端)。</summary>
        static AxisMode DecideAxis(float size, float parentSize, float minMargin, float maxMargin)
        {
            if (parentSize <= 0.0001f) return AxisMode.Min;

            // 1) 拉伸：子尺寸占满父框大半,且两端都贴近父框边
            if (size >= StretchSizeRatio * parentSize
                && minMargin <= StretchMarginRatio * parentSize
                && maxMargin <= StretchMarginRatio * parentSize)
                return AxisMode.Stretch;

            // 2) 居中：两端边距近似相等
            if (Mathf.Abs(minMargin - maxMargin) <= CenterToleranceRatio * parentSize)
                return AxisMode.Center;

            // 3) 锚靠近的一端
            return minMargin <= maxMargin ? AxisMode.Min : AxisMode.Max;
        }

        /// <summary>
        /// 把横轴(modeX)与 Unity 纵轴(modeYUnity)落到 RectTransform。两轴模式均为 Unity 语义
        /// (横 Min=左, 纵 Min=下)；设计→Unity 的纵轴翻转已由调用方完成。
        /// 非拉伸轴用 anchor 单点 + pivot 对齐 + anchoredPosition/sizeDelta；拉伸轴用 anchor 0/1 + offset 保边距。
        /// </summary>
        static void ApplyAxes(RectTransform rt, Box self, Box parent, AxisMode modeX, AxisMode modeYUnity)
        {
            // 横轴：minMargin = 左边距
            float aMinX, aMaxX, pivotX, anchoredX, sizeX;
            ResolveAxis(modeX, self.x - parent.x, self.w, parent.w,
                out aMinX, out aMaxX, out pivotX, out anchoredX, out sizeX);

            // 纵轴：minMargin = 下边距(Unity anchor=0 端)
            float marginBottomUnity = (parent.y + parent.h) - (self.y + self.h);
            float aMinY, aMaxY, pivotY, anchoredY, sizeY;
            ResolveAxis(modeYUnity, marginBottomUnity, self.h, parent.h,
                out aMinY, out aMaxY, out pivotY, out anchoredY, out sizeY);

            rt.anchorMin = new Vector2(aMinX, aMinY);
            rt.anchorMax = new Vector2(aMaxX, aMaxY);
            rt.pivot = new Vector2(pivotX, pivotY);

            // 横轴落值
            if (modeX == AxisMode.Stretch)
            {
                // offsetMin.x = 左边距, offsetMax.x = -右边距
                float left = self.x - parent.x;
                float right = (parent.x + parent.w) - (self.x + self.w);
                var omin = rt.offsetMin; omin.x = left; rt.offsetMin = omin;
                var omax = rt.offsetMax; omax.x = -right; rt.offsetMax = omax;
            }
            else
            {
                var sd = rt.sizeDelta; sd.x = sizeX; rt.sizeDelta = sd;
                var ap = rt.anchoredPosition; ap.x = anchoredX; rt.anchoredPosition = ap;
            }

            // 纵轴落值
            if (modeYUnity == AxisMode.Stretch)
            {
                float top = self.y - parent.y;                                  // 设计上边距 = Unity offsetMax.y(取负)
                float bottom = (parent.y + parent.h) - (self.y + self.h);       // 设计下边距 = Unity offsetMin.y
                var omin = rt.offsetMin; omin.y = bottom; rt.offsetMin = omin;
                var omax = rt.offsetMax; omax.y = -top; rt.offsetMax = omax;
            }
            else
            {
                var sd = rt.sizeDelta; sd.y = sizeY; rt.sizeDelta = sd;
                var ap = rt.anchoredPosition; ap.y = anchoredY; rt.anchoredPosition = ap;
            }
        }

        /// <summary>
        /// 单轴求解。minMargin = 子框靠 anchor=0 一端的边距(Unity 语义：横=左、纵=下)。
        /// 输出锚点对、pivot、以及非拉伸轴的 anchoredPosition/sizeDelta(锚点处局部坐标)。
        /// </summary>
        static void ResolveAxis(AxisMode mode, float minMargin, float size, float parentSize,
            out float aMin, out float aMax, out float pivot, out float anchored, out float sizeOut)
        {
            sizeOut = size;
            switch (mode)
            {
                case AxisMode.Stretch:
                    aMin = 0f; aMax = 1f; pivot = 0.5f; anchored = 0f; sizeOut = size;
                    return;

                case AxisMode.Center:
                    aMin = aMax = 0.5f; pivot = 0.5f;
                    // 中心相对父中心的偏移：子中心 - 父中心
                    // 子中心(距 anchor=0 端) = minMargin + size/2；父中心 = parentSize/2
                    anchored = (minMargin + size * 0.5f) - parentSize * 0.5f;
                    return;

                case AxisMode.Max:
                    // 贴 anchor=1 端：pivot=1，anchoredPosition = -(该端边距)
                    aMin = aMax = 1f; pivot = 1f;
                    anchored = -((parentSize - minMargin) - size); // = -(maxMargin)
                    return;

                default: // Min：贴 anchor=0 端，pivot=0，anchoredPosition = minMargin
                    aMin = aMax = 0f; pivot = 0f;
                    anchored = minMargin;
                    return;
            }
        }

        /// <summary>设计纵轴模式 → Unity 纵轴模式(上下端对调；拉伸/居中不变)。</summary>
        static AxisMode FlipAxisMode(AxisMode designMode)
        {
            if (designMode == AxisMode.Min) return AxisMode.Max;  // 设计贴上 → Unity 贴上(anchor=1)
            if (designMode == AxisMode.Max) return AxisMode.Min;  // 设计贴下 → Unity 贴下(anchor=0)
            return designMode;
        }

        // ───────────────────────── 显式 anchor 覆盖 ─────────────────────────

        /// <summary>
        /// 把预设 anchor 字符串映射到横/纵轴模式。返回 false 表示空或非法(由调用方回退到推断)。
        /// Y 维度用 Unity 语义(top=贴上=Max, bottom=贴下=Min)，不再翻转。
        /// </summary>
        static bool TryParseAnchor(string anchor, out AxisMode modeX, out AxisMode modeY)
        {
            modeX = AxisMode.Center; modeY = AxisMode.Center;
            if (string.IsNullOrWhiteSpace(anchor)) return false;

            switch (anchor.Trim().ToLowerInvariant())
            {
                case "top-left": modeX = AxisMode.Min; modeY = AxisMode.Max; return true;
                case "top": modeX = AxisMode.Center; modeY = AxisMode.Max; return true;
                case "top-right": modeX = AxisMode.Max; modeY = AxisMode.Max; return true;
                case "left": modeX = AxisMode.Min; modeY = AxisMode.Center; return true;
                case "center": modeX = AxisMode.Center; modeY = AxisMode.Center; return true;
                case "right": modeX = AxisMode.Max; modeY = AxisMode.Center; return true;
                case "bottom-left": modeX = AxisMode.Min; modeY = AxisMode.Min; return true;
                case "bottom": modeX = AxisMode.Center; modeY = AxisMode.Min; return true;
                case "bottom-right": modeX = AxisMode.Max; modeY = AxisMode.Min; return true;
                case "stretch": modeX = AxisMode.Stretch; modeY = AxisMode.Stretch; return true;
                case "stretch-x": modeX = AxisMode.Stretch; modeY = AxisMode.Center; return true;
                case "stretch-y": modeX = AxisMode.Center; modeY = AxisMode.Stretch; return true;
                case "top-stretch": modeX = AxisMode.Stretch; modeY = AxisMode.Max; return true;
                case "bottom-stretch": modeX = AxisMode.Stretch; modeY = AxisMode.Min; return true;
                case "left-stretch": modeX = AxisMode.Min; modeY = AxisMode.Stretch; return true;
                case "right-stretch": modeX = AxisMode.Max; modeY = AxisMode.Stretch; return true;
                default: return false; // 非法 → 回退推断
            }
        }

        // ───────────────────────── 视觉 ─────────────────────────

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
            // 标注了切图(path+sprite 均非空)→ 直接引子图填图；否则维持占位色行为(向后兼容)
            if (!string.IsNullOrEmpty(n.path) && !string.IsNullOrEmpty(n.sprite))
            {
                // sprite 不含扩展名则补 .png；含扩展名按原样
                string file = System.IO.Path.HasExtension(n.sprite) ? n.sprite : n.sprite + ".png";
                string full = n.path.TrimEnd('/') + "/" + file;
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(full);
                if (sp != null)
                {
                    img.sprite = sp;
                    img.color = Color.white;                 // 不染色，显示原图
                    img.type = n.sliced ? Image.Type.Sliced : Image.Type.Simple;
                    img.raycastTarget = true;
                    return;
                }
                // 取不到(路径错 / 资源非 Sprite)→ 告警 + 回退占位色，不抛异常
                Debug.LogWarning($"[UguiBaker] 节点「{n.name}」填图失败，回退占位色：未找到 Sprite at {full}");
            }

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
