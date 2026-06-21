---
name: project-dynamic-list-reskin-three-way-options
description: 换皮屏含动态列表给「Widget+池/代码生成/固定槽」三选一,效果图缺/多 API 走分流
metadata:
  type: project
---

换皮屏含「动态列表」(榜单/邮件/背包等条目不定)且效果图元素 ⊃/⊂ 数据层 API 时:

① 列表行实现给 dev「Widget+对象池 / 代码生成 / 固定 N 槽」三选一 + 标推荐 + art 受限退路(固定槽),验收三方案等价锚在「board→行 VM 列表」映射(纯方法可单测,绕 UGUI 实例化);明令 dev grep 工程真实 UIWidget/池/ScrollRect 签名别臆造。

② 效果图**少**的 API(点赞/页签/奖励预览)按「数据支持则可选接、否则留接线点」分流,全记 decisions 不入 blockers。

③ 数据服务构造若有循环依赖(LocalRankSource selfProvider 引用 RankService.GetMyBest)→ 指到数据层自己的测试用例(SK1/往返)给「先声明变量、闭包捕获、后赋值」写法照搬,别让 dev 重推。

④ 服务构造要别的系统实例(RankService 需 IMailService)而那系统表现层未做 → 列 dev「复用未来持有的 / 现场 new / no-op stub」三选一 + 验收不依赖该系统真跑通(锚返码+调用),入 BLK 标「依赖未建」非 FAIL。

**Why:** 2026-06 rank 窗末屏:GetBoard 列表渲染 + selfProvider 闭包 + IMailService 来源三选一先例。

**How to apply:** 动态列表换皮稿四节:① 列表行三选一 + 推荐;② 效果图差集分流入 decisions;③ 循环依赖指现有测试样板;④ 跨系统依赖三选一 + BLK 兜底。
