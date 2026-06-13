export const meta = {
  name: 'pipeline-auto',
  description: 'TEngine_block 流水线自治模式:plan→dev→test 闭环,3 轮打回熔断,返回 PASS/BLOCKED',
  whenToUse: '仅当用户显式激活自治模式(/pipeline auto)时由 boss 启动;常规编排走 pipeline skill,不走本脚本',
  phases: [
    { title: '策划' },
    { title: '开发' },
    { title: '测试' },
  ],
}

// args: {
//   task:      string  任务描述(self-contained,boss 现写)
//   baton:     'full' | 'dev-test' | 'test-only'  参与环节(默认 full)
//                full      = plan→dev→test 全程,plan 产出 baseline
//                dev-test  = 跳 plan,dev→test 打回循环,须给 baseline
//                test-only = 只跑 test 一轮(代码已就绪、仅补运行验证;无 dev 在环不返修),须给 baseline
//   baseline:  string  设计基线文件路径(full 由 plan 产出覆盖;dev-test / test-only 必须传)
//   batonNote: string  微调指令/优化目标(裁剪环节的任务用,可空)
//   devModel / testModel: 'opus' | 'sonnet'  模型档(boss 按 SKILL.md 选档表传;缺省继承会话模型)
// }
// 打回每轮 spawn 新 dev(workflow 内无续接),上下文靠 state 文件交接——文件是真相。
// 自主决策由各角色在返回值 decisions 里上报,boss 收尾统一写 state/boss.md 决策日志。

const RETURN_NOTE = '详细产出写对应 state 文件;结构化返回只填摘要字段,不长篇复述。自主拍板的取舍填 decisions;拿不准/疑似方向问题填 blockers。'

