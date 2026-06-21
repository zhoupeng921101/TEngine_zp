# plan 状态

当前任务:Tier 4 活动系统 · 服务端段 · 第 1 子单(地基:活动基础架构 + 每日登录奖跑通)

## 范围与决策

- **设计稿**:新建 `design-docs/39-activity-server.md`(`nav.js` GROUPS 在「全栈 / 服务端」组 33 之前注册;首页卡片 / 侧边栏自动同步)
- **范围**:本子单 = 「活动基础架构(配置表 + 进度集合 + 周期幂等 + 登录触发 + 复用 32 发奖入口)+ 1 个最简活动实例(每日登录奖)端到端跑通」。9 套活动的其余 8 套留后续逐套刀(每套刀的工作量 ≈ 加 1 行 `activity.xlsx` 配置 + 至多 1 个新 `type` 计数器,不动地基)
- **核心档**:① `activity.xlsx` Luban 配置表(`activity_id / name_text_id / desc_text_id / type / cycle / target / reward / mail_def / start_at / end_at`,`##group=c,s` 双端同源);② MongoDB `activity_progress` 集合(每账号每活动一条记录:`_id={account}_{activityId}, account, activityId, counter, lastClaimedCycleKey, lastUpdatedAt, version`);③ 登录触发达标判定(挂钩在 35 RegisterOrLogin upsert 完成后);④ 周期幂等键(服务端时钟算本周期键,`Daily`/`Weekly`/`OneShot`/`Always` 四档,`FindOneAndUpdate` 条件原子写,同 33 §3.3 范式);⑤ 发奖编排经 32 §3.5 服务端发奖入口 `SendMailTo` 投活动结算邮件(标题 / 正文取 `mail_def` 邮件模板,附件库 id 用 `activity.reward` 覆盖,同 33 §3.2 范式);⑥ 每日登录奖活动实例(`activity_id=1, type=Login, cycle=Daily, target=1, reward=礼包库 id, mail_def=邮件模板 id`)
- **增强档**(默认取,代码量边际增加 0):架构层把 `Cumulative/Schedule/Action` 三类型的内部 API 接缝挖好(同一 `ActivityProgressService.Increment(account, activityId, delta)` 范式),但本子单**不接入触发钩子**(各类型需对应外部触发)。这样后续 8 套若需 `Cumulative` 类只在业务系统接入触发钩子,不动本子单 service 形态
- **可砍档(默认砍)**:① 9 套活动其它 8 套(boss / 产品后续定清单,O1);② `Cumulative/Schedule/Action` 类型节律实现(O3);③ 客户端活动 UI 入口 / 详情(O4,客户端段后续);④ 头像 EVENT 解锁通路(O5,Tier 4 第 2 子单,需扩 16 `UseEffect`);⑤ 直发属性类活动 `reward_type`(O6);⑥ 严格账号级幂等漏发零容忍(O7);⑦ 修 `RankPersistence.dailyClaimDateBin` 本地存档可刷(那是 33-O1 排行榜每日 / 点赞奖,职责正交,本子单不动)

## 安全默认采纳(self-determined,自治默认推进)

