---
name: project-unity-batchmode-editmode-tests
description: Unity Editor 不可达时,命令行 batchmode 可跑 EditMode 单测(前提编辑器未占用工程)
metadata:
  type: project
---

Unity Editor 不可达(未启动/被占用)时,命令行 batchmode 可跑 EditMode 单测:

```
"D:\Program Files\Unity\6000.4.7f1\Editor\Unity.exe" -batchmode -projectPath <UnityProject> -runTests -testPlatform EditMode -testResults <xml> -logFile <log>
```

前提是编辑器未占用工程(先 `Get-Process Unity` 核对)(2026-06-12 实测)。

**Why:** 客户端 dev 交付后 test 需跑 EditMode 单测;Unity Editor MCP 未启动 / 工程被占用时,UI 路径走不通。batchmode 给一条不依赖 Editor 实时连接的兜底路径。

**How to apply:** test 跑单测前先 `Get-Process Unity`:已开 = 走 Editor MCP run_tests;没开 = 走 batchmode 命令行。两者结果格式不同(XML vs MCP JSON),解析时注意。Editor 已开但被占用(锁文件)时,batchmode 也跑不了——这种判 BLOCKED-env。
