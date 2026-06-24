using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UIAtlasPackerTool;

namespace UIAtlasPackerTool.Tests
{
    /// <summary>
    /// UIAtlasPacker 工具行为 EditMode 验收（设计 24 §9.1 A 档：C/R/S/V 组）。
    ///
    /// 夹具策略分两类，规避「同步测试方法内运行时建资产导入延迟」与「收集器同名资源冲突」：
    /// 1. 工具行为(R1/R3/S1/S3/V2/V3)对 <c>_uiap_test_fixture/</c>——一个提交进库的固定只读小夹具
    ///    (6 张唯一命名 PNG：uiap_plate/uiap_btn=border24，uiap_chat/uiap_help/uiap_clear/uiap_x=border0；
    ///    .meta 已设 Sprite/Single/border 并入库)。资产已入库 → Pack 内部 GetAtPath 即时可用、无 NRE；
    ///    文件名唯一(uiap_ 前缀避开 settings/ 与各屏) → 不触发 AddressByFileName 收录冲突。
    ///    每例 TearDown 只删产出表 Sheet__uiap_test_fixture.png，夹具保留。
    /// 2. 核心锚(R2/S2b)读对 settings/ 全集的现行生产表 Sheet_settings.png(21 命名子图、6×24+15×0、无残留名)，
    ///    不重跑 Pack——验「真实全集产出」语义。该表是 5 个窗口运行期 SetSubSprite 寻址的现行 atlas。
    /// 读回子图统一走现代 API ISpriteEditorDataProvider.GetSpriteRects()(与工具写路径同源)。
    /// 运行期寻址(P 组)留给 Play 环节，不在此处。
    /// </summary>
    [TestFixture]
    public class UIAtlasPackerTests
    {
        private const string AtlasRoot = "Assets/AssetRaw/UI/Atlas";

        // 固定只读小夹具(提交进库)：工具行为类测试对它打表。
        private const string FixtureFolderName = "_uiap_test_fixture";
        private const string FixtureDir = AtlasRoot + "/" + FixtureFolderName;
        private const string FixtureSheet = AtlasRoot + "/Sheet_" + FixtureFolderName + ".png";

        // 核心锚只读对象：settings/ 全集的现行生产表(窗口 SetSubSprite 实际寻址的 atlas)。
        private const string SourceSettingDir = AtlasRoot + "/settings";
        private const string ProducedFullSheet = AtlasRoot + "/Sheet_settings.png";

        // 夹具中两个九宫格底图(border=24)，其余 4 个 border=0。
        private static readonly HashSet<string> FixtureBorder24 = new HashSet<string>
        {
            "uiap_plate", "uiap_btn"
        };

        // settings/ 全集中六个九宫格底图(border=24)，其余 15 个 border=0(R2/S2 核心锚用)。
        private static readonly HashSet<string> SettingBorder24 = new HashSet<string>
        {
            "base_plate", "base_plate2", "base_plate3", "box1", "box2", "button"
        };

        [TearDown]
        public void TearDown()
        {
            // 只删本类测试可能产出的临时表，夹具(_uiap_test_fixture/)与核心锚表(Sheet_settings.png)均保留。
            if (File.Exists(ToAbs(FixtureSheet)))
            {
                AssetDatabase.DeleteAsset(FixtureSheet);
                AssetDatabase.Refresh();
            }
            // S3 在夹具目录内写过 _border_override.json，清掉以免影响后续例。
            string ovr = ToAbs(FixtureDir + "/" + UIAtlasPacker.BorderOverrideFileName);
            if (File.Exists(ovr))
            {
                AssetDatabase.DeleteAsset(FixtureDir + "/" + UIAtlasPacker.BorderOverrideFileName);
                AssetDatabase.Refresh();
            }
        }

        // 对固定夹具打表(工具行为类测试公用)。simulateBuild:false(EditMode 不重建模拟清单)。
        private static UIAtlasPacker.PackResult PackFixture()
        {
            return UIAtlasPacker.Pack(FixtureDir, simulateBuild: false);
        }

        private static TextureImporter ImporterOf(string assetPath)
        {
            return (TextureImporter)AssetImporter.GetAtPath(assetPath);
        }

