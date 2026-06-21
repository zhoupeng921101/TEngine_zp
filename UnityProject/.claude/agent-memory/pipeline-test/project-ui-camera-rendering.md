---
name: project-ui-camera-rendering
description: UI 经 UICamera(ScreenSpaceCamera)渲染,Play 手验/截图须经它取景才能看到 UI
metadata:
  type: project
---

UI 经 UICamera(ScreenSpaceCamera)渲染,Play 手验/截图须经它取景。

**Why:** 2026-06 collect 实测发现:用默认 Main Camera 截图看不到 UI,因为 UI 在独立的 UICamera 层渲染,渲染模式为 ScreenSpaceCamera 而非 Overlay。

**How to apply:** Play 模式做 UI 截图或手验时,确认相机引用 = UICamera(非 Main Camera);execute_code 中要查找 UI 节点也须确认其挂在 UICamera 渲染链下。
