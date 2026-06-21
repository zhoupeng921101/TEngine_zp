---
name: project-mongosh-not-in-portable
description: mongodb-portable 不含 mongosh,MongoDB 直查须用服务端 Log 替代或另装 mongosh
metadata:
  type: project
---

mongosh 不在本项目 mongodb-portable 包内(portable 包只含 mongod.exe),MongoDB accounts 集合直查须用服务端 Log 替代:AccountServiceComponent Init + 客户端完整登录链路 console 组合构成 E1 真往返的充分证据;若要直查须另行安装 mongosh。

**Why:** 2026-06 account-client E1 实测,误以为 portable 包含 shell,实测目录只有 mongod.exe;直查不可达时用服务端 log 双边对照构造证据。

**How to apply:** 账户/持久化数据 E1 验证:首选服务端 Log + 客户端 console 双边对照证据链;非要直查再单独装 mongosh(下载 mongosh.zip 解压加 PATH)。