        // 读回子图：现代 API ISpriteEditorDataProvider.GetSpriteRects()(与工具写路径同源)。
        private static SpriteRect[] ReadSpriteRects(string assetPath)
        {
            var ti = ImporterOf(assetPath);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
            dp.InitSpriteEditorDataProvider();
            return dp.GetSpriteRects();
        }

        // ───────────────────────── R 组：产出结构 ─────────────────────────

        [Test]
        public void R1_Importer_AlignsBaseline()
        {
            var r = PackFixture();
            Assert.IsTrue(r.Success, "打表应成功：" + r.ErrorMessage);
            Assert.AreEqual(FixtureSheet, r.SheetAssetPath, "产出表路径为 Sheet_<夹具目录名>.png");
            Assert.IsTrue(File.Exists(ToAbs(r.SheetAssetPath)), "产出表 PNG 应生成");

            var ti = ImporterOf(r.SheetAssetPath);
            Assert.AreEqual(TextureImporterType.Sprite, ti.textureType, "textureType==Sprite");
            Assert.AreEqual(SpriteImportMode.Multiple, ti.spriteImportMode, "spriteImportMode==Multiple");
            Assert.IsTrue(ti.sRGBTexture, "sRGBTexture==true");
            Assert.IsTrue(ti.alphaIsTransparency, "alphaIsTransparency==true");
            Assert.AreEqual(2048, ti.maxTextureSize, "maxTextureSize==2048");

            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            Assert.AreEqual(SpriteMeshType.FullRect, s.spriteMeshType, "spriteMeshType==FullRect");
        }

        // R2 核心锚：读 settings/ 全集的现行生产表 Sheet_settings.png(21 命名子图、无残留名)。
        [Test]
        public void R2_TwentyOneNamedSprites_NoResidualNames()
        {
            Assert.IsTrue(File.Exists(ToAbs(ProducedFullSheet)),
                "核心锚表 Sheet_settings.png 应存在(settings/ 全集的现行生产 atlas)");

            var metas = ReadSpriteRects(ProducedFullSheet);
            Assert.AreEqual(21, metas.Length, "正好 21 个 SpriteRect");

            var producedNames = metas.Select(m => m.name).ToHashSet();
            var sourceNames = Directory.GetFiles(ToAbs(SourceSettingDir), "*.png")
                .Select(Path.GetFileNameWithoutExtension).ToHashSet();
            Assert.IsTrue(producedNames.SetEquals(sourceNames),
                "name 集合 == 21 源文件名(去扩展名)集合");

            foreach (var name in producedNames)
                Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(name, @"_\d+$"),
                    $"不含自动切残留名(形如 Xxx_N)：{name}");
        }

        [Test]
        public void R3_RectSize_EqualsSourcePixelSize_NoOverlap_InBounds()
        {
            var r = PackFixture();
            Assert.IsTrue(r.Success, r.ErrorMessage);

            var sheetTex = AssetDatabase.LoadAssetAtPath<Texture2D>(r.SheetAssetPath);
            int sheetW = sheetTex.width, sheetH = sheetTex.height;

            var metas = ReadSpriteRects(r.SheetAssetPath);
            foreach (var m in metas)
            {
                string srcPng = FixtureDir + "/" + m.name + ".png";
                var (sw, sh) = ReadPngSize(ToAbs(srcPng));
                Assert.AreEqual(sw, (int)m.rect.width, $"{m.name} rect.width == 源宽");
                Assert.AreEqual(sh, (int)m.rect.height, $"{m.name} rect.height == 源高");

                Assert.GreaterOrEqual(m.rect.x, 0, $"{m.name} 在表内(x>=0)");
                Assert.GreaterOrEqual(m.rect.y, 0, $"{m.name} 在表内(y>=0)");
                Assert.LessOrEqual(m.rect.xMax, sheetW, $"{m.name} 在表内(xMax<=表宽)");
                Assert.LessOrEqual(m.rect.yMax, sheetH, $"{m.name} 在表内(yMax<=表高)");
            }

            // 两两不重叠
            for (int i = 0; i < metas.Length; i++)
                for (int j = i + 1; j < metas.Length; j++)
                    Assert.IsFalse(RectsOverlap(metas[i].rect, metas[j].rect),
                        $"{metas[i].name} 与 {metas[j].name} rect 不应重叠");
        }

