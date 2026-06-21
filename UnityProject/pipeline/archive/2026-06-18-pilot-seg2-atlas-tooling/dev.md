# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:试点段二·atlas 工程件(dev-test)

设计基线:`C:\Users\pc\.claude\plans\ai-ai-unity-ai-ui-rippling-thimble.md`「段二·Unity 工程」+ design-docs/{23,24,28}。
范围三件(A/B/C),明确排除 comfy_client.py 与底图运行时钩子。

> **boss 交叉检返工 #1 已完成(本轮)**:B/C 的「Sheet 命名对齐」上一轮只删单数残留表、未触源文件夹名,生产链「folder→Pack→表名」仍断。本轮根治:源文件夹 `setting`→`settings`(MoveAsset 保 GUID)+ 删旧表真跑 `Pack("settings")` 端到端产出 `Sheet_settings.png`(21 子图、6 border 源继承)。A 合格、未动。详见下方 B/C 节。

### 接法预验结论(第一步,已确认可行,PASS)

Auto9Slicer `Slicer.Slice(Texture2D, SliceOptions)` 是**静态纯函数**,返回新 `SlicedTexture`,**不改输入纹理、不碰任何资产**。
- `SlicedTexture.Border.ToVector4()` = `new Vector4(Left, Bottom, Right, Top)` = (l,b,r,t),与 `UIAtlasPacker.cs:357-364` 的 `_border_override.json` 取值序 [l,b,r,t] **零转置对齐**。
- 「Slice 会裁图+覆盖源」的破坏式行为只存在于上游 `Auto9SliceTester.cs`(它在 Slice 之后 `File.WriteAllBytes(源PNG)` + 改 `importer.spriteBorder`)。**本工程不内置 Tester**,只内置 Slicer/SlicedTexture/SliceOptions 三件,只调 `Slicer.Slice` 读 `.Border`、丢弃 `.Texture` → 天然绕开所有写回。border-only 提取按设计可行,无需替代接法。

### A — Auto9Slicer→_border_override.json 胶水(完成)

- **引入方式**:vendor kyubuns/Auto9Slicer v1.1.1 (MIT) 库核心 3 个源到 `Assets/Editor/Auto9Slicer/`(独立 Editor-only asmdef `Auto9Slicer.Editor`,`autoReferenced:false`)。**刻意不内置 `Auto9SliceTester.cs`**(破坏式写回工具)。
- **新工具**:`UIAtlasPackerTool.BorderOverrideGenerator`(`Assets/Editor/UIAtlasPacker/BorderOverrideGenerator.cs`)。
  - 入口:菜单 `Tools/UI/生成 9-slice border 覆盖(Auto9Slicer -> _border_override.json)`(选中切图目录后执行);核心是可 EditMode 直调的静态 `Generate(folderPath, SliceOptions options=null)`。
  - 行为:对目录每张 PNG(Ordinal 排序)读字节 `LoadImage` 到临时可读纹理(源 isReadable=0,不改源 importer)→ `Slicer.Slice` 只读 `Border.ToVector4()` → `DestroyImmediate` 丢弃裁剪 Texture 与临时输入纹理 → 非零 border 写 `_border_override.json`(键=文件名去扩展名,值=[l,b,r,t],标准 json 无注释,格式与 packer 的 `ReadBorderOverrides` 手解对齐)。
  - **非破坏式(已 git+断言双证)**:绝不回写源 PNG、绝不改源 importer.spriteBorder。
  - **误检处理**:`GenerateResult` 把每个非零探测标 `LowConfidence`(四向不全相等 / 部分方向有 border = 疑似误检);高置信仅扁平轴对称四向相等(如 base_plate [15,15,15,15])。低置信信息走返回值/菜单弹窗上报,不污染 json。json 是非破坏式、人可编辑的单一调参源——人工据上报在 json 内手填校正。

### B — Sheet 命名对齐(真·名对齐:源文件夹改名 setting→settings)

