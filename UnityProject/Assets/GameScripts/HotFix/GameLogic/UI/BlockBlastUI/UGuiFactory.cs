using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.BlockBlastUI
{
    /// <summary>
    /// 纯几何 UGUI 构建工厂。所有元素用纯色 Image / 内置字体 Text 代码创建，无 atlas 依赖。
    /// 统一坐标系：固定尺寸 1080×1920 原生面板（居中、pivot 中心、localScale=1）下，
    /// 元素一律 center 锚点 + center pivot，按"元素中心的设计坐标"（左上原点、Y 下正）定位。
    /// </summary>
    public static class UGuiFactory
    {
        private static Font _font;
        private static Font DefaultFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return _font;
            }
        }

        /// <summary>
        /// 固定尺寸内容面板：1080×1920 原生，锚点&amp;pivot 居中、localScale=1。
        /// 不随屏幕高度拉伸，保证 ScreenPointToLocalPoint 与 anchoredPosition 同坐标系。
        /// </summary>
        public static RectTransform CreateContentPanel(Transform parent, string name = "Content")
        {
            var rt = CreateNode(parent, name);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(BlockLayout.DesignWidth, BlockLayout.DesignHeight);
            rt.anchoredPosition = Vector2.zero;
            // 原生 1080×1920 设计坐标系，与参考分辨率一致，不缩放
            rt.localScale = Vector3.one;
            return rt;
        }

        /// <summary>空 RectTransform 子节点（容器，center 锚点）。</summary>
        public static RectTransform CreateNode(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            return rt;
        }

        /// <summary>按设计中心坐标定位一个 RectTransform。</summary>
        public static void PlaceByDesignCenter(RectTransform rt, float designCx, float designCy, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = BlockLayout.DesignToAnchored(designCx, designCy);
        }

        /// <summary>纯色 Image，按设计中心坐标定位。</summary>
        public static Image CreateImage(Transform parent, string name, float designCx, float designCy, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            PlaceByDesignCenter(rt, designCx, designCy, w, h);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>文本，按设计中心坐标定位。</summary>
        public static Text CreateText(Transform parent, string name, float designCx, float designCy, float w, float h,
            string content, int fontSize, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            PlaceByDesignCenter(rt, designCx, designCy, w, h);
            var txt = go.GetComponent<Text>();
            txt.font = DefaultFont;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = anchor;
            txt.fontStyle = style;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        /// <summary>纯色背景 + 居中文本的按钮。</summary>
        public static Button CreateButton(Transform parent, string name, float designCx, float designCy, float w, float h,
            string label, int fontSize, Color bgColor, Color textColor, out Image bgImage, out Text labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            PlaceByDesignCenter(rt, designCx, designCy, w, h);
            bgImage = go.GetComponent<Image>();
            bgImage.color = bgColor;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bgImage;

            var lgo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var lrt = lgo.GetComponent<RectTransform>();
            lrt.SetParent(rt, false);
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            labelText = lgo.GetComponent<Text>();
            labelText.font = DefaultFont;
            labelText.text = label;
            labelText.fontSize = fontSize;
            labelText.color = textColor;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.fontStyle = FontStyle.Bold;
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelText.verticalOverflow = VerticalWrapMode.Overflow;
            labelText.raycastTarget = false;
            return btn;
        }
    }
}
