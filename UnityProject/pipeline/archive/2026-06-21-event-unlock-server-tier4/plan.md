# plan 状态

当前任务:Tier 4 活动系统 · 服务端段 · 第 2 子单(EVENT 头像解锁通路 · server 段)

## 范围与决策

- **设计稿**:新建 `design-docs/40-event-unlock-relay.md`(`nav.js` GROUPS 在「全栈 / 服务端」组 39 之前注册;首页卡片 / 侧边栏自动同步);同步在 `16-item-system.md` §3.7 末尾加 `useeffect-event` 旁注节(EVENT 接缝声明);同步更新 `39-activity-server.md` §七 O5 状态(从「不在本子单」→「由 40 兑现」)
- **范围**:本子单 server 段 = 「配置层接通 EVENT 通路」= 4 张已有 Luban 表各加 1 行(`__enums__.xlsx` 加 `EVENT=5` 档 + `item.xlsx` 加 EVENT 解锁道具行 + `giftrandom.xlsx` 加 EVENT 礼包行 + `activity.xlsx` 加 EVENT 解锁活动行)+ `AuthoritativeDefs` 注册新道具/礼包/活动行。客户端段 UseEffect 解析 + 适配器调 `GrantUnlock` 留下一子单(独立刀)
- **核心档**:① `__enums__.xlsx` 使用效果枚举加 `EVENT=5`(沿 16 §3.7 现状 5 档 0-4 自然扩第 6 档);② `item.xlsx` 加 EVENT 解锁道具 `id=30101, type=2 材料, 使用效果=5 EVENT, 效果目标=3 头像 id avt_star, 效果数量=1, 自动使用=1, 叠放=0, 品质=4 史诗`;③ `giftrandom.xlsx` 加 EVENT 礼包 `所属礼包=6101, 奖品=30101, 数量=1, 权重=100`(单项必中);④ `activity.xlsx` 加 EVENT 解锁活动 `activity_id=2, name_text_id=390003, desc_text_id=390004, type=Login, cycle=OneShot, target=7, reward=6101, mail_def=7002, start_at=0, end_at=0`(语义:累计登录 7 次永久解锁 EVENT 头像);⑤ Fantasy.Net `AuthoritativeDefs` 注册新道具 / 礼包 / 活动行(沿 39 §3.1 同源加载范式)
- **可砍档(默认砍)**:① 客户端 UseEffect 解析 + 适配器接 `GrantUnlock`(O5,留客户端段下一刀);② 9 套 EVENT 解锁头像活动清单(O1,运营后续逐套刀);③ 跨设备已解锁集合同步(O6,Tier 3 范围);④ EVENT 头像 / 道具多语言 textId(O7,后续);⑤ EVENT 礼包多项随机抽 1(O4,本子单单项必中,避免「白给已解锁头像」);⑥ EVENT 道具进背包模式(O3,本子单立即结算,避免「玩家手动用」体验断层);⑦ EVENT 解锁活动 cycle=Weekly/Daily(O8,本子单 OneShot 永发,EVENT 是限量限定资源)

## 安全默认采纳(self-determined,自治默认推进)

