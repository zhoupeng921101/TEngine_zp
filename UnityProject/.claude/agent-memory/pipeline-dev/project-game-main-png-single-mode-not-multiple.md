---
name: project-game-main-png-single-mode-not-multiple
description: game_main(UIRaw/Atlas)散图换皮要按文件名 SetSprite，但 PNG 默认导入成 Multiple+自动切片(图标被切碎成多子图)会让 SetSprite 失效；改 Single 模式根治。
metadata:
  type: project
---

`Assets/AssetRaw/UIRaw/Atlas/<组>/` 下散图收集规则 = `AddressByFileName`(文件名即 location) + `PackDirectory` + `CollectAll`，运行时 `image.SetSprite("文件名")` 寻址（不是 SetSubSprite，game_main 同 blocks 散图口径；SpriteAtlas v2 仅供编辑器打批次，不参与寻址）。`LocationToLower:0` 故中文名/原名照用。

坑：美术导入的整图 PNG 常被默认设成 **Sprite Mode=Multiple** 并经自动切片器切成多张子精灵（实测 icon_gem 被切成 8 子图、icon_temple 8 子图、整图背景切成 `<名>_0` 单子图）。Multiple 模式下 YooAsset `LoadAssetAsync<Sprite>(location)` 取的主对象是 Texture2D 不是 Sprite → `SetSprite` 拿到 null/贴不上；被切碎的图标即便能取到也只是碎片。

根治：把这些"一张图就是一个整精灵"的 PNG 全部改 **Single** 模式（`TextureImporter.spriteImportMode=Single` + `SaveAndReimport`），主资源即整张 Sprite，`SetSprite("文件名")` 直接生效，子图碎片也消失。

**Why:** Single 模式 PNG 的主对象就是 Sprite；Multiple 模式主对象是 Texture、Sprite 是子资源（要 SetSubSprite 按子图名取，且自动切片会把单图标切碎）。换皮目标是"一图一精灵按文件名贴"，Single 才匹配（2026-06，MergeOrderWindow game_main 换皮）。

**How to apply:** 贴 UIRaw/Atlas 散图前用 execute_code 核 `TextureImporter.spriteImportMode`：整图素材应为 Single；遇 Multiple 就批量改 Single+SaveAndReimport。Play 内用 `package.CheckLocationValid("文件名")` 确认 location 注册（含中文名）；改资源后 SimulateBuild 或 Play 重建清单 location 才生效。
