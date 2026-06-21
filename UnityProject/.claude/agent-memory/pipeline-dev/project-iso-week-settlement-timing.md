---
name: project-iso-week-settlement-timing
description: 周循环结算用 ISO 周(周一为周起点):todayIso=((int)now.DayOfWeek+6)%7+1;周日属上一 ISO 周;未到点判据=lastSettle 落在本周内。
metadata:
  type: project
---

周循环结算时机(spec valid_type=3 星期 X)用 ISO 周(周一为周起点)算「本自然周星期 X 的结算时刻」做判据:

```
todayIso = ((int)now.DayOfWeek + 6) % 7 + 1   // DayOfWeek.Sunday==0 → 周日=7
monday   = now.Date.AddDays(-(todayIso-1))
settleAt = monday.AddDays(targetWeekday - 1)
```

周日属上一 ISO 周(其周一在 6 天前),故「本周内任意一天都 ≥ 本 ISO 周周一」——别写「周日 < 本周周一结算时刻 → 不到点」类断言(在 ISO 周模型里不成立,本周内必到点);未到点的正确判据是 lastSettle 落在本周内。

**Why:** .NET DayOfWeek 以周日=0,直观容易写错;ISO 周以周一起算与策划「周 X 结算」语义匹配;周日属哪个 ISO 周是常见混淆点,写错会出现「周日反复结算/不结算」(2026-06,rank)。

**How to apply:** 周循环结算:① todayIso 公式照抄;② 算 monday、settleAt;③ 判到点:`now >= settleAt && lastSettle < settleAt`;④ 单测覆盖周一/周四/周日跨周边界。
