---
name: project-uiwindow-rename-four-places
description: 改 UIWindow 类名(按文件名寻址)必须同步改四处:.cs 类名+Window attr 字符串/同名 prefab 文件名/prefab 内 m_Name/prefab .meta,漏一处运行时找不到资源。
metadata:
  type: project
---

改 UIWindow 类名(AssetRaw/UI 按文件名寻址)必须同步改四处,漏一处运行时找不到资源:① .cs 类名 + `[Window(location:"...")]` 字符串;② 同名 prefab 文件名;③ prefab 内 `m_Name`;④ prefab .meta。

操作流程:prefab/.cs 改名用 `git mv` 连 .meta 一起搬保 GUID;.cs 内容用 Write 重建后 `git mv` 旧 .meta→新名 .meta(GUID 不变)、rm 旧 .cs。代码内 `ShowUIAsync<T>/CloseUI<T>` 泛型引用随类名走,不涉寻址。

**Why:** TEngine UI 按文件名寻址(YooAsset location),类名/prefab 名/m_Name/.meta GUID 四处任意一处不一致即运行时 Load 失败、报「找不到资源」,且报错指向资源加载非类型;改名时疏漏一处的概率极高(2026-06,collect-rename)。

**How to apply:** 改 UIWindow 类名前列清单核对四处;Bash 改名优先 `git mv` 保 GUID;改完跑一遍 Play 确认窗口能 Show。
