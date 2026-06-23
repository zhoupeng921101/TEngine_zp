---
name: MergeOrderWindow 静态 prefab 手写经验
description: 直接写 YAML prefab 文件时的关键约束和坑
type: project-feedback
---

## Rule

MCP 工具不可用时，直接写 Unity YAML prefab 文件可行，但须遵守：

1. **Content 的 m_Children 必须与实际子节点 RectTransform fileID 精确对应**。写入后如果追加了节点（层节点用了不同 ID 范围），必须回来修正 m_Children 列表，否则 Unity 导入时子节点挂载会乱序或报错。

2. **Button GameObject 的 m_Component 必须同时列出 Image + Button 两个 MonoBehaviour fileID**，不能只列 Image。漏写 Button 引用会导致该 GO 在 Unity 里缺少 Button 组件。

3. **负数 fileID（如 -6211453905060949989）在 YAML 里直接写负数**，Unity 可识别；不需要转 uint64。

4. **层节点（BoardLayer 等空 RectTransform）不需要 CanvasRenderer 和 Image**，只需 RectTransform 即可。

5. **sprite 引用格式**：`{fileID: <internalIDFromMeta>, guid: <textureGuid>, type: 3}`，fileID 来自 `.meta` 文件的 `internalIDToNameTable[213]` 字段。

## Why

Unity YAML prefab 对节点引用完整性要求严格：m_Children 列表是父子关系的唯一来源，m_Component 是组件附着的唯一来源。任何一处不匹配，Unity 导入时要么忽略该节点要么报序列化错误。

## How to apply

写完整个文件后用 PowerShell 统计 `--- !u!1 &` 和 `--- !u!224 &` 数量是否相等，再逐一核对 m_Children 中的 fileID 与实际写入的 RectTransform anchor fileID 是否一一对应。
