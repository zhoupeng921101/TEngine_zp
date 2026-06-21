---
name: project-luban-multi-row-aggregate-pattern
description: spec 「一张表控所有 X」同主键多行,Luban 设 row_id 唯一,桥接按语义 id 聚合
metadata:
  type: project
---

spec 是「一张表控所有 X」(同主键多行 = 一个 X 的多个子档,如排行榜同 id 多行=多名次奖励档)时:Luban 须另设唯一行主键(row_id),spec 的语义 id 当普通字段;桥接按语义 id 聚合多行成「主对象(榜级字段取首行)+ List<子档>(按区间字段升序)」,同 16 礼包子项/20 兑换码奖励子表做法。

设计稿显式写明「榜级字段各行须一致、桥接取首行」的配置规范。

**Why:** Luban 主键必须唯一,spec 语义 id 重复就不能直接当主键。2026-06 rank id 多行聚合 RankDef + List<RankRewardTier> 先例。

**How to apply:** 多行同 id 表:① Luban schema 加 row_id 唯一主键;② 语义 id 普通字段;③ 桥接逻辑按语义 id GroupBy + 子档按区间升序;④ 设计稿写「榜级字段各行须一致、桥接取首行」配置规范。
