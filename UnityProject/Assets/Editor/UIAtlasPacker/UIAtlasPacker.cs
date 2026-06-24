using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace UIAtlasPackerTool
{
    /// <summary>
    /// 散切图打表工具：把一个散切图目录合成一张 Multiple 模式精灵表 PNG。
    /// 子图名=源文件名(去扩展名)、pivot 居中、alignment=Center、border 从源 importer 级 spriteBorder
    /// 继承(可选 _border_override.json 覆盖)、rect 由 PackTextures 自动排布(尺寸=源像素尺寸)。
    /// 产出落 AssetRaw/UIRaw/Atlas/ 被收集器收录，运行期经 Image.SetSubSprite(location, 子图名) 寻址。
    /// 设计依据：design-docs/24-ui-atlas-packer.md(经 design-docs/index.html 浏览)。
    ///
    /// 打表核心是可被 EditMode 直调的静态方法 <see cref="Pack"/>，菜单项只是薄壳。
    /// 仅编辑器程序集(不打包、不热更)。只读源 importer 的 spriteBorder，不改任何源导入设置。
    /// 写/读子图元数据用现代 API ISpriteEditorDataProvider(Unity.2D.Sprite.Editor)，非 obsolete spritesheet。
    /// </summary>
    public static class UIAtlasPacker
    {
        public const string OutputDirAssetPath = "Assets/AssetRaw/UIRaw/Atlas";
        public const string DefaultPackageName = "DefaultPackage";
        public const string BorderOverrideFileName = "_border_override.json";

        private const int Padding = 2;
        private const int MaxAtlasSize = 2048;

        private const string MenuPath = "UITools/打表(散切图 -> Multiple 精灵表)";

        /// <summary>
        /// 打表结果。Success=false 时 ErrorMessage 给中止原因、未产出任何文件。
        /// </summary>
        public class PackResult
        {
            public bool Success;
            public string ErrorMessage;
            public string SheetAssetPath;   // 成功时为产出表资源路径
            public int SpriteCount;
            public List<string> SkippedNonPng = new List<string>();   // 已跳过的非 PNG 文件名
            public List<string> SkippedSubDirs = new List<string>();  // 未递归的子目录名
        }

        // ───────────────────────── 菜单薄壳 ─────────────────────────

        [MenuItem(MenuPath, false, 2000)]
        private static void PackSelectedFolderMenu()
        {
            string folderPath = GetSelectedFolderPath();
            var result = Pack(folderPath, simulateBuild: true);
            if (!result.Success)
            {
                EditorUtility.DisplayDialog("打表失败", result.ErrorMessage, "确定");
                return;
            }

            var msg = $"打表完成：{Path.GetFileName(result.SheetAssetPath)}，{result.SpriteCount} 个子图。";
            if (result.SkippedNonPng.Count > 0)
                msg += $"\n已跳过(非 PNG)：{string.Join(", ", result.SkippedNonPng)}";
            if (result.SkippedSubDirs.Count > 0)
                msg += $"\n未递归(子目录)：{string.Join(", ", result.SkippedSubDirs)}";
            EditorUtility.DisplayDialog("打表完成", msg, "确定");

            var sheetObj = AssetDatabase.LoadMainAssetAtPath(result.SheetAssetPath);
            if (sheetObj != null) EditorGUIUtility.PingObject(sheetObj);
        }

        [MenuItem(MenuPath, true)]
        private static bool PackSelectedFolderMenu_Validate()
        {
            string folderPath = GetSelectedFolderPath();
            return !string.IsNullOrEmpty(folderPath) && IsUnderOutputTree(folderPath);
        }

        private static string GetSelectedFolderPath()
        {
            var obj = Selection.activeObject;
            if (obj == null) return null;
            string path = AssetDatabase.GetAssetPath(obj);
            return AssetDatabase.IsValidFolder(path) ? path : null;
        }

        // ───────────────────────── 打表核心(可 EditMode 直调) ─────────────────────────

        /// <summary>
        /// 对一个散切图目录打表。校验不过/已存在即中止并返回 Success=false、不产出任何文件。
        /// </summary>
        /// <param name="folderPath">散切图目录的资源路径(如 Assets/AssetRaw/UIRaw/Atlas/settings)</param>
        /// <param name="simulateBuild">成功后是否重建 YooAsset 模拟清单(EditMode 测试一般传 false)</param>
        public static PackResult Pack(string folderPath, bool simulateBuild = true)
        {
            var result = new PackResult();

            // 校验：选中是目录
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                result.ErrorMessage = "请在 Project 选中一个切图目录。";
                return result;
            }

            folderPath = folderPath.Replace("\\", "/").TrimEnd('/');

            // 校验：目录在收录树下(且非该目录本身)
            if (!IsUnderOutputTree(folderPath))
            {
                result.ErrorMessage =
                    $"目录须在 {OutputDirAssetPath}/ 下，否则产出表不被收集器收录、运行期寻址不到。\n当前：{folderPath}";
                return result;
            }

            string folderName = Path.GetFileName(folderPath);
            string sheetAssetPath = $"{OutputDirAssetPath}/Sheet_{folderName}.png";

            // 校验：表已存在则不覆盖、中止(用户拍板：不覆盖)
            if (File.Exists(ToAbsolute(sheetAssetPath)))
            {
                result.ErrorMessage =
                    $"Sheet_{folderName}.png 已存在，本工具不覆盖；如需重打请先手动删旧表。";
                return result;
            }

            // 收集顶层文件(不递归)，分流 PNG / 非 PNG / 子目录
            string absFolder = ToAbsolute(folderPath);
            var pngAssetPaths = new List<string>();
            foreach (var absFile in Directory.GetFiles(absFolder))
            {
                string ext = Path.GetExtension(absFile).ToLowerInvariant();
                if (ext == ".meta") continue;
                string fileName = Path.GetFileName(absFile);
                if (ext == ".png")
                    pngAssetPaths.Add($"{folderPath}/{fileName}");
                else if (string.Equals(fileName, BorderOverrideFileName, StringComparison.OrdinalIgnoreCase))
                    { /* 覆盖配置文件：既不打表、也不报"已跳过" */ }
                else
                    result.SkippedNonPng.Add(fileName);
            }
            foreach (var absSub in Directory.GetDirectories(absFolder))
                result.SkippedSubDirs.Add(Path.GetFileName(absSub));

            // 校验：含 PNG 源
            if (pngAssetPaths.Count == 0)
            {
                result.ErrorMessage = "目录内无 PNG 切图。";
                return result;
            }

            // 确定性：按文件名 Ordinal 排序后再喂 PackTextures
            pngAssetPaths.Sort(StringComparer.Ordinal);

            // 校验：子图名唯一(去扩展名)
            var nameSet = new HashSet<string>();
            foreach (var p in pngAssetPaths)
            {
                string spriteName = Path.GetFileNameWithoutExtension(p);
                if (!nameSet.Add(spriteName))
                {
                    result.ErrorMessage = $"子图名重复：{spriteName}（去扩展名后文件名须唯一）。";
                    return result;
                }
            }

            // 校验 + 读源：每张须 Sprite 导入类型、读出像素与 border。源 isReadable=0 → 读 PNG 字节 LoadImage 到临时纹理，不动源 importer。
            var srcTextures = new List<Texture2D>(pngAssetPaths.Count);
            var spriteNames = new List<string>(pngAssetPaths.Count);
            var srcBorders = new List<Vector4>(pngAssetPaths.Count);
            var srcSizes = new List<Vector2Int>(pngAssetPaths.Count);
            try
            {
                foreach (var pngPath in pngAssetPaths)
                {
                    var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
                    if (importer == null || importer.textureType != TextureImporterType.Sprite)
                    {
                        result.ErrorMessage =
                            $"{Path.GetFileName(pngPath)} 非 Sprite 导入类型（无 spriteBorder 可继承）；请先把源导入设置好，工具不擅自改源导入。";
                        DestroyAll(srcTextures);
                        return result;
                    }

                    byte[] bytes = File.ReadAllBytes(ToAbsolute(pngPath));
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tex.LoadImage(bytes))
                    {
                        UnityEngine.Object.DestroyImmediate(tex);
                        result.ErrorMessage = $"{Path.GetFileName(pngPath)} 像素读取失败（PNG 解码失败）。";
                        DestroyAll(srcTextures);
                        return result;
                    }

                    srcTextures.Add(tex);
                    spriteNames.Add(Path.GetFileNameWithoutExtension(pngPath));
                    srcBorders.Add(importer.spriteBorder);   // importer 级，非 sprites[0].border(Single 下后者是占位 {0,0,0,0})
                    srcSizes.Add(new Vector2Int(tex.width, tex.height));
                }

                // 可选 border 覆盖(方案 A：目录内 _border_override.json)
                var overrides = ReadBorderOverrides(folderPath);

                // 排布：PackTextures 自动排到一张大图，返回归一化 rect[]
                var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Rect[] uvRects;
                try
                {
                    uvRects = sheet.PackTextures(srcTextures.ToArray(), Padding, MaxAtlasSize, false);
                }
                catch (Exception e)
                {
                    UnityEngine.Object.DestroyImmediate(sheet);
                    result.ErrorMessage = $"排布失败：{e.Message}（子图总面积可能超 {MaxAtlasSize}×{MaxAtlasSize}）。";
                    DestroyAll(srcTextures);
                    return result;
                }

                int sheetW = sheet.width, sheetH = sheet.height;
                if (uvRects == null || uvRects.Length != spriteNames.Count || sheetW > MaxAtlasSize || sheetH > MaxAtlasSize)
                {
                    UnityEngine.Object.DestroyImmediate(sheet);
                    result.ErrorMessage =
                        $"子图总面积超 {MaxAtlasSize}×{MaxAtlasSize}（排出 {sheetW}×{sheetH}），本屏切图过大或过多，需拆分目录 / 压缩源图。";
                    DestroyAll(srcTextures);
                    return result;
                }

                // 逐子图 SpriteRect：name / rect / pivot 居中 / alignment=Center / border 继承(+覆盖) / 唯一 spriteID
                // rect 位置取 PackTextures 归一化 rect 左下角(四舍五入到像素)，尺寸用源 PNG 实际尺寸：
                // 消除"归一化 × 表尺寸"的浮点取整偏差，保证 rect 尺寸严格 == 源尺寸(验收 R3)；并夹到表内防取整推出界。
                var spriteRects = new SpriteRect[spriteNames.Count];
                for (int i = 0; i < spriteNames.Count; i++)
                {
                    var uv = uvRects[i];
                    int w = srcSizes[i].x;
                    int h = srcSizes[i].y;
                    int x = Mathf.RoundToInt(uv.x * sheetW);
                    int y = Mathf.RoundToInt(uv.y * sheetH);
                    if (x + w > sheetW) x = sheetW - w;
                    if (y + h > sheetH) y = sheetH - h;
                    if (x < 0) x = 0;
                    if (y < 0) y = 0;

                    Vector4 border = srcBorders[i];
                    if (overrides != null && overrides.TryGetValue(spriteNames[i], out var ov))
                        border = ov;

                    spriteRects[i] = new SpriteRect
                    {
                        name = spriteNames[i],
                        rect = new Rect(x, y, w, h),
                        pivot = new Vector2(0.5f, 0.5f),
                        alignment = SpriteAlignment.Center,
                        border = border,
                        spriteID = GUID.Generate(),
                    };
                }

                // 写盘：EncodeToPNG → File.WriteAllBytes → ImportAsset
                byte[] png = sheet.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(sheet);
                File.WriteAllBytes(ToAbsolute(sheetAssetPath), png);
                AssetDatabase.ImportAsset(sheetAssetPath, ImportAssetOptions.ForceSynchronousImport);

                // 设 TextureImporter(对齐基准 Sheet_settings.png)
                var ti = (TextureImporter)AssetImporter.GetAtPath(sheetAssetPath);
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Multiple;
                ti.spritePixelsPerUnit = 100;
                ti.mipmapEnabled = false;
                ti.sRGBTexture = true;
                ti.alphaIsTransparency = true;
                ti.filterMode = FilterMode.Bilinear;
                ti.maxTextureSize = MaxAtlasSize;

                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = new Vector2(0.5f, 0.5f);
                ti.SetTextureSettings(settings);

                // 写子图元数据：现代 API ISpriteEditorDataProvider(非 obsolete spritesheet)。
                // 只写 N 个命名子图，不依赖 Unity 自动切，故无 Sheet_xxx_0..N 残留名。
                var factory = new SpriteDataProviderFactories();
                factory.Init();
                var dataProvider = factory.GetSpriteEditorDataProviderFromObject(ti);
                dataProvider.InitSpriteEditorDataProvider();
                dataProvider.SetSpriteRects(spriteRects);
                dataProvider.Apply();

                EditorUtility.SetDirty(ti);
                ti.SaveAndReimport();

                result.Success = true;
                result.SheetAssetPath = sheetAssetPath;
                result.SpriteCount = spriteRects.Length;
            }
            finally
            {
                DestroyAll(srcTextures);
            }

            // 重建模拟清单：EditorSimulateMode 下 location 立即可寻址(Play 启动也会自动重建，故 EditMode 测试可跳过)
            if (simulateBuild)
            {
                AssetDatabase.Refresh();
                try
                {
                    YooAsset.EditorSimulateModeHelper.SimulateBuild(DefaultPackageName);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UIAtlasPacker] SimulateBuild 失败(不影响产出表，Play 启动会自动重建)：{e.Message}");
                }
            }

            return result;
        }

        // ───────────────────────── 辅助 ─────────────────────────

        private static bool IsUnderOutputTree(string folderPath)
        {
            folderPath = folderPath.Replace("\\", "/").TrimEnd('/');
            string prefix = OutputDirAssetPath + "/";
            return folderPath.StartsWith(prefix, StringComparison.Ordinal) && folderPath.Length > prefix.Length;
        }

        /// <summary>
        /// 读目录内可选的 _border_override.json：{"chat":[8,8,8,8],"help":[12,12,12,12]}。
        /// 不存在/解析失败 → 返回 null(全继承源)。
        /// 形态是「子图名 → 四元数组」的扁平字典，JsonUtility 不吃此形态，故手解。
        /// </summary>
        private static Dictionary<string, Vector4> ReadBorderOverrides(string folderPath)
        {
            string overridePath = ToAbsolute($"{folderPath}/{BorderOverrideFileName}");
            if (!File.Exists(overridePath)) return null;

            try
            {
                string json = File.ReadAllText(overridePath);
                var map = new Dictionary<string, Vector4>();
                int i = 0, n = json.Length;
                while (i < n)
                {
                    int keyStart = json.IndexOf('"', i);
                    if (keyStart < 0) break;
                    int keyEnd = json.IndexOf('"', keyStart + 1);
                    if (keyEnd < 0) break;
                    string key = json.Substring(keyStart + 1, keyEnd - keyStart - 1);

                    int arrStart = json.IndexOf('[', keyEnd);
                    if (arrStart < 0) break;
                    int arrEnd = json.IndexOf(']', arrStart);
                    if (arrEnd < 0) break;
                    string inner = json.Substring(arrStart + 1, arrEnd - arrStart - 1);

                    var nums = inner.Split(',');
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    var ns = System.Globalization.NumberStyles.Float;
                    if (nums.Length >= 4
                        && float.TryParse(nums[0].Trim(), ns, ci, out var l) && float.TryParse(nums[1].Trim(), ns, ci, out var b)
                        && float.TryParse(nums[2].Trim(), ns, ci, out var r) && float.TryParse(nums[3].Trim(), ns, ci, out var t))
                    {
                        map[key] = new Vector4(l, b, r, t);
                    }
                    i = arrEnd + 1;
                }
                return map.Count > 0 ? map : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UIAtlasPacker] {BorderOverrideFileName} 解析失败，按全继承处理：{ex.Message}");
                return null;
            }
        }

        private static void DestroyAll(List<Texture2D> textures)
        {
            foreach (var t in textures)
                if (t != null) UnityEngine.Object.DestroyImmediate(t);
            textures.Clear();
        }

        private static string ToAbsolute(string assetPath)
        {
            // Application.dataPath = <project>/Assets；assetPath 以 "Assets/" 开头
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath).Replace("\\", "/");
        }
    }
}
