---
name: feedback-blocked-vs-fail
description: BLOCKED 与 FAIL 的判定边界:环境阻塞不判 FAIL,代码缺陷不判 BLOCKED
metadata:
  type: feedback
---

总判定三态:PASS / FAIL / BLOCKED。

- FAIL = 代码缺陷(编译失败/CR 发现逻辑错误/协议漏同步/源生成器注册缺口)
- BLOCKED = 环境阻塞(MongoDB 不可达/端口占用/依赖缺失),致运行验证跑不了;**不是代码缺陷**
- BLOCKED 时第 1/2/4 类(编译/源生成器/Code Review)仍照做,能挡编译/逻辑/规范缺陷

**Why:** 用户明确拍板「本机无 MongoDB → 真往返标 BLOCKED-环境,不判 FAIL」;混淆两者会导致要求开发修复不可修复的环境问题,或反之放过真实代码缺陷。

**How to apply:** 运行验证跑不起来时先判断是环境原因还是代码原因:代码问题(比如数据库访问路径写错、异常未捕获)判 FAIL;外部依赖不可达判 BLOCKED,在报告中明确写补跑命令清单。
