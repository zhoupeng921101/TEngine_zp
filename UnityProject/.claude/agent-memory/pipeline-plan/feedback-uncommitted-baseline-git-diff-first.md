---
name: feedback-uncommitted-baseline-git-diff-first
description: 「以未提交工程现状为基线」的补完任务,先 git diff 逐文件核实前序落点,别照过时简报设计
metadata:
  type: feedback
---

「以未提交工程现状为基线」的补完任务:先 `git diff` 逐文件核实前序(main/他人)的真实落点,别照 boss/简报的过时快照设计。设计 = 在 diff 核实过的真实现状上补完+验证。

**Why:** 简报可能停在动手前的快照。2026-06 实例:简报称「CollectDemo.cs 核心逻辑还在」,实际 main 已把它收敛成纯表现工具类——照简报设计就在改一个已不存在的现状。

**How to apply:** 接到补完任务先跑 `git diff` + `git log` 核实涉及文件的真实状态;简报与现状冲突时以现状为准、在设计稿里显式指出偏差并据此收敛。
