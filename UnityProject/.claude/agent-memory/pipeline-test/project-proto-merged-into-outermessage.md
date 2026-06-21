---
name: project-proto-merged-into-outermessage
description: 协议生成物合并成 OuterMessage.cs/OuterEnum.cs 单文件,grep 协议字段查这两个文件而非按消息名 glob
metadata:
  type: project
---

协议生成物在本项目合并成单文件(OuterMessage.cs / OuterEnum.cs),grep 查协议字段时目标须是这两个文件,而非按消息名 glob。

**Why:** 2026-06 rank client 实测,默认按消息名 `*RankXxx*.cs` glob 查不到,因协议生成器把全部消息平摊进 OuterMessage.cs 单文件。

**How to apply:** 查协议字段/消息定义:`grep -n "XxxMessage\|XxxResponse" OuterMessage.cs`;查协议枚举值同理查 OuterEnum.cs。
