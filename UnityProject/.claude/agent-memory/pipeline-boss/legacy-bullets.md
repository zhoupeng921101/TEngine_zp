---
name: legacy-bullets
description: pipeline-boss 历史经验整体归档(从 pipeline/memory/boss.md 迁入,后续触发时按条独立化)
metadata:
  type: project
---

> 本文件是 2026-06-21 迁移期的整体归档:把 `pipeline/memory/boss.md` 原 bullet list 一次性搬入,保留紧凑性。后续 boss 收尾沉淀新经验时按结构化格式独立写(`feedback-*.md` / `project-*.md` 等),遇本文件内重复或过时条目可独立化或删除。

- 项目 CLAUDE.md 会自动注入子 agent,但注入的是**主会话启动时的快照**:改 CLAUDE.md 后须重启会话才对子 agent 生效(2026-06-12 探针实测,新增章节未出现在子 agent 上下文)
- SendMessage 续接子 agent 在本环境不可用(2026-06-12 探针实测):打回一律 spawn 新 dev、靠 state/test.md 可复现清单交接;再次 spawn 同类型 agent 是全新会话、零上下文(隔离彻底,独立评审保证成立)
- Workflow 机制可用(2026-06-12 探针实测:后台启动/内部 spawn/返回值/完成通知唤醒四环节全通,不占主会话回合)。按 `Workflow({name:'pipeline-auto', args:{...}})` 调用,args 由 pipeline-auto.js 直接消费。
- 备用手段·transcript 抢救:子 agent 对话全文落盘 `~\.claude\projects\<项目>\<session>.jsonl`(JSONL,保留约 30 天)。多轮打回怀疑新 dev 重蹈前任覆辙时,读上一轮 dev 的 transcript 提炼「前任工作纪要」塞进简报——手工版续接。小概率场景的手段,不进固定流程
- Unity Editor 不可达(未启动/被占用)时,命令行 batchmode 可跑 EditMode 单测:`"D:\Program Files\Unity\6000.4.7f1\Editor\Unity.exe" -batchmode -projectPath <UnityProject> -runTests -testPlatform EditMode -testResults <xml> -logFile <log>`,前提是编辑器未占用工程(先 `Get-Process Unity` 核对)(2026-06-12 实测)
- batchmode 跑不了 Play 交互手验;UI 表现类验收要么留给带 Unity MCP/人工的环节,要么在验收标准里就把逻辑与表现拆开(逻辑进单测,表现列遗留)(2026-06)
