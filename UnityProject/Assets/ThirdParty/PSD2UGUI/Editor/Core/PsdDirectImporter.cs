using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Ntreev.Library.Psd;
using UnityEditor;
using UnityEngine;

namespace PSDUIImporter
{
    /// <summary>
    /// PSD 直解导入器（路线 A）：在 Unity 内用 Ntreev 直接解析 .psd，
    /// 复用图层命名约定（@Button/@Grid/...），构建现有 PSDUI 数据图 + 切图 PNG，
    /// 然后走现有 PSDImportCtrl 绘制管线生成 UGUI。无需 Photoshop / 无需 XML。
    ///
    /// 适配 TEngine_block 版 PSD2UGUI 的契约：
    /// - 切图加载路径 = PSDImportUtility.baseDirectory + PSImage.CustomImageName + 后缀(.png)。
    ///   本导入器 customFolder 留空，name 即文件名；切图写到 Assets/&lt;psd名&gt;/&lt;name&gt;.png。
    /// - 九宫格 border：T 的 setSpriteBorder = Vector4(a0,a1,a2,a3) = Unity(left,bottom,right,top)，
    ///   故 arguments 输出顺序为 [left, bottom, right, top]。
    /// - 文本层 → Label（活的 UGUI Text），字号/颜色/字体从 EngineData 提取（带兜底）。
    /// - @PNG/@JPG 组 → 手动 alpha 合成为单张图（仅 Normal 混合模式）。
    /// - 启用了"投影"的图层 → 绘制后补一个 UGUI Shadow 组件（近似参数）。
    /// - 本文件未在编辑器外编译验证，首次需在 Unity(6000.4) 内确认编译。
    /// </summary>
    public class PsdDirectImporter
    {
        [MenuItem("UITools/PSD TO UGUI", false, 1)]
        public static void ImportPsdDirect()
        {
            string psdPath = EditorUtility.OpenFilePanel("选择要导入的 PSD", Application.dataPath, "psd");
            if (string.IsNullOrEmpty(psdPath))
                return;
            try
            {
                new PsdDirectImporter().Run(psdPath);
            }
            catch (Exception e)
            {
                Debug.LogError("PSD Direct Import 失败: " + e);
            }
            finally
            {
                GC.Collect();
            }
        }

        // ---- 运行期状态 ----
        private string baseDir;        // "Assets/<name>/"
        private string baseFilename;
        private int canvasW, canvasH;
        private int uuid = 1;
        private readonly Dictionary<string, byte[]> pendingPng = new Dictionary<string, byte[]>(); // 绝对路径 -> bytes
        private readonly Dictionary<string, string> renderedKeyToName = new Dictionary<string, string>(); // 内容hash|type|args -> 已生成图名（去重：重复拷贝图只生成1张）
        private readonly List<ShadowInfo> shadowTargets = new List<ShadowInfo>(); // 需要补 Shadow 的对象 + 精确参数

        private struct ShadowInfo
        {
            public string name;
            public Color color;
            public Vector2 distance;
        }

        public void Run(string psdPath)
        {
            PSDImporterConst.LoadConfig();
            baseFilename = Path.GetFileNameWithoutExtension(psdPath);
            baseDir = "Assets/" + baseFilename + "/";
            string absDir = Path.Combine(Application.dataPath, baseFilename);
            Directory.CreateDirectory(absDir);

            PSDUI ui = new PSDUI();
            using (PsdDocument doc = PsdDocument.Create(psdPath))
            {
                canvasW = doc.Width;
                canvasH = doc.Height;
                ui.psdSize = new Size { width = canvasW, height = canvasH };

                List<Layer> tops = new List<Layer>();
                foreach (IPsdLayer child in doc.Childs)
                {
                    Layer l = ExportLayer(child);
                    if (l != null) tops.Add(l);
                }
                ui.layers = tops.ToArray();
            }

            // 落盘所有切图，刷新成 Unity 资源
            foreach (var kv in pendingPng)
                File.WriteAllBytes(kv.Key, kv.Value);
            AssetDatabase.Refresh();

            // 复用现有绘制管线
            PSDImportCtrl ctrl = new PSDImportCtrl(ui, baseDir, baseFilename);
            ctrl.BeginDrawUILayers();
            ctrl.BeginSetUIParents();

            ApplyShadows();
            AssetDatabase.Refresh();
            Debug.Log("PSD Direct Import 完成: " + psdPath + "  → " + baseDir);
        }

        //====================================================================
        // 图层名 = 标注来源（CEP 在 PS 端把 @ 标签写进图层名）。无标注则为原始图层名。
        //====================================================================
        private string EffName(IPsdLayer layer)
        {
            return layer == null ? "" : (layer.Name ?? "");
        }

        //====================================================================
        // 调度：对应 jsx 的 exportLayer / exportLayerSet / exportArtLayer
        //====================================================================

        // 隐藏层（PSD 关了眼睛）不导入；隐藏组在此返回 null → 整个子树跳过
        private static bool IsHidden(IPsdLayer l)
        {
            Ntreev.Library.Psd.PsdLayer pl = l as Ntreev.Library.Psd.PsdLayer;
            return pl != null && !pl.IsVisible;
        }