- **D1 配置存 Luban 不存 MongoDB 集合**:沿 22/32/33 双端同源范式(`##group=c,s` 导出);后续客户端 UI 接入活动列表 / 详情时可直接读 c 组配置而无需新协议;运营改活动数值改 xlsx 重导,不需改服务端代码。备选(存数据库)留 O6 范式(运营热改时再切)
- **D2 进度独立集合 `activity_progress` 不并入 `players`**:活动进度横向跨活动(每账号 N 条),并入 `players` 致其文档膨胀 + 每次活动判达标都写 `players` 致写竞争;独立集合让职责单一,同 35 `accounts` vs 37 `players` 分集合范式
- **D3 复合主键 `{account}_{activityId}` 字符串拼接而非两字段**:`_id` 单字段比双字段索引查询天然 O(1)(MongoDB `_id` 内置主键索引);串拼简洁、可读、定位快;同 33 `settle_records._id={rankId}_{cycleTime}` 范式
- **D4 周期键沿 33 同口径**(本周期键 = 单调时刻 ticks,服务端时钟算):`Daily`=今日 0:00 ticks / `Weekly`=本周一 0:00 ticks / `OneShot`=1 常量;原子条件写 `{ _id, lastClaimedCycleKey: {$lt: 本周期键} }` set 新键;同 33 §3.3 已建坑
- **D5 抢占在副作用之前**:claim-then-act 范式 — 先原子抢占周期键 → 再投邮件;不取反向(反向 = 先发后写,崩在中间 → 超发);接受漏发窄窗(发邮件失败,运营可补)+ 不接受超发(同 33 §四诚实取舍)
- **D6 登录触发挂钩在 35 RegisterOrLogin upsert 完成后**:沿 37 「首登 setOnInsert players」同一挂钩点形态;不动 35 既有 accounts schema、不动 37 既有 players schema;遍历 `type=Login` 活动 → 各自 `counter+1 + 判达标 + 发奖编排`
- **D7 头像 EVENT 解锁本子单不接**:`AvatarUnlockService.GrantUnlock` 是客户端进程内 API,服务端发放需走「服务端发邮件挂礼包 → 客户端领奖时礼包 `UseEffect` 调 `GrantUnlock`」通路,需扩 16 `UseEffect` 加「解锁头像 N」效果(本子单超范围,留 Tier 4 第 2 子单)
- **D8 跨设备同 UUID 双登 = 原子键阻断双收**:不解决「同 UUID 是否真同一玩家」(Tier 3 跨设备识别另开);本子单只防同一 UUID 在原子层不双收 — 这是反作弊红线的可达范围,诚实边界明示

## 守不变量(给 dev / test 的硬边界)

- **客户端工程零 diff**:本子单纯 server only;`Assets/` 下零改动;若 9 套活动需客户端 UI(活动入口 / 详情)留客户端段后续刀
- **既有六个全栈特性(30/31/32/33/35/37)+ 36/38 验收点不破**:本子单只在 Fantasy.Net 仓加新代码(`activity` 配置加载 + `activity_progress` Mongo 集合 + 登录链路挂钩点)、既有 accounts / players / mail / rank_scores / settle_records / mail_claims / redeem_records 集合零 schema 改、既有客户端 38 协议契约零改
- **发奖入口必经 32 §3.5 `IMailSourceService.SendMailTo` 不另造**:活动结算邮件复用既有发奖入口(挂 `activity.reward` 库 id),同 33 §3.2 旁注「标题正文取邮件模板,附件库 id 用名次档 reward 覆盖」同口径
- **`activity_progress` 独立集合,不混入 `players` / `accounts` / 任何既有集合**:D2 红线
- **达标触发服务端时钟,无客户端触发协议**:本子单无新客户端 RPC;沿 33 §3.4 「服务端内部触发非客户端 RPC」红线
- **周期键服务端时钟算,客户端本地时钟无关**:D4 红线;沿 33 §3.1 服务端时钟口径
- **本子单 `type=Login` 实做,`Cumulative/Schedule/Action` 留架构接缝不实做**:dev 须在 `ActivityProgressService` 内只留 `Login` 调用路径,其余三类的 `Increment(account, activityId, delta)` API 形态挖好但**不接触发钩子**(留 O3 各业务后续接)
- **AvatarUnlockService 内核零改 + RankPersistence 字段零改**:D7 红线(头像解锁通路留 Tier 4 第 2 子单)+ `RankPersistence.dailyClaimDateBin` 是 33-O1 范围(本子单不修)
- **客户端 38 PASS 不破**:Tier 2 客户端段已通过;本子单不引入新 RPC 不破

## 服务端段交付清单(给 server-dev)

> server-dev 开工第一步 = ① 核 fantasy-net 工程 35 RegisterOrLogin upsert 挂钩位置 + 37 首登 setOnInsert players 挂钩范式(本子单的登录触发挂钩沿同一形态);② 核 32 服务端进程内 `SendMailTo` 入口实际签名;③ 核 Luban 服务端导出 `##group=c,s` 加载约定(同 22 rank.xlsx / 32 mail.xlsx)。本稿沿现状 code-blind 不指字面量。

### 服务端段落地清单(按 39 §三 + §3.6 + §八)

