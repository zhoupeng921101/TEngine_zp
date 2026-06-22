---
name: project-board-cells-solid-color-not-atlas
description: MergeOrderWindow 棋盘格用纯色 Image(BlockLayout.ColorOf)渲染,不走 blocks_main 图集;窗内 SetSubSprite 都是外框/HUD 皮,非方块。GameWindow 已退役无活入口。
metadata:
  type: project
---

BlockBlast 活动主玩法窗 = `MergeOrderWindow`(`GameWindow` 退役、无活入口,设计 29 §4.2)。棋盘格(已落子方块)渲染在 `MergeOrderWindow.RenderBoard`,用 `BlockLayout.ColorOf((BlockColor)colorIdx)` 贴**纯色 Image**——`Atlas_blocks_blocks_main`/`blocks_main_<n>` sprite **未接入棋盘格**。窗内 `SetSubSprite(Atlas,...)` 调用(`Sheet_tarot_mode`)都是棋盘外框 chess/背景 chessboard/HUD,**不是方块本体**。

**Why:** 方块外观历史上用纯色块表达类型,从未用 sprite 图集;美术底料(blocks_main/blocks_skin 图集)是后来加的皮肤资源,接入棋盘渲染需 dev 在 RenderBoard 显式分叉,不是改图集就自动生效。

**How to apply:** 任何「方块换皮/换图」任务的渲染接缝 = `MergeOrderWindow.RenderBoard`(不是 GameWindow,不是图集配置)。给棋盘格贴 sprite:每格 Image `SetSprite(location)` + 白 tint;blocks_skin 是 SpriteAtlas v2,SetSubSprite 不工作(见 [[project-setsubsprite-not-spriteatlas-v2]]),走散 PNG 按文件名寻址(`blocks_skin_atlas_<n>`,UIRaw 收集器组覆盖 `Assets/AssetRaw/UIRaw/Atlas` 路径)。候选 sprite 编号集从源目录实际文件导出,别信 README(blocks_skin README 标 337 张实际 374,且段不符)。
