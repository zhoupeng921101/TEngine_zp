---
name: project-execute-code-async-via-unitask-void
description: execute_code 无顶层 await,异步用 UniTask.Void + Debug.Log 标签 + read_console filter 捞输出
metadata:
  type: project
---

execute_code 异步入口:在 PlayMode 中无法写顶层 await,须用 `UniTask.Void(async () => { ... })` 发起异步调用、在内部 `Debug.Log` 记结果,再用 `read_console(filter_text=...)` 捞特定标签的输出作为断言依据。

**Why:** 2026-06 E1 rank 真往返实测,execute_code 包进同步 method body 不允许顶层 await;UniTask.Void 是 fire-and-forget 的异步包装,内部可用 await,通过 console 输出回传结果。

**How to apply:** 异步真往返手验模板:`UniTask.Void(async () => { var r = await XxxAsync(); UnityEngine.Debug.Log("[TAG] r=" + r); });` → 等数秒 → `read_console(filter_text="[TAG]")` 取结果。
