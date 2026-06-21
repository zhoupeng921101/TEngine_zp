# AI 流水线:策划 → 开发 → 测试

流水线由 `/pipeline` 驱动:编排逻辑(boss)在 `.claude/skills/pipeline/SKILL.md`,执行角色是 `.claude/agents/` 下的 pipeline-plan / pipeline-ui / pipeline-dev / pipeline-test(客户端)与 pipeline-server-dev / pipeline-server-test(服务端,工作根在 Fantasy 仓库 `D:\work\TEngine_block\Fantasy\`)(角色卡即 agent 定义,spawn 自动注入)。自治模式脚本:`.claude/workflows/pipeline-auto.js`(target=client/server 选端)。

本目录是流水线的**数据区**:

| 目录 | 内容 | 维护 |
|------|------|------|
| `state/` | 各角色交接区 + boss 编排日志(进度真相) | 角色干活时更新;关单时归档并重置空槽 |
| `memory/` | 各角色跨任务经验(准入见各文件头) | 角色收尾沉淀;过时即删 |
| `archive/` | 已关单任务的 state 快照 | boss 关单事务写入 |

## 铁律

- **文件是真相**:进度只从 `state/` 各交接区现场推导,不在别处记副本
- 角色开工读自己的 state + memory;收尾更新交接区、沉淀经验
- 编码规范/强制工作流:项目根 `CLAUDE.md`;持久文件写作:`.claude/rules/conventions.md`
