# plan 状态

当前任务:Tier 2 玩家属性权威 · 服务端段 · 第 1 子单(金币 / 钻石 / 体力 三属性搬服务端)

## 范围与决策(本子单 = Tier 2 服务端段地基,自治默认推进)

- **设计稿**:新建 `design-docs/37-player-attr-server.md`(`nav.js` GROUPS 在「全栈 / 服务端」组 36 之前注册 `37-player-attr-server.html` + 详细 desc)
- **范围**:严守 boss brief 「服务端权威 + 通用变更入口 + 主动推送」三件事的服务端段铺地基;**不做** ledger 历史 / 商店购买路径接通 / 活动 / 任务 / 排行榜结算的奖励发放路径接通 / 退款 / 客户端 PlayerPrefs 迁移 / Player 模块改持有方式 / 其它属性(经验/等级/体力上限/恢复速率) / 多端跨进程推送
- **客户端工程业务代码零 diff**(协议生成物允许更新,因新增三条 RPC):本子单纯 server 段;PlayerPrefs / Player 模块 / PlayerNameGenerator 全保留,客户端段下一刀(Tier 2 第 2 子单)接

## 安全默认采纳(boss brief 给的全部 + 设计自治新增)

- **D1 独立 `players` 集合,主键 = UUID,与 35 `accounts._id` 1:1 同源**:不合并到 `accounts` 加字段——职责单一(35=身份账本、37=属性账本)、更新频率不同(35 低频、37 高频)、未来扩展(Tier 2+ 加 ledger 流水时外键自然)、零迁移(新建无需 schema 兼容);plan 决策(boss brief 留 plan 自决,取此为安全默认)
- **D2 单一通用变更入口 `C2G_PropertyChangeRequest`(类型 + delta + reason)**:不细分增/减/消费三 RPC——校验集中、reason 字段统一审计、增减语义由 reason 字符串自标(boss brief 给的默认,本稿沿用 + §一切分表「为何单一好」论证)
- **D3 登录拉一次属性快照 + 服务端主动推送 `G2C_PropertyDeltaPush`**:不每次操作前 GetSnapshot(每次 + 1 RTT 体验差 + 仍需通用入口才能反作弊);快照随登录响应捎带 / 独立 RPC 形态由服务端段定(boss brief 给的默认 + 服务端段微调 O4)
- **D4 校验体系**:余额下界(不许负) + 类型上界(运营配置,默认 金币 999999999 / 钻石 999999 / 体力 5) + 原子 `FindOneAndUpdate`(MongoDB 端校验,**非**应用层先查后写)— 反作弊核心(boss brief 给的默认,§3.4 论证「为何放 MongoDB 不放应用层」+ §5.4 SV11 验「初始 100 并发各扣 80 = 总扣 80 不超发」)
- **D5 首登在 35 RegisterOrLogin upsert 钩子内顺带 setOnInsert 写一份初始 `players`**(与 35 同事务模式但落 `players` 集合;两条 setOnInsert 互相不影响,各自只在 insert 路径写默认值)— 沿 35 §3.2 范式;boss brief 给的默认
- **D6 三属性首登初始值**:金币 0 / 钻石 0 / 体力 5(运营配置可改;**不读** PlayerPrefs 老值——任何客户端可改的 PlayerPrefs 都会破反作弊红线,故首登 = 服务端配置值为真,这是「服务端权威」基线下的必然取舍)— plan 设计自治拍板,记 decisions(boss brief 未明说)
- **D7 服务端进程内变更 API 与 PropertyChangeRequest 共用同一套校验 + 写库 + 推送**:不另造发奖路径——业务系统(商店/邮件/活动/任务/排行榜结算)调内部 API 时仍过同套上界/下界,内部 bug 无法因「内部调用」绕过反作弊 — plan 设计自治拍板(boss brief 留 plan 自决「与 35 RegisterOrLogin 钩子的初始化关系按 plan 拍板」,§3.5 论证)
- **D8 客户端绝对值禁报**:PropertyChangeRequest **不含**绝对余额字段;handler 仅消费类型 + delta + reason;Code Review 必拦(§3.3.2 + §5.5 + SV17 ②)— boss brief 红线「服务端永不信客户端传入绝对数值」的落实
- **D9 反作弊缺口(PlayerNameGenerator.cs:152 钻石校验恒返 true)处理路径**:本刀不动客户端;由「服务端校验」一侧合上(服务端校验上线 + 客户端段下一刀让钻石消费走 PropertyChangeRequest)。**不**走「修复客户端校验」治标路径(客户端任何校验都可被绕过,治标不治本)— plan 设计自治拍板,记 decisions
- **D10 三条新 RPC + 服务端进程内 API 形态由服务端段定**:plan code-blind 不指字面量(消息名/字段代码名/方法名/类名等);plan 给行为语义 + 字段集语义,service-dev 据 Fantasy.Net 现状取(沿 35 / 30 / 31 / 32 / 33 范式)
- **D11 推送丢失不重试**:推送是「绝对余额快照」非「相对变更流水」,丢失 = 下次登录拉快照对齐;Tier 3 实时一致性强求时加(§5.7 + O6)
- **D12 ledger 历史本子单不做**:仅持单字段「末次变更时间」;reason 字段虽接收任意字符串但不存流水(Tier 2+ ledger 落地刀启用)
- **D13 既有四个全栈特性 + 35 零回归**:30/31/32/33/35 业务集合 schema 与处理逻辑零改动;account 字段语义不变(SV16)

