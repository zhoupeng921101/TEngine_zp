---
name: unitymcp-run-tests-filter
description: UnityMCP run_tests 的 test_filter 不生效（恒跑全 EditMode 套件）且失败列表上限 25 条，如何判定目标用例真绿
type: rule
---

rule: UnityMCP `run_tests`（mode=EditMode）会忽略 `test_filter`，每次都跑整个 EditMode 套件（本工程约 539 用例），并且 `get_test_job` 返回的 `failures_so_far` 截断在 25 条（`failures_capped:true`，`failed_tests_offset` 翻页也只回同一页）。因此不能靠「filter 只跑我的类 + 失败列表为空」来判目标用例通过。判定方法：当前套件基线为绿（0 failed，18 例 `UIAtlasPackerTool.Tests.*` 因缺图集 fixture 标 `[Explicit]` → Skipped，不计 failed）。跑全套后预期 failed=0；若出现失败，逐条核对是否全由本次改动引入——本次改动文件被这些失败引用即须修，否则为新的预存红需单独甄别。失败超 25 条时截断盲区重现，仍靠「多次运行失败集稳定 + git diff 确认改动文件不被失败用例引用」排除。

Why: filter 失效会让人误以为只跑了目标类；25 条截断会让人误以为「没看到我的类失败 = 我的类没跑/全过」，但其实是被截断挤掉的可能性未排除。套件基线为绿后，判绿锚到「全套 failed=0」最直接；一旦引入失败导致超 25 条截断，再退回「失败集跨运行稳定 + git diff 确认改动文件不被失败用例引用」排除盲区。另：不要为拿精确 per-class 结果而临时写 TestRunnerApi+ICallbacks 的 dump 脚本——EditMode 跑在编辑器帧循环里，自建脚本易把编辑器卡在 entering PlayMode，后续 MCP run_tests 全部报 "Cannot start a test run while the Editor is in or entering Play Mode"，得删脚本+域重载才能恢复，得不偿失。

How to apply: 改动涉及 EditMode 单测时，用 MCP `run_tests` 跑全套；读 `get_test_job(include_failed_tests:true)`，预期 failed=0（`UIAtlasPackerTool.Tests.*` 为 Explicit-skip 不计）。出现失败则逐条用 `git diff --stat` 核对是否引用本次改动文件：引用即本次引入须修，不引用则为新预存红另行甄别。需要确认某新方法/新用例已被编译发现时，用 `execute_code`（action=execute，注意：脚本体内不能写顶层 `using`，全用全限定名）反射测试程序集数 `[Test]` 方法数即可，无需真跑。
