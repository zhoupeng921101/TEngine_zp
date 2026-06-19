export const meta = {
  name: 'pipeline-auto',
  description: 'TEngine_block 流水线自治模式:plan→dev→test 闭环,3 轮打回熔断,返回 PASS/FAIL/BLOCKED(FAIL 仅 test-only 档:无 dev 在环不返修)。target=client(默认)走客户端角色对;target=server 走服务端角色对(pipeline-server-dev/test,工具链 dotnet、知识库 fantasy-net)',
  whenToUse: '仅当用户显式激活自治模式(/pipeline-auto)时由 boss 启动;常规编排走 pipeline skill,不走本脚本',
  phases: [
    { title: '策划' },
    { title: '开发' },
    { title: '测试' },
  ],
}

// args: {
//   task:      string  任务描述(self-contained,boss 现写)
//   target:    'client' | 'server'  端(默认 client)
//                client = 客户端角色对(pipeline-dev / pipeline-test,Unity MCP 工具链)
//                server = 服务端角色对(pipeline-server-dev / pipeline-server-test,dotnet 工具链 + fantasy-net 知识库)
//                plan 环节两端共用 pipeline-plan(设计 code-blind、与端无关)
//   baton:     'full' | 'dev-test' | 'test-only'  参与环节(默认 full)
//                full      = plan→dev→test 全程,plan 产出 baseline
//                dev-test  = 跳 plan,dev→test 打回循环,须给 baseline
//                test-only = 只跑 test 一轮(代码已就绪、仅补运行验证;无 dev 在环不返修),须给 baseline
//   baseline:  string  设计基线文件路径(full 由 plan 产出覆盖;dev-test / test-only 必须传)
//   batonNote: string  微调指令/优化目标(裁剪环节的任务用,可空)
//   feasibilityCheck: string[]  boss 判高风险/接法存疑时传入的存疑接缝 + 待验证问题(转 dev 前跑只读可行性预检;空/缺省则跳过)。
//                由 boss 判定触发(对齐 SKILL「可行性预检」),不由 plan 产出——plan code-blind、不产代码接缝
//   devModel / testModel: 'opus' | 'sonnet'  可选覆盖;缺省用 agent 卡 frontmatter 的 model 档(plan 固定 opus,见下 spawn)
// }
// 打回每轮 spawn 新 dev(workflow 内无续接),上下文靠 state 文件交接——文件是真相。
// 自主决策由各角色在返回值 decisions 里上报,boss 收尾统一写 state/boss.md 决策日志。

const RETURN_NOTE = '详细产出写对应 state 文件;结构化返回只填摘要字段,不长篇复述。遇不确定按三段阶梯处置:①有明显安全默认(不抵触 spec/GDD 主线、可逆)→ 立即取默认、记 decisions,不为此调查;②无明显默认 → 先调查取证(读相关代码 / grep 现成链路 / 核对 GDD 原文与现有 design-docs),据证据定最优解、记 decisions 并写明依据;③仅当调查也定不了、且不可逆、且抵触 GDD 原文,三者同时成立 → 填 blockers。blockers 非空会中止本环节、攒给用户,故只留给这第三类。'

const PLAN_SCHEMA = {
  type: 'object',
  properties: {
    summary: { type: 'string', description: '一句话结论' },
    statePath: { type: 'string', description: '交接区路径' },
    designDoc: { type: 'string', description: '设计稿路径(design-docs/xx.html)' },
    decisions: { type: 'array', items: { type: 'string' }, description: '本环节自主拍板的取舍(自治审计用)' },
    blockers: { type: 'array', items: { type: 'string' }, description: '必须用户裁决才能推进的方向问题:调查取证也定不了、且不可逆、且抵触 GDD 原文,三者同时成立才填。有安全默认、或调查后能据证据拍板的(含范围开关)走 decisions 默认推进,不进此处——blockers 非空会令流水线在 plan 环节中止' },
    taskFlaw: { type: 'string', description: 'boss 派的任务定义本身有硬伤(需求矛盾/基线指错/与工程现状冲突/范围不可行)且非设计可解时填原因,否则省略——对称 dev.designFlaw' },
  },
  required: ['summary', 'statePath', 'designDoc'],
}

