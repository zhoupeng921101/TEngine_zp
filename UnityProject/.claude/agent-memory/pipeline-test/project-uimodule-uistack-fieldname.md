---
name: project-uimodule-uistack-fieldname
description: UIModule 字段名 `_uiStack`(List 私有);GameContext.Instance 须 FlattenHierarchy 才能从 SimpleSingleton 基类取到
metadata:
  type: project
---

UIModule 字段名为 `_uiStack`(List 类型,私有);GameContext 的 Instance 需带 FlattenHierarchy flag 才能从 SimpleSingleton<T> 基类继承的静态属性取到(`BindingFlags.Public|Static|NonPublic|FlattenHierarchy`)。

**Why:** 2026-06 tarot-hud-attr-bind 实测,反复需要查 UIModule 内部栈和取 GameContext 单例,这两个细节固定且易记错。

**How to apply:** ①取 UIModule 内栈:`uiModule.GetType().GetField("_uiStack", Public|NonPublic|Instance)` ②取 GameContext.Instance:`typeof(GameContext).GetProperty("Instance", Public|Static|NonPublic|FlattenHierarchy)`。
