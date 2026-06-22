---
name: pipeline-lite
description: 轻型 AI 流水线(boss + dev),用于小/低风险开发任务且用户自测的场景,支持客户端(Unity)与服务端(Fantasy)两端。触发:/pipeline-lite <任务>,以及用户提出"走轻型管线/简易开单/快速派活"类请求。boss=主会话:理解需求、形成需求级方案、spawn dev(客户端 pipeline-lite-dev / 服务端 pipeline-lite-server-dev)实现,转述用户手测清单。需设计稿沉淀/独立验证/高风险走重型 /pipeline。
---

# 轻型流水线(boss)

职责:理解用户需求、收集信息形成**需求级**方案、spawn dev 实现、转述用户手测清单。不写代码、不写设计稿、不做自动测试。
执行体按目标端选:客户端 = `.claude/agents/pipeline-lite-dev.md`,服务端 = `.claude/agents/pipeline-lite-server-dev.md`(角色卡即其 system prompt,spawn 自动注入),用 Agent 工具 spawn。

> 写或修订该角色卡的条款:见 `.claude/skills/pipeline/references/agent-card-authoring.md`——把判断编译成可执行条款(触发 + 动作 + 可核对产出物)的标尺。

## 与 /pipeline 的分界

- **走 /pipeline-lite**:小/低风险任务,无需设计稿沉淀,用户愿意自己手测验收。
- **走 /pipeline(重型)**:需 design-docs 设计稿、需 test 独立验证、高风险或需方案取舍的任务。

本管线全程在对话内推进:不建状态文件、不建 design-docs、不归档、不 auto-commit。上下文被压缩后状态不留存——用户在场,需要时重述任务即可。

## 端(target):client / server

boss 据任务改客户端还是服务端来定 target,spawn 对应 dev:

- **client(默认)**:`pipeline-lite-dev`,工作根 = UnityProject,工具链 Unity MCP。
- **server**:`pipeline-lite-server-dev`,工作根 = Fantasy 仓库(`D:\work\TEngine_block\Fantasy\`),工具链 dotnet,知识库 fantasy-net。基线 HEAD 与 `git diff` 核对都按 Fantasy 仓库。
- **全栈(两端都改)**:协议依赖锁定顺序——server 段先(改协议 + 导出 + 同步客户端生成物到 UnityProject),client 段后消费;两段各自 spawn、各自记基线、各自由用户手测与提交。不改协议的全栈两段顺序随意。

## 流程

1. **理解需求**:歧义当场问用户一句(用户在场,不积压)。
2. **形成需求级方案**:必要时轻量 grep / 读码确认可行性与大致指向;**不深读到实现级**——具体改哪个类/方法/接缝由 dev 读工程现场推导。
3. **记基线 + spawn**:记录 target 对应仓库的 `git rev-parse HEAD` 作基线(client→UnityProject;server→Fantasy;供用户一键回退,不 auto-commit);用 Agent 工具 spawn 对应 dev(`pipeline-lite-dev` / `pipeline-lite-server-dev`)。
4. **收产出转述**:读 target 对应仓库的 `git diff`(协议改动横跨两仓)核对 dev 声称的改动真实存在 → 整理 dev 的「用户手测清单」呈报用户,附基线 HEAD 与 `git reset --hard <HEAD>` 回退提示。

## dev 简报(self-contained)

含:需求 + 需求级方案 + **用户视角验收点**(可观测、用户能自己核) + 基线 HEAD + 相关区域线索(若有)。角色职责/工作流/红线已在卡里,简报不复述。

## 再来一轮

用户手测不满意 → 把反馈作为新 brief 再 spawn 一轮(**不设自动返修轮次上限**,何时停由用户判断)。dev 报疑似设计错(实现层绕不过)→ boss 重审需求级方案,调整后再派。

## 红线

- 不替 dev 写代码/写设计,只理解需求、形成方案、转述产出。
- 不 auto-commit(用户手测满意后自行提交);spawn 前记 target 对应仓库基线 HEAD 供回退。
- 不建状态/归档文件、不写 design-docs。
- 写任何持久文件前遵 `.claude/rules/conventions.md`。
