---
name: project-trychange-boundary-reference-values
description: TryChangeAsync 边界测试参考(design 38):AttrType.All 本地拒、delta=0 服务端 Success、连续两次依序处理
metadata:
  type: project
---

TryChangeAsync 边界测试参考值(design-docs/38 落地数据):AttrType.All=-1 本地即拒,无 RPC;delta=0 服务端返 Success(余额不动,无推送);连续两次请求服务端依序处理(无重入锁问题);E2 真往返路径:用 +500 充入→-100 扣减验改名,可在 PlayMode execute_code 中用 TryChangeAsync 直接触发。

**Why:** 2026-06 player-attr E2/E3 实测,这几个边界点反复出现在相关任务的验收项,记下来下次不用重新探。

**How to apply:** 涉 TryChangeAsync 验证直接套这几条参考值:①AttrType.All=-1 本地拒(可作负用例)②delta=0 走完整 RPC 但无副作用 ③并发依序处理 ④改名验证用 +500/-100 充扣套路。
