---
name: pipeline-lite-server-dev
description: 轻型流水线服务端实现段。在 Fantasy(Fantasy.Net)服务端工程实现功能,dotnet 编译自检后交付。由 pipeline-lite 主会话流程在 server 实现段用 Skill 工具调用,不单独触发。
---

# 轻型流水线 · 服务端实现段

pipeline-lite 主会话在 server 实现段调用本 skill:基于需求级简报,在 **Fantasy(Fantasy.Net)** 服务端工程实现功能并自检,产出直接进主流程。与客户端实现段对称,工具链/工程/知识库全不同。

## 工作根与仓库
- **工作根 = `D:\work\TEngine_block\Fantasy\`**(独立 git 仓库,业务在 `examples/Server/APP/`:Entity 数据 / Hotfix 逻辑 / Main 入口三层)。
- dotnet / git 命令在该仓库根执行(当前 cwd 是 UnityProject,需 `cd` 进 Fantasy)。**不跑 `/unity-check`**——服务端不碰 Unity。
- 改动提交进 Fantasy 仓库,不混入 UnityProject(唯一例外:协议同步的客户端生成物)。

## 知识源(全部引用,不在此复制——副本必漂移)
- 强制工作流 / 编码红线 / ECS / 协议 / 数据库 / 时钟等:开工 Read `D:\work\TEngine_block\Fantasy\Skills\fantasy-net\SKILL.md`,顺导航跳本任务相关 `references/*.md`;构建命令与约定 Read `D:\work\TEngine_block\Fantasy\CLAUDE.md`「常用命令」。
- 共享 server-dev 经验库 `.claude/agent-memory/pipeline-server-dev/`(本 skill 不被系统自动注入,手动 Read)。

## 输入
主会话的 self-contained 简报即规格:需求 + 需求级方案 + 用户视角验收点 + 基线 HEAD(Fantasy 仓库)。**无 design-docs、无 plan.md**。

> 自行读服务端工程把需求映射到 ECS/Handler/协议接缝。确是需求层错、实现层绕不过才回主流程重审需求级方案。

## 自检(交付前必做,本管线唯一拦截)
无独立 server-test、无用户手测,自检是交付前唯一一道检查:
1. **编译干净**:`dotnet build examples/Server/Server.sln` 0 error 0 warning(sln 级不传 `--framework`;命令/坑以 `Fantasy/CLAUDE.md` 为准)。
2. **源生成器产物核对**:Handler / 协议 / SceneType 注册按预期生成(不手改 `.g.cs`、改注册改源重 build)。
3. **若改协议**:按 fantasy-net `references/protocol/*` 改源 + 跑导出,**把客户端生成物同步进 UnityProject 的 Fantasy Generate 目录**(漏同步 = 前后端协议错位,全栈最高风险点),在呈报里声明同步状态。
4. **过异常路径不只 happy path**:挑改动涉及机制最可能崩的一类——空/null、断线重连、并发消息、Entity 未就绪、跨 Scene 路由失败;能跑就手验,依赖 MongoDB 不可达则在呈报标注「待用户在有库环境自行核」。

## 红线
- 疑似需求/方案有错时先取证(读相关代码/接缝、核对验收点),确是实现层绕不过的才停手回主流程重审需求级方案,不越界自改需求。
- 改动提交进 Fantasy 仓库,不混入 UnityProject(协议同步的客户端生成物除外)。
- 不手改 `.g.cs`、不手动注册 Handler/System(以 fantasy-net 正本为准)。
- 写持久文件前遵 `.claude/rules/conventions.md`。

## 收尾
仅当一条经验是**规则型、可复用、能改变未来同类任务行为**时,才沉淀到 `.claude/agent-memory/pipeline-server-dev/<slug>.md`(共享经验库)。准入按 `.claude/rules/conventions.md`§规则准入:举得出「没有这条、下次会做错」的**复发**场景才写;单次事件、「修过 X / 解决了 Y」式过程记录、模型默认就会的事一律不写——这些进 git 历史,不进记忆。结构:独立结构化文件,frontmatter name/description/type,body rule + **Why:** + **How to apply:**;加索引到该目录 MEMORY.md。
