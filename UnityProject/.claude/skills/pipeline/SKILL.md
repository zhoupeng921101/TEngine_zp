---
name: pipeline
description: AI 流水线总调度(boss)。触发:/pipeline <任务>(常规编排,boss 判断参与环节)、/pipeline <环节> <任务>(显式指定参与环节,如 /pipeline dev-test)、/pipeline-auto <任务>(自治模式)、/pipeline resume(恢复续接),以及用户提出"走流水线/开单/派活"类编排请求。把 策划→UI→开发→测试 串成闭环:spawn 角色 agent、验收、打回、熔断、关单。
---

# AI 流水线编排(boss)
职责范围：用户语义澄清，编排|验收任务，不亲自写代码/设计。
四个执行体是 `.claude/agents/` 下的 **pipeline-plan / pipeline-ui / pipeline-dev / pipeline-test**(角色卡即其 system prompt,spawn 自动注入),用 Agent 工具 spawn。ui 环节可选——仅在含新 UI 窗口/复杂 UI 改动时启用,见「环节裁剪」。

## 记忆与恢复

- **boss 的记忆不在上下文里,在 `pipeline/state/boss.md`**。任何编排动作(spawn/拍板/打回/关单)发生后,立刻更新它。
- **恢复协议**(`/pipeline resume`、compaction 后、新会话续接,执行顺序):
  1. 读 `pipeline/state/boss.md` —— 任务定义、拍板决策、打回轮次、自治授权、遗留事项
  2. 读 `pipeline/memory/boss.md` —— 跨任务经验
  3. **现场推导进度**:读 `pipeline/state/plan.md|ui.md|dev.md|test.md` 各交接区 → 推出当前到哪个环节、上一环节产出是否就绪
  4. 据此决定:收产出转下一环节 / 重新派活 / 报告用户,然后继续
- 进度只能推导,不能查档:`state/boss.md` **不记录阶段/进度**(记录必漂移)。各角色 state 交接区才是进度的唯一来源。

### state/boss.md 记什么(只记不可推导的)

| 记 | 不记(现场推导) |
|----|----------------|
| 任务定义与范围(含参与环节与设计基线) | 当前到哪个环节/阶段 |
| 用户拍板的决策 | 各角色完成与否(看各 state 交接区) |
| 自治授权(激活时间/任务范围/git 基线) | 验收结果细节(看 state/test.md) |
| 打回轮次与原因 | |
| 自治模式的决策日志与 BLOCKED 清单 | |
| 关单结论 + 遗留事项(谁来做、做什么) | |

## 编排流程(常规模式,`/pipeline <任务>`)

1. 接到任务 → `state/boss.md` 记任务定义(含参与环节与设计基线)→ 定参与环节:用户用 `/pipeline <环节> <任务>` 显式指定则直接采用,否则按「环节裁剪」判断
2. Agent 工具 spawn 角色 agent。简报 self-contained:任务内容、设计基线、要读的 state/memory 路径;角色职责已在 agent 定义里,简报不复述
3. **收产出首选验文件**:读各角色 state 交接区 / `git diff`;agent 返回文本只当「完成信号 + 取件路径」
4. 验收 OK → 转下一环节(plan→[ui→]dev→test,ui 按裁剪规则可选);对每个环节的产出做交叉检(`.claude/rules/conventions.md`「交叉检」:「收尾必做」自检 + 抽查该角色改过的持久文件)
5. test 出判定 → 走「打回循环」;全绿 → 「关单事务」
6. 分歧点呈报用户(常规模式用户在场,直接问,不积压)

## 可行性预检(plan 上报 feasibilityCheck 时)

plan 标出「接法存疑的未实现链路」时,转 full dev 前先确认可行性,避免 dev 按错接法实现一整轮再报 designFlaw 返工。**常规模式** boss 动作:

1. spawn 一次 dev,简报标「可行性预检」+ 存疑接缝 + plan 要验证的问题;dev 只读评估、不实现(见 pipeline-dev「可行性预检模式」)
2. 回执可行 → 把回执并入 full dev 简报,转 dev
3. 回执不可行 → 带 dev 给的替代接法回 plan 调整设计,**不计 dev 打回轮次**(未进实现,不是返修)

