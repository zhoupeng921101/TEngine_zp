# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:ui-atlas-packer · Editor 散切图打表工具(2026-06-15)

设计稿:`design-docs/24-ui-atlas-packer.html`(已注册进 `design-docs/assets/nav.js` GROUPS「代码 / 工具」组)。
设计基线见设计稿 §立项信息;以下为交接区(给 dev / test 的验收标准)。

---

## 交接区(给 dev / test)

### 一句话目标
新建一个 Editor 菜单工具:输入散切图目录 `AssetRaw/UIRaw/Atlas/<screen>/` → 输出一张 Multiple 模式精灵表 PNG `Sheet_<目录名>.png`(落 `AssetRaw/UIRaw/Atlas/`),子图名=源文件名、自动排布切 rect、pivot 居中、border 从源 `TextureImporter.spriteBorder` 继承(+ 可选覆盖),设好 importer(Sprite/Multiple/FullRect)并 SimulateBuild。替代手工合表,服务后续约 20 屏 UI 换皮(设计 23 范式的生产工具)。

### 涉及模块 / 资源 / API(给 dev 定位)
- 新建工具脚本:`Assets/Editor/UIAtlasPacker/UIAtlasPacker.cs`(编辑器程序集,不打包不热更)。打表核心做成可调静态方法,菜单项 `[MenuItem]` 只是薄壳(便于 EditMode 测试直调)。
- 关键 API:`UnityEditor.TextureImporter` / `AssetImporter.GetAtPath` / `Texture2D.PackTextures` / `Texture2D.EncodeToPNG` / `TextureImporterSettings`(设 FullRect/Center)/ `AssetDatabase.ImportAsset`+`Refresh` / `YooAsset.EditorSimulateModeHelper.SimulateBuild("DefaultPackage")`(ResourceModule.cs:177 实证)。
- **写/读子图元数据用现代 API `UnityEditor.U2D.Sprites.ISpriteEditorDataProvider`(程序集 `Unity.2D.Sprite.Editor`,已经 unity_reflect 核实存在)**:`SpriteDataProviderFactories().Init()` → `GetSpriteEditorDataProviderFromObject(importer)` → `InitSpriteEditorDataProvider()` → `SetSpriteRects(SpriteRect[])`(name/rect/pivot/alignment/border)→ `Apply()` → `SaveAndReimport()`。**`TextureImporter.spritesheet`(`SpriteMetaData[]`)已 Obsolete**(unity_reflect `is_obsolete=true` + 官方文档「removed, use ISpriteEditorDataProvider」),仍可写作兜底,但两路同验收、实做以现代 API 为准(设计稿 §五 callout)。
- 收集器(✓ 不改):组 `UIRaw` 收 `Assets/AssetRaw/UIRaw/Atlas`(`AddressByFileName`+`PackDirectory`,`AssetBundleCollectorConfig.xml:32-34`)→ `Sheet_setting.png` 落该目录 → location=`Sheet_setting`(文件名)。
- 对照基准(✓ 不改):现有手工表 `Assets/AssetRaw/UIRaw/Atlas/Sheet_settings.png`(+ `.meta`)。importer:textureType=Sprite(8)/spriteMode=Multiple(2)/spriteMeshType=FullRect(1)/sRGB=1/alphaIsTransparency=1/maxTextureSize=2048/compressionQuality=50/filterMode=1/spriteExtrude=1/spritePixelsToUnits=100/alignment=0/spritePivot={0.5,0.5}。其 `spriteSheet.sprites` 实际 21 命名子图(6×border24 + 15×border0),另带 `Sheet_settings_0..25` 自动切残留名(只在 name 表、不在 sprites 列表,count=21)。
- 源 `setting/` 21 张 PNG 已是 Single Sprite(spriteMode=1)、pivot 居中、border 设对(6 底图 importer.spriteBorder={24}:base_plate/base_plate2/base_plate3/box1/box2/button;其余 15 图标={0})。源 `isReadable=0`(读像素须临时设 readable 后恢复,或读字节自行 LoadImage 到临时纹理,不动源 importer)。

