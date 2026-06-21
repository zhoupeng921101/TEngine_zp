---
name: feedback-no-fantasy-ref-in-client-tests
description: 客户端段单测别给 test asmdef 加 Fantasy.Unity 引用核协议字段;那是 server 段编译期事实,client 只测服务层透传 + 桩远程源 + 断服降级。
metadata:
  type: feedback
---

客户端段单测**别给 test asmdef 加 Fantasy.Unity 引用**去核协议字段:`typeof(Fantasy.C2G_XxxRequest)` 在 `BlockBlast.Tests`(无 Fantasy.Unity 引用)报 CS0234。协议「无自报账号字段」是生成物 + server 段(SV8)保证的编译期事实,client EditMode 不重复核生成物字段;客户端段只测「服务层原样透传 rankId/score、客户端 API 面不接账号参」即够(用桩远程源记 `LastSubmitRankId/Score`)。要核断服降级用真实 `RemoteSource` 在无会话下 `Assert.DoesNotThrow`+返 null/ServiceUnavailable。

**Why:** Fantasy.Unity 引用进 test asmdef 会让 EditMode 跑 Fantasy 协议生成路径,易踩 server-side 依赖;协议字段约束在 server 段已有验证,client 重复核是低收益高维护;桩远程源测的是 client 自身意图(透传、不接账号参),与协议字段正交(2026-06,rank-client)。

**How to apply:** 客户端写测远程交互:① test asmdef 不加 Fantasy.Unity 引用;② 桩 IRemoteSource 实现记请求字段;③ 断「服务层调 remote 传的 rankId/score 等于 client API 入参」;④ 断服降级测:真实 RemoteSource 无会话调 `Assert.DoesNotThrow` + 返 null;⑤ 协议字段约束让 server 段去守。
