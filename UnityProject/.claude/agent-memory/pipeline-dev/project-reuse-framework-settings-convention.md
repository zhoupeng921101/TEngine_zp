---
name: project-reuse-framework-settings-convention
description: 音频开关复用 TEngine.Constant.Setting.MusicMuted/SoundMuted 键 + Utility.PlayerPrefs,注意 on/muted 语义取反,GameModule.Audio 在 HotFix 可达。
metadata:
  type: project
---

复用框架既有设置约定(音频开关)而非另造存储栈:服务落盘直引 `TEngine.Constant.Setting.MusicMuted/SoundMuted` 键 + 经接缝包 `Utility.PlayerPrefs`(非阻塞 KV,不触同步 IO 红线),启动 `ProcedureLaunch.InitSoundSettings` 零改动即生效。

关键是「开(on)/静音(muted)」语义取反:`SetBool(MusicMuted, !MusicOn)`、`MusicOn=!GetBool(MusicMuted,false)`,取反写反则启动加载语义颠倒(关音乐反而下次开)。`GameModule.Audio`(`GameModule.cs:56` `=> Get<IAudioModule>()`)在 HotFix 可达,生产 sink 直用,无需退回 `ModuleSystem.GetModule`。

**Why:** 复用框架约定避免「设置无法启动加载」「与框架其他模块冲突」类隐性 bug;on/muted 取反易写反、且写反不会立即报错(本次启动正常、下次启动颠倒),特别难发现(2026-06,settings)。

**How to apply:** 接入既有框架设置项:① 先 grep `TEngine.Constant.Setting.*` 找现有键;② 复用 `Utility.PlayerPrefs` 不另造 wrapper;③ 写 toggle 时画 truth table 确认 on/muted 方向;④ 写一条「toggle on→落盘→重载→断言 on」往返测,防取反写反。
