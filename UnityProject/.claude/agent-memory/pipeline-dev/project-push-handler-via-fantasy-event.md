---
name: project-push-handler-via-fantasy-event
description: 服务端推送 G2C_*Push 接客户端业务:Handler 落 FantasyClient,不能直调 HotFix(循环依赖);FantasyNetwork 加静态事件,HotFix 在 GameApp.StartGameLogic 订阅。
metadata:
  type: project
---

服务端推送类协议(`G2C_*Push` / `G2C_*Snapshot`)接客户端业务层:Handler 落在 `Assets/Fantasy/Scripts/Handlers/`(FantasyClient asmdef,源生成器自动注册 Message<T>),**Handler 内不能直调 HotFix 业务类**(GameLogic.asmdef 已引 FantasyClient,反向引会循环依赖)。

最简方案 = 在 `FantasyNetwork` 类加静态事件 `OnXxx`(沿 `OnLoggedIn` 范式)+ internal Raise 方法,Handler 内 `FantasyNetwork.RaiseXxx(...)`,HotFix 侧在 `GameApp.StartGameLogic` 的 `#if FANTASY_UNITY` 块内订阅事件,把数据转给 HotFix 服务。比把 Handler 落 HotFix 让源生成器扫的方案改动面小、一致性高(现存所有 Handler 都在 FantasyClient)。

**Why:** Fantasy 源生成器只扫 FantasyClient asmdef 内的 Handler 注册,把 Handler 搬到 HotFix 要改源生成器扫描范围;事件模式让 Handler 不感知 HotFix 类型,反向耦合消失(2026-06,player-attr-client)。

**How to apply:** 加推送类协议接业务:① Handler 写在 `Assets/Fantasy/Scripts/Handlers/`;② FantasyNetwork 加 `public static event Action<...> OnXxx` + `internal static void RaiseXxx(...)`;③ Handler 调 `FantasyNetwork.RaiseXxx(...)`;④ HotFix 在 GameApp.StartGameLogic 的 #if FANTASY_UNITY 块订阅 + 转给业务服务。
