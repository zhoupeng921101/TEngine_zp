# pipeline-dev 共享经验库索引

> 重型 dev 与轻型 dev 共享。每条经验为独立结构化 .md（frontmatter + rule + Why + How to apply）。本文件只做索引，准入按 `.claude/rules/conventions.md`§规则准入。

| 文件 | 适用场景 |
|------|---------|
| [unitymcp-run-tests-filter.md](unitymcp-run-tests-filter.md) | 用 UnityMCP `run_tests` 跑 EditMode 单测时，filter 失效 + 失败列表截断，如何稳妥判定目标用例真绿 |
| [csharp-no-overload-on-generic-constraint.md](csharp-no-overload-on-generic-constraint.md) | 想让同名泛型调用按 T 类型自动分流到两套实现(如经典/Mono UI 路径)时，为何不能加约束不同的重载、正确放宽公共接口 + 运行时分流的做法 |
