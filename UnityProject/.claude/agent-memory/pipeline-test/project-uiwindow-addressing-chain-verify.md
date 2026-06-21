---
name: project-uiwindow-addressing-chain-verify
description: UIWindow 寻址链验证用 ShowUIAsync 反射 + GameObject.Find 三处一致即可,无需拖拽
metadata:
  type: project
---

UIWindow 寻址链验证(尤其 prefab/location 改名)绕开拖拽通关:Play 中用 execute_code 反射调 `GameModule.UI.ShowUIAsync<T>(new object[]{userData})`(T 从已加载 GameLogic 程序集按全名取),再 `GameObject.Find("<WinName>")` 确认实例化 + 子节点齐全,即证 location↔prefab 文件名↔m_Name 三处一致、LoadGameObjectAsync 未断;CloseUI<T> 泛型解析则触发对应按钮 onClick.Invoke 验。

**Why:** 2026-06 collect-rename 实测,改名场景下要核验「YooAsset location ↔ prefab 文件名 ↔ 根节点 m_Name」三处一致,反射 ShowUIAsync 后 Find 命中即一次性证明全链路;CloseUI<T> 调用须验泛型解析正确,直接点关闭按钮触发更接近真实路径。

**How to apply:** UIWindow 改名/换位/换 prefab 类任务,优先此路径而非走真实 UI 按钮点击链(更短更稳);若 Find 失败逐层定位是 location 错还是 prefab 错还是 m_Name 错。
