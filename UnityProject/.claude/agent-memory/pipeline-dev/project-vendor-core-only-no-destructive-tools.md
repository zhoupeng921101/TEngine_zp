---
name: project-vendor-core-only-no-destructive-tools
description: 引入第三方 Editor 库只取核心纯函数:vendor 库核心源单独 asmdef,刻意不内置库自带破坏式工具(回写源 importer 的 Tester),用临时纹理喂库不动源。
metadata:
  type: project
---

引入第三方 Editor 库但只需其一小块纯函数(如 kyubuns/Auto9Slicer 只取 border 探测):vendor 库**核心源**(Slicer/SlicedTexture/SliceOptions)到 `Assets/Editor/<lib>/` 配独立 Editor-only asmdef,**刻意不内置库自带的破坏式工具**(Auto9SliceTester 会 `File.WriteAllBytes(源PNG)`+改 `importer.spriteBorder` 回写源)。判据:写回逻辑常在 Tester/便利封装里、不在核心 Slice——只调核心纯函数读返回值即天然绕开。

源 PNG `isReadable=0` 也能探:读字节 `new Texture2D(2,2).LoadImage(bytes)` 到临时可读纹理喂库,用完连同库产出的裁剪 Texture 一起 `DestroyImmediate`,不动源 importer(git status 源目录零改动即非破坏式实证)。

**Why:** 第三方库 Tester/便利封装常做破坏式动作(回写源/改 importer),原意是给工具链使用,但纳入项目内会污染美术原始资产;只搬核心纯函数 + 临时纹理输入 = 非破坏式集成(2026-06,border-override)。

**How to apply:** 引入 vendor 库:① 只复制核心算法源(Slice/Solver 类),拒绝 Tester/Editor Window;② 给 vendor 目录独立 asmdef;③ 用临时纹理给库,源 importer 不动;④ 跑完工具 git status 源目录应零改动。