| 落点 | 行为级完成定义 |
| --- | --- |
| 新建 Luban 配置表 `Configs/GameConfig/Datas/activity.xlsx` | 字段集见 39 §3.1;`##group=c,s` 双端导出;首行 = 每日登录奖示例(`activity_id=1, type=Login, cycle=Daily, target=1, reward=既有 giftrandom 行 id, mail_def=既有 mail.xlsx 行 id, start_at=0, end_at=0`)。`type / cycle` 枚举档先在 `__enums__.xlsx` 加;`reward / mail_def` 引既有库,若需新建对应行同步加 |
| 新建 MongoDB `activity_progress` 集合 schema(Fantasy.Net 持久化层) | 字段集见 39 §3.2;`_id=string` 复合主键 `{account}_{activityId}`;`account / activityId / counter / lastClaimedCycleKey / lastUpdatedAt / version` 字段;首登时不强制写空记录(`Login` 触发时若读为空则 upsert),与 37 首登 setOnInsert 不同 |
| 新建 `ActivityProgressService`(服务端进程内,名字 server-dev 定) | 提供:① `Increment(account, activityId, delta)` 内部 API(`Login` 触发调 delta=1;`Cumulative` 类型留待业务接入);② `EvaluateAndClaim(account, activityId)` — 读 progress / 算本周期键 / 判达标 + 周期未发 / 原子 `FindOneAndUpdate` 条件写抢占 → 抢占成功调 32 `SendMailTo`;③ `Initialize(activities)` 加载 Luban activity 表 + 缓存(冷启动一次)|
| 35 RegisterOrLogin upsert 完成后挂钩 | 在登录链路 upsert accounts + 37 setOnInsert players 完成后(plan 不指代码位置,server-dev 据 fantasy-net 现状定),调 `ActivityProgressService` 遍历 `type=Login` 活动 → 各自 `Increment(account, activityId, 1) → EvaluateAndClaim(account, activityId)`;同 37 「首登 setOnInsert」挂钩范式 |
| 发奖编排经 32 §3.5 SendMailTo | 抢占成功后调 32 服务端进程内 `SendMailTo(account, mail_def, reward_override=activity.reward)`;`mail_def=0` → 占位文案(110806/110805 沿 33 §3.5);`reward=0` → 仅记已发不投邮件;不直接写 `mails` 集合(否则绕过 32 防重领 / 抽奖) |
| 周期键算法 | 沿 39 §3.3:`Daily` = 当前服务端时钟所在自然日 0:00 ticks;`Weekly` = 当前所在自然周(周一 0:00)ticks;`OneShot` = 1 常量;服务端时区取 fantasy-net 既有约定(同 33 §3.1) |
| 服务端时钟 | 同 33 / 32 服务端时钟约定,server-dev 应有可注入的时间源(测试模拟跨日界用) |

### server-dev 须自行据工程现状定的实现选择

- 35 RegisterOrLogin upsert 完成后的具体挂钩点(沿 37 setOnInsert players 挂钩范式;若是 attribute-based 自动注册则放对应 namespace,若是手动注册则在登录响应 handler 末尾加)
- Luban `##group=c,s` 服务端加载方式(沿 22 rank.xlsx / 32 mail.xlsx 服务端段落地的加载方式)
- `__enums__.xlsx` 加 `ActivityType` / `ActivityCycle` 枚举的位置(沿既有 enum 定义范式)
- MongoDB 集合命名(本稿用 `activity_progress` 描述,具体名按 fantasy-net 集合命名约定取,沿 30-37 已建集合范式如 `redeem_records / rank_scores / mail_claims / settle_records / accounts / players`)
- 时间源注入方式(沿 33 服务端段已建的时间注入接缝)
- 周期键单位:本稿用 ticks(.NET DateTime.Ticks 100ns 精度),server-dev 若用 unix ms 也可,只要单调可比就行;序列化字段 `long`
- `Initialize` 调用时机:服务端冷启动加载 Luban 表的标准时机(沿 30/31/32/33 同一冷启动加载范式)

## 验收标准(test 逐条核)

