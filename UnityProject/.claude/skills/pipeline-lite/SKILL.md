---
name: pipeline-lite
description: 轻型 AI 流水线,用于小/低风险开发任务,支持客户端(Unity)与服务端(Fantasy)两端。触发:/pipeline-lite <任务>,以及用户提出"走轻型管线/简易开单/快速派活"类请求。全程在主会话执行:理解需求→实现(客户端/服务端/UI)→自检(客户端先探 UnityMCP:连通即时 run_tests、否则入队待 /pipeline-lite-selftest 补跑;服务端内联)→自审→呈报,不 spawn subagent。
---

# 轻型流水线(全程主会话)

`/pipeline-lite` 全程在主会话内推进:理解需求、形成需求级方案、按端调用实现子 skill、自检(客户端探 UnityMCP:即时或入队 / 服务端内联)、自审、呈报。不 spawn subagent。

实现段按目标端用 **Skill 工具在会话内调用**对应子 skill(不开新上下文):
- 客户端 = `pipeline-lite-dev`(含新 UI 窗口/prefab 时先 `pipeline-lite-ui` 搭建)。
- 服务端 = `pipeline-lite-server-dev`。

> 写或修订实现子 skill 的条款:见 `.claude/skills/pipeline/references/agent-card-authoring.md`——把判断编译成可执行条款(触发 + 动作 + 可核对产出物)的标尺。

## 适用范围