        // ───────────────────────── S 组：语义字段 ─────────────────────────

        [Test]
        public void S1_PivotCentered_AlignmentCenter()
        {
            var r = PackFixture();
            Assert.IsTrue(r.Success, r.ErrorMessage);

            foreach (var m in ReadSpriteRects(r.SheetAssetPath))
            {
                Assert.AreEqual(new Vector2(0.5f, 0.5f), m.pivot, $"{m.name} pivot == (0.5,0.5)");
                Assert.AreEqual(SpriteAlignment.Center, m.alignment, $"{m.name} alignment == Center(0)");
            }
        }

        // S2a：对固定夹具打表，border 按源继承(2×24 + 4×0)。
        [Test]
        public void S2a_BorderInherited_FromFixtureSource()
        {
            var r = PackFixture();
            Assert.IsTrue(r.Success, r.ErrorMessage);

            var produced = ReadSpriteRects(r.SheetAssetPath).ToDictionary(m => m.name, m => m.border);
            Assert.AreEqual(6, produced.Count, "夹具 6 个子图");
            foreach (var kv in produced)
            {
                var expected = FixtureBorder24.Contains(kv.Key)
                    ? new Vector4(24, 24, 24, 24)
                    : Vector4.zero;
                Assert.AreEqual(expected, kv.Value, $"{kv.Key} border 应为 {expected}(从源 importer 继承)");
            }
        }

        // S2b 核心锚：settings/ 全集现行生产表 Sheet_settings.png 的子图 border 为 6×24 + 15×0(从源 importer.spriteBorder 继承：6 张 plate/box/button 源带 {24,24,24,24}，其余 15 张为 0)。
        [Test]
        public void S2b_BorderMatchesReferenceSheet_FullSet()
        {
            Assert.IsTrue(File.Exists(ToAbs(ProducedFullSheet)),
                "核心锚表 Sheet_settings.png 应存在");

            var produced = ReadSpriteRects(ProducedFullSheet).ToDictionary(m => m.name, m => m.border);
            Assert.AreEqual(21, produced.Count, "全集 21 子图");

            // 6 个九宫格底图 border=24、其余 15 个=0(从源 importer.spriteBorder 继承)
            foreach (var kv in produced)
            {
                var expected = SettingBorder24.Contains(kv.Key)
                    ? new Vector4(24, 24, 24, 24)
                    : Vector4.zero;
                Assert.AreEqual(expected, kv.Value, $"{kv.Key} border 应为 {expected}");
            }
        }

        [Test]
        public void S3_BorderOverride_TakesEffect()
        {
            // 目录内放覆盖配置：把 uiap_chat(源 0) 覆盖成 8、uiap_help(源 0) 覆盖成 12。
            string overridePath = ToAbs(FixtureDir + "/" + UIAtlasPacker.BorderOverrideFileName);
            File.WriteAllText(overridePath, "{\"uiap_chat\":[8,8,8,8],\"uiap_help\":[12,12,12,12]}");
            AssetDatabase.Refresh();

            var r = PackFixture();
            Assert.IsTrue(r.Success, r.ErrorMessage);

            var produced = ReadSpriteRects(r.SheetAssetPath).ToDictionary(m => m.name, m => m.border);
            Assert.AreEqual(new Vector4(8, 8, 8, 8), produced["uiap_chat"], "uiap_chat border 取覆盖值 8");
            Assert.AreEqual(new Vector4(12, 12, 12, 12), produced["uiap_help"], "uiap_help border 取覆盖值 12");
            // 其余仍继承源
            Assert.AreEqual(new Vector4(24, 24, 24, 24), produced["uiap_btn"], "uiap_btn 仍继承源 24");
            Assert.AreEqual(Vector4.zero, produced["uiap_clear"], "uiap_clear 仍继承源 0");
        }

        // ───────────────────────── V 组：校验与幂等 ─────────────────────────

