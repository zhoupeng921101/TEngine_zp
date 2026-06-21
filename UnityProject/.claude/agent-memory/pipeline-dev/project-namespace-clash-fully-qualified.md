---
name: project-namespace-clash-fully-qualified
description: GameLogic.Mail 与 GameConfig.Mail 同名时桥接全限定消歧;嵌进 JsonUtility 的元素类要标 [Serializable],红点用 getter 属性(JsonUtility 不序列化属性)。
metadata:
  type: project
---

`GameLogic.Mail` 命名空间与 Luban 行类 `GameConfig.Mail` 同名(模块名做表名时通用):桥接处全限定 `GameConfig.Mail` 即消歧,POCO 不必改名(同 item-system 行类命名条);`MailItem` 这类要嵌进 JsonUtility 序列化容器(`List<MailItem>` in `[Serializable]` DTO)的元素类自身也须标 `[System.Serializable]`,且红点/可删用 expression-bodied getter 属性表达——JsonUtility 只序列化字段不序列化属性,派生态天然不进盘。

**Why:** 模块名既是 namespace 又是表名时类型同名是设计上的对称,改 POCO 名会破坏 Luban 与代码的对齐;JsonUtility 不序列化 list 内元素类除非元素自身 `[Serializable]`;属性不被 JsonUtility 处理,刚好用来表达「派生态、不入盘」(2026-06,mail)。

**How to apply:** 模块设计:① 同名冲突优先全限定名解决,不改 POCO;② 嵌套序列化:外层 DTO 标 Serializable,内层元素类也要 Serializable;③ 派生态(红点/总数/可删):用 `=>` 属性(不被 JsonUtility 序列化)。
