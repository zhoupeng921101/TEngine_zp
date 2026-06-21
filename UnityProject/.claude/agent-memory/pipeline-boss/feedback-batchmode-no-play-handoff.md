---
name: feedback-batchmode-no-play-handoff
description: batchmode 跑不了 Play 交互手验;UI 表现类验收要么交带 Unity MCP/人工的环节,要么在验收标准里把逻辑与表现拆开
metadata:
  type: feedback
---

batchmode 跑不了 Play 交互手验;UI 表现类验收要么留给带 Unity MCP/人工的环节,要么在验收标准里就把逻辑与表现拆开(逻辑进单测,表现列遗留)。

**Why:** batchmode 是 headless 跑 EditMode 单测,Play 模式需 Editor 上下文(运行时实例、UI 渲染、事件系统);batchmode 没这些,Play 手验类(execute_code 反射查窗口、点击按钮、读 console)全跑不了。

**How to apply:** plan 写验收标准时分两栏:① 逻辑层(单测断言,batchmode 也能跑)/ ② 表现层(Play 手验,需 Editor MCP)。test 看到表现层验收点而 Editor 不可达 → 该项 BLOCKED-env,不判 FAIL;逻辑层照常验。设计稿写「全代码生成 UI 的关键纯函数 BuildRowVM 抽 public static」就是为支撑单测覆盖表现层的核心(沿 28 RankWindow 范式)。
