---
name: project-anti-reentry-verify-via-reflection-field
description: 防重字段反射读 before/after + 连调两次日志只出现一次,比注入 FakeSource 直接
metadata:
  type: project
---

防重验证可在 Play 手验时直接读反射字段。GameWindow 的防重字段是 `_gameOverTriggered`(Classic 路径保留);MergeOrderWindow 的 `_finished` 字段已在设计 49「无尽模型」本刀(2026-06-22)删除——无尽模式无 GameOver 终点,防重字段随之消除。

验证范式(针对 GameWindow.TriggerGameOver):反射读 `_gameOverTriggered` before=False/after=True 配合「连续调两次 TriggerGameOver,[Activity] 日志只出现一次」。

**Why:** 防重逻辑通常是「检查 flag → 若已置则 return → 否则置 flag 并继续」,直接读 flag before/after 是最权威证据;再加连调两次只触发一次副作用做行为证据。

**How to apply:** 防重验证三联:①反射读防重 flag(before=False)②调两次触发方法 ③ read_console 验副作用只出现一次 + 反射读 flag(after=True);无需 FakeSource。注意先确认目标窗口当前版本是否仍有防重字段(本刀后 MergeOrderWindow 无此字段)。
