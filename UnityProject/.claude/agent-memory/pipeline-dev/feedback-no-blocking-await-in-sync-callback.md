---
name: feedback-no-blocking-await-in-sync-callback
description: 同步发 RPC+等响应类设计别用 GetAwaiter().GetResult() 等 UniTask;重构=扣费前移:读价→await RPC→服务端 OK 才调本地业务,扣费接缝传 _=>true。
metadata:
  type: feedback
---

「同步发 RPC + 等响应 + 接业务接缝」类设计稿要求(扣钻 / 扣体力 / 扣道具等):本地业务方法的 sync 回调签名(如 `Func<int,bool> trySpendDiamond`)**别用 `GetAwaiter().GetResult()` 同步等 UniTask**(会卡 UI 主线程)。

重构思路 = 把扣费拆到本地业务方法**之前**:① 读价(read-only,不动状态)→ ② cost>0 时 await RPC → ③ 服务端 OK 才调本地业务方法、扣费接缝传 `_=>true`(扣费已在 ② 完成)。

注意业务方法的成功路径若有副作用(写名 / 计数 +1)就不能做 dry-run 校验,否则二次进入时计费会污染(单测能直接捕获这种 bug)。

**Why:** UniTask `GetAwaiter().GetResult()` 在主线程上等异步是死锁/卡帧主因;扣费前移让 RPC 在异步入口完成,本地同步路径只做无 IO 工作;有副作用的成功路径不能做 dry-run 是常被忽略的陷阱(2026-06,player-attr-client)。

**How to apply:** 设计稿要「同步本地方法 + 服务端校验扣费」时:① 不用 GetResult 同步等;② 异步入口流程:read 价→await RPC→服务 OK→调同步业务、扣费接缝 `_=>true`;③ 业务方法有副作用就别 dry-run 校验,改为前置 read-only 校验;④ 单测构造「读价→RPC→业务」三步分别失败的路径。
