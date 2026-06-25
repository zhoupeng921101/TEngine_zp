---
name: csharp-no-overload-on-generic-constraint
description: C# 不能仅靠泛型约束(where T:A vs where T:B)区分同签名重载；想让同名泛型调用按类型自动分流到两套实现时的正确做法
type: rule
---

rule: C# 重载决议**不看**泛型约束(`where` 子句)。两个方法 `M<T>(...) where T:A` 与 `M<T>(...) where T:B` 参数列表相同即视为同签名，编译报 `CS0111: already defines a member with the same parameter types`。要让同名泛型调用点按 `T` 的实际类型自动走两套不同实现(例:经典 `UIWindow` 路径 vs `UIWindowMono` 路径),不能加第二个约束不同的重载，正确做法是**保留单个泛型方法、把约束放宽到两套类型的公共基/接口**(如二者共有的 `IUIWindow`)、方法体内用 `typeof(B).IsAssignableFrom(typeof(T))` 在运行时分流;若原实现依赖 `new()` 约束(如 `new T()` 构造),改走**基于 `Type` 的实现**(`Activator.CreateInstance(type)` 反射构造)以摆脱 `new()`，这样调用点 `M<具体类型>()` 一律不改写。

Why: UI 框架 Mono 化迁移分多批进行，每批把若干窗口从 `UIWindow` 改基类为 `UIWindowMono`。直觉(以及迁移计划的字面表述)是「为 Mono 窗加一组 `where T:UIWindowMono` 的 ShowUIAsync/CloseUI 重载，与经典 `where T:UIWindow` 并存，靠约束自动分流」——这在 C# 里直接 CS0111 编译不过，泛型约束不参与重载决议。放宽到公共接口 `IUIWindow` + 运行时 `IsAssignableFrom` 分流 + Type 版反射构造，可在零改写调用点的前提下达成同一目标(2026-06 Stage B 首批迁移实测:6 个窗口的 `ShowUIAsync<那窗口>()`/`CloseUI<那窗口>()` 调用点全部不动即编译绿、PlayMode 开关正常)。

How to apply: 给「需按泛型实参类型分流到两套实现」的同名泛型方法做迁移时,先确认两套目标类型的公共基/接口,把唯一泛型方法的约束放宽到它、方法体内 `typeof(子集类型).IsAssignableFrom(typeof(T))` 分流;原方法若有 `new()` 约束则同步把内部实现切到 `Type` 版反射构造。不要试图新增「仅约束不同」的重载——必撞 CS0111。
