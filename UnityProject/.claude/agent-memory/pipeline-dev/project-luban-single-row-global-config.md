---
name: project-luban-single-row-global-config
description: 单行全局配置表(maxCount/retainDays 整张表就一行)用 Luban 普通 map 表:加 id 列固定填 1,运行期取 DataList[0],表空 getter 用 ??= new 兜默认值。
metadata:
  type: project
---

单行全局配置表(maxCount/retainDays 一类「整张表就一行」的旋钮)用 Luban 普通 map 表实现即可:加一列 `id`(`##type int`,固定填 1)做主键、`index=id`/`mode=map` 注册,运行期桥接取 `Tables.TbXxx.DataList[0]`,表空时桥接里 `_global` 留 null、由 getter `??= new XxxConfig()` 兜底返代码默认值——使「表缺省返默认」可单测且无需 KV 模式。不必为单行表另找特殊表类型。

**Why:** 全局参数(系统级旋钮)用「id=1 的单行表」可复用既有 Luban map 流程,无需引入 KV 表类型;表空 fallback 是 EditMode 跑测必经路径(测试不喂表)(2026-06,mail)。

**How to apply:** 新增全局配置:① 设计列 id(int)+ 实际字段;② Excel 填 id=1 一行;③ 桥接 `_global` 字段 + getter `_global ??= new XxxConfig()`;④ 单测验「表空时取 getter 返默认值」+「填表后取真实值」。
