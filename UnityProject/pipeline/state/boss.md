# boss 状态

## 当前状态(2026-06-22)

**在跑:无。** mongod 27017 在,Main 进程已停。

**自治批关单进度**(2026-06-18 起):

| Tier | 特性 | 状态 |
| --- | --- | --- |
| Tier 0 | 真实账号体系全栈(注册 + 登录会话 + 登出) | ✅ 收口 |
| Tier 1 | 兑换码 / 排行榜数据源 / 邮件 / 排行榜结算 / 邮件运营推送 server 段 | ✅ 全清结(详见 backlog 段) |
| Tier 2 | 玩家属性权威全栈 + 客户端 HUD + 服务端 ledger(写/查) + 客户端 ledger UI | ✅ 完全收口 |
| Tier 4 | 活动系统 4 类节律全栈(Login + EVENT/Action + Cumulative)+ 4 套样例活动 | ✅ 节律基础设施收口,加新活动 = 加配置行 + 加触发钩子 |

> 提交链、轮次细节与各特性 archive 路径见下方「最近关单」段;backlog 走向与剩余项见「服务端遗留功能 backlog」段。

**Carry forward**:
- design-docs/40(3 处)+ 39(1 处)过时「客户端段下一刀实做」标注 — 代码行为已正确,文档同步遗漏,test 按文档同步漏点先例不升 FAIL,留下一轮顺手清。
- 邮件 E1 真往返欠(server `22c21843` code-complete,等环境稳后补)。
- ~~Fantasy `PlayerAttrLedgerDoc.Kind` doc comment 误差~~ — 已清 `1f18d35b`(2026-06-21 Tier 2 client 段第 4 子单跨仓清)
- **`CompletedOrders` 持久化 bug**(2026-06-22 发现):`MergeOrderState.ImportMeta` L603 把 dto.completedOrders 灌回 `CompletedOrders`,而 `IsDemoComplete()` L636 用 `CompletedOrders >= 5` 判通关——两套语义冲突,本机累计满 5 单后再开新局就秒触发 `TriggerWin()` 弹「恭喜通关」窗。用户已手动清存档绕过,根因诊断与三方案选项见主会话记录。修法待用户拍板(推荐方案 A:`ImportMeta` 不覆盖 `CompletedOrders`,需要长期统计另起 `LifetimeCompletedOrders` 字段;附带改 design-docs/14 §3.1「累计完成单数」描述)。
- **本次任务遗留**:测试注释 `MergeOrderTests.cs:366`「不再 FIFO 抽干」含 diff 叙事,dev 下次顺手清;人工冒烟未跑(MCP 不支持拖拽,分布性已由单测覆盖);dev 报 EditMode 515 vs test 实跑 497 差异待澄清(可能 PlayMode 用例混入)。

**流水线变更 ✅ committed `bc31dbd4`**:server-test 路由由 codex 启动器(`pipeline-server-test-codex`)切回 Claude 卡 `pipeline-server-test`——codex 执行流程当前不稳定;启动器卡 + run-codex-verify.mjs 保留在盘可逆,`SKILL.md`/`pipeline-auto.js` 旁注记重新启用路径。

**下一单方向**:Tier 2 ① 玩法路径接入金币消费(大刀,需玩法循环改造 + 体力升级为「跨会话余额+局内消费」二层模型,touches GameWindow/BlockGameState/PlayerAttrService) / Tier 4 剩 4 套活动 + Schedule 类节律(留运营定具体活动 + Schedule 需定时器实做,Cumulative 类加新活动 = 加配置行+加触发钩子,基建已建) / Tier 3 跨设备同步(blocked 在 Tier 0 OpenID/邮箱绑定流前置,留运营/产品需求后再开)。

## 服务端遗留功能 backlog

> 2026-06-18 盘点。来源 = design-docs 服务器接缝 + 客户端 stub。**逐项详情不在此转述**(随代码演进会漂移)——读括注的接缝位置现场推导。分层/优先级/依赖是规划判断。多数功能客户端已留「可注入数据源/校验器」接缝(本地占位能跑、远程存根返空),服务端接上即兑现。优先级 = 对玩法成立/上线的必要性。

**Tier 0 · 地基(其余全部的前置)**
- 真实账号体系 — **全栈收口**(2026-06-20):server 段 1 子单 `df8e1162` + client 段 2 子单 `0ff27e80`;LoginUI 真空壳保留(自动登录无声跑通是设计意图),登出按钮 = 实做(Shutdown→Boot)。第 3 子单(OpenID/邮箱绑定流,Tier 3 跨设备恢复前置)留后续运营/产品需求后再开
- ~~客户端业务协议 Handler 接线~~ — backlog 认知错(2026-06-20 boss 调查):Fantasy.Net 的 `Call(req).Wait()` 范式不需要回包 Handler,Mail/Rank/Redeem RPC 已全通(三类 Remote*Source.cs 实装);Handlers/ 只放推送类(2 demo 正确用法);Property 是 Tier 2 数值权威范畴,后端没建客户端无需建。本项移除。

