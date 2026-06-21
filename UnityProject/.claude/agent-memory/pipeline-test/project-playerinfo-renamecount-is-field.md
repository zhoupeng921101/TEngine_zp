---
name: project-playerinfo-renamecount-is-field
description: PlayerInfo.RenameCount 是 field 不是 property,反射用 GetField,先 GetFields 列全集再定位
metadata:
  type: project
---

PlayerInfo 的 RenameCount 是字段(field)不是属性(property):`GetProperty("RenameCount")` 返 null,须用 `GetField("RenameCount", BindingFlags.Public|NonPublic|Instance)`;在不熟悉时先用 `GetFields(Public|NonPublic|Instance)` 列全字段集再定位。

**Why:** 2026-06 player-attr E2 实测,Fantasy 协议 message 类的成员默认是 public field(非 C# property),GetProperty 必返 null 致 NRE。

**How to apply:** 反射访问 Fantasy message 成员前先确认是 field 还是 property:不熟先 `type.GetFields(Public|NonPublic|Instance)` + `type.GetProperties(...)` 列出来看;message 类几乎都是 field。