参见 39 §八全部验收点(SV1-SV12 + CV1 + E1-E3)。主验:
- **SV1 `activity.xlsx` 同源导出** + 每日登录奖示例行存在
- **SV2 `activity_progress` 集合 schema** 字段集对
- **SV3 登录触发达标判** 沿 35 upsert 链挂钩工作
- **SV4 首次跨日 + 同日重发** = 首次发 + 同日跳过(玩家不双收)
- **SV5 跨日重发** = 模拟服务端时钟 +24h 后再领可领
- **SV6 原子并发幂等** = `FindOneAndUpdate` 条件写仅一次 matchedCount=1
- **SV7 跨会话 / 重启幂等** = 重启后同日跳过 / 跨日正常
- **SV8 `mail_def` / `reward` 边界** = 兜底文案投出仍挂奖 / `reward=0` 仅记已发不投邮件
- **SV9 真往返** = `activity_progress` 写入 + 邮件落 `mails` + 32 客户端拉列表 / 领取得奖
- **SV10 服务端时钟独立** = 周期键算法严格用服务端时钟,客户端本地时钟无关
- **SV11 既有六全栈零回归** = 30/31/32/33/35/37/38 全部 PASS
- **SV12 Code Review** = 周期幂等键原子 / 发奖必经 32 §3.5 / `activity_progress` 独立集合 / 登录触发挂钩在 35 upsert 完成后 / 服务端时钟算周期键 / `type=Login` 实做其余三类留架构接缝不实做(逐条核)
- **CV1 客户端工程零 diff** = `Assets/` 下零改动
- **E1-E3 真往返联调** = 与 32 客户端段(已落地)拉邮件 / 领奖链路自然形成联调

### BLOCKED 边界

- 本机 MongoDB / Fantasy 服务端不可达 → SV9 / E1-E3 判 **BLOCKED 非 FAIL**(沿 31/32/33/35/37 口径 + memory `local-mongodb-for-server-roundtrip` + server-test memory `feedback-blocked-vs-fail`)
- 编译 + Code Review(SV1-SV2 + SV10-SV12)+ Luban 导出产物核对(SV1-SV2)+ 行为单测(SV3-SV8)照常验
- Luban 工具链不可达列 **BLOCKED 非 FAIL**(沿 numeric-system 先例)

## 已拍板决策(decisions,自治默认推进,要改另开增量)

- **设计稿 = 新建 39-activity-server.md**(`nav.js` GROUPS 已注册「全栈 / 服务端」组,放在 33 之前)
- **D1-D8 全部据安全默认推进**(详见 §安全默认采纳节);全部记 decisions,不入 blockers
- **简报描述按现状重解读 + 不入 taskFlaw**:`AvatarUnlockService.GrantUnlock` 是已设计好的活动发放接缝(等待对接、非「钩子恒返未解锁的缺口」),且本子单接入它需扩 16 `UseEffect`(超本子单范围),故本子单不接、留 Tier 4 第 2 子单——这不是简报基线错(定性偏差但实质目的「让活动能解锁头像」明确,只是需多刀完成);`RankPersistence.dailyClaimDateBin` 本地判可刷描述正确,但那是 33-O1 排行榜每日 / 点赞奖范畴,与活动系统正交,本子单不动
- **9 套活动具体清单本子单不锁定**:O1 留 boss / 产品后续刀逐套定;本子单按通用 `type` 抽象架构使任何类型可接入,具体 9 套是哪 9 套不属本子单决策
- **每日登录奖示例数值占位**:`reward=礼包库 id`、`mail_def=邮件模板 id` 在交接区写占位描述(具体 id 由 server-dev 据 `giftrandom.xlsx` / `mail.xlsx` 既有行取或新建),不在本设计稿钉死特定数值——9 套活动各自数值由产品定,本子单只跑通架构
- **配置存 Luban 不存 MongoDB**(D1):运营改活动数值改 xlsx 重导,不需改服务端代码;运营热改若需则切 O6 范式(本子单不取)

## 影响半径(本次改动 + 同步落点)

