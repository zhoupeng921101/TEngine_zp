---
name: project-static-unit-test-limits
description: 静态核验单测可预判 90% 编译/逻辑错,但 HybridCLR 热更编译 + YooAsset 真导入仍须 Editor,不可代签 PASS
metadata:
  type: project
---

静态核验单测:逐例手工走断言路径(尤其级联/快照回滚类),配合 grep 实际签名,可在无 Editor 时预判 90% 编译与逻辑错;但 HybridCLR 热更编译 + YooAsset 模拟寻址的真导入仍须 Editor 确认,不可代签 PASS。

**Why:** 2026-06 实测,静态分析能逮 90% 的接口签名错配、断言路径走错、级联状态漏更新,但 HybridCLR AOT 元数据补充失败、YooAsset Provider 寻址失误这两类只在 Editor 真加载链触发。

**How to apply:** Editor 不可用时跑静态核验仍有价值,但报告状态最高只能到「静态 PASS,Editor 阻塞」(对应 BLOCKED 三态);Editor 恢复后必须补跑真导入,不能据静态核验直接签 PASS。
