---
name: project-fantasy-server-log-as-evidence
description: Fantasy 服务端 Develop log 是真往返证据,mongosh 不可用时可替代 MongoDB 直查
metadata:
  type: project
---

服务端 Log 是真往返的双边证据之一:Fantasy 服务端对每次 RPC 处理结果写 Develop Debug.log,路径 `Bin/Debug/Logs/Server/YYYYMMDD/Develop/Log.Develop.*.Debug.log`;上报/查榜条目与客户端 console 对照可完整核验往返正确性,mongosh 不可用时可用服务端 log 替代 MongoDB 直查。

**Why:** 2026-06 rank E1 实测,本项目 mongodb-portable 包不含 mongosh,无法直查集合;服务端 Develop log 详细记录每次 RPC 输入/输出,与客户端 console 对照足以构成真往返证据。

**How to apply:** 真往返证据链优先级:①客户端 console + 服务端 Develop log 双边对照(最稳)②有 mongosh 时再加 MongoDB 直查;log 路径每天一目录(YYYYMMDD)。
