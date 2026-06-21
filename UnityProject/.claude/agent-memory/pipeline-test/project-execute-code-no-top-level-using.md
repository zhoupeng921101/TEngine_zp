---
name: project-execute-code-no-top-level-using
description: execute_code 无顶层 using,反射须 FullyQualified,静态成员取 FlattenHierarchy,注入精确类型
metadata:
  type: project
---

execute_code 不能写顶层 `using`(代码被包进方法体,using 触发 "Identifier expected"):全程用全限定名(`System.Reflection.BindingFlags`/`UnityEngine.PlayerPrefs`);反射取基类静态成员(如 SimpleSingleton<T>.Instance)须带 `FlattenHierarchy` flag,否则 GetProperty 返 null 致 NRE;反射注入私有字段前先确认 `FieldType.Name`——类型不匹配(如 float 值传给 int 字段)会 ArgumentException,需改用精确类型强转(如 `(int)-200`)。

**Why:** 2026-06 save-system/fusion-test 实测,execute_code 把传入代码包进一个 method body,using 是 namespace 级语句不允许出现在方法内;SimpleSingleton<T>.Instance 是基类静态属性,GetProperty 默认不穿透继承层级须加 FlattenHierarchy;反射 SetValue 不做隐式转换,float→int 直接抛。

**How to apply:** execute_code 写代码模板:①类型全限定 ②静态成员 BindingFlags 加 `|FlattenHierarchy` ③ SetValue 前先 `field.FieldType` 看声明类型再强转值。