        private Layer ExportLayer(IPsdLayer l)
        {
            if (l == null) return null;
            if (IsHidden(l)) return null;
            if (EffName(l) != null && EffName(l).IndexOf("@NoExport", StringComparison.Ordinal) >= 0) return null;

            bool isGroup = l.Childs != null && l.Childs.Length > 0;
            Layer result = isGroup ? ExportLayerSet(l) : ExportArtLayer(l);
            return result;
        }

        private Layer ExportLayerSet(IPsdLayer g)
        {
            string name = EffName(g) ?? "";

            // @PNG / @JPG：整组合成为单张图
            if (Has(name, "@PNG") || Has(name, "@JPG"))
                return ExportGroupAsImage(g);

            if (Has(name, "@ScrollView")) return BuildScrollView(g);
            if (Has(name, "@Grid")) return BuildGrid(g);
            if (Has(name, "@Button")) return BuildChildrenLayer(g, LayerType.Button);
            if (Has(name, "@Toggle")) return BuildChildrenLayer(g, LayerType.Toggle);
            if (Has(name, "@Panel")) return BuildPanel(g);
            if (Has(name, "@Slider")) return BuildSlider(g);
            if (Has(name, "@Group")) return BuildLayoutGroup(g);
            if (Has(name, "@InputField")) return BuildChildrenLayer(g, LayerType.InputField);
            if (Has(name, "@Scrollbar")) return BuildScrollBar(g);
            if (Has(name, "@LE")) return BuildLayoutElement(g);
            if (Has(name, "@TabGroup")) return BuildChildrenLayer(g, LayerType.TabGroup);

            // 普通组 → Normal 容器，递归子层
            Layer layer = new Layer { name = name, type = LayerType.Normal };
            layer.layers = ExportChildren(g);
            return layer;
        }

        private Layer ExportArtLayer(IPsdLayer l)
        {
            string name = EffName(l) ?? "";
            if (Has(name, "@Size") || Has(name, "@NoExport")) return null;

            Layer layer = new Layer { name = MakeValidName(name), type = LayerType.Normal };
            PSImage img = new PSImage { name = layer.name, opacity = l.Opacity * 100f };
            layer.image = img;

            bool isText = l.Resources != null && l.Resources.Contains("TySh");
            bool forceImage = Has(name, "_ArtStatic") || Has(name, "@PNG") || Has(name, "@JPG");

            if (isText && !forceImage)
            {
                BuildLabel(l, img);
            }
            else if (Has(name, "Texture"))
            {
                img.imageType = ImageType.Texture;
                img.imageSource = SourceOf(name);
                SetGeom(img, l);
                QueueRender(img, l);
            }
            else
            {
                BuildImage(l, img, name);
            }

            // 投影：读精确参数，绘制后补 Shadow
            ShadowInfo si;
            if (TryExtractDropShadow(l, layer.name, out si))
                shadowTargets.Add(si);

            return layer;
        }

        //====================================================================
        // 各组件构建（以 Unity 端 *LayerImport 读取字段为准）
        //====================================================================

        // Button / Toggle / InputField / TabGroup：子层为各自的 art 子图
        private Layer BuildChildrenLayer(IPsdLayer g, LayerType type)
        {
            Layer layer = new Layer { name = StripTag(EffName(g)), type = type };
            layer.layers = ExportChildren(g);
            return layer;
        }

        // Panel：image = 名字含 background 的子图；layers = 其余子层
        private Layer BuildPanel(IPsdLayer g)
        {
            Layer layer = new Layer { name = StripTag(EffName(g)), type = LayerType.Panel };
            List<Layer> others = new List<Layer>();
            foreach (IPsdLayer c in g.Childs)
            {
                if (EffName(c) != null && EffName(c).ToLower().Contains("background") && IsImageLayer(c))
                {
                    PSImage img = new PSImage { name = MakeValidName(EffName(c)) };
                    BuildImage(c, img, EffName(c));
                    layer.image = img;
                }
                else
                {
                    Layer cl = ExportLayer(c);
                    if (cl != null) others.Add(cl);
                }
            }
            // 无 background 子层时的兜底：必须带 size/position，否则 SpriteImport 取 image.size 会 NRE
            if (layer.image == null)
            {
                PSImage fb = new PSImage { name = layer.name, imageType = ImageType.Image, imageSource = ImageSource.Custom };
                SetGeom(fb, g);
                layer.image = fb;
            }
            layer.layers = others.ToArray();
            return layer;
        }

        private Layer BuildLayoutElement(IPsdLayer g)
        {
            Layer layer = new Layer { name = StripTag(EffName(g)), type = LayerType.LayoutElement };
            SetLayerGeom(layer, SizeRef(g));
            layer.layers = ExportChildren(g);
            return layer;
        }

