---
name: project-main-menu-cold-start-via-real-ui
description: 主菜单冷启动直开游戏内窗 NRE,正路是先点真实入口按钮走完整 UI 路径再开子窗
metadata:
  type: project
---

主菜单冷启动直接 ShowUIAsync 开游戏内窗口常 NRE(MergeState/UI 栈未就绪):正路是先点真实入口按钮(如 MainMenuWindow 的 BtnMerge)进对局,等 `GameObject.Find("MergeOrderWindow")` 出现后再注入 state + 点窗内入口按钮开子窗——全程走真实 UI 路径,比反射硬开模块内部稳且更接近用户操作。

**Why:** 2026-06 temple 手验,游戏内窗依赖 MergeState/GameContext 等运行期单例,冷启动后这些未初始化,直接 ShowUIAsync 触发字段访问就 NRE;走真实按钮链则被动触发完整初始化序列。

**How to apply:** 开游戏内窗(非纯展示型)做手验,优先「BtnX.onClick.Invoke()」级联开窗,而非反射 ShowUIAsync;反射只用于公共窗口或绕开拖拽场景。
