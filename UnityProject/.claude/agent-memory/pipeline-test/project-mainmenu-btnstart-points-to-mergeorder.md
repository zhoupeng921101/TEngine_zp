---
name: project-mainmenu-btnstart-points-to-mergeorder
description: 主菜单 BtnStart 指向 MergeOrderWindow(合成订单)非 GameWindow,HUD 验证直接 ShowUIAsync(Type,Object[]) 开 GameWindow
metadata:
  type: project
---

主菜单 BtnStart 在此项目指向 MergeOrderWindow(合成订单)而非 GameWindow(Classic);test 需用 ShowUIAsync(Type, Object[]) 直接打开 GameWindow 做 HUD 验证,而非走按钮点击路径。

**Why:** 2026-06 tarot-hud-attr-bind V1 实测,本项目主菜单按钮路由到合成玩法,Classic GameWindow 不通过按钮链可达;HUD 验证只关心 GameWindow 本身,直开更直接。

**How to apply:** 验 GameWindow 类 HUD/属性绑定:跳过 BtnStart 点击链,反射 ShowUIAsync(typeof(GameWindow), userData) 直开;别按「点 BtnStart」的常规套路。
