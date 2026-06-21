---
name: project-fantasy-static-gated-method-reflection
description: FantasyClient.FantasyNetwork.Shutdown/Boot 反射可达,是测登出链路最短路径(无需开设置窗)
metadata:
  type: project
---

FANTASY_UNITY-gated 的静态方法(FantasyClient.FantasyNetwork.Shutdown/Boot)在 PlayMode execute_code 反射可达:从 Fantasy.Unity 程序集用稳健收集器(per-assembly try/catch)取 FantasyClient.FantasyNetwork 类型,GetMethod 后 Invoke 即可;这是测试登出链路的最短路径(无需找 GameModule.UI 开设置窗)。

**Why:** 2026-06 account-client E2 实测,虽然方法被 #if FANTASY_UNITY 包裹,只要该宏在 PlayMode 生效就在程序集里有真实符号;反射 Invoke 比走「开设置窗 → 点登出按钮」短数步。

**How to apply:** 登出/重连类 E2 验证:①稳健收集器找 FantasyClient.FantasyNetwork 类型 ② GetMethod("Shutdown"/"Boot", Static|Public) ③ Invoke(null, args);跳过 UI 路径。
