---
name: project-clock-dep-pass-today-param
description: 跨天/依赖时钟的逻辑做成纯函数,today 当参数传入;同步 Reset 路径用的是真实 DateTime.Now,date-coupled 断言会随运行日期漂移。
metadata:
  type: project
---

跨天/依赖时钟的逻辑(每日重置)做成纯函数并把 `today` 当参数传入(生产传 `DateTime.Now.ToString("yyyy-MM-dd")`,单测传构造日期),否则依赖真实时钟不可测。

注意:经同步 `ResetForMergeOrder` 加载的路径用的是真实 `DateTime.Now`,date-coupled 断言会随运行日期漂移——单测里要么显式传 today、要么只断言 date-independent 字段。

**Why:** 直接调 `DateTime.Now` 会让单测产生随运行日期变化的失败(2026 跑通、2027 跑挂),且 CI 漂移无法 debug;但有些既有同步入口已硬编时钟,要在测试里规避(2026-06,save-system)。

**How to apply:** 写跨天逻辑:① 抽纯函数 `static bool IsNewDay(string lastDate, string today)`;② 入口生产侧传 `DateTime.Now.ToString("yyyy-MM-dd")`;③ 单测构造 today 参数;④ 测既有调用 Now 的入口时,只断 date-independent 字段(如「count 增 1」不断「日期是 X」)。
