# 状态:Boss(编排日志)

> 只记不可推导信息:任务定义、拍板决策、自治授权、打回轮次、关单结论与遗留事项。
> **不记阶段/进度**——恢复时从各 `state/*.md` 交接区现场推导(规则见 `.claude/skills/pipeline/SKILL.md`)。
> 关单后:当前任务编排日志归档进 `archive/<任务>/boss.md`;本文件只留「当前任务 + 关单索引 + 活遗留」,不留关单详情(详情进 archive,防主文件只增不减+转述副本漂移)。

## 当前任务

### mail 通用邮件系统·数据逻辑层 + 服务器/运营接缝(自治·放手默认)

**来源**:`C:\Users\pc\Downloads\1002通用邮件系统.xlsx`。**baton** full,opus 三档。git 基线 `f51bd660`(redeem-code 关单,本轮起点工作树干净)。

**方向决策(2026-06-14 用户拍板,本批次共用)**:剩余队列(兑换码/排行榜/邮件)本质是服务器/在线功能,与本作离线·去变现·无服务器方向不合;用户选「**继续建底层,留服务器接缝**」——当可复用通用底层做,逻辑层可测、服务器侧抽接缝 + TODO,离线版 inert。兑换码已落地(关单)。**增量排序(boss 放手默认授权内调整)**:邮件先于排行榜——排行榜结算奖励经邮件发放(spec mail 字段=邮件id),且邮件 spec 明写「为其他功能留邮件调用接口,用于奖励发放」,故先建邮件底层,排行榜届时接真实本地邮件服务而非再 stub。

**spec 摘要(boss 提取自 xlsx)**:系统发信息与奖励,后台全服/私人/附件邮件(运营侧)。
- 邮件规则:保留期默认一月到期自动消失;未领取数 > N(默认 100)按时间删最早、新邮件插尾,总上限 N;N 与保留时间入全局配置。
- 邮件格式:发件人 + 发件时间 + 标题 + 内容 + 奖励包(可选)。
- 邮件配置表(表 sheet):id | 标题(多语言 textId)| 内容(多语言 textId)| 有效期(天)| Reward表id(奖励随机库表 id,接 16 道具系统 gift 表)。5 条 demo 行。
- 状态:已读/未读 × 奖励(无/已领/未领);红点 = 未读 OR 有奖励未领。
- 操作:一键领取(发所有未领奖→弹奖励展示→全标已读)/ 单封领取 / 删除已读(已读且奖励已领或无奖励)/ 列表排序(已读>未读,再时间)。
- 开启:1 级即开。道具/跑马灯需求无;红点有;运营/服务器需求 = 后台发删定时邮件 + 留对外调用接口(服务器侧,标待定/后面做)。美术:UI 见界面,原画/特效/动画无,icon = 主界面邮件图标。

**boss 自治授权 + 预先拍板(放手默认)**:
- **中心定位**:交付**数据逻辑层 + 服务器/运营接缝**。① 邮件 Luban 配置表 + 全局配置(maxCount/retainDays 可配,默认 100/30);② 邮件数据模型(发件人/时间/标题/内容/奖励附件/已读/已领取);③ 收件箱服务 MailboxService:**收件(对外 API `IMailService.Send`,供排行榜结算/活动回收/系统奖励调用——即 spec 的「留邮件调用接口」)** / 列表(已读>未读+时间) / 标记已读 / 领取(单封 + 一键,复用 16 `ItemGrant.GrantOnAcquire` + 17 `RewardView` 归一,领后弹奖励展示) / 删除已读 / 自动清理(>N 删最早 + 过期删除,注入时钟) / 红点 getter(未读 OR 有奖励未领);④ 持久化复用 `Persistence.Provider`(专用键 `Mail.Inbox`,序列化收件箱,反序列化脏数据产合法空集合不抛);⑤ 服务器/运营接缝 `IMailSource`(后台发/定时/区服多选)= stub + TODO,离线不实现,区服离线视单一本地区服。
- **奖励发放**复用 16/17,不另造;奖励附件经 Reward表id → gift 表 → `ItemGrant`。
- **时钟注入** NowProvider(默认系统时钟),有效期/保留/过期清理用注入 today,可单测(同 save/redeem)。
- **UI 表现层延后**(需美术):邮件界面(列表/详情/无邮件/全部删除提示)+ 红点显示 + icon = 留服务 + 红点状态 getter,UI 投放转遗留;多语言文案存 textId 占位。
- 验收强度:EditMode 覆盖 收件 / 排序 / 标记已读 / 领取(单+一键)/ 删除已读 / 自动清理(超 N + 过期)/ 红点计算 / 持久化往返 / 奖励复用;Luban 直读;编译 0 error、零回归;Code Review 5 红线。
- 命名空间 `GameLogic.Mail`(同 Redeem/Settings 体例),配置桥接 `MailConfigMgr` 归 `GameLogic.Config`。
- 仅遇与 GDD/spec 方向抵触才停问用户(本方向已拍板);有安全默认的范围开关 plan 走 decisions 默认推进。