**Tier 1 · 全部清结**(Tier-1 全栈批进度表 4 项 + 邮件领奖服务端校验 — 全已建)
- ~~邮件领奖服务端校验~~ — 已建(MailDecisionHelper.cs 五子项:身份校验/邮件归属/过期/防重领/发奖。boss 调查 2026-06-20)
- ~~全局限量~~ — 设计 32 §七 O2 自治不做(留运营后续/运营无需求时不进 backlog)
- ~~定时发送(过期滤)~~ — 已建;~~定时投递 + 主动清理~~ — 设计 32 §七 O1 自治不做(同上)

> **backlog 认知矫正(2026-06-20)**:Tier 1 五项里 4 项实质已在 server `22c21843` + `420272b8` 完成(plan 阶段曾被标 TODO 但代码已落),1 项(限量/定时投递)在 32 §七 O1/O2 服务端段设计时已拍板不做。Tier 1 至此全清。同源风险:Tier 0「客户端业务协议 Handler 接线」可能同样多数已建(否则兑换码/排行榜 E1 真往返跑不通),下一步 boss 主会话先调查再决定起不起流水线。

**Tier 2 · 防作弊/服务端权威(数值真相搬服务端,改动较大)**
- 玩家属性权威(金币/钻石/体力)— **完全收口**(2026-06-20/21):
  - server 段 1 子单 `c248669f`(三属性 mongodb players 集合权威)
  - client 段 2 子单 `b1772838`(钻石客户端实装 + 改名扣钻真接线)
  - client 段 3 子单 `c861d610`(tarot HUD 三属性接 PlayerAttrService)
  - server 段 2 子单 ledger 写入 Unity `3825e1d1` + Fantasy `750ec980`(player_attr_ledger 集合 + source 10 档枚举 + 索引 + 旁路挂钩)
  - server 段 3 子单 ledger 查询 RPC Unity `3d2e966c` + Fantasy `29597b26`(C2G_QueryAttrLedgerRequest + handler + 协议生成物)
  - client 段 4 子单 ledger UI Unity `b1fdec0c` + Fantasy `1f18d35b`(我的流水窗 PlayerAttrLedgerWindow + RemoteAttrLedgerService + carry-forward 注释清)
  
  后续刀(留 backlog 备查,非急):① 玩法路径接入(金币玩法消费/产出 + 体力升级为「跨会话余额 + 局内消费」二层模型,需玩法循环改造)
- ~~道具配置服务端导出 + 发奖合法性~~ — 已实质被覆盖(2026-06-21 boss 认知矫正):16 §3.2「客户端+服务端都导出」是字段语义层声明,实现层服务端用 `AuthoritativeDefs`/`GiftPoolSeeds` 单一权威源(Tier 4 第 2 子单 server 段已建,activity_id=2/3/4 道具 id 30002/30101/30001/30003 已落);发奖合法性走「服务端达标 → 邮件礼包 → 客户端只解析既定 id」+ Tier 2 第 1 子单 PlayerAttr 服务端权威账本。本项移除。

**Tier 3 · 账号上服后的跨设备衍生**
- 云存档备份 / 跨设备恢复 — 中 — 14-save-system.md
- 玩家信息跨设备同步(名字/等级/头像框/解锁集)— 中 — 18-player-info.md §3.8
- 设置云同步(音乐/音效)— 低 — 19-settings-system.md
- 改名屏蔽词表远程下发 — 低 — 18-player-info.md §3.3

**Tier 4 · 活动系统**(第 1 子单 ✅ 基础架构+每日登录奖,`de5d4da7`+`f5d44f7f`;第 2 子单 ✅ EVENT 解锁全栈收口,server `bafed768`+`d3e3b4fd`、client `37977dc7`,UseEffect=5 + AvatarUnlockService.GrantUnlock + 三领奖路径 SavePlayer + E1 全栈真往返实跑确认 UnlockedAvatarIds=[1,3] 重启持久)
- **剩后续**:9 套活动其余 7 套(逐套刀加 Type=Cumulative/Schedule/Action 触发钩子,server only 配置加行,可批量做)+ 客户端活动 UI(若需活动列表/进度展示,纯邮件收奖可砍)
- 原 backlog 描述「AvatarUnlockService.cs 钩子恒返未解锁」**已实质矫正**:实为已建接缝非缺口,通路需扩 16 UseEffect
- 原 backlog 描述「RankPersistence 日期本地判可刷」**归错系统**:属 33-O1 范畴非 Tier 4

**待决策(非遗留,方向问题)**
- 多人/PvP — Fantasy 已有 demo Unit/Move,是否做多人模式未定;做才扩单位同步

