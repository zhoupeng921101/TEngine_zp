---
name: project-code-gen-window-status-verify
description: 全代码生成窗 Play 手验:ShowUIAsync → 等 1 帧 → GetComponentsInChildren<Text> 读 m_text_Status 内容
metadata:
  type: project
---

全代码生成窗口(prefab 仅最小壳,全部节点运行时 BuildLayout 生成)的 Play 手验路径:ShowUIAsync → 等 1 帧 → `GetComponentsInChildren<Text>(true)` 读 m_text_Status 内容即可断言状态栏文案(正在加载.../暂无流水/网络异常 等),不需找 prefab 节点。

**Why:** 2026-06 ledger-client V1 实测,prefab 里没有完整节点树(全运行时建),传统 transform.Find("XXX") 找不到;但运行时建好后 GetComponentsInChildren 能扫到。

**How to apply:** code-built 窗状态验证:①ShowUIAsync ②`yield WaitForEndOfFrame` 或 `UniTask.Yield()` 等一帧 ③`win.GetComponentsInChildren<Text>(true)` LINQ 按 name 筛 ④断言 .text 内容。
