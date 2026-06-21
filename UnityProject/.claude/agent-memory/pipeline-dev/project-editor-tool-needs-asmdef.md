---
name: project-editor-tool-needs-asmdef
description: 给 EditMode 测试直调的 Editor 工具不能只放 Assets/Editor/(预定义 asm),overrideReferences 的测试 asmdef 引用不了;须给工具配独立 Editor-only asmdef。
metadata:
  type: project
---

要给 EditMode 测试直调的 Editor 工具,落点不能只放 `Assets/Editor/`(无 asmdef = 预定义程序集 `Assembly-CSharp-Editor`):既有测试 asmdef(如 `BlockBlast.Tests`,`overrideReferences:true`)**无法引用预定义程序集**(Unity 限制,asmdef 引用列表只认具名 asmdef)。

正确做法:给工具配独立 Editor-only asmdef(`includePlatforms:["Editor"]`,引用工具用到的具名程序集如 `YooAsset`),测试配自己的 asmdef 引用该工具 asmdef + TestRunner。这比无 asmdef 落进 Assembly-CSharp-Editor 更严格地只在 Editor 编译,也满足「不打包不热更」。

**Why:** Unity 的 asmdef 引用解析只查具名 asmdef 列表,无 asmdef 的 .cs 落进预定义程序集无法被 asmdef 引用;overrideReferences 切断了对预定义程序集的自动 implicit 引用,必须显式配 asmdef 把工具命名(2026-06,ui-atlas-packer)。

**How to apply:** 加 Editor 工具且要被 EditMode 测调:① 新建工具目录加 .asmdef(`includePlatforms:["Editor"]` + 引用工具用到的具名包);② 测试 asmdef references 加该工具 asmdef;③ 不把 EditMode 测要调的 Editor 代码扔进无 asmdef 的 `Assets/Editor/`。
