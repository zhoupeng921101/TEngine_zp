---
name: project-setsprite-async-verify-pattern
description: YooAsset SetSprite 是异步调用,反射触发后须多等一个 execute_code 轮次再读 sprite.name 才能拿到真实贴图名(含 _0 后缀)
metadata:
  type: project
---

`SetSprite(location)` 通过 YooAsset 异步加载散 PNG。在 execute_code 里反射调用 `RenderBoard()`/`ApplyCellSkin()` 之后，**不能在同一个 execute_code 里立即读 `Image.sprite.name`** — 贴图尚未到位，读到的是 null 或旧值。

正确模式：

1. 第一个 execute_code：触发 `OnAllClear` + `RenderBoard()`
2. 第二个 execute_code：读 `_cellImages[r, c].sprite?.name` — 此时 YooAsset 异步已完成

验证时 sprite.name 会带 `_0` 后缀（YooAsset sub-asset 命名规则）：`blocks_skin_atlas_227` → `blocks_skin_atlas_227_0`；`blocks_main_14` → `blocks_main_14_0`。断言时匹配含后缀的名字，或用 `.StartsWith()` 剥掉后缀验核心部分。

**Why:** execute_code 每次调用之间经过了足够帧数，YooAsset 协程/UniTask 到位，等价于等一帧再读。同一 execute_code 内的「触发 + 立即读」是竞态。

**How to apply:** 凡验证贴图切换效果（皮肤/换装/动态 sprite 替换），总是分两轮 execute_code：第一轮触发，第二轮断言。
