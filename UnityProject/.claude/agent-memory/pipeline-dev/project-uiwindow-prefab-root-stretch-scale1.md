---
name: project-uiwindow-prefab-root-stretch-scale1
description: UIWindow prefab 根 RectTransform 必须 stretch 锚点(0,0)-(1,1)+scale1+无 CanvasScaler，否则 root scale=0 整窗不可见（编译/控制台无错但屏上空白）。
metadata:
  type: project
---

TEngine UIWindow 的 prefab 根（带 Canvas + GraphicRaycaster + 可选 UIBindComponent）被 UIModule parent 到 UICanvas 下后，靠 **stretch 锚点填满父级**。工作窗口（GameWindow/MainMenuWindow）的根约定：`localScale=(1,1,1)`、`anchorMin=(0,0)`、`anchorMax=(1,1)`、`pivot=(0.5,0.5)`、组件仅 `RectTransform,Canvas,GraphicRaycaster`（UIBindComponent 窗口多一个绑定组件）。

UIBindComponent 工具/手搭新窗口若把根建成 `anchorMin=anchorMax=(0,0)` + `localScale=(0,0,0)` + 挂了 `CanvasScaler`，则 root 世界尺寸塌成一点：**编译 0 错、控制台 0 错、节点树/绑定全对、Play 不报错，但屏上整窗空白**（root.GetWorldCorners 全为同一点、lossyScale=0）。CanvasScaler 不该挂在窗口根（缩放由 UIRoot canvas + Content 节点 localScale 负责）。

**Why:** 嵌套 Canvas 不自动驱动自身 RectTransform，要靠锚点拉伸；scale 0 直接让所有子节点不可见。这类故障骗过所有"无错"自检（编译/控制台/单测/节点核对都过），只有 Play 截图或核 root.GetWorldCorners 才暴露——交付前必跑一次 Play 目视或核根节点世界角。

**How to apply:** 接手 UIBindComponent 搭的新窗口 prefab，dev 接线前用 execute_code 核根节点：`localScale==1` 且 `anchorMax==(1,1)`，与已知工作窗口（GameWindow.prefab）对比。不符即修：root `localScale=Vector3.one / anchorMin=(0,0) / anchorMax=(1,1) / pivot=(0.5,0.5) / sizeDelta=0`，删 CanvasScaler（`PrefabUtility.LoadPrefabContents`→改→`SaveAsPrefabAsset`）。交付前 Play 开窗截图目视，不只信"控制台 0 错"。
