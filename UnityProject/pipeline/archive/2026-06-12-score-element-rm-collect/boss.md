# Boss 关单总结:消除得分驱动元素生成 + 移除收集 demo(score-element-rm-collect)

> 关单时从 `state/boss.md`「当前任务」编排日志归档而来。冻结历史,不修改。

- **结论:PASS 交付**。plan→dev→test 三个环节走完,打回轮次 0。
- **需求①**:消除得分 → `ElementsForScore(k)`(A 档 `k=clamp(ceil(clearScore/200),1,4)`,无消除 0,积压封顶 12)→ 压 `PendingElements` 队列(轮转均摊)→ 补牌 FIFO 抽干填候选块(队空=纯方块)。旧概率注入/消除门控/保底计数器整套退役。计分公式抽到新增 `BlockScoring.cs`(Classic 与 merge-order 共用单一信息源)。
- **需求②**:收集 demo(prefab/cs/测试)删净,16 个收集专属符号全工程 0 匹配、无悬空引用;保留集(merge-order 元素携带基础设施 + 复用为通关面板的 `CollectWinWindow`)未牵连。
- **文件**:1 新增(`BlockScoring.cs`)+ 6 改(`MergeOrderConfig/State`、`BlockGameState`、`GameWindow`、`MergeOrderWindow`、`MergeOrderTests`)+ collect 删除,全在未提交 working tree。
- **运行验证(boss 经本会话 Unity MCP 补跑)**:编译 0 error、`BlockScoring.cs.meta` 已生成;EditMode 全量 **96/96 绿、0 失败**,无回归。
- **test 独立验收 PASS**:① Classic 计分逐数字不变(读 `git diff GameWindow.cs` 逐表达式判等价,GameWindow 仅 2 行改、落子/消除分均与原内联公式等价)②悔棋回滚队列 / off 零触 / 既有用例全绿 ③新增 7 用例断言均有实质 ④需求②移除净。
- **拍板(2026-06-12,用户)**:① 映射=A 档(`ScorePerElement=200/Min1/Max4/MaxPendingElements=12`);② 投放=补牌时 FIFO 抽入新候选块;③ 共享设施 `Collect*` 命名=保留+标注(正名列独立低优任务,即后续 collect-rename)。
- **dev/test 模型档**:dev=opus(改状态机核心补牌逻辑,有耦合);test=opus(无 Classic 计分专用单测,须读 `git diff` 做纯等价判定,真判断题)。
- **遗留**(活的待办仍在 `state/boss.md`「遗留事项」):#9(Play 拖拽手验,元素随得分目视确认)。
- **子会话登记**:

  | taskName | childSessionKey | runId |
  |----------|-----------------|-------|
  | plan-score-element-rm-collect | agent:main:subagent:37b270a7-f184-4349-ae28-f8112e67c323 | fe9c62e6-a084-47af-8d12-3eec94c17f70 |
  | dev-score-element-rm-collect | agent:main:subagent:fe59a8cd-828e-4ce4-8a81-59237d575e48 | 2aca8154-15d2-4b53-b25f-15e8161c3b8d |
  | test-score-element-rm-collect | agent:main:subagent:b0657719-0912-4a5d-8e5a-8014c83b9fd1 | 8935563a-2b12-4328-8772-4f68e3ce1166 |
