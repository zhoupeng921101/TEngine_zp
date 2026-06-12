# 规则审计日志

> `/audit` 每次执行后追加一段:日期、规则栈基线(git rev 或文件清单时间)、结论。增量短路以最近一段的基线为准。本文件超 ~200 行归档到同目录 `archive/`。

## 2026-06-12 首次审计(全量)

- 基线:git `2703b2bd`(工作流水管线迁移到 claude code)+ 未提交改动(`.claude/skills/pipeline/SKILL.md`、`pipeline/memory/boss.md`)
- 范围:CLAUDE.md、`.claude/rules/`、各 SKILL.md、`.claude/workflows/pipeline-auto.js`、`.claude/agents/*.md`、`pipeline/memory/*` 文件头
- 结论:
  1. **重复(删除候选,待用户拍板)**:CLAUDE.md「触发时机」表 +「📚 References 参考文档」表 与 tengine-dev SKILL.md「文档路由」表同源枚举,且已漂移(7 主题 vs 11 行路由;14 篇 vs 11 行)。候选修法:CLAUDE.md 侧两表压成指针(知识路由的单一信息源归 SKILL.md)
  2. **失效引用(修正候选,待用户拍板)**:CLAUDE.md 两处指向 `.claude/memory/`(冲突仲裁记录、自我优化 problem_*.md 落点),该目录不存在。候选修法:(a) 建目录 (b) 改指既有位置
  3. **矛盾(已修)**:打回简报「附上一轮 dev 最终回复」在 pipeline SKILL.md 与 pipeline-auto.js 两套实现间不同步,已同步 js
  4. 健康面:活跃规则栈零 OpenClaw 残留、零失效路径(wiki-query-agent I:\ 已修)、红线/写作规约均单源+指针、无对话残留(lint 命中仅 conventions 规则原文,合法);archive/ 内 roles/ 引用为冻结历史证据,不修
  5. 死规则:首轮无累计窗口,不判;下轮起累计观察
- 处置(同日,用户拍板):①已修——CLAUDE.md 两表压成指针,路由单源归 tengine-dev SKILL.md;②选 a——已建 `.claude/memory/`(含 README 说明落点);③源头解决——「档案体不进对话」入 CLAUDE.md「表达与协作」(归位:表达层规则覆盖一切对用户呈报,pipeline SKILL.md 不打补丁)
