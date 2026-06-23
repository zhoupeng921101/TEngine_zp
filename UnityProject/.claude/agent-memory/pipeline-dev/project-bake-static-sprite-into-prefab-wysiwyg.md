---
name: project-bake-static-sprite-into-prefab-wysiwyg
description: 把运行时 SetSprite 的静态视觉改成烤进 prefab 的 m_Sprite/m_Color（编辑器所见即所得），同时删冗余代码、翻转旧回归断言、核对 YooAsset 不重复打包。
metadata:
  type: project
---

把「代码运行时 SetSprite 贴静态图」改成「sprite + tint 直接烤进 prefab 绑定节点」让美术在 prefab 内可视化预览/修改时的成套做法：

**烤图（最稳路径=execute_code + PrefabUtility，不手编 YAML GUID）**
`PrefabUtility.LoadPrefabContents(path)` → `GetComponentsInChildren<Image>(true)` 按 `gameObject.name` 匹配绑定节点 → `img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath)` + `img.color = ...` → `PrefabUtility.SaveAsPrefabAsset(root, path)` → `UnloadPrefabContents(root)`。
注意 execute_code 把代码包进方法体，**顶层不能写 `using` 指令**，类型要全限定（`UnityEditor.PrefabUtility` / `UnityEngine.Sprite`）。
烤前先核源 PNG 是 Single 模式（`spriteImportMode==Single`，主对象即整 Sprite）且 `LoadAssetAtPath<Sprite>` 非 null，否则取到的是 Texture（见 project-game-main-png-single-mode-not-multiple）。

**删冗余代码**：prefab 烤好后，BuildStaticUI 里这些节点的 `SetSprite(...)` + `.color=` 必须删——代码再贴会在 OnCreate 覆盖美术在 prefab 的调整，违「prefab 为静态视觉唯一来源」。但**动态染色保留**（如 ClearTool gate 按体力门控染图标的 RefreshClearTool）、动态换皮保留（RenderBoard/RenderSlots/RenderElements 随状态 SetSprite）、按钮 onClick/引用赋值保留。

**翻转旧回归断言（同任务内做，别留给验收方报假阳）**：文本核断型回归测（`File.ReadAllText + Assert.IsTrue(src.Contains("SetSprite(\"X\")"))`）会断言「代码 SetSprite 了 X」——删 SetSprite 后这断言必 FAIL。把它翻成新事实 `Assert.IsFalse(src.Contains("SetSprite(\"X\")"), "已烤进 prefab，代码不应再 SetSprite")`，并补一条「动态换皮仍走 SetSprite」的正向断言锚住没误删动态路径。

**YooAsset 不重复打包的判据**：prefab（如 AssetRaw/UI 组）直引的 PNG，只要该 PNG 自身也在某 **MainAssetCollector** 组（如 AssetRaw/UIRaw/Atlas）被收集 = 有自己的 bundle，则 prefab 直引只是跨 bundle 依赖、PNG 不被复制进 prefab bundle，无重复打包、无 warning。会重复的反例 = 引一个没有任何 collector 的 asset（被当无主依赖塞进每个引用方 bundle）。改前 grep AssetBundleCollectorConfig.xml 确认源 PNG 落在 MainAssetCollector 组即可。

**Why:** WYSIWYG 要求 prefab Image 持有真实 sprite GUID 引用（编辑器渲染只看 m_Sprite，不跑 OnCreate），故必须烤进 prefab 而非运行时贴；代码同时贴会与 prefab 冲突，删冗余是配套动作；文本核断回归对「换皮换法」天然脆，翻转是 dev 同任务义务（见 feedback-flip-stale-assertions-on-merge）。（2026-06，MergeOrderWindow game_main 静态壳烤进 prefab）

**How to apply:** 验收核心 = 不进 Play 用 LoadPrefabContents 读 `img.sprite!=null`（或 grep prefab YAML `m_Sprite: {guid:...}` 非 `{fileID: 0}`）；Play 期若异步导航被并发损坏（如 Fantasy 网络层每帧 NRE）打不开窗，可直接 `Instantiate(prefab)` 进 Play UICanvas 验 `img.sprite.texture!=null` 后 Destroy 探针，绕开导航。run_tests 前先确认未在 Play（Play 期 run_tests 直接 failed: "Cannot start a test run while ... Play Mode"）。
