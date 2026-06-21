---
name: project-sg-empty-zero-handler-proof
description: SG 产物目录为空 = 零新 Handler/System 的充分证明,编译 0 error 即足
metadata:
  type: project
---

带 `EmitCompilerGeneratedFiles=true` 编译后,源生成器产物目录为空,说明本子单新建的文档模型类/枚举/静态 Helper 均不需要 SG 注册。编译 0 error 是 SG 产物正确的充分证明。

**Why:** Fantasy.Net 用源生成器注册 Handler / System 类;若新建了 Handler 类但未被 SG 扫到,会运行时无效。SG 产物目录是 SG 是否正确注册的权威输出。

**How to apply:** 服务端段四类验证之「源生成器产物」类——若本子单未新建 Handler/System(只新建 POCO/Helper/枚举),SG 产物目录为空属正常,不当 FAIL;编译 0 error 即覆盖该类验证。若新建了 Handler/System,SG 产物须有对应注册条目,否则 FAIL。
