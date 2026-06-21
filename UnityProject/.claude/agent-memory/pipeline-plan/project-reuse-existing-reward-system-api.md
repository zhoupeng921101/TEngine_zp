---
name: project-reuse-existing-reward-system-api
description: 新系统发奖经既有奖励下发系统对外 API,本系统只「算谁收哪封带什么奖的邮件」
metadata:
  type: project
---

新系统要「发奖励」且工程已有「奖励下发系统」(如邮件 21 的 IMailService.Send)时:新系统经该系统对外 API 借力发奖,本系统不直接接 ItemGrant/落 MergeOrderState——奖励发放是下发系统的职责,新系统只「算出谁该收哪封带什么奖的邮件」。

验收锚在「注入下发系统的 IMailService,断言收到挂对应奖励库 id 的 Send 调用」,不碰真实发奖落点。批次内系统逐层叠时(num→item→reward→mail→rank)后者复用前者对外接口,链条越往后越只是编排,几乎不新造底层。

**Why:** 2026-06 rank 结算/每日/点赞奖全经 21 IMailService.Send,排名层零 MergeOrderState 引用先例。

**How to apply:** 设计稿明确「本系统职责=计算邮件清单,下发由 IMailService 负责」;验收锚 Send 调用 mock 断言,不依赖真实库落地。
