# Boss 关单总结:收集玩法 collect — 独立 demo 切片(方案 A)

> 关单时从 `state/boss.md`「当前任务」编排日志归档而来。冻结历史,不修改。

- **结论:PASS 交付**。策划→开发→测试三个环节走完,测试四类验证全过(本目录 test.md,77/77 单测 + 5 张截图证据,`Assets/Screenshots/collect_0*.png`)。
- 拍板记录(2026-06-10,§3.3):元素来源=A 纯候选块携带;窗口=A 独立 CollectDemoWindow;失败兜底=复用 GameOver;表现=glyph/纯色。
- 打回轮次:0(一次通过)。
- **子会话登记**(均已结束,留档供追溯):

  | taskName | childSessionKey | runId |
  |----------|-----------------|-------|
  | plan | agent:main:subagent:89760266-3741-4c59-9021-60fd498268e6 | d404ca2e-bcd5-462a-bb11-e92a33dc3f4b |
  | dev | agent:main:subagent:78903465-02f8-4e19-a268-53710a8ab610 | e13da92b-1008-42b4-bd46-703e4af84a50 |
  | test | agent:main:subagent:d045faa8-fa6d-4139-92b0-e3e8187726e5 | 59d9464d-0e0d-4267-822e-c3bc8d57e006 |