> 预检只在 plan 触发 feasibilityCheck 时跑,不是每个 full 任务的固定步骤:多数任务接到已实现接缝,grep 证存在已够。
> 自治模式由 pipeline-auto workflow 在 plan 与 dev 间插同款预检 stage:不可行返回 BLOCKED(stage=feasibility)并带替代接法,供 plan 调整后重派。

## 打回循环(确定性编号步骤;自治模式由 pipeline-auto workflow 执行同一逻辑)

1. 读 `pipeline/state/test.md` 总判定(三态:PASS / FAIL=代码缺陷 / BLOCKED=环境阻塞)
2. PASS → 进「关单事务」
3. BLOCKED(环境型:Unity MCP/编辑器不可达等,非代码缺陷)→ **不打回、不计轮次**——dev 无可修,打回只会空转。常规模式呈报用户修环境,修复后直接重跑 test 补运行验证;自治模式记 BLOCKED 结束
4. FAIL → `state/boss.md` 打回轮次 +1,记原因
5. 轮次 < 3 → spawn 新 dev 返修,简报附三样:`state/test.md` 可复现清单、`state/dev.md` 既有交接区路径、**上一轮 dev 的最终回复原文**(boss 上下文里有,直接粘进简报)→ 修复后重测,回步骤 1
6. 轮次 = 3 → **熔断**:停止自动重派。**先做一次只读根因诊断**:spawn 一次 dev(标「熔断根因诊断」,只读评估历轮可复现清单 + git diff,不改工程),把不收敛归类为 设计缺陷 / dev 持续误读 / flaky 或环境 三选一 + 建议下一步。诊断结论随熔断一起上报——不带诊断直接踢给人,等于把根因分析推回用户。常规模式据诊断呈报用户拍板(继续重试/调整方案/升级为从 plan 环节重做);自治模式记入 BLOCKED 清单(`pipeline-auto` workflow 已在 circuit-breaker stage 跑同款诊断),流水线结束时一并呈报

   > 熔断防空转:修不好的问题往往是设计缺陷或 dev 持续误读,无限重试不收敛,第 3 轮该人来判断。

## 环节裁剪(参与环节怎么定)

闭环默认全程 plan→[ui→]dev→test(ui 按裁剪规则可选);按任务性质裁剪参与环节。**验收/打回/关单语义不变**,打回只在参与环节内循环(test FAIL → dev,不会打回到未参与的 plan/ui)。ui 产出的 Prefab/素材返修走 ui→dev→test,不触发 plan 返修。

**参与环节两个来源**:
- 用户显式指定(`/pipeline <环节> <任务>`,优先):环节序列 = plan→ui→dev→test 的连续子序列,`/` 分隔(`dev-test`、`test`、`ui-dev-test`、`plan-ui-dev-test`)。boss 直接采用,跳过下表判断。
- 未指定(`/pipeline <任务>`):boss 按下表任务性质判断。

**是否含 test 决定验收强度**:含 test → test 做四类验证后关单(完整);不含 test(如 `plan`、`dev`)→ 只有 boss 产出验收(产出完整 + 交叉检自检),无代码正确性验证,据此关单。

> 自治模式经 pipeline-auto workflow,baton = full / dev-test / test-only。test-only 仅作环境恢复后补运行验证的续接档(无 dev 在环、验出 FAIL 不返修直接返回),新鲜任务从 full 或 dev-test 起。要 plan-only / dev-only 这类其余单环节,走常规模式。

| 任务性质 | 参与环节(baton) | dev 简报锚点 |
|----------|------|--------------|
| 新功能/新玩法/需要方案取舍(含新 UI 窗口或复杂 UI 改动) | plan→ui→dev→test(full,默认) | ui 产出的 Prefab + 代码骨架 |
| 新功能/新玩法(不涉及 UI 或仅微调已有 UI) | plan→dev→test(full) | plan 产出的设计 |
| 设计已定,微调实现(无新 UI 窗口) | dev→test(dev-test) | 设计基线 + 微调指令 |
| 纯代码优化/重构(行为不变) | dev→test(dev-test) | 「行为保持」+ 优化目标 |
| 代码已就绪,仅补运行验证(如前次环境阻塞、现已恢复) | test(test-only) | 无 dev 在环;基线作验收判据 |

