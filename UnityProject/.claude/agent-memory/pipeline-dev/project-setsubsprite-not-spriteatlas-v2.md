---
name: project-setsubsprite-not-spriteatlas-v2
description: Image.SetSubSprite 对 SpriteAtlas v2 不工作(SubAssets count=0);要走精灵表 PNG(Mode=Multiple)或散 PNG,改资源后需 SimulateBuild 重建模拟清单。
metadata:
  type: project
---

`Image.SetSubSprite(location, 子图名)` 在本工程对 **SpriteAtlas v2 资源不工作**:内部走 `YooAssets.LoadSubAssetsAsync<Sprite>(location).GetSubAssetObject<Sprite>(名)`,而 SpriteAtlas 是打包容器、不把子精灵作为自身子资源暴露 → 实测 SubAssets count=0、按名取全 NULL。

SetSubSprite 的 location 必须是「含多个命名 Sprite 子对象的单一资源」=Sprite Mode=Multiple 的精灵表 PNG(子图名=Sprite Editor 里的子精灵名),或散 PNG(每张 SubAssets count=1,工程 blocks/ 即此)。要「每屏一图集 + 子图名寻址」就把切图合成一张精灵表 PNG:代码读源 PNG → `Texture2D.PackTextures` → `EncodeToPNG` 写出 → TextureImporter 设 Multiple + 逐子图 `SpriteMetaData`(name/rect/pivot/border)。落 `AssetRaw/UIRaw/Atlas/`(收集器 UIRaw/Atlas 组按文件名收录),改资源后须 `EditorSimulateModeHelper.SimulateBuild(包名)` 重建模拟清单 location 才生效;Play 启动会自动重建。

**Why:** SpriteAtlas v2 与 Multiple 精灵表 PNG 在 Unity 同是「打包+子图」概念,但 YooAsset SubAssets API 只对后者展开;SimulateMode 下的清单不会自动跟踪运行期资源变动(2026-06,settings-window)。

**How to apply:** 用 SetSubSprite 寻址前:① 确认 location 是 Multiple PNG 或散 PNG,**不是** SpriteAtlas;② 改资源后(新增子图/换名)调 `EditorSimulateModeHelper.SimulateBuild(包名)`;③ 现场失败时打日志看 SubAssets count(=0 即源类型错)。
