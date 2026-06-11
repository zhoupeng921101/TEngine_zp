# AI 流水线:策划 → 开发 → 测试

本目录是三个角色 session 的**身份锚 + 进度锚**,用于在上下文被压缩/清空后无缝续工。

## 铁律(每个 session 必须遵守)

> **开工** → 先读 `roles/<我的角色>.md` + `state/<我的角色>.md` + `memory/<我的角色>.md`(+ 相关 `../design-docs/`)
> **干活** → 按角色卡职责执行
> **收尾 / 每完成一个子步骤** → 立刻更新 `state/<我的角色>.md`;新的可复用经验沉淀到 `memory/<我的角色>.md`
> **上下文太长** → 压缩或清空 → 回到「开工」重读即可续,不靠记忆

## 角色与产出

| 角色 | 文件 | 输入 | 产出 | 交接给 |
|------|------|------|------|--------|
| Boss | roles/boss.md | 用户任务 | 编排/验收/重试 + `state/boss.md` 编排日志 | 各角色 |
| 策划 plan | roles/plan.md | 需求/想法 | `../design-docs/*.html` + 验收标准 | 开发 |
| 开发 dev  | roles/dev.md  | 设计文档 + 验收标准 | 代码改动 + 改动摘要 + 验证点 | 测试 |
| 测试 test | roles/test.md | 改动摘要 + 验证点 | 测试报告(4 类) | 开发(打回)或你(通过) |

> **Boss 也会失忆**:main 会话被 clear/压缩后,boss 开工先读 `roles/boss.md` + `state/boss.md`,
> 再从各 `state/*.md` 交接区 + `subagents(action=list)` **现场推导进度**决定续跑。
> `state/boss.md` 只记不可推导信息(任务定义/决策/spawn 登记/遗留事项),**不记阶段进度**;
> 编排动作(spawn/拍板/打回/关单)发生后必须立刻更新它,尤其是子会话的 session key。

## 交接方式(方案 B:boss 编排)

由 main(boss)用 `sessions_spawn` 拉起角色 sub-agent,收产出按 `roles/boss.md`「能力边界」节执行(首选验文件,会话型才 pull),再把上一棒产出喂给下一棒。
开发完成后,交接内容固定为「**改动摘要 + 文件清单 + 验证点**」写入 `state/dev.md` 的「交接区」,测试从那里读。

## 项目规范来源(不要重复造轮子)

- 编码规范 / 强制工作流:`../CLAUDE.md`(L1-L4 + tengine-dev skill + 编码红线)
- 知识库:`.claude/skills/tengine-dev/references/`
- 设计文档:`../design-docs/`