- **设计稿**:
  - 新建 `design-docs/39-activity-server.md`(`nav.js` GROUPS 已注册;首页卡片 / 侧边栏自动同步)
  - **不同步改既有 design-docs**:本特性是 Tier 4 第 1 子单全新地基,**与 18 / 22 / 32 / 33 / 35 / 37 / 38 全部正交**;§六 与既有特性的关系表已列(全标「零改动」)。具体核实:
    - 18(头像 EVENT 解锁):本子单不调 `GrantUnlock`,18 §3.5 解锁条件 / §3.6 三态语义无变化,无须改写
    - 22(排行榜每日 / 点赞奖):那是排行榜专属机制,与通用活动正交;`dailyClaimDateBin` 留 33-O1,本特性不动
    - 32(发奖入口):本特性复用 32 §3.5,32 设计稿描述「调用方负责结算幂等」恰是本子单实现,无需改写 32(本子单是其调用方之一,沿 33 同地位)
    - 33(排行榜结算):同范式但非同代码,33 设计无变化
    - 35(账号):登录挂钩点是「在 RegisterOrLogin upsert 完成后加挂钩」,35 设计「§六 留接口余量声明」已说后续业务系统可在该点加钩,本特性恰是其用例,无需改 35
    - 37(玩家属性):正交;本子单 `reward` 走礼包(经 32 邮件)不直发属性,后续若加直发属性(O6)再说
    - 38(客户端属性):正交;本子单零客户端 diff
- **代码**(server-dev):
  - 服务端新增:`activity` 配置加载 + `activity_progress` Mongo 集合 + `ActivityProgressService`(`Increment` / `EvaluateAndClaim` / `Initialize`)+ 登录链路挂钩点
  - 协议生成物:**零新增协议**(本子单服务端内部触发,无客户端 RPC)
  - Luban 配置:`Configs/GameConfig/Datas/activity.xlsx` 新建 + `__enums__.xlsx` 加 `ActivityType` / `ActivityCycle` 枚举档
- **不同步**(明令不改):
  - 客户端工程任何代码(本子单零 client diff)
  - 30/31/32/33/35/36/37/38 既有 design / proto / handler / 集合 schema
  - `AvatarUnlockService.cs` 内核(D7 留 Tier 4 第 2 子单)
  - `RankPersistence.cs` 字段(33-O1 后续刀)

## 自检(plan 收尾必做,conventions §「收尾必做」)

- [x] **过程性内容不在正文**:39 设计稿无 diff 叙事 / 无「按你说的改成」/ 无对话痕迹;§读前必看 + §立项 + §一-§九 全是事实陈述与设计契约;§二现状审计是基线证据陈述非过程
- [x] **正文无可推导事实的副本**:本子单引用 `AvatarUnlockService.cs` 全文件 94 行 / `RankPersistence.cs.dailyClaimDateBin` 字段是**现状审计接线点**类(同 38 「PlayerInfoWindow.cs:152 接线点」例外:Tier 收口 / 接缝定义型 design-docs 允许引接线点位置作 dev 落地锚);设计稿正文规范本体的代码符号仅为本设计稿声明的新概念(`ActivityProgressService / Increment / EvaluateAndClaim`,沿 35/37 范式)+ 既有引用(`SendMailTo / GrantUnlock / ChangeProperty / RegisterOrLogin` 在关系表作锚)
- [x] **正文无拟人 / 口语比喻**:grep 「钉死 / 打死 / 焊死 / 绑死 / 死在」零命中
- [x] **工作态内容可识别所属任务**:本 state 文件头部明标「Tier 4 服务端段 · 第 1 子单」+ 9 套活动延后明示
- [x] **被改动规范的旁注仍成立**:本子单**不动既有设计稿**(全部正交,§六 关系表全标零改动);无需同步他篇,无孤儿理由
- [x] **过时正文已重写或删除**:本子单是新建,无既有过时叙事处理
- [x] **design-docs 正文 code-free 大体守住**:39 正文规范本体的代码符号仅为本设计稿声明的新概念 + 现状审计接线点(在 §二 + §六作 dev 落地锚)+ 既有引用(在 §六 / §相关文档作锚);**正文规范本体无随意符号点缀**,与 30-37 既有全栈稿同口径
- [x] **设计稿章节骨架完整**:立项信息表(类型 / 方向约束 / 范围 + 需求降层 a/b/c)→ 现状审计(§二)→ 改什么与为什么(贯穿 §一-§三)→ 方案正文(§三 + §3.6 时序图)→ 整局走查 + 崩法 6 类(§五)→ 验收点(§八 SV / CV / E)→ 待拍板清单(§七 O1-O7)→ 风险表(§九)+ 每个功能点标核心 / 增强 / 可砍三档(§七末 + §一-§三正文)
- [x] **整局走查 6 类崩法齐**(§五:并发 / 中途存档 / 恶意利用 客户端伪造 / 恶意利用 客户端改时钟 / 跨设备双登 / 配置错指)
- [x] **范围闸**:每个功能点已标核心 / 增强 / 可砍 三档;砍掉所有可砍档(O1/O3/O4/O5/O6/O7)后核心循环「登录 → 服务端判达标 → 投活动邮件 → 玩家次日邮箱领奖」仍成立 = 范围已收敛

