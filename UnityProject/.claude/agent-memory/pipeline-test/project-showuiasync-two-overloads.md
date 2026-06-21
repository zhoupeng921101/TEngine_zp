---
name: project-showuiasync-two-overloads
description: GameModule.UI.ShowUIAsync 有泛型/非泛型两重载,反射调用非泛型(Type,Object[])更稳
metadata:
  type: project
---

GameModule.UI 的 ShowUIAsync 有两个重载:泛型 `ShowUIAsync<T>(Object[])` 和非泛型 `ShowUIAsync(Type, Object[])`;execute_code 反射调用时,非泛型重载(`GetMethod("ShowUIAsync", new Type[]{typeof(Type), typeof(object[])})`)更稳定,泛型重载需先 `MakeGenericMethod`,两路均可行。

**Why:** 2026-06 tarot-hud-attr-bind V1 实测,泛型反射多一步 MakeGenericMethod 容易出错且参数构造繁琐;非泛型直接传 Type + Object[] 一步到位。

**How to apply:** 反射 ShowUIAsync 模板:`uiModule.GetType().GetMethod("ShowUIAsync", new Type[]{typeof(Type), typeof(object[])}).Invoke(uiModule, new object[]{ targetType, new object[]{ userData } })`。