- **D1 不取简报方案 A 加 `ActivityDef.EventUnlockId` 字段、不扩 32 SendMailTo 签名**:简报 ②a「ActivityDef 增 EventUnlockId 字段;reward 列表附 EVENT 类型 item」需要扩 32 SendMailTo 让 EVENT 标记跟着 reward 流到客户端,**违简报自身守不变量 ④「32/33 SendMailTo 签名不动」**。调查后取方案 B(沿 16 §3.7 UseEffect 范式扩):EVENT 就是「一个特殊使用效果(`UseEffect=5`)的道具」,经礼包随机库携带,在 39 ActivityDef 的 `reward` 字段填 EVENT 礼包 id 即可。**记 decisions 并写明依据**:① 32/33 SendMailTo 签名零改;② 39 ActivityDef schema 零字段加;③ 16 §3.7 既有 5 档使用效果(0/1/2/3/4)本就是「道具产出种类的扩展点」,EVENT 自然属第 6 档,与既有架构同源扩展;④ 客户端段下一刀的工作量(16 加 1 个 EVENT 分支 + 1 个适配器调 GrantUnlock)与方案 A 等价。本子单同源调查后据证拍板,**不入 blockers**(走「调查后据证定最优解」的第 ② 类阶梯)
- **D2 不在 39 加新字段,纯在 16/giftrandom/activity 4 张已有表加行**:沿 conventions §3 「正文无可推导事实的副本」+ 39 §四 守不变量。EVENT 通路本质 = 「39 ActivityDef.reward 指向一个 EVENT 礼包 id」,完全沿 39 §3.4 发奖编排同流程,零代码 handler 改动。**记 decisions 并写明依据**:42 Cumulative/Schedule/Action 三类节律实现留 39-O3 不动;39 §3.5 已实做的 `Login` 类节律恰好对应「累计登录」EVENT 活动
- **D3 EVENT 道具 `效果目标` 字段直接存头像/框 id 不存活动 id**:沿 16 §3.7 既有适配器范式(`货币` 档存货币 id、`图案` 档存图案标识),EVENT 档存「头像/框 id」最自然;头像 vs 框由 18 §3.5 头像表「类型」字段区分(头像=1 / 框=2),适配器查表分流写入 `UnlockedAvatarIds` vs `UnlockedFrameIds`。**反向方案**(EVENT 档存活动 id → 适配器扫头像表反查)需新建索引或扫表,违加法式
- **D4 EVENT 道具 `自动使用=1` 立即结算不进背包**:玩家邮件领取后立即解锁,不需要「进背包再点用」二次操作;沿 16 §3.7 「自动使用字段」的「立即结算 vs 进背包」二分,EVENT 类语义只能「立即结算」
- **D5 EVENT 礼包单项必中权重 100**:沿 16 §3.6 「单项奖池必中该项」;避免「随机抽到已解锁头像」白给体验;运营若想做「多项 EVENT 头像随机抽 1」沿 16 §3.6 同范式扩,本子单不取
- **D6 EVENT 解锁活动 `cycle=OneShot` 一次性永发**:EVENT 头像是限量限定资源,周期重发与「事件限定」语义矛盾;沿 39 §3.3 OneShot 周期键 = 1 常量,达标后 `lastClaimedCycleKey=1` 永远 ≥ 1 自然不重发
- **D7 设计稿独立成稿(40-event-unlock-relay.md)而非在 39 增订**:本子单跨 16/18/39 三个子系统的中继,有独立设计域;在 39 增订会让稿子继续厚,新建独立稿 + 40-event-unlock-relay 命名清晰交付 EVENT 解锁中继职责
- **D8 守 39 第 1 子单 PASS 基线(Fantasy de5d4da7 + 设计稿 f5d44f7f)**:本子单纯配置层增量,不触发任何 39 第 1 子单 PASS 时的代码 / schema / 协议 / 集合;SV9/SV11/SV12 验收点确保 39 / 30/31/32/33/35/36/37/38 零回归

## 守不变量(给 server-dev / server-test 的硬边界)

- **客户端工程零 diff**:本子单纯 server 段;`Assets/` 下零改动;客户端段 UseEffect 解析 + 适配器实做留下一子单
- **32 SendMailTo 签名零改**:EVENT 通路完全沿 39 §3.4 发奖编排同流程,SendMailTo 入参不变(`目标账号 + 发件人 textId + 标题/正文 textId + 附件库 id + 有效期`),附件库 id 仍是 `giftrandom` 库 id
- **39 ActivityDef schema 零字段加**:不加 `EventUnlockId` 等任何新字段;`activity.xlsx` 仅按现有字段集加新行(`activity_id=2`)
- **39 §3.4 发奖编排流程零代码改**:EVENT 活动自动进入既有「判达标 + 周期未发 → 原子写已发周期键 → 调 32 SendMailTo 投邮件」流程,无新分支
- **既有 16 道具表 18 字段结构零改**:本子单沿用,只加 1 行(`id=30101`);`使用效果` 字段值域从 0-4 自然扩到 0-5
- **既有 18 头像表零改**:`avt_star` 等 EVENT 头像沿 18 §3.5 现状样例,本子单 `效果目标=3` 引用现有 `id=3 avt_star` 行
- **既有四全栈(30/31/32/33)+ 35/36/37/38 + 39 PASS 不破**:本子单纯配置层增量,无新代码 / 新集合 / 新协议
- **MongoDB `activity_progress` schema 零字段加**:EVENT 活动的进度记录沿 39 §3.2 现有字段集(`_id, account, activityId, counter, lastClaimedCycleKey, lastUpdatedAt, version`)
- **`AvatarUnlockService.GrantUnlock` API 本子单 server 段不动**:客户端进程内 API,客户端段下一刀调,本子单 server 段触发不可达

