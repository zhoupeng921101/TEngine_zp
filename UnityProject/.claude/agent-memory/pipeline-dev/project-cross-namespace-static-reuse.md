---
name: project-cross-namespace-static-reuse
description: 跨命名空间复用规整/工具静态方法:同一 GameLogic 程序集内全限定名直调,配置层 key 与服务层比对 key 走同一 Normalize 防「输对码却 NotFound」。
metadata:
  type: project
---

跨命名空间复用规整/工具静态方法(配置桥接 `GameLogic.Config.RedeemConfigMgr` 调服务层 `GameLogic.Redeem.RedeemService.Normalize`):同一 GameLogic 程序集内全限定名直调即可,配置层存 key 与服务层比对 key 走同一 Normalize 是防「玩家输对码却 NotFound」的根治做法(不在两处各写一份规整逻辑)。

**Why:** 同一程序集内不同 namespace 可全限定名直调,无需跨 asmdef 引用;规整逻辑(去空格/大小写/全角半角)若两处副本,任意一处改动后必然漂移,玩家输对的码也会 NotFound——根治是 single source(2026-06,redeem-code)。

**How to apply:** 涉及输入→匹配的功能:① 找规整/normalize 静态方法的现有归属(优先放服务层);② 所有读/写/比对路径都 import 该 namespace 调同一方法;③ 不在多处写"等价"的规整逻辑;④ 加单测「同一字符串经任意路径规整结果相同」。
