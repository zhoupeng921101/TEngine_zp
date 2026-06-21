---
name: project-meta-layer-authoritative-source
description: 把既有状态机字段并入元层存档:平铺字段+Export/Import 挂宿主类+中枢节点串联;两键同名时元层是权威,UI Load 旧键须从元层补回,迁移期取两者较大值。
metadata:
  type: project
---

把既有状态机字段(非独立新模型,如 `BlockGameState.HighScore`)并入元层存档 `MergeMetaSave`:加平铺字段 + Export/Import 纯方法挂在该字段的宿主类(BlockGameState),在元层中枢节点(`GameContext.LoadPlayer/SavePlayer`,与 PlayerInfo 同一份 DTO)串联加载/落盘。与「新模型并入放独立静态方法」(player-info 那条)同口径,区别只是宿主是既有状态机而非新建模型。

关键陷阱:若该字段在两个存档键里同名共存(`HighScore` 既在元层新字段、又在旧局内瞬态键 `block_blast_save_v1.highScore`),进窗口/主菜单时两键加载有先后,后加载者覆盖——确立元层为权威源后,UI 层 Load 旧键后须显式从元层补回,且迁移期取两者较大值(元层缺省 0 时不抹掉旧键历史值),否则老玩家最高分被新空元层清零。

**Why:** 元层为权威是为了跨局/跨设备的最终真理源;但旧版本数据迁移期间双键并存,加载先后顺序不稳定,直接信元层会清掉历史值;取较大值是只在迁移期使用的过渡逻辑(2026-06,gameplay-fusion)。

**How to apply:** 状态机字段并入元层:① Export/Import 挂宿主类(不另建 model);② 元层中枢节点统一调;③ 旧键存在时:Load 旧键后再 Load 元层并取 max;④ 迁移完成后(若有标记)再砍掉取 max 逻辑;⑤ 加单测覆盖「老玩家旧档迁移高分不被抹」。
