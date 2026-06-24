---
name: unitymcp-run-tests-filter
description: UnityMCP run_tests 的 test_filter 不生效（恒跑全 EditMode 套件）且失败列表上限 25 条，如何判定目标用例真绿
type: rule
---

rule: UnityMCP `run_tests`（mode=EditMode）会忽略 `test_filter`，每次都跑整个 EditMode 套件（本工程约 556 用例），并且 `get_test_job` 返回的 `failures_so_far` 截断在 25 条（`failures_capped:true`，`failed_tests_offset` 翻页也只回同一页）。因此不能靠「filter 只跑我的类 + 失败列表为空」来判目标用例通过。判定方法：跑全套后，确认 25 条失败全部落在与本次改动无关的预存失败类（本工程当前长期红：引用不存在的 `GameWindow.cs` 的 `ActivityClientTests`/`GameWindowReskinRegressionTests`/`SettlementWindowRegressionTests`，以及缺图集 fixture 的 `UIAtlasPackerTool.Tests.*`），且这 25 条跨多次运行稳定不变（改动前后同样红）→ 自己的目标测试类（不在失败列表中）即全绿。多次运行对照 + git diff 确认改动文件不被这些失败用例引用，是排除「截断盲区」的关键。

Why: filter 失效会让人误以为只跑了目标类；25 条截断会让人误以为「没看到我的类失败 = 我的类没跑/全过」，但其实是被截断挤掉的可能性未排除。把判定锚到「失败集稳定且全属预存无关红」而非「失败列表空」，才不漏判。另：不要为拿精确 per-class 结果而临时写 TestRunnerApi+ICallbacks 的 dump 脚本——EditMode 跑在编辑器帧循环里，自建脚本易把编辑器卡在 entering PlayMode，后续 MCP run_tests 全部报 "Cannot start a test run while the Editor is in or entering Play Mode"，得删脚本+域重载才能恢复，得不偿失。

How to apply: 改动涉及 EditMode 单测时，用 MCP `run_tests` 跑全套；读 `get_test_job(include_failed_tests:true)`，核对失败条目是否全是上述预存无关红、且条数跨运行稳定；再用 `git diff --stat` 确认本次改动文件不在这些失败用例的引用范围内。三者齐 → 判目标测试全绿。需要确认某新方法/新用例已被编译发现时，用 `execute_code`（action=execute，注意：脚本体内不能写顶层 `using`，全用全限定名）反射测试程序集数 `[Test]` 方法数即可，无需真跑。
