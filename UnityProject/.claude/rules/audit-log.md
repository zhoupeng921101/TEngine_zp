# 规则审计日志

> `/audit` 每次执行后追加一段:日期、规则栈基线(git rev 或文件清单时间)、结论。增量短路以最近一段基线为准。
> 本文件在自动注入信道(`.claude/rules/` 下所有 .md 递归注入、每窗口常驻),故只留**最近审计段**作增量基线;更早历史段归档到 `.claude/rules-archive/audit-log-2026-06.md`(在递归注入范围外)、按需 Read。本文件再超 ~200 行就把旧段移入该归档(conventions 规则4)。

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
