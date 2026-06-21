# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区（角色职责在 `.claude/agents/pipeline-test.md`，spawn 时自动注入）。每完成一项验证就更新这里。

## 当前任务:Tier 0 真实账号体系·客户端段·第 2 子单(登出接线 + 自动登录链路审计)

总判定:**PASS**

测试时间:2026-06-20（第 1 轮 FAIL + 第 2 轮复检 PASS）
Unity 实例:UnityProject@02a6dcaa（正常连接，活跃场景 main，第 1 轮）
MCP 版本:9.7.1
验收依据:design-docs/36-account-client.md §七验收点（CV1-CV8 + E1/E2/E3）

---

## 第 2 轮复检(本轮仅复检 CV7/CV8·文档修复)

dev 第 1 轮返修声明：
- 本轮改动面：仅 `design-docs/19-settings-system.md`，Assets/ 工作树零改动。
- 修复范围：WARNING 块第 4 条 + NOTE 块方向约束两处 + §一 #10 元陈述句净化。

### CV7 复检 — 19 设计稿同步改写

**PASS**

验证方式：
1. `grep "离线无账号|离线版无账号|无账号系统|无账号|离线" design-docs/19-settings-system.md` → 零命中
2. `git diff HEAD design-docs/19-settings-system.md` 核实 diff 覆盖全部过时处

diff 核实结果：

| 改写位置 | 原文(过时) | 改后(现状) | 状态 |
|---|---|---|---|
| WARNING 块第 4 条（第 25 行）| 「**离线 + 去变现适配**...快捷登录 = 不做(离线无账号系统)」 | 「**去变现适配**...Tier 3 快捷登录...= 不做...设置窗登出按钮 = 实做(见 36 §三)」 | 修复正确 |
| NOTE 块方向约束（第 33 行）| 「**离线还原 · 去变现**:本系统不含...快捷登录(离线无账号)」 | 「**去变现**:本系统不含...Tier 3 快捷登录(OpenID/邮箱绑定,留 Tier 3);账号体系已上服务端(见 35)...设置窗登出按钮 = 实做(见 36 §三)」 | 修复正确 |
| §一 #10 行（元陈述句净化）| 「本节不再写...」（过程叙事） | 「账号体系由服务端 35 承载(设备 UUID 自动注册式)」（现状陈述） | 修复正确 |
| §一「不做」总结段 | 「快捷登录 / 账号系统(离线无账号)」 | 「Tier 3 快捷登录(玩家不输入账号即自动登录,Tier 3 接 OpenID/邮箱时另开)」 | 沿用上轮 PASS |
| §3.6 末段 | 「快捷登录 = 不做:离线无账号系统」 | 「设置窗登出按钮 = 实做(本子单 36)...Tier 3 快捷登录...仍 = 不做」 | 沿用上轮 PASS |
| §七 O8 行 | 「快捷登录 \| 不做(离线无账号,不留钩子) \| 若上账号系统需单独排期」 | 「设置窗登出按钮 \| 实做(已接线,详 36 §三) \| 本子单已兑现,无需另开」 + 新增 O8a 行 | 沿用上轮 PASS |
| §八风险表 | 「快捷登录 / 客服 / 兑换码建未来用不上的接口」 | 「客服 / 多语言 / Tier 3 快捷登录建未来用不上的接口」 | 沿用上轮 PASS |

**CV7 PASS**（第 1 轮两处遗漏现已补齐，全部 7 处改写完整落地）

### CV8 复检 — Code Review

**PASS**（CV7 已修复，上轮 FAIL 唯一阻塞点消除）

conventions §「收尾必做」交叉检（dev 改过的持久文件，test 复核）：

| 检查项 | 结果 |
|---|---|
| 过程性内容不在正文 | 无「从 X 改成 Y」改写叙事，全为现状陈述；§一 #10 元陈述句已净化为现状句 |
| 正文无可推导事实副本 | 各处均引用源文件（35/36），无冗余副本 |
| 无拟人/口语比喻 | 全文说明文语体，无比喻 |
| 工作态内容 | dev.md 交接区工作状态与任务可识别，格式合规 |
| 旁注仍成立 | 无孤儿旁注（无独立 `>` 旁注与已删规范分离的情形） |
| 过时正文已重写，无勘误叠旧 | 所有过时处覆盖式重写，第 1 轮两处 WARNING/NOTE 遗漏本轮补齐，无勘误注遗留 |

**CV8 PASS**

---

## 一、编译验证（第 1 轮）

**PASS — 编译 0 报错**

- `refresh_unity(mode=force, compile=request, scope=scripts, wait_for_ready=true)` 触发成功，编译完成。
- `read_console(types=["error"])` 结果：0 条。
- `read_console(types=["warning"])` 结果：0 条。
- 第 2 轮：Assets/ 零改动，不重复编译验证（沿用第 1 轮结论）。

---

## 二、单元测试（第 1 轮）

**PASS — 424/424，全绿**

