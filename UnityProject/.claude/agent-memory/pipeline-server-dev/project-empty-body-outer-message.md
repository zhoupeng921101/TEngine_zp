---
name: project-empty-body-outer-message
description: 空 body 的 Outer 消息可用 — `message C2G_X // IRequest,G2C_Y { }` 大括号内留空行;用于身份从会话取的无参请求
metadata:
  type: project
---

空 body 的 Outer 消息(`message C2G_X // IRequest,G2C_Y { }` 大括号内留空行)导出工具支持、生成可用的对象池类型。

**Why:** 「把我的 X 给我」这类无业务参数请求,身份从会话取(防客户端伪造拉他人),消息体没什么要带。define-common.md 的消息示例都带字段,易误以为消息必须有字段——实测 OuterMessage.proto 已有先例(C2G_TestEmptyMessage / C2G_CreateAddressableRequest)。

**How to apply:** 设计新 RPC 协议时遇到「无业务参数」类(如查询自己的 ledger / mailbox),直接写空 body,导出工具会生成正确的对象池类型。Handler 内身份从 `session.GetAccount()` 取,不从协议字段读。
