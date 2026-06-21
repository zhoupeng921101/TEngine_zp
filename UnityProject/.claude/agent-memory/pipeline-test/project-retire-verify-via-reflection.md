---
name: project-retire-verify-via-reflection
description: 接口退役核验用 PlayMode 反射 GetMethods/GetFields 确认缺席,比 grep 强(覆盖热更程序集真实类型表面)
metadata:
  type: project
---

接口退役核验可用 PlayMode execute_code 反射:`type.GetMethods(BindingFlags.Public|Instance)` 遍历确认方法名缺席、`type.GetFields/GetProperties` 确认字段名缺席,比 grep 更强(覆盖运行期热更程序集的真实类型表面,而非仅静态源码);配合 grep 零命中双重保险。

**Why:** 2026-06 rank settle retire 实测,grep 看静态源码可能漏 partial class、自动生成器、IL 后处理等;反射看真实加载后的类型表面,是「热更程序集运行期真实状态」的权威。

**How to apply:** 接口退役类任务 Code Review:①grep 零命中初筛 ②反射 GetMethods/GetFields/GetProperties(BindingFlags.Public|NonPublic|Instance|Static)二次确认 ③报告附两路证据。
