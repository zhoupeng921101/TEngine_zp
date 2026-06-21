---
name: project-handwritten-poco-fixture-drift
description: 手写 POCO 夹具单测自洽闭环测不出与源 xlsx 漂移,Play 调真实 Get(id) 比对源 xlsx 才能逮到
metadata:
  type: project
---

单测用手写 POCO 夹具(非真实 .bytes 行)的逻辑层,夹具值可能与源 xlsx 漂移而单测全绿——夹具是自洽闭环测不出数据偏差。Play 模式调真实 Get(id) 反射读字段比对源 xlsx 能逮到:实例 ItemSystem 中 itemdef.xlsx 30006 automatic=0 但夹具写 1,因无验收项校验 automatic、无用例调用读它的 GrantOnAcquire,夹具值从未被断言。判据:某字段只被夹具设置、无验收项校验、无用例读取 → 既是覆盖缺口也是数据漂移温床,报记录项不判 FAIL。

**Why:** 2026-06 item-system 实测,夹具的输入输出都在测试代码内闭环,xlsx 漂移到夹具时单测看不出任何异常;只有 Play 真实加载链比对外部源(xlsx)才能识别数据漂移。

**How to apply:** Code Review 时识别「只被夹具设置 + 无断言 + 无生产路径读取」三联,判为「夹具数据漂移温床」,降级为记录项(非 FAIL),建议 dev 下轮补真实 .bytes 用例或断言。