### dev 须自行核实的工程出入(设计稿 §八 callout)
1. 源像素读取:源 `isReadable=0`,`PackTextures` 需可读源。取「临时设 readable 读完恢复」或「读 PNG 字节 LoadImage 到临时纹理(不动源,更干净)」。grep 工程有无同类读图工具可复用。
2. SimulateBuild 包名:勘察值 `"DefaultPackage"`(`ProcedureInitPackage.cs` 用 `_resourceModule.DefaultPackageName`);Editor 工具拿不到运行期模块时直接传字符串常量。

---

## 验收标准(逐条,test 可核对)

> 拆两档:工具行为 EditMode 可验(batchmode 跑 EditMode,直调打表入口 + `AssetImporter` 读回产出表断言);运行期寻址需 Play。核心锚(以 boss.md 为准):对 `setting/` 跑工具 → `Sheet_setting.png` 含 21 命名子图、寻址可取、各子图 rect/pivot/border 与现有 `Sheet_settings.png` 对应子图一致(6×border24 + 15×border0)、不带 `Sheet_settings_0..25` 残留。

### A. 工具行为可单测(EditMode)

| # | 验收点(完成定义) |
|---|------|
| C1 | `UIAtlasPacker.cs` 编译 0 error(编辑器程序集);现有 EditMode 全绿(零回归——工具在 Editor 区,不碰运行期/热更) |
| C2 | Code Review:工具只读源 importer 的 `spriteBorder`、不改源导入设置;产出落 `AssetRaw/UIRaw/Atlas/`;不触网;不改收集器/框架/既有 `Sheet_settings.png` |
| R1 | 对 `setting/`(21 源 PNG)跑工具 → `Assets/AssetRaw/UIRaw/Atlas/Sheet_setting.png` 生成;读回 importer:`textureType==Sprite` && `spriteImportMode==Multiple` && `spriteMeshType==FullRect` && `sRGBTexture==true` && `alphaIsTransparency==true` && `maxTextureSize==2048` |
| R2 | 读回子图(`ISpriteEditorDataProvider.GetSpriteRects()`):正好 **21** 个,其 `name` 集合 == 21 源文件名(去扩展名)集合;**不含任何 `Sheet_setting_0..N` / `Sheet_settings_0..25` 形态的自动切残留名**(对照现有 `Sheet_settings.png` 的 `Sheet_settings_0..25` 残留——产出表不得有,用户拍板③核心) |
| R3 | 每个子图 `SpriteMetaData.rect` 的 width/height == 对应源 PNG 像素尺寸(rect 坐标由排布定、**不要求**等于现有手工表坐标);各子图 rect 互不重叠且都在表尺寸内 |
| S1 | 每个子图 `pivot == {0.5,0.5}` && `alignment == Center(0)` |
| S2 | **border 继承 + 对照基准(核心锚)**:base_plate/base_plate2/base_plate3/box1/box2/button 六个子图 `border == {24,24,24,24}`;其余 15 个 == {0,0,0,0}。**与现有 `Sheet_settings.png` 对应子图逐一相等**(同时读两表子图集 `GetSpriteRects()` 按 name 配对断言)。对照真值可从源切图 importer 现读、不硬编码 |
| S3 | border 覆盖(若实现 §六 方案 A `_border_override.json`)生效:目录内放覆盖配置列某子图 → 产出该子图 border==覆盖值,其余仍继承源 |
| V1 | 对「非 `AssetRaw/UIRaw/Atlas/` 下目录」/「空目录」/「无 PNG 目录」跑 → 工具中止、不产出文件、给明确报错 |
| V2 | 表已存在时重跑 → 不覆盖、中止、报「已存在」;旧表内容不变(用户拍板:不覆盖) |
| V3 | 确定性:同一目录(删旧后)两次跑,产出表 子图 name→rect 映射一致(输入按文件名排序后喂 PackTextures) |

### B. 运行期寻址(需 Play;MCP 能 ShowUI+截图,不能模拟指针)

| # | 验收点 |
|---|------|
| P1 | Play 中对 `Sheet_setting` 调 `YooAssets.LoadSubAssetsAsync<Sprite>("Sheet_setting")`:SubAssets **count==21**(不含残留名) |
| P2 | 按名取得到:21 个名逐一 `GetSubAssetObject<Sprite>(名)` 返非 null;任取一名 `_img.SetSubSprite("Sheet_setting","button")` → Image 显示该子图 |
| P3 | 九宫格 border 生效:对 border=24 子图(如 `button`)用 `Image.type=Sliced` 拉伸,四角不糊、中段平铺 |

