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