## 服务端段交付清单(给 server-dev)

> server-dev 开工第一步 = ① 核 Fantasy.Net 工程 `__enums__.xlsx` 现有「使用效果」枚举的导出形态(沿 16 §3.7 既有 5 档结构);② 核 `item.xlsx` / `giftrandom.xlsx` Luban 服务端加载约定(沿 39 第 1 子单已建的 AuthoritativeDefs 加载范式);③ 核 18 头像表的 `id=3 avt_star` 行确实存在(若 Fantasy 仓尚无 18 头像表,本子单 EVENT 道具 `效果目标=3` 是「指向客户端 18 头像表的占位」,服务端无需校验头像表实存——服务端仅守「道具 id × 数量」抵达邮件,头像表是客户端段事务)。本稿沿现状 code-blind 不指字面量。

### 服务端段落地清单(按 40 §三 + §3.6 时序图 + §八 SV/E)

| 落点 | 行为级完成定义 |
| --- | --- |
| `__enums__.xlsx` 使用效果枚举加 EVENT=5 档 | 沿 16 §3.7 现状 5 档(0 无 / 1 货币 / 2 图案 / 3 自选 / 4 随机)加第 6 档 `EVENT=5`;`##group=c,s` 双端同源导出 |
| `Configs/GameConfig/Datas/item.xlsx` 加 EVENT 解锁道具行 | `id=30101, name_text_id=110101, desc_text_id=110102, type=2 材料, 使用效果=5 EVENT, 效果目标=3 (指向 18 头像表 avt_star), 效果数量=1, 图案等级=0, 参数=0, 叠放=0, 品质=4 史诗, 自动使用=1, 限时整套字段填 0`(沿 16 §3.3 样例的字段集);`##group=c,s` 双端同源 |
| `Configs/GameConfig/Datas/giftrandom.xlsx` 加 EVENT 礼包行 | `所属礼包=6101, 奖品=30101, 数量=1, 权重=100`(单项必中);`##group=c,s` 双端同源 |
| `Configs/GameConfig/Datas/activity.xlsx` 加 EVENT 解锁活动行 | `activity_id=2, name_text_id=390003, desc_text_id=390004, type=Login, cycle=OneShot, target=7, reward=6101, mail_def=7002, start_at=0, end_at=0`;`##group=c,s` 双端同源 |
| Fantasy.Net `AuthoritativeDefs` 注册新道具 / 礼包 / 活动行 | 服务端冷启动后 `AuthoritativeDefs` 缓存新道具 / 礼包 / 活动行;查询接口能按 id 取到;沿 39 第 1 子单 PASS 时已建的 AuthoritativeDefs 加载机制,无新加载点 |

### server-dev 须自行据工程现状定的实现选择