**队列剩余**(续接同一决策):排行榜(`1007排行榜底层-----.xlsx`,接本轮邮件服务做结算发奖)。TOAST(1008,纯 UI/阻塞于美术)与 音乐(1009,与 settings 音频重合)离线近空。

## 最近关单(索引;详情见各 `archive/<任务>/boss.md`)

| 日期 | 任务 | 结论 | 归档 |
|------|------|------|------|
| 2026-06-14 | redeem-code 通用兑换码系统·数据逻辑层 + 服务器接缝(IRedeemValidator 可注入/本地 Luban 校验+远程 stub;发奖复用 16;去重复用 Persistence;表现层+真实服务器转 #25) | PASS(自治·放手默认;full,0 打回;BlockBlast 311/311 + Redeem 17/17 + Luban 直读) | `archive/2026-06-14-redeem-code-system/` |
| 2026-06-14 | settings 通用设置系统·数据逻辑层(音频开关+持久化复用框架键/信息 getter;表现层转 #24) | PASS(自治·放手默认;full,0 打回;EditMode 294) | `archive/2026-06-14-settings-system/` |
| 2026-06-14 | player-info 玩家信息系统·数据逻辑层(档案/头像表/名字/改名/解锁三态;表现层转 #22) | PASS(自治·放手默认;full,0 打回;EditMode 279;plan 空停根治首次实战无空停) | `archive/2026-06-14-player-info/` |
| 2026-06-14 | reward-display 通用奖励展示·归一层(奖励→RewardView 归一;表现层转 #20) | PASS(自治·放手默认;full+dev-test,0 打回;EditMode 251) | `archive/2026-06-14-reward-display/` |
| 2026-06-14 | item-system 道具底层(道具/礼包/背包;Luban 3 表) | PASS(自治·放手默认;full+dev-test,0 打回;EditMode 234) | `archive/2026-06-14-item-system/` |
| 2026-06-14 | numeric-system 数值底层(配置化数值/Luban 货币表) | PASS(自治·放手默认;首个 Luban 表;full+dev-test,0 打回;EditMode 209) | `archive/2026-06-14-numeric-system/` |
| 2026-06-14 | save-system MergeOrderState 跨会话存档 | PASS(自治·放手默认;核心 full + 时机层 dev-test,各 0 打回;解决遗留 #14) | `archive/2026-06-14-save-system/` |
| 2026-06-14 | piety-temple-repair 虔诚币+神庙修复主线 | PASS(自治·放手默认;full 一轮过,0 打回) | `archive/2026-06-14-piety-temple-repair/` |
| 2026-06-14 | tarot-blind-box 神秘塔罗盲盒 | PASS(自治;dev-test 一轮过,0 打回) | `archive/2026-06-14-tarot-blind-box/` |
| 2026-06-13 | core-loop-completion 核心玩法补全 | PASS(自治;环境恢复后 test-only 收尾) | `archive/2026-06-13-core-loop-completion/` |
| 2026-06-12 | collect-rename Collect* 正名 | PASS(dev-test,0 打回) | `archive/2026-06-12-collect-rename/` |
| 2026-06-12 | score-element-rm-collect 得分驱动元素+移除 collect | PASS(full,0 打回) | `archive/2026-06-12-score-element-rm-collect/` |
| 2026-06-12 | merge-order-energy 合成订单体力切片 | PASS(full,0 打回) | `archive/2026-06-12-merge-order-energy/` |
| 2026-06-11 | collect-demo-slice 收集玩法切片 | PASS(full,0 打回) | `archive/2026-06-11-collect-demo-slice/` |

## 遗留事项(未清,逐条标注归属)
1. **[用户·人工]** Play 模式拖拽手感点验:真实拖拽落子 + ghost 落点高亮,Classic 与 Collect 两窗各一遍(MCP 无法模拟指针拖拽,其余渲染/逻辑已有截图+单测覆盖)。
2. **[出包时]** 改动全在 GameScripts/HotFix/GameLogic,正式出包需 HybridCLR 重新生成热更 dll;无需 Luban。Editor 直跑无需额外步骤。
3. **[待用户定夺·可不做]** UX 取舍:收集失败复用 GameOverWindow,「PLAY AGAIN」回 Classic、SCORE 显 0;如要「失败回收集」需单独排期。
4. ~~**[待派活·与本任务无关]** EditMode 全量含 2 条 HtmlToUGUI 示例测试失败(Xxhq.Htmltougui.Editor.Tests.EditorExampleTest,NullReferenceException),属 html-to-ugui 管线遗留,建议清理。~~ **已清理(2026-06-11):** 删除整个 `Assets/HtmlToUGUI/Tests/` 目录(仅含包自带示例测试 EditorExampleTest + 叶子测试 asmdef,无人反向引用)及 `Tests.meta`。
5. **[本条以上 1-3 用户已表态不处理]**(2026-06-11):拖拽手感点验/出包 HybridCLR/收集失败 UX 三条用户明确「不用管」,留档不再跟进。
6. **[用户·人工]** merge-order Play 手验:体力条扣/返/补、订单卡点亮/置灰/交付、合成区 token 重建、悔棋按钮态、双失败弹窗(软死亡/棋盘塞满)、通关弹窗、拖拽落子手感(逐项清单见 `archive/2026-06-12-merge-order-energy/test.md` 第 3 类;逻辑层已被 19 例单测覆盖,遗留仅 UI 交互表现)。
7. **[生产化时·设计约束]** 自动两两配对使非封顶等级库存恒 ≤1,「Lv1×N(N≥2)」型订单不可满足——demo 订单池已按此约束重排;正式版如要多个低级件订单,须改合成规则(如允许订单直接消耗未合成的低级件)。
8. **[待用户定夺·可不做]** merge-order 通关复用 CollectWinWindow,「再来一局」回收集 demo 而非本模式;如要回本模式需单独排期。
9. **[用户·人工]** score-element-rm-collect Play 手验:拖拽落子触发消除后,目视确认「得分越高、待选区出元素越多」「无消除时纯方块」,以及 A 档映射体感是否平衡(数值要调可回头改 `ScorePerElement` 等常量)。MCP 无法模拟指针拖拽,逻辑层已被 7 例新单测覆盖。
10. **[plan 环节或用户·低优]** design-docs/09、10 中对 Collect* 设施的「已转用」标注仍用旧名,需更新为新名(MergeElement / MergeElementVisual / MergeOrderWinWindow / HarvestClearedElements)——collect-rename 环节刻意不动 design-docs(角色边界:dev 不碰设计文档)。
11. **[用户·人工]** core-loop-completion Play 手验:进 `MergeOrderWindow` 真实拖拽落子,目视确认弹字(COMBO x{n} / MultiLabel / PERFECT)、连消/多消/全清的视觉表现。逻辑层已由 129 全绿单测兜底,MCP 无法模拟指针拖拽,仅核视觉呈现。
12. **[待查·框架·低优]** TEngine 框架 `ResourceModuleDriver.Update()`(`Assets/TEngine/Runtime/Module/ResourceModule/ResourceModuleDriver.cs:302`,`_resourceModule.UnloadUnusedAssets()`,`_resourceModule` 为 null)在 Play 模式 Update 触发 NRE。疑为直接进 Play、未走启动引导致 Resource 模块未初始化(环境/操作产物),与核心玩法改动无关;若正常启动流程下复现,需单独排查框架初始化时序。
13. **[后续轮次·设计 11 遗留]** 特殊订单(`SpecialOrderTrack`/`DeliverSpecial`)尚未接入任何 UI(`Request` 仅 tests 调用)。tarot-blind-box 的「特殊订单附赠盲盒」钩子(F10)已实现并被单测 A8 覆盖,但**真机暂无触达路径**——玩家本轮只能靠消除挑战解锁(连消阈值/全清,F6/F7)获得盲盒。接入特殊订单窗口投放/交付后,F10 渠道方真机可达。
14. ~~**[待用户定夺·影响 GDD 长期主线语义]** `MergeOrderState` 整体不做跨会话磁盘存盘,只入悔棋快照……虔诚币/神庙/经验/守护者等级/盲盒计数 都是单局尺度,退出清零。~~ **已解决(2026-06-14,save-system 关单)**:元层 13 字段经 `MergeMetaPersistence` 跨会话落盘(PlayerPrefs),启动加载、元动作/全清/pause 落盘,版本号+迁移+跨天重置;EditMode 190/190。沙盒文件介质 + 局内棋盘断点续玩列可选独立升级轮次。
15. **[用户·人工]** tarot-blind-box Play 拖拽手验:真实拖拽落子,连消到第 4 连 / 全清时目视确认弹「+1 ◈」且计数自增、开盒弹字与产物入合成区。逻辑层已由单测 A5/A6/A4 兜底,MCP 无法模拟指针拖拽,仅核视觉呈现。
16. **[用户·人工]** piety-temple-repair Play 手验:神庙按钮叠层开 TempleWindow 不丢局、12 厅四态渲染、点修复扣币+发奖+升级弹字+顶部刷新、关窗回底层刷虔诚币。逻辑层已由 19 例单测 + 一次真实 UI 路径手验覆盖,仅核拖拽落子等指针交互的视觉呈现。
17. **[用户·人工·真机]** save-system 真机切后台落盘:`UpdateDriver` 应用暂停事件→FlushSaveIfDirty 的真实触发须真机/真切后台验(MCP 不能挂起编辑器)。逻辑等价路径(pause==true 落盘 / false 不写 / 全清跨会话保真)已用生产 PlayerPrefs 路径跑通,仅核真机暂停回调确触发。
18. **[出包/CI·环境]** Luban 重导表(改任何配置表后)本机须带环境变量 `DOTNET_ROLL_FORWARD=Major`(本机无 .NET 7.0 runtime,Luban.dll 目标 .NET7,前滚到已装高版本运行),或装 .NET 7.0 runtime。CI 复跑导表脚本须配此环境。详见 `pipeline/memory/dev.md` Luban 条 + numeric-system 归档。
19. **[后续轮·接 GrantOnAcquire 调用方时]** item-system 的 `ItemGrant.GrantOnAcquire`(Automatic 分流 + 礼包递归,深度上限 5)无单测兜底(本轮经 Play 反射直调验证逻辑正确);且源 `itemdef.xlsx` 30006 automatic=0 与测试夹具 automatic=1 不一致(automatic 非验收项、无 test 调 GrantOnAcquire,故不影响本轮)。待礼包「获取即开」接到真实调用方时:补 GrantOnAcquire 单测 + 统一 30006 automatic 语义。
20. **[后续轮·reward-display 表现层;阻塞于美术 + 具体 UI 流程]** reward-display 本轮只交付归一层(奖励→RewardView)。源 spec 1003 的两种**可见展示**未做:① 弹框类网格布局(超过一排从左上往下排、不足一排居中);② 基础类飞图标到锚点(奖励类型→对应 UI 锚点:货币→资源栏、道具/图案→探险位等)。二者基于 RewardView 驱动,但 grid-calc 与 anchor-map 的契约依赖尚不存在的真实弹窗容器 / HUD 锚点 Transform + 无美术(图标 Sprite),此刻盲建为投机性工作。待有美术 + 具体奖励 UI 流程触达时:对真实容器实现 grid 布局与飞行组件,逻辑层(布局计算 溢出顶左/不足居中、奖励类型→锚点映射)补 EditMode 单测,补间/指针视觉走 Play 手验。设计 §七 O1/O4/O8(真实 Sprite/多语言文本表/单件恒显 x1 重载)同属「接 UI 时做」,归一层不返工,低优,在 17-reward-display.html §七 备查。
21. **[独立正名任务·低优]** reward-display 新建 6 档权威品质色 `RewardDisplay.QualityColor`(白/绿/蓝/紫/橙/红,对齐道具 EItemQuality);旧 4 档 `NumericDisplay.QualityColor`(白/蓝/紫/红,色序不一致)本轮冻结不删——它仍被 NumericDisplay 自用。收编:让 NumericDisplay 也走 RewardDisplay.QualityColor、删旧 4 档,须先核 UI 引用再删(对称 #10 collect-rename design-docs 旧名更新,均「先核引用后改」)。
22. **[后续轮·player-info UI 表现层;阻塞于美术 + 具体 UI 流程]** player-info 本轮只交付数据逻辑层。6 个 UI 未投放:玩家信息界面、改名界面、头像/框三态网格(当前佩戴/已解锁/未解锁)、等级+经验槽、等级奖励预览 tips、主界面左上角入口(MainMenuWindow 已留 TODO 钩子)。均基于本层服务驱动,届时接真实窗口容器 + Play 手验交互。
23. **[后续轮·player-info 数据层接线待补]** 钩子/接缝已就绪、待对应系统或调用方接入:① 解锁条件 type 2「活动发放」(`AvatarUnlockService.GrantUnlock` 钩子就绪,无活动系统不判);② 钻石可花费余额(`PlayerRenameService` 经 `trySpendDiamond` 接缝,生产默认 no-op,待数值系统实装钻石余额接真实扣减——与 item-system #19 钻石现状同源);③ 屏蔽字真实词表(`ProfanityFilter` 词表可注入,当前空表不拦,真实词表是后续数据);④ 多语言文本表(头像「解锁文字」存 textId,真实查表延后,与 numeric/reward NameTextId 同);⑤ 玩家经验真实来源接入 + 等级奖励内容(`PlayerExpService` 容器 + `PlayerLevelConfig` 曲线就绪,未接真实经验来源,等级奖励为占位接口)。均数据层不返工。
24. **[后续轮·settings UI 表现层 + 接线待补;阻塞于美术 + 未建系统]** settings 本轮只交付数据逻辑层(音频设置 `GameLogic.Settings` + 信息 getter)。① UI 表现层:设置界面窗口 + 各按钮(音乐音效开关/联系客服/新手说明/版本号/用户协议·隐私/用户ID/兑换码入口)未投放,阻塞于美术(设置图标),基于本层服务驱动;② 接线待补:联系客服界面(spec 标待定)/ 协议·隐私真实 URL(当前占位常量,UI 接时 Application.OpenURL)/ 兑换码入口(兑换码系统已建,2026-06-14;入口按钮接 `GameLogic.Redeem` 服务即可,见 #25)/ 新手关跳转(依赖教学关)/ 提示文案多语言真实查表(存 textId 占位 190001-4)。音频开关本身已实装并经框架键持久化(`Setting.MusicMuted/SoundMuted`),真实音频实听走 Play 手验。
25. **[后续轮·redeem-code 表现层 + 真实服务器;阻塞于美术 + 无网络模块]** redeem-code 本轮交付数据逻辑层 + 服务器接缝(`GameLogic.Redeem`:`IRedeemValidator` 可注入,本地 `LocalConfigRedeemValidator` 查 Luban 表 + 远程 `RemoteRedeemValidator` stub 返 SourceUnavailable 零网络调用;发奖复用 16 `ItemGrant.GrantOnAcquire`;去重复用 `Persistence.Provider` 键 `Redeem.Redeemed`)。未做:① 真实服务器校验——工程无网络模块,`RemoteRedeemValidator` 待网络模块就绪后实现,服务层零改动切注入;② UI 表现层(需美术):兑换码输入界面 + 结果弹窗 + 真实 Sprite;③ 设置界面兑换码入口按钮接线(`SettingsLinks.OpenRedeemCode()` TODO 已指向本服务,#24)。结果文案 textId 占位 110601–110606,真实多语言查表延后(同 num/item/reward/settings)。逻辑层不返工。