## 守不变量(给 server-dev / server-test 的硬边界)

- **客户端工程 `Assets/GameScripts/HotFix/` git diff 为空**:Unity 工程业务代码零改动;`Assets/GameProto/` 协议生成物允许新增(本子单新增三条 RPC,生成物自然会更新),但**业务代码零 diff**(SV15 区分协议生成物 vs 业务代码)
  - `Assets/GameScripts/HotFix/GameLogic/UI/*` 不动(PlayerPrefs 读写 / Player 模块持有 / PlayerNameGenerator 校验 全保留)
  - `Assets/Fantasy/Scripts/*` 不动
  - `Assets/GameProto/*` 客户端侧 proto 文件 / 自动生成的客户端 stub 可更新(随服务端 proto 同步)
- **35 `accounts` 集合 schema 与 upsert 逻辑零改**:本子单只**加一步** `players` setOnInsert 到 35 RegisterOrLogin 同处理链,不动 `accounts` schema、不动 35 已有 SV1-SV12
- **既有四个全栈特性(30 / 31 / 32 / 33)业务集合 schema 与处理逻辑零改**:account 字段值语义不变(= UUID = 既有口径);所有已 PASS 的 SV 仍 PASS(SV16 验)
- **服务端段三条新 RPC**:
  - `G2C` 属性快照(随登录响应 / 独立 RPC,形态服务端段定)
  - `C2G_PropertyChangeRequest` + `G2C_PropertyChangeResponse`(消息名服务端段定)
  - `G2C_PropertyDeltaPush`(服务端主动推送,消息名服务端段定)
- **服务端进程内变更 API**:本子单只声明形态(同步/异步由服务端段定);**真正的调用方(商店/邮件/活动/任务/排行榜结算)由各业务系统刀按其各自范围接** — 本刀不接

## 服务端段交付清单(给 server-dev)

> server-dev 开工第一步 = **核 fantasy-net 现状的 35 RegisterOrLogin 处理链入口**(在 35 已落 PASS 的处理链上加 `players` setOnInsert + 快照下发两步),其它三 RPC handler 按 30/31/32 范式独立加。

### 服务端段落地清单(按 37 §3 协议契约 + §五 校验体系)

