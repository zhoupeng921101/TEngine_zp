---
name: feedback-dll-lock-blocked
description: Main.exe 持有产物 DLL 文件锁时 dotnet run 报 MSB3027 的处置口径
metadata:
  type: feedback
---

当现有 Main 进程正在运行时,`dotnet run` 触发增量构建会因 DLL 文件锁(MSB3027/MSB3021「文件被 Main (PID) 锁定」)失败,无法起新服进程。

**Why:** 产物 DLL 被占用属构建环境错,与代码质量无关;按角色卡判定「产物 DLL 被占用 = BLOCKED」而非 FAIL。[[feedback-blocked-vs-fail]]

**How to apply:**
- 判定 BLOCKED-env,不判 FAIL
- 编译/源生成器/Code Review 三类仍执行(用 `dotnet build Server.sln` 而不是 `dotnet run`,build 不触发重复复制产物)
- MongoDB 探针(`D:\tmp\mongo_probe_mail` 等)可独立运行验证存储持久层(SV12/SV13),不受进程锁影响
- RPC 真往返(依赖客户端会话触发)列为 BLOCKED,写补跑命令清单进报告
- 等 Main 进程退出后可直接执行补跑清单验 SV1-SV9/SV11
