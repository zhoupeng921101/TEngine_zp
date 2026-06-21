---
name: project-persistence-split-serialization-and-io
description: 持久化按介质拆两层,序列化层纯同步可单测,IO 层异步外壳;PlayerPrefs 非阻塞不触红线
metadata:
  type: project
---

设计「异步 IO 红线 vs 工程现有同步 Persistence 接缝」类持久化时,按介质拆两层:序列化层(对象↔字符串,纯逻辑同步、单测往返到 string/InMemory Provider,不碰真实磁盘)+ 磁盘 IO 层(异步 UniTask 外壳)。

纯逻辑状态类不 using UniTask、不含 IO 调用,保持可在纯 C# 单测直接 new 出来跑;验收点全锚在同步纯方法,异步外壳只要求编译通过+人工冒烟,不强求 EditMode 覆盖。

**Why:** CLAUDE.md「禁同步 IO」红线针对阻塞磁盘/资源 IO,PlayerPrefs 非阻塞 KV 不触线。两层拆分让纯逻辑可单测、异步外壳不污染状态类(2026-06 save-system 设计 14 复用 Persistence.Provider 接缝先例)。

**How to apply:** 设计持久化系统时显式画两层结构图,纯逻辑层签名同步、验收锚同步方法 + InMemory Provider 往返测试;异步外壳只跑冒烟。
