# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:settings 通用设置系统·数据逻辑层(自治·放手默认)

设计稿 `design-docs/19-settings-system.html`(已加 index 卡片 + 全库 15 篇 sidebar 文档树同步 19 行 + 断链检查通过 + 无 BOM)。源 spec `1005通用设置系统.xlsx`。xlsx 系统底层批次第五个增量。

### 交接区(给 dev / test)

本轮交付**数据逻辑层**:音频设置模型 + 本地持久化(复用框架既有设置约定)+ 应用接缝 + 信息 getter。设计稿 §五是 dev 改动清单,§六是验收点,逐条对应下表。

**单一事实源 = 代码(已 grep 核实的真实符号)**:
- 音频开关:`TEngine.IAudioModule.MusicEnable`/`SoundEnable`(bool get/set,`Assets/TEngine/Runtime/Module/AudioModule/IAudioModule.cs:43,48`);运行期 `ProcedureLaunch` 经 `ModuleSystem.GetModule<IAudioModule>()`(`ProcedureLaunch.cs:19`)。
- 设置键约定:`TEngine.Constant.Setting.MusicMuted`/`SoundMuted`(`Constant.cs:13,15`,值 `"Setting.MusicMuted"`/`"Setting.SoundMuted"`);读写 `TEngine.Utility.PlayerPrefs.GetBool(key,def)`/`SetBool(key,v)`/`Save()`(`Utility.PlayerPrefs.cs:162,153,283`);启动加载 `ProcedureLaunch.InitSoundSettings()`(`ProcedureLaunch.cs:83`,`MusicEnable=!GetBool(MusicMuted,false)`)。
- 持久化接缝范本:`GameLogic.BlockBlast.IPersistenceProvider`+`Persistence.Provider`(`Persistence.cs`,生产 PlayerPrefs / 测试 InMemory)——`ISettingsStore` 仿此。
- 用户 ID:`GameLogic.BlockBlast.Player.PlayerInfo.Id`(string,设计 18)。版本号:`UnityEngine.Application.version`。
- 测试 asmdef `Assets/Editor/Tests/BlockBlast/BlockBlast.Tests.asmdef` 已引用 `GameLogic`+`TEngine.Runtime`,`Constant.Setting`/`Utility.PlayerPrefs` 可达。

**关键约束(防 dev 做歪)**:
1. **复用框架既有键,不另造存储栈**:写 `Constant.Setting.MusicMuted/SoundMuted` 同一套键,故 `InitSoundSettings` 零改动即「下次登录用本地配置」。`MusicOn`↔`MusicMuted` 取反映射(muted=false 即 on)。**不**塞进 `MergeMetaSave`、**不**新建第二套 PlayerPrefs 键(对比设计 18 玩家信息属玩法元层故并入 MergeMetaSave;音频设置属引擎级设置故就近复用框架约定,理由见 §2.2)。
2. **键常量直接引用 `TEngine.Constant.Setting.*`,不硬编码字面量**(防与框架键漂移)。
3. **纯逻辑可单测 + 注入隔离副作用**:模型 / 服务纯内存;持久化经 `ISettingsStore`(测试注 InMemory)、应用音频经可注入 `Action<bool,bool>` sink(测试记录)。验收锚在模型 + 往返 + 服务逻辑,不碰真实 PlayerPrefs / 真实音频模块。
4. **不改框架代码**:`Constant.cs`/`ProcedureLaunch`/`IAudioModule` 一律不动。
5. **`GameModule.Audio` 在 HotFix 可达性 dev 须 grep 核实**;不可达则 sink 内改 `ModuleSystem.GetModule<IAudioModule>()`(同 ProcedureLaunch)。sink 隔离使验收不依赖此选择。
6. **延后项分四档不投机性建接口**:占位 URL 常量(协议 / 隐私)/ stub(客服,spec 自身标待定)/ TODO 钩子(兑换码 / 新手关,依赖未建系统)/ 不做(快捷登录,离线无账号)。
7. **命名空间 `GameLogic.Settings`**(通用系统,与 BlockBlast 玩法解耦),物理目录建议 `GameScripts/HotFix/GameLogic/Module/Settings/`。

**dev 新建文件(§五清单)**:`AudioSettings`(POCO 两 bool 默认全开)/ `ISettingsStore`+`PlayerPrefsSettingsStore`+`InMemorySettingsStore` / `SettingsService`+`SettingKind` / `SettingsInfo`(版本号 provider + UserId)/ `SettingsLinks`+`SettingsText`(占位 URL + 客服 stub + 提示 textId)/ 测试 `SettingsSystemTests.cs`。

**验收点(EditMode 可测,17 条,设计稿 §六逐条断言定义)**:
- 模型 M1–M3:默认全开 / 字段初值 true / `[Serializable]` 无 Unity 依赖。
- 存储往返 P1–P5(注 InMemory):空 store 默认全开 / `SetMusic(false)`→muted==true(取反正确)/ 跨实例 Load 保真 / 两键独立 / 关后再开回 true。
- 服务 S1–S4:Toggle 翻转 / `SetMusic` 触发 sink 以 (false,SoundOn) 调用且 null 不抛 / `ToggleTipTextId` 四组各返不同非 0 textId / 写入 key 等于框架常量(非硬编码)。
- 信息 I1–I3:VersionProvider 可注入 / 不注入返 `Application.version` / `UserId(null)` 返空串不抛。
- 回归 R1–R2:编译 0 error + 现有 279 例 EditMode 全绿 + 新测全绿 / Code Review 5 红线(重点 PlayerPrefs 非阻塞不触同步 IO + 音频访问正路径)。

**Play 手验遗留(boss 授权,不在本轮验收)**:真实音频开 / 关实听、设置界面 UI 视觉、各跳转按钮(协议网址 / 兑换码 / 新手关 / 客服)。

**待拍板(均取安全默认,boss 自治授权内,decisions 已记;要改另开增量)**:UI 投放延后 / 客服 stub / 协议隐私占位 URL / 兑换码·新手关 TODO 钩子 / 提示文案 textId 占位 / 音量滑条不做 / 快捷登录不做 / UISound 开关不做。
