---
name: project-anti-reentry-verify-via-reflection-field
description: 防重(_gameOverTriggered/_finished)反射读字段 before/after + 连调两次日志只出现一次,比注入 FakeSource 直接
metadata:
  type: project
---

防重验证(_gameOverTriggered/_finished)可在 Play 手验时直接读反射字段:反射读 `_gameOverTriggered` before=False/after=True 配合「连续调两次 TriggerGameOver,[Activity] 日志只出现一次」,比纯桩注入更直接且不需注入 FakeSource。

**Why:** 2026-06 activity-client V3 实测,防重逻辑通常是「检查 flag → 若已置则 return → 否则置 flag 并继续」,直接读 flag before/after 是最权威证据;再加连调两次只触发一次副作用做行为证据。

**How to apply:** 防重验证三联:①反射读防重 flag(before=False)②调两次触发方法 ③ read_console 验副作用只出现一次 + 反射读 flag(after=True);无需 FakeSource。