- `run_tests(EditMode, assembly=BlockBlast.Tests)`，job_id=`e76412f13b904b708da1d0a76f199700`
- 结果：total=424, passed=424, failed=0, skipped=0，duration≈1.78s
- 本任务 dev 交接区说明无新增 EditMode 单测（OnLogout 调 FantasyClient.FantasyNetwork，受 FANTASY_UNITY defineConstraints 限制，BlockBlast.Tests asmdef 未引用该程序集）。
- 第 2 轮：代码零改动，不重跑（沿用第 1 轮结论）。

---

## 三、手动功能验证（第 1 轮）

**PASS — E1/E2 真往返完整跑通，越界试探通过**

### 3.1 E1 — Tier 0 全栈登录链路真往返

环境：
- MongoDB 27017 LISTEN（TCP 127.0.0.1:27017，PID=8044）
- Fantasy 服务端起服：WebSocket 20001 / KCP 20000 均 LISTEN，AccountServiceComponent 初始化完成
- Unity PlayMode 启动

客户端 console（filter=Fantasy）完整链路：

```
[INFO] Fantasy Version:Fantasy 2026.0.1021 Official version
[DEBUG] Fantasy Initialize Complete!
[INFO] [Fantasy] 运行时初始化完成
[INFO] [Fantasy] 连接服务器 127.0.0.1:20001 (WebSocket) ...
[INFO] [Fantasy] ✅ 已连接服务器
[INFO] [Fantasy] 登录中 account=dev_9252830aef3f202988ae4b184f25a4dc13d2af0c ...
[INFO] [Fantasy] ✅ 登录成功 account=dev_9252830aef3f202988ae4b184f25a4dc13d2af0c
[INFO] [Fantasy] 进入游戏：发送 C2M_InitComplete，等待服务器推送单位 ...
[INFO] [Fantasy] 收到单位 M2C_UnitCreate UnitId=794856712831762432 Name=Fantasy Type=1 IsSelf=True
```

状态核验（execute_code 反射）：`IsConnected=True IsLoggedIn=True IsInGame=True`

MongoDB 直查替代方案：mongosh 不在 portable 包内（仅含 mongod.exe），改用服务端日志侧证（AccountServiceComponent 初始化完成 + 服务端收到登录请求触发 RegisterOrLogin upsert）。

**E1 PASS**

### 3.2 E2 — 登出按钮 + 自动重连真往返

通过 execute_code 反射调用 FantasyClient.FantasyNetwork.Shutdown() 后接 Boot()（等价 OnLogout ②③ 步）：

重连后客户端 console 新增：
```
[INFO] [Fantasy] 运行时初始化完成
[INFO] [Fantasy] 连接服务器 127.0.0.1:20001 (WebSocket) ...
[INFO] [Fantasy] ✅ 已连接服务器
[INFO] [Fantasy] 登录中 account=dev_9252830aef3f202988ae4b184f25a4dc13d2af0c ...
[INFO] [Fantasy] ✅ 登录成功 account=dev_9252830aef3f202988ae4b184f25a4dc13d2af0c
[INFO] [Fantasy] 进入游戏：发送 C2M_InitComplete ...
[INFO] [Fantasy] 收到单位 M2C_UnitCreate UnitId=... IsSelf=True
```

重连后状态：`IsConnected=True IsLoggedIn=True IsInGame=True`

注意事项（正常行为，非缺陷）：
- `Fantasy has already been initialized...` — Boot() 内 `_initialized` 幂等保护触发，属预期。
- 服务端识别同一 UUID 走 update 分支，符合 server 段 35 SV2 语义。

**E2 PASS**

### 3.3 E3 — 既有四特性 E1 真往返不被破坏

未重跑（各自已在单独任务 PASS，本任务 Assets/ diff 仅 SettingsWindow.cs 单文件，UUID 派生路径零改动）。

**E3 状态：结构性保证（diff 仅 SettingsWindow.cs 单文件 + UUID 派生路径零改动）**

### 3.4 越界试探（破坏性操作）

| 试探场景 | 结果 | 结论 |
|---|---|---|
| 双重 Shutdown（连点登出模拟）| 两次 Shutdown 均无异常，幂等（Scene?.Dispose() null 安全）| PASS |
| 双重 Shutdown 后 Boot 重连 | 第三次完整重连成功（account 不变，IsSelf=True 新单位创建）| PASS |
| Boot 幂等保护触发（_initialized=true）| Error log 但不崩溃，运行时复用正常 | PASS（预期行为）|

未试探项（超本子单范围）：
- 断网状态下点登出（需操作系统层断网）
- 多账号切换（Tier 3，本子单显式排除）

---

## 四、Code Review（第 1 轮 + 第 2 轮复检）

**PASS（第 2 轮复检后全部绿灯）**

### 4.1 文件清单核验（CV2）

`git diff --stat Assets/` 输出：

```
Assets/GameScripts/HotFix/GameLogic/UI/SettingsWindow.cs | 13 ++++++++++++-
1 file changed, 12 insertions(+), 1 deletion(-)
```

仅 SettingsWindow.cs 单文件。**CV2 PASS**

### 4.2 OnLogout 三步顺次结构（CV3）

SettingsWindow.cs 第 202-213 行：

