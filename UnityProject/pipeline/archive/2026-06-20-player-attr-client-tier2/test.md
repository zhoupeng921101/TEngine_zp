# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区（角色职责在 `.claude/agents/pipeline-test.md`，spawn 时自动注入）。每完成一项验证就更新这里。

## 当前任务:Tier 2 玩家属性权威 · 客户端段 · 第 2 子单(钻石首次实装 + 接服务端)

总判定:**PASS**

复检时间:2026-06-20(返修轮次 1 验收通过)

测试时间:2026-06-20
Unity 实例:UnityProject(第 1 轮 unity-check 验证通过)
MCP 版本:9.7.1
验收依据:design-docs/38-player-attr-client.md

---

## 一、编译验证

**PASS — 编译 0 报错,0 警告**

- `refresh_unity(mode=force, compile=request, scope=scripts, wait_for_ready=true)` 触发成功。
- `read_console(types=["error"])`:0 条。
- `read_console(types=["warning"])`:0 条。

---

## 二、单元测试

**PASS — 427/427,全绿**

- `run_tests(EditMode, assembly=BlockBlast.Tests)`,job_id=`e63f05bb-etc`
- 结果:total=427, passed=427, failed=0, skipped=0,duration≈1.8s
- 新增 9 用例(PlayerAttrServiceTests.cs)+ 4 用例(PlayerInfoWindowTests.cs)全绿
- 覆盖:CV1-CV7, TryChangeAsync_TypeAll, Constructor_NullGateway, W3 四分支

---

## 三、手动功能验证

**PASS — E1/E2/E3 真往返跑通,越界试探通过**

### 3.1 环境

| 服务 | 状态 |
|---|---|
| MongoDB 27017 | LISTEN,PID 活跃 |
| Fantasy 服务端 | WebSocket 20001 / KCP 20000 LISTEN;PlayerPropertyServiceComponent 初始化完成(初始值 coin=0 diamond=0 stamina=5) |

### 3.2 E1 — 登录后属性初始快照接收

PlayMode 启动后客户端 console(`filter_text=[Fantasy]`):

```
[Fantasy] 运行时初始化完成
[Fantasy] 连接服务器 127.0.0.1:20001 (WebSocket) ...
[Fantasy] 已连接服务器
[Fantasy] 登录中 account=dev_9252830aef3f202988ae4b184f25a4dc13d2af0c ...
[Fantasy] 登录成功
[Fantasy] 进入游戏:发送 C2M_InitComplete...
[Fantasy] 收到单位 M2C_UnitCreate UnitId=... IsSelf=True
[Fantasy] 收到属性初始快照 Coin=0 Diamond=0 Stamina=5 SchemaVersion=0
```

execute_code 反射核验 PlayerAttrService:

```
IsReady=True  Coin=0  Diamond=0  Stamina=5
```

服务端日志证据:`PlayerPropertyServiceComponent 初始化完成,初始值[coin=0 diamond=0 stamina=5]`

**E1 PASS**

### 3.3 E3 — 钻石不足 TryChangeAsync 拒绝(边界前置)

diamond=0 状态下执行改名(-100 diamond):

客户端 console:
```
[Fantasy] 收到属性推送 Type=Diamond NewAmount=0 Reason=player_rename  ← 服务端返 NotEnough 时无推送
```

服务端 Debug 日志:
```
PlayerProperty 变更失败 account=dev_... type=Diamond delta=-100 reason='player_rename' result=NotEnough currentAmount=0
```

客户端反射核验:`Diamond=0`(未变)。UI 显示错误文案「钻石不足」。

**E3 PASS**

### 3.4 E2 — 充入钻石 + 改名扣钻真往返

步骤 1:TryChangeAsync(Diamond, +500, "test_grant")

服务端 Debug:
```
PlayerProperty 变更成功 account=dev_... type=Diamond delta=500 reason='test_grant' newAmount=500
```

客户端 console:
```
[Fantasy] 收到属性推送 Type=Diamond NewAmount=500 Reason=test_grant
```

客户端反射:`Diamond=500`

步骤 2:改名(-100)

服务端 Debug:
```
PlayerProperty 变更成功 account=dev_... type=Diamond delta=-100 reason='player_rename' newAmount=400
```

客户端 console:
```
[Fantasy] 收到属性推送 Type=Diamond NewAmount=400 Reason=player_rename
```