**out-of-scope(上线前通用基建,本项目未设计,非本盘点遗留功能,仅备忘)**
推送通知/原生推送、事件分析上报、IAP 校验(红线去变现)、协议版本/强更、资源完整性校验、反外挂风控、客服工单、账号 2FA、存档版本迁移、跨时区结算

## 最近关单

- 2026-06-16 · gameplay-fusion-impl 玩法融合代码落地 · PASS（dev-test）· `archive/2026-06-16-gameplay-fusion-impl/`
- 2026-06-17 · 01 改订对齐 29 融合 · PASS（plan-only）· `archive/2026-06-17-01-fusion-align/`
- 2026-06-18 · 试点段二·atlas 工程件（Auto9Slicer→json 胶水 + Sheet 命名对齐 + Pack 链路验证）· PASS（dev-test，含 boss 交叉检返工 1 轮）· `archive/2026-06-18-pilot-seg2-atlas-tooling/`
- 2026-06-20 · 排行榜结算客户端段·本地结算退役 + 红点交棒邮件 · PASS（full,round 0 一次过）· client `80ef8a49` · `archive/2026-06-20-rank-settle-client/`
- 2026-06-20 · 邮件运营推送来源·服务端段 · BLOCKED-env（实质 = 现状审计,代码已在 22c21843 落地,Main PID 54856 持锁起不了新服真往返；33 PASS 反向佐证）· 无 diff · `archive/2026-06-20-mail-broadcast-server/`
- 2026-06-20 · Tier 0 真实账号·服务端段第 1 子单（注册 + 登录会话）· PASS（full,round 0；server-test 静默挂起 5.5h 后 resume 一次过）· Fantasy `df8e1162` + 设计稿 `7602ecd3` · `archive/2026-06-20-account-server-tier0/`
- 2026-06-20 · Tier 0 真实账号·客户端段第 2 子单（登出接线 + 自动登录链路审计；Tier 0 全栈收口）· PASS（full,round 1：round 0 因 19 设计稿 WARNING/NOTE 块「离线无账号」文案漏改 FAIL → dev F1 覆盖式重写 → round 1 干净）· client `0ff27e80` · `archive/2026-06-20-account-client-tier0/`
- 2026-06-20 · Tier 2 玩家属性权威·服务端段第 1 子单（金币/钻石/体力反作弊核心铺地基）· PASS（full,round 0 一次过；47 分钟；MongoDB 探针实跑 SV2-11/14/16）· Fantasy `c248669f` + 设计稿/生成物 `2e5bcba8` · `archive/2026-06-20-player-attr-server-tier2/`
- 2026-06-20 · Tier 2 玩家属性权威·客户端段第 2 子单（钻石首次实装 + 改名扣钻真接线；Tier 2 全栈收口；plan 矫正 boss 基线错）· PASS（baton=dev-test,round 1：round 0 因 18 设计稿 WARNING 块「故扣减为空操作」过时文案 FAIL → dev F1 覆盖式重写 26/34/103/165-170 同源 → round 1 干净）· client `b1772838` · `archive/2026-06-20-player-attr-client-tier2/`
- 2026-06-21 · Tier 4 活动系统·服务端段第 1 子单（基础架构 + 每日登录奖最简活动）· PASS（full,round 0；首次因 session-limit 触顶 BLOCKED → resume 续 server-dev/server-test 一次过）· Fantasy `de5d4da7` + 设计稿 `f5d44f7f` · `archive/2026-06-21-activity-server-tier4/`
- 2026-06-21 · Tier 4 活动系统·服务端段第 2 子单（EVENT 解锁活动 - 每日登录 7 次得头像）· PASS（full,round 0；plan 现场转方案 B 沿 16 UseEffect=5 范式,零动 39 ActivityDef schema 零 helper 代码改;E3 客户端段未实做 BLOCKED 非 FAIL 不打回）· Fantasy `bafed768` + 设计稿 `d3e3b4fd` · `archive/2026-06-21-event-unlock-server-tier4/`
- 2026-06-21 · Tier 4 活动系统·客户端段第 2 子单（EVENT 解锁通路接通 — EVENT 解锁全栈收口）· PASS（full,round 0；62 分钟；BlockBlast.Tests 444/444 + E1 全栈真往返跑通 UnlockedAvatarIds=[1,3] 重启持久；plan 又一次矫正 boss 简报偏差：工程 ItemDef.UseEffect 是 int 无 EffectType 枚举/handler 体系）· client `37977dc7` · `archive/2026-06-21-event-unlock-client-tier4/`
- 2026-06-22 · merge-order 候选块元素分配改「容量加权随机」· PASS（dev-test,round 0 一次过；BlockBlast.Tests 497/497 + 1 改写 + 3 新增用例全过；旁路发现 `CompletedOrders` 持久化 bug 进 Carry forward）· client `c86d7bbf` · `archive/2026-06-22-merge-order-trio-element-distribution/`

> 完整关单历史以 `archive/` 目录为准（boss.md 仅留最近指针）。
