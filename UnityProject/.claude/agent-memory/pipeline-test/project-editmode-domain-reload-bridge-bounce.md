---
name: project-editmode-domain-reload-bridge-bounce
description: EditMode run_tests 触发域重载会让桥短暂注销,瞬态读数不判 BLOCKED,重试 manage_scene 待桥重注册即可
metadata:
  type: project
---

EditMode `run_tests` 触发的域重载会让桥会话短暂注销:`get_test_job` 轮询中途可能报 "No Unity Editor instances found"。先判子类——`Get-Process Unity` 仍 `Responding=True` 且 `active_instance` 服务端记录未变 → 域重载期瞬态注销(可恢复),非环境阻塞;重试 `manage_scene get_active` 待桥重注册即可继续轮询同一 job_id 取回完整结果。别据此瞬态读数判 BLOCKED。

**Why:** 2026-06 settings 实测,EditMode 测试会触发 AppDomain.Reload,期间 Unity 主进程仍在但 UnityMCP 桥短暂掉线,误判 BLOCKED 会浪费一轮。

**How to apply:** 轮询 get_test_job 报无实例时:①确认 Unity 进程 Responding ②确认 active_instance 不变 ③等几秒重试 manage_scene get_active 探活 ④活了继续轮询同一 job_id;此瞬态不报 BLOCKED。