const DEV_SCHEMA = {
  type: 'object',
  properties: {
    summary: { type: 'string', description: '一句话结论' },
    statePath: { type: 'string', description: '交接区路径' },
    decisions: { type: 'array', items: { type: 'string' }, description: '本环节自主拍板的取舍' },
    designFlaw: { type: 'string', description: '设计本身有错且微调救不了时填原因,否则省略' },
  },
  required: ['summary', 'statePath'],
}

const FEASIBILITY_SCHEMA = {
  type: 'object',
  properties: {
    feasible: { type: 'boolean', description: '能否按设计接线' },
    effort: { type: 'string', description: '粗略工作量档:小 / 中 / 大' },
    note: { type: 'string', description: '现成可复用链路 / 关键判断依据' },
    altApproach: { type: 'string', description: '若不可行,可行的替代接法(供 plan 调整);可行则省略' },
  },
  required: ['feasible'],
}

const TEST_SCHEMA = {
  type: 'object',
  properties: {
    verdict: { type: 'string', enum: ['PASS', 'FAIL', 'BLOCKED'], description: '总判定。BLOCKED=环境阻塞(客户端:Unity MCP/编辑器不可达;服务端:跑不动服/MongoDB 不可达/端口占用)致运行验证跑不了,非代码缺陷——代码缺陷一律 FAIL' },
    statePath: { type: 'string', description: '报告路径' },
    reason: { type: 'string', description: 'FAIL/BLOCKED 主因一句话' },
    decisions: { type: 'array', items: { type: 'string' }, description: '本环节自主拍板的取舍' },
  },
  required: ['verdict', 'statePath'],
}

const DIAGNOSIS_SCHEMA = {
  type: 'object',
  properties: {
    category: { type: 'string', enum: ['design-flaw', 'dev-misread', 'flaky-or-environment'], description: '3 轮不收敛的根因归类:设计缺陷 / dev 持续误读 / flaky 或环境' },
    rationale: { type: 'string', description: '归类依据(历轮可复现清单与 diff 的共性)' },
    recommendation: { type: 'string', description: '建议下一步(回 plan 调设计 / 换 dev 接法重做 / 人工查环境)' },
  },
  required: ['category', 'recommendation'],
}

// args 防御:runner 可能把 args 序列化成 JSON 字符串送达,先尝试解析回对象;再校验必填字段
if (typeof args === 'string') {
  try { args = JSON.parse(args) } catch (e) { /* 解析失败则维持字符串,下方校验拦截 */ }
}
if (!args || typeof args !== 'object' || !args.task) {
  return { status: 'BLOCKED', stage: 'launch', blocked: ['启动参数缺失或格式错:args 须为对象且含 task 字段(收到:' + (typeof args) + ')。正确示例:Workflow({name:"pipeline-auto", args:{task:"...", target:"server", baton:"dev-test", baseline:"design-docs/xx.html"}})'], decisions: [] }
}

const baton = args.baton || 'full'
if (!['full', 'dev-test', 'test-only'].includes(baton)) {
  return { status: 'BLOCKED', stage: 'launch', blocked: [`baton 取值非法(收到:${baton});合法值:full / dev-test / test-only`], decisions: [] }
}
if ((baton === 'dev-test' || baton === 'test-only') && !args.baseline) {
  return { status: 'BLOCKED', stage: 'launch', blocked: [`baton=${baton} 必须提供 baseline(设计基线路径),作验收判据`], decisions: [] }
}

