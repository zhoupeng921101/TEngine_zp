---
name: project-code-gen-uiwindow-pattern
description: 整窗 UI 代码生成范式:最小壳 prefab(沿 MainMenuWindow 80 行模板)+ OnCreate 全代码生成 UI;避 unityMCP 多坑,_disposed+token 双保险防响应到达时已 dispose。
metadata:
  type: project
---

整窗 UI 代码生成范式(避开 prefab + unityMCP 多坑):对功能型新窗(列表 / 流水 / 表格类)用「**最小壳 prefab + OnCreate 全代码生成 UI**」=

① prefab 沿 `MainMenuWindow.prefab`(80 行模板)只摆根 GameObject + RectTransform(stretch 0,0~1,1)+ Canvas(`m_RenderMode:2` WorldSpace,memory L26 警告)+ GraphicRaycaster(GUID `dc42784cf147c0c48a680349fa168899` 沿 SettingsWindow.prefab),手写 YAML 即可(新 GUID 用 16 hex 字符,如 `7c6b1d8e9f0a4c5b8e2d1f3a4c5b6d7e`,加同名 .meta);
② `ScriptGenerator` 留空(无 FindChildComponent 可绑);
③ `OnCreate` 调 `BuildLayout` 用 `NewRect / AddImage / AddImage2 / AddText / AddPanel`(沿 28 RankWindow `BuildRowGo` 范式)代码生成所有控件挂 `rectTransform`(=prefab 根)下;
④ 按钮绑定在生成时直接 `btn.onClick.AddListener(...)`,生命周期随 GameObject 自动清;
⑤ 关键纯函数(行 VM 映射 / 文案格式)抽成 `public static`,EditMode 单测直调验,UI 层薄壳留 Play 手验;
⑥ 列表静态垂直排版 + `RectMask2D` 裁剪超出部分(取代 ScrollRect 字段绑定的繁琐),首屏 N 条仅前 ~K 行可见是已知诚实边界(test 反馈再升级 ScrollRect);
⑦ tab 高亮 / token 防覆盖类状态字段挂窗口实例,`_disposed` + `_fetchToken` 双保险防响应到达时已 dispose(`OnDestroy` 内置 `_disposed=true`,响应回来时核 `_disposed || token != _fetchToken` 跳过 UI 更新);
⑧ 入口按钮挂别的窗口子节点用同范式代码生成(`BuildXxxButton` 内 `FindChildComponent<Transform>("Root/XxxBlock")` 父节点缺失返 null 自动 null-safe 跳过),不动宿主 prefab。

比 unityMCP 路径(独立条 unitymcp-prefab-pitfalls)改动面小、单测易写、字体引用 + 节点 id 多坑全避开;Editor 看 prefab 是空 RectTransform 属正常非异常。

**Why:** 功能型窗口(列表/流水)节点多、unityMCP 创节点慢且踩坑;代码生成把 UI 结构集中在 OnCreate,单测可直调纯函数验 VM,Build 部分 Play 手验即可;双保险防异步响应到达时窗口已关(2026-06,attr-ledger-window)。

**How to apply:** 新功能型 UIWindow:① prefab 只 80 行壳(根+Canvas WorldSpace+GraphicRaycaster);② ScriptGenerator 留空;③ OnCreate 走 NewRect/AddImage/AddText 代码生成;④ 纯函数(VM/格式)抽 static 单测;⑤ 列表用 RectMask2D 静态排版;⑥ 异步响应处理加 _disposed+token 双保险;⑦ 入口按钮代码挂宿主子节点 null-safe。