const PLAN_SCHEMA = {
  type: 'object',
  properties: {
    summary: { type: 'string', description: '一句话结论' },
    statePath: { type: 'string', description: '交接区路径' },
    designDoc: { type: 'string', description: '设计稿路径(design-docs/xx.html)' },
    decisions: { type: 'array', items: { type: 'string' }, description: '本环节自主拍板的取舍(自治审计用)' },
    blockers: { type: 'array', items: { type: 'string' }, description: '需用户裁决的方向性问题' },
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

const TEST_SCHEMA = {
  type: 'object',
  properties: {
    verdict: { type: 'string', enum: ['PASS', 'FAIL', 'BLOCKED'], description: '总判定。BLOCKED=环境阻塞(Unity MCP/编辑器不可达等)致运行验证跑不了,非代码缺陷——代码缺陷一律 FAIL' },
    statePath: { type: 'string', description: '报告路径' },
    reason: { type: 'string', description: 'FAIL/BLOCKED 主因一句话' },
    decisions: { type: 'array', items: { type: 'string' }, description: '本环节自主拍板的取舍' },
  },
  required: ['verdict', 'statePath'],
}

// args 防御:runner 可能把 args 序列化成 JSON 字符串送达,先尝试解析回对象;再校验必填字段
if (typeof args === 'string') {
  try { args = JSON.parse(args) } catch (e) { /* 解析失败则维持字符串,下方校验拦截 */ }
}
if (!args || typeof args !== 'object' || !args.task) {
  return { status: 'BLOCKED', stage: 'launch', blocked: ['启动参数缺失或格式错:args 须为对象且含 task 字段(收到:' + (typeof args) + ')。正确示例:Workflow({name:"pipeline-auto", args:{task:"...", baton:"dev-test", baseline:"design-docs/xx.html"}})'], decisions: [] }
}

const baton = args.baton || 'full'
if (!['full', 'dev-test', 'test-only'].includes(baton)) {
  return { status: 'BLOCKED', stage: 'launch', blocked: [`baton 取值非法(收到:${baton});合法值:full / dev-test / test-only`], decisions: [] }
}
if ((baton === 'dev-test' || baton === 'test-only') && !args.baseline) {
  return { status: 'BLOCKED', stage: 'launch', blocked: [`baton=${baton} 必须提供 baseline(设计基线路径),作验收判据`], decisions: [] }
}

const decisions = []
const blocked = []

let baseline = args.baseline || ''

if (baton === 'full') {
  phase('策划')
  const plan = await agent(
    `任务:${args.task}\n开工读 pipeline/state/plan.md 与 pipeline/memory/plan.md;产出设计稿(design-docs/)与验收标准(写交接区)。${RETURN_NOTE}`,
    { agentType: 'pipeline-plan', phase: '策划', schema: PLAN_SCHEMA, model: 'opus' }
  )
  if (!plan) return { status: 'BLOCKED', stage: 'plan', blocked: ['plan agent 异常退出'], decisions }
  decisions.push(...(plan.decisions || []))
  if (plan.blockers && plan.blockers.length) {
    // 设计层方向性问题不带病推进:直接停,攒给用户
    blocked.push(...plan.blockers)
    return { status: 'BLOCKED', stage: 'plan', blocked, decisions }
  }
  baseline = plan.designDoc
  log(`策划完成:${plan.summary}`)
}

const devOpts = { agentType: 'pipeline-dev', phase: '开发', schema: DEV_SCHEMA }
if (args.devModel) devOpts.model = args.devModel
const testOpts = { agentType: 'pipeline-test', phase: '测试', schema: TEST_SCHEMA }
if (args.testModel) testOpts.model = args.testModel

// MCP 桥在 Unity 域重载时可能拆掉连接,socket close 打断 agent 收尾返回(2026-06-13 实测:test 已跑完
// 129/129、写完报告后断连,agent 返回 null,workflow 误判 BLOCKED)。触碰 Unity 的环节(dev 编译自检 /
// test 运行验证)统一套一层重试:返回 null 即重试一次,并提示重跑 agent 先读已写入的 state 文件,有结
// 果则复用、不重复全量;两次都失败再交上层判定。
async function withReconnectRetry(prompt, opts, statePath) {
  let r = await agent(prompt, opts)
  if (!r) {
    log(`${opts.phase} agent 异常退出,重试一次(疑似 Unity 域重载致 MCP 桥断连)`)
    r = await agent(
      prompt + `\n注:上一次本环节 agent 在返回前异常退出(疑似 MCP 桥断连)。先读 ${statePath},若已有本次结果则据此直接产出结构化返回,不重复跑全量验证。`,
      opts
    )
  }
  return r
}

const testBrief = `被测任务:${args.task}\n开工读 pipeline/state/test.md、pipeline/memory/test.md 与 pipeline/state/dev.md 交接区;按角色卡四类验证执行。验收判据:${baseline}。判定三态:代码缺陷=FAIL;环境阻塞(MCP/编辑器不可达,运行验证跑不了)=BLOCKED,勿判 FAIL。**先把报告写入 pipeline/state/test.md 持久保存,再返回结构化结果**——返回阶段若遇 MCP 桥断连,已写入的报告可被重试 agent 复用。${RETURN_NOTE}`

// test-only:代码已就绪,只补运行验证。无 dev 在环,不进打回循环;按 verdict 直接定结果。
if (baton === 'test-only') {
  phase('测试')
  const verdict = await withReconnectRetry(testBrief, testOpts, 'pipeline/state/test.md')
  if (!verdict) return { status: 'BLOCKED', stage: 'test', blocked: blocked.concat(['test agent 两次异常退出(疑似 MCP 桥断连);验证可能已完成,人工查 pipeline/state/test.md 确认']), decisions }
  decisions.push(...(verdict.decisions || []))
  if (verdict.verdict === 'PASS') return { status: 'PASS', stage: 'test-only', blocked, decisions }
  if (verdict.verdict === 'BLOCKED') {
    blocked.push(`环境阻塞(非代码缺陷):${verdict.reason || '见 pipeline/state/test.md'}`)
    return { status: 'BLOCKED', stage: 'environment', blocked, decisions }
  }
  // FAIL:test-only 无 dev 在环返修,直接报代码缺陷,boss 决定是否转 dev-test
  blocked.push(`test-only 验出代码缺陷(无 dev 在环返修):${verdict.reason || '见 pipeline/state/test.md'}`)
  return { status: 'FAIL', stage: 'test-only', blocked, decisions }
}

let round = 0
let verdict = null
let devBrief = `任务:${args.task}\n设计基线:${baseline}${args.batonNote ? '\n附加指令:' + args.batonNote : ''}\n开工读 pipeline/state/dev.md、pipeline/memory/dev.md、pipeline/state/plan.md 交接区与设计基线;实现后编译自检,交接区写「改动摘要+文件清单+验证点」。${RETURN_NOTE}`

while (round < 3) {
  phase('开发')
  const dev = await withReconnectRetry(devBrief, devOpts, 'pipeline/state/dev.md')
  if (!dev) return { status: 'BLOCKED', stage: 'dev', blocked: blocked.concat(['dev agent 两次异常退出(疑似 MCP 桥断连);人工查 pipeline/state/dev.md 确认']), decisions, round }
  decisions.push(...(dev.decisions || []))
  if (dev.designFlaw) {
    blocked.push(`dev 报告设计缺陷(微调救不了):${dev.designFlaw}`)
    return { status: 'BLOCKED', stage: 'dev', blocked, decisions, round }
  }

  phase('测试')
  verdict = await withReconnectRetry(testBrief, testOpts, 'pipeline/state/test.md')
  if (!verdict) return { status: 'BLOCKED', stage: 'test', blocked: blocked.concat(['test agent 两次异常退出(疑似 MCP 桥断连);验证可能已完成,人工查 pipeline/state/test.md 确认']), decisions, round }
  decisions.push(...(verdict.decisions || []))
  if (verdict.verdict === 'BLOCKED') {
    // 环境型阻塞:dev 无可修,打回只会空转烧轮次(2026-06-13 core-loop 3 轮实测)——直接结束呈报,不计打回
    blocked.push(`环境阻塞(非代码缺陷):${verdict.reason || '见 pipeline/state/test.md'}`)
    return { status: 'BLOCKED', stage: 'environment', blocked, decisions, round }
  }
  if (verdict.verdict === 'PASS') break

  round++
  log(`第 ${round} 轮打回:${verdict.reason || '见 pipeline/state/test.md'}`)
  devBrief = `打回返修(第 ${round} 轮)。任务:${args.task}\n设计基线:${baseline}\n上一轮 dev 自述:${dev.summary}\n开工读 pipeline/state/test.md 的可复现清单与 pipeline/state/dev.md 既有交接区;修复后编译自检,更新交接区。${RETURN_NOTE}`
}

if (!verdict || verdict.verdict !== 'PASS') {
  blocked.push(`3 轮熔断:${(verdict && verdict.reason) || '见 pipeline/state/test.md'}`)
  return { status: 'BLOCKED', stage: 'circuit-breaker', blocked, decisions, round }
}

return { status: 'PASS', round, blocked, decisions }