// target:选客户端 / 服务端角色对。plan 两端共用 pipeline-plan;dev/test 与其 state/memory 路径按端切换。
const target = args.target || 'client'
if (!['client', 'server'].includes(target)) {
  return { status: 'BLOCKED', stage: 'launch', blocked: [`target 取值非法(收到:${target});合法值:client / server`], decisions: [] }
}
const isServer = target === 'server'
const devAgentType  = isServer ? 'pipeline-server-dev'  : 'pipeline-dev'
const testAgentType = isServer ? 'pipeline-server-test' : 'pipeline-test'
const devStatePath  = isServer ? 'pipeline/state/server-dev.md'  : 'pipeline/state/dev.md'
const testStatePath = isServer ? 'pipeline/state/server-test.md' : 'pipeline/state/test.md'
const devMemPath    = isServer ? 'pipeline/memory/server-dev.md'  : 'pipeline/memory/dev.md'
const testMemPath   = isServer ? 'pipeline/memory/server-test.md' : 'pipeline/memory/test.md'

const decisions = []
const blocked = []

let baseline = args.baseline || ''
let feasibilityNote = ''

// 看门狗:给每个 agent() 调用套超时竞速,把「静默挂起(永不返回)」转成「超时→null」,复用既有
// null-死亡处理(retry 一次→仍超时则 BLOCKED + 通知),不再无限等(2026-06-19 实测 server-dev
// 静默挂起 9.5h 无人知、harness 还把任务回收成无主僵尸)。超时是死亡的一种,走同一恢复路径。
// 沙箱若无 setTimeout 则退回无超时(best-effort,绝不因看门狗本身破坏流程)。默认 45min/调用
// (远低于挂起阈、远高于正常单轮);boss 可经 args.agentTimeoutMin 覆盖。超时不杀孤儿 agent(沙箱内
// 杀不了),只停止等待并走 retry;孤儿若后续完成会写 state,retry agent 读 state 可复用。
const AGENT_TIMEOUT_MS = Math.max(5, Number(args.agentTimeoutMin) || 45) * 60 * 1000
function withTimeout(p, label) {
  if (typeof setTimeout !== 'function') return p
  return Promise.race([
    p,
    new Promise(resolve => setTimeout(() => {
      log(`${label} 超时 ${Math.round(AGENT_TIMEOUT_MS / 60000)}min 未返回,判疑似挂起(按 null-死亡处理,走 retry/BLOCKED)`)
      resolve(null)
    }, AGENT_TIMEOUT_MS)),
  ])
}

if (baton === 'full') {
  phase('策划')
  const plan = await withTimeout(agent(
    `任务:${args.task}\n开工读 pipeline/state/plan.md 与 pipeline/memory/plan.md;产出设计稿(design-docs/)与验收标准(写交接区)。${RETURN_NOTE}`,
    { agentType: 'pipeline-plan', phase: '策划', schema: PLAN_SCHEMA, model: 'opus' }
  ), '策划')
  if (!plan) return { status: 'BLOCKED', stage: 'plan', blocked: ['plan agent 异常退出'], decisions }
  decisions.push(...(plan.decisions || []))
  if (plan.taskFlaw) {
    // 任务定义本身有硬伤(boss 派错):退回用户,设计无从谈起。对称 dev.designFlaw
    blocked.push(`plan 报告任务定义缺陷(非设计可解):${plan.taskFlaw}`)
    return { status: 'BLOCKED', stage: 'task-definition', blocked, decisions }
  }
  if (plan.blockers && plan.blockers.length) {
    // 设计层方向性问题不带病推进:直接停,攒给用户
    blocked.push(...plan.blockers)
    return { status: 'BLOCKED', stage: 'plan', blocked, decisions }
  }
  baseline = plan.designDoc
  log(`策划完成:${plan.summary}`)
}

const devOpts = { agentType: devAgentType, phase: '开发', schema: DEV_SCHEMA }
if (args.devModel) devOpts.model = args.devModel
const testOpts = { agentType: testAgentType, phase: '测试', schema: TEST_SCHEMA }
if (args.testModel) testOpts.model = args.testModel

