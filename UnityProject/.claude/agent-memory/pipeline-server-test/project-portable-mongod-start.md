---
name: project-portable-mongod-start
description: 便携版 mongod 起服命令(D:\mongodb-portable + 27017 + 日志路径)
metadata:
  type: project
---

mongod 不在运行时可直接启动:

```
D:\mongodb-portable\mongodb-win32-x86_64-windows-8.3.4\bin\mongod.exe --dbpath D:\mongodb-portable\data --port 27017 --logpath D:\mongodb-portable\mongod.log --logappend
```

后台运行(Bash & 方式),3s 后检查 27017 端口即可。

**Why:** 服务端真往返类 SV(改名扣钻/邮件领奖/ledger 写入等)依赖 MongoDB 27017 可达;不可达判 BLOCKED 非 FAIL,但能起就别 BLOCKED。

**How to apply:** server-test 开工先 `Get-Process mongod` + `Test-NetConnection 127.0.0.1 27017` 探测;未起则用上方命令拉起,无需依赖已有 mongod 进程。已起则直接用,别重复起(会 DBPathInUse 锁文件占用)。