### 关单判据(给 boss)
R2 + S2 是核心(产出表读回 21 命名子图、border 与现表逐一相等、无残留名 = 精确复现手工表语义);P1/P2 证产出表运行期可寻址。二者齐 = 工具可替代手工合表。

### EditMode 测试落地提示(给 dev/test)
- 测试夹具:可直接用真实 `setting/`(21 张,核心锚以此为准),或建小夹具目录(几张含/不含 border 的 PNG)更快更隔离 → dev 取其一。
- 读回:`(TextureImporter)AssetImporter.GetAtPath(sheetPath)` 取 `spriteImportMode`,子图集经 `ISpriteEditorDataProvider.GetSpriteRects()` 读 name/rect/pivot/border(与写路径同源;obsolete 的 `.spritesheet` 读法可作兜底);border 对照基准对 `Sheet_settings.png` 同样 `GetSpriteRects()` 按 name 配对。
- 测试与工具程序集边界:`BlockBlast.Tests.asmdef` 引用 `GameLogic`/`GameProto`/`TEngine.Runtime`,**不**引用 Editor 工具程序集(`Assets/Editor` 无 asmdef 脚本编进 `Assembly-CSharp-Editor`)。故测试走「读回产物」(`AssetImporter`+`ISpriteEditorDataProvider`,UnityEditor 可达)而非「调内部逻辑」;若 dev 要直接单测打包逻辑,给工具加独立 asmdef 并被 Tests 引用(可选,非必须)。
- 测试产出表若不应入库 → `TearDown` 删除或写临时目录(R 组不依赖寻址,临时目录可;寻址类断言 P 组走 Play)。

---

## decisions(范围开关默认值,交 boss 关单复核;均有安全默认、不入 blockers)
- B1 pivot 覆盖:默认统一居中 {0.5,0.5},不做 pivot 覆盖(现状各表均居中,setting 复现不需要);某屏需非居中再加,本轮不预建。
- B2 「可选覆盖」承载:默认方案 A 旁置 `_border_override.json`(轻量、可 git 追溯);大量手调需求出现再升级 EditorWindow(方案 B)。
- B3 强制重打变体:默认不提供,只「不覆盖+报已存在」(用户拍板:不覆盖);高频迭代繁则后续加带二次确认的强制变体。
- B4 入口形态:默认菜单项 + Project 选中目录(零额外 UI)。
- B5 超 2048 处置:默认报错中止(目标屏不会超);真有超限屏另开增量支持多页/4096。
- B6 测试产出表入库:默认 TearDown 删除/临时目录;`setting/` 正式 `Sheet_setting.png` 是否入库由 boss/后续换皮轮定。
- B7 写子图 API:默认 `ISpriteEditorDataProvider`(现代、非 obsolete、已核实存在);`TextureImporter.spritesheet`(已 Obsolete 仍可写)作兜底,两路同验收。**本轮对设计稿草稿的唯一实质修正**——草稿原以 obsolete `spritesheet` 为主路且未提示,经 unity_reflect+docs 核实 `is_obsolete=true` 后改为现代 API 优先(设计稿 §五 callout + §八 dev 清单 + B7 行)。
- 菜单路径:取草稿现值 `Tools/UI/…`(简报建议 `Tools/UI Art/…`);boss 若要改只动 `[MenuItem]` 字符串、零逻辑改,不阻塞。

## blockers(无安全默认/抵触 spec/不可逆)
- 无。本任务范围开关均有安全默认、可逆、不抵触设计 23 寻址范式。

## taskFlaw(任务定义硬伤)
- 无。任务定义与工程现状一致:寻址范式已实证(dev.md #40)、对照基准 `Sheet_settings.png` 真实存在且字段已勘察、源 `setting/` 21 张 border 已设对、收集器收录路径正确、SimulateBuild API 签名核实。boss 简报提供的所有事实经勘察核对一致,无矛盾/基线指错/范围不可行。