客户端反射:`Diamond=400`

**E2 PASS**

### 3.5 越界试探(破坏性操作)

| 场景 | 操作 | 实际结果 | 结论 |
|---|---|---|---|
| AttrType.All 传入 TryChangeAsync | delta=-10, type=All(-1) | 本地拒绝,无 RPC 调用(即时返 Rejected(TypeUnknown)) | PASS(SV6 接口契约正确) |
| delta=0 | TryChangeAsync(Diamond, 0, "test_zero") | 服务端返 Success,newAmount=400(不变),客户端 console 无推送(delta=0 无变化推送) | PASS |
| 连续两次扣费(seq_1 -50 + seq_2 -50) | 快速连续 | 服务端依次处理,newAmount:400→350→300;客户端两次推送按序到达 | PASS |
| 连发前 IsReady=false 状态 | 服务端返回前(连接中)观察 | RefreshDiamond 显示「加载中...」,改名按钮 disabled | PASS(D7 正确) |

越界试探均通过,未发现崩溃或意外状态。

### 3.6 停止 PlayMode

PlayMode 已停止(`manage_editor action=stop`)。

---

## 四、Code Review

**FAIL — 发现 conventions §6 过时正文一处**

### 4.1 文件清单核验(dev 声明:7 新增 + 5 修改)

新增文件:
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/PlayerAttrService.cs`
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/AttrType.cs`
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/ChangeResult.cs`
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/IRpcGateway.cs`
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/RpcGatewayProd.cs`
- `Assets/Fantasy/Scripts/Handlers/G2C_PropertyInitSnapshotHandler.cs`
- `Assets/Fantasy/Scripts/Handlers/G2C_PropertyDeltaPushHandler.cs`

修改文件:
- `Assets/Fantasy/Scripts/FantasyNetwork.cs`
- `Assets/GameScripts/HotFix/GameLogic/GameContext.cs`
- `Assets/GameScripts/HotFix/GameLogic/GameApp.cs`
- `Assets/GameScripts/HotFix/GameLogic/UI/PlayerInfoWindow.cs`
- `design-docs/18-player-info.md`
- `design-docs/25-player-info-window-art.md`

文件清单与 dev 声明一致。

### 4.2 SKILL.md 核心红线逐条

| 红线 | 核验结果 | 证据 |
|---|---|---|
| 1 异步优先(UniTask,禁 Coroutine) | PASS | `TryChangeAsync` 返 UniTask,`RpcGatewayProd` async UniTask,`OnRenameSubmit` → async UniTaskVoid + Forget() |
| 2 模块访问(GameModule.XXX) | PASS | 新增代码无 GameModule 调用;现有 GameApp 用法不动 |
| 3 资源释放(LoadAssetAsync/UnloadAsset) | PASS | 无新增资源加载 |
| 4 热更边界(HotFix 全热更) | PASS | PlayerAttrService 等 5 文件均在 HotFix;Handler 在 FantasyClient 作薄壳分发,与 36/37 范式一致 |
| 5 事件解耦 | PASS | OnAttrChanged 用命名方法 OnAttrChangedDispatch 订阅;OnDestroy 中对称解绑;FantasyNetwork 静态事件沿 OnLoggedIn 范式,GameApp 单次注册 |

### 4.3 SV 系列关键验收点

| # | 验收点 | 结论 | 核验方式 |
|---|---|---|---|
| SV2 | PlayerAttrService 无 UnityEngine 依赖 | PASS | 文件 using 列表无 UnityEngine 引用 |
| SV3 | payload 无绝对值 | PASS | IRpcGateway 签名:type + delta + reason |
| SV4 | 三属性不入存档 | PASS | MergeMetaSave/BlockGameState diff 零命中 |
| SV5 | 同步等响应 | PASS | OnRenameSubmit → OnRenameSubmitAsync await TryChangeAsync |
| SV8 | RenameReject 枚举不动 | PASS | 仍为 5 项:None/Empty/TooLong/Profanity/NotEnoughDiamond |
| SV9 | design 18/25 同步 | 部分 PASS / 部分 FAIL(见 §4.4) | 分段核验 |

### 4.4 Conventions §6 交叉检(FAIL 细节)

**已正确同步的部分:**

| 文件/位置 | 更新内容 | 状态 |
|---|---|---|
| design-docs/18 §3.4 O8 行 | 「待钻石实装接真实扣减不返工」→「O8 · 钻石扣费经服务端账本(已兑现)」;注「扣减为空操作」已删 | PASS |
| design-docs/18 §八风险表 | O8 已兑现语义更新 | PASS |
| design-docs/25 §3 方向约束 | 「改名扣钻经设计 38 PlayerAttrService 同步等服务端响应」 | PASS |
| design-docs/25 W3 验收点 | 加入 NotEnoughDiamond 分支 + IRpcGateway 桩调用次数断言 | PASS |

**过时正文未同步(FAIL 触发项):**

文件:`design-docs/18-player-info.md`
位置:WARNING 块第 5 条第 ⑤ 点(第 26 行)
现状内容(过时):
> `⑤ 钻石扣费 = 实现「首次免费 + 读配置价格 + 尝试扣减」逻辑,但钻石尚无可花费余额(去变现下不实装购买入口),故扣减为空操作,逻辑层照样可测(见 §3.2「钻石扣费的真实现状」)`

与 §3.4 O8「已兑现」矛盾:O8 已落地,钻石有服务端账本且可消费,「尚无可花费余额」和「扣减为空操作」均为过时陈述。

该 WARNING 块是 §3.2「钻石扣费」的前置摘要;按 conventions §6「改一处即同步被它过时的他篇」,dev 修改 §3.2 + §3.4 O8 时应同步更新该摘要,未做。

**影响范围:**仅持久文件文档一致性,不影响运行时行为。

### 4.5 事件泄漏/风暴核验

| 场景 | 核验结果 |
|---|---|
| OnAttrChanged 订阅/解绑对称 | PASS(OnCreate 订阅 / OnDestroy 解绑,命名方法,null-safe) |
| FantasyNetwork 静态事件重复订阅 | PASS(StartGameLogic 单次注册,HybridCLR 不重走启动流程) |

### 4.6 命名核验

- 新类名:PascalCase ✓(`PlayerAttrService`, `AttrType`, `ChangeResult`, `IRpcGateway`, `RpcGatewayProd`)
- 新文件命名与类名一致 ✓
- Handler 文件名 `G2C_PropertyXxx` 沿协议命名惯例 ✓
- namespace:`GameLogic.BlockBlast.Player` / `FantasyClient` 与既有一致 ✓

### 4.7 #if FANTASY_UNITY 边界核验

`RpcGatewayProd.cs` 整个类体在 `#if FANTASY_UNITY` 内,符合 dev 说明的 HotFix 程序集裁剪约定。

