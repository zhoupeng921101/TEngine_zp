# 关单总结 · no-rounds 增量① 无尽核心

- **任务**:`gameplay-no-rounds-infinite` 增量① 无尽核心(client,baton=dev-test)
- **日期**:2026-06-22
- **判定**:PASS(round 0 一次过)
- **git 基线**:`6c1978fe`(前置美术料/工具基线)

## 范围(本刀五件 + 持久化两字段)

落地「无局·无尽」模型(设计基线 `design-docs/49-infinite-no-rounds.md`,plan 已于 `19303cdc`+`7a8441d1` 提交)的无尽核心:
1. 删通关(`IsDemoComplete`/`DemoGoalOrders`/`TriggerWin`)。
2. 删软/硬 GameOver(无处可落 / 体力耗尽都不结束、不弹面板;删 `_finished` 门控)。
3. 时基恢复(含离线):实装 `RegenPerTick`/`RegenIntervalSec`,无条件纯时间驱动 + 离线真实时差补算 + 余秒留存;负时差/首次无记录有兜底。
4. 消除道具:清「一行+一列」、代价体力 `ClearToolCost=25`(≤ `EnergyCap=30`)、无限可用只 gate 体力;做成体力 gate 的棋盘操作(非持有消耗品);按钮挂 MergeOrderWindow + `BlockBoardTapper` 指定格捕获。
5. 47/48/26 触发源善后:删 GameOver/Win 后 `AccumulatePlayCount` hook 随之删;活动基建/结算窗代码保留编译绿、不触发。
- 持久化:仅体力 + 「上次恢复时刻」进盘(CurrentVersion 不升);整盘续存留增量②。

## 验证(test 四类)

- 编译 0 error;EditMode 550/550(14 新增 + 5 翻转用例);Code Review 三条硬约束 + TEngine 红线全绿。
- Play 手验 BLOCKED:MCP 不支持拖拽 + arming overlay 点击,逻辑层已单测覆盖,表现/交互层留人工冒烟(非 FAIL)。

## 关键决策

- 消除道具不走 ItemBag 持有计数(设计49「不要任何持有道具」)= 体力 gate 棋盘操作。
- 体力进盘只动两字段、CurrentVersion 不升(平铺,旧档缺省兜底);无记录档(lastEnergyRegenTime≤0)体力夹回 `EnergyStart`,不信缺省 0。
- 时基恢复纯方法注入 now、只推进整除秒数(余秒留存防短间隔吞零头)。

## 遗留 / follow-up(转 boss carry-forward)

1. **「累计游戏 N 局」活动(AccumulatePlayCount,设计47/48)无可达触发源**:无尽删「局」后唯一玩法内触发已删,Classic 入口又已下线。基建保留编译绿不触发,**新语义待 boss/plan 重定**(每完成 N 单 / 每用消除道具 / 废弃)。
2. **CompletedOrders/TotalScore 生涯统计进盘**:设计49 §五 O4 列为增强项,本刀未实装(仍每会话归 0),增量② 整盘续存时一并定。
3. **GameOverWindow/MergeOrderWinWindow「重试→MergeOrderWindow」回调**在无尽下语义模糊,代码保留未动(Classic 仍用 GameOverWindow,R1/R4 绿),如需清理交后续。
4. 文档同步小遗漏(不升 FAIL):`MergeOrderTests.cs:366` diff 叙事注释;`ActivityClientTests.cs:228` 注释提已删的 `_finished`。dev 下轮顺手清。
