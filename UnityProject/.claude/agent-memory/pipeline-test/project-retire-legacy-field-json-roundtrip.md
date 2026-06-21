---
name: project-retire-legacy-field-json-roundtrip
description: 退役类越界试探须专项覆盖「老存档含遗留字段」:真实 JSON 喂 JsonUtility.FromJson,断言不抛+其他字段保真
metadata:
  type: project
---

退役类改动的越界试探:除正向用例外须专项覆盖「老存档含遗留字段」场景——用真实 JSON 字符串含退役字段喂 JsonUtility.FromJson,断言不抛+其他字段保真;PlayMode execute_code 直接调 JsonUtility 即可,无须单独起测试。

**Why:** 2026-06 rank settle retire 实测,玩家本地老存档可能仍含已退役字段,反序列化若严格匹配字段集会炸——必须验「未知字段被静默忽略」+「已知字段值不丢」。

**How to apply:** 退役字段任务必加越界用例:构造含退役字段的 JSON 字符串 → execute_code 调 `JsonUtility.FromJson<T>(json)` → 断言不抛 + 关键字段值正确;写入报告做凭证。
