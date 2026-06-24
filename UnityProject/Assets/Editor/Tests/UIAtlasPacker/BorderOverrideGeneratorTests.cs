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
    /// BorderOverrideGenerator（Auto9Slicer → _border_override.json 胶水）EditMode 验收。
    ///
    /// 夹具：复用 packer 测试的固定只读小夹具 <c>_uiap_test_fixture/</c>（6 张唯一命名 PNG）。
    /// 这 6 张取自真实 settings/ 切图，像素固定 → Auto9Slicer 探测结果确定性可断言。
    /// 探测值（默认 SliceOptions：Tolerate0/Margin2/CenterSize2）：
    ///   uiap_plate [15,15,15,15]（四向相等→高置信，源自 base_plate）；
    ///   uiap_clear [26,26,26,32]、uiap_help [2,2,59,68]（非对称→低置信）；
    ///   uiap_btn / uiap_chat / uiap_x → border 全 0（不写入 json）。
    ///
    /// 每例 TearDown 删产出 _border_override.json，夹具源保留。
    /// 非破坏式由 git 工作树兜底 + N1 例显式断言源 importer.spriteBorder 与源 PNG 字节不变。
    ///
    /// 当前 [Explicit]：夹具 _uiap_test_fixture/ 未入库，无头环境不可运行；提供真实夹具后移除类级 [Explicit] 即恢复。
    /// </summary>
    [TestFixture]
    [Explicit("依赖未入库的切图夹具 _uiap_test_fixture/(6 张特定像素 PNG)，不在仓库且无 git 历史，无头/CI 环境无法运行。" +
              "断言锚在 Auto9Slicer 对具体像素的探测值(如 uiap_plate [15,15,15,15]、uiap_help [2,2,59,68])，" +
              "不能凭空造图凑绿；需提供真实夹具后移除本标记。")]
    public class BorderOverrideGeneratorTests
    {
        private const string AtlasRoot = "Assets/AssetRaw/UI/Atlas";
        private const string FixtureDir = AtlasRoot + "/_uiap_test_fixture";
        private static string OverrideJson => FixtureDir + "/" + UIAtlasPacker.BorderOverrideFileName;

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(ToAbs(OverrideJson)))
            {
                AssetDatabase.DeleteAsset(OverrideJson);
                AssetDatabase.Refresh();
            }
        }

        // ───────────────────────── G 组：探测与写出 ─────────────────────────

        [Test]
        public void G1_Generate_ScansAllPng_WritesJson()
        {
            var r = BorderOverrideGenerator.Generate(FixtureDir, Auto9Slicer.SliceOptions.Default);
            Assert.IsTrue(r.Success, "探测应成功：" + r.ErrorMessage);
            Assert.AreEqual(6, r.ScannedPngCount, "扫到 6 张 PNG");
            Assert.AreEqual(OverrideJson, r.OverrideJsonPath, "产出 json 路径在目录内");
            Assert.IsTrue(File.Exists(ToAbs(r.OverrideJsonPath)), "json 文件应生成");
        }

        [Test]
        public void G2_DetectedBorders_AreDeterministic()
        {
            var r = BorderOverrideGenerator.Generate(FixtureDir, Auto9Slicer.SliceOptions.Default);
            Assert.IsTrue(r.Success, r.ErrorMessage);

            var nz = r.NonZeroBorders.ToDictionary(e => e.SpriteName, e => e.Border);
            Assert.AreEqual(3, nz.Count, "3 个非零 border");
            Assert.AreEqual(new Vector4(15, 15, 15, 15), nz["uiap_plate"], "uiap_plate [15,15,15,15]");
            Assert.AreEqual(new Vector4(26, 26, 26, 32), nz["uiap_clear"], "uiap_clear [26,26,26,32]");
            Assert.AreEqual(new Vector4(2, 2, 59, 68), nz["uiap_help"], "uiap_help [2,2,59,68]");

            // 全 0 的不写入、归 ZeroBorderSprites
            var zero = r.ZeroBorderSprites.ToHashSet();
            Assert.AreEqual(3, zero.Count, "3 个 border 全 0");
            Assert.IsTrue(zero.Contains("uiap_btn") && zero.Contains("uiap_chat") && zero.Contains("uiap_x"),
                "uiap_btn / uiap_chat / uiap_x border 为 0");
        }

        [Test]
        public void G3_Confidence_FlagsAsymmetricAsLow()
        {
            var r = BorderOverrideGenerator.Generate(FixtureDir, Auto9Slicer.SliceOptions.Default);
            Assert.IsTrue(r.Success, r.ErrorMessage);
            var conf = r.NonZeroBorders.ToDictionary(e => e.SpriteName, e => e.LowConfidence);

            // 四向相等 → 高置信；非对称（部分方向 / 四向不等）→ 低置信，留人工核
            Assert.IsFalse(conf["uiap_plate"], "uiap_plate 四向相等 → 高置信");
            Assert.IsTrue(conf["uiap_clear"], "uiap_clear 四向不等 → 低置信");
            Assert.IsTrue(conf["uiap_help"], "uiap_help 非对称 → 低置信");
        }

        // ───────────────────────── N 组：非破坏式 ─────────────────────────

        [Test]
        public void N1_DoesNotMutateSource_PngBytes_NorImporterBorder()
        {
            // 记录源 PNG 字节 + 源 importer.spriteBorder（探测前）
            string platePng = FixtureDir + "/uiap_plate.png";
            byte[] beforeBytes = File.ReadAllBytes(ToAbs(platePng));
            var beforeBorder = ((TextureImporter)AssetImporter.GetAtPath(platePng)).spriteBorder;

            var r = BorderOverrideGenerator.Generate(FixtureDir, Auto9Slicer.SliceOptions.Default);
            Assert.IsTrue(r.Success, r.ErrorMessage);

            byte[] afterBytes = File.ReadAllBytes(ToAbs(platePng));
            var afterBorder = ((TextureImporter)AssetImporter.GetAtPath(platePng)).spriteBorder;

            Assert.AreEqual(beforeBytes, afterBytes, "源 PNG 字节不变（绝不回写源）");
            Assert.AreEqual(beforeBorder, afterBorder, "源 importer.spriteBorder 不变（绝不改源导入设置）");
        }

        // ───────────────────────── I 组：与 packer 的契约（往返）─────────────────────────

        // 探测写出的 json，UIAtlasPacker.Pack 能读回并按其覆盖打表子图的 border。
        // 这是 A 与 packer 的真实集成点：验证 BuildJson 格式与 ReadBorderOverrides 解析对齐。
        [Test]
        public void I1_GeneratedJson_ConsumedByPacker_OverridesBorder()
        {
            // 先探测写 json（含 uiap_plate [15,15,15,15] 等）
            var gen = BorderOverrideGenerator.Generate(FixtureDir, Auto9Slicer.SliceOptions.Default);
            Assert.IsTrue(gen.Success, gen.ErrorMessage);

            // 用同一目录打表（夹具产出表 Sheet__uiap_test_fixture.png）
            string sheet = AtlasRoot + "/Sheet__uiap_test_fixture.png";
            if (File.Exists(ToAbs(sheet)))
            {
                AssetDatabase.DeleteAsset(sheet);
                AssetDatabase.Refresh();
            }
            var pack = UIAtlasPacker.Pack(FixtureDir, simulateBuild: false);
            try
            {
                Assert.IsTrue(pack.Success, "打表应成功：" + pack.ErrorMessage);

                var produced = ReadSpriteRects(pack.SheetAssetPath).ToDictionary(m => m.name, m => m.border);

                // 被 json 覆盖的子图：取 json 值（而非源 importer 值）
                Assert.AreEqual(new Vector4(15, 15, 15, 15), produced["uiap_plate"],
                    "uiap_plate border 取 json 覆盖值 [15,15,15,15]");
                Assert.AreEqual(new Vector4(26, 26, 26, 32), produced["uiap_clear"],
                    "uiap_clear border 取 json 覆盖值 [26,26,26,32]（源 importer 为 0，证明覆盖生效）");

                // 未被 json 覆盖的子图：仍继承源 importer.spriteBorder
                // uiap_btn 源 importer=24、json 未含 → 应保持 24
                Assert.AreEqual(new Vector4(24, 24, 24, 24), produced["uiap_btn"],
                    "uiap_btn 不在 json 中 → 继承源 importer 24");
            }
            finally
            {
                if (File.Exists(ToAbs(pack.SheetAssetPath ?? sheet)))
                {
                    AssetDatabase.DeleteAsset(pack.SheetAssetPath ?? sheet);
                    AssetDatabase.Refresh();
                }
            }
        }

        // ───────────────────────── V 组：校验中止 ─────────────────────────

        [Test]
        public void V1_NoPngFolder_Aborts()
        {
            string noPngDir = AtlasRoot + "/uiap_bog_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            Directory.CreateDirectory(ToAbs(noPngDir));
            File.WriteAllText(ToAbs(noPngDir + "/readme.txt"), "no png");
            AssetDatabase.Refresh();
            try
            {
                var r = BorderOverrideGenerator.Generate(noPngDir, Auto9Slicer.SliceOptions.Default);
                Assert.IsFalse(r.Success, "无 PNG 目录应中止");
                Assert.IsNull(r.OverrideJsonPath, "不写 json");
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(noPngDir)) AssetDatabase.DeleteAsset(noPngDir);
                AssetDatabase.Refresh();
            }
        }

        // ───────────────────────── 辅助 ─────────────────────────

        private static SpriteRect[] ReadSpriteRects(string assetPath)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
            dp.InitSpriteEditorDataProvider();
            return dp.GetSpriteRects();
        }

        private static string ToAbs(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath).Replace("\\", "/");
        }
    }
}