| 落点 | 行为级完成定义 |
| --- | --- |
| `players` 集合 schema | 主键 `_id`(字符串 UUID,与 `accounts._id` 同值 1:1)+ 金币余额(整数 ≥0)+ 钻石余额(整数 ≥0)+ 体力余额(整数 ≥0)+ 末次变更时间(日期时间)+ schema 版本(整数 = 1)。索引:主键天然唯一,无额外索引 |
| 35 RegisterOrLogin 处理链扩展 | 在 35 已有的 `accounts` upsert 步骤后加两步:① `players` setOnInsert(UUID + 三属性初始值 + 末次变更 = 服务端时钟 + version = 1),与 35 同事务模式,失败 → 短路登录失败;② 读 `players` 当前余额 + 随登录响应捎带 / 独立 RPC 下发属性快照(形态由服务端段定) |
| 三属性初始值配置 | 启动期读运营配置 → 校验初始值在 [0, 类型上界] 范围内 → 配置非法 → 服务端启动失败;默认值:金币 0 / 钻石 0 / 体力 5 |
| 三属性类型上界配置 | 启动期读运营配置 → 校验上界在 Int 类型安全范围内 → 配置非法 → 服务端启动失败;默认值:金币 999999999 / 钻石 999999 / 体力 5 |
| `C2G_PropertyChangeRequest` handler | ① 从会话取 account(35 已挂)→ 未挂返「未登录」错码;② 解析类型(枚举三类)→ 未知 返「类型未知」错码;③ delta 范围校验([-类型上界, +类型上界])→ 超出返「请求非法」错码;④ MongoDB `FindOneAndUpdate` 单条原子命令(条件:`_id` = UUID + `currentBalance + delta >= 0` + `currentBalance + delta <= 类型上界`,更新:`$inc: { 该属性: delta }, $set: { 末次变更 = now }`)→ 未匹配 → 按 delta 正负返「余额不足」/「上界溢出」错码 + 含当前实际余额;⑤ 匹配 → 返「成功 + 新余额」+ 起 `G2C_PropertyDeltaPush` 到该 UUID 全部在线会话;⑥ MongoDB 不可达 → 返「服务暂不可用」错码,不抛异常断连 |
| `G2C_PropertyDeltaPush` 推送 | 写库成功后服务端起,推送目标 = 该 UUID 当前在线**全部**会话(不只触发会话);携带类型 + 新余额 + reason;离线 = 推送丢弃(下次登录拉快照对齐);失败 = 不重试 |
| 服务端进程内变更 API | 入参 UUID + 类型 + delta + reason;**与 PropertyChangeRequest 共用同一套校验 + 写库 + 推送**(非两套独立实现);返回结果码 + 成功时新余额;成功 → 起推送到该 UUID 全部在线会话 |
| 客户端绝对值禁报 | handler **仅消费**类型 + delta + reason 三字段;**不解析**绝对余额字段(即使 proto schema 误带);Code Review 必拦 |

### server-dev 须自行据工程现状定的实现选择

- 三条新 RPC 的消息名 / 字段代码名(以 fantasy-net 既有 30 / 31 / 32 / 35 RPC 命名约定为准)
- `players` 集合实际名 / 字段命名(以 Fantasy.Net + MongoDB BSON 命名约定为准)
- 属性快照下发形态(登录响应捎带 vs 独立 RPC)— 据 fantasy-net 既有登录响应字段结构 + RPC 注册约定选择
- 服务端进程内变更 API 形态(同步 / 异步)— 据 fantasy-net 既有内部 API 约定取
- 35 处理链 `players` setOnInsert 的挂钩点位置 — server-dev 据 35 已 PASS 落地的处理链结构定(plan code-blind 不指字面量)
- 启动期配置校验时机 — server-dev 据 Fantasy.Net 启动流程定
- `FindOneAndUpdate` 的 MongoDB C# Driver 具体 API(BuildersFilter / UpdateDefinition / FindOneAndUpdateOptions 等) — server-dev 据现有 30/31/32/35 MongoDB 调用风格定
- 推送到 UUID 全部在线会话的查询机制 — server-dev 据 Fantasy.Net 既有会话注册 / 查询 API 定(typical:Scene.GetComponent<GateSessionRegister> 类查找)

## 验收标准(server-test 逐条核;完成定义 = 行为可观测)

