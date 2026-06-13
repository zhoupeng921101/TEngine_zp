---
name: unity-check
description: UnityMCP 连接自检。碰 Unity 前确认 MCP 连到正确实例;或交互中工具突然报错、怀疑连接掉线/绑错实例时手动诊断。触发:/unity-check、"Unity 连不上/连接自检/绑错实例"。
---

# UnityMCP 连接自检

碰 Unity 前先确认三件事:MCP server 在跑、绑定的 Unity 实例活着、绑的是正确工程。三步探针,全是廉价调用,按序执行。

目标实例**按名绑定** `UnityProject`。

> 不钉 hash:工程副本/重开后实例 hash 会变,工程名稳定。

## 三步

1. **server 在不在** — `debug_request_context`
   - 读 `session_state.active_instance`,记下当前绑的是谁。
   - 此调用本身报错 = MCP server 进程没起 → 人工启动 server,止于此步。

2. **绑的 Unity 活不活** — `manage_scene` action=`get_active`
   - 成功返回当前场景 = 绑定实例在响应、工程对得上,自检通过。
   - 失败/超时 = 绑的实例死了或没响应 → 进第 3 步。

3. **修正绑定** — `set_active_instance` instance=`UnityProject`,再跑一次第 2 步
   - 重绑后第 2 步通过 = 之前绑错了实例,已修正。
   - 重绑后仍失败 = 没有活着的 UnityProject 实例(Unity 没开/崩了/正在编译域重载)→ 人工确认 Unity 已打开并空闲。

## 边界

只有「绑错实例」这一种自检能自动修(第 3 步)。「server 没起」「Unity 没开/没响应」都在宿主环境,自检负责**定位到哪一环**,处置交人工。

「Unity 没响应」(第 2/3 步失败)再分两种,处置不同:
- **进程冻结**:`Get-Process Unity` 见 `Responding=False` → 编辑器无响应,人工重启。
- **桥会话未注册**(更常见):进程与 `mcp-for-unity` 桥都活、`Responding=True`,但 `mcpforunity://instances` 为空(`instance_count:0`)= 编辑器未向桥注册会话、握手未完成,而非进程冻结;若此时 `Unity.ILPP.Runner` 在场,是停在内部编译/域重载、到不了 `ready_for_tools`。处置:人工在 Unity 端重连桥,而非盲目重启。

两种子 agent 都无法安全自解(不强杀用户编辑器,有未保存态风险),只能定位 + 上报。

> 开多个 Unity 实例时 active_instance 是 server 进程级全局共享的——绑错会让所有 agent 一起连不上。根治在环境侧:只留一个 UnityProject 实例、杀掉僵尸实例,自检就几乎不进第 3 步。
