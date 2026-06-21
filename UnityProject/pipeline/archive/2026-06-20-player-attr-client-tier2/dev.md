# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:Tier 2 玩家属性权威 · 客户端段 · 第 2 子单(钻石首次实装 + 接服务端 + 改名扣钻真接线)

## 交接区(交测试)

### 1. 改动摘要(做了什么 / 为何 / 关键决策)

按 plan §客户端段交付清单落地 Tier 2 玩家属性客户端段,**首次为客户端建钻石(及金币/体力)的元层数据持有 + 接服务端推送 + 改名扣钻一个真接线点**。简报基线错(数据源迁移 + 反作弊缺口关闭)在 plan §taskFlaw 已亮,boss 关单时复核。

**核心档落地**:① 新增 `PlayerAttrService`(纯逻辑 C# 类,内存持 Coin/Diamond/Stamina + 订阅 InitSnapshot/DeltaPush + 同步发 ChangeRequest 等响应);② `AttrType` 枚举(协议命名 Coin/Diamond/Stamina,对齐 `Fantasy.PropertyType`);③ `ChangeResult` 结构 + `ChangeReject` 枚举(6 失败码 + Success);④ `IRpcGateway` 接缝(生产 + 测试两实现);⑤ `RpcGatewayProd` 生产实现(在 HotFix 内 `#if FANTASY_UNITY` 包裹,沿 `RemoteRankSource` 范式);⑥ 两个薄壳 Handler 在 `Assets/Fantasy/Scripts/Handlers/`(源生成器自动注册 Message<T>);⑦ `FantasyNetwork` 加 `OnPropertyInitSnapshot` / `OnPropertyDeltaPush` 静态事件(避免 FantasyClient 反向依赖 GameLogic 致循环);⑧ `GameContext.PlayerAttr` 挂入 + `InitPlayerAttrWith` 测试入口;⑨ `GameApp.StartGameLogic` 内挂订阅(早挂,Boot 之后、登录前,保 Snapshot 不丢);⑩ `PlayerInfoWindow` 改名扣钻接缝改写(`OnRenameSubmit` 包成 async + 走 `Attr.TryChangeAsync` 等响应)+ 钻石余额行 + IsReady=false 时改名按钮 disabled;⑪ 钻石面板 OnAttrChanged 订阅 + OnDestroy 解绑。

**增强档默认取(plan 拍板)**:`PlayerAttrService` 同步持 Coin/Stamina(无 UI 接入,代码量边际增加 0);后续业务玩法刀(商店 / 兑换码 / 邮件 / 任务)按需调 `TryChangeAsync` 即可接入。

**砍掉**:① 金币/体力玩法路径接入(无既有调用方);② HUD 全屏显示;③ ledger 历史;④ `PlayerNameGenerator` 改动(简报误指,文件 31 行只是名字生成器);⑤ `RenameReject` 枚举扩展(SV8,5 项保持);⑥ 服务端 37 任何契约(零改 Fantasy 仓)。

#### 关键决策(本环节自主拍板,自治默认推进)

- **AttrType 枚举对齐协议命名(Coin / Diamond / Stamina)**:服务端协议是 `PropertyType.Coin/Diamond/Stamina`,枚举值 0/1/2 一一映射 → 客户端枚举直用 0/1/2(`(PropertyType)type` 强转可用)。若客户端用 Gold/Diamond/Energy 历史命名 → 两套命名漂移,容易出 bug。客户端字段命名也用 Coin/Stamina。
- **RpcGatewayProd 落在 HotFix(非 plan 说的 Assets/Fantasy/Scripts/)**:沿 `RemoteRankSource.cs` 现成范式(同款「网络层 RPC 包到 #if FANTASY_UNITY 内,接口面返 UniTask」)— HotFix 的 GameLogic.asmdef 已引用 FantasyClient + Fantasy.Unity,在此放生产实现一致性最高,无新增 asmdef 调整。Plan 说在 Assets/Fantasy/Scripts/ 是非强约束(plan 段已标「沿 35/36 既有注册约定」),我据现状取更优。
- **Handler 走 FantasyNetwork 静态事件转发,不直调 GameLogic.GameContext**:`FantasyClient` asmdef 不能引 `GameLogic`(后者已引前者,反向引 = 循环依赖);若把 Handler 落 HotFix 让源生成器扫,改动面更大且现有所有 Handler 都在 FantasyClient,沿用一致性更高。最简方案 = 在 FantasyNetwork 加 `OnPropertyInitSnapshot/OnPropertyDeltaPush` 静态事件(沿 `OnLoggedIn` 范式),Handler 内 Raise,HotFix 侧 `GameApp.StartGameLogic` 订阅事件转交 PlayerAttrService。
- **改名扣钻同步等响应 — 不用 dry-run 做本地校验**:初版用 dry-run TryRename 检查空/长度/屏蔽字,但 dry-run 在合法时会写名 + RenameCount++(PlayerRenameService 内 cost=0 时不会进 trySpendDiamond → 直接走到写名),污染计费(W3_FirstRename_DoesNotCallRpc 直接捕获此 bug)。最终方案 = 先读 cost(read-only)→ cost>0 时发 RPC → 服务端 OK 再调一次 TryRename 落定。本地校验失败发生在 RPC 之后,理论上是边界情形(InputField 可在 UI 层提前防空/长),交接区 §5 诚实标注。
- **OnRenameSubmit 包成 async**:`onEndEdit` 不能直接绑 `async UniTaskVoid`(InputField API 是 `UnityAction<string>`)→ 加间接层 `OnRenameSubmit` 调 `OnRenameSubmitAsync(newName).Forget()`(沿 HotFix 既有 async UI 范式)。
- **钻石余额行节点 `m_text_DiamondBalance` 在 prefab 中暂无**:`FindChildComponent<Text>("Root/NameBlock/m_text_DiamondBalance")` 返 null,RefreshDiamond 内 null-safe(prefab 未接入也不崩),美术 prefab 补节点后即生效。设计 25 §3.2 已声明节点路径,但本子单不动 prefab(art 受限退路 = 文本提示走 Log.Info,玩家体验上钻石面板缺失退化为按钮可点但无可见余额)。**已交接 § 标注**。
- **改名按钮 disabled 联动:OnAttrChanged 订阅触发 RefreshRenameInteractable**:All / Diamond 事件都触发(IsReady 变 true 时事件 type=All)。
- **OnRenameSubmit 服务端响应 NewBalance 刷视图 — 包括 NotEnoughBalance / OverLimit**:服务端在这两种码下返实际余额便于客户端 toast「需要 X,你有 Y」(沿 37 §3.3.2)→ PlayerAttrService 内在这两种码下也 SetByType + 触发事件。CV5 测试覆盖。

### 2. 文件清单

新增(7 个 .cs):
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/AttrType.cs`(枚举 Coin/Diamond/Stamina/All)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/ChangeResult.cs`(结构 + ChangeReject 6 项枚举)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/IRpcGateway.cs`(接缝接口,返 UniTask<ChangeResult>)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/PlayerAttrService.cs`(纯逻辑核心档,~110 行)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/RpcGatewayProd.cs`(生产实现,`#if FANTASY_UNITY`,沿 RemoteRankSource 范式)
- `Assets/Fantasy/Scripts/Handlers/G2C_PropertyInitSnapshotHandler.cs`(薄壳分发,调 `FantasyNetwork.RaisePropertyInitSnapshot`)
- `Assets/Fantasy/Scripts/Handlers/G2C_PropertyDeltaPushHandler.cs`(薄壳分发,调 `FantasyNetwork.RaisePropertyDeltaPush`)

新增(EditMode 测试):
- `Assets/Editor/Tests/BlockBlast/PlayerAttrServiceTests.cs`(CV1-CV7 + 2 防御性测,共 9 例,全 PASS)

修改:
- `Assets/Fantasy/Scripts/FantasyNetwork.cs`(加 2 静态事件 + 2 internal Raise 方法,~20 行新增)
- `Assets/GameScripts/HotFix/GameLogic/GameContext.cs`(+`PlayerAttr` 成员 + `OnInit` 创建 + `InitPlayerAttrWith` 测试入口,~10 行新增)
- `Assets/GameScripts/HotFix/GameLogic/GameApp.cs`(`#if FANTASY_UNITY` 块内挂 2 事件订阅,~10 行新增)
- `Assets/GameScripts/HotFix/GameLogic/UI/PlayerInfoWindow.cs`(`OnRenameSubmit` 改 async + 加钻石余额行 + OnAttrChanged 订阅,~80 行新增/改写)
- `Assets/Editor/Tests/BlockBlast/PlayerInfoWindowTests.cs`(改写 1 例 + 加 3 例:W3 新口径走桩 IRpcGateway,~60 行改动)

未触:
- `Assets/Fantasy/Scripts/FantasyNetworkConfig.cs`(无关本任务,已 staged 一处非本任务改动 `ReconnectMaxAttempts 0→3`)
- `Assets/GameProto/*`(零改 — 服务端 37 协议生成物已就位,客户端零回归)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/PlayerNameGenerator.cs`(简报误指,守不变量)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/PlayerRenameService.cs`(接缝不动,内核换 trySpendDiamond 调用方)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/PlayerInfo.cs`(无三属性字段,守 D2)
- `Assets/GameScripts/HotFix/GameLogic/Module/Settings/*`(MergeMetaPersistence 等,无三属性 key,守 D2)
- Fantasy 仓(零 diff,守不变量)

### 3. 验证点(逐条对应验收 / 该验什么 / 怎么验 / 预期)

| # | 验收点 | 怎么验 | 预期 | 自跑状态 |
|---|---|---|---|---|
| **SV1**·编译 | `read_console types=error` | 0 error;域重载完成 | PASS |
| **SV2**·PlayerAttrService 不依赖 UnityEngine | grep `using UnityEngine` 在 PlayerAttrService.cs / AttrType.cs / ChangeResult.cs / IRpcGateway.cs | 零命中 | PASS |
| **SV3**·客户端绝对值禁报 | grep `IRpcGateway.cs` 接口签名 + `RpcGatewayProd.cs` `C2G_PropertyChangeRequest((PropertyType)type, delta, reason)` 调用点 | 请求负载仅 type + delta + reason 三字段;**不**传当前余额 | PASS(协议 `C2G_PropertyChangeRequest` 字段集 = Type/Delta/Reason,生成物固定) |
| **SV4**·三属性不入本地存档 | `git diff MergeMetaSave / PlayerInfo / MergeMetaPersistence` | 零改动 | PASS(git status 核 — 上述文件未列) |
| **SV5**·同步等响应 | grep `OnRenameSubmit` 结构 | OnRenameSubmit 是 sync 转发 → OnRenameSubmitAsync 是 `async UniTaskVoid` + `await Attr.TryChangeAsync` | PASS |
| **SV6**·视图覆盖语义 | grep `ApplyDeltaPush / ApplyChangeResponse` 实现 + CV2/CV3 测 | 按 type 直接 set,无「旧值 + delta」相对推断 | PASS(CV2/CV3 测过) |
| **SV7**·启动接线时机 | grep `GameApp.StartGameLogic` 内 FantasyNetwork.OnProperty* 订阅位置 | 在 `FantasyNetwork.Boot()` 之后、登录前,挂订阅 | PASS |
| **SV8**·RenameReject 枚举不动 | `grep "enum RenameReject" PlayerRenameService.cs` | 仍 `None / Empty / TooLong / Profanity / NotEnoughDiamond` 5 项 | PASS |
| **SV9**·设计 25/18 同步落地 | grep 25 §七 O8 + §3.2 + §九 H1 + 18 §3.2 + §3.4 O8 | 全部已重写为「调 PlayerAttrService 接服务端」 | PASS(plan 阶段已完成,grep 复核 OK) |
| **CV1-CV7**·PlayerAttrService 单测 | `run_tests assembly=BlockBlast.Tests` | 全 PASS | PASS(9 例全过) |
| **W3 + 新加 3 例**·PlayerInfoWindow 改名扣钻单测 | 同上 | 全 PASS:RpcSuccess / RpcNotEnough / RpcServiceUnavailable / FirstRename | PASS |
| **E1**·真往返 启动 → 收快照 → IsReady | Fantasy 服务端 + MongoDB 起服 + Unity Play | 5s 内 IsReady=true + 三属性 = 服务端初值 (0,0,5);Log 见 `[Fantasy] 收到属性初始快照 Coin=0 Diamond=0 Stamina=5` | **未自跑**(dev 不进 PlayMode,留 test) |
| **E2**·真往返 改名扣钻(钻石充足) | 用 server 进程内 API 给 UUID 加 500 钻 → 客户端钻石面板刷 500;改名 → MongoDB players 集合钻石 -100、客户端钻石面板刷 400;`[Fantasy] 收到属性推送 Type=Diamond NewAmount=...` | 端到端通 | **未自跑**(留 test) |
| **E3**·真往返 改名扣钻(余额不足) | 钻石 = 50,改名 → 服务端拒「余额不足 NewAmount=50」→ 客户端 UI「钻石不足」+ MongoDB 钻石仍 = 50 + 客户端钻石面板刷为 50 | 端到端通 | **未自跑**(留 test) |
| **E4·零回归**·30/31/32/33/35/36/37 既有验收点 | 各自已 PASS 端到端用例仍 PASS;account / 登录链路 / 排行 / 邮件 / 兑换码全不动 | 全 PASS | **未自跑**(留 test;改动面只在新 .cs + 1 处 GameApp 加事件订阅 + GameContext 加成员,不动既有路径) |

#### 异常路径自检(已走查,部分在 CV/W 中)

- **PlayerAttr 初始化早于首次 Snapshot 到达**:`IsReady=false` 时改名按钮 disabled(W2 已加,但 W2 未在测试中显式断言 — 留 test 用 UI 真走查;主因测试反射调 OnRenameSubmit 不需经按钮)。RefreshRenameInteractable 在 OnAttrChanged + OnRefresh 内调,实测路径覆盖。
- **PlayerAttr 尚未初始化时收 Snapshot/Push**:Handler 内 GameContext.Instance.PlayerAttr 为 null → 仅 Log.Warning,丢弃(沿 37 §5.7 推送丢失下次登录拉新快照对齐口径)。GameContext.OnInit 内已 new PlayerAttrService,正常路径下 GameApp.StartGameLogic 早于任何登录响应,理论上不会发生。
- **改名时 Session 断开**:`RpcGatewayProd.SendChangeRequestAsync` 内先查 `IsConnected` / `IsLoggedIn`,断 → 返 NetworkDown / NotLoggedIn,UI 显示对应文案,不冒进。
- **RPC 异常(协议解析失败 / 超时)**:`RpcGatewayProd` 内 try/catch,均转 ServiceUnavailable,不抛(沿 RemoteRankSource 范式);**未加显式超时**(plan O3 默认 10s 是 PlayerAttrService 层超时口径,本子单 RpcGatewayProd 直接 await `session.Call`,该 API 内部已有 Fantasy 自带超时;若 test E2 发现超时不可控可后续加 UniTask.WhenAny 包外层)。
- **响应 + 推送乱序到达**:CV3 测试覆盖(同值幂等)+ §三模型 + §7.3 走查。
- **OnDestroy 解绑事件**:已加(沿 38 §7.4);未在测试中显式断言(UI bare new 不进 Init,纯反射调,OnDestroy 路径不易触).
- **AttrType.All 传入 TryChangeAsync**:防御性 — 直接返 TypeUnknown,不发 RPC(`TryChangeAsync_TypeAll_RejectedWithoutRpc` 测过)。
- **构造空 gateway**:抛 ArgumentNullException(`Constructor_NullGateway_Throws` 测过)。

### 4. 标注(交接必看)

- **涉及热更程序集**:`Assets/GameScripts/HotFix/GameLogic/` 在 `GameLogic` 程序集(热更);`Assets/Fantasy/Scripts/Handlers/` 在 `FantasyClient` 程序集(非热更,但 Fantasy.SourceGenerator 自动注册 Message<T>,无需热更更新)。
- **不需 Luban 重生成**:本段未碰 Luban 源 / 导表 / 配置二进制。
- **协议同步状态**:**无新增 / 删除协议**,客户端 `Assets/GameProto/*` 零改动(SV1),`Assets/Fantasy/Generate/NetworkProtocol/*` 也零改(服务端 37 已生成)。
- **需进 Play 模式手验的功能点(E1 / E2 / E3 真往返)**:
  - 起 Fantasy 服务端(`dotnet run --project examples/Server/APP/Main/Main.csproj --framework net9.0 -- --m Develop`)+ 本机 MongoDB(`D:\mongodb-portable` 27017 LISTEN)
  - Unity PlayMode 启动 → 自动登录链路通(Log 见 `[Fantasy] ✅ 登录成功` + `[Fantasy] 收到属性初始快照 Coin=0 Diamond=0 Stamina=5`)
  - 进 MainMenuWindow → 打开 PlayerInfoWindow(若有入口)→ 见钻石余额行(prefab 节点缺失退路:文本不显示,但日志 OK)
  - 用 server 工具或 33 排行结算路径触发 `服务端 ChangeProperty(UUID, Diamond, +500)` → 见 `[Fantasy] 收到属性推送 Type=Diamond NewAmount=500`
  - 改名 → MongoDB 查 `players._id=dev_<UUID>.properties.coin/diamond/stamina` 钻石 -100 + 客户端钻石面板刷新
  - 钻石不足时改名 → UI「钻石不足」+ MongoDB 钻石不变
- **BLOCKED 档**:本机 MongoDB / Fantasy 服务端不可达 → E1/E2/E3 判 BLOCKED 非 FAIL(沿 35/36/37 口径)
- **EditMode 单测 = 9 + 4 = 13 例覆盖 CV/W**(plan §9.1 CV1-CV9 大部 + W1-W6 大部);CV8(GameContext.PlayerAttr 持有)未单测 — 经 W3 系列间接覆盖(InitPlayerAttrWith → GameContext.PlayerAttr != null)
- **Code Review 5 红线复核**(对照 SKILL.md 核心红线):
  - ① 异步优先:`PlayerAttrService.TryChangeAsync` 返 UniTask;`RpcGatewayProd` 内 `session.Call` 是异步 await;`OnRenameSubmit` 改 async UniTaskVoid 经 Forget()。OK
  - ② 模块访问规范:HotFix → FantasyClient.FantasyNetwork 全限定 + #if FANTASY_UNITY 包裹(GameApp);PlayerAttrService 纯逻辑、不引 Fantasy 命名空间;RpcGatewayProd 在 #if FANTASY_UNITY 块内引用 Fantasy.*。OK
  - ③ 资源释放:OnDestroy 解绑 OnAttrChanged。OK
  - ④ 热更边界:核心档全在 HotFix(纯逻辑);非热更只加 2 薄壳 Handler(自动注册,无需热更)。OK
  - ⑤ 事件解耦:沿 FantasyNetwork.OnLoggedIn 范式加 OnProperty* 静态事件,GameApp 订阅;PlayerAttrService.OnAttrChanged 是业务层事件。OK

### 5. 设计边界(诚实标注,非缺陷,交 test/boss 知会)

- **钻石余额行 prefab 节点暂未接入**:`m_text_DiamondBalance` 在 PlayerInfoWindow.prefab 中不存在 → RefreshDiamond 内 null-safe 不崩,但 UI 上不显示钻石余额(日志 OK);属 art 后续刀(设计 25 §3.2 已声明该节点路径)。本子单不动 prefab(art 受限退路与 28 排行榜窗 prefab 受限同口径)。
- **改名扣钻同步等响应的「本地校验失败」边界**:RPC 在前、本地校验在后 → 服务端已扣钻但本地拒(空 / 长度 / 屏蔽字)的情形,玩家钻石确实扣了。处置:UI 层 InputField 提前拦空/长(prefab 设 characterLimit + onValueChanged 校验是常规做法),屏蔽字本子单接空词表(O6 去变现),实际触发概率低;服务端已扣钻在改名失败路径仍刷视图(玩家可见),属可接受档。深度修要么把改名移到「校验 → RPC → 落名」三步、要么把校验改成无副作用 dry-run(需改 PlayerRenameService 添加 dry-run 模式)— 都超出本子单范围。
- **RpcGatewayProd 无显式超时**:`session.Call` 内部依赖 Fantasy 自带超时(默认 5s 或配置);plan O3 默认 10s 是 PlayerAttrService 层口径,本子单 RpcGatewayProd 直接 await,未在外层加 UniTask.WhenAny 包装。若 test E2 发现超时不可控可后续加。
- **登出后客户端 PlayerAttrService 状态**:本子单不接登出钩子(36 已落 Shutdown/Boot 一对);若登出 → Shutdown → Boot 再登,会收新 Snapshot 覆盖三属性视图(IsReady 不主动重置为 false,但下一次 Snapshot 把 IsReady 重置为 true 是冪等的)。Tier 1+ 若需「登出时 IsReady=false」可加 OnDisconnected 订阅 — 本子单不加,沿 37 §5.7 推送丢失下次登录对齐口径。
- **金币 / 体力 UI 显示**:本子单仅 PlayerInfoWindow 改名面板加钻石余额行;金币 / 体力的 HUD 全屏显示 = 表现层后续刀(设计 25/27/HUD)。Coin/Stamina 数据层已挂入,后续业务玩法只需调 `Attr.TryChangeAsync(AttrType.Coin/Stamina, ...)` 即可,接入面最小。

### 6. 需 boss/用户复核

无阻塞项。plan §taskFlaw 已亮简报基线错(数据源迁移 + 反作弊缺口 = 无源可迁 + 占位非缺口),按现状重切范围已落地,boss 关单时复核 taskFlaw + 范围切分是否符合预期。

---

## 返修轮次 1(2026-06-20):FAIL-1 修复

### 修复点

test 报告唯一 FAIL 项:`design-docs/18-player-info.md` 第 26 行 WARNING 块第 5 条⑤过时正文(「钻石尚无可花费余额…故扣减为空操作」)与 §3.4 O8「已兑现」语义矛盾。

### 改动文件

仅 `design-docs/18-player-info.md` 一篇,3 处同源过时口径覆盖式重写为「O8 已兑现」现状(非加勘误注):

| 行 | 节 | 改前要点 | 改后要点 |
|---|---|---|---|
| 26 | 读前必看 边界⑤ | 「钻石尚无可花费余额,故扣减为空操作,逻辑层照样可测」 | 「经服务端账本扣减(O8 已兑现,客户端段实现见设计 38);去变现 = 不实装购买入口,余额由发奖路径注入」 |
| 34 | 立项信息 方向约束 | 「改名扣钻仅作『读价 + 尝试扣减』逻辑,钻石无可花费余额时为空操作」 | 「改名扣钻经服务端账本扣减(设计 37 权威 + 设计 38 客户端调用),余额由发奖路径注入(去变现 = 不实装购买入口,非『钻石无余额』)」 |
| 103 | 二、对照表 钻石扣费行 | 「扣减经数值路径尝试;钻石无可花费余额时为空操作」 | 「扣减经服务端账本(设计 37 权威);客户端调用见 §3.2 / 设计 38,失败时钻石不动且改名拒绝」 |

> 本应只改第 26 行(test 报告点出处),但同源过时口径在 34、103 行还有命中——conventions §6「改一处即同步被它过时的他篇」要求一并清理,否则下次审计仍会被翻出。

### 自检

- 编译/单测/真往返:本轮仅文档改动,test 报告明示无需重跑(test §六 FAIL-1 修复影响范围 = 仅文档一致性)
- `grep "尚无可花费余额|扣减为空操作|待钻石实装|钻石未实装|TODO.*钻石|钻石无可花费余额"`:18-player-info.md 零命中
- 与 §3.2(第 156 / 168-169 行)+ §3.4 O8(第 382 行)现状陈述全文一致,无新引入矛盾
- 改写体例沿 conventions §6 覆盖式重写,无勘误注叠旧

### 复检指引(交 test)

仅需 grep 复核 + 通读 18 第 18-26 / 32-34 / 95-103 三段是否「O8 已兑现」一致。编译/单测/真往返不重跑。
