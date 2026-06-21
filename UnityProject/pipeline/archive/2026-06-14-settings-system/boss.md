# 关单总结:settings — 通用设置系统·数据逻辑层(2026-06-14;自治模式·放手默认)

**结论:PASS 交付(数据逻辑层;UI 表现层转遗留)**。baton=full(plan→dev→test),**打回 0**。git 基线 `5251634a`。设计基线 `design-docs/19-settings-system.html`(§六 17 验收点)。源 spec `C:\Users\pc\Downloads\1005通用设置系统.xlsx`。

## 交付范围(本 spec 主体是 UI + 跳转未建系统,可测逻辑薄)
数据逻辑层:音频设置模型 + 本地持久化(复用框架既有约定)+ 应用接缝 + 信息 getter + 延后项钩子常量。新独立命名空间 `GameLogic.Settings`(与 BlockBlast 玩法解耦)。UI 窗口 + 各按钮投放 = 表现层延后(需美术,icon=设置图标),只留服务 + 钩子 + TODO。

## 运行验证(test 经 MCP)
- 编译 0 error;EditMode `BlockBlast.Tests` **294/294**(279 基线 + 15 新 SettingsSystemTests,覆盖 17 验收点),零回归。
- 第 3 类手验 = N/A(纯数据逻辑层,无运行期 UI);真实音频实听 / UI 视觉 / 跳转按钮 = boss 授权 Play 手验遗留。
- Code Review 5 红线全过;框架代码(Constant.cs/ProcedureLaunch/IAudioModule)零改动,纯新增文件、既有玩法零行为变化。

## 实现范围(加法式,全新增无改既有)
- AudioSettings(POCO 两 bool 默认全开)+ ISettingsStore(PlayerPrefsSettingsStore 生产 / InMemorySettingsStore 测试)+ SettingsService(切换/落盘/应用音频/提示文案)+ SettingsInfo(版本号 provider + 用户ID)+ SettingsLinks(协议/隐私占位 URL + 客服 stub + 兑换码/新手关 TODO)+ SettingsText(提示文案 textId 占位 190001-4)+ SettingsSystemTests 15 例。落 `GameScripts/HotFix/GameLogic/Module/Settings/`。

## 关键拍板(均安全默认,无 spec/GDD 抵触)
- **复用框架既有约定,不另造存储栈**:落盘直引 `TEngine.Constant.Setting.MusicMuted/SoundMuted` 键 + `Utility.PlayerPrefs`,「开(on)↔静音(muted)」语义取反映射,启动 `ProcedureLaunch.InitSoundSettings` 零改动即生效。判据:引擎级设置归框架约定、玩法元层进度才归项目存档(MergeMetaSave)——对比 player-info 并入 MergeMetaSave。
- 音频应用经可注入 `Action<bool,bool>` sink 隔离副作用,生产推 `GameModule.Audio.MusicEnable/SoundEnable`(grep 核实 GameModule.cs:56 在 HotFix 可达);验收锚在模型 + 存储往返 + 服务逻辑(InMemory 注入),不依赖真实音频/真实 PlayerPrefs。
- 延后项四档处置:协议/隐私占位 URL 常量、客服 stub(spec 自身标待定)、兑换码/新手关 TODO 钩子(依赖未建系统)、快捷登录不做(离线无账号)。音量滑条不做(spec 只给开/关)、UISound 独立开关不做(spec 只列两项)。
- 测试类型名 `GameLogic.Settings.AudioSettings` 与 `UnityEngine.AudioSettings` 重名(CS0104):测试文件加类型别名消歧,生产侧不受影响。

## 模型档 / 运行登记
- plan/dev/test=opus。Run `wf_de9b8cd9-84c`(full,round 0)PASS。plan 全部范围开关进 decisions、无 blockers(plan 空停根治持续有效)。

## 遗留/观察(转主 boss.md #24)
- settings **UI 表现层**:设置界面窗口 + 各按钮(音乐音效开关、联系客服、新手说明、版本号、用户协议/隐私、用户ID、兑换码入口)未投放,阻塞于美术(设置图标);基于本层服务驱动。
- settings **延后接线待补**:联系客服界面(spec 标待定)、协议/隐私真实 URL(当前占位常量)、兑换码入口(依赖未建的兑换码系统)、新手关跳转(依赖教学关)、提示文案多语言真实查表(存 textId 占位)。均钩子/常量就绪,接对应系统时补。

## 过程留痕(语体交叉检)
关单交叉检发现本轮 agent 在持久文件再次写入比喻/私造词:memory/dev.md「类型名撞内置」「撞引擎内置名」(撞)、state/plan.md「批次第五刀」(刀)、state/dev.md「AudioSettings 撞内置」(撞)——均已就地改平实(重名/同名/第五个增量)后再归档。佐证:conventions 收尾 lint 正则(`打死|挂了|收口|死在|尾巴上`)未含「撞/刀」,故只靠 lint 漏标,须 boss 关单人工通读兜底(本次即此)。