- `__enums__.xlsx` 加 `EVENT=5` 档的具体语法(`##type=enum` 行加值,沿 39 第 1 子单 PASS 时加 `ActivityType / ActivityCycle` 枚举的范式)
- `item.xlsx` / `giftrandom.xlsx` 服务端段是否已在 Fantasy 仓加载(若 Fantasy 仓尚未引入 16 道具系统的服务端导出,本子单需 server-dev 顺带补「`item.xlsx` / `giftrandom.xlsx` 加 `##group=...s` 导出」);若已加载则只需补 `AuthoritativeDefs` 注册新行
- 邮件模板 `mail_def=7002` 行可由 server-dev 据 `mail.xlsx` 既有行取或新建(标题占位「累计登录 7 天奖励」、正文占位「您累计登录 7 次,获得限定头像!」)
- `mail_def=7002` 若仍占位(等同 0),沿 39 §3.4 兜底文案投出(110806/110805)
- `AuthoritativeDefs` 注册新行的具体形态(沿 39 第 1 子单 PASS 的注册范式)

## 验收标准(test 逐条核)

参见 40 §八全部验收点(SV1-SV12 + CV1 + E1-E3)。主验:
- **SV1-SV4 Luban 导出**:`__enums__` 加 EVENT=5 档 + `item.xlsx` 30101 道具行 + `giftrandom.xlsx` 6101 礼包行 + `activity.xlsx` 2 号活动行均存在,字段值与 40 §3.2-3.5 一致;双端同源导出
- **SV5 AuthoritativeDefs 加载 EVENT 行**:服务端冷启动后查询接口能按 id 取到新道具 / 礼包 / 活动行
- **SV6 EVENT 活动经 39 §3.4 发奖编排流程**:模拟账号累计登录 7 次 → 第 7 次抢占 `lastClaimedCycleKey=1` 写入 → 投活动邮件(`mail_def=7002, reward=6101`)
- **SV7 OneShot 永发幂等**:第 8/9/... 次再登录 → `lastClaimedCycleKey=1 < 1 = false` 跳过不重发
- **SV8 真往返**(力争):起服 + 预置 4 张 Luban 表新行 + mongod 探针验 `activity_progress` 表 `activityId=2` 文档 `counter` 从 1 → 7 + `lastClaimedCycleKey=1` + `mails` 集合多一封 `mail_def=7002` 邮件附件库 id=6101 + 32 客户端拉列表见该邮件 + 领取响应「成功」+ 奖励列表 `[(道具 id=30101, 数量=1)]`
- **SV9-SV11 零回归**:30/31/32/33/35/36/37/38 + 39 第 1 子单 PASS 不破;客户端工程零 diff;`activity_progress` schema 零字段加;`SendMailTo` 签名零改;`ActivityDef` schema 零字段加
- **SV12 Code Review**:沿 40 §八列出的 6 条核(无 SendMailTo 签名改 / 无 ActivityDef 字段加 / 无新 handler/集合/协议 / EVENT 通路完全沿 39 §3.4 流程 / 16 道具表 + 18 头像表零字段改)
- **CV1 客户端工程零 diff**:`Assets/` 下零改动
- **E1-E3 联调**:server 段切片真往返(从登录 → 邮件 → 领取响应),E3「头像真正解锁」标 BLOCKED(客户端段下一刀实做后联调验)

### BLOCKED 边界

- 本机 MongoDB / Fantasy 服务端不可达 → SV8 / E1-E2 判 **BLOCKED 非 FAIL**(沿 31/32/33/35/37/39 口径 + memory `local-mongodb-for-server-roundtrip` + server-test memory `feedback-blocked-vs-fail`)
- 编译 + Code Review(SV9-SV12)+ Luban 导出产物核对(SV1-SV4)照常验
- Luban 工具链不可达列 **BLOCKED 非 FAIL**(沿 numeric-system / 39 第 1 子单先例)
- **E3 客户端段未实做时整条链不通**列 **BLOCKED 非 FAIL**(本子单 server 段固有切片状态,客户端段下一刀实做后联调验)

## 已拍板决策(decisions,自治默认推进,要改另开增量)

