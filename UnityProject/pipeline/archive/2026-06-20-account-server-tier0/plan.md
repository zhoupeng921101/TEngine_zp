# plan 状态

当前任务:Tier 0 真实账号体系 · 服务端段 · 第 1 子单(注册 + 登录会话)

## 范围与决策(本子单 = 地基,自治默认推进)

- **设计稿**:新建 `design-docs/35-account-server.md`(`nav.js` GROUPS 已预注册 `35-account-server.html` + 详细 desc,本任务正稿据 desc 设计意图 + 30/31/32/33 范式落成;无需改 nav.js)
- **范围**:严守 brief 「注册 + 登录会话」两件事;**不做** OpenID / 邮箱 / 密码 / 跨设备恢复(Tier 3)、登出协议 / 客户端 LoginUI 接线(Tier 0 第 2 子单)、运营踢号 / 封号(后续运营后台刀)
- **安全默认采纳**:**设备 UUID 自动注册式账号**(account = 客户端 PlayerPrefs UUID,服务端按 UUID 自动 upsert);**无新客户端可见 RPC**(复用既有 `C2G_LoginGameRequest` / `G2C_LoginGameResponse`,只是处理语义升级:认证 = 注册 + 登录二合一);**客户端工程零 diff**;**复用既有 demo Account 实体 + GateAccountFlagComponent 链路**,本子单只新建 MongoDB accounts 集合持久层 + LoginGameHandler 加 upsert 钩子
- **既有四个全栈特性零迁移、零数据兼容性问题**:account = UUID 字符串语义不变,30/31/32/33 业务集合 account 字段 schema 不动
- **accounts 集合 schema**(字段集,实际字段名以 fantasy-net Fantasy.Net + MongoDB BSON 约定为准):主键 `_id` = UUID 字符串、首次注册时间(日期时间)、末次登录时间(日期时间)、状态(整型枚举,默认 0 = 正常;本子单不消费、不误改,Tier 1+ 运营后台再消费)
- **upsert 必须原子**:单条 `findOneAndUpdate` + `upsert=true` + `$setOnInsert`(首次注册时间)+ `$set`(末次登录时间),**不**先查后写两步(防并发同 UUID 双登)
- **upsert 失败硬约束**:MongoDB 不可达 / 写失败 → 返登录失败结果码 / 短路后续 / **不挂会话身份**(沿用 30 「服务不可用不本地放行」)

## 守不变量(给 server-dev / server-test 的硬边界)

- **不动客户端工程**:Unity 工程(`Assets/`)git diff 须为空;**包括** `Assets/GameProto/` 协议生成物——本子单无新协议、无 proto 改动
- **不动既有四个全栈特性**(30 / 31 / 32 / 33):account 语义不变、业务集合 schema 不变;既有 PASS 验收点须仍 PASS(零回归)
- **不动既有 demo Account 实体的字段集**:本子单只加持久化层挂钩,Account 内存态字段不扩(扩玩法字段是 Tier 1+ 议题)
- **不动既有 GateAccountFlagComponent 挂会话身份链路**:本子单在其**之前**挂 upsert 钩子,挂会话身份本身不动
- **不消费状态字段 + 不误改状态字段**:upsert 的 `$set` / `$setOnInsert` 都不带 status(防 Tier 1+ 运营改写被本子单重置)
- **不新增 RPC / 不新增客户端可见协议**:无新 proto 改动 → 客户端段 proto 生成物 0 diff

## 服务端段交付清单(给 server-dev)

> server-dev 开工第一步 = **grep fantasy-net 工程已落地的「LoginGameHandler 处理链 / Account 实体 / GateAccountFlagComponent / MongoDB 连接配置」现状**(简报已给关键文件路径作参考:`Server/Hotfix/Outline/Server/Map/Entity/Account.cs` / `Server/Hotfix/Outline/Server/Map/Scene/Process.cs` / `Server/Hotfix/Share/Outline/Server/Gate/Account/GateAccountFlagComponent.cs`,但**plan 不替 dev 调查**实际接缝)。

### 服务端落地清单(按 35 §一切分表 + §3.2 处理顺序)