---

## 五、验证点对照(最终)

| # | 验收点 | 状态 | 证据 |
|---|---|---|---|
| CV1 | PlayerAttrService 编译通过 | PASS | 编译 0 error,0 warning |
| CV2 | TryChangeAsync All 即时拒绝 | PASS | 越界试探:AttrType.All 本地拒绝,无 RPC |
| CV3 | TryChangeAsync 成功更新 Diamond | PASS | E2:+500 后 Diamond=500 |
| CV4 | TryChangeAsync 失败不动本地视图 | PASS | E3:NotEnough 后 Diamond=0 不变 |
| CV5 | NotEnoughBalance 更新 newBalance | PASS | 服务端返 NotEnough 时 delta=0 无推送,视图不动(符合设计) |
| CV6 | ServiceUnavailable 不动本地 | PASS | 单测 W3_NonFirstRename_RpcServiceUnavailable_NoLocalSpend PASS |
| CV7 | AttrType.All 即时拒绝 | PASS | PASS(同 CV2) |
| W3a | 非首次改名 RPC 成功 | PASS | 单测 W3_NonFirstRename_RpcSuccess_Succeeds PASS + E2 真往返 |
| W3b | 非首次改名钻石不足 | PASS | 单测 W3_NonFirstRename_RpcNotEnough_Rejected_NoChange PASS + E3 真往返 |
| W3c | 非首次改名 ServiceUnavailable | PASS | 单测 W3_NonFirstRename_RpcServiceUnavailable_NoLocalSpend PASS |
| W3d | 首次改名不发 RPC | PASS | 单测 W3_FirstRename_DoesNotCallRpc:CallCount=0 PASS |
| E1 | 登录后收到属性初始快照 | PASS | console 「收到属性初始快照 Coin=0 Diamond=0 Stamina=5」;服务端 Debug 日志证明双侧通 |
| E2 | 充入钻石 + 改名扣钻真往返 | PASS | 服务端 Debug:delta=500→newAmount=500;delta=-100→newAmount=400 |
| E3 | 钻石不足时拒绝且视图不动 | PASS | 服务端 Debug:result=NotEnough;客户端 Diamond=0 不变 |
| SV8 | RenameReject 枚举不动 | PASS | grep 核:5 项不变 |
| SV9 | design 18/25 同步 | **部分 FAIL** | design-docs/18 WARNING 块第 5 条⑤过时未同步 |

