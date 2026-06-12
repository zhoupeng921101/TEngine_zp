---
name: audit
description: 规则栈审计,防规则副本/死规则/矛盾腐烂。触发:/audit、用户要求"审计规则/查规则栈"、pipeline 关单事务发现规则栈有改动时。
---

# 规则审计

审计程序的单一信息源是 `.claude/rules/conventions.md`「规则审计」节,本文件只是触发入口与执行要点,不复制程序条文。

执行要点:

1. **增量短路**:读 `.claude/rules/audit-log.md` 的上次审计基线(git rev/日期),规则栈自基线无改动 → 在 audit-log 记「无变化,跳过」即止
2. **规则栈范围**:项目根 `CLAUDE.md`、`.claude/rules/`、`.claude/skills/*/SKILL.md`、`.claude/agents/*.md`、`pipeline/memory/*.md` 文件头准入规则
3. 有改动 → 按 conventions「规则审计」查三样:重复 / 死规则 / 矛盾
4. 结论追加 `.claude/rules/audit-log.md`(日期、基线、结论;一次审计一段)
5. **删除候选先报用户拍板,不静默删**