| 落点 | 行为级完成定义 |
| --- | --- |
| 新增 MongoDB accounts 集合持久化层 | 集合存在(启动后或首次 upsert 自动创建);字段集含主键 / 首次注册时间 / 末次登录时间 / 状态四字段(实际字段命名以 Fantasy.Net + MongoDB BSON 约定为准) |
| LoginGameHandler 挂 upsert 钩子 | 在「挂会话身份」之前插入步骤:解析 AccountName(= UUID)→ 单条 `findOneAndUpdate` + `upsert=true` + `$setOnInsert`(首次注册时间 = 服务端时钟)+ `$set`(末次登录时间 = 服务端时钟)+ 不触碰状态字段 → 成功 → 继续既有挂会话身份 / 创建 Player 流程;失败 → 返登录失败结果码 / 短路 / 不挂会话身份 |
| 不动既有 LoginGameHandler 其它步骤 | 解析请求 / 挂会话身份 / 创建 Player / 回响应五段沿用既有 |
| 不动协议 | `C2G_LoginGameRequest` / `G2C_LoginGameResponse` 字段集不变,无新 proto |
| 不动客户端 | Unity 工程 git diff 须空(SV11) |

### server-dev 须自行据 Fantasy.Net 现状定的实现选择

- accounts 集合实际名(plan 不指定字面量)
- 字段实际命名(以 BSON 约定为准)
- upsert 失败结果码具体码值(沿用既有 Fantasy.Net 结果码体系)
- MongoDB 连接配置沿用工程现有 `Fantasy.config` 口径,**不新增连接串**

## 验收标准(server-test 逐条核;完成定义=行为可观测)

参见 35 §七 验收点 SV1-SV12 全条。主验:
- **SV1 编译**、**SV3 首连自动注册**、**SV4 重连首次注册时间不变**、**SV5 状态字段不被覆盖**、**SV7 并发同 UUID 原子**、**SV9 既有四特性零回归**、**SV11 客户端工程零 diff**、**SV12 Code Review**

辅验:**SV2 集合 schema**、**SV6 重启持久**、**SV8 Mongo 不可达失败短路**、**SV10 account 字段语义对齐**

### BLOCKED 边界

- 本机 MongoDB(`D:\mongodb-portable`)不可达 → 真往返写库类 SV(SV3 / SV4 / SV6 / SV7 / SV10)判 **BLOCKED 非 FAIL**(memory `local-mongodb-for-server-roundtrip` + server-test memory `feedback-blocked-vs-fail`)
- 编译 / Code Review(SV1 / SV12)、客户端工程零 diff(SV11)、既有四特性零回归(SV9)照常验

## 已拍板决策(decisions,自治默认推进,要改另开增量)

- **设计稿 = 新建 35-account-server.md**(`nav.js` GROUPS 已预注册占位 + desc 作前序设计意图锚,本任务据其落正稿);设计 18 / 19 / 30 / 31 / 32 / 33 **零改写**(本子单与之均正交或沿用既有契约)
- **安全默认 = 设备 UUID 自动注册式账号**:account = UUID 字符串(沿用既有四特性 account 语义);服务端首连自动 upsert;无密码 / 无第三方;Tier 3 升「绑定 OpenID / 邮箱」时是加字段非改主键
- **无新客户端可见 RPC**:复用既有 `C2G_LoginGameRequest` / `G2C_LoginGameResponse`,处理语义升级为「认证 = 注册 + 登录二合一」(注册成功 vs 登录成功对客户端无差别)
- **客户端工程零 diff**:Unity 工程 `Assets/` 须无改动(含 `Assets/GameProto/` 协议生成物);LoginUI 空壳 / `FantasyNetworkConfig.DefaultAccountName` UUID 生成 / 设置窗登出占位均保留
- **accounts 集合字段 = 主键 UUID + 首次注册时间 + 末次登录时间 + 状态(默认 0)**;状态字段本子单**不消费**(Tier 1+ 运营后台再消费)、不能**误改**(upsert 严禁触碰状态);不加投机性时间索引(Tier 1+ 真需要再加)
- **upsert 必须原子**:单条 `findOneAndUpdate` + `upsert=true` + `$setOnInsert`(首次注册时间)+ `$set`(末次登录时间);两步先查后写违此红线(SV7 + SV12 Code Review 拦)
- **upsert 失败 → 返登录失败 + 短路后续 + 不挂会话身份**(沿用 30 「服务不可用不本地放行」)
- **既有裸 UUID 数据「真正首次注册时间」失真接受**(无法反推);文档显式声明限制
- **既有四特性 account 语义零迁移**:30 / 31 / 32 / 33 业务集合 account 字段值不变、schema 不变、PASS 验收点须仍 PASS
- **Account 内存态实体字段集不扩**:本子单只加持久化层 + 登录时机挂钩,内存态扩字段是 Tier 1+ 议题
- **不投机性预留 Tier 3 接口**(同 19 settings「快捷登录 = 不做、不留钩子」做法):Tier 3 加 OpenID / 邮箱时是局部加字段加协议,本子单不预留
- **触发节律 / Handler 注册细节由 server-dev 据 Fantasy.Net 现状定**(plan code-blind):upsert 挂钩具体在哪个方法 / handler 自动注册路径属代码层接缝定位,plan 给行为契约(「在挂会话身份之前」「原子单条命令」),dev 据工程现状落
- **multi-session 同 UUID 挤号不防**(§5.3,Tier 1+ 议题);**真正首次注册时间补齐不做**(§5.5);**踢号 / 封号 / 登出协议不做**(§5.7 + Tier 1+);全部显式承认 / 文档声明

