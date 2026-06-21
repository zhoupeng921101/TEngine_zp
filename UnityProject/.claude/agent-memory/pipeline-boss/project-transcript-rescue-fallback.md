---
name: project-transcript-rescue-fallback
description: 子 agent 对话全文落盘 ~/.claude/projects/<项目>/<session>.jsonl(JSONL,约 30 天);多轮打回时读上轮 transcript 提炼「前任工作纪要」塞简报
metadata:
  type: project
---

备用手段·transcript 抢救:子 agent 对话全文落盘 `~\.claude\projects\<项目>\<session>.jsonl`(JSONL,保留约 30 天)。

**Why:** [[project-sendmessage-not-usable]] 决定打回必 spawn 新 agent;新 agent 零上下文有时会重蹈前任覆辙(走同样错误路径)。读上一轮 dev 的 transcript 能提炼出「前任试过什么、卡在哪、为什么没走通」,塞进简报作为手工版续接。

**How to apply:** 小概率场景的手段,不进固定流程。多轮打回(典型 3 轮+)怀疑新 dev 重蹈覆辙时才用;一两轮内的打回靠简报「FAIL 报告 + 改动建议」即可,不必动 transcript。
