---
name: project-reflection-assembly-loader-robust
description: 反射收集程序集类型须 per-assembly try/catch,裸 SelectMany 遇 ReflectionTypeLoadException 会半途终止
metadata:
  type: project
---

execute_code 反射收集程序集类型别用裸 `asms.SelectMany(a=>a.GetTypes())`:某程序集中途抛 ReflectionTypeLoadException 会让整段枚举半途终止,导致同一会话两次调用一会找得到 GameLogic.GameModule、一会找不到(采样不稳)。改用 per-assembly try/catch、catch `ReflectionTypeLoadException` 取其 `.Types` 非空项、其他异常 `continue` 的稳健收集器。

**Why:** 2026-06 temple 手验,域里有部分程序集类型加载失败但仍可读出可用项,SelectMany 一旦抛异常整个 IEnumerable 流被切断,后续程序集完全没遍历到,产生非确定性结果。

**How to apply:** 写收集器:`foreach var asm: try { yield asm.GetTypes() } catch ReflectionTypeLoadException e { yield e.Types.Where(t=>t!=null) } catch { continue }`;遇「同一调用结果不稳」先怀疑此问题。