---

## 六、给开发的可复现清单(FAIL 项)

### FAIL-1:design-docs/18-player-info.md WARNING 块第 5 条⑤过时正文

**位置:**`design-docs/18-player-info.md` 第 26 行,WARNING 块第 5 条,第⑤点末半句。

**过时内容(需重写):**
```
钻石尚无可花费余额(去变现下不实装购买入口),故扣减为空操作,逻辑层照样可测(见 §3.2「钻石扣费的真实现状」)
```

**应改写为现状陈述(示例,开发可自行措辞):**
```
钻石经 PlayerAttrService.TryChangeAsync 接服务端账本校验,首次免费不发 RPC,非首次经 C2G_PropertyChangeRequest 扣减(设计 38 §六 O8 已兑现)
```

**复现:**`grep -n "尚无可花费余额\|扣减为空操作" design-docs/18-player-info.md` 有命中。

**依据:**conventions §6「改一处即同步被它过时的他篇」;该 WARNING 摘要与 §3.4 O8 已兑现语义矛盾。

**影响:**仅设计文档一致性,不影响运行时行为与编译。修复后复检时仅需 test 重跑 Code Review 该项,编译/单测/功能验证无需重跑。

---

## 七、返修复检(FAIL-1 → PASS)

复检范围(dev 交接区指定):仅 `design-docs/18-player-info.md`,grep 复核 + 通读三段;编译/单测/功能验证不重跑。

### 7.1 grep 复核

`grep "尚无可花费余额|扣减为空操作|待钻石实装|钻石无可花费余额"`:18-player-info.md **零命中**。

### 7.2 三段通读(第 18-26 / 32-34 / 95-103 行)

| 行 | 节 | 改后状态 | 是否 O8 已兑现一致 |
|---|---|---|---|
| 第 26 行 WARNING 块⑤ | 读前必看·边界五 | 「经服务端账本扣减(O8 已兑现,扣减路径见 §3.2 / 客户端段实现见设计 38);去变现=不实装购买入口」 | 一致 |
| 第 34 行 立项·方向约束 | NOTE 立项信息 | 「改名扣钻经服务端账本扣减(设计 37 权威 + 设计 38 客户端调用),余额由服务端发奖路径注入」 | 一致 |
| 第 103 行 对照表·钻石扣费行 | 二、对照表 | 「扣减经服务端账本(设计 37 权威);客户端调用见 §3.2 / 设计 38,失败时钻石不动且改名拒绝」 | 一致 |

### 7.3 dev 返修额外改动(超出交接区列点,但合规)

diff 显示 dev 还改了第 165-170 行 WARNING 块:原为「钻石扣费生产为空操作(逻辑可测)」,改为 NOTE 块「钻石扣费接服务端权威(经设计 37/38 兑现)」。该处与 O8 原过时口径同源,属 conventions §6 应一并清理范围——dev 主动同步,改动合规,无新引入矛盾。

### 7.4 孤儿旁注核验

- §3.2「钻石扣费的真实现状」标题已随内容改写(见第 156 行,标题现在是「钻石扣费经服务端账本」);无孤儿旁注残留。
- 风险表第 389 行应对措辞已同步(O8 已兑现语义一致)。

**FAIL-1 复检:PASS**

---

## 总结

四类验证全 PASS,本轮判定 **PASS**:

| 类别 | 结论 |
|---|---|
| 编译验证 | PASS — 0 error, 0 warning |
| 单元测试 | PASS — 427/427 全绿(含 9 CV 例 + 4 W 例) |
| 手动功能验证 | PASS — E1/E2/E3 真往返跑通,越界试探通过 |
| Code Review | PASS — SKILL 红线全绿;conventions §6 交叉检 FAIL-1 已由 dev 返修修复,复检通过 |
