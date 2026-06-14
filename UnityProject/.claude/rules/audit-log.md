# 规则审计日志

> `/audit` 每次执行后追加一段:日期、规则栈基线(git rev 或文件清单时间)、结论。增量短路以最近一段的基线为准。本文件超 ~200 行归档到同目录 `archive/`。

## 2026-06-12 首次审计(全量)

- 基线:git `2703b2bd`(工作流水管线迁移到 claude code)+ 未提交改动(`.claude/skills/pipeline/SKILL.md`、`pipeline/memory/boss.md`)
- 范围:CLAUDE.md、`.claude/rules/`、各 SKILL.md、`.claude/workflows/pipeline-auto.js`、`.claude/agents/*.md`、`pipeline/memory/*` 文件头
- 结论:
  1. **重复(删除候选,待用户拍板)**:CLAUDE.md「触发时机」表 +「📚 References 参考文档」表 与 tengine-dev SKILL.md「文档路由」表同源枚举,且已漂移(7 主题 vs 11 行路由;14 篇 vs 11 行)。候选修法:CLAUDE.md 侧两表压成指针(知识路由的单一信息源归 SKILL.md)
  2. **失效引用(修正候选,待用户拍板)**:CLAUDE.md 两处指向 `.claude/memory/`(冲突仲裁记录、自我优化 problem_*.md 落点),该目录不存在。候选修法:(a) 建目录 (b) 改指既有位置
  3. **矛盾(已修)**:打回简报「附上一轮 dev 最终回复」在 pipeline SKILL.md 与 pipeline-auto.js 两套实现间不同步,已同步 js
  4. 健康面:活跃规则栈零 OpenClaw 残留、零失效路径(wiki-query-agent I:\ 已修)、红线/写作规约均单源+指针、无对话残留(lint 命中仅 conventions 规则原文,合法);archive/ 内 roles/ 引用为冻结历史证据,不修
  5. 死规则:首轮无累计窗口,不判;下轮起累计观察
- 处置(同日,用户拍板):①已修——CLAUDE.md 两表压成指针,路由单源归 tengine-dev SKILL.md;②选 a——已建 `.claude/memory/`(含 README 说明落点);③源头解决——「档案体不进对话」入 CLAUDE.md「表达与协作」(归位:表达层规则覆盖一切对用户呈报,pipeline SKILL.md 不打补丁)

## 2026-06-13 第二次审计(增量)

- 基线:上次审计基线 git `2703d2bd`(首段误记为 `2703b2bd`,b/d 笔误,本段订正)+ 首轮处置 commit `c41c13fd`。自基线规则栈有改动(新增 unity-check skill;test 判定三态进 boss SKILL 与 `pipeline-auto.js`;plan/dev/test 角色卡编辑;conventions 增规则6 语体),不短路,查三样
- 范围:CLAUDE.md、`.claude/rules/`、各 SKILL.md、`.claude/agents/*.md`、`.claude/workflows/pipeline-auto.js`、`pipeline/memory/*` 文件头
- 结论:
  1. **矛盾(已修)**:`pipeline-test.md` 总判定仍两态 PASS/FAIL,与 boss SKILL「打回循环」、`pipeline-auto.js` TEST_SCHEMA、`pipeline/memory/test.md` 已用的三态 PASS/FAIL/BLOCKED 冲突(commit `253e0eb4` 加 BLOCKED 时漏更角色卡)。常规模式 test 凭角色卡判定,会把环境阻塞误判 FAIL → boss 多记一轮空转打回。修:角色卡「产出」「返回契约」补 BLOCKED,与其余三处对齐
  2. **重复(已删)**:`pipeline/memory/plan.md` 第7条(范围开关集中列待拍板清单)、第9条(数值给公式/常量/旋钮、不写日后再调)已被 plan 角色卡新增的「设计稿章节骨架」涵盖,按 memory 文件头准入(只记 agent 定义未覆盖的经验)删除;第9条连带的 merge-order 体力档预算数学先例可从该设计稿(design-docs §四)与 git 历史取回。同节余下「映射函数 vs 固定值」条触发场景不同,评估后保留
  3. **重复(已合并)**:Unity 连接诊断「进程冻结 vs 桥会话未注册(Responding=True 但 instances 空)」在 `pipeline/memory/dev.md`、`pipeline/memory/test.md` 近乎逐字重复,且与新增的 unity-check skill 同域而该 skill 未收录。诊断上提进 unity-check SKILL「边界」(dev/test 开工前均跑 /unity-check,都能拿到),两 memory 改为指针;各角色专属处置(dev 静态复核+上报 boss;test 判 BLOCKED+补跑清单)留在原 memory
  4. 健康面:编码红线/conventions 引用/「开工前 /unity-check」均为指向共享源的指针、非副本,合规;返回契约「角色卡散文 vs auto.js schema」属两信道(常规自由文本/自治结构化),列观察项不处置;CLAUDE「表达·平实」与 conventions 规则6「语体」打击不同病(文言公文 vs 拟人比喻)、作用域不同,互补
  5. 死规则:距首轮约 1 天,累计窗口不足,不判;无举不出触发实例的条款
