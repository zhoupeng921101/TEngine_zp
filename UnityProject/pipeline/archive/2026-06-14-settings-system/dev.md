# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:settings 通用设置系统·数据逻辑层(自治·放手默认)

设计基线 `design-docs/19-settings-system.html`。dev 已完成实现 + 编译自检,交接测试。

### 交接区(给 test)

**改动摘要**

- 按设计 §五清单实现「数据逻辑层」:音频设置 POCO + 存储接缝(隔离 PlayerPrefs)+ 服务编排(切换/落盘/应用音频/提示文案)+ 信息 getter(版本号/用户ID)+ 延后项钩子常量。全部落 `GameScripts/HotFix/GameLogic`(热更区),独立命名空间 `GameLogic.Settings`(与 BlockBlast 玩法解耦)。
- 复用框架既有键:服务落盘直引 `TEngine.Constant.Setting.MusicMuted/SoundMuted`(经核实 `Constant.cs:13,15`),不另造存储栈、不硬编码字面量。模型「开(on)」语义,落盘到框架「静音(muted)」键时取反(`SetBool(MusicMuted, !MusicOn)`),故启动 `ProcedureLaunch.InitSoundSettings`(读 `!GetBool`)零改动即生效。
- `GameModule.Audio` 在 HotFix **可达**(已 grep 核实 `GameModule.cs:56` `public static IAudioModule Audio => _audio ??= Get<IAudioModule>()`),故生产 sink 可直用 `GameModule.Audio.MusicEnable/SoundEnable`,无需退回 `ModuleSystem.GetModule`。`IAudioModule.MusicEnable/SoundEnable` 为 `{get;set;}`(`IAudioModule.cs:43,48`)。本轮只给 sink 契约,UI 投放延后未注线。
- 框架代码零改动(`Constant.cs`/`ProcedureLaunch`/`IAudioModule` 未动);既有玩法逻辑零行为变化(纯新增文件,无改既有文件)。
- 延后项按四档处置:占位 URL 常量(协议/隐私)/ stub 常量(客服)/ TODO 注释钩子(兑换码/新手关)/ 不做(快捷登录)——不投机性建接口。

**关键决策(自治默认内)**

- 测试文件 `using UnityEngine;` 里 `GameLogic.Settings.AudioSettings` 与 `UnityEngine.AudioSettings` 重名(CS0104),首轮编译报错;加类型别名 `using AudioSettings = GameLogic.Settings.AudioSettings;` 消歧。生产侧不受影响(生产文件无 `using UnityEngine`,且自身在 `GameLogic.Settings` 命名空间内本地优先)。
- 提示文案 textId 占位段选 190xxx(避开既有系统文本段),四值各异且非 0,满足 S3。
- 17 个验收点用 15 个 `[Test]` 方法覆盖(S2 含 sink 调用 + null 安全两断言;I3 含 id 返回 + null 安全两断言,合并入单方法)。

**文件清单(全新增,无修改既有)**

- `Assets/GameScripts/HotFix/GameLogic/Module/Settings/AudioSettings.cs`(模型 POCO,M1-M3)
- `Assets/GameScripts/HotFix/GameLogic/Module/Settings/ISettingsStore.cs`(接缝 + `PlayerPrefsSettingsStore` 生产 + `InMemorySettingsStore` 测试)
- `Assets/GameScripts/HotFix/GameLogic/Module/Settings/SettingsService.cs`(服务 + `SettingKind` 枚举,S1-S4)
- `Assets/GameScripts/HotFix/GameLogic/Module/Settings/SettingsInfo.cs`(版本号/用户ID getter,I1-I3)
- `Assets/GameScripts/HotFix/GameLogic/Module/Settings/SettingsLinks.cs`(协议/隐私 URL 占位 + 客服 stub + 兑换码/新手关 TODO)
- `Assets/GameScripts/HotFix/GameLogic/Module/Settings/SettingsText.cs`(提示文案 textId 占位常量 190001-190004)
- `Assets/Editor/Tests/BlockBlast/SettingsSystemTests.cs`(15 个 `[Test]`,覆盖 17 验收点)

**验证点(逐条对应设计 §六)**

- M1-M3 模型:`SettingsSystemTests.M1/M2/M3` — 默认全开 / `new` 字段初值 true / `[Serializable]` 无 Unity 依赖。
- P1-P5 存储往返(注 InMemory):`P1`(空 store 默认全开)/`P2`(SetMusic(false)→MusicMuted==true 取反正确)/`P3`(跨实例 Load 保真)/`P4`(两键独立 + SoundMuted==true)/`P5`(关后再开回 true)。**键映射取反是最高风险点**(设计 §八),P2/P3/P5 专门兜。
- S1-S4 服务:`S1`(Toggle 翻转、两次回原)/`S2`(SetMusic 触发 sink 以 (false,SoundOn) 调用 + null 不抛)/`S3`(四组 textId 各异且非 0)/`S4`(落盘 key 等于框架常量 `Setting.MusicMuted/SoundMuted`)。
- I1-I3 信息:`I1`(VersionProvider 可注入,用后还原)/`I2`(默认返 `Application.version` 非空)/`I3`(UserId 返 PlayerInfo.Id,null 返空串)。
- R1 回归:dev 已自跑 EditMode 全量 = **294/294 全绿**(279 既有 + 15 新增,0 failed,0 skipped,resultState Passed);编译 0 error(console 无 CSxxxx)。
- R2 Code Review 5 红线:本层无资源加载、无 GameEvent、不热更框架代码;重点核 ① PlayerPrefs 经 `Utility.PlayerPrefs.GetBool/SetBool/Save` 非阻塞 KV、不触同步 IO 红线 ② 音频经 `GameModule.Audio`(框架便捷属性)正路径。

**测试怎么验**:`run_tests mode=EditMode test_filter=SettingsSystemTests`(dev 已跑通,job 全 294 例)。纯 EditMode 注入隔离,不进 Play 模式、不碰真实 PlayerPrefs/真实音频。

**标注**

- 涉及热更程序集:是(`GameLogic.asmdef`,全在 HotFix 区)。
- Luban 重生成:否(本系统无配置表)。
- Play 模式手验(boss 授权遗留,不在本轮验收):真实音频开/关实听、设置界面 UI 视觉、各跳转按钮(协议网址/兑换码/新手关/客服)。

### 上一单存档

player-info 玩家信息系统·数据逻辑层已于 2026-06-14 关单 PASS,归档 `pipeline/archive/2026-06-14-player-info/dev.md`。