        // Grid: arguments = [rows, cols, cellW, cellH, gapX, gapY]
        private Layer BuildGrid(IPsdLayer g)
        {
            Layer layer = new Layer { name = StripTag(EffName(g)), type = LayerType.Grid };
            IPsdLayer sizeRef = SizeRef(g);
            SetLayerGeom(layer, sizeRef);

            string[] ps = (EffName(g) ?? "").Split(':');
            int rows = ParseInt(ps, 1, 1);
            int cols = ParseInt(ps, 2, 1);

            IPsdLayer cell = FirstContentChild(g);
            float cellW = cell != null ? (cell.Right - cell.Left) : 0;
            float cellH = cell != null ? (cell.Bottom - cell.Top) : 0;
            float W = layer.size != null ? layer.size.width : 0;
            float H = layer.size != null ? layer.size.height : 0;
            float gapX = cols > 1 ? (W - cellW * cols) / (cols - 1) : 0;
            float gapY = rows > 1 ? (H - cellH * rows) / (rows - 1) : 0;

            layer.arguments = new[] {
                rows.ToString(), cols.ToString(),
                Mathf.RoundToInt(cellW).ToString(), Mathf.RoundToInt(cellH).ToString(),
                Mathf.RoundToInt(gapX).ToString(), Mathf.RoundToInt(gapY).ToString()
            };
            layer.layers = ExportChildren(g, skipSize: true);
            return layer;
        }

        // ScrollView: arguments = [dir(H/V), spacing, leftPad, topPad]
        private Layer BuildScrollView(IPsdLayer g)
        {
            Layer layer = new Layer { name = StripTag(EffName(g)), type = LayerType.ScrollView };
            IPsdLayer sizeRef = SizeRef(g);
            SetLayerGeom(layer, sizeRef);

            string[] ps = (EffName(g) ?? "").Split(':');
            string dir = ps.Length > 1 ? ps[1] : "V";

            List<IPsdLayer> content = ContentChildren(g);
            float spacing = 0, padX = 0, padY = 0;
            if (layer.size != null && content.Count >= 1)
            {
                IPsdLayer c0 = content[0];
                float c0w = c0.Right - c0.Left, c0h = c0.Bottom - c0.Top;
                if (dir.ToUpper().Contains("H"))
                {
                    padX = c0.Left - (PsCenterX(sizeRef) - layer.size.width / 2f);
                    padY = (layer.size.height - c0h) / 2f;
                    if (content.Count >= 2) spacing = (content[1].Left) - (c0.Left) - c0w;
                }
                else
                {
                    padX = (layer.size.width - c0w) / 2f;
                    padY = c0.Top - (PsCenterY(sizeRef) - layer.size.height / 2f);
                    if (content.Count >= 2) spacing = (content[1].Top) - (c0.Top) - c0h;
                }
            }
            layer.arguments = new[] {
                dir, Mathf.RoundToInt(spacing).ToString(),
                Mathf.RoundToInt(padX).ToString(), Mathf.RoundToInt(padY).ToString()
            };
            layer.layers = ExportChildren(g, skipSize: true);
            return layer;
        }

        // Group(LayoutGroup): arguments = [dir(V/H), span]
        private Layer BuildLayoutGroup(IPsdLayer g)
        {
            Layer layer = new Layer { name = StripTag(EffName(g)), type = LayerType.Group };
            SetLayerGeom(layer, SizeRef(g));
            string[] ps = (EffName(g) ?? "").Split(':');
            string dir = ps.Length > 1 ? ps[1] : "V";
            string span = ps.Length > 2 ? ps[2] : "0";
            layer.arguments = new[] { dir, span };
            layer.layers = ExportChildren(g, skipSize: true);
            return layer;
        }

        // Slider: arguments = [dir]; 子层 _bg/_fill/_handle
        private Layer BuildSlider(IPsdLayer g)
        {
            Layer layer = new Layer { name = StripTag(EffName(g)), type = LayerType.Slider };
            SetLayerGeom(layer, SizeRef(g));
            string[] ps = (EffName(g) ?? "").Split(':');
            layer.arguments = new[] { ps.Length > 1 ? ps[1] : "L" };
            layer.layers = ExportChildren(g, skipSize: true);
            return layer;
        }

        // ScrollBar: arguments = [dir, percent]; Unity 端只读单个 layer.image
        private Layer BuildScrollBar(IPsdLayer g)
        {
            Layer layer = new Layer { name = StripTag(EffName(g)), type = LayerType.ScrollBar };
            string[] ps = (EffName(g) ?? "").Split(':');
            layer.arguments = new[] { ps.Length > 1 ? ps[1] : "B", ps.Length > 2 ? ps[2] : "0.2" };
            foreach (IPsdLayer c in g.Childs)
            {
                if (!IsImageLayer(c)) continue;
                PSImage img = new PSImage { name = MakeValidName(EffName(c)) };
                BuildImage(c, img, EffName(c));
                if (EffName(c) != null && EffName(c).ToLower().Contains("background")) { layer.image = img; break; }
                if (layer.image == null) layer.image = img;
            }
            return layer;
        }

        // @PNG/@JPG 组 → 合成单张图（Normal 层 + image）
        private Layer ExportGroupAsImage(IPsdLayer g)
        {
            Layer layer = new Layer { name = MakeValidName(EffName(g)), type = LayerType.Normal };
            PSImage img = new PSImage
            {
                name = layer.name,
                imageType = ImageType.Image,
                imageSource = SourceOf(EffName(g)),
                opacity = g.Opacity * 100f
            };
            SetGeom(img, g);
            byte[] png = CompositeGroup(g);
            if (png != null && img.imageSource != ImageSource.Global)
                pendingPng[Path.Combine(Application.dataPath, baseFilename, img.name + ".png")] = png;
            layer.image = img;
            return layer;
        }