// 触碰外部运行时(客户端 Unity MCP / 服务端 dotnet 跑服)的环节,agent 可能在收尾返回前异常退出
// (实测:客户端 MCP 桥在 Unity 域重载时拆连,test 已跑完却返回 null,workflow 误判 BLOCKED)。统一套一层
// 重试:返回 null 即重试一次,并提示重跑 agent 先读已写入的 state 文件,有结果则复用、不重复全量;两次都失
// 败再交上层判定。对服务端 agent 同样适用(null 重试无害)。
async function withReconnectRetry(prompt, opts, statePath) {
  let r = await withTimeout(agent(prompt, opts), opts.label || opts.phase)
  if (!r) {
    log(`${opts.phase} agent 异常退出/超时,重试一次(疑似连接中断或挂起)`)
    r = await withTimeout(agent(
      prompt + `\n注:上一次本环节 agent 在返回前异常退出/超时。先读 ${statePath},若已有本次结果则据此直接产出结构化返回,不重复跑全量验证。`,
      opts
    ), opts.label || opts.phase)
  }
  return r
}

const testBrief = `被测任务:${args.task}\n开工读 ${testStatePath}、${testMemPath} 与 ${devStatePath} 交接区;按角色卡四类验证执行。验收判据:${baseline}。判定三态:代码缺陷=FAIL;环境阻塞(运行验证跑不了)=BLOCKED,勿判 FAIL。**先把报告写入 ${testStatePath} 持久保存,再返回结构化结果**——返回阶段若遇连接中断,已写入的报告可被重试 agent 复用。${RETURN_NOTE}`

// test-only:代码已就绪,只补运行验证。无 dev 在环,不进打回循环;按 verdict 直接定结果。
if (baton === 'test-only') {
  phase('测试')
  const verdict = await withReconnectRetry(testBrief, testOpts, testStatePath)
  if (!verdict) return { status: 'BLOCKED', stage: 'test', blocked: blocked.concat([`test agent 两次异常退出;验证可能已完成,人工查 ${testStatePath} 确认`]), decisions }
  decisions.push(...(verdict.decisions || []))
  if (verdict.verdict === 'PASS') return { status: 'PASS', stage: 'test-only', blocked, decisions }
  if (verdict.verdict === 'BLOCKED') {
    blocked.push(`环境阻塞(非代码缺陷):${verdict.reason || '见 ' + testStatePath}`)
    return { status: 'BLOCKED', stage: 'environment', blocked, decisions }
  }
  // FAIL:test-only 无 dev 在环返修,直接报代码缺陷,boss 决定是否转 dev-test
  blocked.push(`test-only 验出代码缺陷(无 dev 在环返修):${verdict.reason || '见 ' + testStatePath}`)
  return { status: 'FAIL', stage: 'test-only', blocked, decisions }
}

// 可行性预检:boss 判高风险/接法存疑时经 args.feasibilityCheck 传入存疑接缝,转 dev 前 dev 只读评估接线可行性,
// 避免按错接法实现一整轮再报 designFlaw 返工。由 boss 判定触发(对齐 SKILL「可行性预检」),不依赖 plan 产字段
// (plan code-blind、不产代码接缝)。空/缺省则跳过;full / dev-test 均适用(test-only 已在上方返回)。
if (args.feasibilityCheck && args.feasibilityCheck.length) {
  phase('开发')
  const precheckOpts = { agentType: devAgentType, phase: '开发', schema: FEASIBILITY_SCHEMA, label: '可行性预检' }
  if (args.devModel) precheckOpts.model = args.devModel
  const precheck = await withTimeout(agent(
    `可行性预检(只读评估,不实现、不碰工程、不进交接区)。任务:${args.task}\n设计基线:${baseline}\n存疑接缝与待验证问题:\n${args.feasibilityCheck.join('\n')}\n判定:能否按设计接线、粗略工作量(小/中/大)、有无现成链路可复用;不可行则给可行的替代接法。`,
    precheckOpts
  ), '可行性预检')
  if (!precheck) return { status: 'BLOCKED', stage: 'feasibility', blocked: blocked.concat(['可行性预检 agent 异常退出']), decisions }
  if (!precheck.feasible) {
    // 接法不可行 = 提前发现的 designFlaw:不进 dev 空耗,带替代接法停,供 boss/plan 调整后重派
    blocked.push(`可行性预检不通过:${precheck.note || '接法不可行'}${precheck.altApproach ? ';替代接法:' + precheck.altApproach : ''}`)
    return { status: 'BLOCKED', stage: 'feasibility', blocked, decisions }
  }
  feasibilityNote = `通过(工作量 ${precheck.effort || '未估'})${precheck.note ? ',' + precheck.note : ''}`
  decisions.push(`可行性预检${feasibilityNote}`)
}

