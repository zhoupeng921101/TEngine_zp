---
name: project-modern-sprite-data-provider
description: 写/读 Multiple 精灵表子图元数据用现代 API(Unity.2D.Sprite.Editor):SpriteDataProviderFactories+SetSpriteRects,SpriteRect.spriteID 必须 GUID.Generate(),alignment 是枚举非 int。
metadata:
  type: project
---

写/读 Multiple 精灵表子图元数据的现代 API(`TextureImporter.spritesheet` 已 Obsolete,B7 拍板用现代):程序集 `Unity.2D.Sprite.Editor`(asmdef 须显式引用,工具与测试两侧都要)。

写:
```
new SpriteDataProviderFactories().Init()
  → GetSpriteEditorDataProviderFromObject(importer)
  → InitSpriteEditorDataProvider()
  → SetSpriteRects(SpriteRect[])
  → Apply()
  → importer.SaveAndReimport()
```
读回(测试同源):`...GetSpriteRects(): SpriteRect[]`。

`SpriteRect` 命名空间是 `UnityEditor`(**不是** `UnityEditor.U2D.Sprites`),字段 name/rect[Rect]/pivot[Vector2]/alignment[**SpriteAlignment 枚举**,非 int]/border[Vector4]/spriteID[UnityEngine.GUID];新建每个 SpriteRect 必须赋 `spriteID = GUID.Generate()`(唯一 id,漏赋可能被 SetSpriteRects 拒/读回异常)。只写 N 个命名 SpriteRect=无 `Xxx_N` 自动切残留名。

**Why:** Obsolete API 在新版 Unity 可能彻底移除;命名空间与字段类型(alignment 枚举)有反直觉处,漏 spriteID 不会立即抛异常但读回行为不稳定(2026-06,ui-atlas-packer)。

**How to apply:** 写 Multiple 子图元数据:① asmdef 加 `Unity.2D.Sprite.Editor` 引用;② 严格按上述链调用;③ 每个新 SpriteRect 赋 `spriteID = UnityEngine.GUID.Generate()`;④ alignment 用 SpriteAlignment 枚举(Center=0 等);⑤ 期望「只有命名 rect」=别用 Obsolete API,只用现代 API 写,自动 _N 残留是 obsolete API 副作用。
