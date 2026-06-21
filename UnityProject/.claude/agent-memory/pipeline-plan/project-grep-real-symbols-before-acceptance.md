---
name: project-grep-real-symbols-before-acceptance
description: 复用基线链路时先 grep 实现文件查真实符号,验收表「涉及模块」点到现有方法名供 dev 定位
metadata:
  type: project
---

复用基线已实现链路时,先查找真实符号(grep 实现文件)再写挂接点。验收表「涉及模块」才能点到现有方法名(接缝)供 dev 定位。

**Why:** 设计稿挂接点若用臆造名/猜测名,dev 拿到无法定位,返修代价高。真实符号是 dev 的单一事实源。

**How to apply:** 写验收表前 grep 实现文件,确认 `CollectClearedElements`/`PlaceAndResolve` 这类已存在方法名;「涉及模块」一栏写真实方法名,不写抽象描述。
