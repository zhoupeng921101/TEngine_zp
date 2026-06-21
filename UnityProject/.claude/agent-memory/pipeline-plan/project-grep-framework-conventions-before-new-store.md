---
name: project-grep-framework-conventions-before-new-store
description: 设计持久化/设置前先 grep TEngine 框架既有约定,引擎级关注点复用框架,玩法元层走项目存档
metadata:
  type: project
---

设计持久化/设置类系统前,先 grep 框架(TEngine)本身是否已拥有该关注点的约定,而非只看项目 GameLogic 侧:框架常把「设置」「本地配置」这类引擎级关注点连键名+读写工具+启动加载一并备好。命中即复用其既有键 + 工具(本层只补缺的那一环,如「运行期可切换并落盘」),启动加载零改动即生效、与框架口径一致;别在项目侧另造平行存储。

判据「该关注点归谁」:玩法元层进度归项目存档(如 MergeMetaSave),引擎级设置归框架约定——两者不强行合并。

**Why:** 项目侧另造平行存储 = 与框架启动加载割裂 + 键名口径不一致(2026-06 settings 复用 TEngine.Constant.Setting.MusicMuted/SoundMuted + Utility.PlayerPrefs + ProcedureLaunch.InitSoundSettings,对比 18 player-info 并入 MergeMetaSave 先例)。

注意框架键可能是反向语义(muted vs on),映射时取反并在键映射表逐档代入验证,与启动读取侧(!GetBool)对齐。

**How to apply:** 设计持久化前 grep TEngine.Constant/Utility 看框架约定;命中复用、未命中再造;玩法元层归项目 Save,引擎级归框架,不混合。反向语义键做映射表逐档验证。
