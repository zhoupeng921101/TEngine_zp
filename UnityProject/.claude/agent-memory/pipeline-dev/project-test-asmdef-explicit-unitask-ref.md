---
name: project-test-asmdef-explicit-unitask-ref
description: 测试 asmdef(overrideReferences:true)要实现返 UniTask 的接口须显式加 UniTask 引用——asmdef 引用不传递,只引被测程序集不够。
metadata:
  type: project
---

测试 asmdef(`overrideReferences:true`/`autoReferenced:false`)要实现一个返 `UniTask` 的接口或命名 UniTask 类型:须显式把 `UniTask`(`Packages/UniTask/Runtime/UniTask.asmdef`,name=`UniTask`,无 define 约束)加进 test asmdef references——只引被测程序集(`GameLogic`)不够,asmdef 引用不传递。

**Why:** Unity asmdef 引用是显式的、不传递的;被测程序集引用了 UniTask 不等于测试程序集自动可见 UniTask;不加引用时 `typeof(UniTask)` 报 CS0246,但报错指向类型缺失易被误判为「漏 using」(2026-06,redeem-client)。

**How to apply:** 测试要构造/接收 UniTask 类型时:① 在 test asmdef references 加 `UniTask`;② 编译失败找不到 UniTask 类型 → 先查 asmdef references 再查 using;③ 任何被测程序集传递依赖,测试 asmdef 都要显式加。
