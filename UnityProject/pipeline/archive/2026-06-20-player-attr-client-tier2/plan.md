# plan 状态

当前任务:Tier 2 玩家属性权威 · 客户端段 · 第 2 子单(钻石首次实装 + 接服务端 + 改名扣钻真接线)

## 范围与决策(本子单 = Tier 2 客户端段收口;按现状重切范围,自治默认推进 + taskFlaw 上报简报基线错)

- **设计稿**:新建 `design-docs/38-player-attr-client.md`(`nav.js` GROUPS 在「全栈 / 服务端」组 36 / 33 之间注册)
- **范围(按现状重切)**:本子单 = 「**钻石(及金币/体力)作为元层属性首次实装** + 改名扣钻一个真接线点端到端经服务端校验」 而非 「数据源迁移 + 反作弊缺口关闭」(简报描述与现状基线不符,见 §taskFlaw)
- **核心档**:① `PlayerAttrService`(命名空间 `GameLogic.BlockBlast.Player`,纯逻辑 C# 类,持 Gold/Diamond/Energy + 订阅 G2C_PropertyDeltaPush + 接 G2C_PropertyInitSnapshot + 提供 TryChangeAsync 同步等响应);② `GameContext.PlayerAttr` 挂入;③ 启动接线(`FantasyNetwork` 消息分发钩子 + `OnLoggedIn` + GameContext 初始化);④ `PlayerInfoWindow.cs:152` 改名扣钻 `trySpendDiamond` 改为「同步调 PlayerAttrService.TryChangeAsync」;⑤ PlayerInfoWindow 改名面板加钻石余额行(支撑「钻石不足」可观测)
- **增强档(默认取,代码量边际增加 0)**:`PlayerAttrService` 同步持 Gold/Energy(无 UI 接入,后续业务玩法刀按需消费)
- **可砍档(默认砍)**:① 金币玩法路径接入(全工程无金币消费/产出路径,无可接);② 体力玩法路径接入(Energy 是 merge-order Demo 局内态非元层余额,接入需先升级为「跨会话余额 + 局内消费」二层模型,超本子单范围);③ HUD 全屏显示三属性(UI 表现层职责,需美术 + 设计 25/27 配套);④ ledger 历史(Tier 2+ 后续刀);⑤ PlayerNameGenerator 改动(简报误指,该文件与属性无关)

## 安全默认采纳(self-determined,自治默认推进)

- **D1 客户端三属性数据层独立挂入 `GameContext.PlayerAttr`,不并入 `PlayerInfo`**:职责正交(`PlayerInfo` = 本地玩家信息层,`PlayerAttrService` = 服务端账本视图)+ 持久化口径不同(`PlayerInfo` 落 `MergeMetaSave`,`PlayerAttrService` 不持久)+ 沿 36 GameContext 多服务持有范式
- **D2 三属性视图不入本地存档**:沿 37 客户端不持权威值红线;`MergeMetaSave` / `PlayerPrefs` 不加三属性字段 / key;避免本地态与服务端账本两份漂移
- **D3 改名扣钻同步等响应,不 fire-and-forget**:`OnRenameSubmit` 改 `async UniTask`,`trySpendDiamond` 内核走 `await PlayerAttrService.TryChangeAsync(Diamond, -cost, "player_rename").ContinueWith(r => r.Success)`;失败 → 不写名 + UI 显示拒绝原因;若 fire-and-forget = 「先成功后扣钻」错位 + 两端不一致 + 出错难追溯
- **D4 RenameReject 枚举不动**:5 项(`None / Empty / TooLong / Profanity / NotEnoughDiamond`)保持;服务端新错码(余额不足 / 服务暂不可用 / 未登录 / 网络断 / 上界溢出)在 `PlayerInfoWindow.ShowRenameReject` 内按 `ChangeResult.Reason` 分支映射文案,避免扩散面到设计 18 数据层契约
- **D5 服务不可用拒改名,不本地放行**:沿 30 兑换码不本地放行口径,关闭超发面;服务端不可达 → 改名拒,UI 显示「网络异常,请稍后再试」
- **D6 视图覆盖语义**:`ApplyDeltaPush` / `ApplyChangeResponse` 按 type 直接 set 新余额(沿 37 §5.4 推送是绝对快照),不做「旧值 + delta」相对推断,顺序无关
- **D7 `IsReady = false` 时禁改名**:`PlayerInfoWindow` 在 `OnRefresh` / `OnCreate` 内检查 `IsReady`;false → 钻石面板显示「加载中...」+ 改名按钮 disabled;`OnAttrChanged` 订阅触发 enabled
- **D8 启动接线落点**:`FantasyNetwork.cs` 加 2 处消息分发钩子(`G2C_PropertyInitSnapshot` / `G2C_PropertyDeltaPush` → `GameContext.Instance.PlayerAttr.Apply*`);`GameContext.OnInit` 加 `PlayerAttr` 创建;接线在 `GameApp.StartGameLogic` 启动早期,确保 Snapshot 不丢

## 守不变量(给 dev / test 的硬边界)

- **服务端段 37 契约零改**:三 RPC(快照 / 通用变更入口 / 推送)+ 服务端进程内变更 API + 校验体系 + 反作弊红线;Fantasy 仓零 diff
- **既有四个全栈特性(30 / 31 / 32 / 33)+ 35 + 36 + 37 全部 PASS 验收点不破**:本子单只在 `Assets/GameScripts/HotFix/*` 加文件 + 改 2 处(`GameContext.cs` + `PlayerInfoWindow.cs`)+ `Assets/Fantasy/Scripts/*` 加薄壳分发钩子;不动 35 LoginUI 真空壳 / 36 登出接线 / 37 服务端段任何契约
- **`PlayerInfo` / `MergeMetaSave` / `PlayerPrefs` 不加三属性字段 / key**:本子单红线,避免两份漂移(D2)
- **`RenameReject` 枚举 5 项保持不动**:服务端新错码经 UI 内分支映射(D4)
- **`PlayerNameGenerator` 不动**:文件仅含名字生成,与属性无关;简报误指为「反作弊核心缺口」(实为名字生成器,见 §taskFlaw)
- **客户端绝对值禁报红线**:`PlayerAttrService.TryChangeAsync` 请求负载**仅含**类型 + delta + reason 三字段;**不**传当前余额(沿 37 §3.3.2)
- **同步等响应,不 fire-and-forget**(D3):避免改名先成功后扣钻错位

## 客户端段交付清单(给 dev)

> dev 开工第一步 = **核 fantasy-net 37 服务端段已 PASS 的三 RPC 实际消息名 + 字段名**(沿 37 服务端段实际落地的字段名取,非按本设计稿口径——本稿沿 37 code-blind 不指字面量;dev grep 服务端段已 PASS 的 proto / 生成物取)。其它落地按本节清单。

### 客户端段落地清单(按 38 §四服务契约 + §五接线 + §六时序)

| 落点 | 行为级完成定义 |
| --- | --- |
| 新建 `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/PlayerAttrService.cs` | 纯逻辑 C# 类(不 using UnityEngine、可纯 EditMode 测);命名空间 `GameLogic.BlockBlast.Player`;字段 `Gold/Diamond/Energy`(int 只读对外)+ `IsReady`(bool);方法 `ApplySnapshot(int gold, int diamond, int energy)`(覆盖三属性 + 置 IsReady = true + 触发 OnAttrChanged All)、`ApplyDeltaPush(AttrType type, int newBalance, string reason)`(按 type set + 触发)、`ApplyChangeResponse(同 ApplyDeltaPush)`、`UniTask<ChangeResult> TryChangeAsync(AttrType type, int delta, string reason)`(经构造期注入的 IRpcGateway 发 RPC + 等响应,超时 10s);事件 `event Action<AttrType, int, string> OnAttrChanged` |
| 新建 `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/AttrType.cs` | 枚举 `AttrType { All = 0, Gold, Diamond, Energy }`;映射服务端三类(具体值与服务端枚举值对齐,dev 据 37 服务端段实际值取)|
| 新建 `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/ChangeResult.cs` | 结构 `(bool Success, ChangeReject Reason, int NewBalance)`;枚举 `ChangeReject { None / NotEnoughBalance / TypeUpperOverflow / TypeUnknown / ServiceUnavailable / NotLoggedIn / NetworkDown }` |
| 新建 `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Player/IRpcGateway.cs` | 接缝接口 `interface IRpcGateway { UniTask<ChangeResult> SendChangeRequestAsync(AttrType type, int delta, string reason); }`(纯接缝,生产 + 测试两实现) |
| 新建 `Assets/Fantasy/Scripts/RpcGatewayProd.cs`(或同等位置) | 生产实现 `RpcGatewayProd : IRpcGateway`,内部经 `FantasyNetwork.Session.Call<C2G_PropertyChangeRequest, G2C_PropertyChangeResponse>` 调用 + 转换 `ChangeResult`(成功 → 用服务端返码 → ChangeReject;Session = null / 异常 → ServiceUnavailable / NetworkDown);try/catch 内异常 → ServiceUnavailable |
| `Assets/Fantasy/Scripts/FantasyNetwork.cs` 加消息分发钩子 | 在 `OnLoggedIn` 或 `OnConnectComplete` 后,注册 `G2C_PropertyInitSnapshot` / `G2C_PropertyDeltaPush` 的 handler(沿 Fantasy.Net 既有 handler 注册约定);收到消息 → `GameLogic.GameContext.Instance.PlayerAttr.Apply*`(经反射 / 直接引用,沿 36 OnLoggedIn 已有的跨域调用方式);**仅薄壳分发,业务逻辑全在 HotFix `PlayerAttrService`** |
| `Assets/GameScripts/HotFix/GameLogic/GameContext.cs` 加成员 | 加 `public PlayerAttrService PlayerAttr { get; private set; }`;`OnInit` 内加 `PlayerAttr = new PlayerAttrService(new RpcGatewayProd())`;加测试入口 `public void InitPlayerAttrWith(IRpcGateway gateway) { PlayerAttr = new PlayerAttrService(gateway); }`(沿 InitSettingsWithStore 范式) |
| `Assets/GameScripts/HotFix/GameLogic/UI/PlayerInfoWindow.cs` 改名扣钻接缝改写 | `OnRenameSubmit` 改 `async UniTaskVoid` / `async UniTask`;内部:① 若 `RenameCount == 0`(首次免费)→ 直调 `PlayerRenameService.TryRename(P, newName, 空词表, _ => true)`(不发 RPC);② 否则 → `await GameContext.Instance.PlayerAttr.TryChangeAsync(AttrType.Diamond, -RenamePriceConfig.PriceFor(P.RenameCount), "player_rename")` → 把 `result.Success` 包成 `trySpendDiamond: _ => result.Success` 调 `PlayerRenameService.TryRename`;③ 失败时 `ShowRenameReject` 按 `ChangeResult.Reason` 内部分支选文案(细分见 38 §六对照表);④ 成功 → 写名 + `SavePlayer`(同既有路径) |
| `PlayerInfoWindow` 钻石余额行 | 在改名面板下方加一行 Text(节点 `Root/NameBlock/m_text_DiamondBalance` 或同位置)显示当前钻石余额(读 `GameContext.Instance.PlayerAttr.Diamond`);`PlayerAttrService.OnAttrChanged` 订阅刷新;`IsReady = false` 时显示「加载中...」+ 改名按钮 disabled |
| `PlayerInfoWindow` UI 子图 | 钻石图标(可复用 17 reward-display 的 num 图标或占位文字「钻石」二字);本子单不强求新切图,art 受限退路 = 纯文本「钻石:X」 |

### dev 须自行据工程现状定的实现选择

- 服务端 37 段三 RPC 的实际消息名 / 字段名 / 错码值(以服务端段已 PASS 的 proto / 生成物为准 grep 取;本稿沿 37 code-blind 不指字面量)
- `FantasyNetwork.cs` 消息分发钩子的具体注册方式(沿 35/36 已落地的 OnLoggedIn / Boot 内 RPC 注册约定;若是 attribute-based 自动注册则放对应 namespace,若是手动注册则在 `OnConnectComplete` / `OnLoggedIn` 钩子内加)
- `UniTask` vs `FTask` 选择(沿 HotFix 既有 UniTask 范式 + Fantasy.Async.FTask 边界 — `IRpcGateway.SendChangeRequestAsync` 在 HotFix 侧返 UniTask,内部经 `FTask` 等响应再 `await` 转 UniTask)
- 测试用桩 `IRpcGateway` 的形态(沿 22/28 测试 fake 范式)
- `PlayerInfoWindow` 钻石面板的具体节点路径(以 prefab 实际节点为准,设计 25 §节点树命名已建立)
- 超时实现(`UniTask.WhenAny(rpcCall, UniTask.Delay(timeout))` 或同等)
- `OnRenameSubmit` 改 async 后,UI 上下文丢失防护(沿 HotFix 既有 async UI 范式;若无,加 `try { ... } catch { ... }` 兜底)

## 验收标准(test 逐条核)

参见 38 §九 全部验收点(CV1-CV9 + W1-W6 + E1-E3 + SV1-SV9)。主验:
- **SV1 编译 0 error**(dotnet / Unity)
- **SV2 PlayerAttrService 不依赖 UnityEngine**(可纯 EditMode 测)
- **SV3 客户端绝对值禁报**(请求负载仅含类型 + delta + reason)
- **SV4 三属性不入本地存档**(MergeMetaSave / PlayerPrefs 无三属性)
- **SV5 同步等响应**(OnRenameSubmit 是 async,无 GetResult 同步阻塞)
- **SV8 RenameReject 枚举不动**(5 项)
- **SV9 设计 25 / 18 同步落地**(grep 25 §七 O8 + §3.2 + §九 H1 + 18 §3.2 + §3.4 O8 全部已重写,本任务 plan 已完成)
- **CV1-CV9 PlayerAttrService 单测**(EditMode,纯逻辑)
- **W1-W6 PlayerInfoWindow 改名扣钻单测**(EditMode 可达部分,注桩 `IRpcGateway`)
- **E1-E3 真往返**(Fantasy + MongoDB,改名扣钻 + 钻石余额刷新 + 余额不足拒)

辅验:外加 dev 改 25 / 18 同步项的 grep 复核(test 应验本任务交接区已注明的两处他篇同步落地)

### BLOCKED 边界

- 本机 MongoDB / Fantasy 服务端不可达 → E1 / E2 / E3 判 **BLOCKED 非 FAIL**(沿 35/36/37 口径 + memory `local-mongodb-for-server-roundtrip` + server-test memory `feedback-blocked-vs-fail`)
- 编译 + Code Review(SV1-SV9)+ EditMode 单测(CV1-CV9 + W1-W6)照常验

## 已拍板决策(decisions,自治默认推进,要改另开增量)

- **设计稿 = 新建 38-player-attr-client.md**(`nav.js` GROUPS 已注册「全栈 / 服务端」组,放在 36 之后 33 之前);**设计 18 §3.2 + §3.4 O8 + §九风险表 + 25 §读前必看方向约束 + §六改名子节 + §七控件分流 + §九 W3 同步重写**(conventions §6 覆盖式)
- **D1-D8 全部据安全默认推进**(详见 §安全默认采纳节);全部记 decisions,不入 blockers
- **范围按现状重切**:核心 = 钻石数据层 + 改名扣钻;增强 = 金币/体力数据层挂入;砍 = 玩法接入 + HUD + ledger + PlayerNameGenerator(后者文件本就无关)
- **简报描述与现状不符的部分按现状解读**:未停机走 boss 重派,因目的(让钻石客户端段接服务端)明确且可推进;但 taskFlaw 字段亮出基线错让 boss 复核(下方 §影响半径节列具体)
- **PlayerNameGenerator 零改动**:简报指其为「反作弊核心缺口」实为名字生成器(`PlayerNameGenerator.cs` 31 行 = `static class` 含 `Generate(System.Random rng)`),与属性 / 校验无关;真实占位行 = `PlayerInfoWindow.cs:152`(本子单改写处)
- **不为 PlayerPrefs 三属性 key 退役开 sweep**:本就无此 key(grep 全 HotFix:PlayerPrefs 命中仅在 settings / mail / rank / save-system 等本职位置)
- **不让 RenameReject 枚举扩展**(D4):新错码 UI 内分支映射,避免改设计 18 数据层契约

## 影响半径(本次改动 + 同步落点)

- **设计稿**:
  - 新建 `design-docs/38-player-attr-client.md`(`nav.js` GROUPS 已注册;首页卡片 / 侧边栏自动同步)
  - **同步重写 `design-docs/18-player-info.md`**(本任务 plan 已完成):§3.2 改名子节 + §3.4 O8 + §九风险表对应行
  - **同步重写 `design-docs/25-player-info-window-art.md`**(本任务 plan 已完成):§立项方向约束 + §3.2 改名实做(伪代码示例)+ §七控件分流表改名行 + §九 W3 验收点
- **代码**(dev):
  - 客户端新增 4 文件(`PlayerAttrService.cs` / `AttrType.cs` / `ChangeResult.cs` / `IRpcGateway.cs`,全在 `GameLogic/Module/BlockBlast/Player/`)
  - 客户端 Fantasy 接线 1 新文件(`Assets/Fantasy/Scripts/RpcGatewayProd.cs`)+ 1 改 `FantasyNetwork.cs` 加 2 钩子
  - 客户端改 2 文件:`GameLogic/GameContext.cs`(+成员 + InitPlayerAttrWith)、`GameLogic/UI/PlayerInfoWindow.cs`(改名扣钻 + 钻石面板)
  - 协议生成物(`Assets/GameProto/*`)若 37 服务端段已生成则零改,否则随服务端段同步(沿 30/31/32/35/37 范式)
- **不同步**(明令不改):服务端 37 任何契约、`PlayerInfo` / `MergeMetaSave` / `PlayerPrefs` / `PlayerNameGenerator` / `PlayerRenameService` 内核、其它 UI 窗(Tier 2+ 表现层后续刀)、Fantasy 仓零 diff

## taskFlaw(简报基线与现状的差异,呈 boss 复核)

简报基于过时 / 误读快照,与现状代码不符,逐项列证据(grep + Read 核实):

1. **「PlayerNameGenerator.cs:152 钻石消费校验恒返 true 反作弊缺口」**:该文件**不存在第 152 行**(全文件 31 行,只是名字生成器 `static class PlayerNameGenerator { Generate(System.Random rng) }`,逐字符抽 6 字符);**与钻石校验无关**。真实占位行 = `PlayerInfoWindow.cs:152` `trySpendDiamond: cost => true`;且**不是反作弊缺口**——`PlayerRenameService.cs:48` 注释:「钻石 num_id=3 无可花费余额字段,生产默认返 true,待钻石实装接真实扣减不返工」;`ItemGrant.cs:85`:「钻石(=3)数值系统**尚未实装专门字段**」;设计 18 §3.4 O8 + 设计 25 §七 O8 显式声明。「占位」与「缺口」本质不同。
2. **「客户端 PlayerPrefs 持金币/钻石/体力」**:`grep -rn PlayerPrefs Assets/GameScripts/HotFix/` 命中只在 settings / mail / rank / save-system / player-info 等本职位置,**无三属性 key**。
3. **「Player 模块持有金币/钻石/体力,改持有方式」**:`PlayerInfo`(132 行)字段集 = `Id / Name / RenameCount / Exp / CurrentAvatarId / CurrentFrameId / UnlockedAvatarIds / UnlockedFrameIds`,**不持三属性**;`MergeMetaSave` DTO 同样不持。「改持有方式」无对象。
4. **「玩法路径:打方块拿金币 / 改名消费钻石 / 体力消耗」**:① 打方块**无金币产出**(`ClearSettlement` + `MergeOrderState` 全文件:产消除得分 / 元素 / 触发宝箱 / 触发盲盒,无金币);② 体力 = `BlockGameState.MergeState.Energy` 是 **merge-order Demo 局内态**(开局 Reset),非跨会话元层余额;③ 唯一有 spec 的钻石消费 = 改名扣钻(`PlayerInfoWindow.cs:152`)。
5. **「数据源迁移」**:无源可迁(1 + 2 + 3 + 4 推论);本子单实质 = 「**钻石作为元层属性首次实装**」。

**影响**:按简报原范围(数据源迁移 + 反作弊缺口关闭)开工,dev / test 都会基于错前提工作:dev 找不到 PlayerPrefs 三属性 key 去删 / 找不到 PlayerNameGenerator.cs:152 去修;test 验收点也无法核实。

**本 plan 的处置(不停机)**:以现状为基线,把任务实质目标(让客户端钻石/金币/体力接服务端、收口 Tier 2 全栈)按现状切分为「核心档 + 增强档 + 可砍档」实现,目的不变。简报基线错呈 boss 在关单复核时改写;若 boss 要求按原简报口径打回(数据源迁移 + 关缺口)→ 工程上无法做,届时再返工。

## 自检(plan 收尾必做,conventions §「收尾必做」)

- [x] **过程性内容不在正文**:38 设计稿无 diff 叙事 / 无「按你说的改成」/ 无对话痕迹;§读前必看 + §立项 + §一-§十一 全是事实陈述与设计契约;现状审计节(§二)是基线证据陈述非过程
- [x] **正文无可推导事实的副本**:本子单引用文件路径 / 行号(`PlayerInfoWindow.cs:152` + `PlayerRenameService.cs:48` + `ItemGrant.cs:85` + `PlayerNameGenerator.cs:31` 全文件)是**现状审计接线点**类设计稿(conventions §3 design-docs 例外:Tier 收口刀类设计需引用接线点位置作 dev 落地锚);非「设计基线」类符号引用;`AttrType / ChangeResult / IRpcGateway` 是本设计稿**声明的新概念**(同 35/36/37 范式声明新组件),非引用既有代码
- [x] **正文无拟人 / 口语比喻**:grep 「死 / 打死 / 钉死 / 焊死 / 绑死」无;「合上 / 铺地基 / 收口」是工程常用名词非比喻(沿 30-37 既有口径)
- [x] **工作态内容可识别所属任务**:本 state 文件头部明标「Tier 2 客户端段 · 第 2 子单」+ taskFlaw 节呈 boss 复核
- [x] **被改动规范的旁注仍成立**:本子单同步重写设计 18 + 25 各 3 处,逐处旁注仍成立(O8 改「已兑现」、§3.2 改「接服务端」、§3.4 §九 H1 同步映射);无孤儿理由
- [x] **过时正文已重写或删除**:18 §3.4 O8 + §3.2 + §九风险表 + 25 §读前必看 + §六 OnRenameSubmit 伪代码 + §七控件分流 + §九 W3 已重写到现状(钻石经服务端);**不留**「钻石未实装」「待钻石实装」「TODO 钻石实装」类过时叙事(conventions §6);grep 三篇 + 38 篇:`待钻石实装 / 钻石未实装 / TODO.*钻石` 已全部清理或重写为现行表述
- [x] **design-docs 正文 code-free 大体守住**:38 正文规范本体的代码符号仅为本设计稿声明的新类型(`PlayerAttrService / AttrType / ChangeResult / IRpcGateway / ChangeReject`,沿 35-37 范式)+ 现状接线点(`PlayerInfoWindow.cs:152` / `PlayerRenameService.cs:48` / `ItemGrant.cs:85` / `PlayerNameGenerator.cs`,在 §读前必看 + §二现状证据 + §三相关位置作 dev 落地锚,符合 design-docs 「接线点」例外)+ 既有引用(`PlayerInfo / MergeMetaSave / GameContext / FantasyNetwork / OnLoggedIn / RenameReject` 在关系表 / 影响半径节作锚);**正文规范本体无随意符号点缀**,与 30-37 既有全栈稿同口径

## 自治审计(本环节自主拍板的取舍,简记)

- **简报基线错处置**:不停机走 boss 重派 — 因目的(让钻石客户端段接服务端、收口 Tier 2 全栈)明确、可推进;按现状重切范围 + 在 taskFlaw 字段亮出基线错让 boss 关单复核;若 boss 要求按原简报口径打回 → 工程上无法做,届时再返工。这是「立场朝结果(让 Tier 2 收口落地)、不朝当下势头(按简报错前提硬做)」的典型独立成判
- **范围闸**:核心档 = 钻石数据层 + 改名扣钻 + GameContext 挂入 + 启动接线(本子单完整范围,Tier 2 客户端段收口的最小可观测改动);增强档 = 金币/体力数据层一同挂入(代码量边际增加 0);可砍档(全部砍掉)= 金币/体力玩法接入 + HUD 全屏 + ledger + PlayerNameGenerator 改动 + RenameReject 扩展。所有可砍档归 Tier 2+ / Tier 3 / 简报误指,本子单只留核心 + 增强,范围已收敛
- **整局走查崩法 6 类齐**(§七 7.1-7.6):快照未到 / 改名时网络断 / 改名同时余额被推送变更 / 钻石未刷新 UI / 服务端拒后客户端冒进 / RPC 异常断连;每类均给对策;§7.7 诚实边界明示守 7 项 + 不守 7 项防 test/boss 误判范围
- **整局走查向上对体验**:① 诚实玩家:钻石余额首次在 UI 可见(改名面板);改名扣钻经服务端校验真实生效(过去是 cost => true 形同虚设;现在余额不足时会拒)——但首次免费保留,玩家无感差异;② 工程:Tier 2 服务端段地基首次端到端验证;后续业务玩法刀(商店 / 兑换码奖励 / 邮件领奖等)只需调 `PlayerAttrService.TryChangeAsync`,接入面最小;③ 反作弊:服务端 37 已建权威,本子单让客户端真把 RPC 走起来,反作弊基线落地。三类都答得上「让什么体验/工程目标更好」
- **不停机走 boss 重派的理由(对照 conventions «常规模式先问 boss» 例外触发)**:auto 模式 + 简报目的清晰 + 现状已收集足证据可据证拍板 = 第 ② 类「无明显默认 → 调查取证后据证定最优解,记 decisions 不入 blockers」;blockers 仅留给第 ③ 类(调查也定不了且不可逆且抵触 GDD 原文 — 本子单不抵触 GDD,GDD 说「钻石经服务端账本」本子单做的正是它;只是简报描述与现状不符,目的本身合理)
- **PlayerNameGenerator.cs:152 「反作弊缺口」表述的处置**:在 38 §读前必看 + §二现状证据 + 本 plan taskFlaw 三处显式呈现 boss 看到;不在设计稿 / state 中静默把它解读为「真实意图是 PlayerInfoWindow.cs:152」就过去 — 因这等于替简报作者改需求,违「独立成判 + 立场朝结果(让 boss 看见基线问题)」;但同时按现状重切范围推进,不让自治流水线空停
