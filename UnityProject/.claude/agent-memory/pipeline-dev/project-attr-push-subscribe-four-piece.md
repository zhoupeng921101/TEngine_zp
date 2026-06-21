---
name: project-attr-push-subscribe-four-piece
description: code-built UIWindow 叠订阅服务端属性推送+销毁解绑四件套:Attr getter 双 null-safe/Build 末尾订阅/OnDestroy 解绑/Dispatch switch on type;FormatAttr 共用防漂移。
metadata:
  type: project
---

给 code-built UIWindow(如 `GameWindow`)叠订阅服务端属性推送 + 销毁解绑:沿用 `PlayerInfoWindow` 已建范式四件套:

① `Attr` getter `GameContext.Instance?.PlayerAttr`(双重 null-safe 兜 EditMode 反射 / OnInit 未跑);
② `BuildXxx` 末尾 `if (Attr != null) Attr.OnAttrChanged += OnAttrChangedDispatch`;
③ `protected override void OnDestroy()` 内同 null-safe `-=`(UIBase.OnDestroy 是 virtual,基类已暴露,可放心 override);
④ `OnAttrChangedDispatch(type, _, _)` switch on type(All → 全刷 / Coin/Diamond/Stamina → 刷对应字段)。

设计稿要求「资源条数字初值绑余额 + IsReady=false 显占位」时把「占位文本 / `Attr.<Type>.ToString()`」抽 `FormatAttr` 共用,防 `BuildTopBar` 初值与 `Refresh` 刷新两处文案漂移。

验收侧若有「旧版字符串源核断言」(本任务 R3b 原断言 `string num = i == 0 ? _initialHigh.ToString() : "0";`)已被新设计意图对立化:由 dev 在同任务内同步翻转测试断言到新设计验收锚(订阅/解绑/三属性枚举/占位字面值 grep),不留给 test 报假阳越界——同「两窗合一融合时既有断言与融合意图冲突」条同源。

**Why:** 双 null-safe Attr getter 兼容 EditMode 反射触发 + 生产 OnInit 未跑;OnDestroy 解绑防 GameObject 销毁后事件回调引发 NRE;FormatAttr 共用是单一事实源(2026-06,tarot-hud-attr-bind)。

**How to apply:** UIWindow 接服务端推送:① Attr 用 `?.` 双重 null-safe getter;② Build 末尾订阅,OnDestroy 解绑;③ Dispatch 用 switch on AttrType;④ 文本格式化抽 FormatAttr;⑤ 同任务翻转被对立化的旧断言。
