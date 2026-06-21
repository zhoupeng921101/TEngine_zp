---
name: project-sendmessage-not-usable
description: SendMessage 续接子 agent 在本环境不可用;打回一律 spawn 新 agent + state 交接
metadata:
  type: project
---

SendMessage 续接子 agent 在本环境不可用(2026-06-12 探针实测)。

**Why:** 探针调 SendMessage 想给已完成的 dev agent 续接打回,实际未触发——可能是 Claude Code 配置/版本不支持,或子 agent 完成后已被回收。再次 spawn 同类型 agent 是全新会话、零上下文(隔离彻底,独立评审保证成立)。

**How to apply:** 打回路径一律走 spawn 新 dev,靠 state/test.md 的可复现清单交接(diff/symptom/repro)。简报里把 test 的 FAIL 报告原样附上,让新 dev 读后自决定改动范围。备用的 transcript 抢救手段见 [[project-transcript-rescue-fallback]]。
