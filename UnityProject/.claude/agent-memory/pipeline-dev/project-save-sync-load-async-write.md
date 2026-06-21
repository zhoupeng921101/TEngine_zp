---
name: project-save-sync-load-async-write
description: 同步生命周期钩子里加载存档:同步读 PlayerPrefs Provider(非阻塞)+ 写盘走 SaveAsync().Forget() 外壳,纯逻辑序列化层同步可单测。
metadata:
  type: project
---

同步生命周期钩子(`ResetForMergeOrder`、`OnCreate` 返回 void)里要加载存档又不想把钩子改成 async:加载走同步读 `Persistence.Provider`(PlayerPrefs 为非阻塞内存级读、不触「禁阻塞 IO」红线,与既有 `BlockGameState.Load` 同口径),写盘走 UniTask 外壳 `SaveAsync().Forget()` 满足异步红线。纯逻辑序列化层(对象↔string)同步可单测、与磁盘外壳分离,EditMode 注入 InMemory Provider 往返不碰真实文件。

**Why:** 同步钩子改 async 会传染整条调用链 + UI 卡顿;PlayerPrefs 是内存级 KV 不算阻塞 IO,可同步读;写盘是异步红线,但写比读频率低,容易包外壳(2026-06,save-system)。

**How to apply:** 设计存档时:① 序列化层做纯函数(string ↔ DTO);② Provider 抽接口,生产 PlayerPrefs/磁盘,测试 InMemory;③ 同步钩子里 Provider.Read+反序列化;④ 写盘统一 `SaveAsync().Forget()`;⑤ EditMode 注入 InMemory Provider 测往返。
