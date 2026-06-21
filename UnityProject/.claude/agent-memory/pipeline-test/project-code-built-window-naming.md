---
name: project-code-built-window-naming
description: 全代码生成窗口的节点用运行时名,naming-rules 的 m_btn_/m_text_ 前缀不适用
metadata:
  type: project
---

code-built 窗口(UGuiFactory 运行时建节点、无 prefab codegen)的节点名是运行时查找名,naming-rules 的 `m_btn_`/`m_text_` 前缀规则不适用,与全窗既有裸名体例一致即合规——别误判前缀缺失为违规。

**Why:** 2026-06 tarot-blindbox 实测,运行时 UGuiFactory 建的节点其 name 是 `GetComponentsInChildren` 的查找键,加 m_btn_ 前缀反而破坏既有裸名约定;naming-rules 的前缀规约针对的是 prefab codegen 产物(自动生成的 m_XXX 字段)。

**How to apply:** review code-built 窗口时,节点命名只需与本窗既有裸名风格统一,不强求 m_btn_/m_text_ 前缀;有疑问时 grep 同窗其他 GetComponentsInChildren 调用确认查找键命名风格。
