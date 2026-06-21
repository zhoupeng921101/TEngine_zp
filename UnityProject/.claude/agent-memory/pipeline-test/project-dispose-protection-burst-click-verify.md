---
name: project-dispose-protection-burst-click-verify
description: `_disposed + _fetchToken` 双保险验证用 burst tab click + 立刻 closeBtn.onClick.Invoke,无 NRE 即证生效
metadata:
  type: project
---

`_disposed + _fetchToken` 双保险在 Play 模式中可用 burst tab click + 立刻 closeBtn.onClick.Invoke() 方式触发验证:连点 tab × N → 立刻关窗,无 NRE 即证 dispose 保护生效,不需复杂 async 注入。

**Why:** 2026-06 ledger-client PV11 实测,要测异步回调收到时窗已关的并发场景,模拟「关窗时仍有飞行中请求」的方法是 burst 点 tab 触发多个并发 fetch 后立即关窗;无 NRE 即证 dispose 后回调被吞。

**How to apply:** 异步生命周期保护验证:①找 N 个 tab 按钮 ② foreach btn.onClick.Invoke() burst 触发 ③立刻 closeBtn.onClick.Invoke() ④看 console 无 NRE = PASS;比注入 FakeSource 简单。