        //====================================================================
        // 图片 / 文本 / 渲染
        //====================================================================

        private void BuildImage(IPsdLayer l, PSImage img, string rawName)
        {
            img.imageSource = SourceOf(rawName);
            SetGeom(img, l);

            if (Has(rawName, "_9S"))
            {
                img.imageType = ImageType.SliceImage;
                img.arguments = ParseNineSlice(rawName);
            }
            else if (Has(rawName, "LeftHalf")) img.imageType = ImageType.LeftHalfImage;
            else if (Has(rawName, "BottomHalf")) img.imageType = ImageType.BottomHalfImage;
            else if (Has(rawName, "Quarter")) img.imageType = ImageType.QuarterImage;
            else img.imageType = ImageType.Image;

            QueueRender(img, l);
        }

        // 文本 → Label（活的 UGUI Text）。arguments=[colorHex,font,size,text,(justify)]
        private void BuildLabel(IPsdLayer l, PSImage img)
        {
            img.imageType = ImageType.Label;
            img.imageSource = ImageSource.Custom;
            SetGeom(img, l);

            TextStyle ts = ExtractTextStyle(l);
            List<string> args = new List<string> { ts.colorHex, ts.font, ts.size.ToString(), ts.text };
            if (!string.IsNullOrEmpty(ts.justification)) args.Add(ts.justification);
            img.arguments = args.ToArray();
            if (!string.IsNullOrEmpty(ts.outlineHex) && ts.outlineWidth > 0f)
                img.outline = ts.outlineHex + "|" + ts.outlineWidth.ToString("0.##", CultureInfo.InvariantCulture);
            // Label 不切图（TextImport 用字体渲染）
        }

        // 渲染图层像素 → PNG bytes，排队写入（Global 源不渲染，引用既有公共图集）。
        // 去重：同像素内容 + 同切图类型/9宫格参数 的层只生成 1 张，重复层复用同名。
        private void QueueRender(PSImage img, IPsdLayer l)
        {
            if (img.imageSource == ImageSource.Global) return;
            byte[] png = RenderLayerPng(l);
            if (png == null) return;

            string key = ContentKey(png, img);
            string existing;
            if (renderedKeyToName.TryGetValue(key, out existing))
            {
                img.name = existing; // 命中重复：指向已生成的那张，不再写新文件
                return;
            }
            renderedKeyToName[key] = img.name;
            pendingPng[Path.Combine(Application.dataPath, baseFilename, img.name + ".png")] = png;
        }

        // 去重键 = PNG 内容 MD5 + 切图类型 + 参数（9宫格 border 不同则不共享）
        private static string ContentKey(byte[] png, PSImage img)
        {
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                string hex = System.BitConverter.ToString(md5.ComputeHash(png)).Replace("-", "");
                string args = (img.arguments != null) ? string.Join(",", img.arguments) : "";
                return hex + "|" + (int)img.imageType + "|" + args;
            }
        }

        private static byte[] RenderLayerPng(IImageSource src)
        {
            int w = src.Width, h = src.Height;
            if (w <= 0 || h <= 0 || !src.HasImage) return null;
            IChannel[] ch = src.Channels;
            if (ch == null || ch.Length == 0) return null;

            byte[] r = null, g = null, b = null, a = null;
            foreach (IChannel c in ch)
            {
                switch (c.Type)
                {
                    case ChannelType.Red: r = c.Data; break;
                    case ChannelType.Green: g = c.Data; break;
                    case ChannelType.Blue: b = c.Data; break;
                    case ChannelType.Alpha: a = c.Data; break;
                }
            }
            if (r == null || g == null || b == null) return null;

            int n = w * h;
            Color32[] px = new Color32[n];
            for (int i = 0; i < n; i++)
                px[i] = new Color32(
                    i < r.Length ? r[i] : (byte)0,
                    i < g.Length ? g[i] : (byte)0,
                    i < b.Length ? b[i] : (byte)0,
                    (a != null && i < a.Length) ? a[i] : (byte)255);

            return EncodePng(w, h, px);
        }

        // 手动 alpha-over 合成一个组（仅 Normal 混合模式；丢弃图层样式如投影）
        private byte[] CompositeGroup(IPsdLayer g)
        {
            int W = g.Right - g.Left, H = g.Bottom - g.Top;
            if (W <= 0 || H <= 0) return null;
            Color32[] canvas = new Color32[W * H]; // 透明
            CompositeInto(g, g.Left, g.Top, W, H, canvas);
            return EncodePng(W, H, canvas);
        }