let round = 0
let verdict = null
let devBrief = `任务:${args.task}\n设计基线:${baseline}${feasibilityNote ? '\n可行性预检回执:' + feasibilityNote : ''}${args.batonNote ? '\n附加指令:' + args.batonNote : ''}\n开工读 ${devStatePath}、${devMemPath}、pipeline/state/plan.md 交接区与设计基线;实现后编译自检,交接区写「改动摘要+文件清单+验证点」。${RETURN_NOTE}`

while (round < 3) {
  phase('开发')
  const dev = await withReconnectRetry(devBrief, devOpts, devStatePath)
  if (!dev) return { status: 'BLOCKED', stage: 'dev', blocked: blocked.concat([`dev agent 两次异常退出;人工查 ${devStatePath} 确认`]), decisions, round }
  decisions.push(...(dev.decisions || []))
  if (dev.designFlaw) {
    blocked.push(`dev 报告设计缺陷(微调救不了):${dev.designFlaw}`)
    return { status: 'BLOCKED', stage: 'dev', blocked, decisions, round }
  }

  phase('测试')
  verdict = await withReconnectRetry(testBrief, testOpts, testStatePath)
  if (!verdict) return { status: 'BLOCKED', stage: 'test', blocked: blocked.concat([`test agent 两次异常退出;验证可能已完成,人工查 ${testStatePath} 确认`]), decisions, round }
  decisions.push(...(verdict.decisions || []))
  if (verdict.verdict === 'BLOCKED') {
    // 环境型阻塞:dev 无可修,打回只会空转、白费轮次(2026-06-13 core-loop 3 轮实测)——直接结束呈报,不计打回
    blocked.push(`环境阻塞(非代码缺陷):${verdict.reason || '见 ' + testStatePath}`)
    return { status: 'BLOCKED', stage: 'environment', blocked, decisions, round }
  }
  if (verdict.verdict === 'PASS') break

  round++
  log(`第 ${round} 轮打回:${verdict.reason || '见 ' + testStatePath}`)
  devBrief = `打回返修(第 ${round} 轮)。任务:${args.task}\n设计基线:${baseline}\n上一轮 dev 自述:${dev.summary}\n开工读 ${testStatePath} 的可复现清单与 ${devStatePath} 既有交接区;修复后编译自检,更新交接区。${RETURN_NOTE}`
}

if (!verdict || verdict.verdict !== 'PASS') {
  // 3 轮仍不过:先做一次只读根因诊断,让 BLOCKED 报告可执行(供链式 boss 判断剩余增量能否继续),不自动 re-route
  const diagOpts = { agentType: devAgentType, phase: '测试', schema: DIAGNOSIS_SCHEMA, label: '熔断根因诊断' }
  if (args.devModel) diagOpts.model = args.devModel
  const diag = await withTimeout(agent(
    `熔断根因诊断(只读评估,不改工程、不进交接区)。3 轮返修后 test 仍未过。任务:${args.task}\n设计基线:${baseline}\n读 ${testStatePath} 历轮可复现清单、${devStatePath} 交接区与 git diff,归类不收敛根因(design-flaw 设计缺陷 / dev-misread dev 持续误读 / flaky-or-environment)并给建议下一步。`,
    diagOpts
  ), '熔断根因诊断')
  const diagNote = diag ? `;根因诊断=${diag.category}:${diag.recommendation}` : ';根因诊断 agent 异常退出,人工查 pipeline/state'
  blocked.push(`3 轮熔断:${(verdict && verdict.reason) || '见 ' + testStatePath}${diagNote}`)
  return { status: 'BLOCKED', stage: 'circuit-breaker', blocked, decisions, round }
}

return { status: 'PASS', round, blocked, decisions }
