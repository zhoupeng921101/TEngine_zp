---
name: batch-rpc-check-ratelimit-granularity
description: 把逐项单发 RPC 合并成批量前,先查服务端频率闸粒度;per-account 粒度闸下循环调 gated 单发会使第 2 项起被 RateLimited 误拒,必须抽 gate-free 核心 + 整批只检一次
metadata:
  type: reference
---

把「一个事件逐项串行单发」的 RPC 合并成一条批量 RPC 时,服务端 handler 常想「循环调用现有单发链路的核心方法」复用逻辑。**做前必须先查那条单发核心的频率闸(rate gate)是按什么粒度 keyed 的**,否则批量会被自己的闸误拒。

判据:
- 闸 key = `account | type`(每种 type 一个 key)→ 一个 batch 内每 type 至多一项时天然不撞,循环调单发**安全**(如属性变更 C2G_PropertyChange,gate key 含 PropertyType)。
- 闸 key = `account`(或 `account | 固定串`)单键 → 一个 batch 内第 1 项置位后,第 2 项起 `now - prev < min` 必被 RateLimited **误拒**(如修饰解锁 CosmeticHelper,gate key = `account|"unlock"`)。

per-account 单键闸的正确批量做法:
1. 把单发核心拆成两段:①频率闸判定(读写 LastAtMs 字典);②gate-free 的 sanity + 写库核心。
2. 单发 = 闸 + 核心;批量 = **整批只调一次闸**(一次合法批操作),过闸后**逐项只调 gate-free 核心**,逐项 sanity 失败跳过不阻断整批。
3. 被闸挡 → 整批返 RateLimited + 当前权威态(客户端下次边界重试)。

**Why:** 单发核心「看起来可复用」,直接 foreach 调它是最自然的写法,但把 per-account 闸也循环触发了——batch 里除第一项全被拒,静默丢奖励/丢解锁,且 happy-path 单测(单项 batch)测不出来(只有多项 batch 才暴露)。

**How to apply:** 服务端加任何「批量版」RPC handler 前,grep 目标单发 helper 的频率闸 rateKey 构造;per-account 单键就重构出 gate-free 核心、批量层 gate 一次。相关:属性批量与修饰解锁批量分别是「安全(per-type)」与「需重构(per-account)」的对照实例。