- **设计稿 = 新建 40-event-unlock-relay.md**(`nav.js` GROUPS 已注册「全栈 / 服务端」组,放在 39 之前;独立成稿描述跨 16/18/39 三子系统的 EVENT 中继职责)
- **同步改 16-item-system.md §3.7 末尾加 `useeffect-event` 旁注节**(EVENT 接缝声明:UseEffect=5 档 + 适配器调 GrantUnlock + 服务端段先行 / 客户端段下一刀分工);沿 conventions §6「改一处即同步被它过时的他篇」原则,本任务内同步
- **同步改 39-activity-server.md §七 O5 状态**(从「不在本子单 Tier 4 第 2 子单」更新为「由 40 兑现」+ 描述 40 兑现路径);沿 conventions §6 同步
- **D1-D8 全部据安全默认推进**(详见 §安全默认采纳节);全部记 decisions,不入 blockers
- **不取简报方案 A 加字段 + 扩 SendMailTo 签名**:经调查后据证拍板取方案 B(沿 16 §3.7 UseEffect 范式扩),理由见 D1 + 40 立项框「需求降层 c」;**这是「调查后据证定最优解」的第 ② 类**,不入 blockers
- **EVENT 解锁活动具体数值占位**:`activity_id=2` / `target=7`(累计登录 7 次)/ `reward=6101`(EVENT 礼包)/ `mail_def=7002`(占位邮件模板) — 在交接区写占位描述,具体数值由 server-dev 据 `mail.xlsx` 既有行取或新建,不在本设计稿钉死;运营后续逐套 EVENT 活动可改 `target` / `reward` / `mail_def` 数值
- **EVENT 道具 `效果目标=3` 占位指向 18 §3.5 头像表 `avt_star`**:本子单 server 段只守「道具 id × 数量」抵达邮件,头像表实存由客户端段下一刀守(客户端段读 18 头像表查找 id=3 → 类型=头像 → 调 GrantUnlock 写 UnlockedAvatarIds)

## 影响半径(本次改动 + 同步落点)

- **设计稿**:
  - **新建**:`design-docs/40-event-unlock-relay.md`(EVENT 头像解锁通路 · server 段;`nav.js` GROUPS 已注册;首页卡片 / 侧边栏自动同步)
  - **同步改写**(conventions §6「改一处即同步被它过时的他篇」,本任务内同步):
    - `design-docs/16-item-system.md` §3.7 末尾加 `useeffect-event` 旁注节(EVENT 接缝声明:UseEffect=5 档结构 + EVENT 适配器调 GrantUnlock + server 段先行 / 客户端段下一刀分工 + 「为什么不在 ActivityDef 加 EventUnlockId 字段 / 不在邮件 reward 列表附 EVENT 标记」)
    - `design-docs/39-activity-server.md` §七 O5 状态更新(从「不在本子单 Tier 4 第 2 子单」→「由 40 兑现」)
  - **不同步改**:18 / 32 / 33 / 35 / 37 / 38 / 21 / 14 全部正交;40 §六 关系表已列「零改动」
- **代码**(server-dev):
  - 服务端新增:`__enums__.xlsx` 加 EVENT 枚举档 + `item.xlsx` 加 EVENT 道具行 + `giftrandom.xlsx` 加 EVENT 礼包行 + `activity.xlsx` 加 EVENT 解锁活动行 + Fantasy.Net `AuthoritativeDefs` 注册新道具 / 礼包 / 活动行
  - 协议生成物:**零新增协议**(本子单纯配置层增量,无客户端 RPC 改)
  - Fantasy.Net handler / 集合 schema / 既有代码:**零改动**(EVENT 活动完全沿 39 §3.4 既有发奖编排流程)
- **不同步**(明令不改):
  - 客户端工程任何代码(本子单纯 server 段)
  - 30/31/32/33/35/36/37/38 既有 design / proto / handler / 集合 schema
  - 39 第 1 子单 ActivityDef schema(D2 红线)+ §3.4 发奖编排代码(D2 红线)
  - 32 SendMailTo 签名(D1 红线)
  - 16 道具表 18 字段结构(D1 红线;仅加新道具行 30101)
  - 18 头像表(D1 红线;仅引用现有 `id=3 avt_star`)
  - `AvatarUnlockService.GrantUnlock` API(本子单 server 段触发不可达)
  - MongoDB `activity_progress` schema

## 自检(plan 收尾必做,conventions §「收尾必做」)