**根因(boss 交叉检 #1 指出,本轮根治)**:`UIAtlasPacker.cs:113-114` 按 folder 名派生表名(`Path.GetFileName(folderPath)` → `Sheet_{folderName}.png`)。源文件夹叫 `setting` → Pack 必产 `Sheet_setting`(单数),与 5 窗口 const + prefab GUID 引用的 `Sheet_settings`(复数)错配。上一轮只删了单数残留表、未触文件夹名 → 生产链「folder→Pack→表名」仍断:将来真素材落进 `setting/` 再 Pack 会重建错名表、破窗口寻址。**补丁救不了,必须从源头(文件夹名)对齐。**

**本轮处置**:
- **重命名源文件夹 `setting` → `settings`**(`AssetDatabase.MoveAsset`,GUID 保持 `bc32ca61...` 不变;21 张 PNG 各自 .meta GUID 随文件移动不变)。改后生产路径全链一致:folder `settings` → `Sheet_settings`(Pack 产出名)= 5 窗口 const = prefab GUID 指向表。即插即用链打通。
- **现行表 = `Sheet_settings.png`(复数)**:5 窗口 `const Atlas="Sheet_settings"`(SettingsWindow/GameOverWindow/MergeOrderWinWindow/PlayerInfoWindow/RankWindow)+ `PlayerInfoWindow.prefab` 按 GUID `1025c762...` 引用(7 处)。重 Pack 后该表 GUID 仍 `1025c762...`(删 .png 重写字节、.meta 留存 → GUID 不变,git diff HEAD 该 .meta 为空),prefab GUID 引用未断;窗口按名寻址不受重 Pack 布局变化影响。
- **单数表 `Sheet_setting.png` 上一轮已删**(本轮无需再动)。改名后绝不会再生成单数表(folder 已是 `settings`)——find 全树零 `Sheet_setting.png`。
- **副作用(已查明、保留)**:TEngine `EditorSpriteSaveInfo`(AtlasMaker,import 钩子)按 folder 名自动派生 `Atlas_<folder>.spriteatlasv2`。改名触发它把 `Atlas_setting.spriteatlasv2` 自动重生成为 `Atlas_settings.spriteatlasv2`(在 `Assets/AssetArt/Atlas/`)。此 SpriteAtlas v2 非寻址路径(设计明确不用、`SetSubSprite` 对其返 NULL,见 memory),无任何代码按名引用 → 是框架自动产物,改名后命名更对齐,保留。`ProjectSettings/EditorBuildSettings.asset` 仅 CRLF 行尾噪声(numstat 0/0),已 `git checkout` 还原。

**配套测试改动(本轮)**:`UIAtlasPackerTests.cs` 核心锚源目录常量 `SourceSettingDir` 由 `/setting` 改 `/settings`(R2 据它读 21 源文件名集合做比对,不改则空集 → R2 挂);S2b 注释「手工调定值」订正为「从源 importer.spriteBorder 继承」(重 Pack 后 border 来自源继承,非表上手调);`BorderOverrideGenerator.cs` / `UIAtlasPacker.cs` doc-comment 示例路径 setting→settings。

### C — Pack 链路端到端验证(真跑 Pack 产出可寻址表 + EditMode 测试)

- **端到端真证(本轮新做)**:删现行 `Sheet_settings.png`(Pack 拒覆盖,先删)→ `UIAtlasPacker.Pack("Assets/AssetRaw/UIRaw/Atlas/settings", true)` → 产出 `Sheet_settings.png`。读回 live `GetSpriteRects` 核对:**子图数 21**、名集合 == 21 源文件名集合、**零 `_N` 残留名**、**border 6×24 + 15×0**(base_plate/base_plate2/base_plate3/box1/box2/button 从源 importer.spriteBorder 继承 {24,24,24,24},其余 15 张 0;无 override json)、importer = Sprite/Multiple/2048。`LoadAllAssetRepresentations` 二次数得 21 子精灵(交叉印证无丢图,防 tarot-mode 那条「单图超表静默丢图」)。这是 C 要求的端到端证明:用现有切图、真 Pack、产出窗口可寻址的表、命名按 B 对齐。
- **子图数 = 21**(按 `settings/` 目录实际 PNG 数推导,非硬编码)。设计稿 23/28 写「22 张」是规划期估值,目录实际 21、live 表实际 21 子图——按 brief「勿硬编码、按目录推导」取 21,实现层据现状解消,非 designFlaw。
- **Pack 链路 EditMode**:`R2`(21 命名子图无残留名,据 `SourceSettingDir=/settings` 比对)+ `S2b`(border 6×24+15×0)对 `Sheet_settings.png` 验证通过;`I1` 验证 A 写的 json 经 `UIAtlasPacker.Pack` 读回并覆盖子图 border(A→Pack 真实集成点)。
- **新测文件**:`Assets/Editor/Tests/UIAtlasPacker/BorderOverrideGeneratorTests.cs`(6 例:G1 扫描写出 / G2 探测确定性 / G3 置信分级 / N1 非破坏式 / I1 与 packer 往返 / V1 无 PNG 中止),对既有固定夹具 `_uiap_test_fixture/` 跑。
- **测试总数**:`UIAtlasPacker.Tests` 程序集 18 例(既有 12 + 新 6)**全绿**(本轮改名 + 测试锚订正后复跑 18/18 passed)。

## 改动文件清单

**新增**:
- `Assets/Editor/Auto9Slicer/Slicer.cs` / `SlicedTexture.cs` / `SliceOptions.cs`(vendor 库核心,不含 Tester)
- `Assets/Editor/Auto9Slicer/Auto9Slicer.Editor.asmdef`(独立 Editor-only 程序集)
- `Assets/Editor/UIAtlasPacker/BorderOverrideGenerator.cs`(border 生成胶水工具)
- `Assets/Editor/Tests/UIAtlasPacker/BorderOverrideGeneratorTests.cs`(6 例 EditMode)

**修改**:
- `Assets/Editor/UIAtlasPacker/UIAtlasPacker.Editor.asmdef`(references 加 `Auto9Slicer.Editor`)
- `Assets/Editor/Tests/UIAtlasPacker/UIAtlasPacker.Tests.asmdef`(references 加 `Auto9Slicer.Editor`)
- `Assets/Editor/Tests/UIAtlasPacker/UIAtlasPackerTests.cs`(核心锚 Sheet_setting→Sheet_settings、S2b 简化、删 ReferenceSheet 常量;**本轮**:`SourceSettingDir` /setting→/settings、S2b border 注释订正为「源 importer 继承」、注释路径 setting→settings)
- `Assets/Editor/Tests/UIAtlasPacker/BorderOverrideGeneratorTests.cs`(**本轮**:注释 setting→settings)
- `Assets/Editor/UIAtlasPacker/BorderOverrideGenerator.cs` / `UIAtlasPacker.cs`(**本轮**:doc-comment 示例路径 setting→settings)

**重命名(本轮,MoveAsset 保 GUID)**:
- `Assets/AssetRaw/UIRaw/Atlas/setting/` → `settings/`(folder GUID `bc32ca61...` 不变,21 PNG .meta GUID 随移动不变)

**重生成(本轮,删旧表真跑 Pack)**:
- `Assets/AssetRaw/UIRaw/Atlas/Sheet_settings.png`(删旧→Pack 重产;.meta GUID `1025c762...` 不变 → prefab GUID 引用不断)

**框架自动产物(本轮副作用,保留)**:
- `Assets/AssetArt/Atlas/Atlas_setting.spriteatlasv2` → `Atlas_settings.spriteatlasv2`(TEngine AtlasMaker 按 folder 名自动重生成;非寻址路径,无代码引用)

**删除(上一轮)**:
- `Assets/AssetRaw/UIRaw/Atlas/Sheet_setting.png`(+.meta)(单数重复表;本轮已确认改名后不会再生成)

## 验证点(给 test 复核)

| 项 | 怎么验 | 预期 |
|----|--------|------|
| 编译 0 错 | `read_console` filter CS | 无 CSxxxx(`disposed object` 是桥重连瞬态,非编译错) |
| EditMode 全绿 | `run_tests` assembly=`UIAtlasPacker.Tests`(先确认非 Play Mode) | 18/18 passed(本轮已复跑通过) |
| **B 真·名对齐(本轮核心)** | `ls Atlas/` 看源文件夹名 + Pack 产出名 | 源文件夹 = `settings/`(非 `setting`);Pack(`settings`) 产出 = `Sheet_settings.png`,与 5 窗口 const 一致。`find Assets -name Sheet_setting.png` 零命中(改名后不会再生成单数表) |
| **C 端到端(本轮核心)** | `execute_code` 删 `Sheet_settings.png` → `UIAtlasPacker.Pack(".../settings", true)` → 读回 live `GetSpriteRects` | Success、SpriteCount=21、名集合==21 源文件名、零 `_N` 残留名、border 6×24+15×0(6 张 plate/box/button 从源 importer 继承)、`LoadAllAssetRepresentations`=21(交叉印证无丢图) |
| **重 Pack 未破寻址(本轮)** | `git diff HEAD` 看 `Sheet_settings.png.meta` GUID;grep prefab 引用 | .meta GUID 仍 `1025c762...`(空 diff);`PlayerInfoWindow.prefab` 7 处 GUID 引用未断;5 窗口 const = `Sheet_settings` 全在 |
| A 工具可达 | EditMode 直调 `BorderOverrideGenerator.Generate("Assets/AssetRaw/UIRaw/Atlas/settings", null)` | Success=true、Scanned=21、NonZero=10。**跑后须删掉它写出的 settings/_border_override.json**(见下「遗留陷阱」) |
| A 非破坏式 | Generate 后 `git status settings/` | 源 PNG / .meta 零改动 |

### 已过的异常路径(挑最可能崩的手验,均通过)

- **改名 GUID 漂移(本轮最可能崩的一类)**:`MoveAsset` 后 folder GUID + 21 PNG .meta GUID 全保不变;删表重 Pack 后 `Sheet_settings.png.meta` GUID 不变(git diff HEAD 空)→ prefab GUID 引用未断。**这是「即插即用链」是否真通的关键证据**:链通 = 源文件夹→Pack→表名→代码/prefab 引用 四环 GUID/名全对齐。
- 重 Pack 静默丢图(tarot-mode 那条):`SpriteCount` 报 21 + `LoadAllAssetRepresentations` 二次数 21,两法一致 → 无图被丢。
- 空目录 / 无 PNG 目录 → `Generate`/`Pack` 中止,不写产物(EditMode V 组覆盖)。
- `null` options → `?? SliceOptions.Default` 兜底,不 NRE。
- 重复运行 Pack(表已存在)→ **拒覆盖中止**(V2 例);Generate 写 json 则覆盖幂等 → 见下陷阱。

## Play 手验步骤(交 test 执行)

1. 确认非 Play Mode → 进 Play(经 GameApp 启动,YooAsset SetDefaultPackage 生效)。
2. 从主菜单/HUD 设置入口打开设置窗(`ShowUIAsync<SettingsWindow>`)。
3. 看图标/底板/框是否按 `SetSubSprite("Sheet_settings", 名)` 正确显示(本轮重 Pack 是等价再生:21 子图同源、6 border 同值、表 GUID 不变 → 寻址不应受影响)。
4. 带 border 的底板/框 9-slice 缩放无变形:box1/box2/base_plate/base_plate2/base_plate3/button(现行表内 border={24,24,24,24},本轮已核实从源 importer 继承)。
> 运行期取图非 null 必须 Play 桥验:EditMode 下 YooAsset default package 未初始化(`SetSubSprite` 会抛 "Default package is null"),故 P 组寻址留 Play。

## 标注

- **热更程序集**:不涉及。新增/改动全在 Editor-only 程序集(`Auto9Slicer.Editor` / `UIAtlasPacker.Editor` / `UIAtlasPacker.Tests`),不打包、不热更。
- **Luban**:不涉及。
- **遗留陷阱(须告知人工/test)**:`BorderOverrideGenerator` 是「**草稿生成器**」——重跑会**覆盖**目录内 json(含人工已手填的校正)。且对 `settings/` 实跑的自动探测值与该屏 6 张 plate/box/button 源 importer 的 {24,24,24,24} **不一致**(自动探测在 box1/box2/button 上得 0、在 icon 上误检出 border)。故:① **绝不在 `settings/` 留 `_border_override.json`**——本轮 Pack 正是靠「无 override → 6 border 从源 importer 直继承」产出正确表;若有人跑 Generator 留下 json,下次重 Pack 会用错 border(自动探测值)覆盖掉源继承值。验证用的 json 跑完即删。② 此工具定位是**新屏**切图的 border 标注起点(那些源 importer 尚无 border 的屏),产物须人工核(尤其低置信项)后才作准——与设计「Auto9Slicer 只省 border 标注、华丽/胶囊手填」一致。setting 这一屏的 6 border 已在源 importer 里、无需 Generator。
- **无 designFlaw 上报**:子图数 22→21、自动探测 border≠源 importer border 两处「设计预期 vs 现状」差异均在实现层据目录/像素现状解消,brief 已预期(「勿硬编码按目录推导」「人工微调起点」),非设计层错。本轮根因(folder 名错配)亦是实现层修复(改名),非设计层错。

## 自检(交接前)

- [x] 编译 0 CSxxxx 错(改名 + 测试锚订正后强制 refresh+compile、域重载完成 idle、console 零 CS)
- [x] EditMode 18/18 全绿(本轮改名 + `SourceSettingDir` 订正后复跑 passed)
- [x] **本轮核心路径自跑**:`MoveAsset` 改名(GUID 保)→ 删旧表 → 真跑 `Pack("settings")` → 读回 live 子图核对(21/名集合/零残留名/6×24+15×0/无丢图)
- [x] **即插即用链端到端通**:源文件夹 `settings`→Pack→`Sheet_settings.png`→5 窗口 const + prefab GUID `1025c762...`(未断)四环对齐
- [x] 异常路径手验(改名 GUID 漂移 / 重 Pack 丢图 / 空/null/重复中止)
- [x] 改动集干净(还原了 Unity 顺手改的 EditorBuildSettings CRLF 噪声;AtlasMaker 自动重生成的 spriteatlasv2 是框架产物、保留)
- [x] 新资产 .meta 齐全;改名后 21 PNG + folder + 产出表 .meta GUID 全核
- [x] conventions 收尾过一遍本文件