参见 37 §七 全部验收点(SV1-SV17)。主验:
- **SV1 编译 + 源生成器产物**
- **SV3 首登初始化 + 1:1 关联**(MongoDB `accounts` + `players` 各一条新记录,`_id` 同值)
- **SV5 重登余额不变**(setOnInsert 在 update 路径不执行)
- **SV6 / SV7 增加 / 消费成功路径**(通用变更入口 + 推送)
- **SV8 / SV9 余额不足 / 上界溢出**(失败响应含当前余额)
- **SV11 并发同账号双扣原子(反作弊核心)**(初始 100 并发各扣 80 = 总扣 80 不超发)
- **SV12 服务端进程内 API**(与 PropertyChangeRequest 同行为)
- **SV15 客户端业务代码零 diff**(Assets/GameScripts/HotFix 为空)
- **SV16 既有四特性 + 35 零回归**
- **SV17 Code Review**(单条原子 / 仅消费类型+delta+reason / setOnInsert 严格分 / 共用校验 / 推送到全部在线会话)

辅验:**SV2 schema**、**SV4 快照下发**、**SV10 类型未知 / delta 极值**、**SV13 MongoDB 不可达失败**、**SV14 重启持久**

### BLOCKED 边界

- 本机 MongoDB(`D:\mongodb-portable`)不可达 / Fantasy 服务端起不来 → 真往返写库类 SV(SV3 / SV5 / SV6 / SV7 / SV8 / SV9 / SV11 / SV12 / SV14)判 **BLOCKED 非 FAIL**(memory `local-mongodb-for-server-roundtrip` + server-test memory `feedback-blocked-vs-fail`)
- 编译 / Code Review(SV1 / SV17)、客户端工程零 diff(SV15)、零回归(SV16)照常验

## 已拍板决策(decisions,自治默认推进,要改另开增量)

- **设计稿 = 新建 37-player-attr-server.md**(`nav.js` GROUPS 已注册「全栈 / 服务端」组,放在 36 之前);设计 14 / 16 / 18 / 19 / 20 / 21 / 22 / 30 / 31 / 32 / 33 / 35 / 36 **零改写**(本子单与之均正交或沿用既有契约,见 37 §六关系表)
- **D1 独立 `players` 集合**:不合并到 35 `accounts`(职责单一 + 更新频率差 + 未来扩展);plan 自决取此安全默认(boss brief 留 plan 拍板「a) ... 默认独立 players _id=account UUID 沿 35」)
- **D2 单一通用变更入口** `C2G_PropertyChangeRequest`(类型 + delta + reason):boss brief 给的默认,plan 沿用 + §一切分表「为何不细分三 RPC」论证
- **D3 登录拉一次快照 + 服务端主动推送 `G2C_PropertyDeltaPush`**:boss brief 给的默认;形态(响应捎带 vs 独立 RPC)留 server-dev O4
- **D4 校验红线**:余额下界(不许负) + 类型上界(运营配置) + 原子 `FindOneAndUpdate`;boss brief 给的默认
- **D5 首登 35 钩子内 setOnInsert**(与 35 同事务模式):boss brief 给的默认
- **D6 首登初始值**(金币 0 / 钻石 0 / 体力 5,运营配置可改;**不读** PlayerPrefs 老值):plan 设计自治拍板;客户端段下一刀迁移时玩家「老 PlayerPrefs 数据丢失」是服务端权威基线下的必然取舍——任何「读 PlayerPrefs 老值带入服务端」都给玩家提供了「改 PlayerPrefs 给自己加值」的反作弊缺口,故首登 = 服务端配置默认为真(`PlayerPrefs.HasKey` 在生产线上玩家本来就极少积累很大的钻石/金币,Tier 2 上线的「玩家无感」体验由「服务端配置初始值合理 + 必要时运营补偿邮件」保证)
- **D7 服务端进程内变更 API 与 PropertyChangeRequest 共用一套校验**:plan 设计自治拍板;不另造发奖路径,反作弊红线统一,业务系统无法因「内部调用」绕过校验
- **D8 客户端绝对值禁报**(handler 仅消费类型 + delta + reason):boss brief 红线落实
- **D9 反作弊缺口路径**(`PlayerNameGenerator.cs:152` 钻石校验恒返 true):本刀**不动客户端**,由「服务端校验」一侧合上;客户端段下一刀让钻石消费走 PropertyChangeRequest,缺口彻底关闭。不走「修复客户端校验」治标路径(客户端任何校验都可被绕过,治标不治本) — plan 设计自治拍板
- **D10 三条新 RPC + 内部 API 形态由 server-dev 据 fantasy-net 现状定**:plan code-blind,只给行为语义
- **D11 推送丢失不重试**:绝对快照覆盖式,丢失 = 下次登录拉快照对齐;Tier 3 强一致再加
- **D12 ledger 不做**(本子单只持「末次变更时间」单字段);Tier 2+ 后续刀
- **D13 既有四特性 + 35 零回归**:account 字段语义不变,业务集合 schema 不动
- **D14 服务端段三条新 RPC + 服务端进程内 API + `players` 集合 + 校验 + 推送 = 本子单完整范围**;商店 / 邮件 / 活动 / 任务 / 排行榜结算的真实接入 = 各业务系统刀(后续),本子单不接

