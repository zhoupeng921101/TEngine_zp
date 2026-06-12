# 角色记忆:Boss(跨任务经验)

> 开工先读本文件;收尾把新的可复用经验沉淀进来(一条一行,过时即删)。
> 准入:只记跨任务可复用、且 pipeline SKILL/conventions 未覆盖的经验。

- Unity Editor 不可达(未启动/被占用)时,命令行 batchmode 可跑 EditMode 单测:`"D:\Program Files\Unity\6000.4.7f1\Editor\Unity.exe" -batchmode -projectPath <UnityProject> -runTests -testPlatform EditMode -testResults <xml> -logFile <log>`,前提是编辑器未占用工程(先 `Get-Process Unity` 核对)(2026-06-12 实测)
- batchmode 跑不了 Play 交互手验;UI 表现类验收要么留给带 Unity MCP/人工的环节,要么在验收标准里就把逻辑与表现拆开(逻辑进单测,表现列遗留)(2026-06)
