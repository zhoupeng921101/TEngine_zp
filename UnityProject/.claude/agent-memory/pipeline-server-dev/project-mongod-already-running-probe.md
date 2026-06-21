---
name: project-mongod-already-running-probe
description: 本机 MongoDB 可能已有便携版 mongod 在跑;再起会 DBPathInUse;先探测再用
metadata:
  type: project
---

本机 MongoDB 可能已有 mongod 在跑(便携版 `D:\mongodb-portable`);再起一个会 DBPathInUse(锁文件占用)。

**Why:** 便携版 mongod 用文件锁防多实例;同 dbpath 启第二个 mongod 实例会立即崩。已在跑就用现有实例即可,起服需求是「确保 27017 可达」而非「启新进程」。

**How to apply:** server-dev / server-test 起服前先 `Get-Process mongod` 探测 + `Test-NetConnection 127.0.0.1 27017`,已在跑就直接用,别重复起。便携版只带 mongod/mongos,无 mongosh/mongo 客户端壳——查库走 .NET 驱动探针(参 [[project-mongodb-isolated-probe]]),不靠 shell。
