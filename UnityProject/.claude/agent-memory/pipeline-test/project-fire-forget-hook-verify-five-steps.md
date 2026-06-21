---
name: project-fire-forget-hook-verify-five-steps
description: fire-and-forget hook 五步验证:反射取窗实例→调私有触发→read_console 捞→看后续窗弹出,证调用+日志+不阻塞
metadata:
  type: project
---

fire-and-forget hook(`?.IncrementAndLogAsync(...).Forget()`)的 PlayMode 手验路径:①反射从 _uiStack 取窗口托管实例;②反射调私有 TriggerGameOver/TriggerWin;③read_console filter_text="[Activity]" 捞日志;④检查对应窗口(GameOverWindow/MergeOrderWinWindow)是否弹出;五步合一即证 hook 调用 + 日志 + 不阻塞三要素。

**Why:** 2026-06 activity-client 实测,fire-and-forget hook 的难点是「证它调了 + 没阻塞主流程 + 日志出现」三要素同时满足;五步组合形成完整证据链。

**How to apply:** 任何 .Forget() 的 hook 验证套此五步:取窗 → 调触发 → 捞日志 → 看后续 UI 弹出(证不阻塞);单一证据(如只有日志)不充分。
