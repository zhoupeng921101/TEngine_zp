---
name: project-unitymcp-prefab-pitfalls
description: 用 unityMCP 建 UIWindow prefab 八坑:target 用 by_path 而非负 instanceID/Color 用对象/m_Content 用整数 ID/容器需补 RectTransform/根不挂窗口脚本/z 序=建序/字体不便赋/必删场景临时实例。
metadata:
  type: project
---

用 unityMCP 建 UIWindow prefab(scene 建树 → `manage_prefabs create_from_gameobject` 落 `AssetRaw/UI/Prefabs/`)的实操坑:

① 节点 target 一律用 `search_method:by_path`(全路径如 `RankWindow/Root/m_node_MyRank/m_text_MyRank`),**负 instanceID 在 delete/modify 上常报 `not found using method 'default'`**(find 能返该 id 但 CRUD 解析不了);
② `set_property` 的 Color 必须 `{r,g,b,a}` 对象、不能数组(数组报 JObject 解析错);
③ 场景对象引用(如 ScrollRect 的 `m_Content`/`m_Viewport`——序列化名带 `m_` 前缀,非 `content`)用**整数 instanceID** 赋值,`{path:...}` 只解析资产路径(对场景对象报 No asset found);
④ `manage_gameobject create` 一个无 UI 组件的容器节点(如 Root/Content)会得**普通 Transform**(给 UI 子节点不会自动升级父),须显式 `manage_components add RectTransform` 再 set;
⑤ UIWindow 范式 prefab **根不挂窗口脚本**(对照 `SettingsWindow.prefab`:根只 RectTransform+Canvas+GraphicRaycaster,框架按 `[Window(location)]` 运行期挂逻辑)→ 不必给 prefab 加 RankWindow 组件;
⑥ z 序=兄弟序(先=后面),`create` 只能 append、无 sibling-index 重排:要某节点在底(如面板底板在最前)就**先建它**,要内容在上就后建——顺序错了删掉重建(by_path delete 可用);
⑦ 内置字体引用 MCP 不便赋(`font` 设路径报转换失败)→ prefab 内静态 Text 可能不显字,代码生成的 Text 在运行期 `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` 自赋正常,交接标注此为 art 受限占位、非 FAIL;
⑧ `create_from_gameobject` 后必删场景临时实例(否则脏了 main.unity,越界);存盘后 grep prefab YAML 复核 `m_RenderMode:2`(WorldSpace)+ 根 RectTransform stretch+scale1 仍在。

**Why:** unityMCP 的 instanceID 在不同会话/操作类型间不稳定,by_path 是唯一可靠定位;Color/对象引用的序列化格式约定与 Unity Editor 不同,需现学;RectTransform 升级 + 字体引用是 UI 特有坑(2026-06,rank-window)。

**How to apply:** 用 unityMCP 建 UI prefab:① 所有 target 强制 by_path;② Color 用对象格式;③ 场景对象引用用 instanceID;④ UI 容器节点先 add RectTransform;⑤ 根 prefab 不挂窗口脚本;⑥ 节点 z 序按需建次;⑦ 字体不赋(代码生成兜),交接标占位;⑧ 落盘后清场景临时实例 + grep YAML 复核。
