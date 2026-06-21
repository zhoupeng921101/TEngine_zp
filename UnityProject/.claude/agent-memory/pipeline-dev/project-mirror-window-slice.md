---
name: project-mirror-window-slice
description: 镜像既有窗口(如 GameWindow)做新切片窗口:逻辑层单测可全覆盖,风险集中在拖拽手势交互层,交接时显式标注。
metadata:
  type: project
---

镜像既有窗口(如 GameWindow)做新切片窗口:逻辑层单测可全覆盖,风险集中在拖拽手势交互层,交接时显式标注。

**Why:** 镜像现成窗口比从零搭建快、风险低,但拖拽/手势交互依赖运行时 EventSystem 与具体输入设备,EditMode 单测难以覆盖;不显式交接会让测试漏检手势退化(2026-06)。

**How to apply:** 复制既有 UIWindow 做新切片窗口时:① 逻辑函数/状态机抽出来由 EditMode 全覆盖;② 拖拽/PointerDown/PointerUp 类交互在交接说明里显式标「需 Play 手验」;③ 测试角色据此安排 Play 手验项,不被「单测全绿」误导为全验。