## 影响半径(本次改动 + 同步落点)

- **设计稿**:新建 `design-docs/35-account-server.md`(`nav.js` 已预注册占位 + desc,无需改 nav.js;首页卡片 / 侧边栏自动同步)
- **既有设计稿同步**:**无改写**——本子单与 18 / 19 / 30 / 31 / 32 / 33 均**正交或沿用既有契约**,不触发任何既有稿过时(见 35 §六 关系表)。Tier 0 第 2 子单(客户端 LoginUI 接线 + 设备 UUID 迁移 + 设置窗登出协议)将触发 19 / Login 链路相关稿改写,**留下一刀**
- **代码**:server-dev 据 Fantasy.Net 现状判断(LoginGameHandler 挂钩 / accounts 集合 / MongoDB 连接);Unity 工程零 diff(SV11 硬验);`Assets/GameProto/` 无新协议生成物(SV11)
- **不同步**(明令不改):18 player-info 数据层(正交)、19 settings 登出占位(Tier 1+ 客户端段刀再接)、30 / 31 / 32 / 33 业务集合 schema(零迁移)、既有 demo Account 实体 / GateAccountFlagComponent 链路(沿用)

## 自检(plan 收尾必做,conventions §「收尾必做」)

- [x] 过程性内容不在正文(本设计稿无 diff 叙事、无「按你说的改成」、无对话痕迹)
- [x] 正文无可推导事实的副本(本子单是新建稿,旁注「fantasy-net 现状以工程为准」沿用 30/31/32/33 范式,不复述代码)
- [x] 正文无拟人 / 口语比喻(grep 「死/打死/收口/钉死」:§5.7 用「钉死」一处 — 检视:用「守住」替更平实)
- [x] 工作态内容可识别所属任务(本 state 文件头部明标「Tier 0 第 1 子单」)
- [x] 被改动规范的旁注仍成立(本子单无改既有稿,无孤儿旁注)
- [x] 过时正文已重写或删除(本子单是新建,既有 18/19/30/31/32/33 不过时)
- [x] design-docs 正文 code-free(grep `[A-Z][a-zA-Z]+\.[A-Z]` 类符号:只放行 §六 关系表必要提及的「既有 demo Account 实体 + GateAccountFlagComponent」框架文件路径作 server-dev 调查锚 — 检视:本子单的特殊性是「复用 demo 既有链路」必须给 dev 指出文件位置,否则 dev 无从 grep;这与 conventions 「design-docs 与代码解耦」的张力 — 沿用 31/32/33 「在 Fantasy.Net 工程内」类指代是 code-blind 安全的做法,但 35 因「复用既有 demo Account.cs / Process.cs / GateAccountFlagComponent.cs」是本子单的关键设计决策(不是另造 Account 实体),路径不写出 dev 可能误以为要新建 — 取「§读前必看第 5 条 + §一切分表 + §3.2 表」三处提及文件路径作 dev 调查锚,但**不**写代码符号 / 类成员 / 方法签名,守 code-blind 80% 红线;同样口径 30 §一 / 31 §一 「沿用 30 已确立的身份口径」也指了既有稿的 anchor)
