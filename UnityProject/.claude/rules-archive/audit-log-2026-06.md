# 规则审计日志 · 历史归档(2026-06-12 首次审计 ~ 第十次审计 + 各改动登记)

> 这是更早的历史归档段(2026-06-12 首次 ~ 第十次审计),按需查阅、已移出自动注入范围。活跃基线指针在 `../rules/audit-log.md`(注入态 stub),近期审计叙述与改动登记在同目录 `audit-log.md`(活账本);注入信道为何分层、历史为何移出注入范围,见本文件下方「注入信道分层」+「历史归档移出注入范围」两个登记段。

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

## 2026-06-14 第六次审计(增量·player-info 关单触发)

- 基线:第五次审计之后,commit `88c4cc47`(语体清理)。自基线规则栈改动:register-sweep(通读 + 对抗复核 + grep 兜查)在活跃持久文件查出拟人/口语比喻 + 私造词,改平实词——`.claude/agents/pipeline-plan.md`(砍掉→不做)、`.claude/skills/unity-check/SKILL.md`(强杀→强制关闭)、`.claude/skills/pipeline/SKILL.md`(开跑→启动、该闸→这道启动前检查)、`.claude/workflows/pipeline-auto.js`(空转烧轮次→空转、白费轮次)。触发动因:用户 2026-06-14 点名「下一刀是不是黑话」,顺查整个规则栈语体。有改动,不短路,查三样。
- 范围:CLAUDE.md、`.claude/rules/`、各 SKILL.md、`.claude/agents/*.md`、`.claude/workflows/pipeline-auto.js`、`pipeline/memory/*` 文件头。
- 结论:
  1. **重复(无)**:本次是纯词面替换(比喻→平实),无新增规则内容,不产生新副本。
  2. **矛盾(无)**:词面改动不动语义(砍掉→不做、强杀→强制关闭、开跑→启动 等),且方向与 conventions 规则6「平实语体」一致——是合规化而非冲突。被替换的旧词非任何条款依赖的术语。
  3. **死规则(无)**:未新增规则。
  4. **健康面**:① player-info 轮(commit 待本次关单)是 plan 空停根治(31e5e37b)首次实战——plan 把全部范围开关放进 decisions、未塞 blockers,流水线一路 plan→dev→test 无空停,根治有效;② register-sweep 暴露固定词 lint 的局限(它漏标 memory/dev.md「丢掉」、未覆盖 .js),印证 conventions 规则6「grep 非穷尽、须人工通读」——本次靠 通读 agent + 直接 grep 兜查 双道补齐;③ 冻结归档与 conventions/语体记忆里「作为反面示例引用」的比喻词正确未动。
  5. **carry-forward**:第五次审计观察项 1(decisions/blockers 判据落 plan 角色卡 / RETURN_NOTE / schema 描述 3 触点,漂移监视)——本次未触这三处,仍一致,继续监视。
  6. 死规则累计:距首轮约 2 天,窗口仍不足,不判。
- 处置:无删除候选(纯合规化词面替换)。carry-forward 观察项续留。

## 2026-06-14 第七次审计(增量·settings 关单触发)

