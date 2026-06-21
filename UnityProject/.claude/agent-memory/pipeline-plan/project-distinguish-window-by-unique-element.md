---
name: project-distinguish-window-by-unique-element
description: 判定效果图对应哪个在跑窗抓「独有元素」而非共有元素,判据写进设计稿
metadata:
  type: project
---

判定效果图对应哪个在跑窗(同构窗易混)时:抓「独有元素」而非「共有元素」——棋盘/候选块两窗都有(无区分力),大居中分数(GameWindow 独有)/ 订单卡·合成区·体力条(MergeOrderWindow 独有)才是判据。

读图列「效果图有什么 vs 没什么」逐项对两窗代码核,有独有 A 元素 + 无独有 B 元素 → 是 A 窗。判定写进设计稿「§哪个窗·给证据」节。

**Why:** 2026-06 tarot_mode 有大分数·无订单区→GameWindow 先例。共有元素无区分力,只用独有元素能消除误判。

**How to apply:** 同构窗换皮稿专设「§哪个窗·给证据」节,列独有元素正反清单(有 A 独有 + 无 B 独有)+ 对应代码符号。