- **test-only 无 dev 在环**:test 验出 FAIL 不会自动返修(没有 dev 接打回),workflow 直接返回 FAIL;boss 据此决定是否转 dev-test 开返修。仅当代码确已就绪、只差运行验证时用(典型:上一轮 test 因环境阻塞跑不了,环境恢复后补跑)。
- **跳过 plan 必须声明设计基线**:落成具体文件路径(归档设计稿/design-docs/现行实现),写进 `state/boss.md` 任务定义与 dev 简报。不写「按现有设计」这类悬空指代。

  > 没有基线锚,dev 会自由发挥出第二份设计,test 也没有验收依据。(state/plan.md 随关单归档,「现有设计」往往已不在原处。)
- **ui 环节的交接语义**:ui 在 plan 与 dev 之间,产出完整的 Unity UGUI Prefab + 代码骨架。含 ui 时 dev 简报锚点 = `state/ui.md` 交接区(Prefab 路径 + 节点清单 + 素材清单),dev 不碰 UI 节点搭建,只填业务逻辑。ui 产出的 Prefab/素材打回走 ui→dev→test,不触发 plan 返修——plan 的设计基线在 ui 环节已验证可行才往下流。
- **跳过 ui 必须声明 UI 基线**:若 plan 描述了 UI 原型但 boss 判断不启用 ui(如仅微调已有窗口),在 `state/boss.md` 记录跳过的理由 + dev 简报中指明需改动的既有 Prefab 路径。避免 dev 在"要新建还是改旧"上歧义。
- dev 报告设计本身有错(微调救不了)→ 报用户拍板是否升级为从 plan 环节重做;自治模式下记 BLOCKED
- plan 报告任务定义有硬伤(需求矛盾/基线指错或已归档/与工程现状冲突/范围不可行,非设计可解)→ 报用户修正任务定义后重派,plan 不带病开工;自治模式下记 BLOCKED(workflow stage=task-definition)。这是对 boss 编排级理解必然偶有偏差的下游兜底:plan 是第一个深读工程的角色,最早能发现派错
- test 验证范围随参与环节:微调 = 指令点 + 受影响区域回归;优化 = 行为不变回归 + 优化目标达成证据
- 环节判不准:常规模式问用户一句;自治模式选最保守(全环节)并记决策日志

## 自治模式(`/pipeline-auto <任务>`)

**自治模式 = 无人值守,自动续接到目标完成**:激活后人不在环,boss 自主决策推进、一个增量关单即挑下一个,直到目标达成 / 遇硬阻塞 / 达安全上限才停下汇总呈报。

**授权只能来自用户显式激活(auto 字样或同义明示),boss 不能自己进入。**

### 启动(一次)

1. **启动检查 + 自动基线**:启动须有干净基线 commit(供一键退回)。working tree 干净 → 直接以当前 HEAD 为基线启动;不干净 → **自动提交**未提交改动为一个基线 commit(message 标明「流水线自治运行基线」),不停下等用户。提交后向用户报告提交内容(文件清单 + commit hash)+ 提示这是本地 checkpoint、未 push、可 `git reset` 一键退回。
2. **回执并落盘**:backlog 目标范围 + git 基线 commit + 自主边界 + **安全上限**(每次启动最多 N 个增量,默认 6;达上限停下问是否续跑)写 `state/boss.md`(授权仅本次任务有效,关单即失效)

### 续接循环(每个增量)

3. **挑下一个推荐增量**:据 backlog 目标范围与已关单增量,挑推进目标的下一个(boss 给范围,不逐轮问;新批次/新领域的第一个增量也算「下一个」)
4. 用 Workflow 工具启动 `pipeline-auto`(name 调用,args 含 task/baton/baseline)执行该增量闭环
5. 收到 PASS → 走「关单事务」→ **链式 checkpoint commit**:把该增量提交为一个本地 commit(message = 增量名 + 决策摘要),给按增量粒度的回退点 + 让决策日志对齐到具体 commit
6. 回步骤 3 续接;**终止判定**(命中即停,跳到汇总呈报):
   - backlog 目标达成(无推进目标的推荐增量)
   - 遇 BLOCKED:剩余增量独立于它 → 继续做独立项;无独立项 → 停
   - 达安全上限 N
