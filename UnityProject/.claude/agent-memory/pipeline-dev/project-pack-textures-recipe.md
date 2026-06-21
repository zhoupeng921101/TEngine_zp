---
name: project-pack-textures-recipe
description: 散切图打 Multiple 精灵表 PNG 五步:读字节进临时纹理/PackTextures 像素 rect 用源尺寸/border 读 importer 字段/写顺序 Multiple→Read→改→Write→现代 API→SaveAndReimport/输入按文件名 Ordinal 排序保确定性。
metadata:
  type: project
---

散切图打表(Multiple 精灵表 PNG)落地要点:

① 源 `isReadable=0` 不必改源 importer,读 PNG 字节 `new Texture2D(2,2).LoadImage(bytes)` 到临时纹理读像素(读完 DestroyImmediate);
② `sheet.PackTextures(texArray, padding, max)` 返归一化 uvRect[],像素 rect 位置=`Mathf.Round(uv.x*sheetW)`,但 w/h 直接用源像素尺寸(不用 uv.width*sheetW,避免缩放误差,保 R3「rect 尺寸==源尺寸」);
③ border 读 `importer.spriteBorder`(importer 级,**非** `spriteSheet.sprites[0].border`,后者 Single 模式是 {0,0,0,0} 占位);
④ 写 importer 顺序:先设 `spriteImportMode=Multiple` 再 `ReadTextureSettings`(读进当前 Multiple)→ 改 `spriteMeshType=FullRect`/`spriteAlignment=Center`/`spritePivot`→`SetTextureSettings`→现代 API 写子图(见独立条 modern-sprite-data-provider,非 obsolete `ti.spritesheet`)→`SaveAndReimport`;
⑤ 确定性:输入按文件名 `StringComparer.Ordinal` 排序后再喂 PackTextures,重跑映射一致。

**Why:** 每条都是踩出来的坑——uv.width*sheetW 在浮点下偏 1 像素破坏「rect 尺寸=源尺寸」R3;Single 模式 border 字段是占位;写 importer 顺序错会导致字段丢;输入顺序变化导致映射漂移破坏可重跑性(2026-06,ui-atlas-packer)。

**How to apply:** 写 PackTextures 工具时:① isReadable=0 走临时纹理读像素,源 importer 不动;② rect 位置取 Round(uv*sheet),尺寸用源像素;③ border 读 importer.spriteBorder;④ 严格按 5 步写 importer;⑤ 输入按 Ordinal 排序。