- 处置(同日,用户拍板三项均取推荐):①已修——test 角色卡三态;②已删——plan memory 第7、9条;③已合并——Unity 连接诊断归 unity-check skill,dev/test memory 转指针

## 2026-06-13 第三次审计(增量·关单触发)

- 基线:第二次审计之后,core-loop-completion 关单会话又改规则栈两处——`.claude/skills/pipeline/SKILL.md`(环节裁剪表新增 test-only 行 + baton 标注)、`.claude/workflows/pipeline-auto.js`(args 字符串 JSON.parse 兜底 / test-only 档 / dev·test 断连重试 + test 先写报告后返回)。有改动,不短路,查三样
- 范围:同上(CLAUDE.md、`.claude/rules/`、各 SKILL.md、`.claude/agents/*.md`、`pipeline-auto.js`、`pipeline/memory/*` 文件头)
- 结论:
  1. **重复(无,记观察)**:test-only 的 baton 值同时见于 `pipeline-auto.js` arg 注释(枚举+行为,权威源)与 SKILL.md 环节裁剪表(任务性质→baton 路由),属「路由层引用机制层」、与既有 full/dev-test 行同体例,机制单一信息源仍在 js,非问题副本
  2. **矛盾(轻微,记观察不阻塞)**:`pipeline-auto.js` meta.description 仍写「返回 PASS/BLOCKED」,未含 test-only 新增的 FAIL 返回态;描述性散文非契约,低优,下次顺手补。test-only「无 dev、FAIL 不返修」与打回循环不冲突(SKILL.md 已显式声明例外)
  3. **死规则(无)**:三项改动触发场景具体可举(args 解析←3 次作废启动;test-only←本单环境恢复后补测;断连重试←实测 test 跑完 129/129 后 socket 断连致 agent 返 null)
  4. 健康面:js 与 SKILL.md 同次改动一致、无漂移;断连重试与「test 先写报告后返回」配套(文件是真相→重跑可复用),与 conventions「可推导的不记录」同向
  5. 死规则累计:距首轮约 1 天,窗口仍不足,不判
- 处置:观察项 1、2 报用户知会,不静默改;无删除候选

## 2026-06-14 第四次审计(增量·tarot-blind-box 关单触发)

- 基线:第三次审计之后。自基线规则栈改动:① CLAUDE.md 增「立场朝结果,不朝当下势头」条款;② `.claude/skills/pipeline/SKILL.md` 增「参与环节两个来源 / 含 test 决定验收强度 / 自治 baton 旁注(:63)/ @角色 vs /pipeline 单环节对比」(①② 同在 commit `4ef8b90e`,2026-06-14);③ `pipeline/memory/plan.md`(+2)、`dev.md`(+1)、`test.md`(+2)经验条。有改动,不短路,查三样。
- 范围:CLAUDE.md、`.claude/rules/`、各 SKILL.md、`.claude/agents/*.md`、`.claude/workflows/pipeline-auto.js`、`pipeline/memory/*` 文件头。
- 结论:
  1. **矛盾(修正候选,报用户拍板)**:SKILL.md §环节裁剪 新增旁注(:63)「自治模式经 pipeline-auto workflow,baton 仅 full/dev-test 两档;要单环节走常规模式」与三处冲突——(i) 同节环节裁剪表 test-only 行(:70)+ test-only 行为条(:72);(ii) `pipeline-auto.js` baton 枚举含 test-only(:14、:71-72 校验、:128-141 专门分支);(iii) 2026-06-13 core-loop-completion 关单实测用 auto test-only(Run `wf_f9e0c4dc`,一轮 PASS),且第三次审计本身记录过 test-only 同时进 js 与 SKILL 表、判为健康。旁注「仅两档」与 test-only 实为 auto 第三档相冲突。候选修法:**(a 推荐)** 旁注改为「baton = full/dev-test/test-only;test-only 仅作环境恢复后补运行验证的续接档,新鲜任务从 full/dev-test 起」——承认 test-only,撤掉会重蹈环境阻塞空跑 dev 一轮的低效(正是第三次审计/core-loop 加它的初衷);**(b)** 真要从 auto 撤 test-only → 同步删 pipeline-auto.js test-only 分支 + 表行 :70 + 条 :72,环境恢复补跑改走常规模式。
  2. **CLAUDE「立场朝结果」(健康)**:与既有「评估提案先独立成判」(怎么评)、「修问题先亮牌」(补丁vs根治)非重复——本条管「何时主动拉高视角」(触发①难回退方向 ②同类摩擦第二次),且显式 cross-ref 两条。条内自带 2026-06-14 流水线成本触发实例,非死规则。无矛盾。
  3. **SKILL 其余增量(健康)**:参与环节两个来源 = description 触发的 body 详解(单源在 body);含 test 验收强度、@角色 vs /pipeline 单环节对比 = 既有节澄清补充,非副本。除上 1 外无矛盾。
  4. **memory 5 条(健康)**:plan(sidebar 同步核对、已建成未接UI数据层验收锚定单测)、dev(run_tests Play Mode 判读 + EditMode 即编译自检)、test(反射驱动 UIWindow 私有成员含 ValueTuple Item* 坑、code-built 窗口命名)——均带 2026-06 先例、平实语体、与既有条无同义重复。准入合格。
  5. **carry-forward 观察项**:第三次审计 #2(pipeline-auto.js `meta.description` 仍写「返回 PASS/BLOCKED」、未含 test-only 的 FAIL 返回态)仍未补,低优;若取上 1 的 (a) 修法,顺手把 description 补成「PASS/FAIL/BLOCKED」一并对齐。
  6. 死规则累计:距首轮约 2 天,窗口仍不足,不判。