        private void CompositeInto(IPsdLayer node, int ox, int oy, int W, int H, Color32[] canvas)
        {
            // 自底向上（Ntreev Childs 已是底→顶）
            foreach (IPsdLayer c in node.Childs)
            {
                if (IsHidden(c)) continue;   // @PNG 合成同样跳过隐藏子层
                if (c.Childs != null && c.Childs.Length > 0)
                {
                    CompositeInto(c, ox, oy, W, H, canvas);
                    continue;
                }
                if (!c.HasImage) continue;
                int lw = c.Width, lh = c.Height;
                if (lw <= 0 || lh <= 0) continue;
                byte[] cr = null, cg = null, cb = null, ca = null;
                foreach (IChannel ch in c.Channels)
                {
                    switch (ch.Type)
                    {
                        case ChannelType.Red: cr = ch.Data; break;
                        case ChannelType.Green: cg = ch.Data; break;
                        case ChannelType.Blue: cb = ch.Data; break;
                        case ChannelType.Alpha: ca = ch.Data; break;
                    }
                }
                if (cr == null || cg == null || cb == null) continue;
                int dx = c.Left - ox, dy = c.Top - oy;
                for (int y = 0; y < lh; y++)
                {
                    int cy = dy + y;
                    if (cy < 0 || cy >= H) continue;
                    for (int x = 0; x < lw; x++)
                    {
                        int cx = dx + x;
                        if (cx < 0 || cx >= W) continue;
                        int si = y * lw + x;
                        byte sa = (ca != null && si < ca.Length) ? ca[si] : (byte)255;
                        if (sa == 0) continue;
                        int di = cy * W + cx;
                        Color32 dst = canvas[di];
                        float fa = sa / 255f;
                        float ia = 1f - fa;
                        canvas[di] = new Color32(
                            (byte)(cr[si] * fa + dst.r * ia),
                            (byte)(cg[si] * fa + dst.g * ia),
                            (byte)(cb[si] * fa + dst.b * ia),
                            (byte)Mathf.Min(255, sa + dst.a * ia));
                    }
                }
            }
        }

        //====================================================================
        // 几何 / 命名 / 工具
        //====================================================================

        private void SetGeom(PSImage img, IPsdLayer l)
        {
            img.size = new Size { width = l.Right - l.Left, height = l.Bottom - l.Top };
            img.position = new Position { x = PsCenterX(l) - canvasW / 2f, y = canvasH / 2f - PsCenterY(l) };
        }

        private void SetLayerGeom(Layer layer, IPsdLayer geomRef)
        {
            if (geomRef == null) return;
            layer.size = new Size { width = geomRef.Right - geomRef.Left, height = geomRef.Bottom - geomRef.Top };
            layer.position = new Position { x = PsCenterX(geomRef) - canvasW / 2f, y = canvasH / 2f - PsCenterY(geomRef) };
        }

        private static float PsCenterX(IPsdLayer l) { return l == null ? 0 : (l.Left + l.Right) / 2f; }
        private static float PsCenterY(IPsdLayer l) { return l == null ? 0 : (l.Top + l.Bottom) / 2f; }

        private Layer[] ExportChildren(IPsdLayer g, bool skipSize = false)
        {
            List<Layer> list = new List<Layer>();
            foreach (IPsdLayer c in g.Childs)
            {
                if (skipSize && EffName(c) != null && EffName(c).IndexOf("@Size", StringComparison.Ordinal) >= 0) continue;
                Layer l = ExportLayer(c);
                if (l != null) list.Add(l);
            }
            return list.ToArray();
        }

        // 含 @Size 的子层定义容器尺寸/位置；没有则用组自身 bounds
        private IPsdLayer SizeRef(IPsdLayer g)
        {
            foreach (IPsdLayer c in g.Childs)
                if (EffName(c) != null && EffName(c).IndexOf("@Size", StringComparison.Ordinal) >= 0)
                    return c;
            return g;
        }

        private IPsdLayer FirstContentChild(IPsdLayer g)
        {
            foreach (IPsdLayer c in g.Childs)
                if (EffName(c) == null || EffName(c).IndexOf("@Size", StringComparison.Ordinal) < 0)
                    return c;
            return null;
        }

        private List<IPsdLayer> ContentChildren(IPsdLayer g)
        {
            List<IPsdLayer> list = new List<IPsdLayer>();
            foreach (IPsdLayer c in g.Childs)
                if (EffName(c) == null || EffName(c).IndexOf("@Size", StringComparison.Ordinal) < 0)
                    list.Add(c);
            return list;
        }

        private static bool IsImageLayer(IPsdLayer l)
        {
            bool isGroup = l.Childs != null && l.Childs.Length > 0;
            bool isText = l.Resources != null && l.Resources.Contains("TySh");
            return !isGroup && !isText && l.HasImage;
        }

