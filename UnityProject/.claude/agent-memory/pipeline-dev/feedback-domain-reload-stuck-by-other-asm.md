---
name: feedback-domain-reload-stuck-by-other-asm
description: 判「编译成功但域未重载」:dll mtime 新+validate 0 err+test total=0+reflect 0 hits;常因别的程序集带错;不擅改别人的包,标 blocker 上报。
metadata:
  type: feedback
---

判「编译成功但域未重载」(改了源、dll 已生成,但新类型用不了)需要四个信号同时出现=域没换、磁盘 dll 新但内存域旧:

① `Library/ScriptAssemblies/<asm>.dll` mtime 已更新(编译跑过且 0 error,失败不产 dll);
② `validate_script`(standard)0 error;
③ `run_tests` 该 asmdef 返 succeeded 但 **total=0**(测试不被发现);
④ `unity_reflect` search 该类(scope=project)返 0。

根因常是**别的程序集编译带错**(本轮实例:外部 Fantasy 本地包缺 `System.Collections.Immutable` → 全工程编译带错 → Unity 拒绝域重载),自己的代码并无问题。判据:跑一个**既有**测试 asmdef(如 BlockBlast.Tests)若仍全绿=已有缓存 dll 在旧域里能跑、零回归成立,印证「只是新 dll 没进域」。处置:这是任务外环境阻塞,不擅自改别人的包/manifest,记 blocker 上报、附「该错修好/移除后域重载即可跑测」的重试条件。

**Why:** 域重载是 Unity 全工程级事件,任何一处编译错都阻断重载,但报错位置往往跟当前任务无关;若擅自改外部包/manifest 会越界且常引入二次问题(2026-06,ui-atlas-packer)。

**How to apply:** 测试发现新类型用不了/total=0 时:① 跑四信号自检;② 跑既有 asmdef 验「旧 dll 可执行」;③ 检查 Unity Console 看 build error 是哪个程序集;④ 非本任务程序集报错→标 BLOCKED 上报,不擅自改。
