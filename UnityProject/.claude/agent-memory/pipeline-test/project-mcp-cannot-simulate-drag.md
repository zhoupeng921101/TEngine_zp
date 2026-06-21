---
name: project-mcp-cannot-simulate-drag
description: MCP 无法可靠模拟指针拖拽,拖拽/手势类验证点一律标「需人工 Play 手验」
metadata:
  type: project
---

MCP 无法可靠模拟指针拖拽(BlockPieceDragger 等),拖拽/手势类验证点一律标「需人工 Play 手验」;其逻辑层改用单测 + state 注入覆盖。

**Why:** 2026-06 实测,MCP 的指针事件无法稳定触发 Unity EventSystem 的 BeginDrag/Drag/EndDrag 链,DragHandler 类组件收不到事件。

**How to apply:** 遇拖拽/手势/swipe 类验收项,test 报告里标注「需人工 Play 手验」而非尝试自动化;逻辑分离原则下,把可拖拽对象的状态机和数值判定下沉到可单测的纯函数层,用 state 注入跑 EditMode 覆盖。