        private static bool Has(string name, string token)
        {
            return name != null && name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string StripTag(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int at = name.IndexOf('@');
            return at > 0 ? name.Substring(0, at) : name;
        }

        private ImageSource SourceOf(string name)
        {
            if (Has(name, "Global")) return ImageSource.Global;
            if (Has(name, "Common")) return ImageSource.Common;
            return ImageSource.Custom; // CustomAtlas 统一按 Custom 处理
        }

        // 生成切图文件名：去掉 _9S/_JB/_OL/@后缀，非法字符替换，去重加 uuid
        private string MakeValidName(string name)
        {
            string v = (name ?? "").Trim();
            v = Regex.Replace(v, @"\s*_9S(\:\d+)+", "");
            v = Regex.Replace(v, @"\s*_JB(\:[a-zA-Z0-9]+)+", "");
            v = Regex.Replace(v, @"\s*_OL(\:[a-zA-Z0-9]+)+", "");
            foreach (char c in "\\*/?:\"|<>") v = v.Replace(c.ToString(), "");
            v = v.Replace(' ', '_');

            int at = v.IndexOf('@');
            if (at >= 0) v = v.Substring(0, at);
            if (string.IsNullOrEmpty(v)) v = "img";
            return v + "_" + (uuid++);
        }

        // 九宫格 border：约定 name 含 "_9S:all" 或 "_9S:left:top:right:bottom"
        // T 的 setSpriteBorder = Vector4(a0,a1,a2,a3) = Unity(left,bottom,right,top)
        private string[] ParseNineSlice(string name)
        {
            Match m = Regex.Match(name ?? "", @"_9S((?:\:\d+)+)");
            if (!m.Success) return new[] { "0", "0", "0", "0" };
            string[] nums = m.Groups[1].Value.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            if (nums.Length == 1)
                return new[] { nums[0], nums[0], nums[0], nums[0] };
            if (nums.Length >= 4)
                // 输入 left:top:right:bottom → 输出 left,bottom,right,top
                return new[] { nums[0], nums[3], nums[2], nums[1] };
            return new[] { "0", "0", "0", "0" };
        }

        private static int ParseInt(string[] arr, int idx, int def)
        {
            int v;
            if (arr != null && idx < arr.Length && int.TryParse(arr[idx], out v)) return v;
            return def;
        }

        //====================================================================
        // 文本样式提取（EngineData）
        //====================================================================

        private struct TextStyle
        {
            public string text;
            public string colorHex; // RRGGBB
            public string font;
            public int size;
            public string justification;
            public string outlineHex;   // 描边颜色 RRGGBB；null=无描边
            public float outlineWidth;  // 描边粗细(px)
        }

        private TextStyle ExtractTextStyle(IPsdLayer l)
        {
            TextStyle ts = new TextStyle { text = "", colorHex = "FFFFFF", font = "Arial", size = 24, justification = null, outlineHex = null, outlineWidth = 0f };
            try
            {
                IProperties tysh = l.Resources["TySh"] as IProperties;
                IProperties text = tysh != null ? tysh["Text"] as IProperties : null;
                if (text == null) return ts;

                if (text.Contains("Txt"))
                    ts.text = Convert.ToString(text["Txt"]);

                double scaleY = 1.0;
                if (tysh.Contains("Transforms"))
                {
                    double[] m = tysh["Transforms"] as double[];
                    if (m != null && m.Length >= 4) scaleY = Math.Abs(m[3]);
                }

                IProperties engine = text.Contains("EngineData") ? text["EngineData"] as IProperties : null;
                IProperties engineDict = engine != null && engine.Contains("EngineDict") ? engine["EngineDict"] as IProperties : null;
                IProperties styleRun = engineDict != null && engineDict.Contains("StyleRun") ? engineDict["StyleRun"] as IProperties : null;
                IProperties styleData = FirstStyleSheetData(styleRun);
                if (styleData != null)
                {
                    if (styleData.Contains("FontSize"))
                    {
                        double fs = Convert.ToDouble(styleData["FontSize"]);
                        ts.size = Math.Max(1, (int)Math.Round(fs * scaleY));
                    }
                    if (styleData.Contains("FillColor"))
                        ts.colorHex = ColorHexFrom(styleData["FillColor"] as IProperties);
                    if (styleData.Contains("Font"))
                    {
                        int fontIdx = Convert.ToInt32(styleData["Font"]);
                        string fn = FontNameFrom(engine, fontIdx);
                        if (!string.IsNullOrEmpty(fn)) ts.font = fn;
                    }
                }

                // 段落对齐：ParagraphRun→RunArray[0]→ParagraphSheet→Properties→Justification
                IProperties paraRun = engineDict != null && engineDict.Contains("ParagraphRun") ? engineDict["ParagraphRun"] as IProperties : null;
                if (paraRun != null && paraRun.Contains("RunArray"))
                {
                    IProperties pr0 = NthOf(paraRun["RunArray"], 0) as IProperties;
                    IProperties psheet = pr0 != null && pr0.Contains("ParagraphSheet") ? pr0["ParagraphSheet"] as IProperties : null;
                    IProperties pprops = psheet != null && psheet.Contains("Properties") ? psheet["Properties"] as IProperties : null;
                    if (pprops != null && pprops.Contains("Justification"))
                        ts.justification = "Justification." + JustName(Convert.ToInt32(pprops["Justification"]));
                }

                // 描边：lfx2.FrFX → 颜色 + 粗细(px)，喂给 TextImport 加 Outline
                string oh; float ow;
                if (TryExtractStroke(l, scaleY, out oh, out ow)) { ts.outlineHex = oh; ts.outlineWidth = ow; }
            }
            catch (Exception e)
            {
                Debug.LogWarning("文本样式提取失败(已用兜底): " + (EffName(l) ?? "") + " : " + e.Message);
            }
            return ts;
        }

        // EngineData justification 码 → TextImport.Justification 枚举名（喂给 ParseAlignmentPS2UGUI）
        private static string JustName(int j)
        {
            switch (j)
            {
                case 0: return "LEFT";   // 左
                case 1: return "RIGHT";  // 右
                case 2: return "CENTER"; // 中
                case 3: return "LEFT";   // justify-left ≈ 左
                case 4: return "RIGHT";  // justify-right ≈ 右
                default: return "CENTER";
            }
        }

        private static IProperties FirstStyleSheetData(IProperties styleRun)
        {
            if (styleRun == null || !styleRun.Contains("RunArray")) return null;
            object first = NthOf(styleRun["RunArray"], 0);
            IProperties run = first as IProperties;
            if (run == null) return null;
            IProperties sheet = run.Contains("StyleSheet") ? run["StyleSheet"] as IProperties : null;
            if (sheet == null) return null;
            return sheet.Contains("StyleSheetData") ? sheet["StyleSheetData"] as IProperties : null;
        }

        private static string FontNameFrom(IProperties engine, int idx)
        {
            try
            {
                IProperties res = engine != null && engine.Contains("ResourceDict") ? engine["ResourceDict"] as IProperties : null;
                object fontSet = res != null && res.Contains("FontSet") ? res["FontSet"] : null;
                object item = NthOf(fontSet, idx);
                IProperties fp = item as IProperties;
                if (fp != null && fp.Contains("Name"))
                    return Convert.ToString(fp["Name"]);
            }
            catch { }
            return null;
        }

        private static string ColorHexFrom(IProperties fill)
        {
            try
            {
                if (fill == null || !fill.Contains("Values")) return "FFFFFF";
                IList list = fill["Values"] as IList;
                if (list == null || list.Count < 4) return "FFFFFF";
                int r = To255(list[1]), g = To255(list[2]), b = To255(list[3]);
                return r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
            }
            catch { return "FFFFFF"; }
        }

        private static int To255(object o)
        {
            double d = Convert.ToDouble(o, CultureInfo.InvariantCulture);
            return Mathf.Clamp((int)Math.Round(d * 255.0), 0, 255);
        }

        private static object NthOf(object o, int idx)
        {
            if (o == null) return null;
            IList list = o as IList;
            if (list != null) return (idx >= 0 && idx < list.Count) ? list[idx] : null;
            IEnumerable e = o as IEnumerable;
            if (e != null)
            {
                int i = 0;
                foreach (object x in e) { if (i == idx) return x; i++; }
            }
            return null;
        }

        //====================================================================
        // 投影
        //====================================================================

        // 从 lfx2.DrSh 读精确投影参数 → UGUI Shadow(effectColor + effectDistance)
        // PS: Clr(RGB 0..255) / Opct(%) / lagl(角度) / Dstn(距离px)；blur 无 UGUI 对应，忽略。
        private bool TryExtractDropShadow(IPsdLayer l, string objName, out ShadowInfo info)
        {
            info = default(ShadowInfo);
            try
            {
                if (l.Resources == null || !l.Resources.Contains("lfx2")) return false;
                IProperties fx = l.Resources["lfx2"] as IProperties;
                IProperties ds = fx != null && fx.Contains("DrSh") ? fx["DrSh"] as IProperties : null;
                if (ds == null) return false;
                if (ds.Contains("enab") && !Convert.ToBoolean(ds["enab"])) return false;

                double dist = UnitVal(ds, "Dstn");
                double angle = UnitVal(ds, "lagl");
                double opct = ds.Contains("Opct") ? UnitVal(ds, "Opct") : 100.0;

                float r = 0, g = 0, b = 0;
                IProperties clr = ds.Contains("Clr") ? ds["Clr"] as IProperties : null;
                if (clr != null)
                {
                    r = (float)(ToD(clr, "Rd") / 255.0);
                    g = (float)(ToD(clr, "Grn") / 255.0);
                    b = (float)(ToD(clr, "Bl") / 255.0);
                }

                // PS 角度→偏移：阴影背光投射；UGUI y 向上故取负。
                double rad = angle * Math.PI / 180.0;
                float ex = (float)(-dist * Math.Cos(rad));
                float ey = (float)(-dist * Math.Sin(rad));

                info = new ShadowInfo
                {
                    name = objName,
                    color = new Color(r, g, b, (float)(opct / 100.0)),
                    distance = new Vector2(ex, ey)
                };
                return true;
            }
            catch { return false; }
        }

        // 从 lfx2.FrFX(描边/Stroke) 读颜色 + 粗细 → 喂 TextImport 加 UGUI Outline。
        // PS: Clr(Rd/Grn/Bl 0..255) / Sz(px)。enab=false 跳过。粗细按文本缩放同步。
        private bool TryExtractStroke(IPsdLayer l, double scale, out string hex, out float width)
        {
            hex = null; width = 0f;
            try
            {
                if (l.Resources == null || !l.Resources.Contains("lfx2")) return false;
                IProperties fx = l.Resources["lfx2"] as IProperties;
                IProperties fr = fx != null && fx.Contains("FrFX") ? fx["FrFX"] as IProperties : null;
                if (fr == null) return false;
                if (fr.Contains("enab") && !Convert.ToBoolean(fr["enab"])) return false;

                double sz = UnitVal(fr, "Sz");
                if (sz <= 0) return false;

                int r = 255, g = 255, b = 255;
                IProperties clr = fr.Contains("Clr") ? fr["Clr"] as IProperties : null;
                if (clr != null)
                {
                    r = Mathf.Clamp((int)Math.Round(ToD(clr, "Rd")), 0, 255);
                    g = Mathf.Clamp((int)Math.Round(ToD(clr, "Grn")), 0, 255);
                    b = Mathf.Clamp((int)Math.Round(ToD(clr, "Bl")), 0, 255);
                }
                hex = r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
                width = (float)Math.Max(1.0, sz * (scale <= 0 ? 1.0 : scale));
                return true;
            }
            catch { hex = null; width = 0f; return false; }
        }

        // 读 StructureUnitFloat 的 Value
        private static double UnitVal(IProperties parent, string key)
        {
            if (parent == null || !parent.Contains(key)) return 0;
            IProperties p = parent[key] as IProperties;
            if (p != null && p.Contains("Value")) return Convert.ToDouble(p["Value"]);
            return Convert.ToDouble(parent[key]);
        }

        private static double ToD(IProperties p, string key)
        {
            return (p != null && p.Contains(key)) ? Convert.ToDouble(p[key]) : 0.0;
        }

        private void ApplyShadows()
        {
            if (shadowTargets.Count == 0 || PSDImportUtility.canvas == null) return;
            foreach (ShadowInfo si in shadowTargets)
            {
                Transform t = FindDeep(PSDImportUtility.canvas.transform, si.name);
                if (t == null) continue;
                if (t.GetComponent<UnityEngine.UI.Graphic>() == null) continue;
                UnityEngine.UI.Shadow sh = t.GetComponent<UnityEngine.UI.Shadow>();
                if (sh == null) sh = t.gameObject.AddComponent<UnityEngine.UI.Shadow>();
                sh.effectColor = si.color;
                sh.effectDistance = si.distance;
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        //====================================================================
        // 极简 PNG 编码（RGBA）
        //====================================================================

        private static byte[] EncodePng(int w, int h, Color32[] px)
        {
            byte[] raw = new byte[h * (w * 4 + 1)];
            int p = 0;
            for (int y = 0; y < h; y++)
            {
                raw[p++] = 0; // filter none
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    Color32 c = px[row + x];
                    raw[p++] = c.r; raw[p++] = c.g; raw[p++] = c.b; raw[p++] = c.a;
                }
            }
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0, 8);
                byte[] ihdr = new byte[13];
                WriteBE(ihdr, 0, w); WriteBE(ihdr, 4, h);
                ihdr[8] = 8; ihdr[9] = 6;
                Chunk(ms, "IHDR", ihdr);
                Chunk(ms, "IDAT", ZlibCompress(raw));
                Chunk(ms, "IEND", new byte[0]);
                return ms.ToArray();
            }
        }