        [Test]
        public void V1a_FolderNotUnderAtlasTree_Aborts()
        {
            // 在收录树外建目录(直接放 Assets 下)
            string outsideDir = "Assets/uiap_outside_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            Directory.CreateDirectory(ToAbs(outsideDir));
            AssetDatabase.Refresh();
            try
            {
                var r = UIAtlasPacker.Pack(outsideDir, simulateBuild: false);
                Assert.IsFalse(r.Success, "树外目录应中止");
                Assert.IsNull(r.SheetAssetPath, "不产出文件");
                StringAssert.Contains("Atlas", r.ErrorMessage);
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(outsideDir)) AssetDatabase.DeleteAsset(outsideDir);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void V1b_EmptyFolder_Aborts()
        {
            string emptyDir = AtlasRoot + "/uiap_empty_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            Directory.CreateDirectory(ToAbs(emptyDir));
            AssetDatabase.Refresh();
            try
            {
                var r = UIAtlasPacker.Pack(emptyDir, simulateBuild: false);
                Assert.IsFalse(r.Success, "空目录应中止");
                Assert.IsNull(r.SheetAssetPath, "不产出文件");
                Assert.IsFalse(File.Exists(ToAbs(AtlasRoot + "/Sheet_" + Path.GetFileName(emptyDir) + ".png")), "无产出表");
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(emptyDir)) AssetDatabase.DeleteAsset(emptyDir);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void V1c_NoPngFolder_Aborts()
        {
            string noPngDir = AtlasRoot + "/uiap_nopng_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            Directory.CreateDirectory(ToAbs(noPngDir));
            File.WriteAllText(ToAbs(noPngDir + "/readme.txt"), "no png here");
            AssetDatabase.Refresh();
            try
            {
                var r = UIAtlasPacker.Pack(noPngDir, simulateBuild: false);
                Assert.IsFalse(r.Success, "无 PNG 目录应中止");
                Assert.IsNull(r.SheetAssetPath, "不产出文件");
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(noPngDir)) AssetDatabase.DeleteAsset(noPngDir);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void V2_AlreadyExists_NoOverwrite_Aborts()
        {
            var first = PackFixture();
            Assert.IsTrue(first.Success, first.ErrorMessage);

            // 记录旧表字节，重跑应中止且旧表不变
            byte[] before = File.ReadAllBytes(ToAbs(first.SheetAssetPath));
            var second = PackFixture();
            Assert.IsFalse(second.Success, "表已存在应中止");
            StringAssert.Contains("已存在", second.ErrorMessage);
            byte[] after = File.ReadAllBytes(ToAbs(first.SheetAssetPath));
            Assert.AreEqual(before, after, "旧表内容不变");
        }

        [Test]
        public void V3_Deterministic_NameToRect_StableAcrossRuns()
        {
            var first = PackFixture();
            Assert.IsTrue(first.Success, first.ErrorMessage);
            var map1 = ReadSpriteRects(first.SheetAssetPath)
                .ToDictionary(m => m.name, m => m.rect);

            // 删旧重跑
            AssetDatabase.DeleteAsset(first.SheetAssetPath);
            AssetDatabase.Refresh();

            var second = PackFixture();
            Assert.IsTrue(second.Success, second.ErrorMessage);
            var map2 = ReadSpriteRects(second.SheetAssetPath)
                .ToDictionary(m => m.name, m => m.rect);

            Assert.AreEqual(map1.Count, map2.Count, "两次子图数一致");
            foreach (var kv in map1)
            {
                Assert.IsTrue(map2.ContainsKey(kv.Key), $"两次都含 {kv.Key}");
                Assert.AreEqual(kv.Value, map2[kv.Key], $"{kv.Key} 两次 rect 一致(确定性)");
            }
        }

        // ───────────────────────── 辅助 ─────────────────────────

        private static bool RectsOverlap(Rect a, Rect b)
        {
            return a.xMin < b.xMax && b.xMin < a.xMax && a.yMin < b.yMax && b.yMin < a.yMax;
        }

        private static (int, int) ReadPngSize(string absPath)
        {
            byte[] d = File.ReadAllBytes(absPath);
            // PNG IHDR：宽在偏移 16(4字节大端)、高在偏移 20
            int w = (d[16] << 24) | (d[17] << 16) | (d[18] << 8) | d[19];
            int h = (d[20] << 24) | (d[21] << 16) | (d[22] << 8) | d[23];
            return (w, h);
        }

        private static string ToAbs(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath).Replace("\\", "/");
        }
    }
}