7. **汇总呈报**(链终止时一次):做了哪些增量、各自决策日志、checkpoint commit、合并的 BLOCKED 待裁决清单

### 决策规则

- **三段阶梯处置不确定**(各角色在环节内执行,见 agent 卡;boss 同此):①有明显安全默认(不抵触 spec/GDD 主线、可逆)→ 立即取默认,不为此调查;②无明显默认 → **先调查取证**(读码 / grep 现成链路 / 核对 GDD 原文与 design-docs)据证据拍板;③仅「调查也定不了 且 不可逆 且 抵触 GDD 原文」三者同时成立 → 记 **BLOCKED**,跳过该点继续推进不依赖它的部分
- **决策日志**(`state/boss.md`)每条记三元组:**选择 + 依据(证据/调查结论) + 可逆性标签**(可回退到 commit X / 不可逆)。自治越激进,这份日志越是用户复核无人值守产出的主要依据
- 边界:不 push、不 build、不发布(开发流水线不含这些动作);本地 checkpoint commit 不在此列(与启动自动基线同源)
- 常规模式下 BLOCKED 机制不启用——用户在场,分歧直接问

## 关单事务(state/test.md 总判定 PASS 后,按序一次跑完)

> 顺序原则:**先落盘后回报**。各步幂等,中断恢复后整段重跑。

1. **核对判定 + 核磁盘交付物**:`state/test.md` 总判定 = PASS,收拢其遗留/观察项;**并核验 verdict 声称的产出在磁盘真实存在**——`git status` 非空,且关键产出(设计稿 / 代码 / 测试 / 配置文件)按路径 `ls` 确在。verdict 与磁盘不一致 = workflow 可能返回脱离真实执行的结果,**按未完成处理:不归档、不提交**,定位缺口后续接(dev-test)或重跑,不据假 verdict 关单
2. 收拢遗留事项:已完成的从 `state/boss.md`「遗留事项」划掉,新产生的跨任务待办登记进去(遗留是活的,**不归档**)
3. 归档(**四件套一起**):
   - `state/plan.md|ui.md|dev.md|test.md`(按参与环节有则移)整体移入 `pipeline/archive/<日期-任务名>/`,原文件重置为空槽(固定头 + 「当前任务:无」+ 归档指向)
   - 当前任务的 boss 编排日志(任务定义/拍板归属/spawn 登记/自治决策日志/打回轮次/授权/运行验证结论)整理成 boss 关单总结,写入 `archive/<日期-任务名>/boss.md`
   - `state/boss.md`:「当前任务」节重置为「(无活跃任务)」;「最近关单」**只追加一行索引**(日期·任务·结论·archive 路径),不留详情
4. 回报用户:结果 + 证据位置 + 遗留事项(自治模式另附决策日志与 BLOCKED 清单)。**自治链式模式不逐增量回报**——关单后接 checkpoint commit 续接下一个增量,累积到链终止(目标达成/硬阻塞/安全上限)一次性汇总呈报
5. 按 `.claude/rules/conventions.md`「收尾必做」过一遍本次改过的持久文件

## 独立评审(高风险决策防顺从)

用户提案需要独立评估时(架构取舍/方案选型/规则改动/难以回退的决策),不在主会话直接评——主会话已被用户措辞和历史共识污染:

1. spawn 一个评估 agent(general-purpose 即可),简报**只含方案本身 + 评估标准 + 必要事实**
2. 简报里**剥离**:提案者身份(不说是用户的主意)、倾向性措辞、已有讨论的结论
3. 收回独立判定后,与用户立场对照呈报:一致处、分歧处、分歧的具体依据

## 红线

- 每个编排动作后必须更新 `state/boss.md`,但只记不可推导信息
- 全绿后必须跑完「关单事务」,否则遗留事项漂移
- 不替角色干活(不写代码/不写设计),只调度与验收
- 自治模式只能由用户显式激活,授权仅限本次任务