## 影响半径(本次改动 + 同步落点)

- **设计稿**:新建 `design-docs/37-player-attr-server.md`(`nav.js` GROUPS 已注册;首页卡片 / 侧边栏自动同步)
- **既有设计稿同步**:**14 / 16 / 18 / 19 / 20 / 21 / 22 / 30 / 31 / 32 / 33 / 35 / 36 零改写**(均正交或沿用既有契约,见 37 §六关系表;本子单加法式不破坏既有现状)
- **代码**(server-dev):
  - 服务端新增三条 RPC 协议(proto / 自动生成 stub 客户端服务端同步)
  - 服务端新增 `players` 集合 schema 与 setOnInsert(35 处理链扩展)
  - 服务端新增三条 RPC handler + 服务端进程内变更 API + 共用校验 / 写库 / 推送
  - 服务端启动期配置校验(初始值 + 上界)
- **不同步**(明令不改):客户端 Unity 工程业务代码 `Assets/GameScripts/HotFix/*`、`Assets/Fantasy/Scripts/*`、客户端 PlayerPrefs key 与读写、Player 模块持有方式、PlayerNameGenerator 校验函数(全保留,客户端段下一刀做)
- **协议生成物允许更新**:`Assets/GameProto/*` 客户端侧 proto stub 因新增三条 RPC 会更新,这是协议生成物的自然更新(沿 30/31/32 范式),业务代码零 diff 仍守

## 自检(plan 收尾必做,conventions §「收尾必做」)

