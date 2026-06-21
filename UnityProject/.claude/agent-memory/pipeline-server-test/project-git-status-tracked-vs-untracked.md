---
name: project-git-status-tracked-vs-untracked
description: git diff --name-only HEAD 只显示 tracked 文件修改,新建文件须 git status --short 查 `??` 前缀
metadata:
  type: project
---

`git diff --name-only HEAD` 只显示已跟踪文件的修改,新建文件(untracked)不在其中;须用 `git status --short` 查全貌,`??` 前缀即为新建未跟踪文件。

**Why:** 交接区 dev 写「改动文件清单」时,若 dev 用 `git diff --name-only HEAD` 漏报新建文件,test 复核会以为 dev 漏改某文件;实际 dev 已建但还未 stage。

**How to apply:** test 复核交接区文件清单时,两者结合:① `git diff --name-only HEAD`(改动)+ ② `git status --short` 看 `??`(新建)。若 dev 清单只有改动文件、缺新建文件,可能是 dev 用错命令,核对后补全清单不算 FAIL。
