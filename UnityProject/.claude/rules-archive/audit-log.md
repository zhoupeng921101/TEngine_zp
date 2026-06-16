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
