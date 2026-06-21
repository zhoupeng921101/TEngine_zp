---
name: project-new-model-export-on-self
description: 持久化新模型并入既有扁平 DTO 时,Export/Import + 逐字段保底夹值放在新模型自己的纯静态方法,不塞进既有状态机的 ExportMeta。
metadata:
  type: project
---

持久化新模型并入既有扁平 DTO(MergeMetaSave)时,Export/Import + 逐字段保底夹值放在**新模型自己的纯静态方法**(`Model.ExportToMeta(dto)`/`ImportFromMeta(dto,rng,validators?)`),不塞进既有状态机(MergeOrderState)的 ExportMeta/ImportMeta——后者不持有新模型、强挂会耦合无关域并污染悔棋 Snapshot。

DTO 字段仍平铺在同一份存档(做法 a 口径不变),旧档缺字段 JsonUtility 给缺省(int[]→空数组非 null,保底要同时判 null 与 Length==0)。

**Why:** 把新模型 Export 塞进状态机会让状态机被强行知道新模型存在、悔棋 Snapshot 被无关字段污染、模块耦合上升;放新模型自己的静态方法既符合「自治」原则又便于单测(2026-06,player-info)。

**How to apply:** 新增持久化模型时:① Export/Import 是新模型的 public static 方法,签名带 DTO 参数;② 中枢节点(LoadPlayer/SavePlayer)按顺序调各模型的 Export/Import;③ 旧档兼容:JsonUtility 反序列化后立即夹值(null→空数组,Length==0 视作缺省);④ 既有状态机的 ExportMeta 保持不动。
