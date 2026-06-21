<style>
  pre.code { margin:8px 0; padding:12px; background:#0d1622; border:1px solid var(--border); border-radius:8px; overflow:auto; font-size:13px; line-height:1.55; }
  table.tight td, table.tight th { padding:6px 10px; }
  .pill-core{ display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(91,214,160,.16); color:#5bd6a0; margin-left:6px; }
  .pill-enh { display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(108,140,255,.16); color:#6c8cff; margin-left:6px; }
  .pill-cut { display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(255,122,138,.16); color:#ff7a8a; margin-left:6px; }
</style>

# tarot HUD 三属性绑定 · 表现层接续刀(Tier 2 第 3 子单)

把 Classic 主玩法窗 `GameWindow` 顶栏 3 资源条从「占位 + 第 1 条接 HighScore + 加号 stub」改为绑定 `PlayerAttrService` 的金币 / 钻石 / 体力实时余额,订阅 `OnAttrChanged` 自动刷新。**收口** Tier 2 「真实玩家属性权威体系」的客户端表现层链路(数据层 38 已建、HUD 接入是其表现层后续刀)。

> [!NOTE]
> **立项信息**
>
> | 项 | 内容 |
> | --- | --- |
> | **类型** | 表现层接续刀 · Tier 2 真实玩家属性权威体系第 3 子单(客户端段 · HUD)。承接 38 客户端数据层(`PlayerAttrService` + `GameContext.PlayerAttr` + 订阅范式);本子单只把已有数据层接进 Classic 主玩法窗顶栏视图。零新增协议 / 零新增数据层类 / 零改服务端。 |
> | **方向约束** | 离线还原 · **去变现**:HUD 顶栏三属性是**只读余额展示**,无内购 / 充值 / 看广告;资源条加号点击保留为「待建 Log」(与设计 27 §5.3 现状一致,本子单不接购买路径);加法式接入,主玩法逻辑零回归(玩法窗在跑,这是本子单第一硬约束)。 |
> | **需求降层** | **a. 表层要求**(boss 简报):tarot 主玩法 HUD 顶栏 3 资源条改为绑 `PlayerAttrService` 金币 / 钻石 / 体力余额,订阅服务端推送实时刷新。 **b. 底层目的**:让玩家在 Classic 整局过程中**实时可见自己的三属性账本** —— 38 已让数据层落地、改名扣钻一处真接线点端到端验证,但玩家在跑 Classic 时看不到余额(顶栏第 1 条显的是 HighScore = 非元层资源、第 2/3 条 0);把 HUD 接进 `PlayerAttrService` 后,后续业务玩法刀(任务奖励 / 邮件领奖 / 兑换码到账)产生的余额变动玩家**在主玩法窗即可观察到**,无需进 PlayerInfoWindow 才能看见。这是 Tier 2 客户端表现层的最后一公里。 **c. 有无更直达 b 的做法**:b 的本质 = 「HUD 顶栏显示三属性 + 余额变动实时刷新」,本设计是最直达做法(订阅 `OnAttrChanged` 范式 38 已建,PlayerInfoWindow 已用,本子单复用)。**备选(已否)**:① 不接 HUD,只让 PlayerInfoWindow 显余额 → 玩家进主玩法窗后看不到余额变动,Tier 2 客户端表现层不收口;② 加号接进购买流(违去变现);③ 把 HUD 三属性改成可编辑 / 可花费按钮(本子单是「显示接入」,具体消费路径归各业务玩法刀)。 |
> | **范围(产品 · 玩法)** | **改既有**:`GameWindow.cs` `BuildTopBar()` 三资源条数字位绑定改写(第 1 条 Coin、第 2 条 Diamond、第 3 条 Stamina,顺序见 §三);新增订阅 `PlayerAttrService.OnAttrChanged` + `OnDestroy` 解绑(防泄漏)+ `IsReady=false` 加载中态。 **不动**:`PlayerAttrService` / `IRpcGateway` / `GameContext` / `AttrType` / `RpcGatewayProd`(38 数据层零改);`PlayerInfoWindow` 钻石余额行(已建,本子单不动);`MergeOrderWindow`(不碰,合成订单玩法门控独立);Classic 玩法逻辑(`RenderBoard` / `RenderSlots` / 落子 / 消除 / ghost / GameOver / `OnUpdate` / `BlockLayout` / `BlockGameState`);切图 / 精灵表 / 收集器;HighScore 显示位(已挪到大分数下方独立 `Best` 文本,设计 27 §5.1,本子单不动)。 **协议**:零新增。 **服务端**:零改(沿 37 三 RPC + 推送)。 |
> | **关键约束** | 主玩法窗在跑,**零玩法回归**是硬约束:本子单只动 `BuildTopBar()` 的 3 处文本初值 + 1 处订阅注册 + 1 处解绑,其它行不改;`PlayerAttrService.IsReady=false`(快照未到)时三资源条显示「—」(加载中态),不显示「0」误导玩家;`OnAttrChanged` 订阅在 `OnCreate` 末挂、`OnDestroy` / `OnCloseInternal` 解(沿 PlayerInfoWindow 范式);事件回调在主线程(Fantasy Scene 已是主线程,沿 38 §7.4 已论证);加号点击行为不变(仍 `Log` 待建,去变现)。 |

## 一、做什么与为什么 {#what}

现状(grep 核实):

| 现状 | 位置 | 表现 |
| --- | --- | --- |
| `GameWindow.BuildTopBar()` 3 资源条数字 | `GameWindow.cs` BuildTopBar 内 | 第 1 条显 `_initialHigh`(Classic HighScore,设计 27 §5.3 D1 默认),其余 2 条静态 `"0"`,加号 `Log` 待建 |
| `PlayerAttrService` 数据层 | `Module/BlockBlast/Player/PlayerAttrService.cs` | 38 已落,持 `Coin/Diamond/Stamina`,有 `OnAttrChanged` 事件 + `IsReady` 标记 |
| `GameContext.Instance.PlayerAttr` | `GameContext.cs:37` | 已挂入,生产用 `RpcGatewayProd`(沿 38 §五接线) |
| `PlayerInfoWindow` 钻石余额行 | `PlayerInfoWindow.cs:106-160` | 已订阅 `OnAttrChanged` + `IsReady` 加载中态范式,本子单 HUD 接入复用同范式 |

本子单交付:

| # | 交付物 | 落法 | 性质 |
| --- | --- | --- | --- |
| 1 | HUD 顶栏 3 资源条接 `PlayerAttrService` 三属性 | `BuildTopBar()` 三数字初值 + 订阅 + 刷新方法 | <span class="pill-core">核心档</span> |
| 2 | `IsReady=false` 加载中态 | 三数字显「—」直到 `ApplySnapshot` 触发 | <span class="pill-core">核心档</span> |
| 3 | `OnDestroy` 解绑 `OnAttrChanged` | 防 Window 销毁后事件持引用泄漏 | <span class="pill-core">核心档</span> |
| 4 | 资源条图标语义对齐(可选微调) | 第 1 = Coin → 金币图标、第 2 = Diamond → 钻石图标、第 3 = Stamina → 体力图标(按现有切图 `gemstone/gemstone2/potion` 择最贴语义者,见 §三) | <span class="pill-enh">增强档</span> |

**不做(本子单明确排除)**:<span class="pill-cut">改 PlayerAttrService 任何契约</span>(38 已建,本子单零修);<span class="pill-cut">让 HUD 加号变购买入口</span>(去变现,仍 Log 待建);<span class="pill-cut">在 HUD 实现属性变更接线点</span>(本子单是只读展示,变更入口归各业务玩法刀,如 38 已接的改名扣钻);<span class="pill-cut">动 MergeOrderWindow / 合成订单 HUD</span>(本次范围只 Classic `GameWindow`);<span class="pill-cut">把 HighScore 挤出顶栏</span>(已挪到分数下方 `Best`,本子单不动);<span class="pill-cut">改资源条节点结构 / 位置 / 尺寸</span>(布局沿 27 §5.3 既有);<span class="pill-cut">加 HUD 钻石不足提示 / 红点</span>(本子单只显数值,提示归各消费路径)。

## 二、与既有稿关系 {#related}

| 既有稿 | 关系 | 本子单是否触发同步重写 |
| --- | --- | --- |
| [27 · 主玩法 HUD 美术换皮](#27-tarot-mode-hud-art) | 27 §三 D1 + §5.3 + §6 + §10 D1 的「3 资源条 = 视觉占位 + 第 1 条 HighScore + 加号 stub」叙事已过时(本子单接入了真数据源) | **是**(同任务内重写 27 对应节为现状,沿 conventions §6 覆盖式重写) |
| [38 · 玩家属性客户端](#38-player-attr-client) | 38 §一(切分表)「显示属性 HUD」标「**后续刀**」、§7.7 诚实边界「本子单不守 HUD 全屏显示三属性」—— 本子单兑现此后续刀 | **是**(38 §一切分表对应行 + §7.7 接续记「HUD 全屏三属性已接续 = 设计 42」) |
| [37 · 玩家属性服务端](#37-player-attr-server) | 协议契约不动 | **不改** |
| [25 · 个人信息窗换皮](#25-player-info-window-art) / `PlayerInfoWindow` | 钻石余额行已接 `PlayerAttrService.Diamond`,本子单只是把同范式扩到 HUD | **不改**(25 / PlayerInfoWindow 行为不变) |
| [29 · 玩法融合](#29-gameplay-fusion) | 29 §5.4 经典最高分并入元层落盘,与本子单接入的 HUD 三属性正交 | **不改** |

## 三、HUD 顶栏 3 资源条数据映射 {#mapping}

接现状(`BuildTopBar` 三资源条索引 0/1/2,自左到右排列):

| 槽 | AttrType | 现状图标(27 §5.3) | 图标对位评估 | 本子单图标处置 |
| --- | --- | --- | --- | --- |
| 资源条 0 | `Coin`(金币) | `gemstone`(宝石) | 金币语义最近的可选项是 `gemstone`(发亮宝石)或保留;dev 读图择最贴金币者(可备选 `gemstone2`) | 默认沿用 `gemstone`,dev 落地可微调 |
| 资源条 1 | `Diamond`(钻石) | `gemstone2`(宝石 2) | 钻石语义最近的可选项是 `gemstone2`(切割面更明显);dev 读图择最贴钻石者 | 默认沿用 `gemstone2`,dev 落地可微调 |
| 资源条 2 | `Stamina`(体力) | `potion`(药水) | 药水 = 体力补给的常见语义关联,沿用 | 默认沿用 `potion` |

> [!NOTE]
> **为什么不强行换图标?**
>
> 27 §5.3 当前 3 图标(`gemstone/gemstone2/potion`)已是按「资源 / 资源 / 补给」三档语义铺的占位,与三属性 Coin/Diamond/Stamina 语义对位基本可行(gemstone 既可作金币也可作宝石,gemstone2 切割面更近钻石,potion 近体力)。若产品严格要求三属性专属图标(如真金币图、真钻石图、真闪电心图),需要新切图 + 重跑打表工具 = **超本子单纯接线范围**,列待裁决(§五 O1)给产品定;本子单按现有切图取默认对位,不阻塞接入。

**数字位绑定**(行为级,文案由 dev 据 UI 取舍):

| 槽 | `IsReady=false` 时显示 | `IsReady=true` 时显示 | 余额变动时刷新源 |
| --- | --- | --- | --- |
| 资源条 0 | `"—"`(占位破折号) | `Attr.Coin.ToString()` | `OnAttrChanged(All)` / `OnAttrChanged(Coin)` |
| 资源条 1 | `"—"` | `Attr.Diamond.ToString()` | `OnAttrChanged(All)` / `OnAttrChanged(Diamond)` |
| 资源条 2 | `"—"` | `Attr.Stamina.ToString()` | `OnAttrChanged(All)` / `OnAttrChanged(Stamina)` |

> [!NOTE]
> **为什么 `IsReady=false` 显「—」而非 0?**
>
> 沿 PlayerInfoWindow 范式(钻石面板 `IsReady=false` 显「加载中...」):0 是合法余额值(玩家可能真的零钻石零金币),用 0 当占位会让「未加载」与「真零」二态混淆;「—」明确表「数据未到,稍候」,玩家不被误导。主玩法 HUD 字号小、空间紧,用「—」比「加载中」更紧凑。HUD 加号按钮在 `IsReady=false` 时**不必 disabled**(本子单加号本就只 Log 待建、不触发副作用,沿 27 §5.3 去变现处置)。

## 四、接线行为(行为级) {#wiring}

`GameWindow` 是 code-built 窗,接线全在 `BuildTopBar()` + 既有 `OnCreate` / `OnDestroy` 生命周期内。

| 步 | 行为 | 时机 |
| --- | --- | --- |
| 1 | 在 `BuildTopBar()` 三资源条数字 `CreateText` 调用处,把初值从「`_initialHigh.ToString()` / `"0"`」改为「`IsReady ? Attr.<Type>.ToString() : "—"`」 | `OnCreate` 内 `BuildStaticUI` → `BuildTopBar` 当下 |
| 2 | 保存 3 个 `Text` 组件引用(`_resNumCoin / _resNumDiamond / _resNumStamina`,沿 `_scoreText / _bestText` 现有字段命名口径) | `BuildTopBar` 内 |
| 3 | `BuildTopBar` 末:订阅 `Attr.OnAttrChanged += OnAttrChangedDispatch`(`Attr != null` null-safe) | `OnCreate` 内(单次,与 `BuildStaticUI` 同生命周期) |
| 4 | `OnAttrChangedDispatch(type, _, _)`:switch on type(`All` → 三条全刷;`Coin` → 刷条 0;`Diamond` → 刷条 1;`Stamina` → 刷条 2;`default` → 忽略,与 PlayerInfoWindow 一致) | 事件回调(主线程,Fantasy Scene 沿 38 §7.4) |
| 5 | 新增 `OnDestroy()`(或框架对应「窗关闭/销毁」钩子,沿 UIWindow 现有范式;若没有则在 `OnCreate` 末挂、Window 销毁时 GC 解的话留 null-safe + 不强求 `OnDestroy`) | Window 销毁时 |

> [!NOTE]
> **dev 落地查证点:`UIWindow` 解绑时机的真实钩子名**
>
> PlayerInfoWindow 用 `OnCloseInternal` 等价钩子(grep 该文件第 113 行 `-=` 解绑落点)。`GameWindow` 当前没有 `OnDestroy` / `OnClose` 重写;dev 落地时:① 先 grep `UIWindow` 基类,确认它对应的「窗销毁/关闭」虚方法名(沿 TEngine 既有 lifecycle);② 在该钩子内 `Attr.OnAttrChanged -= OnAttrChangedDispatch`;③ 若实在没有合适钩子(基类生命周期不暴露),`GameWindow` 整体随 `CloseUI<GameWindow>` GC 时 `Attr` 持的 delegate 引用也会被打断(C# event 弱引用规则不自动断,但 `Attr` 是单例长存、`GameWindow` 是短命,**必须解绑**以防泄漏)。验收点 SV3 锚在「解绑代码存在 + grep `OnAttrChanged -=` 命中」上,不要求 EditMode 反射驱动销毁。

## 五、走查与崩法 {#walk}

把本子单的接入在「**快照未到 / 改名同时收到推送 / 窗销毁后事件持引用 / Classic 整局走查 / Service 为 null / 余额超 int**」六类下走一遍,各点出最可能炸的崩法 + 对策:

### 5.1 快照未到(`IsReady=false`)

| 崩法 | 后果 | 对策 |
| --- | --- | --- |
| 玩家从主菜单进 Classic 时 Snapshot 还在路上,HUD 三条全显「—」 | UI 体验:玩家短暂看「—」(几百 ms 至 ~5s) | 设计内显式接受:沿 PlayerInfoWindow「加载中...」口径,「—」是已知短窗,Snapshot 到达后 `OnAttrChanged(All)` 即刷新;dev 不强求加超时重试 / 主动 GetSnapshot,沿 38 §5.1 / 风险表 |
| 服务端永远不下发 Snapshot(协议 bug / 服务端崩) | HUD 三条永远「—」 | 沿 38 §7.1:本子单不强求重试机制,真往返验收锚在「Snapshot 必下发」(E1);Snapshot 丢 → 下次登录对齐,本子单不重复造重试链 |

### 5.2 推送实时刷新

| 崩法 | 后果 | 对策 |
| --- | --- | --- |
| Classic 玩到一半,服务端推送 `G2C_PropertyDeltaPush`(改名扣钻 / 邮件领奖 / 后台运营调整) | HUD 对应资源条数字应**实时刷新**(玩家在主玩法窗就能看见) | 订阅 `OnAttrChanged` + 按 type 刷对应 `Text`(`switch on type`,与 PlayerInfoWindow 一致);**这是本子单的核心交付**,验收锚 E2 |
| 推送同时改变 Coin + Diamond(批量) | 38 协议每条推送只一种 type,批量经多条推送陆续到 | 沿 38 §7.3:多条推送各自覆盖,顺序无关;HUD 按各 type 刷各 `Text`,无需聚合 |
| `OnAttrChanged` 在主线程外触发 | UI 异常 | 沿 38 §7.4:`PlayerAttrService` 纯逻辑类,`FantasyNetwork` 已是主线程 Scene,RPC 回调本在主线程触发事件;HUD 直接刷 `Text` 即可 |

### 5.3 窗销毁后事件持引用 / 解绑

| 崩法 | 后果 | 对策 |
| --- | --- | --- |
| `GameWindow` 被 `CloseUI<GameWindow>`(玩家按退出钮),但订阅未解 → `Attr`(单例长存)持 `GameWindow` 实例 delegate → GC 不掉 → Window 内 `Text` / `Image` 全持引用 → 内存泄漏 | 重开 Classic 多次后内存膨胀;再触发推送时已销毁 Window 的 `Text.text = …` 抛 `MissingReferenceException` | `OnAttrChanged -=` 在窗销毁/关闭钩子解(§四步 5);验收 SV3 grep 命中 |
| 重复进 Classic 多次,每次都 `+=`,事件多次回调致刷一次余额 N 次 | 性能浪费(无业务后果);若 dev 没在解绑钩子里解、改在 `BuildTopBar` 头 `-=` 来防重 | `OnCreate` 是 Window 一次性生命周期(每次开新 Window 实例),`+=` 一次即可;销毁时 `-=` 即可避免累积。dev 若担心可在 `+=` 前先 `-=`(防御性),但不必 |

### 5.4 Classic 整局走查(零回归)

把一局从主菜单进 Classic → 摆块 / 落子 / 消除 / 连击弹字 / 补块 / GameOver / 分数滚动 / 最高分 全程走一遍,核 HUD 接入是否影响这些路径:

| 玩法路径 | 本子单是否触碰 | 检验 |
| --- | --- | --- |
| `RenderBoard` / `RenderSlots` / `OnPieceBegin/Drag/End` / `PlaceAndResolve` / `UpdateGhost` / `OnUpdate` 分数滚动 / `UpdateBest` / `TriggerGameOver` | **不动** | 验收 R1:grep diff 仅 `BuildTopBar` + 新增 `OnAttrChangedDispatch` + `OnDestroy`(或等效),其它玩法逻辑行未改 |
| `BlockLayout` 坐标常量 | **不动** | 验收 R2 |
| `BlockGameState` 数据层 | **不动** | 验收 R3:HUD 接的是 `PlayerAttrService`(`GameContext.Instance.PlayerAttr`),不读不写 `BlockGameState`;`Score / HighScore / Combo` 显示路径不变(`_scoreText.text` / `_bestText.text` 在 `OnUpdate` / `UpdateBest` 内不动) |
| HighScore 显示位 | **不动**(已在 27 §5.1 挪到大分数下 `Best` 文本,本子单不挤入顶栏) | 验收 R4:`_bestText` 字段 + `UpdateBest` 方法不动 |
| 退出钮 / 齿轮 / 加号 / 动作按钮 | **不动** | 验收 R5:加号回调仍 `Log` 待建(去变现) |
| `MergeOrderWindow` / 合成订单玩法 | **不动**(本子单不碰) | 验收 R6:git diff 不含 `MergeOrderWindow.cs` |

### 5.5 Service 为 null(防御态)

| 崩法 | 后果 | 对策 |
| --- | --- | --- |
| `GameContext.Instance.PlayerAttr == null`(理论上 38 已挂入,但若 EditMode 测 / 反射跳过 OnInit 等异常路径) | `Attr.OnAttrChanged += …` 抛 NRE | `BuildTopBar` 内 `if (Attr != null) Attr.OnAttrChanged += …`(沿 PlayerInfoWindow 范式,`Attr` getter null-safe);HUD 三条则按 `IsReady=false` 路径显「—」永驻;不阻塞玩法 |

### 5.6 余额数值范围

| 崩法 | 后果 | 对策 |
| --- | --- | --- |
| `PlayerAttrService.Coin / Diamond / Stamina` 是 `long`,UI 用 `ToString()` 可能显示 19 位字符撑爆资源条 | 视觉撑出条底 | 设计上服务端有上界(沿 37 校验体系),实际玩家余额远不到 long 上限;UI 不为此加截断 / K/M 缩写(沿 PlayerInfoWindow 钻石直显 `Attr.Diamond` 范式);若产品后续要 K/M 缩写,另开 UI 抛光刀 |

### 5.7 诚实边界(本子单守 / 不守)

**守住**:
- HUD 顶栏 3 资源条显示三属性实时余额(快照初始化 + 推送刷新)
- `IsReady=false` 时显「—」不误导
- 解绑事件防泄漏
- Classic 整局玩法零回归
- 加号点击行为不变(`Log` 待建,去变现)
- 38 数据层零修(沿 `PlayerAttrService` / `GameContext.PlayerAttr` 既有契约)

**不守**:
- **HUD 钻石不足红点 / 加号变购买入口**(去变现 + 本子单只读展示)
- **属性变更入口在 HUD**(变更归各业务玩法刀;HUD 是显示侧)
- **HUD 三属性图标专属化**(用现有切图 `gemstone/gemstone2/potion` 默认对位,真专属图标需新切图,O1 待裁决)
- **`MergeOrderWindow` 合成订单 HUD 接入**(范围外,合成订单当前体力是局内态,与服务端属性账本是两套口径;若后续要让合成订单体力也接服务端 Stamina,需先把局内态升级为「跨会话余额 + 局内消费」二层模型,沿 38 §一切分表 NOTE,**Tier 2+ 业务玩法刀**)
- **HUD K/M 数值缩写**(int 直显,产品后续要抛光另开)
- **HUD 三属性的变更动画 / 滚动 / 弹字**(直接 `Text.text=` 覆盖,沿 PlayerInfoWindow 范式;若要动画另开 UI 抛光刀)

## 六、dev 改动清单(行为级,代码层 dev 现场定位) {#hook}

> 行为级清单。代码符号 / 文件 / 行号由 dev 实现时 grep 工程现状取(沿 plan 红线「设计稿不指代码符号」)。

| # | 行为 | 落点(行为定位) |
| --- | --- | --- |
| 1 | 把 HUD 顶栏 3 资源条数字初值改为「`IsReady ? Attr.<Type>.ToString() : "—"`」 | `GameWindow.BuildTopBar()` 内三 `CreateText` 调用处 |
| 2 | 保存 3 `Text` 组件引用 + 加 `RefreshResources()` 方法(switch on type 刷对应 Text 或全刷) | `GameWindow` 内 |
| 3 | `BuildTopBar` 末注册 `Attr.OnAttrChanged += OnAttrChangedDispatch`(null-safe) | `GameWindow.BuildTopBar()` 末 |
| 4 | 实现 `OnAttrChangedDispatch(AttrType type, long _, string __)`:All → 三条全刷;Coin/Diamond/Stamina → 刷对应条 | `GameWindow` 内 |
| 5 | 在窗销毁/关闭钩子(沿 PlayerInfoWindow 第 113 行解绑落点的等价 UIWindow 钩子)解 `OnAttrChanged -=` | `GameWindow` 销毁/关闭钩子内 |
| 6 | 加号点击行为不变(仍 `Log` 待建,本子单零修) | 不动 |
| — | `PlayerAttrService` / `IRpcGateway` / `RpcGatewayProd` / `AttrType` / `GameContext` | **不改**(38 数据层) |
| — | `BlockLayout` / `BlockGameState` / `MergeOrderWindow` / 切图 / 精灵表 / 收集器 | **不改** |
| — | Classic 玩法逻辑(`Render*` / 拖拽 / 落子 / ghost / GameOver / `OnUpdate` / `UpdateBest`) | **不改** |
| — | HighScore 显示位(`_bestText`,已在大分数下) | **不改** |

## 七、验收点 {#accept}

拆三档:**回归(硬,零回归)** / **接线(CV,EditMode 纯逻辑)** / **视觉对位 / 真往返(Play / 手验)**。

### 7.1 回归(硬,EditMode + 静态核对) {#regress}

| 组 | # | 验收点(完成定义) |
| --- | --- | --- |
| 编译 C | C1 | `GameWindow.cs` 编译 0 error;现有 EditMode 全绿(零回归);`PlayerInfoWindow` 钻石余额行行为不变 |
| 零回归 R(硬) | R1 | **玩法逻辑行未改**:静态核对 `RenderBoard` / `RenderSlots` / `OnPieceBegin/Drag/End` / `PlaceAndResolve` / `UpdateGhost` / `InitGhostPool` / `ComputeGridPos` / `TriggerGameOver` / `UpdateBest` / `OnUpdate` 与本子单前逐行一致(diff 仅 `BuildTopBar` 内三数字初值 + 新增订阅 + 新增 `OnAttrChangedDispatch` + 销毁钩子解绑) |
| 零回归 R(硬) | R2 | **`BlockLayout` 坐标常量未改**(沿 27 §5.2 / R2) |
| 零回归 R(硬) | R3 | **`BlockGameState` 数据层未改**:HUD 资源条改读 `PlayerAttrService`,**不**读写 `BlockGameState`;`Score / HighScore / Combo` 路径不变;`_scoreText` / `_bestText` / `_displayedScore` / `_initialHigh` 字段与回调不动 |
| 零回归 R(硬) | R4 | **HighScore 显示位未挤掉**:`_bestText` 字段与 `UpdateBest` 方法不动;Classic 整局 GameOver 后最高分仍正常显示 |
| 零回归 R(硬) | R5 | **退出 / 齿轮 / 加号 / 动作按钮回调未改**:退出 `CloseUI<GameWindow>` + `ShowUIAsync<MainMenuWindow>`;齿轮 `ShowUIAsync<SettingsWindow>`;加号 `Log` 待建;动作按钮 `Log` 待建 |
| 零回归 R(硬) | R6 | **`MergeOrderWindow` 未碰**:git diff 不含 `MergeOrderWindow.cs`;合成订单 Demo 仍可进可玩(本子单不动其 HUD) |
| 零回归 R(硬) | R7 | **38 数据层未改**:git diff 不含 `PlayerAttrService.cs` / `IRpcGateway.cs` / `RpcGatewayProd.cs` / `AttrType.cs` / `GameContext.cs`(若 `GameContext` 须改则违范围 —— 本子单只接 UI 侧,不动数据层) |

### 7.2 接线(EditMode,CV)

`PlayerAttrService` 是纯逻辑类(38 已建,可 EditMode 测);HUD 内事件分发逻辑也是纯逻辑。

| # | 验收点 | 完成定义 |
| --- | --- | --- |
| CV1 | `PlayerAttrService.ApplySnapshot(100, 200, 5)` 触发 `OnAttrChanged(All)` | 已在 38 CV1 验过,本子单不重复验,只验 HUD 订阅这里能收到(由 W 组验) |

### 7.3 HUD 订阅刷新(EditMode 反射可达部分,W)

| # | 验收点 | 完成定义 |
| --- | --- | --- |
| W1 | HUD 订阅 `OnAttrChanged` 注册 | grep `GameWindow.cs`:`Attr.OnAttrChanged += OnAttrChangedDispatch`(或同语义)命中一次 |
| W2 | HUD 解绑 `OnAttrChanged` | grep `GameWindow.cs`:`Attr.OnAttrChanged -= …` 命中一次,在窗销毁/关闭钩子内 |
| W3 | `OnAttrChangedDispatch` 分发行为 | 灌入 `OnAttrChanged(All, _, _)` → 三条 `Text.text` 全刷(用反射读 `_resNumCoin / _resNumDiamond / _resNumStamina` 的 text);灌入 `OnAttrChanged(Coin, _, _)` → 只刷条 0;Diamond → 只刷条 1;Stamina → 只刷条 2(若 EditMode 反射驱动 `BuildStaticUI` 成本高,本组可降级为静态核对 + Play 手验) |
| W4 | `IsReady=false` 初值 | 进 `GameWindow` 时 `PlayerAttrService` 未 `ApplySnapshot`,三资源条 `Text.text = "—"`(静态核对 + Play 启动期截图) |

### 7.4 视觉对位 / 真往返(Play / 手验,V + E)

| # | 验收点 | 完成定义 |
| --- | --- | --- |
| V1 | **HUD 三资源条显三属性初始值**(快照到达后) | Unity 客户端 Play 起,自动登录成功 + `ApplySnapshot` 到 → 进 Classic → HUD 三条显「Coin = X / Diamond = Y / Stamina = Z」(= 服务端权威值,与 `GameContext.Instance.PlayerAttr.Coin/Diamond/Stamina` 一致) |
| V2 | **HUD 三资源条订阅推送实时刷新** | Classic 在跑 → 服务端用工具或玩家在另一处触发改名扣钻 / 邮件领奖 → `G2C_PropertyDeltaPush` → HUD 对应资源条数字**实时刷新**(几十 ms 内,无须重开窗) |
| V3 | **`IsReady=false` 显「—」**(快照未到态) | 模拟登录前进 Classic(或断 Fantasy 服 / 不下发 Snapshot)→ HUD 三条显「—」;Snapshot 到 → 立刻刷为真实值(`OnAttrChanged(All)` 触发) |
| V4 | **Classic 整局零回归**(玩家手验) | 从主菜单进 Classic → 摆块 / 落子 / 消除 / 连击弹字 / 补块 / GameOver / 分数滚动 / 最高分全部正常;HUD 三属性显示稳定不闪烁 / 不挡棋盘 / 不挡候选块 / 不挡落子区 |
| V5 | **HighScore 仍在分数下方显示**(`Best` 文本) | Classic 整局 GameOver 后看到 `BEST <最高分>` 正常显示在大分数下方 |
| V6 | **窗销毁不泄漏 + 不抛异常** | 进 Classic → 退出 → 进 Classic → 退出 → 反复 N 次,日志无 `MissingReferenceException`;手动触发推送在退出后到达 → 无异常(因 `OnAttrChanged` 已解) |
| E1 | **真往返刷新**(依赖本机 MongoDB + Fantasy 服务端,沿 38 E1/E2 口径) | 起服 → Play → 进 Classic → HUD 三条显服务端值 → 经服务端 API `ChangeProperty(UUID, Diamond, +500, "test_grant")` → 客户端立刻收推送 → HUD Diamond 条刷新为 +500 后的新值 |

### 7.5 BLOCKED 边界 + 不在本子单验收

- **本机 MongoDB 不可达 / Fantasy 服务端起不来** → V1/V2/V3/E1 判 BLOCKED 非 FAIL(沿 38 / 37 口径)
- **HUD 三属性专属图标** → O1 待裁决,默认沿用现有切图对位
- **合成订单 HUD 三属性接入** → Tier 2+ 业务玩法刀(需先建跨会话体力余额二层模型)
- **K/M 数值缩写 / 滚动动画 / 红点提示** → UI 抛光刀
- **HUD 加号变购买入口** → 去变现红线,永远不做(若产品方向变,另开新设计)

### 7.6 Code Review(SV)

| # | 验收点 | 核什么 |
| --- | --- | --- |
| SV1 | 编译 + 范围 | 编译 0 error;git diff 仅 `GameWindow.cs`(无新文件 / 无新数据层 / 无协议变更) |
| SV2 | 38 数据层零修 | grep diff:`PlayerAttrService.cs` / `IRpcGateway.cs` / `RpcGatewayProd.cs` / `AttrType.cs` / `GameContext.cs` 全部不在 diff |
| SV3 | 解绑 OnAttrChanged | grep `GameWindow.cs`:`OnAttrChanged -=` 命中一次,在窗销毁/关闭钩子内(防泄漏 §5.3) |
| SV4 | null-safe 接入 | grep `GameWindow.cs`:订阅 / 刷新方法对 `Attr` 取值有 null 防御(`if (Attr != null) …`,沿 PlayerInfoWindow 范式) |
| SV5 | 加号行为不变 | grep `GameWindow.cs`:加号 onClick 仍 `Log.Info(…"待建"…)`,无购买入口接入(去变现) |
| SV6 | 设计 27 / 38 同步重写已落地 | grep 27 §三 / §5.3 / §6 / §10 D1 + 38 §一切分表 / §7.7:全部已重写为「HUD 三属性已接绑 / 已接续 = 设计 42」(沿 conventions §6) |

## 八、待拍板清单 {#open}

| # | 开关 | 本子单默认 | 备选 / 触发改动 |
| --- | --- | --- | --- |
| **O1** | HUD 三属性专属图标(`gemstone/gemstone2/potion` 当前是占位) | **沿用现有切图对位**(Coin → `gemstone`、Diamond → `gemstone2`、Stamina → `potion`,§三),dev 落地按效果可微调 | 产品要专属图标(真金币 / 真钻石 / 真闪电心)→ 美术出新切图 + 重跑打表工具 + dev 替图,超本子单纯接线范围,另开 UI 抛光刀 |
| O2 | `IsReady=false` 占位文本 | **「—」**(紧凑,沿 PlayerInfoWindow 加载中态意图) | 也可「...」/ 空白;dev 视觉为准 |
| O3 | `OnAttrChangedDispatch` 实现样式 | **switch on type 刷对应条 + All 刷全部** | 全条无脑刷三条(代码更短,推送频率低无性能问题),但与 PlayerInfoWindow `OnAttrChangedDispatch` 范式略偏;dev 取舍 |
| O4 | HUD 三条顺序(从左到右是 Coin / Diamond / Stamina,还是按效果图 / 27 §5.3 既有图标顺序) | **Coin / Diamond / Stamina 自左到右**(协议枚举顺序,§三映射) | 若产品定要钻石居中(主资源)等其它顺序 → 调换映射;沿当前更直观 |

> [!NOTE]
> **自治分流**
>
> O1–O4 均有安全默认 / 可逆 / 不抵触 GDD 去变现 / 离线还原方向 → 按 plan 红线取默认推进,记 decisions,**不入 blockers**。其中 **O1**(专属图标)最值得产品复核 —— 当前用宝石/宝石/药水占位三属性,语义可行但不严格专属;不阻塞本子单接入。

## 九、风险表 {#risk}

| 风险 | 应对 |
| --- | --- |
| **主玩法窗在跑,接入越界改了玩法逻辑** | §四步 1-5 严格只动 `BuildTopBar` 三数字初值 + 1 处订阅 + 1 处解绑 + 1 个分发方法;R1-R7 硬验收逐条静态核对;git diff 仅 `GameWindow.cs` |
| **窗销毁后事件未解致泄漏 / 抛 MissingReferenceException** | SV3 grep 验解绑;V6 反复进退验无异常;§5.3 走查论证 |
| **`IsReady=false` 显 0 误导玩家** | §三表 / V3 验显「—」非 0;沿 PlayerInfoWindow 加载中态范式 |
| **HUD 实时刷新对帧率有影响** | 推送频率低(玩家行为驱动,非每帧),`Text.text =` 是 UGUI 标准操作开销可忽略 |
| **图标语义对位与产品预期不一致** | O1 记 decisions 交产品;本子单按现有切图取默认对位不阻塞;后续抛光刀替图 |
| **`MergeOrderWindow` 合成订单 HUD 体力被误认为「也要接 Stamina」** | §7.7 诚实边界 + §一不做表显式声明:合成订单体力是局内态非元层余额,本子单范围外 |
| **同步重写 27 / 38 漏改** | SV6 grep 验;本任务交接区列改写清单,test 必核 |
| **加号被产品误判为「漏接购买」** | §7.7 / SV5 显式声明加号去变现红线,Log 待建是设计,不是缺漏 |

## 关联文档

- [27 · 主玩法 HUD 美术换皮(本子单触发 §三 D1 / §5.3 / §6 / §10 D1 同步重写)](#27-tarot-mode-hud-art)
- [38 · 玩家属性客户端(数据层,本子单是其 HUD 表现层接续刀)](#38-player-attr-client)
- [37 · 玩家属性服务端(协议契约源头,本子单零改)](#37-player-attr-server)
- [25 · 个人信息窗美术换皮 / PlayerInfoWindow(钻石面板订阅范式,本子单 HUD 复用)](#25-player-info-window-art)