- 基线:第六次审计之后,commit `5251634a`。自基线规则栈改动:仅 `pipeline/memory/{plan,dev,test}.md` 各加 1-2 条经验(settings 轮沉淀:plan「设计设置/持久化前先 grep 框架是否已有该关注点约定」、dev「GameLogic.Settings 类型名与引擎内置重名 CS0104」+「复用框架 Setting.MusicMuted/SoundMuted 键 on↔muted 取反」、test「EditMode 域重载致桥会话瞬态注销 vs 真实 BLOCKED 的判读」)。无 CLAUDE.md/rules/SKILL/agent 改动。有改动(memory 条),不短路,查三样。
- 范围:同上六项。
- 结论:
  1. **memory 经验条(准入合格)**:3 处新增均带 2026-06 先例、跨任务可复用、与既有条无同义重复(plan 的「grep 框架既有约定」对比既有「grep 实现文件符号」是不同层——框架关注点 vs 项目符号;dev 两条是 settings 专有坑;test 的瞬态注销与既有「no_session 整会话不可达=BLOCKED」显式区分 瞬态可恢复 vs 真实阻塞,非重复)。无死规则、无矛盾。
  2. **语体交叉检(已就地修)**:本轮 agent 在持久文件再次写入比喻/私造词——memory/dev.md「撞」×2、state/plan.md「第五刀」、state/dev.md「撞」,boss 关单人工通读捕获,改平实(重名/同名/第五个增量)后归档。
  3. **lint 正则的精度取舍(评估后不改,记健康)**:conventions 收尾/交叉检 lint 正则(`打死|挂了|收口|死在|尾巴上`)未含规则6 prose 所列的「撞/串」。评估:**不应补**——「撞」会误命中合法物理术语「碰撞/碰撞体/碰撞检测」(本项目 Unity 域高频),补进正则将产生假阳性。正则刻意保守(高精度低召回),规则6 已显式声明「grep 非穷尽、须人工通读判语体」,人工通读是真正兜底且本轮 boss 交叉检已生效(捕获全部 4 处)。故正则保持现状,非缺陷。
  4. **carry-forward**:第五次观察项 1(decisions/blockers 判据 3 触点漂移监视)本轮未触,仍一致,续留。
  5. 死规则累计:距首轮约 2 天,窗口仍不足,不判。
- 处置:无删除候选,无修正候选(lint 正则评估后判健康、不改)。observation:agent 持久文件再污染是常态,backstop = boss 关单人工通读(非 lint),已按 conventions 交叉检执行。

## 2026-06-14 规则栈改动登记(design-docs 导航单源化,主会话直接改)

- 触发:用户提出「每加一篇文档都要改全库各页侧边栏」的维护痛点,主会话评估后确认是 O(N) 副本问题(侧边栏文档树被复制进每篇活跃文档),拍板方案 B(连首页卡片一并单源)+ 主会话直接改。非 /audit 全量,仅登记本次规则栈改动作为下次增量审计基线。
- 改动内容:
  1. **新增** `design-docs/assets/nav.js`:文档清单单一信息源(GROUPS 数据数组),运行时渲染①各页侧边栏文档树(按 location 自动 active)②index 首页卡片③本页目录(扫描正文带 id 的 h2/h3 自动生成 + IntersectionObserver 滚动高亮;立项信息框 `#intro` 置顶)。
  2. **改 14 篇活跃标准文档 + index.html**:侧边栏改为空容器 `<aside class="sidebar" id="sidebar"></aside>`,index 卡片区换成 `<div id="cards-root"></div>`,各页 `</body>` 前引 `nav.js?v=1`。单栏页 `11-core-loop-completion.html` 与 `archive/*` 不在范围(本就无侧边栏)。
  3. **改规则栈** `.claude/agents/pipeline-plan.md` 三处(产出:23 / 文档表现版式:30 / 归档连带事务:43):删去「全库各篇文档树同步增删该行」「各篇 sidebar 文档树同步」「index 撤卡片/收一行入口」等 O(N) 手工指令,改为「只改 nav.js GROUPS 一项」「本页目录由 nav.js 自动生成、标题须带 id」。
