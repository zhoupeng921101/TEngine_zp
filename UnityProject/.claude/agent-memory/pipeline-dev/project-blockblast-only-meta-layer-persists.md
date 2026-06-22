---
name: project-blockblast-only-meta-layer-persists
description: BlockBlast 现行只有元层 DTO 一条跨会话续存通道;局内态(棋盘/手牌/全清武装位/连消链)每局 Reset 重建不落盘;设计 49 的局内态整盘续存通道未落地。
metadata:
  type: project
---

BlockBlast(merge-order)当前**只有元层这一条跨会话续存通道**:`MergeMetaSave` DTO 经 `MergeMetaPersistence`(PlayerPrefs `block_blast_merge_meta_v1`)落盘/加载。局内态(棋盘 SaveArr/手牌 OperaArr/全清武装位 AllClearArmed/连消链 ComboChain)**每次进窗由 `BlockGameState.ResetForMergeOrder`→`MergeOrderState.Reset()` 重建,不落盘**。`ImportMeta` 只覆盖元层字段、不触局内瞬态(两层字段不重叠)。

**Why:** 设计 49(无局·无尽,整盘续存)的「局内态跨会话续存」通道是**已暂停未进 dev 的任务**,现行代码里不存在。design-docs 里凡说「归局内态、同全清武装位通道续存」的,在现行实现中没有可落盘的局内通道——全清武装位/连消链本身就不跨会话存。

**How to apply:** 任何「某状态要跨会话续存」的新需求,落盘只能并入元层 DTO(`MergeMetaSave` 平铺字段,做法同 highScore/玩家字段;`CurrentVersion` 不升,JsonUtility 缺字段给缺省 + Import 逐字段保底)。串联点:`MergeOrderState.ExportMeta`/`ImportMeta`(中枢)。若设计稿把它标成「局内态续存」而行为只要求「退出重进保持」,按元层实现即满足行为验收,在交接区标明归属层分歧给 boss 复核,**不报 designFlaw**(行为验收无误)。需同步进悔棋快照 `Snapshot.Capture/Restore`(见 [[project-state-snapshot-capture-restore]])保一致性。
