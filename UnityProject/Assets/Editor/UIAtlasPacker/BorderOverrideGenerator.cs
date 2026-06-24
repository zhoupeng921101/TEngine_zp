using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Auto9Slicer;
using UnityEditor;
using UnityEngine;

namespace UIAtlasPackerTool
{
    /// <summary>
    /// 用 kyubuns/Auto9Slicer 的 <c>Slicer.Slice</c> 自动探测一个散切图目录下每张 PNG 的 9-slice border，
    /// 把非零 border 写进该目录的 <c>_border_override.json</c>（键=文件名去扩展名，值=[left,bottom,right,top]），
    /// 供 <see cref="UIAtlasPacker.Pack"/> 打表时按子图覆盖 border（设计：AI 生图→UI 资产试点 段二）。
    ///
    /// 非破坏式：只读 <c>Slicer.Slice</c> 返回的 <c>Border</c>，丢弃其裁剪后的 Texture；
    /// 绝不回写源 PNG、绝不改源 importer.spriteBorder（那是 Auto9Slicer 自带 Tester 的行为，本工程不内置 Tester）。
    /// 源 PNG 多为 <c>isReadable=0</c> → 不改源导入设置，改读 PNG 字节 <c>LoadImage</c> 到临时可读纹理喂 Slicer。
    ///
    /// 探测准度：仅扁平、轴对称、border 区为纯色的中心检得准。华丽雕花角 / 渐变中心 / 内阴影 /
    /// 圆形 / 胶囊 / 非对称角框会误检——本工具如实写出全部非零探测值作「人工微调起点」，
    /// 并在结果里把可疑项（四向不相等、或仅单/双向有 border）标为低置信，留人工在 json 内手填校正。
    /// <c>_border_override.json</c> 是非破坏式且人可编辑的单一调参源。
    ///
    /// 仅编辑器程序集（不打包、不热更）。核心是可被 EditMode 直调的静态 <see cref="Generate"/>，菜单项是薄壳。
    /// </summary>
    public static class BorderOverrideGenerator
    {
        private const string MenuPath = "UITools/生成 9-slice border 覆盖(Auto9Slicer -> _border_override.json)";

        /// <summary>单张子图的 border 探测项。</summary>
        public class BorderEntry
        {
            public string SpriteName;
            public Vector4 Border;     // (left, bottom, right, top)
            public bool LowConfidence; // 四向不全相等 / 仅部分方向有 border → 疑似误检，留人工核
        }

        /// <summary>border 探测结果。Success=false 时 ErrorMessage 给中止原因、未写任何文件。</summary>
        public class GenerateResult
        {
            public bool Success;
            public string ErrorMessage;
            public string OverrideJsonPath;          // 成功时为产出 json 的资源路径
            public int ScannedPngCount;              // 扫到的 PNG 总数
            public List<BorderEntry> NonZeroBorders = new List<BorderEntry>();   // 写入 json 的非零项
            public List<string> ZeroBorderSprites = new List<string>();          // border 全 0、不写入的子图
        }

        // ───────────────────────── 菜单薄壳 ─────────────────────────

        [MenuItem(MenuPath, false, 2001)]
        private static void GenerateForSelectedFolderMenu()
        {
            string folderPath = GetSelectedFolderPath();
            if (string.IsNullOrEmpty(folderPath))
            {
                EditorUtility.DisplayDialog("生成 border 覆盖", "请在 Project 选中一个切图目录。", "确定");
                return;
            }

            var result = Generate(folderPath, SliceOptions.Default);
            if (!result.Success)
            {
                EditorUtility.DisplayDialog("生成失败", result.ErrorMessage, "确定");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"扫描 {result.ScannedPngCount} 张 PNG，写入 {result.NonZeroBorders.Count} 个非零 border。");
            if (result.NonZeroBorders.Count > 0)
            {
                sb.AppendLine("非零 border：");
                foreach (var e in result.NonZeroBorders)
                {
                    string b = $"[{(int)e.Border.x},{(int)e.Border.y},{(int)e.Border.z},{(int)e.Border.w}]";
                    sb.AppendLine($"  {e.SpriteName} {b}{(e.LowConfidence ? "  ← 低置信，请人工核" : "")}");
                }
            }
            sb.AppendLine($"已写：{result.OverrideJsonPath}");
            sb.AppendLine("（非破坏式：未改任何源 PNG / 源 importer。可手动编辑该 json 校正误检项。）");
            EditorUtility.DisplayDialog("生成完成", sb.ToString(), "确定");

            var jsonObj = AssetDatabase.LoadMainAssetAtPath(result.OverrideJsonPath);
            if (jsonObj != null) EditorGUIUtility.PingObject(jsonObj);
        }

        [MenuItem(MenuPath, true)]
        private static bool GenerateForSelectedFolderMenu_Validate()
        {
            return !string.IsNullOrEmpty(GetSelectedFolderPath());
        }

        private static string GetSelectedFolderPath()
        {
            var obj = Selection.activeObject;
            if (obj == null) return null;
            string path = AssetDatabase.GetAssetPath(obj);
            return AssetDatabase.IsValidFolder(path) ? path : null;
        }

        // ───────────────────────── 核心(可 EditMode 直调) ─────────────────────────

