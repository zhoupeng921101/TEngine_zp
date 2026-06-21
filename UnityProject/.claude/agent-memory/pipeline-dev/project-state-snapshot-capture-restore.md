---
name: project-state-snapshot-capture-restore
description: 在 merge-order 状态机叠新系统时,新字段必须同步进 Snapshot.Capture/Restore,否则悔棋只回滚旧字段;值类型浅拷贝,List 用 ToArray()+RestoreXxx。
metadata:
  type: project
---

在 merge-order 状态机上叠新系统(结算/特殊轨/灵力/女神):新字段一律同步进 `MergeOrderState.Snapshot.Capture/Restore`,否则悔棋只回滚旧字段、新状态不回滚。值类型(SpecialOrder/Order)浅拷贝即可,List 用 `ToArray()` + `RestoreXxx`。off/Classic 路径不碰这些字段,回归靠原单测兜。

**Why:** Snapshot 是悔棋单一事实源,新字段不入 Snapshot 即「悔棋后新状态不回滚」,在串联系统下产生隐性错位(2026-06,core-loop)。

**How to apply:** 在状态机加新字段时同步:① Capture 拷该字段;② Restore 恢复该字段;③ List 类型用 ToArray() 防引用共享;④ 加单测「设新字段→快照→改→还原→断言等于初值」。
