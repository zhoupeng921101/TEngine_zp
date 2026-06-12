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
//   baton:     'full' | 'dev-test'  棒次(默认 full;dev-test 必须给 baseline)
//   baseline:  string  设计基线文件路径(full 棒次时由 plan 产出覆盖)
//   batonNote: string  微调指令/优化目标(裁棒任务用,可空)
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
    decisions: { type: 'array', items: { type: 'string' }, description: '本棒自主拍板的取舍(自治审计用)' },
    blockers: { type: 'array', items: { type: 'string' }, description: '需用户裁决的方向性问题' },
  },
  required: ['summary', 'statePath', 'designDoc'],
}

const DEV_SCHEMA = {
  type: 'object',
  properties: {
    summary: { type: 'string', description: '一句话结论' },
    statePath: { type: 'string', description: '交接区路径' },
    decisions: { type: 'array', items: { type: 'string' }, description: '本棒自主拍板的取舍' },
    designFlaw: { type: 'string', description: '设计本身有错且微调救不了时填原因,否则省略' },
  },
  required: ['summary', 'statePath'],
}

const TEST_SCHEMA = {
  type: 'object',
  properties: {
    verdict: { type: 'string', enum: ['PASS', 'FAIL'], description: '总判定' },
    statePath: { type: 'string', description: '报告路径' },
    reason: { type: 'string', description: 'FAIL 主因一句话' },
    decisions: { type: 'array', items: { type: 'string' }, description: '本棒自主拍板的取舍' },
  },
  required: ['verdict', 'statePath'],
}

// args 防御:未传 args 或传成 JSON 字符串(而非对象)是已知易错点,显式报错好过深处 TypeError
if (!args || typeof args !== 'object' || !args.task) {
  return { status: 'BLOCKED', stage: 'launch', blocked: ['启动参数缺失或格式错:args 须为对象且含 task 字段(收到:' + (typeof args) + ')。正确示例:Workflow({name:"pipeline-auto", args:{task:"...", baton:"dev-test", baseline:"design-docs/xx.html"}})'], decisions: [] }
}

const decisions = []
const blocked = []

let baseline = args.baseline || ''

if ((args.baton || 'full') === 'full') {
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

let round = 0
let verdict = null
let devBrief = `任务:${args.task}\n设计基线:${baseline}${args.batonNote ? '\n附加指令:' + args.batonNote : ''}\n开工读 pipeline/state/dev.md、pipeline/memory/dev.md、pipeline/state/plan.md 交接区与设计基线;实现后编译自检,交接区写「改动摘要+文件清单+验证点」。${RETURN_NOTE}`

while (round < 3) {
  phase('开发')
  const dev = await agent(devBrief, devOpts)
  if (!dev) return { status: 'BLOCKED', stage: 'dev', blocked: blocked.concat(['dev agent 异常退出']), decisions, round }
  decisions.push(...(dev.decisions || []))
  if (dev.designFlaw) {
    blocked.push(`dev 报告设计缺陷(微调救不了):${dev.designFlaw}`)
    return { status: 'BLOCKED', stage: 'dev', blocked, decisions, round }
  }

  phase('测试')
  verdict = await agent(
    `被测任务:${args.task}\n开工读 pipeline/state/test.md、pipeline/memory/test.md 与 pipeline/state/dev.md 交接区;按角色卡四类验证执行,报告写 pipeline/state/test.md。验收判据:${baseline}。${RETURN_NOTE}`,
    testOpts
  )
  if (!verdict) return { status: 'BLOCKED', stage: 'test', blocked: blocked.concat(['test agent 异常退出']), decisions, round }
  decisions.push(...(verdict.decisions || []))
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