        /// <summary>
        /// 对一个散切图目录探测 border 并写 <c>_border_override.json</c>。
        /// 校验不过即中止并返回 Success=false、不写任何文件。
        /// </summary>
        /// <param name="folderPath">散切图目录的资源路径（如 Assets/AssetRaw/UI/Atlas/settings）</param>
        /// <param name="options">Auto9Slicer 探测参数（Tolerate 容差 / Margin 边距）。null → 取默认。</param>
        public static GenerateResult Generate(string folderPath, SliceOptions options = null)
        {
            options = options ?? SliceOptions.Default;
            var result = new GenerateResult();

            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                result.ErrorMessage = "请提供一个有效的切图目录资源路径。";
                return result;
            }

            folderPath = folderPath.Replace("\\", "/").TrimEnd('/');
            string absFolder = ToAbsolute(folderPath);

            // 收集顶层 PNG（不递归），按文件名 Ordinal 排序（与 UIAtlasPacker 一致，输出稳定）
            var pngAbsPaths = Directory.GetFiles(absFolder)
                .Where(p => Path.GetExtension(p).ToLowerInvariant() == ".png")
                .OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal)
                .ToList();

            if (pngAbsPaths.Count == 0)
            {
                result.ErrorMessage = "目录内无 PNG 切图。";
                return result;
            }

            result.ScannedPngCount = pngAbsPaths.Count;

            foreach (var absPng in pngAbsPaths)
            {
                string spriteName = Path.GetFileNameWithoutExtension(absPng);

                // 读 PNG 字节到临时可读纹理（不动源 importer；源 isReadable=0 也能读字节解码）
                Texture2D tempTex = null;
                SlicedTexture sliced = null;
                try
                {
                    byte[] bytes = File.ReadAllBytes(absPng);
                    tempTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tempTex.LoadImage(bytes))
                    {
                        result.ErrorMessage = $"{Path.GetFileName(absPng)} 像素读取失败（PNG 解码失败）。";
                        return result;
                    }

                    // 只读 Border；丢弃裁剪后的 Texture（下方 finally DestroyImmediate）
                    sliced = Slicer.Slice(tempTex, options);
                    Vector4 border = sliced.Border.ToVector4();

                    if (border == Vector4.zero)
                    {
                        result.ZeroBorderSprites.Add(spriteName);
                    }
                    else
                    {
                        result.NonZeroBorders.Add(new BorderEntry
                        {
                            SpriteName = spriteName,
                            Border = border,
                            LowConfidence = IsLowConfidence(border),
                        });
                    }
                }
                finally
                {
                    // 丢弃裁剪 Texture + 临时输入纹理，绝不回写
                    if (sliced != null && sliced.Texture != null)
                        UnityEngine.Object.DestroyImmediate(sliced.Texture);
                    if (tempTex != null)
                        UnityEngine.Object.DestroyImmediate(tempTex);
                }
            }

            // 写 _border_override.json（与 UIAtlasPacker.ReadBorderOverrides 解析格式一致：{"name":[l,b,r,t],...}）
            string jsonAssetPath = $"{folderPath}/{UIAtlasPacker.BorderOverrideFileName}";
            string json = BuildJson(result.NonZeroBorders);
            File.WriteAllText(ToAbsolute(jsonAssetPath), json, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(jsonAssetPath, ImportAssetOptions.ForceSynchronousImport);

            result.Success = true;
            result.OverrideJsonPath = jsonAssetPath;
            return result;
        }

        // ───────────────────────── 辅助 ─────────────────────────

        /// <summary>
        /// 低置信判据：四向 border 不全相等，或仅单/双向有 border。
        /// 扁平轴对称底板四向相等（如 {24,24,24,24}）→ 高置信；
        /// 华丽雕花角 / 渐变 / 圆形 / 胶囊常出非对称或部分方向 → 标低置信留人工核。
        /// </summary>
        private static bool IsLowConfidence(Vector4 b)
        {
            int l = (int)b.x, bo = (int)b.y, r = (int)b.z, t = (int)b.w;
            bool allEqual = (l == bo && bo == r && r == t);
            int nonZeroDirs = (l > 0 ? 1 : 0) + (bo > 0 ? 1 : 0) + (r > 0 ? 1 : 0) + (t > 0 ? 1 : 0);
            return !allEqual || nonZeroDirs < 4;
        }

        /// <summary>
        /// 拼 {"name":[l,b,r,t],...}（与 UIAtlasPacker.ReadBorderOverrides 手解格式对齐）。
        /// 空集合 → 写 {}（仍是合法 json，packer 读后 map 为空、全继承源 border）。
        /// border 值是整像素，但 Vector4 是 float；用 InvariantCulture 写整数避免 locale 小数点污染。
        /// 不写注释：保持标准 json（低置信信息走 <see cref="GenerateResult"/> 上报，不污染数据文件）。
        /// </summary>
        private static string BuildJson(List<BorderEntry> entries)
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("{\n");
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                int l = (int)e.Border.x, b = (int)e.Border.y, r = (int)e.Border.z, t = (int)e.Border.w;
                sb.Append("  \"").Append(e.SpriteName).Append("\": [")
                  .Append(l.ToString(ci)).Append(", ")
                  .Append(b.ToString(ci)).Append(", ")
                  .Append(r.ToString(ci)).Append(", ")
                  .Append(t.ToString(ci)).Append("]");
                if (i < entries.Count - 1) sb.Append(",");
                sb.Append("\n");
            }
            sb.Append("}\n");
            return sb.ToString();
        }

        private static string ToAbsolute(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath).Replace("\\", "/");
        }
    }
}
