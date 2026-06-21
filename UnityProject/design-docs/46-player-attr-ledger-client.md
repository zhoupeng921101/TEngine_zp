<style>
  pre.code { margin:8px 0; padding:12px; background:#0d1622; border:1px solid var(--border); border-radius:8px; overflow:auto; font-size:13px; line-height:1.55; }
  table.tight td, table.tight th { padding:6px 10px; }
  .pill-srv { display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(255,176,90,.18); color:#ffb05a; margin-left:6px; }
  .pill-cli { display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(108,140,255,.16); color:#6c8cff; margin-left:6px; }
  .pill-core{ display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(91,214,160,.16); color:#5bd6a0; margin-left:6px; }
  .pill-enh { display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(108,140,255,.16); color:#6c8cff; margin-left:6px; }
  .pill-cut { display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(255,122,138,.16); color:#ff7a8a; margin-left:6px; }
  .yes { color:#5bd6a0; font-weight:bold; }
  .no  { color:#ff7a8a; font-weight:bold; }
</style>

# 我的流水 UI + RemoteAttrLedgerService(Tier 2 第 4 子单 · 客户端段)

> 本子单兑现 [45 §六 Tier 2+ 接口余量](#45-player-attr-ledger-query::tier2plus) 的「客户端业务接入 + 我的流水 UI」一刀:在 45 已交付的协议契约 + 服务端 handler + 客户端协议生成物之上,新建 `RemoteAttrLedgerService` 远程数据源(沿 [32 RemoteMailService](#32-mail-server) 远程编排范式)+ 新建独立的「我的流水」UI 窗口,展示个人三属性变动流水(时间 + 属性图标 + delta + source 中文 + 余额变化),支持单 kind 过滤 + 滑动窗口加载更多。

<div class="callout warn" id="must-read">

**读前必看 · 六条边界**

- **本子单是 ledger 查询客户端段表现刀**,服务端段在 45 已交付。协议契约 / handler 字段语义全部沿 [45 §三](#45-player-attr-ledger-query::protocol),客户端**只消费**已有协议;不动 [45 协议字段集](#45-player-attr-ledger-query::protocol)、不动 [44 ledger 写入挂钩](#44-player-attr-ledger::time)、不动 [37 通用变更入口](#37-player-attr-server::internal-api)、不动 [38 `PlayerAttrService`](#38-player-attr-client::service) 核心契约。
- **新建独立窗口而非挂 PlayerInfoWindow Tab。** [PlayerInfoWindow](#25-player-info-window-art) 是模态弹窗(头像 + 改名 + 生日,信息密度低),挂 tab 会把模态弹窗变成多视图复合窗、与 25 设计稿表现层「单一职责」相悖;独立窗 `PlayerAttrLedgerWindow` 沿 [28 排行榜窗](#28-rank-window-art) 同范式(全屏 / 半屏 + 列表 + 过滤栏 + 关闭),入口暂挂 [PlayerInfoWindow](#25-player-info-window-art) 改名面板下方一个新按钮「我的流水」(§4.2 入口决策)。
- **数据源切换:`IAttrLedgerSource` 接缝 + `RemoteAttrLedgerSource`(生产)+ 单测桩**。沿 [32 `IRemoteMailSource`](#32-mail-server) + [38 `IRpcGateway`](#38-player-attr-client::service) 同范式:`RemoteAttrLedgerService` 持 `IAttrLedgerSource` 接缝,生产实现 `RemoteAttrLedgerSource` 经 `FantasyNetwork.Session` 发 `C2G_QueryAttrLedger`,EditMode 单测注入桩源验列表组装 / 过滤展示 / 文案映射 / 错误降级各分支。
- **离线 / 单机模式:`ServiceUnavailable` → 空列表 + 文案「服务暂不可用,稍后再试」**。沿 [32 邮件领奖](#32-mail-server) / [45 §3.3](#45-player-attr-ledger-query::error) ServiceUnavailable 同口径——审计完整性是服务端独占,客户端**不本地缓存 ledger**(无本地副本,与 22 排行榜「断服回退本地源」语义不同;若本地放 ledger 副本会让玩家有「看似有 ledger 实则伪本地态」错觉)。
- **去变现:不含付费 / 充值流水。** 本子单流水仅展示 [44 §3.3 已登记 source 枚举](#44-player-attr-ledger::source) 的 5 个已接路径(`ChangeNameSpend / MailClaim / RedeemCode / RankSettleReward / ActivityReward`)+ 4 个增强档预留(`GameplayConsume / ShopPurchase / AdminGrant / Refund`,Tier 2+ 接入时文案补)+ `Unknown` 兜底;source 文案**硬编码 switch**(去变现下 i18n 系统暂无,[O5](#46-player-attr-ledger-client::open))。
- **本机 MongoDB(`D:\mongodb-portable`,127.0.0.1:27017)真往返必备。** 真往返查 ledger 类 PV(PV3-PV5 / PV9 / E1)在不可达时判 BLOCKED 非 FAIL(沿 [44 §7.2](#44-player-attr-ledger::bloked-list) + [45 §7.4](#45-player-attr-ledger-query::blocked-list))。EditMode 单测 / 编译 / 零回归(PV1 / PV2 / PV10 / CV1)照常验。

</div>

<div class="callout note" id="intro">

**立项信息**

| 项 | 内容 |
| --- | --- |
| **类型** | 全栈特性 · Tier 2 玩家属性权威体系第 4 子单 · **客户端段(收口)**。承接 [45 协议契约 + handler + 协议生成物](#45-player-attr-ledger-query) 已 PASS 地基;本子单在客户端建数据源接缝 + 远程实现 + 我的流水 UI 投放 + 入口接钮,**收口** Tier 2 ledger 通路客户端段。出 code-free 设计意图 + 行为级数据源契约 + UI 投放规范 + 验收点,交客户端段(dev)落地,服务端零改。 |
| **方向约束** | 离线还原 · **去变现**:我的流水是**只读余额变动展示**,无内购 / 充值流水;source 文案硬编码不引入 i18n 系统(去变现下 i18n 暂无,Tier 2+ 真做多语言再迁);UI 沿 [28 排行榜窗](#28-rank-window-art) 同范式占位为先(塔罗 20 屏效果图无「我的流水」屏,本子单不阻塞美术);加法式:新增独立服务 + 独立窗,既有 38 `PlayerAttrService` / 25 PlayerInfoWindow 改名面板 / 27 主玩法 HUD / 42 HUD 三属性绑定零行为变化。 |
| **需求降层** | **a. 表层要求**(boss brief):新建 `RemoteAttrLedgerService` 沿 RemoteMailService 范式发 `C2G_QueryAttrLedger` 拉流水,解析 `source` 整数枚举 → 人类可读中文文本(典型 0=未知 / 1=改名扣钻 / 2=邮件领奖 / 3=兑换码 / 4=排行榜结算 / 5=活动奖励 / 6=玩法消费 / 7=商店购买 / 8=管理员发放 / 9=退款),UI「我的流水」窗口或挂 PlayerInfoWindow tab,显示流水列表(时间 + 属性图标 + delta + source 文本 + 余额变化),滑动窗口加载,过滤 kind(可选,全 vs 单属性 tab 切换),复用 HUD 三属性图标占位;顺手清 Fantasy `PlayerAttrLedgerDoc.Kind` doc comment carry-forward。 **b. 底层目的**:① **让玩家看见自己的钻石 / 金币 / 体力从哪里来到哪里去**——38 让玩家可看到余额、42 让 HUD 顶栏实时刷新,但**不展示历史变动**,玩家若想知「我这 50 钻怎么没的」无处可查,本子单给出可观测窗口;② **客服自助化兜底**——客服查账靠运营 mongo shell 直读(44 § 5.4 不开 GM),玩家自助查可分流低优先级客服工单(「我的金币少了」类玩家先自己看一眼);③ **反作弊间接信号**——玩家若看到不合理变动(自己没做却扣了)会上报,运营可借此发现服务端 / 客户端 bug 或攻击;④ **客户端段闭环 Tier 2 表现层**——38(数据层)+ 42(HUD 接入)+ 25 PlayerInfoWindow 钻石余额(改名面板)是「**当前余额**视图」,本子单是「**历史变动**视图」,二者合起来表现层 Tier 2 收口。 **c. 有无更直达 b 的做法**:b 的本质 = 「玩家能查自己历史变动 + 数据走服务端真值 + 离线安全降级」。**直达做法 = 独立窗口 + RemoteAttrLedgerService 经 45 协议拉真数据 + 离线 ServiceUnavailable 提示**(沿 32 / 38 / 22 已建客户端段范式)。**备选(已否)**:(1) **挂 PlayerInfoWindow Tab**——25 PlayerInfoWindow 是模态弹窗(头像 + 改名 + 生日,半屏紧凑设计),tab 化破模态语义 + 与 25 表现层「头像 + 改名」单一职责相悖(§4.1 入口决策);(2) **HUD 长按弹窗**——交互隐蔽(玩家不会主动尝试长按 HUD),发现性差,违 b ①「玩家能查」(发现成本高 = 等于查不到);(3) **不接客户端段,只服务端 + 协议**——38 / 42 当前余额表现层已闭环,本子单是「**历史变动**视图」缺一不可,不接 = Tier 2 表现层断在「玩家不知 source」(b ① 失);(4) **协议 source 字段升级为字符串文本**——服务端 `AttrChangeSource` 是整数枚举(44 §3.3),协议字段已定为整数(45 §3.2),改字符串 = 改 45 已 PASS 的协议契约(违 boss 硬约束),且字符串文案在客户端做 i18n 更灵活(典型场景:多语言 + 子分类拆字符串)。无更优解,按表层「独立窗 + RemoteAttrLedgerService + 硬编码 source 文案 switch」实现。 |
| **范围(产品 · 玩法)** | **客户端新增**:① 数据源接缝 `IAttrLedgerSource`(纯接口,沿 32 `IRemoteMailSource` 范式)+ 生产实现 `RemoteAttrLedgerSource`(经 `FantasyNetwork.Session` 发 `C2G_QueryAttrLedger` + 收 `G2C_QueryAttrLedgerResponse`,反序列化为客户端 POCO `AttrLedgerEntry`);② 客户端服务 `RemoteAttrLedgerService`(沿 32 `RemoteMailService` 范式,持 `IAttrLedgerSource` 接缝,提供 `FetchPageAsync(kind?, sinceTs?, limit)` 返结构化结果);③ POCO `AttrLedgerEntry`(7 字段映射 45 §3.2 entry:Timestamp / Kind / BalanceBefore / BalanceAfter / Delta / Source / ReasonRaw)+ `AttrLedgerPage` 结果包(Code + Entries[] + HasMore);④ 结果码枚举 `AttrLedgerQueryCode`(Success / InvalidRequest / ServiceUnavailable / NetworkDown,沿 45 §3.3 + 38 ChangeReject 范式补 NetworkDown);⑤ source 整数 → 中文文案映射(硬编码 switch,10 档覆盖 44 §3.3 已登记);⑥ 独立 UI 窗口 `PlayerAttrLedgerWindow`(全屏 / 半屏据 dev 取,沿 28 排行榜窗范式,含过滤栏 + 列表 + 加载更多按钮 + 关闭按钮);⑦ 入口按钮:[PlayerInfoWindow](#25-player-info-window-art) 改名面板下方加「我的流水」按钮(§4.2 入口决策)。 **客户端不动**:38 `PlayerAttrService` 核心契约(可加旁路调 `RemoteAttrLedgerService` 拿快照,不动写入路径);42 HUD 三属性绑定;25 PlayerInfoWindow 改名面板钻石余额行;27 主玩法 HUD;Classic 玩法逻辑。 **协议**:**零新增**(沿用 45 已交付两条协议生成物)。 **服务端零改**(本子单纯 client 段)。 |
| **关键约束** | ① 客户端**不持** ledger 本地副本(无审计副本,服务端独占),每次打开窗口实时拉数据;② 离线 / 服务不可达**不本地放行 ledger**(返空列表 + 文案「服务暂不可用」,不本地造假数据);③ source 文案**硬编码 switch**(去变现下 i18n 暂无,O5);④ 属性图标**复用 HUD 占位**(沿 [42 §三](#42-tarot-hud-player-attr-bind::mapping) `gemstone / gemstone2 / potion` 三档,不新增切图);⑤ 滑动窗口加载用 `sinceTs` 上界过滤是「**取更新**」语义,翻旧页需协议加 `untilTs`(45 §4.2 + O3,本子单不做);**改方案**:本子单首屏拿前 N 条(`sinceTs=0, limit=N`),「加载更多」按当前列表**最旧一行**的 `Timestamp` 为 `sinceTs`——这**与 45 协议语义相反**,故本子单实际「加载更多」需服务端段在 Tier 2+ 加 `untilTs` 字段才能做翻旧页;**本子单先单页拿 50 条**(O2 默认 50),加载更多按钮在「`hasMore=true` 时」**置灰 + 提示「翻旧页 Tier 2+」**(O3 + 风险表 §九);⑥ 时间显示**本地时区**(`DateTime.UtcNow.ToLocalTime()` 标准转换)+ **相对时间**(`3 分钟前 / 2 小时前 / 昨天 / 2026-06-21 14:23`,沿 32 邮件列表常见范式);⑦ 网络层异常 catch 转 `NetworkDown`(沿 38 `TryChangeAsync` 处理范式),不抛异常致 UI 崩;⑧ Window 销毁解订阅(沿 [38 `PlayerInfoWindow.OnCloseInternal`](#38-player-attr-client) 范式)。 |

</div>

## 一、前后端职责切分(本子单视角) {#split}

| 环节 | 归属 | 本子单交付 |
| --- | --- | --- |
| 持流水账本(权威) | **服务端 · 44 已建** | 不动 |
| 协议 `C2G_QueryAttrLedger` / `G2C_QueryAttrLedgerResponse` | **服务端 · 45 已交付** | 不动(消费) |
| Handler:身份取 + 索引读 + kind 过滤 + sinceTs 滑动窗口 + limit 钳制 + 字段裁剪 | **服务端 · 45 已交付** | 不动(消费) |
| 协议生成物两端共识 | **45 已交付** | 不动(消费) |
| 数据源接缝 `IAttrLedgerSource` | **客户端 · 本子单新增** | <span class="yes">✓ 交付</span> |
| 远程实现 `RemoteAttrLedgerSource`(经 `FantasyNetwork.Session` 发协议) | **客户端 · 本子单新增** | <span class="yes">✓ 交付</span> |
| `RemoteAttrLedgerService` 编排层(`FetchPageAsync` + 错误码归一) | **客户端 · 本子单新增** | <span class="yes">✓ 交付</span> |
| POCO `AttrLedgerEntry` + `AttrLedgerPage` + `AttrLedgerQueryCode` | **客户端 · 本子单新增** | <span class="yes">✓ 交付</span> |
| source 整数 → 中文文案 switch | **客户端 · 本子单新增** | <span class="yes">✓ 交付</span> |
| 时间显示(本地时区 + 相对时间) | **客户端 · 本子单新增** | <span class="yes">✓ 交付</span> |
| 属性图标(复用 HUD 占位) | **客户端 · 42 / 27 已有** | 不动(复用 `gemstone / gemstone2 / potion`) |
| 我的流水窗 `PlayerAttrLedgerWindow`(列表 + 过滤栏 + 加载更多按钮) | **客户端 · 本子单新增** | <span class="yes">✓ 交付</span> |
| 入口按钮(PlayerInfoWindow 改名面板下加「我的流水」按钮) | **客户端 · 本子单新增** | <span class="yes">✓ 交付</span> |
| 翻旧页(`untilTs` 协议字段) | **不做 · Tier 2+** | 留 45 §6.2 / O3 |
| i18n 多语言文案 | **不做 · Tier 2+** | 硬编码 switch,O5 |

## 二、与既有稿关系 {#related}

| 既有稿 | 关系 | 本子单是否触发同步重写 |
| --- | --- | --- |
| [45 ledger 查询协议(Tier 2 第 3 子单 · server 段 + 协议生成物)](#45-player-attr-ledger-query) | **本子单的协议地基**:消费 45 已交付的 `C2G_QueryAttrLedger` + `G2C_QueryAttrLedgerResponse` + handler + 客户端协议生成物,行为按 45 §3 契约 | **同任务内同步**(§六同步重写):① 45 §6.2 接口余量表「客户端业务接入(RemoteAttrLedgerService)」+「客户端我的流水 UI 投放」+「source 整数 → 文本映射」三行覆盖式重写为「**46 已交付**」;② 45 §一切分表「数据源 RemoteAttrLedgerService」+「我的流水 UI 投放」+「source 整数 → 文本映射」三行同步;③ 45 §5.4「不守」对应三行同步;④ 45 §7.4 BLOCKED 列表「客户端业务接入 / UI 投放 / source 文案映射」三行同步 |
| [44 ledger 审计日志(Tier 2 第 2 子单 · server)](#44-player-attr-ledger) | **写入侧的对侧**:本子单读 44 已建的 `player_attr_ledger` 集合(经 45 协议中转);44 §3.3 已登记 source 枚举 10 档是 source 文案映射 switch 的依据 | **同任务内同步**:① 44 §一切分表「客户端业务接入(RemoteAttrLedgerService)+ 我的流水 UI」行覆盖式重写为「**46 已交付**」;② 44 §6.2 接口余量表「客户端我的流水 UI / 拉流水 RPC」行同步 |
| [38 玩家属性客户端(Tier 2 第 2 子单)](#38-player-attr-client) | **正交**:38 是属性变更的写入业务,本子单是属性变更的查询业务;`PlayerAttrService` 核心契约不动,但本子单**可旁路调** `RemoteAttrLedgerService.FetchPageAsync` 拿快照(38 `PlayerAttrService` 自身不引依赖) | **不改 38**(其 §一切分表已说明「Tier 2+ 业务玩法刀」,本子单是其中一刀,描述精度足够) |
| [42 tarot HUD 三属性绑定(Tier 2 第 3 子单 · client HUD)](#42-tarot-hud-player-attr-bind) | **正交**:42 是 HUD 顶栏当前余额,本子单是独立窗历史变动;**复用** 42 §三 已确定的属性图标映射(Coin → gemstone / Diamond → gemstone2 / Stamina → potion) | **不改 42** |
| [25 个人信息窗美术换皮](#25-player-info-window-art) | **入口宿主**:本子单在 PlayerInfoWindow 改名面板下方加「我的流水」按钮(沿 25 §三现有节点树扩一个 Button),不破改名面板布局 | **同任务内同步**:25 §三节点树 / §六交付列表 加「我的流水按钮」行(本子单新增) |
| [37 玩家属性服务端(Tier 2 第 1 子单)](#37-player-attr-server) | **正交**:37 是变更入口,本子单是查询入口;37 PropertyType 枚举(`Coin=0 / Diamond=1 / Stamina=2`,见 Fantasy `PropertyType.cs`)是 source 文案映射的属性图标定位依据 | **不改 37** |
| [32 邮件服务端化](#32-mail-server) | **范式参照**:本子单 `RemoteAttrLedgerService` 沿 `RemoteMailService` 编排层范式(`IAttrLedgerSource` 接缝 + 生产实现 + 失败兜底 + 错误码归一) | **不改 32** |
| [22 排行榜底层系统](#22-rank-system) | **范式参照**:本子单远程数据源切换沿 22 RankService 范式(`IRankSource` 接缝 + 远程实现 + 单测桩) | **不改 22** |
| [28 排行榜窗美术换皮](#28-rank-window-art) | **UI 范式参照**:本子单 `PlayerAttrLedgerWindow` 列表 + 过滤栏 + 关闭按钮沿 28 排行榜窗同范式占位为先 | **不改 28** |

> [!NOTE]
> **38 注释/设计稿与代码命名一致性**
>
> 38 设计稿正文写 `Gold / Diamond / Energy`,实际代码 `PlayerAttrService` 已用 `Coin / Diamond / Stamina`(与 44 §3.3 source 枚举 + 服务端 `PropertyType` 同源)。本子单沿**代码现状** Coin / Diamond / Stamina;若 dev 落地时发现 38 设计稿正文与代码漂移,**不**列入本子单同步重写(超本子单范围,可作为 carry-forward 由 dev 在交接区列入下一刀清理)。

## 三、客户端数据源与服务行为 {#service}

### 3.1 POCO 与结果包 {#poco}

`AttrLedgerEntry` 字段集与 45 §3.2 协议响应 entry 一一映射(7 字段);**不**含协议生成物中可能存在的内部字段(若服务端段意外多塞了字段)。

| 字段 | 类型 | 含义 / 约束 | 对位 45 §3.2 |
| --- | --- | --- | --- |
| `Timestamp` | `long` | 应用端 Unix ms UTC(= 44 §3.1 `Timestamp` 同源) | `timestamp` |
| `Kind` | `AttrType` 枚举(沿 [38 已建](#38-player-attr-client::service):`Coin / Diamond / Stamina / All`,本子单只取前三) | 属性种类;反序列化时 0 / 1 / 2 ↔ Coin / Diamond / Stamina;协议生成物中若类型为 int,客户端转 AttrType | `kind` |
| `BalanceBefore` | `long` | 变更前余额(非负) | `balanceBefore` |
| `BalanceAfter` | `long` | 变更后余额(非负;invariant `BalanceAfter = BalanceBefore + Delta`) | `balanceAfter` |
| `Delta` | `long` | 相对变更量(有符号) | `delta` |
| `Source` | `AttrChangeSource` 枚举(本子单新建,10 档同 44 §3.3) | source 枚举码(0 = Unknown / 1 = ChangeNameSpend / ...) | `source` |
| `ReasonRaw` | `string` | 原 reason 字符串(便于业务子分类) | `reasonRaw` |

`AttrLedgerPage` 结果包(`RemoteAttrLedgerService.FetchPageAsync` 返值):

| 字段 | 类型 | 含义 |
| --- | --- | --- |
| `Code` | `AttrLedgerQueryCode` 枚举(本子单新建) | `Success / InvalidRequest / ServiceUnavailable / NetworkDown` 四档 |
| `Entries` | `IReadOnlyList<AttrLedgerEntry>` | 按 `Timestamp` DESC(最新在前),失败时空集合(非 null) |
| `HasMore` | `bool` | 是否还有更旧的行(45 §3.2 `hasMore`);失败时 false |

`AttrLedgerQueryCode` 枚举(4 档,**比 45 §3.3 多一档 `NetworkDown`,沿 38 `ChangeReject` 同范式区分**「服务端返 ServiceUnavailable」与「客户端连接断 / RPC 异常」):

| 枚举值 | 触发 | UI 兜底文案 |
| --- | --- | --- |
| `Success` | 45 §3.3 Success | 正常渲染列表 |
| `InvalidRequest` | 45 §3.3 InvalidRequest(理论上客户端发的参数都合法,出现此码 = 客户端 bug) | 「请求参数异常,请联系客服」(ERROR 级日志便于排查) |
| `ServiceUnavailable` | 45 §3.3 ServiceUnavailable(MongoDB 不可达) | 「服务暂不可用,稍后再试」 |
| `NetworkDown` | 客户端 catch 网络异常(连接断 / 超时 / Session 未建立)| 「网络异常,请检查连接」 |

### 3.2 `IAttrLedgerSource` 接缝 + `RemoteAttrLedgerSource` 生产实现 {#source}

沿 [32 `IRemoteMailSource`](#32-mail-server) 范式:

| 项 | 内容 |
| --- | --- |
| **接缝 `IAttrLedgerSource`** | 唯一方法 `UniTask<AttrLedgerPage> FetchPageAsync(AttrType? kind, long sinceTs, int limit)`;返 `AttrLedgerPage`(失败时 `Code != Success` + `Entries` 空 + `HasMore` false);**不抛**(失败转结果码) |
| **生产实现 `RemoteAttrLedgerSource`** | 构造期注入 `FantasyNetwork`(或薄壳 IRpcGateway 沿 [38 §五](#38-player-attr-client::wiring) 同范式);`FetchPageAsync` 内:① 构造 `C2G_QueryAttrLedger`(kind=AttrType 转 int 0/1/2/3,sinceTs,limit);② 经 `Session.Call<C2G_QueryAttrLedger, G2C_QueryAttrLedgerResponse>` 发送等响应;③ try-catch 网络异常 → 返 `AttrLedgerQueryCode.NetworkDown`;④ 检查 `Session != null`(未登录 / 断开)→ 返 `NetworkDown`;⑤ 响应反序列化为 `AttrLedgerEntry`(逐字段拷贝)+ 按 `resultCode` 映射 `AttrLedgerQueryCode` |
| **单测桩** | `FakeAttrLedgerSource`(EditMode 单测注入,可配置返特定 entries / 特定 code / 异常,验 UI / 服务编排逻辑) |
| **不持本地状态** | 无缓存、无本地副本(每次调真请求);**禁**在客户端持久化 ledger(本机无审计副本,沿 44 §5.4「客户端可查 ledger」由服务端独占) |

### 3.3 `RemoteAttrLedgerService` 编排层 {#orchestrator}

沿 [32 `RemoteMailService`](#32-mail-server) 范式:

| 项 | 内容 |
| --- | --- |
| **职责** | 1. 注入 `IAttrLedgerSource`;2. 提供 `FetchPageAsync` 转发(本子单当前与接缝同 1:1,但服务层保持便于 Tier 2+ 加交叉编排 / 缓存策略 / 重试);3. **不持本地状态**(无缓存) |
| **挂入** | `GameContext.AttrLedger`(沿 [38 `GameContext.PlayerAttr`](#38-player-attr-client::wiring) + 32 `GameContext.Mail` 范式),启动期 `GameApp.StartGameLogic` 创建实例 + 注入生产 `RemoteAttrLedgerSource`(经 `FantasyNetwork.Session`) |
| **API** | `UniTask<AttrLedgerPage> FetchPageAsync(AttrType? kind, long sinceTs, int limit)`;`null` kind → 协议层 `kind=0`(不过滤);limit 客户端不钳制(服务端 45 §3.1 钳制上限 100) |
| **错误兜底** | source 层已返结构化 `AttrLedgerQueryCode`,服务层不再 catch;UI 层据 Code 出文案 |

### 3.4 source 整数 → 中文文案映射 {#source-text}

硬编码 `switch` 实现(沿 [32 MailText](#32-mail-server) 静态文案常量范式),覆盖 [44 §3.3 已登记 10 档](#44-player-attr-ledger::source):

| `AttrChangeSource` 枚举值 | 整数码 | 中文文案 | 来源刀 |
| --- | --- | --- | --- |
| `Unknown` | 0 | 「其他」 | 兜底 |
| `ChangeNameSpend` | 1 | 「改名扣钻」 | [38](#38-player-attr-client) |
| `MailClaim` | 2 | 「邮件领奖」 | [32](#32-mail-server) |
| `RedeemCode` | 3 | 「兑换码」 | [30](#30-redeem-code-server) |
| `RankSettleReward` | 4 | 「排行榜奖励」 | [33](#33-rank-settle-server) |
| `ActivityReward` | 5 | 「活动奖励」 | [39](#39-activity-server) / [40](#40-event-unlock-relay) / [43](#43-activity-login-batch) |
| `GameplayConsume` | 6 | 「玩法消费」 | Tier 2+ 玩法刀(占位) |
| `ShopPurchase` | 7 | 「商店购买」 | Tier 2+ 商店刀(占位) |
| `AdminGrant` | 8 | 「管理员发放」 | Tier 2+ GM 刀(占位) |
| `Refund` | 9 | 「退款」 | Tier 2+ 退款刀(占位) |
| (未来扩 source) | 10+ | 「其他」(默认分支) | 不报错,降级显示 |

**实现要点**(行为级):
- 单一**纯函数** `static string FormatSource(AttrChangeSource s)`,switch 表达式 + default 返 「其他」,**不抛**;
- **不**做 source 名称 i18n / 多语言(O5,Tier 2+ 上 Luban i18n 表再迁,本子单文案直接是中文常量字符串);
- **不**附带 `ReasonRaw` 细节(`ReasonRaw` 是 ad-hoc 子分类如 `mail_claim_456`,运营查 mongo 用;UI 展示简洁起见只显 source 文案,O8 备选「点击展开看 reasonRaw」)。

### 3.5 时间显示:本地时区 + 相对时间 {#time-format}

`Timestamp`(Unix ms UTC)→ 显示文案,两段:

| 时间窗 | 显示格式 | 实现 |
| --- | --- | --- |
| < 60 秒 | 「刚刚」 | (`DateTime.UtcNow - ts`).TotalSeconds < 60 |
| 60 秒 ~ 60 分钟 | 「N 分钟前」 | `.TotalMinutes` < 60 |
| 60 分钟 ~ 24 小时 | 「N 小时前」 | `.TotalHours` < 24 |
| 24 小时 ~ 7 天 | 「N 天前」 | `.TotalDays` < 7 |
| ≥ 7 天 | 「2026-06-21 14:23」(本地时区 yyyy-MM-dd HH:mm) | `.ToLocalTime().ToString("yyyy-MM-dd HH:mm")` |

**为什么混合相对 + 绝对时间?**(决策依据)

- 近期事件用相对(「3 分钟前」)语义更直观,符合社交媒体 / 邮件列表常见范式(32 邮件列表同范式);
- 远期事件用绝对(本地时区 yyyy-MM-dd HH:mm)避免「30 天前」类含糊;
- **不**用 UTC 显示(本地玩家看 UTC = 不直观,如「我下午 3 点改的名,显示 07:00 UTC」会困惑);
- **不**用 ISO 8601 全格式(`2026-06-21T14:23:45.123Z` 信息冗余,玩家不需要看到秒 / 毫秒)。

实现要点:**纯函数** `static string FormatRelativeTime(long timestampMs, DateTime now)`,`now` 入参化便于 EditMode 单测(`now` 注入固定值验各分支);生产代码调时传 `DateTime.UtcNow`。

## 四、UI 投放 {#ui}

### 4.1 独立窗 `PlayerAttrLedgerWindow`(沿 28 排行榜窗范式) {#window}

| 区域 | 内容 | 行为 |
| --- | --- | --- |
| 标题栏 | 「我的流水」+ 关闭按钮(右上 X) | 关闭走 `CloseWindow` 标准 UIWindow 范式 |
| 过滤栏 | 四档 tab:**全部 / 金币 / 钻石 / 体力**(沿 42 三属性顺序 + 一档「全部」头) | 点 tab 触发 `RefetchAsync(kind)`(清空当前列表 + 重新拿首屏);当前选中 tab 视觉高亮 |
| 列表 | 流水行,每行四列:**时间 + 属性图标 + delta + source 文案** + **余额变化**(`{BalanceBefore} → {BalanceAfter}`) | 滚动列表(ScrollView + Vertical Layout);空列表时显「暂无流水」 |
| 加载更多按钮 | 列表底部一个按钮 | `HasMore = true` 时显示「加载更多」(本子单**置灰 + 提示「翻旧页 Tier 2+」**,O3);`HasMore = false` 时显示「已加载全部」(不可点) |
| 状态栏 / 错误兜底 | 列表上方文案区(空时显示状态) | `ServiceUnavailable` → 「服务暂不可用,稍后再试」;`NetworkDown` → 「网络异常,请检查连接」;加载中 → 「正在加载...」 |

**列表行视觉**(行为级,具体由 dev / 美术取):

```
┌──────────────────────────────────────────────────┐
│ 3 分钟前   [钻]  -50    改名扣钻      100 → 50    │
│ 2 小时前   [金]  +500   排行榜奖励    1000 → 1500 │
│ 昨天       [体]  +5     活动奖励      0 → 5       │
│ 2026-06-15 14:23 [钻]  +100  邮件领奖   50 → 150  │
└──────────────────────────────────────────────────┘
```

- **属性图标**:复用 [42 §三](#42-tarot-hud-player-attr-bind::mapping) 占位:Coin → `gemstone` / Diamond → `gemstone2` / Stamina → `potion`;
- **delta 颜色**(增强档,O7):正数(收益)绿色 / 负数(消费)红色;**不做** = 黑色统一字号(本子单核心档默认不做颜色区分)
- **余额变化箭头**:`{BalanceBefore} → {BalanceAfter}`(箭头 → 用 Unicode `→` 或图标,dev 取);
- **列表行高 + 字号**:沿 28 排行榜窗范式(典型 60-80px 行高 + 字号 28-32,dev 据美术调)

**默认拉取条数**:**50 条**(O2 默认 50;沿 32 邮件列表常见范式;100 太多 + 滚动疲劳,30 太少 + 高频改名玩家一屏不够)。

### 4.2 入口决策:挂 PlayerInfoWindow 改名面板下方 {#entry}

**决策**:在 [PlayerInfoWindow](#25-player-info-window-art) 改名面板下方加一个新按钮「我的流水」,点击 → `OpenWindow<PlayerAttrLedgerWindow>()`。

**为什么不挂 PlayerInfoWindow Tab?**

- PlayerInfoWindow 是模态弹窗(头像 + 改名 + 生日,设计 25),tab 化破模态语义 + 需重布局 + 与 25 表现层「头像 + 改名 + 生日」单一职责相悖;
- 独立窗(我的流水)沿 28 排行榜窗范式,信息密度匹配(列表型 UI 需更大空间)。

**为什么不挂 HUD 长按弹窗?**

- 交互隐蔽(玩家不主动尝试长按 HUD),发现性差;
- HUD 长按通常用于「快捷操作」(如长按音量键调音量),不适合「打开复杂窗口」;
- HUD 加号点击当前是 `Log` 待建(沿 27 §5.3 + 42 §三),改长按弹窗会让 HUD 加号语义混乱。

**为什么挂在 PlayerInfoWindow 改名面板下方?**

- PlayerInfoWindow 是玩家自查个人信息的入口(头像 / 玩家名),「我的流水」是「我的钻石 / 金币 / 体力变动」,语义自然连续;
- 入口位置紧凑(改名面板下方加一个 Button,不破 25 现有节点树),不需要新建顶层入口(主菜单 / 设置 / HUD);
- 用户进 PlayerInfoWindow 多是「想看个人信息」,顺手看流水的发现性好。

**UI 节点**(行为级,具体节点名由 dev 取,沿 25 §三现有节点树范式):
- 在 `PlayerInfoWindow.cs` `BuildStatic` 或对应初始化函数中,加 `_btnLedger = FindChildComponent<Button>("Root/NameBlock/m_btn_LedgerEntry")`(或 dev 取的节点路径);
- 绑定 `_btnLedger.onClick.AddListener(() => GameModule.UI.OpenWindow<PlayerAttrLedgerWindow>())`(沿 25 现有按钮注册范式);
- 美术节点(button 底 + 文字「我的流水」)在 prefab 中加,沿 25 §三现有 `Sheet_settings.button` 长条底图占位;若 prefab 节点缺失(美术未补),`_btnLedger` 为 null,运行期 null-safe 跳过(不影响 25 既有改名 / 头像功能)。

### 4.3 加载更多按钮的取舍(本子单的实际行为) {#load-more}

> [!WARNING]
> **本子单「加载更多」按钮是占位**——45 §4.2 + §5.4 已明示:协议字段 `sinceTs` 是「`ts > sinceTs`」**取更新**语义,**不是翻旧页**;翻旧页需 Tier 2+ 协议加 `untilTs` 字段(45 O3)。
>
> **本子单实际行为**:首屏拿 50 条最新(`sinceTs=0, limit=50`);若 `HasMore=true` 表示服务端有更旧的行,但客户端**无法**用当前协议字段翻;**按钮置灰 + 提示文案「翻旧页待 Tier 2+ 协议扩展」**。
>
> **替代**(本子单核心档不做):**下拉刷新**——用 `sinceTs = entries[0].Timestamp` 拉「自上次刷新以来的新增行」是 45 协议天然语义,但本子单首屏已拿 50 条最新 + 用户进窗就实时拉,「下拉刷新」收益低(玩家很少在窗里等几分钟看新增),**O6 备选**。

### 4.4 加号点击 / HUD 不动 {#hud-untouched}

本子单**不**动 [42 HUD 加号](#42-tarot-hud-player-attr-bind),仍 `Log` 待建(沿 27 / 42 去变现处置);本子单**不**动 HUD 三属性绑定(42 已落 + 实时刷新);本子单**不**在 HUD 显示「点击查看流水」入口(去变现下不投流量到流水窗,流水只是辅助查询)。

## 五、整局走查与崩法 {#walk}

把本子单的客户端段在「**首次打开 / 空列表 / 服务不可达 / 网络断 / 高频切 tab / 切 tab 过程中切窗 / 单 kind 过滤 / source 未知值 / 远未来时间戳**」九类场景下走一遍。

### 5.1 数据源 / 网络相关崩法

| 崩法 | 后果 | 对策 |
| --- | --- | --- |
| 玩家打开窗 → 服务端 MongoDB 抖 → 45 handler 返 ServiceUnavailable | UI 显示空列表 + 「服务暂不可用」文案 | `RemoteAttrLedgerSource` 把响应 resultCode 转 `AttrLedgerQueryCode.ServiceUnavailable`;UI 据此显文案(§3.1 表) |
| 玩家打开窗 → 客户端网络断 → Session 未建立 / RPC 抛异常 | 异常上抛致 UI 崩 | `RemoteAttrLedgerSource.FetchPageAsync` try-catch 网络异常 → 返 `AttrLedgerQueryCode.NetworkDown`(§3.1);单测验「Session null 时返 NetworkDown」 |
| 高频切 tab(用户连点)→ 多个 RPC 同时在飞 | 后到的请求覆盖先到的请求 → UI 列表错乱(显示的不是当前 tab) | **取消旧请求**:`PlayerAttrLedgerWindow` 持 `_currentFetchToken`(int 递增),每次发请求记 token,响应回来时核对 token 是否匹配当前;不匹配则丢弃响应(沿 22 RankService 同范式);PV4 验「连点切 tab → 显示的是最后点的 tab」 |
| 玩家切 tab 过程中关窗 → response 回来时 window 已 dispose | 调 `Window.MethodX` 在已 dispose window 上 → NRE | Window 销毁时:① 持 `_disposed=true` 标记;② FetchPageAsync 回来时核 `_disposed` 跳过 UI 更新;沿 38 `PlayerInfoWindow.OnCloseInternal` 解订阅范式 |
| 玩家在窗里 5 分钟没动 → 数据陈旧 | 玩家看到「3 分钟前」其实已经 8 分钟前(相对时间不刷新) | **不做自动刷新**(本子单不持后台计时器);玩家**手动**关窗重开拉新数据;O9 备选「每 30 秒刷新一次相对时间字符串」 |

### 5.2 数据 / 协议相关崩法

| 崩法 | 后果 | 对策 |
| --- | --- | --- |
| 服务端返 source = 10(未来 Tier 2+ 新加 source)→ 客户端 switch 不识别 | switch default 落「其他」 + 不报错 | `FormatSource(default)` 返「其他」(§3.4);PV6 验「source=99 不抛 + 显「其他」」 |
| 服务端返 entry 字段比客户端 POCO 多(Tier 2+ 服务端加字段) | 反序列化层(Fantasy 源生成器 / protobuf)按已知字段拷贝,未知字段忽略 | 沿 protobuf 演进规范(向后兼容);客户端 POCO 不会破 |
| 服务端返 entry 字段比客户端 POCO 少(本子单 entry 期望 7 字段,服务端只返 6 字段)| 客户端 POCO 缺字段为 default(0 / empty string)→ UI 显「1970-01-01 / 0 → 0 / 其他」错乱 | 协议契约 45 §3.2 已锁 7 字段不变(SV15 已守);若实际出现 = 服务端段 bug,联调 E1 拦;客户端**不**额外保底(过度防御) |
| 远未来时间戳(timestamp = 9999 年)→ `TimeSpan` 计算溢出 | 极端情况下「N 天前」算出大数字 | `FormatRelativeTime` 加边界:`if (delta > 365 * 7 days) return ToLocalTime().Format("yyyy-MM-dd HH:mm")`(已覆盖 §3.5 ≥7 天分支,7 年 vs 7 天差异本就不显示「N 天前」);PV7 验「timestamp = far-future → 显绝对时间」 |
| source = `Unknown`(服务端写 ledger 时未映射上)| 客户端显「其他」 + reasonRaw 留全 | 沿 44 §3.5「source 映射未命中 → Unknown + reasonRaw 原样」;UI 不暴露 reasonRaw(§3.4 决策 + O8 备选),「其他」+ 不显 reasonRaw 是设计意图 |

### 5.3 UI / 入口相关崩法

| 崩法 | 后果 | 对策 |
| --- | --- | --- |
| `IsReady=false` 时玩家打开流水窗(快照未到)| 流水拉 OK(独立于 38 `PlayerAttrService.IsReady`),正常显示 | 流水窗**不依赖** 38 PlayerAttrService.IsReady;打开即拉协议,与当前余额视图独立;PV8 验「快照未到时打开流水窗,流水能拉数据」 |
| PlayerInfoWindow prefab 节点 `m_btn_LedgerEntry` 缺失(美术未补)| `FindChildComponent` 返 null,后续 `_btnLedger.onClick.AddListener` NRE | 沿 25 现有 null-safe 范式:`if (_btnLedger != null) _btnLedger.onClick.AddListener(...)`;美术未补时 25 既有功能不破,入口暂不可点(`Log` 提示 + Tier 2+ 美术补) |
| 玩家在网络断 / ServiceUnavailable 状态下连点「加载更多」(本子单置灰但玩家仍点)| 多次发请求 → 多次返失败 → 文案抖动 | 按钮置灰时**禁点击**(Button.interactable = false);仅 `HasMore=true` + 上次 Code=Success 才使能;`HasMore=false` 或上次失败时不可点 |
| 切窗时 source 文案常量被 GC 回收 | 静态常量不会被 GC | `FormatSource` 返 `string` 常量,常量驻留 string pool,无 GC 风险 |

### 5.4 诚实边界(本子单守住什么、不守什么) {#honest-edge}

本子单**守住**:
- 流水查询通路客户端段闭环(POCO + 数据源接缝 + 远程实现 + 服务编排 + UI 投放 + 入口接钮)
- 离线 / 服务不可达**不本地放行 ledger**(空列表 + 提示文案)
- source 文案 10 档覆盖 44 §3.3 已登记
- 时间显示本地时区 + 相对时间(玩家友好)
- 属性图标复用 HUD 占位(不新增切图)
- Window 销毁解订阅 + 取消在飞请求(防 dispose 后 NRE)
- 单 kind 过滤 + 切 tab 重拉(全 / Coin / Diamond / Stamina 四档)
- 38 `PlayerAttrService` 核心契约不动 + 42 HUD 不动 + 25 PlayerInfoWindow 改名面板不动(只加按钮)
- 网络异常 catch 转结构化错误码 `NetworkDown`(不抛)

本子单**不守**:
- **翻旧页 / 加载更多**:加载更多按钮置灰提示「Tier 2+」,需服务端加 `untilTs` 协议字段(45 O3 + O3);本子单首屏拿 50 条最新即可
- **下拉刷新**:本子单不做(玩家手动关窗重开等效),O6
- **本地缓存 ledger**:不持本地副本,每次打开重拉
- **多语言文案 i18n**:硬编码中文,Tier 2+ 上 i18n 系统再迁,O5
- **delta 正负颜色区分**:核心档黑色统一,O7
- **ReasonRaw 展开**:UI 不展示 reasonRaw(运营查 mongo 用),O8
- **自动刷新相对时间**:玩家长时间停留不刷新,O9
- **HUD 入口**:不在 HUD 加「点击查看流水」入口,流水只是辅助查询
- **多账号 / 切账号**:玩家切账号后 `RemoteAttrLedgerService` 实例不重建(沿 GameContext 单例),但下次拉数据时身份从会话取(45 SV11 守),内容自动对位新账号
- **运营 GM 查账 / BI 聚合 / 退款** :沿 44 / 45 §5.4 不守列表 Tier 2+

## 六、同步重写清单(本任务内须完成的他篇覆盖式重写,sweep 范围闭合) {#sweep}

按 conventions §6「同任务内同步重写不留漂移窗口」:

- **45 §6.2 接口余量表**「客户端业务接入(RemoteAttrLedgerService)」/「客户端我的流水 UI 投放」/「source 整数 → 文本映射」三行 演进列覆盖式重写为「**46 已交付**(客户端段:`IAttrLedgerSource` + `RemoteAttrLedgerSource` + `RemoteAttrLedgerService` + `PlayerAttrLedgerWindow` + source 中文文案 switch)」
- **45 §一切分表**「数据源 RemoteAttrLedgerService(切真实 RPC)」/「我的流水 UI 投放(列表行 / 过滤栏 / 分页)」/「source 整数 → 文本映射」三行 本子单是否交付列从「**不交付**」覆盖式重写为「**交付 · 46 客户端段已落**」
- **45 §5.4 不守列表**「客户端业务接入」/「source 整数 → 文本映射」相关行覆盖式重写为「**46 已交付**」(保留「翻旧页 / cursor 分页」「客户端缓存 / 离线降级源」「运营 GM」「全量导出 / BI」「跨进程」「协议加密」继续在不守列表,因本子单也不做)
- **45 §7.4 BLOCKED 列表**「客户端业务接入(`RemoteAttrLedgerService`)」/「客户端「我的流水」UI 投放」/「source 整数 → 人类可读文本映射」三行 覆盖式重写为「**46 已交付**」
- **44 §一切分表**「客户端业务接入(`RemoteAttrLedgerService`)+ 我的流水 UI」行 归属列从「**不做 · Tier 2+ 客户端段后续刀**」覆盖式重写为「**客户端 · 46 已交付**」
- **44 §6.2 接口余量表**「客户端我的流水 UI / 拉流水 RPC」行 演进列追加「**46 客户端段已落**」段
- **25 §三 / §六 节点树 / 交付列表**:加「我的流水入口按钮」(`Root/NameBlock/m_btn_LedgerEntry` 节点占位 + 沿 Sheet_settings.button 长条底图);**美术补**为「Tier 2+ 美术补图」(本子单不阻塞,prefab 节点缺失时 null-safe 跳过)
- **`design-docs/assets/nav.js` GROUPS**:在「全栈 / 服务端 / 客户端段」组的 45 之后加 46 入口(`href: '46-player-attr-ledger-client.html'`,Tier 2 客户端段集中显示);tag / title / desc 详尽度沿 38 / 32 同口径
- **`design-docs/index.html`**:`?v=10` → `?v=11` 递增防缓存

## 七、验收点 {#accept}

客户端段(dev)PV(PlayerView)+ EditMode 单测 + Code Review;联调段 E(End-to-end,真往返)。

### 7.1 客户端段验收(PV)

| # | 验收点 | 完成定义(行为可观测) |
| --- | --- | --- |
| PV1 | Unity 工程编译通过 | Unity 编辑器 / Player 编译 0 error;新增 `IAttrLedgerSource` / `RemoteAttrLedgerSource` / `RemoteAttrLedgerService` / `AttrLedgerEntry` / `AttrLedgerPage` / `AttrLedgerQueryCode` / `AttrChangeSource` / `PlayerAttrLedgerWindow` / source 文案 switch 等新类编译过 |
| PV2 | 已有客户端段零回归 | 38 `PlayerAttrService` 核心契约 / 42 HUD 三属性绑定 / 25 PlayerInfoWindow 改名面板钻石余额 / 27 主玩法 HUD / 30 兑换码 / 32 邮件领奖窗 / 22 排行榜窗 EditMode + PlayMode 测试全 pass;改名扣钻 + HUD 三属性显示正常 |
| PV3 | 打开窗显示空列表(无 ledger 账号) | 全新 UUID 钻石 / 金币 / 体力都 0,从未触发任何属性变更 → PlayerInfoWindow 改名面板下点「我的流水」按钮 → 窗口打开 → 流水列表显「暂无流水」(空状态文案);**不**显错误 + 状态栏不显错误兜底 |
| PV4 | 打开窗显示非空列表(有 ledger 账号) | UUID 触发 5 笔属性变更(改名扣钻 50 + 进程内 API 调 +5 / +10 / +20 / +5 金币)→ 打开流水窗 → 列表显示 5 行,**按 timestamp DESC**(最新在前);每行四列(时间 + 属性图标 + delta + source 文案 + 余额变化);时间显示符合 §3.5(< 1 分钟「刚刚」,余比例显示);source 文案对位(`ChangeNameSpend` → 「改名扣钻」,`Unknown` → 「其他」);属性图标沿 42 §三 占位(Coin → gemstone / Diamond → gemstone2 / Stamina → potion) |
| PV5 | kind 过滤(切 tab 重拉) | 某 UUID 有 5 行 ledger(3 行 Diamond + 2 行 Coin)→ 打开流水窗(全部 tab)显 5 行;切「钻石」tab 显 3 行(仅 Diamond);切「金币」tab 显 2 行(仅 Coin);切「体力」tab 显 0 行 + 「暂无流水」;切「全部」tab 显 5 行;切 tab 时列表先清空再拉(无残留旧 tab 数据) |
| PV6 | source 未知值不抛 + 显「其他」 | EditMode 单测注入 `FakeAttrLedgerSource` 返一行 `Source=(AttrChangeSource)99` → `FormatSource` 返「其他」 + UI 渲染不抛;同理 `Source=Unknown(0)` 显「其他」 |
| PV7 | 时间显示边界(EditMode 单测) | `FormatRelativeTime` 单测覆盖各分支:① now - 30s → 「刚刚」;② now - 5min → 「5 分钟前」;③ now - 2h → 「2 小时前」;④ now - 3d → 「3 天前」;⑤ now - 10d → 「2026-06-XX HH:mm」(本地时区);⑥ now + 1d(未来时间戳)→ 不抛 + 显绝对时间(降级);⑦ now - 365 days × 7 → 显绝对时间 |
| PV8 | 流水窗与 38 `PlayerAttrService.IsReady` 独立 | 38 `IsReady=false`(快照未到)时打开流水窗 → 流水拉协议 OK(独立于 PlayerAttrService);PV3 / PV4 等正常 PASS;不显「加载中」混淆 |
| PV9 | ServiceUnavailable 兜底(MongoDB 不可达) | 模拟 MongoDB 停服(或服务端 ledger 句柄设 null) → 打开流水窗 → 列表空 + 状态栏显「服务暂不可用,稍后再试」;**不**抛异常 + **不**本地造假数据 |
| PV10 | NetworkDown 兜底(客户端断网) | 模拟 `Session=null`(未登录)或 RPC 抛异常 → 打开流水窗 → 列表空 + 状态栏显「网络异常,请检查连接」;**不**抛异常 + 不本地造假;EditMode 单测可注入 `FakeAttrLedgerSource` 模拟抛异常验 catch |
| PV11 | 切窗 + 切 tab 期间响应回来不 NRE | EditMode 单测:Window 销毁后 source 模拟延迟响应回来 → UI 不抛(`_disposed` 标记跳过);PlayMode 手验:打开流水窗 → 切 tab → 立刻关窗(可点关闭按钮)→ 无异常 |
| PV12 | 入口按钮 null-safe | 在 PlayerInfoWindow prefab `Root/NameBlock/m_btn_LedgerEntry` 节点缺失(美术未补)情境 → `_btnLedger` 为 null → PlayerInfoWindow `OnCreate` 不抛;25 既有改名 / 头像 / 生日功能不受影响;入口暂不可点(美术补完即用) |
| PV13 | 加载更多按钮置灰提示 | `HasMore=true` 时「加载更多」按钮置灰(Button.interactable=false)+ 文案显「翻旧页待 Tier 2+」;`HasMore=false` 时显「已加载全部」;玩家点不到 = 无副作用 |
| PV14 | Code Review | 重点核 8 项:① `RemoteAttrLedgerSource` catch 网络异常转 `NetworkDown` 不抛;② Window 销毁解订阅 + 取消请求 token(防 dispose 后 NRE);③ source 文案 switch 覆盖 44 §3.3 10 档 + default「其他」;④ 时间格式纯函数 `FormatRelativeTime(ts, now)` `now` 入参化便于单测;⑤ `RemoteAttrLedgerService` 不持本地状态(无缓存 / 无副本,守 44 §5.4 服务端独占);⑥ 入口按钮 `FindChildComponent` null-safe(节点缺失不抛);⑦ 不引用 Fantasy.* 命名空间在 HotFix 业务层(网络层薄壳沿 38 §五 IRpcGateway 范式);⑧ 不动 38 `PlayerAttrService` 核心契约 / 42 HUD 接线 / 25 PlayerInfoWindow 改名面板 / 27 主玩法 HUD |
| PV15 | Carry-forward 已清(Fantasy `PlayerAttrLedgerDoc.Kind` doc comment) | Fantasy `examples/Server/APP/Entity/Game Examples/Gate/Player/PlayerAttrLedgerDoc.cs:31` 注释 `(沿 37 PropertyType 枚举:Coin=1 / Diamond=2 / Stamina=3,SV10)` 与实际 `PropertyType` 枚举值(Coin=0 / Diamond=1 / Stamina=2,见 `AttrLedgerQueryHelper.cs:32` 解释)不符 → 改注释为现状(具体改文待 dev / server-dev 落地时核对枚举源 + 改 doc comment);本验收点锚在「该注释与实际枚举值一致」上,**Code Review 拦**(由 server-dev 修改 Fantasy 仓的 doc comment,客户端段无变化) |

### 7.2 联调段验收(E)— 真往返

| # | 验收点 | 完成定义 |
| --- | --- | --- |
| E1 | 真往返:改名扣钻后流水可见 | 起服 + 客户端登录 → 在 PlayerInfoWindow 改名(扣 50 钻)→ 关 PlayerInfoWindow 重开 → 点「我的流水」→ 列表显 1 行「N 秒前 [钻] -50 改名扣钻 100→50」;**核**:① source 文案对位 `ChangeNameSpend` → 「改名扣钻」;② delta 显 `-50`;③ 余额变化 `BalanceBefore → BalanceAfter` 与服务端真值一致 |
| E2 | 真往返:登录奖到账后流水可见(若 32 邮件→37 接线已落) | 起服 + 客户端首次登录 → 登录奖发邮件 → 邮件领奖 → 关 PlayerInfoWindow 重开 → 点「我的流水」→ 列表显登录奖那行(典型 `Coin +5 邮件领奖`);**注**:此验收点依赖 32 邮件 → 37 接线全栈刀已落(`MailClaim` source 映射已通);若未落,本验收点判 BLOCKED |
| E3 | 真往返:kind 过滤后服务端只返过滤后行 | 同 E1 场景 → 打开流水窗 → 切「钻石」tab → 列表仅显改名扣钻那 1 行(其它 kind 不显);切「金币」tab → 仅显金币变更行;**核**:服务端 handler 据 `kind` 字段过滤(45 SV7),客户端不本地过滤(本地若过滤会拉 50 条 Coin 后只显 Diamond 部分,造成列表稀疏) |
| E4 | 真往返:服务不可达兜底 | 起服 + 客户端登录 → 改一次名扣钻(确保有 ledger)→ 停 MongoDB(或断服)→ 客户端点「我的流水」→ 列表空 + 显「服务暂不可用,稍后再试」;**不**抛异常;**不**本地造假 |

### 7.3 不在本子单验收 / BLOCKED {#blocked-list}

- **本机 MongoDB(`D:\mongodb-portable`)不可达** → 真往返查 ledger 类 PV(PV3 / PV4 / PV5 / PV9 / E1 / E2 / E3 / E4)判 **BLOCKED 非 FAIL**(沿 [44 §7.2](#44-player-attr-ledger::bloked-list) + [45 §7.4](#45-player-attr-ledger-query::blocked-list));编译 / EditMode 单测(PV1 / PV6 / PV7 / PV8 单测部分 / PV10 单测部分 / PV11 单测部分)+ Code Review(PV14)+ Carry-forward 注释修正(PV15)照常验
- **32 邮件 → 37 接线刀未落 → E2 验收依赖未到位** → E2 判 BLOCKED(本子单 PASS 不阻塞 E2,留 32 全栈接线刀落地后补验)
- **翻旧页 / 加载更多真功能** → Tier 2+(45 O3 / 本子单 O3,需协议加 `untilTs` 字段)
- **下拉刷新** → 本子单不做(O6)
- **本地缓存 ledger** → 本子单不做(守服务端独占)
- **多语言文案 i18n** → 本子单不做(O5,Tier 2+ 上 i18n 系统再迁)
- **delta 正负颜色区分** → 本子单不做(O7,核心档黑色统一)
- **ReasonRaw 展开** → 本子单不做(O8)
- **自动刷新相对时间** → 本子单不做(O9)
- **入口按钮美术** → Tier 2+(占位节点已加,美术补图后即用)

## 八、待拍板清单 {#open}

范围开关,自治授权下**均取安全默认推进**(本设计自治内拍板);列此备查,要改另开增量。

| # | 开关 | 本设计默认 | 备选 / 触发改动 |
| --- | --- | --- | --- |
| O1 | UI 位置 | **独立窗 + PlayerInfoWindow 改名面板下方按钮入口** | 备选:挂 PlayerInfoWindow Tab(否——破模态语义)/ HUD 长按弹窗(否——发现性差);见 §4.1 / §4.2 |
| O2 | 默认拉取条数 | **50 条** | 30 / 100(本设计选 50 = 32 邮件常见 + 信息密度适中) |
| O3 | 翻旧页 / 加载更多 | **本子单不做**(置灰 + 提示「Tier 2+」) | Tier 2+ 加 45 协议 `untilTs` 字段,handler 加 `Timestamp < untilTs` 条件,本子单 UI 按钮去置灰使能 |
| O4 | 属性图标 | **复用 HUD 占位**(沿 42 §三 gemstone / gemstone2 / potion) | Tier 2+ 美术出三属性专属图标(金币图 / 钻石图 / 心型体力图)+ 重跑打表;本子单不阻塞 |
| O5 | source 文案 i18n vs 硬编码 | **硬编码 switch 中文** | Tier 2+ 上 Luban i18n 表(典型 `attr_source.xlsx` + textId 列),`FormatSource` 改 i18n 查表 |
| O6 | 下拉刷新 | **本子单不做** | Tier 2+ 加(玩家手动关窗重开等效) |
| O7 | delta 正负颜色 | **本子单不做**(黑色统一) | 增强档:正数绿 / 负数红(色弱友好场景需谨慎,Tier 2+ 配辅助图标如 + / -) |
| O8 | ReasonRaw 展开 | **本子单不显示** | Tier 2+ 列表行点击展开二级视图(对客服查账 + 玩家自查子分类有用) |
| O9 | 相对时间自动刷新 | **本子单不做**(玩家手动关窗重开) | 增强档:窗内开 30 秒计时器,每 30 秒刷新一次相对时间字符串 |
| O10 | NetworkDown vs ServiceUnavailable 文案区分 | **区分两档**(沿 38 ChangeReject 范式) | 简化方案:统一显「网络异常,请稍后再试」(对玩家区别不大) |
| O11 | 加载中状态 | **状态栏显「正在加载...」** | Loading spinner(美术资源未建,占位文字代替) |
| O12 | 入口按钮位置 | **PlayerInfoWindow 改名面板下方** | 备选:头像下方 / 标题栏右边 / 生日下方;§4.2 默认按改名下方语义连续 |

## 九、风险表 {#risk}

| 风险 | 应对 |
| --- | --- |
| **加载更多置灰提示给玩家「Tier 2+」字眼是露黑话 / 内部术语**(违 conventions §5 + 用户偏好 `plain-language-no-pipeline-jargon`) | **文案改写**:不显「Tier 2+」字眼,改显「已加载本次全部」或「更多功能即将推出」(plain-language 用户友好版);Code Review 拦字面量;dev 落地时按此约束取文案,本设计稿 §4.3 + PV13 提及「Tier 2+」是设计阶段描述,UI 实际文案非此 |
| **PlayerInfoWindow prefab 美术节点 `m_btn_LedgerEntry` 缺失** → 入口暂不可点 | PV12 + null-safe + 美术 carry-forward:本子单 prefab 节点占位 + 按钮 null-safe;美术补图后即用(本子单不阻塞美术) |
| **`RemoteAttrLedgerService` 持本地状态 → 多账号切换时显错** | §3.3 关键约束 + PV14 ⑤:服务编排层不持本地状态,每次拉真;切账号后下次拉数据时身份从会话取,内容自动对位新账号 |
| **协议生成物字段名 dev 取错 → 反序列化失败** | E1 + 沿 32 / 38 已落客户端段范式;`RemoteAttrLedgerSource` 按 45 协议生成物字段一一映射 → POCO `AttrLedgerEntry`,字段映射在 dev 内做(本设计稿不指字面量) |
| **切 tab 高频 / 切窗关闭中响应回来致 NRE** | §5.1 + §5.3 + PV11:`_currentFetchToken` 取消旧请求 + `_disposed` 标记跳过 UI 更新 |
| **服务端段 source 文本映射(O5)与客户端硬编码 switch 漂移** | 44 §3.3「单一映射函数」+ 本子单 §3.4 10 档与 44 §3.3 一一对位;Code Review 拦新增 source 时是否同步更新客户端 switch;Tier 2+ 上 i18n 表后此风险消失 |
| **carry-forward 注释修正(PV15)在客户端段任务内修服务端注释 → 跨仓任务边界** | dev 在本子单收口时**Code Review 拦**,实际改动由 server-dev / dev 一起核;改动文件路径明确(`Fantasy/examples/.../PlayerAttrLedgerDoc.cs:31`),改动内容明确(注释枚举值与代码同步),改动范围极小(单行注释);属客户端段交接区里的服务端 carry-forward(test/server-test 复核) |
| **加载更多按钮置灰是「假交互」让玩家觉得功能没做完** | 文案要诚实(「已加载本次全部」+ 用户友好) + Tier 2+ 协议扩 `untilTs` 后立刻使能(同步重写 45 时同步本设计稿) |
| **本子单与 42 HUD 三属性绑定的属性图标复用关系破** | §四 + PV14 ⑧:Code Review 拦本子单**不**改 27 / 42 切图绑定;若 42 后续 art-replace 用真专属图标(O4),本子单流水窗自动复用(沿同 Atlas 范式) |

## 关联文档

- [45 · 玩家属性 ledger 查询协议(Tier 2 第 3 子单 · server 段 + 客户端协议生成物)— 本子单的协议地基](#45-player-attr-ledger-query)
- [44 · 玩家属性 ledger 审计日志(Tier 2 第 2 子单 · server)— source 枚举 10 档登记](#44-player-attr-ledger)
- [37 · 玩家属性服务端(Tier 2 第 1 子单)— PropertyType 枚举源(Coin=0 / Diamond=1 / Stamina=2)](#37-player-attr-server)
- [38 · 玩家属性客户端(Tier 2 第 2 子单)— `PlayerAttrService` 数据层 + `IRpcGateway` 范式](#38-player-attr-client)
- [42 · tarot HUD 三属性绑定(Tier 2 第 3 子单 · client HUD)— 属性图标占位映射(Coin → gemstone / Diamond → gemstone2 / Stamina → potion)](#42-tarot-hud-player-attr-bind)
- [32 · 邮件服务端化 — `RemoteMailService` + `IRemoteMailSource` 编排范式](#32-mail-server)
- [25 · 个人信息窗美术换皮 — 入口按钮宿主](#25-player-info-window-art)
- [22 · 排行榜底层系统 — 远程数据源切换范式](#22-rank-system)
- [28 · 排行榜窗美术换皮 — 我的流水窗 UI 范式参照](#28-rank-window-art)
