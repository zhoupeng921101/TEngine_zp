---
name: project-uistack-reflection-private-access
description: UI 私有字段/方法验证须从 `_uiStack` 遍历取实例,ValueTuple 字典键的字段运行时是 Item1/Item2
metadata:
  type: project
---

要驱动 UIWindow 私有字段/方法(注入 state + 调私有刷新/点击 handler)而非仅查 GameObject:UI 模块无同步 getter,从其私有字段 `_uiStack`(List)遍历取类型匹配的托管实例,再反射读写私有字段/Invoke 私有方法;ValueTuple 字典键的元素字段名运行时是 `Item1/Item2` 不是声明的具名(`type/level`),反射取值用 Item* 否则 NRE。

**Why:** 2026-06 tarot-blindbox F12/F13 手验:UI 模块没有公开的「按类型取实例」API,只能从私有 `_uiStack` 列表里 LINQ 过滤;ValueTuple 编译后字段总是 Item1/Item2(C# 语言层的 (type:X, level:Y) 命名仅供源码识别),用具名做反射键查必返 null 致 NRE。

**How to apply:** 私有字段/方法手验三步:①反射 UIModule 实例 → 取 `_uiStack` private field → 按窗类型筛 ②对窗实例 GetField/GetMethod 注入 state 或触发私有 handler ③ ValueTuple 键的字典访问统一用 Item1/Item2。
