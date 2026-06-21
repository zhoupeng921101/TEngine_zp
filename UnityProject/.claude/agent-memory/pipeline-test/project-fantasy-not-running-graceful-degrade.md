---
name: project-fantasy-not-running-graceful-degrade
description: Fantasy 未起时「重连次数已达上限」error 属网络降级日志非业务崩溃,不升 FAIL
metadata:
  type: project
---

Fantasy 服务端未起时 PlayMode 下日志会出现「重连次数已达上限」error:属 Fantasy 内部网络降级日志,非业务崩溃,不升 FAIL;RemoteAttrLedgerSource Session=null → NetworkDown 兜底文案「网络异常,请检查连接」属正常降级路径。

**Why:** 2026-06 ledger-client V1 实测,Fantasy 客户端 SDK 内部把重连耗尽后写 error 级日志,但业务侧已捕获并走 NetworkDown 兜底,实际行为正确;误判会让无服务端环境下测试全 FAIL。

**How to apply:** 看 console error 时先判子类:①「重连次数已达上限」+ 业务层有 NetworkDown 兜底文案 = 降级路径,不计 FAIL ②真业务异常(NRE/ArgumentException)= FAIL;BLOCKED 时优先标 BLOCKED-no-server。
