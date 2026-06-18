# 规则审计日志 · 活账本(近期审计 + 改动登记)

> 移出自动注入范围(在 `.claude/rules-archive/`,不在 `.claude/rules/` 递归注入路径内),`/audit` 时按需 Read。注入态只留最小基线指针 `.claude/rules/audit-log.md`(供增量短路 + 手动 /audit 提醒)。更早历史(2026-06-12 首次 ~ 第十次审计完整叙述)见同目录 `audit-log-2026-06.md`。

## 最近审计基线

- 第十次审计 @ commit `5293bb73`(rank 关单,增量):重复/矛盾/死规则均无;信道匹配 #4 首次触发、健康(audit-log 历史归档移出递归注入);无删除候选、无修正候选。carry-forward——第五次(decisions/blockers 判据 3 触点漂移)、第八次(语体指引 conventions/plan 两处)续留监视;死规则累计窗口不足不判。完整叙述见 `audit-log-2026-06.md` 第十次段。

## 2026-06-15 规则栈改动登记(规则4 加注入信道例外指针 + audit-log 压到基线指针,主会话直接改·用户拍板)

- 触发:对 conventions 六条规则做对抗式核验(多视角并行 + 逐条证伪),发现两处真实缺陷,均与「注入信道分层」原则未改到位有关。① 规则4「归档到与该文件同目录的 `archive/`」与 line 18 注入信道旁注互斥:对 `.claude/rules/` 下被递归注入的文件,同目录 `archive/` 子目录照样被注入,须移出 `rules/`。这是「注入信道分层」改动时准入第6条「同次改旧」漏掉了规则4(当时只订正内容分层旁注 / 准入#5 / 审计#4)。磁盘实测佐证:`.claude/rules/archive/` 不存在,`.claude/rules-archive/` 存在,实际落点按 line 18 而非规则4。② audit-log 自身违反 line 18:它是日志(只在 /audit 时读最近一段基线),却以 56 行完整过程叙述常驻注入,占注入规则栈约四成;文件 header 早已声明「只留最近审计段」,实际留了 5 段。
- 改动:① conventions 规则4(line 30)加注入信道例外指针(受注入文件归档须移出 `rules/`,指向「内容分层·注入信道」);② 按 line 18 判据把核磁盘闸登记、第九次审计、注入信道分层登记、历史归档移出登记、第十次审计完整叙述全部移入 `.claude/rules-archive/audit-log-2026-06.md`,活跃文件只留 header + 第十次审计基线指针(commit + 一行结论)+ 本段;③ header 保留策略由「留最近审计段」收紧为「留最近审计的基线指针 + 其后未并入审计的改动登记」(对齐 line 18:audit 内容非每轮相关,常驻只留最小指针),并写明「新审计完成后把上一段叙述移入归档」,使瘦身成为固定步骤、不再次膨胀。
- 反向冲突检查(准入第6条):规则4 例外补全「注入信道分层」段当时漏改的条款,与 line 18 / 准入#5 / 审计#4 方向一致,不新增矛盾。第十次审计结论 #1 曾判「活跃 audit-log 每轮相关、留注入内正确」,未区分「审计基线指针」与「完整过程叙述」;本次把完整叙述移出、活跃只留基线指针,#1 收窄为「仅最小基线指针留注入内,且只在 /audit 时用」。归档「注入信道分层」段那句「规则4 与注入信道非矛盾」基于已被推翻的旧落点,随段移出注入路径,作为冻结历史保留(其后「历史归档移出」段已记录订正),不回改。
- 矛盾/死规则:无。规则4 例外有具体触发实例(本次核验 + 磁盘实测)。
- 处置:加例外指针 + 按 line 18 压到基线指针,非补丁(根在「注入信道分层未传导到规则4 与 audit-log 自身」,本次补到位)。无删除候选。

## 2026-06-15 规则栈改动登记(conventions 核验剩余项·11 条收窄修正,主会话直接改·用户拍板)

- 触发:上段对抗式核验的「部分成立」剩余项,逐条筛(该改/该跳)后改 11 处、跳 6 项,全部落在 conventions.md。
- 改动:① 规则5 自检句对齐旁注真判据(读者共享/望文生义,检索仅辅助信号);② 收尾 lint 标注为正文禁用集的保守子集(撞/串会误命中碰撞/字符串故不入正则,0 命中≠过关);③ 注入路径精确范围单源化(准入#5、审计#4 内联范围改引用 line 18);④ 审计节加旁注「准入(进门把关)与审计(存量复查)分工不重合、不可合并」;⑤ 准入#3 加复发性自检(一次性事故不立常驻规则);⑥ 审计加 #5 净增趋势(观察非硬上限)、#6 孤儿旁注(规范增删时查);⑦ 适用声明界定代码注释边界(纳入但规则2 禁指代豁免;生成物/二进制排除);⑧ 收尾自检各项加指向源编号;⑨ 内容分层加归层裁决旁注(覆盖式真丢历史→须就地);⑩ 交叉检散件自查由「隔会话再自查」(无扳机)改为可推导留痕(commit message 注明已跑 lint);⑪ 准入#1 行为探针改为高争议时备用手段、非每次必跑(如实反映 10 轮零执行)。
- 跳过(报备,均核验阶段判现状更优或会反噬):规则2/3 合并(序号被冻结归档引用,合并有害)、规则1 主句次序(被周边落盘要求兜住、非真风险)、所有权条款(单人项目暂无大碍,协作者增多再补)、四套清单边界(本就清晰、无问题)、规则6 机械检验/遵从度度量(残值并入散件留痕项 ⑩,无独立改)。
- 反向冲突检查(准入第6条):11 处中 10 处为加法/澄清,1 处(准入#1 探针降级)改写既有条款、无孤儿;与 line 18 / 准入#5 / 审计#4 方向一致,无新矛盾。
- 处置:加法+澄清+一处降级,无删除候选。

## 2026-06-15 规则栈改动登记(流水线瘦身后引用图修复 + 关单/audit 触发同步删,主会话直接改·用户拍板)

- 触发:用户对流水线规则栈做大幅瘦身(编码红线搬 tengine-dev/SKILL、强制工作流抽出 conventions-dev、规则审计搬 audit/SKILL、conventions 六条缩五条、删模型选档表与 @角色手动寻址)。对抗式核验(4 维度并行 + 逐条复核,22 agent)发现一批「搬走/删定义但引用未同步」缺陷,根因同准入#5「同次改旧」漏执行。
- 改动:① pipeline-test.md 编码红线引用 CLAUDE→tengine-dev/SKILL「核心红线」并补防漂移旁注;② pipeline-dev.md 补回红线直达指针(指 tengine-dev/SKILL「核心红线」);③ audit/SKILL line3+8 删「pipeline 关单触发」声明(用户选「同步删」:与关单事务已删的巡检步对齐,审计改手动 /audit only),line17 悬空引用改「按上方三样查」;④ conventions 规则编号订正三处(line7 语体 3→5、line36 副本 2→3、line37 比喻 3→5)+ 交叉检/收尾的「lint」措辞改「收尾必做自检」(grep lint 命令上一批已删)+「关单 /audit」改「手动 /audit」;⑤ pipeline/SKILL description `/pipeline auto`→`/pipeline-auto`、两处交叉检 lint 措辞同步;⑥ audit-log header 去「conventions line 18 / 规则4 注入信道例外」悬空指针,注入分层 + 归档移出 rules 的理由内联自包含;⑦ pipeline-auto.js line20 去「按 SKILL.md 选档表传」悬空注释,改述 frontmatter 默认 + 可选覆盖。
- 反向冲突检查(准入第5条):全为订正/澄清,无新增矛盾;关单/audit 触发由「双向声明」收敛为「audit 手动 only」,两侧契约一致。被证伪的两项预判(plan 变现红线「丢失」实由 design-docs 立项表保留;test=sonnet「能力不足」实可经 args.testModel 上调)未改,如实留作复核记录。
- 处置:订正+澄清,无删除候选。

## 第十一次审计 @ 工作树(基线 commit `5293bb73`,流水线大重构 + 引用修复未提交)

三样 + 信道/净增核查(4 维度并行 + 对抗复核 9 agent)。增量短路不适用:自基线规则栈大改(整套流水线瘦身 + 本会话引用修复)。

- 修正(已改·悬空引用,与本会话引用修复同类):`pipeline/memory/plan.md:15`「conventions 规则6」→「规则5「语体」」(语体规则六缩五的编号漂移);`.claude/memory/README.md:3`「CLAUDE.md「自我优化机制」」→「conventions-dev.md」(该节已搬家)。
- 删除候选:无。
- 证伪(发现不成立):`effort`/`memory`/`color` 均 harness 有效字段(claude-code-guide 实证官方 sub-agents.md,effort∈low/medium/high/xhigh/max),非死字段;L1「跳过查询」不与红线冲突(碰框架API/资源/事件的改动按判级必非 L1,红线可达);model 写死 vs args 覆盖是有意逃生舱(已落盘)。
- 已处置(用户「全做」三项):① audit/SKILL 补回 #4 信道匹配查项 + #5 净增趋势/#6 孤儿旁注各一行,与基线期待对齐;② conventions-dev.md(85 行 dev 专属)`git mv` 出注入路径 → `.claude/skills/tengine-dev/conventions-dev.md`(pipeline-dev.md:16 引用按需 Read),并入审计范围;audit-log 收为注入态最小 stub(`.claude/rules/audit-log.md`)+ 活账本移本文件(rules-archive);③ CLAUDE.md「回复风格」补一行呈报语体(对用户呈报用平实说明文、按对话体重组,不照搬持久文件原文)。
- 净增趋势(#5 续记):处置后注入态 `.claude/rules/` ≈ 51 行(conventions 43 + audit-log stub ~8),较裁决前 161 行降约 110;降幅来自 conventions-dev(85,移 skills)+ audit-log 活账本(~40,移本文件)退出注入。
- carry-forward:第八次(语体指引)本轮编号订正已对齐、结案;第五次(blockers 判据 3 触点)当前工作树已收敛(plan/auto 一致)、结案。
- 关单事务:本次审计覆盖上方三段 2026-06-15 改动登记;按 header 策略已随本次结构调整全部移入本活账本(rules-archive),注入态只留 stub 基线指针。

## 第十二次审计 @ 工作树(增量基线 commit `9ac4eddf`,第十一次审计工作落地后)

增量基线取第十一次审计工作落地的 commit `9ac4eddf`(conventions-dev git mv + audit-log stub 均在此),而非注入指针记的 `5293bb73`(那是第十一次审计的工作树基线,其后大重构已提交、不可再当增量起点)。审 `9ac4eddf..HEAD`(`3c775755 优化agents` / `6bea4682 玩法融合`)+ 工作树未提交(本会话 pipeline-plan 粒度边界)。合并提交 `a1dcf15e` 经逐文件核:未触及任何我方治理规则文件(只引入 luban-dev/caveman/grill-me/grill-with-docs/improve-codebase-architecture/openspec-*/wiki-synchelper 等外部 skill 文档),排除出审计核心。

新改动范围(我方治理规则):
- `3c775755`:① 规则准入 5 问从 CLAUDE.md 整段移入 conventions.md(grep 核实仅存 conventions 一处,移动非复制);② CLAUDE.md 回复风格收为一行「结构化优先·多用图表」,删「请使用中文 / 简洁直接 / 平实呈报」三句;③ pipeline-plan 图示化两点由详细 SVG 风格规约缩为简版;④ pipeline-dev `effort: max→xhigh`。
- `6bea4682`:memory/dev.md +2、memory/test.md ±1,均经验沉淀(gameplay-fusion / fusion-test 先例),无规范条款变更。
- 工作树(本会话):pipeline-plan 职责 +#5「设计稿粒度」(答需求·不答代码实现)+ 章节骨架「dev 改动清单→接缝清单」;memory/plan.md:8「精确到方法名→点到现有方法名(接缝)」。

查项结论(1-3 必查):
- **重复**:无。规则准入经 grep 仅存 conventions.md;pipeline-plan #5(文档粒度)与 #4(不碰 Unity 工程)跨层互补(内容粒度 vs 工具动作边界),非同层重复。
- **死规则**:无新增。规则准入 5 问本轮即被 #5 准入实际调用;memory 各条带具体先例。第十一次 carry-forward(第五 / 第八次)已结案,无续留监视项。
- **矛盾**:无。#5 / 接缝清单 / memory:8 三处互相一致,且与 dev「自做实现设计」职责一致。观察一项(非矛盾):CLAUDE.md「多用图表」为无条件指令,触及规则准入 #4 反噬式样(无条件→当默认动作),但句尾含「段落等」已软化、系用户本次有意提交,记观察不作处置;平实呈报指引现仅存用户级 memory + 全局 CLAUDE「详细带解释」,与项目 CLAUDE 不在同一信道、不构成冲突。

条件查项(4-6):
- **#4 信道匹配**:跳过。`.claude/rules/` 注入文件集无增减(规则准入移入已注入的 conventions.md,非新文件进出)。
- **#5 净增趋势**:`.claude/rules/` 58 行(conventions 53 + audit-log stub 5),较第十一次 ≈51 净增 7;增量全系规则准入由 CLAUDE.md 迁入(CLAUDE.md 同步缩约 17 行),真实常驻新增 ≈0,不升「总量复查」。
- **#6 孤儿旁注**:无。规则准入 #2 的 `>` 旁注(「敢反对 vs 先独立成判」例)随段整体迁入 conventions.md、父规范在位;CLAUDE.md 删除段无遗留旁注;本会话编辑未引入 / 遗留旁注。

删除候选:无。修正候选:无(本会话编辑三处已自洽)。下次增量以本会话编辑提交后 commit 为基线。

## 第十三次审计 @ 工作树(增量基线 commit `9ac4eddf`,本会话「dev 可行性预检通道 + plan 三自检」固化未提交)

增量基线沿用第十二次的 `9ac4eddf`(其后第十二次审计的工作树改动已并入提交)。本次规则栈改动全系本会话固化「dev 早期可行性预检」通道 + plan 侧三自检,落在 `.claude/agents/pipeline-plan.md`、`.claude/agents/pipeline-dev.md`、`.claude/skills/pipeline/SKILL.md`(`.claude/workflows/pipeline-auto.js` 是脚本、不在规则栈散文范围,作为 SKILL 旁注所述自治预检 stage 的实现,一并核引用一致性)。

新增条款:
- pipeline-plan:红线 +`feasibilityCheck`(接法存疑的未实现链路,转 full dev 前要 dev 只读预检)、返回契约 +⑤、新增「设计自检」节(整局走查 + 向上对体验)、规则6 旁注加「影响半径清单」工具化落点。
- pipeline-dev:新增「可行性预检模式」(只读评估、不实现、不进交接区)。
- pipeline/SKILL:新增「可行性预检」编排节(常规 boss 动作)+ 自治旁注(指向 pipeline-auto workflow 的同款 stage)。

查项结论(1-3 必查):
- **重复**:无。`feasibilityCheck` 与 `taskFlaw`(需求/基线硬伤,实现前)、`designFlaw`(实现中发现设计错)、plan grep「证符号存在」分属不同时点/不同层(接法可行 vs 符号存在),互补非复制;整局走查(动态跑一局查接缝)≠ 接缝清单(静态列符号);向上对体验为新自检;影响半径是 conventions 规则6「同步他篇」的方法化落点(列受影响项 + 标已/待同步),在 plan 卡内细化「怎么做」、不复述规则6 的「做什么」,加法非同层重复。
- **死规则**:无删除(单窗口不判死)。carry-forward 监视一项:**plan「向上对体验」自检**暂无项目内具体触发实例(feasibilityCheck 有 23§五 GameContext 接法换皮才补、整局走查有 29 三处跨模式隐患融合才现、影响半径有 09/11 库存口径分歧为实例;向上对体验系通用设计质量原则,无落档失败案例),累计窗口续观察。
- **矛盾**:无。① 自治预检 doc/impl 一致:SKILL 旁注「自治走同款 stage」与 pipeline-auto.js 新增 feasibility stage(plan.feasibilityCheck 非空 → spawn dev 预检 → 不可行 BLOCKED stage=feasibility 带替代接法)对齐,本次同改、无漂移(主动规避第十次「audit-log 双向声明」类 doc/impl 漂移);② feasibilityCheck(技术接法可行)与 plan 既有范围开关 decisions/blockers 分流(方向/spec 取舍)正交,不同轴不冲突;③ 三文件 feasibilityCheck 描述一致。观察一项(非矛盾,报备不强改):plan 卡「grep 核实存在——证可行性」措辞在 feasibilityCheck 加入后成为「两级可行性」的存在级与接法级并存,「证可行性」读来略宽但不为假(存在是可行性证据之一);可选收紧为「证存在(基础可行性)」,属措辞精度非错误,留用户裁。

条件查项(4-6):
- **#4 信道匹配**:跳过。`.claude/rules/` 注入文件集无增减(本次只改 `.claude/agents/` 与 `.claude/skills/`,均非 `.claude/rules/` 递归注入路径内文件)。
- **#5 净增趋势**:注入态 `.claude/rules/` 行数不变(本次未触 rules/)。散文增量落在 agent 卡 + SKILL(非注入态计入项):pipeline-plan ≈+6 行、pipeline-dev ≈+2、SKILL ≈+10;均按需注入(spawn / skill-load),不占每窗口常驻,不升「总量复查」。
- **#6 孤儿旁注**:无。本次就地覆盖两处(SKILL 自治旁注「不走预检」→「走同款 stage」、plan feasibilityCheck 红线「常规/自治分叉」→「两模式统一」),均原地重写、无遗留旧正文或孤儿旁注;新增 SKILL「可行性预检」节的 `>` 旁注父节在位。

删除候选:无。修正候选:无(line 32 措辞收紧列为可选观察,非必改)。下次增量以本会话编辑提交后 commit 为基线。

## 第十四次审计 @ 工作树(增量基线 = 第十三次固化 commit `e7b8cd1b`,本会话「自治流水线提示词优化」未提交)

标准基线指针仍记 `9ac4eddf`(第十二/十三次沿用),已严重滞后——`9ac4eddf..HEAD` 含大批中间提交(caveman / grill-me / luban-dev 重写 / openspec / wiki-synchelper 删除 / conventions / CLAUDE 等),非本增量。本次以第十三次固化提交 `e7b8cd1b`(可行性预检落地)为真实增量起点,审 `e7b8cd1b..HEAD` 触及规则栈的提交 + 工作树未提交。增量经核仅:① 提交 `92251a59`(plan 卡「文档表现」HTML→Markdown 重写)② 提交 `a82d3d82`(memory/plan.md)③ 本会话工作树(自治优化)。中间 `80072efe`/`7952ae4e` 只动 design-docs,非规则栈。

本会话改动(自治流水线「投资取证三段阶梯 + 续接整个 backlog」,用户拍板范围=续接链 + 激进自治):
- pipeline/SKILL「自治模式」节重写:二元决策 → 三段阶梯(有默认即取 / 无默认先调查取证据证拍板 / 仅「调查仍无解 且 不可逆 且 抵触 GDD 原文」入 BLOCKED);加「续接循环」(挑增量→workflow→关单→链式 checkpoint commit→续接,终止=目标达成/硬阻塞/安全上限 N=6)+ 决策日志三元组(选择+依据+可逆性标签);关单事务第4步加链式不逐增量回报。
- pipeline-plan 红线 + 旁注:二元 → 三段阶梯(同 SKILL 口径)。
- pipeline-dev 红线:designFlaw 上报前先取证确认「确是设计错·非实现层可绕」。
- `.claude/workflows/pipeline-auto.js`(脚本、不在规则栈散文范围,作 SKILL 实现一并核引用一致):RETURN_NOTE 三段阶梯、PLAN_SCHEMA.blockers 门槛收窄、熔断分支加只读根因诊断(DIAGNOSIS_SCHEMA);`node --check`(运行时 async 包裹)语法过。

查项结论(1-3 必查):
- **重复**:无。三段阶梯在 SKILL 决策规则 / plan 红线 / workflow RETURN_NOTE 三处并存,系多 agent 提示词架构固有(各 consumer 只见自己的 system prompt,boss 与 plan 卡互不可见,须各自自包含);SKILL line112 已以「(各角色在环节内执行,见 agent 卡;boss 同此)」做跨引而非全量复述。此为旧二元规则既有的并列副本同步更新(§反向冲突要求同次改完),非新增重复。feasibilityCheck 仍是窄实例(未实现链路接法),三段阶梯把「先调查再决」泛化到一切不确定,互补。
- **死规则**:无删除。三段阶梯有具体触发(2026-06 三次范围开关误报 blocker 空停一轮 + 用户 2026-06-17 明示诉求),复发性结构问题非一次性。carry-forward 续监视:第十三次「plan 向上对体验自检」仍无项目内触发实例,累计窗口续观察。
- **矛盾**:无。① 三段阶梯的 BLOCKED(设计选择级·取证后·不可逆+抵触 GDD)与 SKILL 其余 BLOCKED 引用分属不同类别——环境型(line55)/ 熔断(line58)/ designFlaw(line85)/ taskFlaw(line86)/ 环节判不准(line88),各自触发条件不交叠,口径一致。② doc/impl 锁步无漂移:SKILL 决策规则 ↔ workflow RETURN_NOTE ↔ plan 红线 ↔ dev designFlaw 四处门槛措辞统一(「调查也定不了 且 不可逆 且 抵触 GDD 原文」),熔断诊断(workflow)↔ SKILL line58 一致(主动规避第十次 doc/impl 漂移式样)。③ `92251a59` plan 卡「文档表现」由「HTML 单源 + 手写内联 SVG」覆盖式重写到「Markdown 正文 + index.html 外壳运行时渲染 + mermaid/SVG」,合 conventions 规则6(过时即重写到现状),卡内自洽、与 design-docs 现实(已迁 Markdown)一致,无矛盾。

**观察(用户已确认有意取消跟踪,非回归)**:用户手动把 `pipeline/` 部分文件移出版本控制,视其为本地临时工作区(根级 `.gitignore` 规则 `UnityProject/pipeline/` 兜底 ignore)。实测 HEAD 现状半跟踪、不一致:`archive/`(~30 已结案任务记录)、`README.md`、`memory/{boss,dev,test}.md`、`state/{dev,test}.md` 仍 tracked;`memory/plan.md`、`state/{boss,plan}.md` 已移出。durability 分层已向用户呈报:`state/` 真临时(关单即重置,丢之无害);`memory/`(跨任务沉淀)与 `archive/`(结案设计依据 + 测试证据)durable。用户拍板**全取消**,本会话执行 `git rm --cached -r pipeline/`(commit `8c22e95d`,94 文件移出索引、本地保留);pipeline/ 成纯本地工作区。**审计机制后果**:`pipeline/memory/*.md` 头准入仍在审计范围,但已脱离 git → 后续审计对 memory 头的增量须本地直读、不能靠 git-diff 短路(头准入稳定、改动罕见,影响小)。非规则内容矛盾,不入删除/修正候选。

条件查项(4-6):
- **#4 信道匹配**:跳过。`.claude/rules/` 注入文件集无增减(本次只改 `.claude/agents/`、`.claude/skills/`、`.claude/workflows/`)。
- **#5 净增趋势**:注入态 `.claude/rules/` 行数不变(未触 rules/)。散文增量(非注入态):SKILL ≈+12 行、plan ≈±0(覆盖式)、dev ≈±0(覆盖式);workflow +~18(脚本非注入)。不升「总量复查」。
- **#6 孤儿旁注**:无。plan 红线旁注(2026-06 三次空停)就地更新到新门槛「取证后仍无解的不可逆 GDD 冲突」,与上方新规则一致,无孤儿;SKILL 重写无遗留旧正文。

删除候选:无。修正候选:无(pipeline/ 跟踪取向 = 用户流程偏好,非规则栈内容订正;现状半跟踪不一致,取向待用户定后由用户执行 git 操作)。**下次增量基线应从本会话提交后 commit 起,并把注入态指针从滞后的 `9ac4eddf` 推进过来**(9ac4eddf 已隔大批中间提交,继续沿用会使增量短路失真)。

## 第十五次审计 @ 工作树→已提交(增量基线 = 第十四次审至的 commit `f36a96a1`,design-docs 大重构收尾 + 角色卡同步)

触发:手动 `/audit`。增量自第十四次(审至 `f36a96a1` 工作树)起。本会话 design-docs SVG→mermaid + ①②③ 杠杆重构(commits `59c53699`/`3a2c6474`/`ce417548`/`0521b0dc`/`4766ba67`)属**文档正文、非规则栈,不入审计范围**;规则栈实改两笔:

- **`6834c1d9`(并行 pipeline-auto 会话)**:失败模式纵深贯穿 plan/dev/test/boss 四角色 + 需求降层 a/b/c + 范围三档(核心/增强/可砍)+ 熔断前只读根因诊断 + 「多用图表」无条件→条件触发。
- **`34692918`(本会话)**:plan 卡「文档表现(版式)」对齐 design-docs 重构现状——强调 `<b>`→markdown `**`、callout→GFM `> [!…]` 块、简单独立表→md 表、新增「GFM 块前后须空行」规约。

查项:
- **重复**:无。6834c1d9「崩法」分摊四角色系有意角色切分(plan 写崩法 / dev 手验异常路径 / test 越界试探 / boss 根因诊断),同第十四次「三段阶梯并列副本」模式、非冗余;34692918 单源 plan 卡。
- **矛盾**:无。6834c1d9「图表无条件→条件触发」是健康反噬修正(消无条件指令);34692918 覆盖式重写旧「CJK `**`/`*` 不可靠、遇「」失效」条款——经 marked v15 实测证伪(10 情形仅「强调内容紧贴 `「」` 标点」1 种失效,余皆可用),收窄为「`**` 默认可靠、唯紧贴 CJK 括号时退 `<b>`」;callout/表格新条款与 nav.js 渲染钩子(commit `0521b0dc` 实装的 blockquote→callout)一致;grep 规则栈确认无他篇引用旧约定。
- **死规则**:无,均有触发场景。
- **#4 信道匹配**:跳过(无文件进/出 `.claude/rules/` 注入路径,改在 `.claude/agents/` 与 plan 卡)。
- **#5 净增趋势**:注入态 `.claude/rules/` 行数不变(未触 rules/)。agents/ 角色卡散文增(spawn 态、非每轮注入),不升总量复查。
- **#6 孤儿旁注**:无。34692918 替换的强调条款原无 `>` 旁注;6834c1d9 新增为行内括注、无 `>` 旁注孤儿。

元观察(供准入#1 复盘):旧「CJK `**` 不可靠」是**未经行为探针、长期沿用的过宽事实条款**,本会话首次实测 marked v15 才证伪并收窄——印证准入#1「真争议事实条款值得跑探针」,该条款现已实测落地。
carry-forward:plan「向上对体验」自检仍无触发实例(续监视);**新增监视**——后续设计文档若仍用旧 `<b>` 次级强调 / `<div class="callout">` 简单框写作,即 plan 卡同步未传导。删除候选:无。下次增量以 `34692918` 为基线。

## 第十六次审计 @ 工作树(增量基线 = 第十五次审至 commit `34692918`,UI 环节落地 + 角色卡写作标尺接入未提交)

触发:手动 `/audit`。审 `34692918..HEAD` 触及规则栈的提交 + 工作树未提交。基线正确性已核:`6834c1d9`(第十五次审过)经 `merge-base --is-ancestor` 确为 `34692918` 祖先,无漏审/双审。`ffbd5659`(第十五次落账)仅动 audit-log.md 基线指针,审计自身记账、非行为规则。

自基线以来规则栈实改:
- **`e479e403`(流水管线添加 UI 环节)**:新增角色卡 `pipeline-ui.md`(183 行,UI 制作 = plan 与 dev 间的 Prefab 生产环节)+ `pipeline/SKILL.md` 全面集成 ui 环节(三→四执行体、baton 序列 plan→[ui→]dev→test 全处统一、环节裁剪表加 UI 行、archive 四件套加 ui.md、ui 交接语义/跳过 ui 声明 UI 基线)+ `html-to-ugui/SKILL.md` 命名格式(8→9 种控件、`data-u-name` 改 TEngine 前缀 `m_{前缀}_{PascalCase}`)+ 新建 `pipeline/memory/ui.md`。
- **`bb3034f8`**:`pipeline-plan.md` 加「design-docs 正文分层」条款 + 旁注(迭代范围/取舍/延后项属工作状态层、不织进规范正文;用具体阶段名不用「本轮」)。
- **本会话未提交**:`audit/SKILL.md` 新增条件查项 7「条款可执行性」+ 同步两处计数;`pipeline/SKILL.md` 加角色卡写作标尺入站指针;新建 `pipeline/references/agent-card-authoring.md`(references 层、不注入、不在审计范围)。

查项结论(1-3 必查):
- **重复**:无。pipeline-ui.md 与他卡共享结构性样板(「写持久文件前遵守 conventions」「返回契约」「收尾沉淀 memory」)系多 agent 提示词固有并列引用(各 consumer 只见自身 system prompt,须自包含),同第十四/十五次并列副本模式、非冗余。本会话查项 7 与查项 2「死规则」分属不同镜头(死规则查「是否触发」,查项 7 查「触发后 动作+产出物 是否可执行可验收」——形容词条款可持续触发却不可执行,死规则抓不到),互补非重复;标尺文档双入站指针(pipeline/SKILL 写卡时 + audit/SKILL 审卡时)对应两个不同触发时点,非同一行为两源。
- **矛盾**:无。① UI 集成内部一致:baton 序列在编排流程/环节裁剪/archive/恢复协议全处同步更新为 plan→[ui→]dev→test;② pipeline-ui.md 描述「用于含新 UI 窗口/复杂 UI 改动」与 SKILL「ui 可选——仅含新 UI 窗口/复杂 UI 改动时启用」一致;③ html-to-ugui「9 种控件」与 pipeline-ui.md Step1 控件清单(div/image/text/button/input/scroll/toggle/slider/dropdown = 9)一致(跨卡同步,非冲突);④ 本会话查项 7 不与查项 1-6 交叠。
- **死规则**:无。新条款均有触发——ui 环节条款于 UI 任务触发、design-docs 分层于 plan 写文档时触发(频繁)、查项 7 于角色卡改动时触发(本次审计即其首个触发实例)。carry-forward:plan「向上对体验」自检仍无项目内触发实例(第十三次起续监视,单窗口不判死);第十五次「设计文档旧 `<b>`/`<div class="callout">` 风格 = plan 卡同步未传导」属 design-docs 内容监视、非规则栈,本次未扫 design-docs 正文,续留。

条件查项(4-7):
- **#4 信道匹配**:核过。新文件均落对信道——`pipeline-ui.md` 入 `.claude/agents/`(角色卡,spawn 态注入、非每窗,正确);`agent-card-authoring.md` + `html-to-ugui/references/*` 入 references/(按需读、不注入,正确)。无错置。
- **#5 净增趋势**:注入态 `.claude/rules/` ≈ 不变(仅 audit-log.md stub 基线指针更新,conventions.md 未触)。pipeline-ui.md +183 系 spawn 态(仅 ui agent 启用时加载),不占每窗口常驻注入预算,不升总量复查。
- **#6 孤儿旁注**:无。pipeline/SKILL.md 就地重写(三→四执行体、baton 序列、环节裁剪表)保留旁注「> 没有基线锚…」父节在位;新增旁注(ui 交接、design-docs 分层、本会话标尺指针)均父节在位;无删除致孤儿。
- **#7 条款可执行性**(首次触发):过。pipeline-ui.md 多为程序性条款(工具链五步、具体 MCP 调用),判断型条款(红线:API 不可达→降级纯色 + 交接区标明;MCP 断连→暂停但产出 Step1-2 + 补跑命令清单;命名前缀逐项对照自检;素材失败→换 seed 重试 1 次后降级)三要素(触发+动作+可核对产出物)均齐,无形容词冒充条款、无 taste 伪装成规则;pipeline-plan.md「design-docs 分层」条款三要素齐(触发=写 design-docs 正文、动作=迭代范围归工作状态层节、产出物=正文符合分层)、具体非形容词。两张改动卡均通过标尺。

memory/ui.md 头准入:本地直读核过(脱离 git-diff 短路,第十四次起须直读)——头为标准准入式样(「只记跨任务可复用且 agent 定义/设计文档/CLAUDE/references 未覆盖的经验」),正文「(暂无)」空,无死规则无孤儿。

自审声明(交叉检最弱点):本会话查项 7 + pipeline/SKILL 指针系审计者本人所改、自评 clean,blast radius 低(条件查 + 引用指针),留下次独立审计复核。

删除候选:无。改写候选:无(pipeline-ui.md 经查项 7 clean)。下次增量以本会话(第十六次落账 + 查项 7 接入 + UI 环节)提交后 commit 为基线。

## 第十七次审计 @ 工作树(增量基线 = 第十六次落账 commit `d8618184`,「设计-代码解耦」并发提交 + pipeline-ui 素材流程修订)

触发:手动 `/audit`。增量自第十六次落账 `d8618184`(标尺文档 + 查项 7 + 落账,核过内容无损)起。审 `d8618184..HEAD` 触及规则栈的提交 + 工作树未提交。

**并发观察(框架级,供用户决策)**:本次审计期间工作树被并发会话改动——HEAD 自第十六次审计时的 `bb3034f8` 推到 `3f2494ce`(6 提交);第十六次本会话未提交工作已被并发提交为 `d8618184`;`pipeline-ui.md` 又被改、未提交。同记忆「concurrent-pipeline-auto-resets-worktree」**第二次命中**,这次命中的是审计流程本身:增量基线在两次审计间被并发推移,且本次落账(未提交)有被并发 git 清理冲掉的风险。根因 = 单一共享 git 工作树同时承载并发自治 git 操作 + 依赖稳定基线的审计。建议升级隔离(并发 pipeline-auto 走独立 git worktree/分支)或串行(pipeline-auto 活动时不跑 /audit)。非规则内容矛盾,不入删除候选,记观察。

自基线以来规则栈实改(并发提交 `3f2494ce`「设计-代码解耦:plan 角色 code-blind」+ 未提交 pipeline-ui):
- **conventions.md +1**:规则3 加「design-docs 专项·与代码解耦」子条款(设计稿不写代码符号/路径/file:line/接缝清单/dev 改动清单/引用代码的设计基线;例外=代码架构文档、UI 实现接线稿)。
- **pipeline-plan.md 重写**:职责#5「不读工程代码、不 grep 符号」、输入「不读工程源码」、产出「行为级完成定义、不含代码定位」、章节骨架**删「接缝清单」**+「正文不写代码符号」、红线**删 feasibilityCheck**+加「设计稿正文不含代码」、返回契约**删⑤ feasibilityCheck**、整局走查「系统接缝」→「机制」。
- **pipeline-dev.md +8**:输入 design-docs = code-free 设计意图 + 旁注「dev 自行读工程映射接缝、代码是 dev 单一事实源」;可行性预检模式触发由「plan feasibilityCheck」泛化为「boss 高风险/接法存疑任务」。
- **pipeline/SKILL.md +13**:「可行性预检」节从「plan 上报 feasibilityCheck 时」重写为「boss 判高风险/接法存疑时」,删自治 stage 旁注。
- **pipeline-ui.md(未提交)+14**:Step 4 素材流程 curl→PowerShell `Invoke-RestMethod`、Flux 默认 Schnell、加实测「已知限制」(RGB 无透明通道 / 长宽比不精确)+ 对策。

查项结论(1-3 必查):
- **重复**:无(候选)。解耦原则现于 conventions 规则3(源)+ plan 卡三处(职责#5/章节骨架/红线)+ dev 输入旁注 + SKILL 可行性预检节;系多 agent 并列副本模式(同第十四/十五次),四处同提交 `3f2494ce` 同步,plan 红线/dev 旁注均带「见 conventions」源指针。**观察(非候选)**:plan 红线「设计稿正文不含代码…」与 conventions 子条款近逐字重叠,是本轮最重副本、最大漂移点——后续改 conventions 须连带 plan 卡同步(§反向冲突)。
- **矛盾**:解耦四文件内部自洽(SKILL 可行性预检节已正确改 boss 触发、删 plan.feasibilityCheck 依赖);conventions 解耦子条款的例外(UI 实现接线稿)正确豁免 pipeline-ui.md(含 `m_` 前缀/C# 路径 = UI 实现接线),不与 ui 卡冲突。**发现 1 处 doc/impl 漂移(改写候选)**:`3f2494ce` 删 plan feasibilityCheck + SKILL 自治 stage 旁注,但**未同步 `.claude/workflows/pipeline-auto.js`**——其 `PLAN_SCHEMA.feasibilityCheck`(行 36)+ feasibility stage(行 126-143,`if (plan.feasibilityCheck…)` → spawn dev 预检 → `BLOCKED stage=feasibility`)仍依赖 plan 产 feasibilityCheck。后果:① 经 plan 触发的 stage 路径已死(plan 卡不再产该字段);② schema 字段描述**反向要求 plan 产出代码接缝信息**,与 plan 新 code-blind 强制冲突(自治模式 plan agent 收冲突指令)。属 conventions 规则6「改一篇须同步他篇」在并发改动中漏执行,同第十三/十四次警惕的 doc/impl 漂移式样。(pipeline-auto.js 非规则栈散文范围,作 SKILL 实现一并核;此处 SKILL 旁注已删对它的引用,漂移落在 脚本↔plan 卡 之间。)
- **死规则**:无(候选)。新条款均可触发(解耦于 plan 写文档时、dev 映射接缝于每 dev 任务、ui Step4 于素材生成)。移除项:plan feasibilityCheck 整体删——由此暴露上方 pipeline-auto.js 残留。carry-forward:plan「向上对体验」自检仍无项目内触发实例(第十三次起续监视)。

条件查项(4-7):
- **#4 信道匹配**:本增量无文件进出注入路径(改在已就位的 conventions/agents/skills;`d8618184` 把标尺文档落 references/ 已第十六次核过)。无变。
- **#5 净增趋势**:注入态 `.claude/rules/` +1 行(conventions 解耦子条款,justified)+ audit-log stub。大改在 agents(spawn 态)/design-docs(非规则栈)。不升总量复查。
- **#6 孤儿旁注**:无。plan 删接缝清单/feasibilityCheck 系自包含删除、无遗留孤儿;「范围开关塞 blockers」旁注父节(三段阶梯)在位;SKILL 可行性预检节旁注就地更新;conventions +1 为加法。**跨文件**:标尺文档 `agent-card-authoring.md` grep 确认无「接缝清单/feasibilityCheck/接缝」引用,其所引「设计稿章节骨架」「设计自检」节与「崩法/降层/三档」条款在 `3f2494ce` 重写后全部存活——解耦未孤儿化标尺文档。
- **#7 条款可执行性**(agents 改动):过。plan 重写后判断条款(需求降层/崩法/范围三档/整局走查/向上对体验)三要素仍齐;解耦新增为具体禁止(不写代码符号/接缝)、非形容词;dev 预检模式 + 「自行映射接缝」旁注三要素齐;pipeline-ui Step4 程序性修订,降级判断条款三要素齐、已知限制为实测事实 + 具体对策、非形容词。三张改动卡均通过标尺。

改写候选(报用户拍板,不静默改):pipeline-auto.js feasibilityCheck 残留(见查项3·矛盾)。选项:(a)删 `PLAN_SCHEMA.feasibilityCheck` 字段 + feasibility stage——自治模式失去早期可行性预检,需确认可接受;(b)保留 stage 但改触发(不读 plan.feasibilityCheck,改由 workflow/boss 级启发式或 args 触发,对齐 SKILL「boss 判高风险」的自治等价)。属「自治模式是否保留可行性预检及如何触发」的设计决策,留用户定;且 pipeline-auto.js 正被并发改动,定后由稳定态执行。

删除候选:无。下次增量以本会话(第十七次落账)提交后 commit 为基线;因并发工作树漂移,若基线期间被 reset,下次须重新核 HEAD 定基线。

## 第十八次审计 @ 工作树(增量基线 = 第十七次审过 commit `3f2494ce`,服务端流水线接入 + pipeline-ui 重写补审)

触发:手动 `/audit`。第十七次落账(audit-log)仍未提交(工作树 `M`),叙述已在本活账本。git 重核 HEAD=`0b41dc3d`,增量自 `3f2494ce` 起,`3f2494ce..HEAD` 触及规则栈两笔:`6160a9e8`(pipeline-ui.md 重写 88 行——超出第十七次只见的 +14 Step4,本次补审)、本会话工作树(服务端流水线接入)。`0b41dc3d`(ui-atlas 工具 + design-docs)未触 `.claude/` 规则栈,排除。

**并发延续**:第十七次落账 + 本次落账均未提交,且审计间已有 `6160a9e8`/`0b41dc3d` 落地——「concurrent-pipeline-auto-resets-worktree」式漂移再现(落账未提交有被并发 git 清理风险),建议尽快提交本审计落账。

自基线以来规则栈实改:
- **本会话(服务端流水线接入:B 形态 + 自治覆盖 + 全栈顺序编排)**:新增 `pipeline-server-dev.md`/`pipeline-server-test.md`(镜像 dev/test、工具链 dotnet、显式 Read fantasy-net 正本、协议跨仓库归属/检查项);`pipeline/SKILL.md` 加 target(client/server)选卡维度 + 跨仓库 checkpoint 提交 +「全栈特性编排」节(协议优先顺序增量)+「不 build」边界澄清(server-test dotnet build=编译验证非发布);`pipeline-auto.js` 加 target 维度(server 换角色对 + 切 state/memory 路径,baton/plan 语义不变);新增 `pipeline/memory/server-dev.md`/`server-test.md` 头;`pipeline/README.md` 角色枚举同步(漏 ui 一并补)。
- **`6160a9e8`(pipeline-ui.md 重写)**:AI 生成降级为可选概念工具,生产对齐已验证精灵表范式(UIAtlasPacker/Sheet_<屏>/SetSubSprite),Replicate/Flux 移入附录「可选概念图·非生产依赖」。

查项结论(1-3 必查):
- **重复**:无候选。server 卡镜像 client 卡结构系多 agent 提示词固有并列(各 consumer 只见自身 system prompt),同第十四~十七次并列副本模式;server 卡红线/工作流均「显式 Read fantasy-net 正本、不复述」,非条文复制。全栈编排 SKILL 节 + target bullet 已收为指针、不重复跨仓库条款。**观察(非候选)**:协议同步职责分布 server-dev 卡(产)+ server-test 卡(检)+ SKILL 全栈节(排序)三处,系角色切分(同 plan 写崩法/test 越界 模式)、role-specific 非逐字,但为本批最高耦合点——改协议同步机制须三处同步。`6160a9e8` 单源 ui 卡。
- **死规则**:无候选。server 卡条款触发于 server 任务(新基建,尚无实跑实例,单窗口不判死);target/全栈编排触发于 server/全栈任务。carry-forward:plan「向上对体验」自检仍无触发实例(第十三次起续监视)。
- **矛盾**:**2 改写候选**(见下)。其余自洽:target 维度与环节裁剪/baton 正交;跨仓库 checkpoint 扩展既有 checkpoint 逻辑;「不 build」边界澄清消解 server-test dotnet build 与 boss 不 build 的表面冲突;server-test「四类」与 client「四类」各自独立、命名平行非冲突;`6160a9e8` 与 atlas 工具/ui-production-plan 记忆一致。

**改写候选 1(carry-forward 第十七次,未解决,报用户)**:`pipeline-auto.js` feasibilityCheck 残留。本会话 target 重写**逐字保留** `PLAN_SCHEMA.feasibilityCheck` + feasibility stage,第十七次所标「plan code-blind ↔ schema 反向要 plan 产代码接缝」冲突依旧。新细节:stage 现 spawn `devAgentType`(server 单子=server-dev、会读码,feasibility 对 server 合理),但触发仍是 `plan.feasibilityCheck`(两端 plan 均 code-blind、不产该字段)→ 经 plan 触发路径仍死、schema 冲突仍在。选项同第十七次((a)删字段+stage / (b)改非 plan 触发),属自治设计决策,留用户定。

**改写候选 2(本会话引入,报用户)**:`pipeline/SKILL.md` 加了 server target 维度 + server-dev/server-test state 文件,但 boss 的「恢复协议」(行18 `state/plan.md|ui.md|dev.md|test.md`)、「打回循环」(读 `state/test.md` 总判定 / 简报附 `state/test.md`+`state/dev.md`)、「关单事务」(行139/143 `state/test.md` PASS、行146 归档枚举)多处**硬编码 client state 文件名,未随 target 同步**。后果:regular-mode server 单子,boss 按字面会引/归档错文件(空的 test.md/dev.md 而非 server-test.md/server-dev.md)。属 conventions 规则6「改一处同步他篇」在本会话漏执行(同第十七次 feasibilityCheck 式样)。建议低改动面修法:在「环节裁剪·端(target)」加一句概括——「下文凡引 state/dev.md、state/test.md 处,server 单子按 target 替换为 state/server-dev.md、state/server-test.md」,不逐处改。(autonomous 模式 pipeline-auto.js 已正确参数化 state 路径,本候选只影响 regular-mode boss 手动编排。)

条件查项(4-7):
- **#4 信道匹配**:核过。server 卡入 `.claude/agents/`(spawn 态注入、非每窗,正确);无文件进出 `.claude/rules/` 递归注入路径。无错置。
- **#5 净增趋势**:注入态 `.claude/rules/` 不变(仅 audit-log stub 基线指针更新,conventions 未触)。新增 server 卡(spawn 态,~120+~100 行)+ SKILL(~+25)+ pipeline-auto.js(~+15,脚本非注入)均不占每窗口常驻,不升总量复查。
- **#6 孤儿旁注**:无。SKILL 全栈 bullet 改指针处无 `>` 旁注;新增「全栈特性编排」节 `>` 旁注父节在位;「不 build」为加法澄清;`6160a9e8` ui 卡重写保留「纯色占位不用 AI」「精灵表寻址」旁注、父规范(纯色占位/精灵表默认)在位。
- **#7 条款可执行性**(agents 改动):过。server-dev/server-test 判断条款(协议变更归属、自检四项、designFlaw 取证、越界试探、协议同步检查项、BLOCKED 判据)三要素(触发+动作+可核对产出物)齐全,无形容词冒充、无 taste 伪装;`6160a9e8` ui 卡红线(切图缺失→纯色占位、MCP 断连→暂停产 Step1-2、命名前缀逐项自检、概念图不绑生产 sprite)三要素齐。三张改动卡均过标尺。**自审声明**:server 两卡系审计者本人本会话所建,自评 clean,blast radius 中(新执行体进流水线),留下次独立审计复核(同第十六次自审式样)。

memory/server-dev.md、server-test.md 头准入:本地直读核过(gitignore、脱 git-diff)——标准准入式样(只记跨任务可复用且 agent 定义/fantasy-net/Fantasy CLAUDE/conventions 未覆盖),正文空,无死规则无孤儿。

观察(minor,非候选):① server-dev 卡 build 命名 `examples/Server/Server.sln`(server 业务)但「具体命令以 Fantasy/CLAUDE.md 常用命令为准」,而 CLAUDE.md 常用命令只示 `dotnet build Fantasy.sln`(框架)+ Main run、未列 Server.sln build——轻微指针不全(非矛盾,卡已自区分 server 业务/框架),日后补 CLAUDE.md 或卡内明确即可。② SKILL 打回循环 BLOCKED 例子「Unity MCP/编辑器不可达等」client 味,server 环境阻塞(跑不动服/MongoDB)由「等」涵盖但不显式——若采纳候选 2 概括句可顺带提一句。

删除候选:无。改写候选:2(均报用户拍板,不静默改)。下次增量以本次落账提交后 commit 为基线(并发未提交风险:若被 reset 须重核 HEAD)。

**落账(同会话用户拍板后执行,主会话直接改)**:
- **候选 1 = 改非 plan 触发(保留预检)**:`pipeline-auto.js` 删 `PLAN_SCHEMA.feasibilityCheck` 字段,可行性预检 stage 移出 `full` 块、改读 `args.feasibilityCheck`(boss 判高风险时传入,full / dev-test 均适用;test-only 已先返回);`SKILL.md`「可行性预检」节加自治旁注(workflow 经 `args.feasibilityCheck` 跑同款只读 stage、不可行 → `BLOCKED stage=feasibility`)。消第十七次起的 plan code-blind ↔ 脚本 doc/impl 漂移。`node` async 包裹语法过;grep 核 `plan.feasibilityCheck` 残留 0、PLAN_SCHEMA 无该字段。
- **候选 2 = 加概括替换句**:`SKILL.md`「环节裁剪·端(target)」加一条——凡下文(恢复协议/打回循环/关单事务)引 `state/dev.md`、`state/test.md` 处,server 单子按 target 换 `state/server-dev.md`、`state/server-test.md`。一句覆盖全部硬编码处,不逐处改、不新增副本。
- 改动者收尾自检过(conventions 隔离/无副本/无孤儿/语体);两候选闭。
