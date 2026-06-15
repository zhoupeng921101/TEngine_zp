# 状态:Boss(编排日志)

> 只记不可推导信息:任务定义、拍板决策、自治授权、打回轮次、关单结论与遗留事项。
> **不记阶段/进度**——恢复时从各 `state/*.md` 交接区现场推导(规则见 `.claude/skills/pipeline/SKILL.md`)。
> 关单后:当前任务编排日志归档进 `archive/<任务>/boss.md`;本文件只留「当前任务 + 关单索引 + 活遗留」,不留关单详情(详情进 archive,防主文件只增不减+转述副本漂移)。

## 当前任务

**ui-rank-window 排行榜表现层(2026-06-15 开单,自治·纯 UI 补完·末屏)** — 兑现遗留 #27
- 范围:新建 RankWindow 显示榜单(我的名次条/榜单列表/点赞/奖励预览),数据层 rank(#27 RankService 离线本地榜+stub)已就绪。**art 受限**:塔罗素材无排行榜专属切图、无榜行/徽章/头像切图 → 复用 Sheet_settings 木板+按钮 + 占位榜行;视觉是结构占位、非高保真。
- 美术:效果图 `…\效果图\排行榜.png`;复用 Sheet_settings + 占位(榜行/徽章/头像待美术补切图)。
- 参与环节:full;验收 boss 桥直跑。**纯 UI 补完范围最后一屏,完后收尾呈报。**

> 背景方向(2026-06-14 用户拍板·xlsx 批次):剩余服务器/在线功能与本作离线·去变现方向不合,选「继续建底层,留服务器接缝」;兑换码/邮件/排行榜已落地关单。通用底层批基本到顶,转入 UI 换皮主线(遗留 #30),本工具是其前置。

## 自治运行(2026-06-15 用户激活:「你自主决策,直到完成全部 UI」)

- **授权范围**:塔罗成套美术(user-memory tarot-art-set)其余约 18 屏 UI 全部完成(遗留 #30)。当前 ui-atlas-packer 是其前置工具(#28)。
- **git 基线**:`ded668bf`(设置窗关单 = 自治起跑基线;本地未 push,可 `git reset` 一键退回)。
- **自主边界**:不 push / 不 build / 不发布。默认全自主推进,每次拍板记下方决策日志;抵触 GDD 方向 / 3 轮熔断 / 无安全默认 → 记 BLOCKED 跳过、继续不依赖项、结束呈报。
- **范式**(每屏复用):精灵表寻址(打表工具产出)+ GameContext 持有无主数据层 + FindChildComponent/m_ 前缀绑定 + [Window]。有数据层屏走 dev-test,缺数据层新功能走 full(建离线底层 + 服务器接缝,对齐去变现·离线方向)。
- **执行顺序(用户裁决=纯 UI 补完;2026-06-15 按美术可复用度再修订)**:① 打表工具 ✅ → ② 个人信息窗 ✅(#22)→ ③ 结算窗 ✅(游戏结束+恭喜通关)→ ④ tarot_mode HUD ✅(Classic GameWindow 再主题,首用打表工具)→ **当前 ⑤ 排行榜表现层(#27,art 受限,末屏)** → ⑥ 收尾呈报。**主菜单移出**(塔罗素材 20 效果图无主菜单屏、无切图,无设计参照)。新系统屏(占卜/牌组/皮肤/成就/每日任务/关卡开始)+ 商店仍 BLK1/BLK3 不在范围。
- **运行验证约定(本轮确立)**:子会话 Unity 桥不稳定 → 每屏 EditMode/Play 验证由 boss 主会话桥(`UnityProject@02a6dcaa`,稳定)直跑兜底;dev/test 子会话桥不可用时按静态分析改、boss 跑测锚定。

### 自治决策日志
- D1:打表工具优先于第二屏(test 建议;一次性投入摊薄 18 屏手工成本)。
- D2:缺数据层新功能屏建「离线可用底层 + 服务器接缝」,对齐既定去变现·离线方向,不接真实付费/服务器。
- D3(2026-06-15·美术盘点):塔罗素材 20 效果图 vs 12 切图文件夹**错配**。① 7 屏无专属切图:个人信息/关卡开始/恭喜通关/成就/排行榜/每日任务/游戏结束;② 错配方向——有数据层的屏(个人信息#22/游戏结束/通关/排行榜#27)恰**无**切图(须复用共享木板/按钮切图),有切图的屏(塔罗模式/占卜/占卜解读/占卜选择/选择主题/选择具体方向/卡组/牌组库/皮肤_棋子/皮肤_背景)多**缺**数据层、且 tarot_mode 含新机制(swap/delete/hint)。结论:「全部 UI」实为「建约 6 套新游戏系统(规则+内容,属 GDD/产品决策)+ 为 7 屏无切图屏改用复用美术 + 商店付费冲突」,远超纯换皮。已就范围向用户呈报(见下 BLK3)。

### 自治 BLOCKED 清单(结束呈报)
- BLK1(抵触方向·待裁决):商店(商店.png)为真实货币内购(£ 标价宝石包),抵触本作去变现·离线·无服务器方向。不自动建付费 UI;可选替代=软通货商店(花游戏内赚的宝石买皮肤/道具,与离线方向相容)。待用户裁决。
- BLK3(范围·**用户已裁决 2026-06-15 = 只做纯 UI 补完**):有切图但无数据层的新游戏系统屏——占卜流程(选主题→选具体方向→占卜→解读)/牌组库·卡组(塔罗卡收集)/皮肤(棋子·背景)/成就/每日任务/关卡开始。这批要建整套游戏规则+内容(GDD/产品决策),本轮**暂不做**,待用户后续给规格再单独开。不自治盲造。

### 自治决策日志(续)
- D4(2026-06-15·用户裁决):「全部 UI」范围收窄为**纯 UI 补完**——只 reskin 有现成逻辑的屏(个人信息#22/排行榜表现层#27/游戏结束/恭喜通关/主菜单/tarot_mode HUD[新机制占位]),无切图屏复用 `设置/` 共享木板+按钮切图。新系统屏(BLK3 那批)暂不做。商店(BLK1)仍待裁。
- BLK2(环境·已恢复 2026-06-15):此前 dev/test 子会话报 Unity 桥未注册 + Fantasy 包编译错阻断域重载。boss 主会话实测:桥已连(active_instance `UnityProject@02a6dcaa`)、工程编译通过(EditMode 377 跑起、BlockBlast 零回归)、Fantasy 编译错已不复现(环境透明恢复)。运行验证已可做,自治线恢复。注:**子会话的 Unity 桥可能仍不稳定(打表工具 dev/test 曾 no_session),boss 主会话桥稳定 → 验证由 boss 直跑兜底**。
- ui-atlas-packer 打回(轮次 1·test FAIL=测试夹具缺陷,非工具缺陷):boss 用 execute_code 对真实 `setting/` 直跑 `Pack` 实证**工具正确**——产出 `Sheet_setting.png`、readback 21 命名子图、border 6×24+15×0、无残留名(验收锚 R2+S2 达成,sheet=Multiple、P 组寻址结构就绪)。但 11 例 EditMode 单测 8 例 NRE:根因在**测试夹具**——临时目录 `File.Copy`/`CopyAsset`+`AssetDatabase.Refresh` 在单测方法内不同步生效,`AssetImporter.GetAtPath` 返 null(`CreateFixtureFrom` 行 77→78 NRE),Pack 没跑到。spawn dev 返修测试夹具(工具勿动)。`Sheet_setting.png` 已由 boss 探针产出在磁盘(正确、保留=设置屏后续可用)。

## 最近关单(索引;详情见各 `archive/<任务>/boss.md`)

| 日期 | 任务 | 结论 | 归档 |
|------|------|------|------|
| 2026-06-15 | ui-tarot-hud tarot_mode 主玩法 HUD reskin(=Classic GameWindow 再主题;路 A 只换 BuildStaticUI 静态壳+新增顶栏/动作按钮;首用打表工具产 Sheet_tarot_mode[13 子图];资源条占位/更换删除提示 stub[无数据源无机制];玩法逻辑/坐标/数据层零碰;纯 UI 补完)| PASS 回归档(自治;full,0 打回;boss 桥直验 EditMode 419[BlockBlast 407 含 14 例换皮回归 R1-R5 零回归];V 组 Play 对位+整局实玩=人工手验;运行期寻址 importer 同 Sheet_settings 等价) | `archive/2026-06-15-ui-tarot-hud/` |
| 2026-06-15 | ui-settlement-windows 结算窗 reskin(游戏结束 GameOverWindow + 恭喜通关 MergeOrderWinWindow;路 A 轻量换皮——UGuiFactory 纯色 Image 链 SetSubSprite("Sheet_settings") 换木质,结算逻辑/UserData/回调零改;装饰图标省略[无数据源无切图];纯 UI 补完)| PASS 回归档(自治;full,0 打回;boss 桥直验 EditMode 405[BlockBlast 393 含 15 例结算回归 R1-R4 零回归 + UIAtlasPacker 12];V 组 Play 对位=人工手验)| `archive/2026-06-15-ui-settlement-windows/` |
| 2026-06-15 | ui-player-info-window 个人信息窗·表现层(新建 PlayerInfoWindow 弹窗;复用设置窗范式+Sheet_settings 精灵表[无专属切图];GameContext 扩持 PlayerInfo;改名委托 PlayerRenameService;生日 UI 占位不入存档;兑现 #22)| PASS 逻辑档(自治;full,0 打回;boss 桥直验 EditMode 390/390[+12 player];V 组 Play 对位/指针=人工手验;头像框/铅笔/下拉箭头占位待美术) | `archive/2026-06-15-ui-player-info-window/` |
| 2026-06-15 | ui-atlas-packer 散切图打表工具(Editor 菜单工具:切图目录→Multiple 精灵表 PNG `Sheet_<dir>`,子图名=源文件名/PackTextures 排布/pivot 居中/border 从源继承+_border_override.json 覆盖;现代 API ISpriteEditorDataProvider;Editor-only 不热更。塔罗 UI 换皮范式的生产工具,遗留 #28) | PASS(自治;full,1 打回[测试夹具资源时序 NRE,工具本身正确];boss 主会话桥直验 EditMode 378/378[BlockBlast 366+UIAtlasPacker 12]+ execute_code 实证工具产出 21 子图 6×24/15×0) | `archive/2026-06-15-ui-atlas-packer/` |
| 2026-06-15 | ui-settings-window 设置窗美术换皮(首个美术驱动 UI 窗口;切图寻址范式=每屏一张 Multiple 精灵表 PNG + SetSubSprite[用户拍板 SpriteAtlas v2 经实测+test 复核技术不可行,等价替代];GameContext 运行期上下文统一持有无主数据层[方案 B];兑现 #24 settings 表现层) | PASS(常规·用户在场;full,0 打回;EditMode 366/366 + GameContextTests 7 真验 + Play V2 寻址独立复验通[Sheet_settings 21 子图、26/26 节点贴图、对位 setting.png]) | `archive/2026-06-15-ui-settings-window/` |
| 2026-06-14 | rank 排行榜系统·数据逻辑层 + 服务器接缝(多榜配置 id 聚合/查榜+分数降序同分 AchievedTicks 升序顺序名次/入榜要求+CountMax+ShowMax/结算时机四档[Always/OpenDays/FixedTime/Weekly周循环]+幂等/每日点赞跨天/红点;发奖三种统一经邮件 21 IMailService.Send,排名层不碰 MergeOrderState/16;IRankSource 离线本地榜+远程 stub 零网络;持久化复用 Provider 键 Rank.Progress;表现层+真实全服榜转 #27) | PASS(自治·放手默认;full,0 打回;EditMode 359/359 + Rank 24 + Luban C3 GREEN) | `archive/2026-06-14-rank-system/` |
| 2026-06-14 | mail 通用邮件系统·数据逻辑层 + 服务器/运营接缝(收件箱/领取单+一键/删除/自动清理/红点;IMailService.Send 对外 API + IMailSource 运营 stub;发奖复用 16、持久化复用 Provider;表现层+真实服务器转 #26) | PASS(自治·放手默认;分两段:plan 撞用量上限→基线 367c080d,dev-test 续接 0 打回;BlockBlast 335/335 + Mail 24/24 + Luban 直读) | `archive/2026-06-14-mail-system/` |
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
24. **[后续轮·settings UI 表现层 + 接线待补;阻塞于美术 + 未建系统]** settings 本轮只交付数据逻辑层(音频设置 `GameLogic.Settings` + 信息 getter)。① UI 表现层:设置界面窗口 + 各按钮(音乐音效开关/联系客服/新手说明/版本号/用户协议·隐私/用户ID/兑换码入口)未投放,阻塞于美术(设置图标),基于本层服务驱动;② 接线待补:联系客服界面(spec 标待定)/ 协议·隐私真实 URL(当前占位常量,UI 接时 Application.OpenURL)/ 兑换码入口(兑换码系统已建,2026-06-14;入口按钮接 `GameLogic.Redeem` 服务即可,见 #25)/ 新手关跳转(依赖教学关)/ 提示文案多语言真实查表(存 textId 占位 190001-4)。音频开关本身已实装并经框架键持久化(`Setting.MusicMuted/SoundMuted`),真实音频实听走 Play 手验。 **部分兑现(2026-06-15 ui-settings-window 关单)**:设置窗 UI 已落地(`SettingsWindow` + prefab + 切图寻址范式打通;`GameContext` 持有 SettingsService;音乐+音效双开关贯通数据层落盘;主菜单入口按钮)。仍待:占位按钮(社交/联系客服/更多游戏/语言切换/退出登录/清除存档)真实实做、协议·隐私真实 URL(当前 OpenURL 占位)、兑换码入口接 `GameLogic.Redeem`(#25)、人工 Play 手验(指针 X/遮罩关窗 + 真实音频实听)。
25. **[后续轮·redeem-code 表现层 + 真实服务器;阻塞于美术 + 无网络模块]** redeem-code 本轮交付数据逻辑层 + 服务器接缝(`GameLogic.Redeem`:`IRedeemValidator` 可注入,本地 `LocalConfigRedeemValidator` 查 Luban 表 + 远程 `RemoteRedeemValidator` stub 返 SourceUnavailable 零网络调用;发奖复用 16 `ItemGrant.GrantOnAcquire`;去重复用 `Persistence.Provider` 键 `Redeem.Redeemed`)。未做:① 真实服务器校验——工程无网络模块,`RemoteRedeemValidator` 待网络模块就绪后实现,服务层零改动切注入;② UI 表现层(需美术):兑换码输入界面 + 结果弹窗 + 真实 Sprite;③ 设置界面兑换码入口按钮接线(`SettingsLinks.OpenRedeemCode()` TODO 已指向本服务,#24)。结果文案 textId 占位 110601–110606,真实多语言查表延后(同 num/item/reward/settings)。逻辑层不返工。
26. **[后续轮·mail 表现层 + 真实服务器;阻塞于美术 + 无网络模块]** mail 本轮交付数据逻辑层 + 两道接缝(`GameLogic.Mail`:收件箱模型 + `MailboxService` 收件/列表/已读/领取单+一键/删除已读/自动清理超N+过期/红点;`IMailService.Send` 对外发件 API 真做;`IMailSource`/`InertMailSource` 运营推送 stub;发奖复用 16 `GiftOpener`→`ItemGrant`、持久化复用 `Persistence.Provider` 键 `Mail.Inbox`)。未做:① UI 表现层(需美术)——邮件界面/详情/无邮件态/全部删除确认/红点显示/icon/真实 Sprite/主界面入口接线;② 真实服务器后台发删/定时邮件 + 区服多选——无网络模块,`IMailSource` 待网络模块就绪后实现(离线视单一本地区服);③ 多语言文案真实查表(textId 占位 110701-110725)。排行榜结算(#队列下一项)届时接 `IMailService.Send` 发奖。逻辑层不返工。
27. **[后续轮·rank 表现层 + 真实服务器/全服榜;阻塞于美术 + 无网络模块]** rank 本轮交付数据逻辑层 + 两道接缝(`GameLogic.Rank`:配置按 id 聚合多榜 `RankConfigMgr` + 名次档 `RankDef.TierForRank`;`RankService` 查榜/分数降序+同分 AchievedTicks 升序+顺序名次/入榜要求+CountMax+ShowMax/我的名次;结算编排 `IsSettleDue` 四档[Always/OpenDays/FixedTime/Weekly 周循环]+`CheckAndSettle` 纯方法幂等防重不起定时器;每日 `ClaimDaily`/点赞 `ClaimPraise` 跨天重置;红点 `HasClaimable`;发奖三种统一经邮件 21 `IMailService.Send` 挂奖励库 id,排名层不碰 MergeOrderState/ItemGrant/GiftOpener;持久化复用 `Persistence.Provider` 键 `Rank.Progress`,只存本机进度,他人成绩不进盘)。未做:① UI 表现层(需美术)——排行榜界面/榜单列表/我的名次条/点赞按钮/奖励预览/头像 + icon 红点显示 + 主界面入口接线;② 真实全服榜 + 真实他人数据——无网络模块,`RemoteRankSource` 返空 stub 零网络,待网络模块就绪后实现、服务层零改动切注入(陪榜成绩当前由配置/注入基准分,非随机 NPC);③ 各玩法接入——`rank_method` 玩法类型整数枚举占位,各玩法届时经 `SubmitScore` 提交成绩;④ 结算自动触发时机——`CheckAndSettle` 触发交调用方(登录/tick 由表现层接),本轮不起后台定时器;⑤ 文案 textId 占位真实多语言查表延后(同 num/item/reward/settings/redeem/mail),含 `RankService.GetMyBest` 暂借 `RankText.SettleSender` 当占位玩家名 textId(O6 范畴,真实查表时归位)。逻辑层不返工。
28. **[后续轮·UI 范式工具化;第二屏换皮前补]** 切图寻址范式已定为「每屏一张 Multiple 模式精灵表 PNG + `Image.SetSubSprite(图集location, 子图名)`」(ui-settings-window 关单确立,SpriteAtlas v2 经实测不向 YooAsset 暴露子精灵故不可用)。当前 `Sheet_settings.png` 是一次性手工合表。**建议在第二屏换皮前做一个 Editor 打表工具**(散切图目录 → 合成 Multiple 精灵表 PNG,子图名=源文件名,自动切 rect、九宫格 border 可配),否则约 20 屏每屏手工打表的成本线性累积且易错(子图名漏改/rect/pivot/border 手设)。交 plan 排期。
29. **[待清理·低优]** `Assets/AssetArt/Atlas/Atlas_setting.spriteatlasv2`(+.meta,单数 setting)是 ui-settings-window 轮 dev 失败 SpriteAtlas 尝试的残留,GUID 全工程仅自身 .meta 引用 = 真孤儿,落在 AssetArt/Atlas(不被收集器收录)运行期无害,应删(避免误导后续 + 无用残留)。
30. **[后续轮·塔罗成套 UI 换皮主线]** 塔罗美术成套素材(见 user-memory tarot-art-set:`Downloads\塔罗\塔罗\`,效果图 20 屏 + 切图 184 张)其余约 19 屏待换皮,按 ui-production-plan 排序:先换皮有数据层的(游戏 HUD tarot_mode / 个人信息 #22 / 游戏结束 / 恭喜通关),再按系统逐个做新功能(商店内购/成就/排行榜表现层 #27/每日任务/皮肤/牌组库/占卜流程——多数需先做数据层)。每屏走 /pipeline,复用本轮确立的「精灵表寻址 + GameContext 持有 + FindChildComponent 绑定 + [Window]」范式。
