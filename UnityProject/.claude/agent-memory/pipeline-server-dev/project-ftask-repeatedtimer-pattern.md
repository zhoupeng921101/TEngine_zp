---
name: project-ftask-repeatedtimer-pattern
description: FTask.RepeatedTimer 触发节律 — 放 Awake 起、Destroy 时 RemoveTimer 取消;节律快慢不影响正确性(幂等兜底)
metadata:
  type: project
---

触发节律(`FTask.RepeatedTimer(scene, 间隔, Action)` 回调内 `helper.XxxAsync().Coroutine()`)放进 Awake 组件、Destroy 时 `FTask.RemoveTimer(scene, ref id)` 取消;节律快慢不影响正确性(幂等兜底),取最省间隔即可。

**Why:** 服务端定时类特性(rank 结算 / mail 清理 / 活动每日重置)需要进程内定时器节律。FTask.RepeatedTimer 是 Fantasy 标准 API,生命周期跟着 scene 组件走,clean shutdown 不漏。

**How to apply:** 新增定时类特性时:
1. 给关联的 Service 组件加 `_timerId` 字段
2. `Awake` 内 `_timerId = FTask.RepeatedTimer(scene, intervalMs, OnTick)`
3. `Destroy` 内 `FTask.RemoveTimer(scene, ref _timerId)`
4. `OnTick` 调 `helper.XxxAsync().Coroutine()`(fire-and-forget,helper 内做实际工作)
5. 节律间隔取「最省 + 业务可接受」(典型:rank 结算 60s 一次;mail 清理 1h 一次);设计稿写明节律选取依据
6. helper 内逻辑必须幂等(同一周期键重复调不重复发奖),保「节律错乱时不放大错」
