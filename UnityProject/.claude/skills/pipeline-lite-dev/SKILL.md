---
name: pipeline-lite-dev
description: 轻型流水线客户端实现段。在 Unity 工程(UnityProject)实现功能;交付前自检先探 UnityMCP,连通则即时跑 EditMode(run_tests),不可用则把自检条目入队 pending-test.md(由 /pipeline-lite-selftest 用孪生 batchmode 补跑)。由 pipeline-lite 主会话流程在 client 实现段用 Skill 工具调用,不单独触发。
---

# 轻型流水线 · 客户端实现段

pipeline-lite 主会话在 client 实现段调用本 skill:基于需求级简报,在 Unity 工程里实现功能并过自检门,产出直接进主流程(无独立 test 角色、无用户手测)。

## 强制工作流 / 编码红线(继承项目规范,不可跳过)
- 严格遵守 `.claude/skills/tengine-dev/conventions-dev.md`「强制工作流」(判级→查规范→编码),步骤细节以正本为准、不在此复述。
- 编码红线唯一信息源:`.claude/skills/tengine-dev/SKILL.md`「核心红线」,逐条遵守。
- 涉及热更/网络(Fantasy / 客户端 RPC):读 `.claude/skills/tengine-dev/references/hotfix-workflow.md`(asmdef 引用不传递、UniTask/FTask 契约、RPC 回包等)。

> 不在此复制上述条文:副本必漂移。

## 开工前
- 读共享 dev 经验库 `.claude/agent-memory/pipeline-dev/`(MEMORY.md 索引 + 各独立 .md;本 skill 不被系统自动注入,手动 Read)。

## 输入
主会话的 self-contained 简报即规格:需求 + 需求级方案 + 用户视角验收点 + 基线 HEAD。**无 design-docs、无 plan.md**。

> 自行读工程把需求映射到接缝(grep 符号 / 找现有链路 / 判可行性),代码是单一事实源。映射不出或接法不通(确是需求层错、实现层绕不过)才回主流程重审需求级方案。

## 自检(交付前必做):先探 UnityMCP,能则即时、不能则入队
客户端自检(编译 + EditMode)按 UnityMCP 可用性二选一,两条路判的都是同一口径:0 编译错误 + 全量 EditMode 绿。

1. **探测**:走 `unity-check`(三步探针)判 UnityMCP 是否连到活的 UnityProject 实例;会话内没挂载 `mcp__unityMCP__*` 工具视同不可用。
2. **可用 → 即时跑**:`mcp__unityMCP__run_tests`(mode=EditMode)驱动已开的 Editor 跑全量 EditMode,`get_test_job` 取回判绿(`test_filter` 失效恒跑全套 / 失败列表 25 条截断 / Explicit-skip 等判绿坑见共享经验库 `unitymcp-run-tests-filter`,不复述)。全套 failed=0 → 交付即绿,不入队;出现失败 → 本轮据失败用例定位修复、重跑,修完再交付。
3. **不可用 → 入队**(unity-check 定位到 server 没起 / 未挂载 / Unity 没开或编译中):往 `.claude/pipeline-lite/pending-test.md` 追加一条自检条目(格式以该文件头注为准:任务标题 / 基线 HEAD / 改动文件 / 关键 EditMode 测试 / 期望),交付标注「自检待跑」,留用户 `/pipeline-lite-selftest` 用 junction 孪生工程 batchmode 补跑。

改动涉及的关键异常路径**可单测的**,补 EditMode 测试进 `Assets/Editor/Tests/`(即时跑当场覆盖;入队则在条目「关键 EditMode 测试」点名让补跑覆盖到)。

> 即时通道优先:Editor 已开、MCP 连通时驱动它跑零额外启动成本、当场出绿;孪生 batchmode 队列是 Editor/MCP 不可用时的兜底,不必为此关 Editor。自检(两条路之一)是本管线去手测后唯一自动验证兜底;走队列时 dev 交付代码尚未过编译,呈报须标注「自检待跑」。EditMode 测不到的 PlayMode / UI 手感 / 真机路径两条路都无结构化验证。

## 红线
- 疑似需求/方案本身有错时,先取证确认(读相关代码/接缝、核对验收点,排除「实现层可微调绕过」)——确是微调救不了的才停手回主流程重审需求级方案,不越界自改需求;能在实现层消解的不报,自己实现。
- 写持久文件前遵 `.claude/rules/conventions.md`。

## 收尾
仅当一条经验是**规则型、可复用、能改变未来同类任务行为**时,才沉淀到 `.claude/agent-memory/pipeline-dev/<slug>.md`(共享经验库)。准入按 `.claude/rules/conventions.md`§规则准入:举得出「没有这条、下次会做错」的**复发**场景才写;单次事件、「修过 X / 解决了 Y」式过程记录、模型默认就会的事一律不写——这些进 git 历史,不进记忆。结构:独立结构化文件,frontmatter name/description/type,body rule + **Why:** + **How to apply:**;加索引到该目录 MEMORY.md。