- 处置:1 个矛盾(修正候选)报用户拍板。**用户取 (a)(2026-06-14)**:已改 SKILL.md:63 旁注为「baton = full/dev-test/test-only;test-only 仅作环境恢复补运行验证的续接档」,并顺手把 `pipeline-auto.js` meta.description 补为 PASS/FAIL/BLOCKED(对齐 carry-forward #5)。其余健康,无删除候选。

## 2026-06-14 第五次审计(增量·reward-display 关单触发)

- 基线:第四次审计之后,commit `31e5e37b`(reward-display 归一层关单)。自基线规则栈改动:plan 空停根治——① `.claude/agents/pipeline-plan.md` 红线「自治模式下拿不准的列入返回的 blockers」替换为 decisions/blockers 分流判据(有安全默认的范围开关→decisions 默认推进;无默认/默认抵触 spec·GDD/不可逆→blockers 停机)+ 理由旁注;② `.claude/workflows/pipeline-auto.js` PLAN_SCHEMA.blockers 描述 + RETURN_NOTE 同步收窄。触发动因:同类摩擦第三次(numeric-system O3 / item-system O2 / reward-display 4 开关均把有默认的范围开关塞进 blockers 致自治流水线在 plan 空停一轮),按 CLAUDE.md「修问题先亮牌」2 次以上复发须源头根治,本轮关单会话同步改完。有改动,不短路,查三样。
- 范围:CLAUDE.md、`.claude/rules/`、各 SKILL.md、`.claude/agents/*.md`、`.claude/workflows/pipeline-auto.js`、`pipeline/memory/*` 文件头。
- 结论:
  1. **重复(无,记观察·漂移点)**:decisions/blockers 判据现落 3 处——pipeline-plan.md 红线(行为权威源 + 旁注)、pipeline-auto.js RETURN_NOTE(注入每个 agent prompt 的精简一句)、PLAN_SCHEMA.blockers 描述(结构化输出层字段说明)。三者是同一判据的精简复述、措辞一致不冲突;后两处由结构化输出层与 spawn prompt 直接消费、无法压成指向角色卡的指针(schema 描述/RETURN_NOTE 是机制必需的就地文本),故 3 触点是结构性的,与既有 baton 枚举跨 js+SKILL(第三次审计判健康「路由层引用机制层」)同类。列观察:三处改一处须同步另两处,下次审计复核是否漂移。非问题副本。
  2. **矛盾(无)**:新判据替换掉旧的过宽指令「拿不准的列入 blockers」(同次编辑替换,无孤儿)。与 plan 角色卡:27「设计稿章节骨架·待拍板清单(范围开关集中列出交 boss/用户)」互补不冲突——后者是设计稿文档信道(boss 关单读,如 17-reward-display §七 列 O1-O8),新判据管结构化返回信道(decisions[] 默认推进 vs blockers[] 停机),两信道并行;blockers[] 字段未被废弃(仍停机,只收窄准入)。无条款被涵盖/过时。
  3. **死规则(无)**:新判据有 3 个具体触发实例(numeric/item/reward plan 空停),非死规则。
  4. **健康面**:pipeline-plan.md 与 pipeline-auto.js 同次改动措辞一致、无漂移;旁注「2026-06 三次」脱离对话成立(绝对日期 + 具体实例),非 diff 叙事;lint 0 命中。本轮关单交付(reward-display 仅归一层、表现层转遗留 #20)属任务范围处置,记主 boss.md,非规则栈事项。
  5. **carry-forward(无)**:第三/四次审计的 meta.description 观察项已于第四次处置闭环;无悬留。
  6. 死规则累计:距首轮约 2 天,窗口仍不足,不判。
- 处置:无删除候选(本次为加法/收窄,且替换旧条同次完成)。观察项 1(3 触点漂移监视)留下次审计复核,不静默改。
