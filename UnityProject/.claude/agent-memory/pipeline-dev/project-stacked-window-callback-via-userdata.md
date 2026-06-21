---
name: project-stacked-window-callback-via-userdata
description: 叠层 UIWindow 关闭后通知底层窗口刷新:ShowUIAsync 把回调经 UserData 传入,被叠窗 OnCreate 取出,OnDestroy 里 Invoke。
metadata:
  type: project
---

叠层 UIWindow 关闭后通知底层窗口刷新:`ShowUIAsync<T>((System.Action)RefreshXxx)` 把回调经 UserData 传入,被叠窗口 `OnCreate` 里 `_cb = UserData as System.Action`、`OnDestroy` 里 `_cb?.Invoke()`。

**Why:** TEngine 无 OnResume/重获焦点钩子,无法在叠层窗口关闭时自动通知底层刷新;UserData 回调是叠层窗口回写底层状态的可行做法(2026-06,piety-temple)。

**How to apply:** 叠层窗口需在关闭时刷底层时:① 底层 Show 子窗时 `ShowUIAsync<Sub>((Action)RefreshSelf)`;② 子窗 OnCreate `_cb = UserData as Action`;③ 子窗 OnDestroy `_cb?.Invoke()`;④ Refresh 函数自包含(不依赖临时变量)。
