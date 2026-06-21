---
name: project-activity-login-counter-trigger
description: 服务端 OnLogin 活动累计只在实际登录触发(不是 GameContext 加载),Stop→Play 反复走握手才计
metadata:
  type: project
---

E1 活动登录计数触发路径:服务端 OnLogin 的 activity 累计计数只在实际登录时触发(不是 GameContext 加载),触发方式是反复 Stop→Play(每次 Play 完整走登录握手);Play 模式加载完但尚未登录时 UnlockedAvatarIds 可能是持久化历史值,须等 MainMenuWindow 出现后再读;服务端 Develop Info log 有 `ActivityEvalHelper: activity_id=X 本周期发奖完成,mailId=...,reward=...` 一行可确认发奖已触发。

**Why:** 2026-06 event-unlock-client E1 实测,GameContext 加载触发的不是「登录」,只有 Stop→Play 重启完整登录握手才进入 OnLogin 计数链路;过早读 UnlockedAvatarIds 取的是历史值不是本次新增。

**How to apply:** 活动登录计数 E1 验证:①Stop→Play 反复 ②等 MainMenuWindow 出现再读 UnlockedAvatarIds ③服务端 Develop Info log grep `ActivityEvalHelper.*本周期发奖完成` 确认。
