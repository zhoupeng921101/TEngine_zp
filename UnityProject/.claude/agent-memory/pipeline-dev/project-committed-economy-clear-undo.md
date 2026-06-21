---
name: project-committed-economy-clear-undo
description: RepairTemple 式「已提交经济动作」一律成功后 _undoStack.Clear() 同交付,否则悔棋会回滚已扣币/已改状态。
metadata:
  type: project
---

`RepairTemple` 式「已提交经济动作」一律成功后 `_undoStack.Clear()`(同交付),否则悔棋会回滚已扣币/已改状态。隔离测快照对该字段的回滚能力须手动改字段后 Undo(不能走会清栈的真实方法)。

**Why:** 经济类动作(扣币/抽奖/购买)一旦交付即不可逆,若悔棋仍能撤销将造成「免费再消费」漏洞;测快照能力要绕开真实方法,否则栈被清掉测不出回滚(2026-06,piety-temple)。

**How to apply:** 涉及经济交付的状态机操作:① 成功后立刻 `_undoStack.Clear()`;② 单测验「快照→改字段→Restore」时手动改字段(不走真实 method),否则测的就是「Clear 之后能否还原」而非「Restore 能力」。
