---
name: project-mail-list-entry-fields
description: MailListEntry 客户端字段(全 public field 无 property,无 RewardPoolId),EVENT 邮件靠 HasReward+Claimed+UnlockedAvatarIds 增量组合识别
metadata:
  type: project
---

MailListEntry 客户端字段结构:字段为 MailId/SenderTextId/TitleTextId/ContentTextId/SendUnixMs/HasReward/Claimed(全 public field,无 property;无 RewardPoolId);领取后无法从列表项直接判断是否是 EVENT 活动邮件,须以「HasReward=True + Claimed=False + 领取后 UnlockedAvatarIds 增量」组合作为 E1 通路验证证据。

**Why:** 2026-06 event-unlock-client E1 实测,MailListEntry 没有「邮件类型」字段,无法从列表项区分系统/活动/奖励/补偿邮件;只能靠领取前后副作用对比识别。

**How to apply:** EVENT 活动邮件 E1 通路验证三联组合:①邮件本身 HasReward=True+Claimed=False ②点领取 ③领取后 UnlockedAvatarIds 比 before 多了 → 证 EVENT 邮件链路打通。