- 反向冲突检查(conventions 准入第6条):pipeline-plan.md 旧的逐篇同步指令与新结构(各篇已无手写树)矛盾,已在同一次改动里替换,无孤儿条款残留。
- 矛盾/死规则:无。新指令有具体触发实例——本轮重构期间一条 redeem-code 流水线新增 `20-redeem-code-system.html`,即按新模式(空 #sidebar + nav.js + GROUPS 加一项)落地,全库 21 页侧边栏 + index 16 卡片自动同步,O(N)→O(1) 根治经实地验证。
- 验证:浏览器预览确认 index(21 侧边栏/16 卡片/5 区块/4 归档链接)、doc 18 与 doc 20(21 侧边栏/active 正/本页目录含立项信息置顶)渲染正确,零控制台错误;滚动高亮配线就绪,但预览环境不保持程序化滚动,未能实地流测(标准 IntersectionObserver 模式)。
- 处置:根治非补丁,无删除候选。本段作下次增量审计基线。

## 2026-06-14 第八次审计(增量·redeem-code 关单触发)

- 基线:第七次审计(commit `5251634a`)+ nav 迁移登记段。自基线规则栈改动:① `pipeline/memory/dev.md` +2 条(Luban string 主键 + 子表 auto_id 外键聚合;跨命名空间复用 Normalize 防「输对码却 NotFound」);② `pipeline/memory/plan.md` —— 旧两条(逐篇 grep sidebar 核对 / 批量插行防 BOM)替换为 nav.js 数据驱动单源条 + 新增「设计稿正文平实说明文·避『钉死/焊死/绑死』X死比喻」语体条;③ `.claude/agents/pipeline-plan.md`(nav 迁移三处,已在 nav 登记段记录,本次不重审)。有改动,不短路,查三样。
- 范围:CLAUDE.md、`.claude/rules/`、各 SKILL.md、`.claude/agents/*.md`、`.claude/workflows/pipeline-auto.js`、`pipeline/memory/*` 文件头。
- 结论:
  1. **矛盾(无)**:memory/plan.md 旧的「逐篇 sidebar 同步 / 批量插行防 BOM」两条与 nav.js 单源新结构(各页已无手写树)矛盾,已在同次编辑替换为 nav 数据驱动条,无孤儿。与 nav 登记段改的 pipeline-plan.md 三处方向一致(均「只改 GROUPS 一行」)。
  2. **重复(无硬重复,记观察)**:plan.md 新增「X死」语体条与 conventions 规则6「平实语体」同域。判健康非删除候选——规则6 是权威规则陈述(性格/不变量层),plan 条是角色专属操作程序(具体 grep 词表 `钉死|焊死|绑死` + 加 20 实测实例 + 短路/爆栈例外),属准入第2「跨层补充」非同层副本;且 conventions 收尾 lint 正则刻意保守不含这些词(第七次审计已判定,防碰撞类假阳性),plan 条按域扩词表是有效补充。观察点:语体操作指引现落 conventions 规则6 + plan memory 两处,下次审计复核是否漂移。
  3. **死规则(无)**:memory 全部新增条带 2026-06 redeem-code/加20 具体实例。
  4. **健康面**:① redeem-code 是 plan 空停根治(31e5e37b)+ 放手默认在「服务器底层 + 接缝」决策下的又一轮零空停 full(plan 八项范围开关全走 decisions、零 blocker);② dev/test agent 本轮持久文件语体干净(boss 关单 grep 0 命中,test.md:49 自己也跑了 conventions lint),比 settings 轮(撞×3 + 第五刀)改善;③ 唯一语体命中是 state/plan.md「第六刀」(boss 交叉检捕获,归档前已改「第六个增量」)。
  5. **carry-forward**:第五次观察项(decisions/blockers 判据 3 触点漂移监视)本轮未触,仍一致,续留;新增观察项 2(语体操作指引 conventions/plan 两处)并入监视。
  6. 死规则累计:距首轮约 2 天,窗口仍不足,不判。
- 处置:无删除候选,无修正候选。观察项 2 报用户知会,不静默改。

## 2026-06-14 规则栈改动登记(关单加「核磁盘交付物」闸,主会话直接改·用户拍板)

- 触发:mail 自治流水线返回脱离真实执行的假 PASS。事件经过:① 首条通知报「PASS,333/333 + Mail 22 测试 + 完整决策」(agent_count:3、563K token);② 主会话关单前看 git status 为空起疑,查 transcript/journal 发现磁盘零 mail 文件;③ 数分钟后第二条通知(同 task-id)更正为真实结果 BLOCKED@plan「plan agent 异常退出」,原因 = 撞会话用量上限(8:30pm 上海重置),实际 plan 写完设计稿 21 + plan.md 交接区后被用量上限中断,dev/test 未跑(agent_count:1、167K token)。首条 PASS 与磁盘、与第二条 usage 数字均对不上,系错误/损坏通知。
- 近失:旧关单事务第 1 步只「核对 state/test.md 总判定 = PASS」,信任 verdict;若按惯例直接归档提交,会给完全不存在的 mail 系统记一笔 PASS 关单。仅因 boss 偶然先看 git status 才拦下。
- 改动内容:`.claude/skills/pipeline/SKILL.md`「关单事务」第 1 步扩为「核对判定 + 核磁盘交付物」——收到 PASS 后必须核验 verdict 声称产出在磁盘真实存在(git status 非空 + 关键路径 ls),不一致按未完成处理(不归档不提交),定位缺口后续接/重跑;附 2026-06-14 mail 假 PASS 旁注。用户 AskUserQuestion 拍板「加(推荐)」。
- 反向冲突检查(conventions 准入第6条):与既有「收产出首选验文件」(编排流程:37)、「进度只能推导不能查档·state 交接区是进度唯一来源」(:20)不冲突——本闸是把「验文件」强化为关单硬前置,并明确「state 交接区文本同样可能脱离执行,只有文件实际存在才算交付」,方向一致(磁盘 > verdict 文本 > 推测)。无既有条款被涵盖/过时,无孤儿。
- 矛盾/死规则:无。新闸有具体触发实例(本次 mail 假 PASS)。
- 处置:加法式强化,非补丁(根在「关单信任 verdict 而非磁盘」,本闸直接堵该根)。无删除候选。本段作下次增量审计基线。

## 2026-06-14 第九次审计(增量·mail 关单时一并查)

- 基线:上方「核磁盘闸」登记段 + 第八次审计(commit `f51bd660`)。自基线规则栈改动:① `.claude/skills/pipeline/SKILL.md` 关单第 1 步加核磁盘闸(已在上方登记段记录,本次不重审);② `pipeline/memory/dev.md` +2 条(单行全局配置表用普通 map 表 + id 固定 1 兜底;`GameLogic.Mail` 与 Luban 行类 `GameConfig.Mail` 同名消歧 + JsonUtility 嵌套元素须 `[Serializable]`、派生态用 getter 属性不进盘);③ `pipeline/memory/test.md` +2 条(配置源 xlsx 在 repo 兄弟目录 `<repo>/Configs/` 不在 UnityProject 内;Luban C3 直读取 GREEN 时即构成 xlsx→bytes 一致性最强交叉核验,不必手解 xlsx)。mail 分两段执行(plan 撞用量上限→基线 `367c080d`,dev-test 续接 PASS)。有改动,不短路,查三样。
- 范围:CLAUDE.md、`.claude/rules/`、各 SKILL.md、`.claude/agents/*.md`、`.claude/workflows/pipeline-auto.js`、`pipeline/memory/*` 文件头。
- 结论:
  1. **重复(无)**:dev「单行配置表」「同名消歧+JsonUtility」均新技术点;后者显式引「同 item-system 行类命名条」为互补(加 JsonUtility 序列化细节)非副本。test「xlsx 兄弟目录路径」「C3 GREEN 核验」与既有 test 条(EditMode 域重载/反射私有成员)非同域,无重复。
  2. **矛盾(无)**:memory 全为加法;核磁盘闸与既有「收产出首选验文件」「进度只能推导」方向一致(登记段已记反向冲突检查)。
  3. **死规则(无)**:memory 4 条 + 核磁盘闸均带 2026-06 mail 具体实例(核磁盘闸实例 = 本轮首条假 PASS)。
  4. **健康面**:① 核磁盘闸首次实战即拦下真问题——mail plan 段工作流先返假 PASS(333/333、磁盘零文件),旧流程会误关单不存在的系统,新闸 git status + ls 核盘拦下;② 首次「撞会话用量上限分段执行」成功恢复(plan 产出提交基线 → 用量恢复后 dev-test 续接,未浪费已落地 plan);③ dev/test 持久文件语体干净(boss 关单 grep 0 命中、test 自跑 conventions lint),延续 redeem 轮的改善;④ memory test 条「xlsx 在兄弟目录」修了一个真实绊点(boss 本轮也曾在 UnityProject 内找 mail.xlsx 扑空)。
  5. **carry-forward**:第五次观察项(decisions/blockers 3 触点)、第八次观察项 2(语体指引 conventions/plan 两处)本轮均未触,仍一致,续留。
  6. 死规则累计:距首轮约 2 天,窗口仍不足,不判。
- 处置:无删除候选,无修正候选。本段 + 上方登记段共作下次增量审计基线。

## 2026-06-14 规则栈改动登记(注入信道分层 + audit-log 历史归档,主会话直接改·用户拍板)

- 触发:用户追问「为何 audit-log 被注入、有何作用」。核查:项目无配置显式加载它(settings 仅权限、CLAUDE.md 无 @import、json 无引用),系 harness 自动注入根 `CLAUDE.md` + `.claude/rules/*.md`;audit-log 27KB「住在 rules 目录」搭便车进每窗口常驻信道,而它是日志(偶尔追溯型),操作上只在审计时需最近一段基线。每窗口全量注入基本是浪费。
- 改动:① conventions「内容分层」加「注入信道也是一层」旁注(注入路径只放规范层每轮相关项,日志/数据放路径外按需读);② 准入 #5 归位 加「核信道」一句;③ 规则审计加第 4 项「信道匹配」(仅注入集有增减时触发,罕见、非每轮);④ audit-log 历史段(首次~第八次审计 + nav 登记)移入 `archive/audit-log-2026-06.md`,活跃文件只留最近基线 + 本段,27KB→数 KB。
- 反向冲突检查(conventions 准入第6条):与规则4「日志超 ~200 行归档到同目录 archive/」一致(本次即执行该归档);「注入信道」视角是对规则4 的补充——规则4 管「何时归档」,新视角管「对注入路径文件为何更急」,非矛盾,未使既有条款过时。
- 待核实(下个上下文窗口):确认活跃 audit-log 注入变小、且 `archive/` 子目录未被递归注入;若 archive 仍被注入(harness 递归扫 `.claude/rules/**`),把归档移出 `.claude/rules/`(如 `pipeline/`)。
- 处置:用户拍板「按你建议的做」。本段 + 上方核磁盘闸登记 + 第九次审计共作下次增量审计基线。

## 2026-06-14 规则栈改动登记(历史归档移出注入范围,主会话直接改)

- 触发:上方「注入信道分层」段的待核实项已核——新窗口注入清单里 `.claude/rules/archive/audit-log-2026-06.md` 仍在,证明 harness 对 `.claude/rules/` 是**递归**注入(子目录一并扫)。上一步把历史挪进 `rules/archive/` 子目录没有真正减注入,只是把一个被注入的文件拆成两个。
- 改动:① `git mv .claude/rules/archive/audit-log-2026-06.md` → `.claude/rules-archive/audit-log-2026-06.md`(与 `rules/` 同级、在递归注入范围外),空掉的 `.claude/rules/archive/` 一并删除;② 把 conventions(内容分层注入信道旁注 / 准入 #5 核信道 / 审计 #4 信道匹配)与本文件 line 4 里「`.claude/rules/*.md`」(暗示仅顶层)订正为「`.claude/rules/` 下所有 .md 递归」,示例归档路径改 `.claude/rules-archive/`;③ 归档文件内部相对指针 `../audit-log.md` 改 `../rules/audit-log.md`。
- 效果:历史段(22KB)真正移出每窗口注入,活跃 audit-log 注入降到数 KB(此前仅拆分未减,见触发)。预期下个窗口注入清单不再含 audit-log-2026-06——`.claude/rules-archive/` 不在 `.claude/rules/` 下;claudeMd 项目指令注入只覆盖 `.claude/rules/`、不递归整个 `.claude/`(证据:`.claude/skills`、`.claude/agents` 从不作为项目指令注入)。
- 反向冲突检查(conventions 准入第6条):旧表述「`.claude/rules/*.md`」与实测递归注入冲突,已同次全栈订正,无孤儿;与「注入信道分层」段方向一致(那段定原则,本段修执行偏差)。
- 处置:订正+加法,无删除候选。本段作下次增量审计基线。

## 2026-06-14 第十次审计(增量·rank 关单触发)

- 基线:第九次审计(commit `f51bd660`)+ 其后两段登记(注入信道分层 `5bc3c7aa`、历史归档移出注入范围 `f191b67a`)。自基线规则栈改动:① 上两段已登记的注入信道/审计条款改动 + audit-log 历史移出 `.claude/rules/`;② `pipeline/memory/dev.md` +2 条(跨系统调用链触及 `ConfigSystem` 须给被调系统 `ConfigMgr.InitForTest` 注入,防 YooAsset「Default package is null」;周循环结算 ISO 周判据);③ `pipeline/memory/plan.md` +2 条(新系统发奖借既有下发系统对外 API、不直接接 ItemGrant;一表控所有 X 用 row_id 唯一主键 + 语义 id 聚合)。有改动,不短路,查三样 + 信道匹配。
- 范围:CLAUDE.md、`.claude/rules/`、各 SKILL.md、`.claude/agents/*.md`、`.claude/workflows/pipeline-auto.js`、`pipeline/memory/*` 文件头。
- 结论:
  1. **信道匹配(#4 首次触发,健康)**:本轮注入集有增减——`.claude/rules/archive/audit-log-2026-06.md` 移出为 `.claude/rules-archive/audit-log-2026-06.md`(出递归注入范围)。核:活跃 audit-log(审计基线,每轮相关)留注入内正确;历史归档(低频追溯)移出正确;新设 `.claude/rules-archive/` 不在 `.claude/rules/` 下、不被注入,归位正确。并订正旧表述「`.claude/rules/*.md`」(暗示仅顶层)→「递归含子目录」,与实测一致。
  2. **重复(无)**:plan「借既有下发系统 API 发奖」对比既有「grep 框架既有约定复用」是不同层(发奖编排 vs 框架键复用);「row_id 聚合一表多档」对比既有「单行全局配置表」「TbItem 模板新建」非同义(多行聚合 vs 单行旋钮 vs 模板取舍)。dev「跨系统 InitForTest 注入」是新坑(EditMode 跨系统 YooAsset);「ISO 周结算判据」是新算法点。均无同义副本。
  3. **矛盾(无)**:memory 全加法;注入信道两段登记的反向冲突检查已记(旧「*.md」表述同次全栈订正,无孤儿)。
  4. **死规则(无)**:memory 4 条均带 2026-06 rank 实例;信道匹配 #4 首次实战即用上(本轮触发)。
  5. **健康面**:① rank 是放手默认 full 又一轮零打回零空停(plan 范围开关全走 decisions、零 blocker;17 条 decisions 全安全默认);② 核磁盘交付物闸第二次实战通过(本轮 verdict 与磁盘一致,git status 非空 + 6 .cs/测试/Luban/设计稿全在),与 mail 轮的假 PASS 拦截形成正反两例;③ 批次内系统逐层叠(num→item→reward→mail→rank)印证 plan 新 memory「越往后越只是编排、几乎不新造底层」——rank 零新底层,全复用 21 邮件 + 16 发奖 + Provider;④ dev/test 持久文件语体干净(test 关单 grep 0 命中 + boss 复核 memory diff 无比喻/指代/diff 叙事)。
  6. **carry-forward**:第五次观察项(decisions/blockers 判据 3 触点漂移)、第八次观察项 2(语体指引 conventions/plan 两处)本轮均未触,仍一致,续留。
  7. 死规则累计:距首轮约 2 天,窗口仍不足,不判。
- 处置:无删除候选;本轮含一次主动订正(`.claude/rules/*.md`→递归,已在 `f191b67a` 完成)。本段作下次增量审计基线。

