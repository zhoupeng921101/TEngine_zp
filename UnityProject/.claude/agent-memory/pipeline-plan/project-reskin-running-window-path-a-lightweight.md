---
name: project-reskin-running-window-path-a-lightweight
description: 换皮已在运行的窗口取路 A 轻量换皮,零回归抬为硬验收,贴子图后底色改 white
metadata:
  type: project
---

换皮对象是**已在运行的窗口**(非从零造 UI)时,取舍与前几屏不同:

① 取**路 A 轻量换皮**(保留 code-built 结构 + 全部逻辑,只对 `UGuiFactory.CreateImage` 返回的 `Image`、`CreateButton` 的 `out Image bgImage` 链 `.SetSubSprite` 换贴图),**不取 prefab 重构**(重写在跑窗口=回归面大、与「纯 UI 补完」相悖);

② **零回归抬为硬验收**:窗在跑则 UserData 解析/结算计算/调用点传参/按钮回调目标一律保留不动,R 组验收以「编译 0 error + 静态核对逻辑行未改」为主(运行期单例 + UIWindow 生命周期反射成本高,不强求 EditMode 反射驱动 OnCreate);

③ 贴子图后底色须改 `Color.white`(既有纯色 Image.color 会把木纹染暗)。

先 grep 调用点(本例 GameOverWindow 被 Classic+订单结束两路、传参语义不同)钉清「不改」清单。

**Why:** 2026-06 settlement 窗:GameOverWindow/MergeOrderWinWindow 路 A 换皮先例。

**How to apply:** 换皮在跑窗:① 路 A 链 SetSubSprite;② 设计稿列「绝不改」清单(逻辑行/传参/回调);③ 提醒 dev 贴图后底色 white;④ R 组验收以编译 + 静态 diff 为主。
