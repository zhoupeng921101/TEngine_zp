---
name: project-luban-mgr-real-loading-verify
description: Luban Mgr/POCO 单测走 AssetDatabase 直读 .bytes 绕 YooAsset,Play 手验补真实 ConfigSystem.Tables.TbXxx 加载链
metadata:
  type: project
---

Luban 配置表桥接(Mgr/POCO)的 EditMode 单测走 AssetDatabase 直读 .bytes 绕 YooAsset,刻意不验真实加载链;Play 手验补走生产真实路径(ResetForTest 清缓存 → Get(id) 强制走 ConfigSystem.Instance.Tables.TbXxx 的 YooAsset 加载 → 反射读 POCO 字段比对源 xlsx),才覆盖 EnsureLoaded 真实 LoadAsset。源↔产物交叉核验:xlsx 表头 ##group 行各列分组 + 生成行类型字段读序(只含 client 组列、按 xlsx 列序)要对得上,否则字节布局错位。

**Why:** 2026-06 numeric-system 实测,EditMode 单测用 AssetDatabase 是为不依赖 YooAsset,但这就漏验 EnsureLoaded 的真 LoadAsset 路径;Luban 字节流是顺序写入,client 组列序错位会导致反序列化整体偏移,只有 xlsx ##group 行 + 生成 .cs 字段读序双向核才能逮到。

**How to apply:** Luban 配置类任务 Play 手验:①反射 ResetForTest ②反射 Get(id) ③反射读 POCO 字段比对源 xlsx;Code Review 另查 xlsx ##group 行与生成 .cs 字段读序一致。
