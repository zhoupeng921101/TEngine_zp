# 状态:Boss(编排日志)

> 只记不可推导信息:任务定义、拍板决策、自治授权、打回轮次、关单结论与遗留事项。
> **不记阶段/进度**——恢复时从各 `state/*.md` 交接区现场推导(规则见 `.claude/skills/pipeline/SKILL.md`)。
> 关单后:当前任务编排日志归档进 `archive/<任务>/boss.md`;本文件只留「当前任务 + 关单索引 + 活遗留」,不留关单详情(详情进 archive,防主文件只增不减+转述副本漂移)。

## 当前任务

### item-system — 道具底层(配置化道具/礼包/背包;2026-06-14 起单;**自治模式·放手默认**)

**参与环节**:plan→dev→test 全程(baton=full,新系统 + 多 Luban 表)。**git 基线 commit 待自动提交补**。设计基线由 plan 产出(design-docs/16)。

**来由**:xlsx 批次第二刀(放手默认自动接,依赖序:数值底层已完成→道具底层)。原始 spec `C:\Users\pc\Downloads\1002道具底层.xlsx`(全表已提取进 workflow task)。

**本轮范围**:Luban 道具表(Id/Name/Desc/Icon/Quality[1-6]/Type/UseEffect/参数/Stacking/Automatic 等)+ 随机礼包表 + 自选礼包表 + 品质/类型枚举 + 运行期道具注册表(按 id 查)+ 礼包开启逻辑(随机按权重/自选)+ 基础背包容器(计数,叠加上限 999→999+)。UseEffect=1 货币复用刚建的数值系统、=2 图案复用 MergeElement。

**明确不在本轮(spec 自身依赖未建系统)**:限时道具(Term/TermTime/到期补偿 Compensate→Reward 表/CompensateEmail→邮件)→ 邮件/奖励系统未建,本轮 stub 或省;背包溢出→邮件补发(邮件未建)→ 本轮仅容量上限、不发邮件;获取跳转 JumpList(UI 导航)→ 省;icon 真实 Sprite/特效 Light(无美术)→ 资源名占位。

**硬约束**:Luban 定表导表(不硬编码;导表环境见 dev memory:DOTNET_ROLL_FORWARD=Major / c,s 组写法);加法式不破坏核心循环 + 已建系统;现有 EditMode 209 例零回归。

**自主边界(放手默认)**:默认全自主,岔路按默认走记决策日志;唯一停下 = 与 spec 抵触、任务定义硬伤、或 Luban 管线不可用。不 push/build/发布。

**模型档**:plan=opus、dev=opus(多 Luban 表 + 礼包逻辑 + 背包,耦合数值系统)、test=opus(配置加载 + 礼包权重 + 背包边界验证)。

**队列(本轮后接,放手默认按依赖序)**:奖励展示 → 玩家信息 → 邮件/兑换码/排行榜 → TOAST/音乐。

**自治运行登记**:Workflow `pipeline-auto`,runId 待补。

## 最近关单(索引;详情见各 `archive/<任务>/boss.md`)

| 日期 | 任务 | 结论 | 归档 |
|------|------|------|------|
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