## 自治审计(本环节自主拍板的取舍,简记)

- **简报现状对照处置**:不停机走 boss 重派 — 因 4 处简报描述 1 处定性偏差(AvatarUnlockService 是已建接缝非缺口,但实质目的「让活动能解锁头像」明确)+ 1 处归错系统(`RankPersistence` 字段是 33-O1 范畴非活动系统)+ 2 处其他描述(目的明确不影响推进)= 调查后据证拍板的第 ② 类:取默认走 architecture + 1 实例,不入 blockers
- **范围闸**:核心档 = 活动配置表 + 进度集合 + 登录触发 + 周期幂等 + 复用 32 发奖 + 每日登录奖实例(本子单完整范围,「地基 + 1 实例」收口);增强档 = 三类型(`Cumulative/Schedule/Action`)的内部 API 接缝挖好但不接入(代码量边际增加 ≈ 0,只是 service 形态预留方法);可砍档(全部砍)= 9 套清单 + 三类型节律实现 + 客户端 UI + 头像 EVENT 解锁通路 + 直发属性 + 严格幂等 + 修 `RankPersistence` 字段;砍后核心循环仍成立
- **整局走查崩法 6 类齐**(§五:并发 / 中途存档 / 恶意利用 / 服务端 vs 客户端时钟 / 跨设备 / 配置错);§诚实边界(§五末「承认的固有限制」)明示守 4 项 + 不守 7 项防 test/boss 误判范围
- **整局走查向上对体验**:① 玩家:每日登录有奖(沿运营常用激励手段)+ 跨设备权威进度(本机改存档无意义);② 运营:统一框架运营 9 套活动,加新活动成本 ≈ 配置一行 + 至多一类新计数器,降低活动上下线成本;③ 工程:为 Tier 4 全部活动铺地基,后续 8 套 + N 套未来活动直接复用,避免每套各自实现;④ 反作弊:登录类计数由服务端记不可被客户端伪造,周期键服务端时钟客户端改时钟无关。四类都答得上「让什么体验 / 工程目标更好」
- **不停机走 boss 重派的理由**:auto 模式 + 简报目的清晰(铺活动地基 + 跑通 1 实例,验证架构)+ 现状调查后可据证拍板 = 第 ② 类「无明显默认 → 调查取证后据证定最优解,记 decisions 不入 blockers」;blockers 仅留给第 ③ 类(调查也定不了且不可逆且抵触 GDD 原文 — 本子单**不抵触 GDD**:GDD 主篇虽未列「活动系统」章节,但「9 套活动」是产品 backlog 范畴,与 GDD 经典玩法机制正交,且服务端框架自然扩展)
- **AvatarUnlockService 处置**:本子单不直接接 — D7 决策。理由:① `GrantUnlock` 在客户端进程内,服务端不可直调;② 服务端发头像解锁需扩 16 `UseEffect`(超本子单范围);③ 本子单先把活动→发邮件这一段通,头像解锁是「邮件领奖落地」的下游能力(Tier 4 第 2 子单);④ 这是「向上对体验 = 让活动能解锁头像」的两阶段实现,不在第一阶段强行打通(否则把 16 道具系统的扩展塞进活动子单,违分层)
- **`RankPersistence.dailyClaimDateBin` 处置**:本子单不动 — `RankPersistence` 是排行榜客户端段本地存档,字段 `dailyClaimDateBin` 服务于设计 22 §3.4.2 每日奖(排行榜专属机制),客户端段本地判可刷确实是缺口但已留 [33-O1](#33-rank-settle-server::open) 后续刀。Tier 4 通用活动系统**从一开始**用服务端权威(`activity_progress` 集合 + 服务端时钟跨天判)避免重蹈,故本特性的 `Daily` 周期机制是「正确实现」、22 §3.4.2 的待修是「另一刀的任务」,两件事正交