- [x] **过程性内容不在正文**:40 设计稿无 diff 叙事 / 无「按你说的改成」/ 无对话痕迹;§读前必看 + §立项 + §一-§九 全是事实陈述与设计契约;§二现状审计是基线证据陈述非过程;16 §3.7 末尾加旁注节为接缝声明非过程
- [x] **正文无可推导事实的副本**:本子单引用 `AvatarUnlockService.GrantUnlock` / `PlayerInfo.UnlockedAvatarIds/FrameIds` / `MergeMetaSave` 仅在 §二现状审计 + §六关系表作 dev 落地锚(沿 38 「PlayerInfoWindow.cs:152 接线点」例外:Tier 收口 / 接缝定义型 design-docs 允许引接线点位置);设计稿正文规范本体的代码符号仅为本设计稿声明的新概念(EVENT 道具 30101 / EVENT 礼包 6101 / EVENT 活动 activity_id=2)+ 既有引用(`GrantUnlock / SendMailTo / UseEffect` 在关系表作锚)
- [x] **正文无拟人 / 口语比喻**:grep 「钉死 / 打死 / 焊死 / 绑死 / 死在」零命中
- [x] **工作态内容可识别所属任务**:本 state 文件头部明标「Tier 4 服务端段 · 第 2 子单 EVENT 头像解锁通路 server 段」+ 客户端段下一刀明示
- [x] **被改动规范的旁注仍成立**:本子单改 16 §3.7 加旁注节(EVENT 接缝声明,与既有 5 档无矛盾);改 39 §七 O5 (状态更新,与既有 O1/O3/O4/O6/O7 无矛盾);均无孤儿理由
- [x] **过时正文已重写或删除**:39 §七 O5 状态更新(从「不在本子单」→「由 40 兑现」)是 conventions §6 「现行体系仍有活引用 → 覆盖式重写到现状」类;不加勘误注叠旧;无其他过时正文
- [x] **design-docs 正文 code-free 大体守住**:40 正文规范本体的代码符号仅为本设计稿声明的新概念(EVENT 道具 30101 / EVENT 礼包 6101 / EVENT 活动 2)+ 现状审计接线点(`AvatarUnlockService.GrantUnlock` / `PlayerInfo.UnlockedAvatarIds` / `MergeMetaSave` 在 §二 / §六作 dev 落地锚)+ 既有引用(在 §六 / §相关文档作锚);**正文规范本体无随意符号点缀**,与 30-39 既有全栈稿同口径
- [x] **设计稿章节骨架完整**:立项信息表(类型 / 方向约束 / 范围 + 需求降层 a/b/c 含完整方案 A vs B 对比)→ 现状审计(§二)→ 改什么与为什么(贯穿 §一-§三)→ 方案正文(§三 + §3.6 时序图)→ 整局走查 + 崩法 6 类(§五)→ 验收点(§八 SV / CV / E)→ 待拍板清单(§七 O1-O8)→ 风险表(§九)+ 每个功能点标核心 / 增强 / 可砍三档(§七末 + §一-§三正文)
- [x] **整局走查崩法 6 类齐**(§五:OneShot 永发幂等 / EVENT 道具进背包 / GrantUnlock 类型错配 / 头像 id 不存在 / 跨设备双登 / 本地存档可改)
- [x] **范围闸**:每个功能点已标核心 / 增强 / 可砍三档;砍掉所有可砍档(O1/O5/O6/O7)后核心循环「累计登录 7 次 → 服务端判达标 → 投活动邮件 → 邮件领取得 EVENT 道具」仍成立 = 范围已收敛(头像真正解锁需客户端段下一刀,本子单 E3 BLOCKED 是固有切片状态)
- [x] **整局走查向上对体验**:① 玩家:累计登录 / 签到 EVENT 行为有真实跨设备权威进度 + 可解锁限定头像激励长期登录;② 运营:抓手做「累计登录 7 天送限定头像」类长期激励 + 加新 EVENT 活动成本 ≈ 4 张表加新行(无新代码);③ 工程:整条链复用 4 个子系统(39/32/16/18),不引入新协议、不引入新集合,扩展面积最小;④ 反作弊:登录类计数由服务端记不可被客户端伪造,周期键服务端时钟客户端改时钟无关;本地存档 `UnlockedAvatarIds` 可改但跨设备(同 UUID)服务端权威 EVENT 邮件仍在(承认 Tier 3 跨设备同步缺口)。四类都答得上「让什么体验 / 工程目标更好」

