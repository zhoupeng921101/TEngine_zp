# 角色记忆:Boss(跨任务经验)

> 开工先读本文件;收尾把新的可复用经验沉淀进来(一条一行,过时即删)。
> 准入:只记跨任务可复用、且 pipeline SKILL/conventions 未覆盖的经验。

- 项目 CLAUDE.md 会自动注入子 agent,但注入的是**主会话启动时的快照**:改 CLAUDE.md 后须重启会话才对子 agent 生效(2026-06-12 探针实测,新增章节未出现在子 agent 上下文)
- SendMessage 续接子 agent 在本环境不可用(2026-06-12 探针实测):打回一律 spawn 新 dev、靠 state/test.md 可复现清单交接;再次 spawn 同类型 agent 是全新会话、零上下文(隔离彻底,盲评保证成立)
- 备用手段·transcript 抢救:子 agent 对话全文落盘 `~\.claude\projects\<项目>\<session>.jsonl`(JSONL,保留约 30 天)。多轮打回怀疑新 dev 重蹈前任覆辙时,读上一轮 dev 的 transcript 提炼「前任工作纪要」塞进简报——手工版续接。小概率场景的手段,不进固定流程
- Unity Editor 不可达(未启动/被占用)时,命令行 batchmode 可跑 EditMode 单测:`"D:\Program Files\Unity\6000.4.7f1\Editor\Unity.exe" -batchmode -projectPath <UnityProject> -runTests -testPlatform EditMode -testResults <xml> -logFile <log>`,前提是编辑器未占用工程(先 `Get-Process Unity` 核对)(2026-06-12 实测)
- batchmode 跑不了 Play 交互手验;UI 表现类验收要么留给带 Unity MCP/人工的环节,要么在验收标准里就把逻辑与表现拆开(逻辑进单测,表现列遗留)(2026-06)
