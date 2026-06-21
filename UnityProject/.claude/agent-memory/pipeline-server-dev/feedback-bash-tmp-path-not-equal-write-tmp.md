---
name: feedback-bash-tmp-path-not-equal-write-tmp
description: bash 工具的 /tmp 与 Write 工具的 /tmp 路径解析不一致;写 bash 临时探针工程用绝对 Windows 路径
metadata:
  type: feedback
---

bash 工具的 `/tmp` = `C:\Users\pc\AppData\Local\Temp`(git-bash usertemp 挂载),但 Write 工具写 `/tmp/...` 时两者路径解析可能不一致(Write 报成功 bash 却 ls 不到)。

**Why:** Write 工具与 Bash 工具背后的 shell 是不同进程/不同 mount 表,Write 解析 `/tmp` 可能落到别处。具体表现:Write 报「文件已创建」,但 bash 后续 `dotnet run` 报 CS5001「找不到 Main」(其实是文件没落到 bash 看的目录)。

**How to apply:** 写「bash 要 dotnet run 的临时探针工程」时,Write 用解析后的绝对 Windows 路径 `C:\Users\pc\AppData\Local\Temp\<name>\Program.cs`,**不**用 `/tmp/<name>/Program.cs`。Bash 里 `dotnet run --project C:\Users\pc\AppData\Local\Temp\<name>` 也用绝对路径。两侧路径一致即避坑。