- **本管线(/pipeline-lite)**:小/低风险任务,无需设计稿沉淀——当前唯一在用的流水线入口。
- **需 design-docs 设计稿 / 独立验证 / 高风险方案取舍**的任务:重型 `/pipeline`(含 plan/ui/test 角色)已移出工程,归档于仓库外 `D:\work\_pipeline-archive\.claude\`;确有此类需要时,把该处的 `skills/pipeline/`、`agents/pipeline-*`、`workflows/pipeline-auto.js.archived` 拷回 `.claude/` 对应位置,并将 `skills/pipeline/SKILL.md.archived` 改名回 `SKILL.md` 再走。

本管线全程在对话内推进:不建过程状态文件、不建 design-docs、不归档。上下文被压缩后状态不留存——用户在场,需要时重述任务即可。

## 端(target):client / server

据任务改客户端还是服务端来定 target:

- **client(默认)**:调 `pipeline-lite-dev`,工作根 = UnityProject,工具链 Unity MCP。
- **server**:调 `pipeline-lite-server-dev`,工作根 = Fantasy 仓库(`D:\work\TEngine_block\Fantasy\`,需 `cd` 进去),工具链 dotnet,知识库 fantasy-net。基线 HEAD 与 `git diff` 核对都按 Fantasy 仓库。
- **全栈(两端都改)**:协议依赖锁定顺序——server 段先(改协议 + 导出 + 同步客户端生成物到 UnityProject),client 段后消费;两段各自记基线。不改协议的全栈两段顺序随意。

## UI / prefab 环节(客户端 · 含新窗口/prefab 时)

任务含**新 UI 窗口或 prefab 搭建** → 先调 `pipeline-lite-ui` 搭 prefab(节点层级/组件/容器/布局 + 按命名前缀表生成 `UIBindComponent` 绑定 `_Gen.g.cs` + impl 脚手架),再调 `pipeline-lite-dev` 填业务逻辑(拿 ui 段产出的节点清单直接用)。纯逻辑 / 无新 UI 的任务跳过本环节直接 dev。

lite-ui **直接 MCP 搭建**(精确可控),不走 html-to-ugui。绑定走 BindComponent + `_Gen.g.cs`(合 frog-client),新窗采用、旧窗维持内联不强迁。

## 流程

1. **理解需求**:歧义当场问用户一句(用户在场,不积压)。
2. **形成需求级方案**:必要时轻量 grep / 读码确认可行性与大致指向;**不深读到实现级**——具体改哪个类/方法/接缝在实现段读工程现场推导。
3. **记基线 + 锚工作树**:记录 target 对应仓库的 `git rev-parse HEAD` 作基线(client→UnityProject;server→Fantasy;供回退到本轮提交前)。开工前把工作树里已有的未提交改动(含未跟踪新文件)锚进 git 留恢复点:`git stash -u` 后立即 `git stash apply`——工作副本原样恢复,stash 项留作可 `git stash pop` / reflog 找回的锚;工作树干净则跳过。
   > 流水线的 commit 精确 add 本轮文件、不含用户并发的未提交改动,这些改动始终裸留工作树;不先锚,任何全树操作或误删都无处恢复。未跟踪新文件最脆弱,故用 `-u`。
4. **实现**:按 target 用 Skill 工具调用对应子 skill(client=`pipeline-lite-dev`,含新 UI 窗口先 `pipeline-lite-ui`;server=`pipeline-lite-server-dev`),在会话内直接实现;子 skill 跑完回到本流程继续。
5. **自检**:
   - **客户端**:实现段先探 UnityMCP(`unity-check` 三步探针,会话没挂载 `mcp__unityMCP__*` 视同不可用)。**连到活的 UnityProject** → 即时 `mcp__unityMCP__run_tests`(mode=EditMode)驱动已开 Editor 跑全量、当场判绿,交付即过。**不可用**(server 没起 / 未挂载 / Unity 没开)→ 把自检条目入队 `.claude/pipeline-lite/pending-test.md`,交付标注「自检待跑」,由用户 `/pipeline-lite-selftest` 用 junction 孪生工程 `UnityProject_selftest`(主 Editor 可开着)batchmode 补跑。
   - **服务端**:交付前直接 `dotnet build`(不入队),未过先修再复跑;过后按 server-dev 卡更新重启本机开发服(运行中的旧进程不加载新构建)。
   - **这是本管线唯一的自动验证兜底**——去用户手测后,EditMode/编译测不到的路径无结构化验证。
6. **自审(裁定权仍以非作者视角自持)**:对本次改动跑 `/code-review`(默认 low/med——少而准、合轻量;大改可升 high),以本轮文件清单为范围,忽略工作树里的并发无关改动。查出真缺陷 → 本轮内自行修复(客户端:即时通道复跑 `run_tests` / 队列通道同步更新条目;服务端复跑 `dotnet build` 并再次更新重启本机开发服)。改动触及安全面(server 鉴权/网络/持久化,或客户端处理不可信输入)才追加 `/security-review`,否则跳过。
7. **提交(gate 实绿才提交)**:自检+自审均过、gate 实绿时自动提交本轮改动——精确 `git add` 本轮呈报文件清单(**禁 `git add -A`**:工作树常有并发无关改动)、conventions 格式 commit message + 结尾 `Co-Authored-By` trailer(按环境 git 约定)、**只 commit 不 push**;当前在主干(main)则先切分支再提。**队列通道例外**:客户端自检入队(交付标『自检待跑』、gate 未实绿)时本步推迟,由 `/pipeline-lite-selftest` PASS 时补提交。
8. **呈报**:把改动摘要 + 文件清单 + 审查结论 + 本轮 commit hash 呈报用户;回退提示只用**非破坏性、且只针对本轮文件**的方式,禁全树 `git reset --hard`:未 push 用 `git checkout <基线HEAD> -- <本轮文件清单>` 或 `git revert <本轮commit>` 撤本轮改动,保留工作树里其它未提交改动。
   > 全树 `git reset --hard <基线>` 会连同用户并发的未提交改动(如手写业务代码)一并删除;流水线的 commit 逐文件精确、本不含这些改动,回退也只该 scoped 到本轮文件。

> 自审与写码同一上下文,隔离弱于原 boss/dev 分离(conventions「交叉检」:改动者刚写完最盲)。本管线靠低风险定位 + `/code-review` 独立框架兜底,不再有非作者裁定层。
> **覆盖缺口(去手测后)**:自检门只覆盖编译 + EditMode;PlayMode / UI 手感 / 真机 / 真服路径无自动验证,呈报时提示用户这些路径需自行运行核对。

## 实现段简报(self-contained)

调实现子 skill 前先明确简报,含:需求 + 需求级方案 + **用户视角验收点**(可观测) + 基线 HEAD + 相关区域线索(若有)。子 skill 的工作流/红线在其卡里,简报不复述。

## 再来一轮

用户看呈报/diff 不满意 → 把反馈作为新 brief 再跑一轮实现(**不设自动返修轮次上限**,何时停由用户判断)。自审查出真缺陷 → 同样在本轮内修复。实现段报疑似设计错(实现层绕不过)→ 重审需求级方案,调整后再跑。每轮 gate 过各自提交一个 commit,返修轮叠加多个 commit(用户按需 squash)。

## 红线

- 自检+自审 gate 实绿即自动提交本轮改动(每轮一 commit):精确 `git add` 本轮文件清单(禁 `git add -A`)、conventions commit message + `Co-Authored-By` trailer、只 commit 不 push、在主干先切分支;客户端队列通道推迟到 `/pipeline-lite-selftest` PASS 补提交。实现前记 target 对应仓库基线 HEAD、并把已有未提交改动锚进 git(`git stash -u` 后 `git stash apply`)供恢复;回退只用非破坏性、scoped 到本轮文件的方式(`git checkout <基线> -- <文件清单>` / `git revert`),禁全树 `git reset --hard`。
- 不建过程状态/归档文件、不写 design-docs。
- 写任何持久文件前遵 `.claude/rules/conventions.md`。
