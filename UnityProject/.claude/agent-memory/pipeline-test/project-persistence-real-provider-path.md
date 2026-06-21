---
name: project-persistence-real-provider-path
description: 持久化类 Play 手验须走生产真实 Provider(PlayerPrefsProvider)而非单测的 InMemory 替身
metadata:
  type: project
---

持久化/存档类功能:单测锚 InMemory Provider,Play 模式手验须补走**生产真实 Provider 路径**——execute_code 反射读 `Persistence.Provider` 确认类型是 PlayerPrefsProvider(非 InMemory),再 SaveAsync→读 PlayerPrefs raw→Reset→Load→ImportMeta 跑跨会话往返,验真实存储栈而非测试替身。

**Why:** 2026-06 save-system 实测,单测全绿不代表生产路径无误——InMemory Provider 不会触发序列化/反序列化/键名冲突/版本号判断等真实问题;只有走 PlayerPrefsProvider 跑一遍跨会话往返才覆盖真实栈。

**How to apply:** 持久化类任务 Play 手验五步:①反射验 Provider 类型 ② SaveAsync ③读 PlayerPrefs raw 验序列化 ④ Reset 清内存 ⑤ Load + ImportMeta 验反序列化和数据一致。
