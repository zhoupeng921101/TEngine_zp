# 状态:Boss(编排日志)

> 只记不可推导信息:任务定义、拍板决策、自治授权、打回轮次、关单结论与遗留事项。
> **不记阶段/进度**——恢复时从各 `state/*.md` 交接区现场推导(规则见 `.claude/skills/pipeline/SKILL.md`)。

## 当前任务

### (无活跃任务)

core-loop-completion 已于 2026-06-13 关单 PASS(见下「最近关单」);自治授权随关单失效。

---

### score-element-rm-collect — 消除得分驱动元素生成 + 彻底移除关卡收集 demo(2026-06-12 起单;**已关单 PASS**)

**参与环节**:plan→dev→test 全环节。**不回退 main 改动**,以工程现状(未提交 working tree)为设计基线,定位为对 main 那版的「补充 + 验证」。

**需求(用户拍板,2026-06-12)**:
1. **元素生成 ← 与消除得分关联**(用户修正,替代原「固定比例/消除后才出」):每次落子触发行/列消除 → 按**该次消除得分**生成元素投放待选区,得分越高生成越多;开局/无消除时待选纯方块。**具体「得分→元素数量」映射(线性/档位/上限)授权 plan 设计出方案,用户审**——不在登记里拍。
2. **彻底移除「关卡收集元素玩法」(2026-06-11 collect 切片)**:代码 + 场景 + UI 入口 + 仅其专属资源,**移除干净**。
   - **不碰** merge-order(2026-06-12 合成订单 demo,要保留)。
   - **甄别保留**:① 与 merge-order 共用的「元素由候选块携带」基础设施;`CollectWinWindow`(merge-order 通关复用,见遗留#8)。移除时别牵连这两块。

**设计基线 = 当前未提交工程现状**。main 已直接做过半截(均未提交):
- 已删:`CollectDemoWindow.prefab/.cs`、`Editor/Tests/.../CollectDemoTests.cs`
- 改未删(移除未尽 / 元素改动落点待 plan 核实):`CollectDemo.cs`(核心逻辑还在)、`BlockGameState.cs`、`CollectWinWindow.cs`、`MainMenuWindow.cs`(疑似已撤收集入口)
- 注:同 working tree 还混着 merge-order 已交付但未提交的改动(MergeOrder*),**那是要保留的成品,勿动**。

**运行验证**:dev/test 子会话无 Unity MCP;编译 + EditMode 单测由 boss 补跑(本 boss 会话有 Unity MCP,可直接驱动;否则 batchmode,见 memory/boss.md)。简报已预告,子会话别拿「跑不了」当阻塞。

**关键风险(交 plan 必须解的)**:移除 collect demo 与「保留 merge-order + 元素携带基础设施 + CollectWinWindow」的边界甄别;main 半截移除留下的 `CollectDemo.cs` 等残留要不要清、清到哪。

**plan 产出验收(2026-06-12,PASS)**:设计稿 `design-docs/10-score-element-rm-collect.html`(落盘+挂 index,09 加替换指针)。plan 已 grep 全工程核实需求②已做净(收集专属符号 0 匹配、无悬空引用/孤儿 meta),与保留 merge-order 无硬冲突,可直推 dev。残留 `Collect*` 仅命名误导,本批不改名。

**用户拍板(2026-06-12,3 项待审)**:
1. **得分→元素映射 = A 档(默认平衡)**:`k = clamp(ceil(clearScore/200),1,4)`,无消除→0,积压上限 12。`ScorePerElement=200/Min1/Max4/MaxPendingElements=12`。
2. **投放时机 = 补牌时 FIFO 抽入新候选块**(plan 推荐;消除即时注入列后续 UX 增强)。
3. **共享设施 `Collect*` 命名 = 保留+标注**(plan 推荐;正名列独立低优任务)。

**dev 环节模型**:opus(需求①改状态机核心 MergeOrderState/BlockGameState 补牌逻辑,有耦合)。

**dev 产出验收(2026-06-12,PASS)**:dev 子会话完成,产出全落盘(`BlockScoring.cs` 新增 + 6 改 + collect 删除均在 working tree)。boss 经本会话 Unity MCP 跑运行验证:
- **编译门 PASS**:force refresh + compile,console 0 error,`is_compiling=false`、`ready_for_tools=true`;`BlockScoring.cs.meta` 已自动生成(59B)。
- **EditMode 全量 PASS**:run_tests job `fa3867c646fd46b991f983c8fac7283e` → **96/96 通过、0 失败**(resultState=Passed,与 merge-order 同基线数,无回归)。
- 据此转 test 环节(dev→test)。

**test 环节模型**:opus。理由:全工程无 Classic 计分专用单测(grep 仅命中 MergeOrderTests),「Classic 计分逐数字不变」(dev 标的唯一风险点)不靠单测兜底,须 test 读 `git diff GameWindow.cs` 做纯等价判定——真判断题,往高配。

## 子会话登记

| taskName | childSessionKey | runId | spawn 时间 |
|----------|-----------------|-------|-----------|
| plan-score-element-rm-collect | agent:main:subagent:37b270a7-f184-4349-ae28-f8112e67c323 | fe9c62e6-a084-47af-8d12-3eec94c17f70 | 2026-06-12 |
| dev-score-element-rm-collect | agent:main:subagent:fe59a8cd-828e-4ce4-8a81-59237d575e48 | 2aca8154-15d2-4b53-b25f-15e8161c3b8d | 2026-06-12 |
| test-score-element-rm-collect | agent:main:subagent:b0657719-0912-4a5d-8e5a-8014c83b9fd1 | 8935563a-2b12-4328-8772-4f68e3ce1166 | 2026-06-12 |

## 最近关单

### 2026-06-13 关单:核心玩法补全(core-loop-completion;**自治模式**)
- **结论:PASS 交付**。参与环节 dev→test(plan 前置手动寻址已完成);设计基线 `design-docs/11-core-loop-completion.html`(§十一验收清单 + §十二 8 条拍板)。被测 commit `0bbb7f01`「核心玩法开发」,git 基线 `421a2c12`。
- **打回轮次**:代码侧 0。前序因环境阻塞(Unity MCP 桥未向编辑器注册会话,instance_count:0)连续 4 轮判 BLOCKED,均非代码缺陷;环境恢复后一轮 test-only 通过。
- **运行验证(环境恢复后 test-only 补跑)**:① 编译 0 CS error(read_console 两条 error 为 MCP 桥 disposed 传输日志 + TEngine 框架 `ResourceModuleDriver.Update` 在 Play 模式的运行期 NRE,均非项目改动文件);② EditMode `BlockBlast.Tests` 129/129 全绿(job `9b9f5e8879264d058f9aa951d735a4b1`;129=96 原+33 新,前序「34/130」为差一误记,实有 33 个 `[Test]`);③ Code Review 五条红线全过。Play 拖拽弹字目视留人工(见遗留#11)。
- **实现范围**:设计 11 §十一(B)新增系统加法式落地——连消/多消/全清结算(`ClearSettlement`)、特殊订单双轨、体力/灵力/祈愿/HammerCost、智能生成 R1–R3 仲裁、宝箱、女神、悔棋全量快照扩展;现状(A)路径零改动,96 原单测兜回归。文件:4 新增 + 2 改(`Module/BlockBlast`)+ 1 改(`MergeOrderWindow`)+ 1 新测(`CoreLoopCompletionTests`,33 例)。
- **证据**:`archive/2026-06-13-core-loop-completion/`(test.md 四类逐项 + dev.md 改动清单 + plan.md 设计交接)。
- **模型档**:dev=opus、test=opus(实际收尾为 test-only 一轮)。
- **自治模式机制实测 + 主线脚本回写(2026-06-13)**:本单暴露通用 pipeline-auto 三处缺口,均已回写主线 `.claude/workflows/pipeline-auto.js`(语法校验通过)+ `.claude/skills/pipeline/SKILL.md` 环节裁剪表:
  1. **args 透传**:runner 把对象序列化成字符串送达脚本,脚本侧 `JSON.parse` 兜底回对象(此前作废的 3 次启动即栽在此,改后一次成功)。
  2. **test-only 档**:代码已就绪、仅补运行验证时跳过 plan/dev 直跑 test 一轮;无 dev 在环,验出 FAIL 不自动返修、直接返回 FAIL 交 boss 决定转 dev-test。
  3. **断连容错**:MCP 桥在 Unity 域重载时拆连接,socket close 致 agent 返回前异常退出(本单实例:test 已跑完 129/129、写完报告后断连,agent 返回 null,工作流一度误判 BLOCKED)。dev/test 套一层重试 + test 改「先写报告再返回结构化结果」,重跑 agent 读已写报告复用。
- **决策日志补(收尾会话)**:
  - 用户拍板:对用户呈报语体——默认平实说明文,拟人/口语比喻(死/打死/收口一类)与私造词同属该避免;写入记忆 + conventions.md 第 6 条「语体」+ 收尾自检项。判据同命名规则:对读者是否共享、外人一遍读懂。
  - boss 代决:环境恢复后用 test-only 收尾(非 dev-test),省 dev 空跑一轮;启动前先 unity-check 自检确认桥在线。
- **pipeline-auto 运行登记(本次收尾)**:Run `wf_f9e0c4dc-82e`(Task `wqr3w3930`),test-only 一轮 PASS,1 agent / 约 6.7 万 token / 6 分钟。

### 2026-06-12 关单:Collect* 共享设施正名(collect-rename;**流水线迁移 Claude Code 后首单**)
- **结论:PASS 交付**。dev→test 两个环节(纯重构裁剪环节),打回轮次 0。
- 来源:score-element-rm-collect 拍板第 3 项注册的低优正名任务。9 个符号正名(`CollectElement→MergeElement`、`CollectDemo→MergeElementVisual`、`CollectWinWindow→MergeOrderWinWindow` 含 prefab/location/[Window]、`CollectClearedElements/CollectAt→HarvestClearedElements/HarvestAt` 等),行为零变化。
- **运行验证**:编译 0 error;EditMode `BlockBlast.Tests` 96/96;**Play 寻址链实测通畅**(ShowUIAsync\<MergeOrderWinWindow\> 实例化+CloseUI 回主菜单,截图 `Assets/Screenshots/collect-rename_02_winwindow.png`);三对 .meta git rename 保 GUID;旧符号全工程 0 匹配。
- 证据:`archive/2026-06-12-collect-rename/test.md`(四类全过)+ 同目录 dev.md(改名清单)。
- 模型档:dev=opus、test=opus(寻址链风险,不省档)。spawn 登记:dev `aafa03ff1c55f2800`、test `ad48020494dfd5675`(均已结束)。
- 流水线机制验证(首单顺带):角色卡硬注入、子 agent unityMCP 直跑运行验证(boss 无需补跑,OpenClaw 时代的代跑路径退役)、返回契约三行便条、交接区协议、关单事务——全部如设计运转;打回循环未触发(0 打回),留待后续任务实测。

### 2026-06-12 关单:消除得分驱动元素生成 + 移除收集 demo(score-element-rm-collect)
- **结论:PASS 交付**。plan→dev→test 三个环节走完,打回轮次 0。
- **需求①**:消除得分 → `ElementsForScore(k)`(A 档 `k=clamp(ceil(clearScore/200),1,4)`,无消除 0,积压封顶 12)→ 压 `PendingElements` 队列(轮转均摊)→ 补牌 FIFO 抽干填候选块(队空=纯方块)。旧概率注入/消除门控/保底计数器整套退役。计分公式抽到新增 `BlockScoring.cs`(Classic 与 merge-order 共用单一信息源)。
- **需求②**:收集 demo(prefab/cs/测试)删净,16 个收集专属符号全工程 0 匹配、无悬空引用;保留集(merge-order 元素携带基础设施 + 复用为通关面板的 `CollectWinWindow`)未牵连。
- **文件**:1 新增(`BlockScoring.cs`)+ 6 改(`MergeOrderConfig/State`、`BlockGameState`、`GameWindow`、`MergeOrderWindow`、`MergeOrderTests`)+ collect 删除,全在未提交 working tree。
- **运行验证(boss 经本会话 Unity MCP 补跑)**:编译 0 error、`BlockScoring.cs.meta` 已生成;EditMode 全量 **96/96 绿、0 失败**,无回归。
- **test 独立验收 PASS**:① Classic 计分逐数字不变(读 `git diff GameWindow.cs` 逐表达式判等价,GameWindow 仅 2 行改、落子/消除分均与原内联公式等价)②悔棋回滚队列 / off 零触 / 既有用例全绿 ③新增 7 用例断言均有实质 ④需求②移除净。
- **遗留**:Play 模式拖拽手验(元素随得分目视确认)——MCP 无法模拟指针拖拽,逻辑层已被单测覆盖,留人工目视(见遗留#9)。

### 2026-06-12 关单:元素合成+订单+体力 demo 切片(merge-order-energy)
- **结论:PASS 交付**。plan→dev→test 三个环节走完,打回轮次 0。
- 玩法定义与完整规则见 `design-docs/09-merge-order-energy.html`(核心循环:体力→落子→消除→元素→合成→订单交付→奖励;在 08 收集切片基线上扩展)。
- **运行验证特殊性**:dev/test 子会话均无 Unity MCP,编译+单测由 boss 经 Unity 6000.4.7f1 命令行 batchmode 补跑——编译 0 error、`MergeOrderTests` 19/19 绿、全量 EditMode **96/96 绿**(77 例基线回归无破坏)。证据:`archive/2026-06-12-merge-order-energy/test.md` + `UnityProject/TestResults/merge-order-editmode.xml`(+ `merge-order-run.log`)。Play 手验未执行,列遗留。

  > 后续任务派 dev/test 时即可预告此路径:子会话跑不了 Unity,运行验证统一由 boss batchmode 补跑(编辑器须未占用工程)。
- 拍板记录:
  - 用户拍板(2026-06-12):工程 = Unity 版 TEngine_block;boss 全程自主决策直到功能完成,不回询用户。
  - boss 代决 6 条:体力无变现入口(自然恢复+奖励道具,体力瓶为合成链产物);消除返还部分体力;设计基线 = design-docs/08 + 其实现;glyph/纯色零美术;demo 范围订单闭环优先、锯齿波可简化;掉率保底/体力定档/落错子兜底三数值风险须在设计中给方案。
  - plan 待澄清 5 项全采纳策划建议:自然恢复暂不实装(常量留口子)/悔棋全量单步快照/合成自动配对库存/demo 终点 5 单通关/订单奖励直接给体力(体力瓶列后续)。
  - dev 自裁 4 项 boss 认可:订单池数值按自动配对约束重排/软死亡加可交付自救判定/通关复用 CollectWinWindow 留小瑕疵/悔棋 LIFO 全量快照。
- spawn 登记(均已结束,留档供追溯):
  | taskName | childSessionKey | runId |
  |----------|-----------------|-------|
  | plan-merge-order-energy | agent:main:subagent:fa8810ff-1355-40e0-803e-ddd95d0ebb11 | 9ce27eb9-3020-4373-8c3f-bc4047aaf3b5 |
  | dev-merge-order-energy | agent:main:subagent:8f62fa82-9f99-4319-93aa-4cb6c61bfc3f | a1acc1e2-0851-40b9-b1dd-ab3090c2aac7 |
  | test-merge-order-energy | agent:main:subagent:8b55ac2c-5e31-48a6-91b2-30cee650ae25 | 12b8193c-28c2-4af1-9016-7dc917080d5f |

### 2026-06-11 关单:收集玩法 collect — 独立 demo 切片(方案 A)
- **结论:PASS 交付**。策划→开发→测试三个环节走完,测试四类验证全过(`archive/2026-06-11-collect-demo-slice/test.md`,77/77 单测 + 5 张截图证据,Assets/Screenshots/collect_0*.png)。
- 拍板记录(2026-06-10,§3.3):元素来源=A 纯候选块携带;窗口=A 独立 CollectDemoWindow;失败兜底=复用 GameOver;表现=glyph/纯色。
- 打回轮次:0(一次通过)。
- 本轮 spawn 登记(均已结束,留档供追溯):
  | taskName | childSessionKey | runId |
  |----------|-----------------|-------|
  | plan | agent:main:subagent:89760266-3741-4c59-9021-60fd498268e6 | d404ca2e-bcd5-462a-bb11-e92a33dc3f4b |
  | dev | agent:main:subagent:78903465-02f8-4e19-a268-53710a8ab610 | e13da92b-1008-42b4-bd46-703e4af84a50 |
  | test | agent:main:subagent:d045faa8-fa6d-4139-92b0-e3e8187726e5 | 59d9464d-0e0d-4267-822e-c3bc8d57e006 |

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