## 自治审计(本环节自主拍板的取舍,简记)

- **简报方案 A 处置**:不停机走 boss 重派 — 因简报方案 A(加 ActivityDef.EventUnlockId 字段 + 扩 SendMailTo 签名)**违简报自身守不变量 ④「32/33 SendMailTo 签名不动」**,且与 16 §3.7 既有 UseEffect 范式正交扩展点冲突,**取证据后据证拍板**取方案 B(沿 16 §3.7 UseEffect 范式扩);**调查后据证定最优解 = 第 ② 类阶梯**,记 decisions 不入 blockers
- **范围闸**:核心档 = 4 张 Luban 表加行 + AuthoritativeDefs 注册 + 1 个 EVENT 解锁活动实例(本子单完整范围,「server 段配置层接通」收口);增强档 = 无(本子单纯配置增量);可砍档(全部砍)= 客户端 UseEffect 解析 + 适配器(O5)+ 9 套 EVENT 头像活动清单(O1)+ 跨设备同步(O6)+ 多语言(O7)+ 多项 EVENT 礼包(O4)+ EVENT 道具进背包(O3)+ Weekly/Daily EVENT 活动(O8);砍后核心循环仍成立
- **整局走查崩法 6 类齐**(§五:OneShot 永发幂等 / EVENT 道具进背包 / GrantUnlock 类型错配 / 头像 id 不存在 / 跨设备双登 / 本地存档可改);§诚实边界(§五末「承认的固有限制」)明示守 4 项 + 不守 6 项防 test/boss 误判范围
- **整局走查向上对体验**:四类(玩家 / 运营 / 工程 / 反作弊)都答得上(详见上节)
- **不停机走 boss 重派的理由**:auto 模式 + 简报目的清晰(EVENT 头像解锁通路接通,验证整条链可达可观测)+ 现状调查后可据证拍板(简报方案 A vs 方案 B 取舍有可论证依据)= 第 ② 类「无明显默认 → 调查取证后据证定最优解,记 decisions 不入 blockers」;blockers 仅留给第 ③ 类(调查也定不了 + 不可逆 + 抵触 GDD 原文 — 本子单**不抵触 GDD**:GDD 主篇虽未列「EVENT 头像活动」章节,但「事件限定头像」是 18 §3.5 已样例的 `avt_star=活动发放` 档,与 GDD 经典玩法机制正交)
- **设计稿独立成稿(40 vs 39 增订)**:取 40 独立成稿。理由:① 本子单跨 16/18/39 三个子系统的中继职责,有独立设计域;② 39 第 1 子单稿子已厚 300+ 行,在 39 增订会让稿子继续厚且喧宾夺主;③ 40 命名清晰交付 EVENT 解锁中继职责;④ conventions §6 「改一处即同步被它过时的他篇」由本任务内同步改 39 §七 O5 状态兑现
- **EVENT 道具 `自动使用=1` 立即结算 vs 进背包(D4)**:取立即结算。理由:① 邮件领取后立即解锁体验直观;② 沿 16 §3.7 「自动使用字段」二分,EVENT 类语义只能「立即结算」(进背包后玩家手动用 = 二次操作 + 背包未实装 UI 需投放);③ 客户端段下一刀的适配器接 GrantUnlock,沿 16 §3.7 「立即结算」分支自然落地
- **不动 18 头像表(D3)**:本子单 server 段 EVENT 道具 `效果目标=3` 占位指向 18 §3.5 头像表 `id=3 avt_star`;头像表实存由客户端段下一刀守(读 18 头像表查找 → 类型=头像 → 调 GrantUnlock 写 UnlockedAvatarIds);本子单 server 段仅守「道具 id × 数量」抵达邮件