- [x] 过程性内容不在正文(37 设计稿无 diff 叙事 / 无「按你说的改成」/ 无对话痕迹;§读前必看 + §立项 + §一-§九 全是事实陈述与设计契约)
- [x] 正文无可推导事实的副本(本子单是新建稿,涉及「现状客户端 PlayerPrefs / Player 模块 / PlayerNameGenerator.cs:152」的描述仅作 boss brief 给定的反作弊缺口陈述与下一刀边界声明,**不写代码符号字面量**(`PlayerNameGenerator.cs:152` 是 boss brief 字面引用,作「缺口标识」用,不作设计基线;符合 conventions design-docs §3 「主题即代码」例外:本刀是「服务端反作弊地基」类全栈,需引用客户端缺口位置作下一刀边界,且仅出现于「读前必看」+「§3.6 协议字段语义升级表」+「§5.8 诚实边界」+「§九风险表」+「关联文档」五处作标识,**正文规范本体不含**)
- [x] 正文无拟人 / 口语比喻(grep 「死/打死/收口/钉死」:无;「合上缺口」「铺地基」用了几次 — 检视:「合上 / 铺地基」是工程常用名词非比喻,且 35 / 36 也用过同表述,留)
- [x] 工作态内容可识别所属任务(本 state 文件头部明标「Tier 2 玩家属性权威 · 服务端段 · 第 1 子单(金币 / 钻石 / 体力 三属性搬服务端)」)
- [x] 被改动规范的旁注仍成立(本子单零改既有规范,无旁注同步问题)
- [x] 过时正文已重写或删除(本子单零改既有,无勘误注问题;未引发其它设计稿过时——本刀是加法式新建,客户端 Player 模块 + PlayerPrefs 在客户端段下一刀才动)
- [x] design-docs 正文 code-free 大体守住(grep `[A-Z][a-zA-Z]+\.[A-Z]` 类符号在正文规范本体未出现;`PlayerNameGenerator.cs:152` 仅作「反作弊缺口标识」出现于五处边界节,呼应 boss brief 给定的现状反作弊缺口,作下一刀边界声明,符合 conventions design-docs 例外「现状审计 + 接线点」类设计稿可指具体落点;沿同 35 设计稿「demo Account 实体 + GateAccountFlagComponent」具体路径作锚的口径)

## 自治审计(本环节自主拍板的取舍,简记)

- **boss brief 全部 plan 自决项已据安全默认推进**(D1 独立 players / D6 首登配置初始值 / D7 共用校验 / D9 反作弊缺口处理路径 / D10 协议形态由 server-dev 定),全部记 decisions 不入 blockers
- **boss brief 给定「c) 客户端读策略...或查询型 RPC,plan 自决」明确处理**:本子单取「登录拉一次 + 主动推送」(D3,boss brief 给的默认 a-e),不取「查询型 RPC」——查询型每次操作 + 1 RTT 体验差且仍需通用入口才能反作弊(§一切分表论证),无更优解
- **boss brief 给定「e) 首登在 35 RegisterOrLogin 钩子内顺带 / 另起独立 Awake,plan 自决」处理**:本子单取「35 钩子内顺带」(D5,boss brief 给的默认 a-e),沿 35 §3.2 范式,与 35 同事务模式;不取「独立 Awake」(无 Awake 时机点能保证首登后玩家首次操作前 players 已 ready,且会分裂处理链)
- **整局走查崩法 8 类 + 诚实边界齐**:§5.1-§5.8 已落,每类均给对策;§5.8 诚实边界明示「不守 ledger 历史 / 退款 / 多服务端进程全局原子 / 业务场景语义 / PlayerNameGenerator 客户端修复 / 多会话同 UUID / 分数进度反作弊」防 test/boss 误判范围
- **范围闸**:核心档 = `players` 集合 + 三条 RPC + 校验体系 + 推送 + 进程内 API(本子单完整范围,boss brief 明列);砍掉 = 无可砍——本子单已是「服务端权威 + 通用变更 + 推送」三件事的最小可观测改动(`players` 集合不建则无账本,RPC 不全则反作弊不闭环,推送不发则多端不一致,内部 API 不声明则后续业务系统刀无接入面),所有「可砍 / 增强」档(ledger / 退款 / 多服务端进程原子 / 其它属性 / 多会话挤号 / 分数反作弊)均归 Tier 2+ / Tier 3,本子单只留核心档,范围已收敛
- **整局走查向上对体验**:本子单的「让玩家属性真值由服务端裁决」对**诚实玩家**是无感的(登录时拿到与 PlayerPrefs 时代一致的初视图、变更响应几十 ms 内、推送实时更新);对**作弊玩家**是「钻石消费 / 充值改 PlayerPrefs / hex 编辑器加值」类作弊路径一次性合上;对**工程**是为反作弊体系 + 后续 Tier ledger / 退款铺地基。三类玩家 / 工程都能答上「让什么体验更好」(诚实玩家:服务端持账本意味着「跨设备登录余额跟随」、「客户端 crash 不丢余额」、「未来 ledger 落地可查流水」;作弊玩家:作弊不再有效;工程:反作弊地基稳)
- **反作弊缺口处理路径(D9)是 plan 设计自治的关键判断**:boss brief 已声明「PlayerNameGenerator.cs:152 已被点名为反作弊核心缺口」,plan 拍板**不在本刀修复客户端校验**而是「让服务端校验上线 + 客户端段下一刀让钻石消费走服务端」。理由:① 客户端任何校验都可被绕过(`UIWindow` 反编译 / DLL 替换 / hex 编辑器修改本地变量等),修复客户端校验是治标不治本;② 服务端权威建立后,客户端校验函数的返值是否正确**不再参与权威路径**(由服务端响应决定是否扣成功);③ Tier 2 第 2 子单是「客户端段」即将到来的下一刀,在那一刀让钻石消费走 PropertyChangeRequest 是最自然的接线点,本刀不抢 — 这是「立场朝结果(项目反作弊体系稳)、不朝当下势头(即刻修复看起来积极)」的典型独立成判
