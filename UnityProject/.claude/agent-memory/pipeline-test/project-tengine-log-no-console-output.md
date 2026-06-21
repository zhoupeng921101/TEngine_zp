---
name: project-tengine-log-no-console-output
description: TEngine Log.Info/Warning/Error 不进 Unity console,验日志调用须 Debug.Log 包一层作执行凭证
metadata:
  type: project
---

TEngine Log.Info/Warning/Error 在 PlayMode execute_code 中调用后不产生 Unity console 对应级别条目(路由到 TEngine 内部日志门面);验证日志调用本身成功须用 `Debug.Log("[TAG] ... called successfully")` 包一层作为执行凭证,而非依赖 read_console filter 捞 Log.Warning 输出。

**Why:** 2026-06 activity-client V7 实测,TEngine 把 Log 路由到自己的 LogHelper(可配自定义 sink),不一定回流到 Unity Debug;read_console 捞不到 Log.Warning 内容会误判「没调用到」。

**How to apply:** 验某代码路径是否执行到 Log.* 调用:在 Log.* 前后加 `UnityEngine.Debug.Log("[TAG] before/after")` 作为执行凭证;read_console filter [TAG] 取这两条,Log.* 本身不依赖 console 取证。
