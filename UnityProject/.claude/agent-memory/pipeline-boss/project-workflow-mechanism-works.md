---
name: project-workflow-mechanism-works
description: Workflow 机制可用(后台启动/内部 spawn/返回值/完成通知唤醒四环节全通),不占主会话回合
metadata:
  type: project
---

Workflow 机制可用(2026-06-12 探针实测:后台启动/内部 spawn/返回值/完成通知唤醒四环节全通,不占主会话回合)。

按 `Workflow({name:'pipeline-auto', args:{...}})` 调用,args 由 pipeline-auto.js 直接消费。

**Why:** 自治模式(/pipeline-auto)和长流水线编排(plan→dev→test 闭环)需要后台异步执行 + 完成时唤醒主会话,Workflow 机制满足这一切。比手动 spawn + 等回执的主会话编排省 token + 不阻塞主会话其他互动。

**How to apply:** /pipeline-auto 走 Workflow({name:'pipeline-auto', args:{task,target,baton,baseline}});常规 /pipeline 编排走主会话 spawn(boss 直接编排,小批可控)。两路并存,看任务规模选。
