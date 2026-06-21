---
name: project-new-mechanic-ui-stub-hook-not-implementation
description: 换皮轮遇效果图新机制 UI 留 stub 钩子+TODO,绝不顺手实现机制
metadata:
  type: project
---

效果图含「新机制 UI」(本例资源条加号 / 更换·删除·提示按钮)而玩法+数据层都无该机制时:**视觉占位 + stub 钩子**(摆节点 + 点击 Log 待建 + TODO),绝不在换皮轮顺手实现机制(实现要动 OperaArr/SaveArr/资源扣费/落子合法性=动玩法逻辑、违纯 UI 补完 + 零回归)。

真机制是独立新玩法功能,记 decisions 交产品定义(花什么资源/几次/规则)后另开轮。与「装饰占位」区别:装饰是无语义图标(占位即终态),新机制 stub 是有语义但待建(留接线点)。

**Why:** 2026-06 tarot_mode 更换/删除/提示 D5 先例。

**How to apply:** 换皮轮遇新机制按钮:① 摆节点 + onClick 接 Debug.Log/TODO;② decisions 记「机制待产品定义」;③ 不动玩法层任何 Save/Opera/合法性逻辑。
