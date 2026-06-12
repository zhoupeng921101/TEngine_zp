# 角色记忆:Boss(跨任务经验)

> 开工随角色卡一起读;收尾把新的可复用经验沉淀进来(一条一行,过时即删)。
> 准入:只记跨任务可复用、且角色卡/CONVENTIONS 未覆盖的经验。

- dev/test sub-agent 会话不带 Unity MCP(2026-06-12 两棒实测),编译/单测运行门由 boss 补跑;boss 会话也无 MCP 时走命令行 batchmode:`"D:\Program Files\Unity\6000.4.7f1\Editor\Unity.exe" -batchmode -projectPath <UnityProject> -runTests -testPlatform EditMode -testResults <xml> -logFile <log>`,前提是编辑器未占用工程(先 `Get-Process Unity` 核对)。派 dev/test 时在简报里预告此分工,避免子会话把「跑不了」当阻塞反复试。
- batchmode 跑不了 Play 交互手验;UI 表现类验收要么留给带 Unity MCP/人工的环节,要么在验收标准里就把逻辑与表现拆开(逻辑进单测,表现列遗留)。
