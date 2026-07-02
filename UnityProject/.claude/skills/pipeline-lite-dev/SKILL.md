---
name: pipeline-lite-dev
description: 轻型流水线客户端实现段。在 Unity 工程(UnityProject)实现功能,交付时不跑 unity,把自检条目入队 pending-test.md(编译 + EditMode 自检由 /pipeline-lite-selftest 补跑)。由 pipeline-lite 主会话流程在 client 实现段用 Skill 工具调用,不单独触发。
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

## 自检入队(交付前必做)
客户端自检(batchmode 编译 + EditMode)与 dev 交付解耦(即时交付 + 批量补检):dev 段**不跑 unity**,改为往 `.claude/pipeline-lite/pending-test.md` 追加一条自检条目,由用户手动 `/pipeline-lite-selftest` 批量补跑(自检跑在 junction 孪生工程,主 Editor 可开着,不需为此关 Editor)。
- 按 `pending-test.md` 头注的条目格式追加一条(任务标题 / 基线 HEAD / 改动文件 / 关键 EditMode 测试 / 期望),不复述格式(以该文件头注为准)。
- 改动涉及的关键异常路径**可单测的**,补 EditMode 测试进 `Assets/Editor/Tests/`,并在条目「关键 EditMode 测试」里点名,让自检覆盖到。

> 自检是本管线唯一自动验证兜底(去用户手测后),但执行时机后移到 `/pipeline-lite-selftest`:dev 交付时代码尚未过编译,呈报须标注「自检待跑」。EditMode 测不到的 PlayMode / UI 手感 / 真机路径无结构化验证,尽量把可单测的异常路径补进队列条目。

## 红线
- 疑似需求/方案本身有错时,先取证确认(读相关代码/接缝、核对验收点,排除「实现层可微调绕过」)——确是微调救不了的才停手回主流程重审需求级方案,不越界自改需求;能在实现层消解的不报,自己实现。
- 写持久文件前遵 `.claude/rules/conventions.md`。

## 收尾
仅当一条经验是**规则型、可复用、能改变未来同类任务行为**时,才沉淀到 `.claude/agent-memory/pipeline-dev/<slug>.md`(共享经验库,重型 dev 也受益)。准入按 `.claude/rules/conventions.md`§规则准入:举得出「没有这条、下次会做错」的**复发**场景才写;单次事件、「修过 X / 解决了 Y」式过程记录、模型默认就会的事一律不写——这些进 git 历史,不进记忆。结构:独立结构化文件,frontmatter name/description/type,body rule + **Why:** + **How to apply:**;加索引到该目录 MEMORY.md。
