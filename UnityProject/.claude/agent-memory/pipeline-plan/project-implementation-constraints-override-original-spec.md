---
name: project-implementation-constraints-override-original-spec
description: 已落地代码的实现约束是地基,优先于原始稿,常反推翻原稿前提
metadata:
  type: project
---

补全「原始稿=完整愿景 vs 已落地切片」类设计:已落地代码的实现约束(配置注释、已编码不变量)是地基,优先于原始稿——常能反推翻原稿的设计前提,须显式指出并据此收敛。先查 Config/注释再设计经济。

**Why:** 原始稿可能在某些前提下成立,但实现已偏离/约束了那些前提。2026-06 实例:`MergeOrderConfig.OrderPool` 注释「自动配对使非封顶级库存恒≤1」直接判死原稿「Lv1×3」订单,据此把订单形态收敛为「Lv1/Lv2 恒1、仅 Lv3 可堆积」。

**How to apply:** 设计前 grep 涉及 Config 类 + 既有代码注释 + 不变量断言;原始稿与实现约束冲突时设计稿显式标注并按实现收敛。
