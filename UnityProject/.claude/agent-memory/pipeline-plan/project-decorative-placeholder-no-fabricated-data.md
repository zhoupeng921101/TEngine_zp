---
name: project-decorative-placeholder-no-fabricated-data
description: 效果图含「无切图+无数据源」装饰元素时占位摆位,不为对位捏造数据层统计字段
metadata:
  type: project
---

效果图含「无切图 + 无数据源」的装饰元素(本例结算窗星/心/草各60,太阳奖励格)时:不为对位而捏造数据层统计字段(grep 数据层确认无对应字段)→ **装饰占位**(摆位对位骨架、数字接已得值或省略、不绑统计、不动逻辑),占位图勉强则退「整组省略只保核心信息+按钮」。

这些图标语义无 spec 定义=不替产品赋义,真做与否记 decisions 交 boss/产品,本轮不停机。

**Why:** 2026-06 settlement D1 先例。

**How to apply:** 效果图含语义不明装饰元素时:grep 数据层确认无对应字段 → 占位摆位 + decisions 记录;占位图缺则整组省略只保核心信息+按钮,不替产品赋义。