        private static void WriteBE(byte[] buf, int off, int v)
        {
            buf[off] = (byte)(v >> 24); buf[off + 1] = (byte)(v >> 16);
            buf[off + 2] = (byte)(v >> 8); buf[off + 3] = (byte)v;
        }

        private static void Chunk(Stream s, string type, byte[] data)
        {
            byte[] len = new byte[4]; WriteBE(len, 0, data.Length); s.Write(len, 0, 4);
            byte[] tb = Encoding.ASCII.GetBytes(type); s.Write(tb, 0, 4);
            s.Write(data, 0, data.Length);
            byte[] cb = new byte[4]; WriteBE(cb, 0, (int)Crc32(tb, data)); s.Write(cb, 0, 4);
        }

        private static byte[] ZlibCompress(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ms.WriteByte(0x78); ms.WriteByte(0x9C);
                using (var ds = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Compress, true))
                    ds.Write(data, 0, data.Length);
                uint adler = Adler32(data);
                ms.WriteByte((byte)(adler >> 24)); ms.WriteByte((byte)(adler >> 16));
                ms.WriteByte((byte)(adler >> 8)); ms.WriteByte((byte)adler);
                return ms.ToArray();
            }
        }

        private static uint Adler32(byte[] d)
        {
            uint a = 1, b = 0;
            for (int i = 0; i < d.Length; i++) { a = (a + d[i]) % 65521; b = (b + a) % 65521; }
            return (b << 16) | a;
        }

        private static uint[] crcTable;
        private static uint Crc32(byte[] type, byte[] data)
        {
            if (crcTable == null)
            {
                crcTable = new uint[256];
                for (uint i = 0; i < 256; i++)
                {
                    uint c = i;
                    for (int k = 0; k < 8; k++) c = ((c & 1) != 0) ? (0xEDB88320 ^ (c >> 1)) : (c >> 1);
                    crcTable[i] = c;
                }
            }
            uint crc = 0xFFFFFFFF;
            for (int i = 0; i < type.Length; i++) crc = crcTable[(crc ^ type[i]) & 0xFF] ^ (crc >> 8);
            for (int i = 0; i < data.Length; i++) crc = crcTable[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFF;
        }
    }
}