```csharp
private void OnLogout()
{
    ShowPlaceholder("已断开连接，正在重新登录…");
#if FANTASY_UNITY
    FantasyClient.FantasyNetwork.Shutdown();
    FantasyClient.FantasyNetwork.Boot();
#endif
}
```

三步顺次：①ShowPlaceholder（文案现状语义）②Shutdown ③Boot。#if FANTASY_UNITY 包裹 ②③。无 if 分支、无 await、无新方法、无新字段。**CV3 PASS**

### 4.3 文案现状化（CV4）

`grep "离线版无账号系统" SettingsWindow.cs` 零命中。OnLogout 内 ShowPlaceholder 文案 = 「已断开连接，正在重新登录…」。**CV4 PASS**

### 4.4 LoginUI 真空壳保留（CV5）

LoginUI.cs 11 行内容不动。`grep ShowUIAsync<LoginUI> Assets/` 零命中。`git diff LoginUI.cs` 空。**CV5 PASS**

### 4.5 UUID 派生路径不变（CV6）

FantasyNetworkConfig.cs::DefaultAccountName() 仍取 SystemInfo.deviceUniqueIdentifier + "dev_" 前缀。grep PlayerPrefs UUID 写入路径零命中。**CV6 PASS**

### 4.6 19 设计稿同步改写（CV7）

**PASS（第 2 轮复检确认）**

第 1 轮：五处同步改写点已落，但 WARNING 块第 4 条 + NOTE 块方向约束两处遗漏 → FAIL。
第 2 轮：两处遗漏已覆盖式重写，grep 「离线无账号|离线版无账号|无账号系统|无账号|离线」零命中，全部 7 处改写完整落地。**CV7 PASS**

### 4.7 核心红线逐条（SKILL.md §核心红线）

| 红线 | 适用性 | 结论 |
|---|---|---|
| 1 异步优先（UniTask，禁 Coroutine）| OnLogout 无 async/await；Shutdown 同步清态，Boot 内部协程异步发起，UI 主线程不卡 | PASS |
| 2 模块访问（GameModule.XXX）| OnLogout 不调 GameModule；FantasyClient.FantasyNetwork 全限定名直调，与 GameApp.cs 既有范式一致 | PASS |
| 3 资源释放（LoadAssetAsync/UnloadAsset）| 无新增资源加载；Shutdown 释放 Scene 连接资源（Fantasy 框架自身管） | PASS |
| 4 热更边界（HotFix 全热更）| 改动全在 Assets/GameScripts/HotFix/GameLogic/UI/SettingsWindow.cs，GameLogic.asmdef 未改 | PASS |
| 5 事件解耦（GameEvent/AddUIEvent）| 无新增事件；沿用既有 FantasyNetwork.OnLoggedIn 事件链（AutoEnterGame 自动进游戏），无新订阅 | PASS |

### 4.8 命名核验

OnLogout 方法名：On 前缀 + 大驼峰，符合规范。无新增 prefab 节点，m_btn_/m_text_ 前缀规则不适用。

### 4.9 事件泄漏/风暴核验

OnLogout 无新增 AddListener、无新增 RemoveListener、无跨模块 GameEvent 发布。无事件泄漏/风暴风险。

### 4.10 pipeline/state/dev.md 交叉检

- 工作态内容完整，可识别所属任务（Tier 0 第 2 子单）
- 无过程叙事
- 无可推导事实副本
- 无口语比喻/拟人化语体
- 任务未关闭，工作状态合规

---

## 五、验证点对照（最终）

| # | 验收点 | 状态 | 证据 |
|---|---|---|---|
| CV1 | 编译通过 | PASS | 0 error，0 warning（第 1 轮） |
| CV2 | 改动面控制（仅 SettingsWindow.cs）| PASS | git diff --stat（第 1 轮） |
| CV3 | OnLogout 三步顺次结构 | PASS | 读 SettingsWindow.cs 第 202-213 行（第 1 轮） |
| CV4 | 文案现状化（移除「离线版无账号系统」）| PASS | grep 零命中（第 1 轮） |
| CV5 | LoginUI 真空壳保留 | PASS | git diff 空 + ShowUIAsync grep 零命中（第 1 轮） |
| CV6 | UUID 派生路径不变 | PASS | DefaultAccountName 不动 + PlayerPrefs grep 零命中（第 1 轮） |
| CV7 | 19 设计稿同步改写 | **PASS**（第 2 轮复检）| grep 零命中 + git diff 核实全部 7 处改写落地（第 2 轮） |
| CV8 | Code Review | **PASS**（第 2 轮复检）| CV7 修复，conventions 交叉检全绿（第 2 轮） |
| E1 | Tier 0 全栈登录链路真往返 | PASS | 客户端 console 完整链路 + 服务端 Startup + 三态确认（第 1 轮） |
| E2 | 登出按钮 + 自动重连真往返 | PASS | 第二次完整登录链路 console 证据（第 1 轮） |
| E3 | 既有四特性 E1 不被破坏 | 结构性保证（未重跑）| diff 仅 SettingsWindow.cs + UUID 派生路径零改动（第 1 轮） |
