---
name: project-ui-prefab-canvas-worldspace
description: 美术换皮 UIWindow prefab 根 Canvas 必须 WorldSpace,不能 ScreenSpaceOverlay,否则根 rect 被驱动归零、窗口逻辑正常但屏上不可见。
metadata:
  type: project
---

美术换皮 UIWindow 的 prefab 根 Canvas 必须 `RenderMode=WorldSpace`(对齐既有 `MainMenuWindow.prefab` 范式),**不能 ScreenSpaceOverlay**:Overlay 顶层 Canvas 的 RectTransform 被 Canvas 系统实时驱动成屏幕值,代码设的 anchorMax/scale 在 `SaveAsPrefabAsset`/`LoadPrefabContents` 时被驱动值覆盖回 0 → 运行时根 rect size=(0,0)、lossyScale=0、窗口逻辑全对(active/sprite/sortingOrder 都正常)但屏上不可见。症状极隐蔽(无报错、数据正确、就是不显示)。

建窗口 prefab 时根三件套照抄 `MainMenuWindow.prefab`:RectTransform stretch(anchorMin0,0/max1,1/sizeDelta0)+ localScale(1,1,1)+ Canvas RenderMode=WorldSpace + overrideSorting=false(UIWindow.Handle_Completed 会接管 sortingOrder,不改 renderMode/rect)。

**Why:** Overlay Canvas 的 RectTransform 被 Unity 内部驱动,代码写入立即被覆盖,无报错但渲染失败;UIWindow 框架自己接管 sortingOrder,prefab 不需要 overrideSorting(2026-06,settings-window)。

**How to apply:** 新建 UIWindow prefab:① 不 New Canvas 走默认设置,先看 `MainMenuWindow.prefab` 范式;② 显式设 RenderMode=WorldSpace;③ 根 RectTransform stretch+scale1;④ overrideSorting=false。
