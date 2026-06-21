---
name: project-claudemd-snapshot-on-spawn
description: 项目 CLAUDE.md 自动注入子 agent,但是「主会话启动时的快照」;改 CLAUDE.md 后须重启会话才对子 agent 生效
metadata:
  type: project
---

项目 CLAUDE.md 会自动注入子 agent,但注入的是**主会话启动时的快照**。

**Why:** 2026-06-12 探针实测,主会话期间改 CLAUDE.md,新增章节未出现在后续 spawn 的子 agent 上下文里;说明系统把 CLAUDE.md 内容在主会话启动时缓存,后续 spawn 子 agent 用缓存。

**How to apply:** boss 编排期发现 CLAUDE.md 需要改动时,改完不能假设子 agent 立刻看到——重启 Claude Code 会话后再 spawn 才对子 agent 生效。短任务可在主会话内手动把要点塞进简报。
