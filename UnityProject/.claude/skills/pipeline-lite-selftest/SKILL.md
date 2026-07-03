---
name: pipeline-lite-selftest
description: 消费 pipeline-lite 客户端自检队列(UnityMCP 不可用时的兜底通道)。手动触发 /pipeline-lite-selftest:读 pending-test.md 待检条目,跑一次 dev-selftest.ps1(在 junction 孪生工程上跑 batchmode 编译 + EditMode 全量,主 Editor 可全程开着),PASS 逐条提交改动+清空条目、FAIL 留队报错。
---

# 轻型流水线 · 自检消费段

pipeline-lite 客户端实现段(pipeline-lite-dev)交付时优先走 UnityMCP `run_tests` 即时自检;仅当 UnityMCP 不可用(Editor 没开 / server 没起 / 未挂载)才把自检条目入队到 `.claude/pipeline-lite/pending-test.md`。本 skill 是该队列的兜底消费端:用户手动 `/pipeline-lite-selftest`,批量补跑客户端自检。自检跑在 junction 孪生工程(`UnityProject_selftest`,Assets/Packages 连接回主工程、自有 Library 与单实例锁),主 Editor 全程可开着——不要求关 Editor。

## 流程

1. 读 `.claude/pipeline-lite/pending-test.md` 「待检条目」段的未勾条目(`[ ]`)。队列为空 → 报「无待检项」,结束。
2. 跑 `.claude/pipeline-lite/dev-selftest.ps1`(默认对孪生工程跑,自动 bootstrap 孪生 + 同步 ProjectSettings)。**跑全量**:编译是全工程的、EditMode 全量 ~50s(孪生 Library 保温后),一次覆盖所有累积条目(N 条攒一次 unity 启动,省 N-1 次 domain reload);内部机制以脚本头注为准,不在此复述。
3. 按脚本 verdict 处置:
   - **PASS**(exit 0 = 0 编译错误 + EditMode 全绿)→ 按条目逐条自动提交(每条一 commit,详见红线)→ 删除「待检条目」段本轮全部条目(清空队列),呈报 PASS + 各 commit hash + 测试统计。
   - **FAIL**(exit 1)→ 条目**留队**,呈报首个编译错误 / 失败测试名;据条目「改动文件」清单定位并修复,修完重跑本 skill。不清队列。
   - **BLOCKED**(exit 3 = 孪生被占,另一个自检正在跑)→ 稍候重跑,不改队列。主 Editor 开着不影响(孪生锁独立)。

## 红线
- 只在 PASS 时清队列;FAIL / BLOCKED 一律保留条目,不丢待检项。
- PASS 时自动提交待检批次改动:按各条目『改动文件』逐条精确 `git add`(禁 `git add -A`)、用条目任务标题作 conventions commit message + 结尾 `Co-Authored-By` trailer(按环境 git 约定)、每条一 commit、只 commit 不 push、在主干先切分支;FAIL / BLOCKED 不提交。
- 改 `pending-test.md` 等持久文件前遵 `.claude/rules/conventions.md`(清空已 PASS 条目属工作状态层维护)。
