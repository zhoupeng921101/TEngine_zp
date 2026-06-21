---
name: project-unitask-await-via-getawaiter
description: execute_code 中 UniTask<T> 用 GetAwaiter + IsCompleted 自旋等待,不能用 AsTask/dynamic
metadata:
  type: project
---

UniTask<T> 在 execute_code 中须用 GetAwaiter + IsCompleted 自旋等待(不能用 AsTask/dynamic):`UniTask<ChangeResult>` 无 AsTask() 方法,dynamic 绑定在此 Unity 环境无 Microsoft.CSharp.RuntimeBinder.Binder;正确路径:`var awaiter = task.GetAwaiter(); while (!awaiter.IsCompleted) { await UniTask.Yield(); } var result = awaiter.GetResult();` 放在 `UniTask.Void(async () => {...})` 内部。

**Why:** 2026-06 player-attr E2 实测,UniTask 不实现 AsTask;Unity 项目默认不含 Microsoft.CSharp 程序集,dynamic 关键字编译过但运行时抛 BinderException。

**How to apply:** 反射调用返回 UniTask<T> 的异步方法:在 `UniTask.Void(async () => { ... })` 内写自旋 `GetAwaiter → while !IsCompleted yield → GetResult` 三步;别尝试 AsTask/dynamic。
