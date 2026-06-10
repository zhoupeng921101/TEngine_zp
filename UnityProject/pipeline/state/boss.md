# 状态:Boss(编排日志)

> 被 clear/压缩后,boss 开工先读 roles/boss.md + 本文件,再用 subagents(action=list) 核对子会话。
> 任何编排动作后立刻更新本文件。

## 当前任务
复刻原版「收集玩法 collect」—— 方案 A:独立收集 demo 切片(不依赖完整关卡框架)。
范围:元素生成 + 收集计数 + collect 达标判定 + 简单 UI,单关 demo,挂接在现有 BlockBlast Classic 切片之上。

## 当前阶段
**第 3 棒:测试(进行中)** —— 开发已完成并经 boss 验收交接区(编译0报错+EditMode 77/77),已 spawn 测试做四类独立复验,等测试完成事件。

## 活跃子会话(clear 后靠这个接回)
| 棒 | taskName | childSessionKey | runId | 状态 |
|----|----------|-----------------|-------|------|
| 策划 | plan | agent:main:subagent:89760266-3741-4c59-9021-60fd498268e6 | d404ca2e-bcd5-462a-bb11-e92a33dc3f4b | 已完成 |
| 开发 | dev | agent:main:subagent:78903465-02f8-4e19-a268-53710a8ab610 | e13da92b-1008-42b4-bd46-703e4af84a50 | 已完成 |
| 测试 | test | agent:main:subagent:d045faa8-fa6d-4139-92b0-e3e8187726e5 | 59d9464d-0e0d-4267-822e-c3bc8d57e006 | 进行中 |

## 用户已拍板的 §3.3 决策(2026-06-10)
元素来源=A 纯候选块携带;窗口=A 独立 CollectDemoWindow;失败兜底=复用 GameOver;表现=glyph/纯色。详见 state/plan.md。

## 流水线进度
- [完成] 策划:`design-docs/08-collect-demo-slice.html` + 9 条验收标准(state/plan.md 交接区)
- [完成] 用户拍板 §3.3(全取策划建议)
- [完成] 开发:9 条验收逐条实现,CollectMode 门控,Classic 回归零变化;自检编译0报错+EditMode 77/77;交接区(改动摘要+文件清单+验证点)已就绪
- [进行中] 测试:四类独立复验(编译/单测/Play手验/CodeReview),重点 Classic 回归红线 + CollectMode off 零副作用

## 下一步
等测试完成事件 → 读 state/test.md 总判定。
- PASS → 回报用户(含 dev 标注的「需 Play 手验/热更出包需 HybridCLR」两条提醒)。
- FAIL → 把 state/test.md 的可复现清单回灌 dev session(sessions_send 到 dev childSessionKey)重修。

## dev 移交时标注的待办(回报用户时带上)
1. 拖拽/glyph/计数动画/胜利面板/Classic 回归 5 项交互需 Play 手验(MCP 难模拟拖拽)。
2. 正式出包需 HybridCLR 重生成热更 dll;无需 Luban。
3. UX 取舍:收集失败复用 GameOver,「PLAY AGAIN」回 Classic、SCORE 显 0——如不满意需单独排期。

## 待用户决策 / 风险
- 收集玩法原本依赖关卡框架(Unity 未做),已按方案 A 切成独立 demo 规避。
- 开发红线:CollectMode=off 时 Classic 行为必须零变化(回归硬验收,验收第 2 条)。
