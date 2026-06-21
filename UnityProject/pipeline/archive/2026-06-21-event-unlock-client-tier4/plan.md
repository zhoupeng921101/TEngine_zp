# plan 状态

当前任务:Tier 4 活动系统 · 客户端段 · 第 2 子单(EVENT 头像解锁通路 · client 段)

## 范围与决策

- **设计稿**:新建 `design-docs/41-event-unlock-client.md`(`nav.js` GROUPS 在「全栈 / 服务端」组 40 之前注册;首页卡片 / 侧边栏自动同步);同步在 `40-event-unlock-relay.md` §8.3 E3 状态(从「客户端段下一刀实做后联调验」→「由 41 §8.2 E1 收口」)、`16-item-system.md` §3.7 EVENT 旁注节状态(server/client 段交付分述 + 「为什么不在 ActivityDef 加 EventUnlockId 字段」段去重)+ §3.7 解析表加 `case 5 EVENT` 行 + 调用方落点表加 EVENT 行、`39-activity-server.md` §七 O5 状态(从「由 40 兑现」→「由 40+41 联合兑现」)
- **范围**:本子单 client 段 = 「Luban 加两行配置 + 客户端代码三处加分支」= ① 客户端 `itemdef.xlsx` 加 1 行 EVENT 解锁道具(`id=30101, use_effect=5, use_value=3 指 avatar id=3 avt_star, automatic=1 立即结算, type=MATERIAL`)+ ② 客户端 `giftrandom.xlsx` 加 1 行 EVENT 礼包(`index=6101, item_id=30101, num=1, rate=100` 单项必中,与 server `GiftPoolSeeds` 共识对齐)+ ③ `ItemGrant.cs` 加 `GrantKind.EventUnlock` 枚举档 + `Resolve` switch 加 `case 5: return GrantPayload(EventUnlock, use_value, count, 0, 0)` + `ResolveAndApply` switch 加 `case GrantKind.EventUnlock` 分支(取 `GameContext.Instance?.Player` + `AvatarConfigMgr.GetAvatar(targetId)` → 都非 null → `AvatarUnlockService.GrantUnlock(p, e)`)+ ④ 三处领奖路径(`MailboxService.ClaimMail` / `RemoteMailService.OnClaimResp` / `RedeemService.Apply`)在 `GrantOnAcquire` 调用后核对 `produced` 列表含 `EventUnlock` 时触发 `GameContext.Instance?.SavePlayer()` 落盘 + ⑤ EditMode 单测三类(Resolve EVENT 分支产出 / ResolveAndApply 适配器 GrantUnlock 落 UnlockedAvatarIds / 边界 entry/playerInfo 为 null 静默) + ⑥ E1 全栈真往返(起服 + 模拟登录 7 次 → 邮件附件 → 领取 → EVENT 解析 → GrantUnlock + SavePlayer + 重启持久 + IsUnlocked=true)
- **可砍档(默认砍)**:① 9 套 EVENT 解锁头像活动清单(O5,运营后续逐套刀);② 跨设备已解锁集合同步(O7,Tier 3 范围);③ EVENT 头像 textId 多语言(O8,后续);④ EVENT 头像选择窗 UI / 三态切换显示(O6,表现层后续刀,本子单内存断言 + 重启读回是可验收的端到端边界,不依赖 UI);⑤ `automatic=0` 进背包路径(EVENT 类语义只能立即结算,沿 40 §3.3 D4)

## 安全默认采纳(self-determined,自治默认推进)

- **D1 不在 `__enums__.xlsx` 加 `EUseEffect.EVENT` 枚举档**:沿 §二审计第 1 行 — 客户端 `ItemDef.UseEffect` 字段是 `int`(`Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Item/ItemDef.cs:34`)、`ItemConfigMgr.ToItemDef` 桥接也是 `int`、Luban 生成类 `_buf.ReadInt()`;`__enums__.xlsx` 现有 7 段无 `item.EUseEffect` 段。`itemdef.xlsx` 的 `use_effect` 列直接填整数 `5` 即可,沿 `ItemGrant.cs` 既有 `switch(def.UseEffect)` 五档加 `case 5: EVENT` 即正交扩展。简报「16 UseEffect 加 EffectType=5 EVENT handler」措辞与现状有出入(`EffectType` 在工程中并不存在),按现状执行不按简报字面。**调查后据证拍板 = 第 ② 类阶梯**,不入 blockers
- **D2 EVENT 道具 `type=MATERIAL`(2)**:沿 §二审计第 2 行 — `item.EItemType` 现有五档(`CURRENCY=1 / MATERIAL=2 / FUNC_MATERIAL=3 / GIFT_SELECT=5 / GIFT_RANDOM=6`),无 `EVENT` 值;`type` 字段语义是「背包归类」(沿 16 §3.3),EVENT 类「立即结算 + 不进背包」语义靠 `automatic=1` 实现而非 `type` 字段区分。取 `type=MATERIAL` 配 `automatic=1` 立即结算(与 30001 经验道具 `type=CURRENCY+automatic=1` 范式同源)。**反向方案**(新加 `EItemType.EVENT` 枚举档)需改 `__enums__.xlsx` + 重导 Luban + 覆盖 ItemConfigMgr 桥接的所有现有 `(item.EItemType)row.Type` cast 路径,范围扩大且重叠 `use_effect=5` 区分位。简报「类型=EVENT」表述与工程实际枚举值不符
- **D3 适配器内不显式 SavePlayer,由调用方按需触发**:沿 §3.5 决策 D3 + §七 O1。理由:① 既有适配器(`ApplyNumeric` / `ApplyPattern`)都只写运行期态,落盘由更外层流程统一触发;② 一封邮件含多个 EVENT 触发即落盘 N 次 = 多次磁盘 IO;③ 三处领奖路径(`MailboxService` / `RemoteMailService` / `RedeemService`)落盘策略不一,本子单不替它们决定 — 留 dev 在三处调用点末尾按 `produced` 列表是否含 `EventUnlock` 决定触发 `SavePlayer`
- **D4 EVENT 道具 `use_value=3` 指向 avatar id**:沿 40 §3.3 + §二审计第 3 行 — `avatar.xlsx id=3 avt_star, unlock_cond=EVENT` 行已样例存在;`use_value` 字段对 `use_effect=5` 时存「头像 / 框 id」(头像 vs 框由 AvatarEntry.Type 自动分流到 `UnlockedAvatarIds` / `UnlockedFrameIds`,本子单适配器**不**自己判 Type 完全依赖 `GrantUnlock` 内部分流)
- **D5 客户端 `giftrandom.xlsx` index=6101 必须加(双端同源 + 共识对齐)**:沿 §3.3 共识对齐硬约束 + Luban `##group=c,s` 双端同源原则。本子单领取路径不依赖客户端礼包池(服务端抽奖返奖励列表),但双端同源是 Luban 工程约定,且未来「展示 EVENT 礼包预览」场景按 `GetGiftRandom(6101)` 查池
- **D6 设计稿独立成稿(41-event-unlock-client.md)而非在 40 增订**:取 41 独立成稿(40 是 server 段独立稿,本子单是 client 段)。理由:① 同 30-39 既有全栈稿「server 段独立稿 + client 段独立稿」(30/31/32/33 + 35/36 + 37/38 + 40/41)范式同源;② 40 §读前必看 + §需求降层 + §五崩法 + §七 O5 多处显式声明「客户端段下一刀」是后续,本子单 41 兑现 + 同步改 40 §8.3 E3 状态符合 conventions §6;③ 41 命名清晰交付 EVENT 解锁中继客户端段职责
- **D7 守 40 server 段 PASS 基线(Fantasy bafed768 + 设计稿 d3e3b4fd)**:本子单纯客户端段,Fantasy 仓零 diff;CV6 / E3 验收点核「既有六全栈 + 35/36/37/38 + 39 + 40 + 16/18 全 PASS 不破」+ Fantasy 仓 git status 仅含与 EVENT 客户端段无关的预存改动
- **D8 落盘归调用方分析涵盖三处领奖路径**:沿 §六 关系表 — `MailboxService.ClaimMail` / `RemoteMailService.OnClaimResp` / `RedeemService.Apply` 各自落盘策略不一,但 EVENT 落盘逻辑统一为「produced 列表含 `EventUnlock` → `SavePlayer`」,dev 在三处实现时同范式

## 守不变量(给 dev / test 的硬边界)

- **Fantasy 仓零 diff**:本子单纯客户端段;Fantasy 服务端代码 / 配置 / 协议零改动;E3 验收点核
- **32 SendMailTo 签名零改**:沿 40 §读前必看第 2 条 + 守不变量(同设计 40 server 段约束)
- **39 ActivityDef schema 零字段加**:沿 40 §读前必看第 2 条(EVENT 通路不需要 ActivityDef 新字段)
- **18 `AvatarUnlockService.GrantUnlock(PlayerInfo, AvatarEntry)` API 签名零改**:本子单调用方,签名 `(PlayerInfo p, AvatarEntry e)` 不变
- **16 `ItemGrant.Resolve` / `GrantOnAcquire` / `ResolveAndApply` / `ApplyNumeric` / `ApplyPattern` 外部签名零改**:仅在 `switch(def.UseEffect)` 加 `case 5` + `switch(payload.Kind)` 加 `case GrantKind.EventUnlock` + `GrantKind` 枚举加 `EventUnlock` 档(C# 枚举加档不破二进制兼容)
- **16 道具表 21 字段结构零改**:`itemdef.xlsx` 仅加新行(`id=30101`),不动 schema 第 0-3 行(##var/##type/##group/##);`use_effect` 字段值域从 0-4 自然扩到 0-5
- **既有 avatar 头像表零改**:仅引用现有 `id=3 avt_star`(EVENT, unlock_param=9001 占位,本子单不动其 unlock_param 9001 ≠ activity_id=2 的现状,沿 40 §二 现状审计)
- **`PlayerInfo` / `MergeMetaSave` DTO 零改**:`UnlockedAvatarIds` 字段已就位、`MergeMetaSave` 平铺持久化路径已就位;EVENT 解锁结果经既有 14 LoadPlayer/SavePlayer 路径自然落盘
- **既有六全栈 + 35/36/37/38 + 39 + 40 + 16/18 PASS 不破**:CV6 / E3 验收点核
- **客户端 `giftrandom.xlsx index=6101` 与 server `GiftPoolSeeds Index=6101` 共识对齐**:`item_id=30101, num=1, rate=100` 四个语义键逐字段对齐,server 段 commit `bafed768` 已固化 server 侧,本子单 CV1 锁客户端侧

## 客户端段交付清单(给 dev)

> dev 开工第一步 = ① 核 `Configs/GameConfig/Datas/itemdef.xlsx` Luban 加行的字段集与桥接(沿 §3.2 字段表 21 列对齐);② 核 `Configs/GameConfig/Datas/giftrandom.xlsx` 加行的 5 列字段(`auto_id, index, item_id, num, rate`);③ 重导 Luban 产物(`Assets/AssetRaw/Configs/bytes/item_tbitemdef.bytes` / `gift_tbgiftrandom.bytes` 更新);④ 改 `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Item/ItemGrant.cs` 三处(`GrantKind` 枚举加 `EventUnlock` + `Resolve` switch 加 `case 5` + `ResolveAndApply` switch 加 `case GrantKind.EventUnlock` 分支);⑤ 改三处领奖路径调用点(`MailboxService.cs` / `RemoteMailService.cs` / `RedeemService.cs`)按 D3 决策核对 `produced` 列表含 EventUnlock 时触发 `GameContext.Instance?.SavePlayer()`;⑥ 加 EditMode 单测三类(CV2-CV5)。

### 客户端段落地清单(按 41 §三 + §3.6 时序图 + §八 CV/E)

| 落点 | 行为级完成定义 |
| --- | --- |
| 客户端 `itemdef.xlsx` 加 EVENT 解锁道具行 | `id=30101, name=110101, desc=210101, icon=icon_avatar_event, quality=EPIC(4), light=空, automatic=1, type=MATERIAL(2), param=0, use_effect=5, use_value=3, use_num=1, use_level=0, stacking=0, 限时整套填 0/空, jump_list=空`;`##group=c,s` 双端同源导出 |
| 客户端 `giftrandom.xlsx` 加 EVENT 礼包行 | `auto_id=11, index=6101, item_id=30101, num=1, rate=100`(单项必中);与 server `GiftPoolSeeds (Index=6101, ItemId=30101, Num=1, Rate=100)` 共识对齐;`##group=c,s` 双端同源 |
| `ItemGrant.cs` 加 `GrantKind.EventUnlock` 枚举档 | 沿 16 §3.7 既有 5 档(None=0/Numeric=1/Pattern=2/GiftSelect=3/GiftRandom=4)加第 6 档 `EventUnlock=5`;C# 枚举加档不破二进制兼容 |
| `ItemGrant.cs` `Resolve` switch 加 `case 5` | `case 5: return new GrantPayload(GrantKind.EventUnlock, def.UseValue, count, 0, 0);`(TargetId=use_value=avatar id;Amount=count 沿既有范式,适配器忽略此字段) |
| `ItemGrant.cs` `ResolveAndApply` switch 加 `case GrantKind.EventUnlock` | 取 `GameLogic.GameContext.Instance?.Player`(防 EditMode null)+ `GameLogic.Config.AvatarConfigMgr.GetAvatar(payload.TargetId)`(查无返 null)→ 若两者非 null → `GameLogic.BlockBlast.Player.AvatarUnlockService.GrantUnlock(playerInfo, entry)`;任何情形(成功 / playerInfo null / entry null)均 `produced.Add(payload)` 记产出结构;任何情形不抛异常 |
| 三处领奖路径调用点 SavePlayer 触发(D3)| `Assets/GameScripts/HotFix/GameLogic/Module/Mail/MailboxService.cs:189` 调用 `GrantOnAcquire` 后 / `Assets/GameScripts/HotFix/GameLogic/Module/Mail/RemoteMailService.cs:106` 调用 `GrantOnAcquire` 后 / `Assets/GameScripts/HotFix/GameLogic/Module/Redeem/RedeemService.cs:119` 调用 `GrantOnAcquire` 后 — 检查 `produced` 列表(或本次的 `all` 累积列表)是否含 `GrantKind.EventUnlock`,含则调 `GameLogic.GameContext.Instance?.SavePlayer()` |
| EditMode 单测三类 | 沿 ItemSystemTests 范式新建 EVENT 子单测;`ItemConfigMgr.InitForTest` 注入 EVENT 道具 POCO + `AvatarConfigMgr.InitForTest` 注入 AvatarEntry;`Resolve` EVENT 分支产出对(CV2)+ ResolveAndApply EVENT 落 UnlockedAvatarIds 命中(CV3)+ entry/PlayerInfo 为 null 静默不抛(CV4/CV5) |

### dev 须自行据工程现状定的实现选择

- `GrantKind.EventUnlock` 枚举值的具体常量(沿 C# 枚举默认递增,EventUnlock 自然 = 5)
- `ResolveAndApply` EVENT 分支内对 `GameContext.Instance` 为 null 的处置(EditMode 单测时 GameContext 实例本身可能未初始化;推荐用 `GameContext.Instance?.Player` 三段安全,或重构适配器接受 PlayerInfo 注入参数 — 但后者改 ResolveAndApply 外部签名,违守不变量,故取前者)
- EditMode 单测对 `AvatarConfigMgr.InitForTest` / `ItemConfigMgr.InitForTest` 的注入数据具体形态(沿 PlayerInfoTests / ItemSystemTests 既有范式)
- 三处领奖路径调用点的 `produced` / `all` 列表变量名(各处略有不同,如 `MailboxService` 内是 `all`、`RedeemService` 内是 `produced`,dev 按现场名取)
- EVENT 单测的具体放置文件(`ItemSystemTests` 内加 EVENT 段 / 新建 `EventUnlockClientTests.cs`,任择)

## 验收标准(test 逐条核)

参见 41 §八全部验收点(CV1-CV8 + E1-E3)。主验:
- **CV1 配置层 Luban 加行 + 重导**:`itemdef.xlsx` 含 `id=30101` 行字段值对 + `giftrandom.xlsx` 含 `index=6101, item_id=30101, num=1, rate=100` 行 + Luban 重导后 `.bytes` 加载能按 id 取到(绕 YooAsset 直读)
- **CV2 `Resolve` EVENT 分支产出**:`Resolve(use_effect=5, use_value=3, count=1)` 返 `GrantPayload{ Kind=EventUnlock, TargetId=3, Amount=1, Level=0, Times=0 }`
- **CV3 EVENT 适配器命中 GrantUnlock**:`ResolveAndApply` EVENT 分支 → `playerInfo.UnlockedAvatarIds` 含 3;再调一次仍只含 3(幂等)
- **CV4 适配器静默 · entry 不存在**:`AvatarConfigMgr` 不注入 → `ResolveAndApply` EVENT 分支(`use_value=999`)不抛 + `UnlockedAvatarIds` 仍为 null/空 + `produced` 含 EVENT 结构
- **CV5 适配器静默 · PlayerInfo 为 null**:`GameContext.Player = null` → `ResolveAndApply` EVENT 分支不抛 + `produced` 含 EVENT 结构
- **CV6 既有 EditMode 全绿零回归**:`ItemSystemTests / PlayerInfoTests / MailSystemTests / MailRemoteSourceTests / RedeemCodeSystemTests / RewardDisplayTests / WeightCfgLubanTests` PASS;`Resolve` 既有五档(0/1/2/3/4)行为不变
- **CV7 Play 启动到主菜单零回归**:工程启动可点开邮件 / PlayerInfoWindow / 设置窗 / 排行榜窗等无报错
- **CV8 三处领奖路径调用点 SavePlayer 触发(D3)**:dev 在三处调用点实做按 D3 决策核对 `produced` 列表含 `EventUnlock` 时触发 `SavePlayer`
- **E1 全栈真往返**(力争):起 Fantasy(mongod 27017 + Main.exe)+ Unity Play + 模拟登录 7 次 → server 段 `activity_progress.activityId=2 counter=7, lastClaimedCycleKey=1` + `mails` 收一封 `mail_def=7002, reward=6101` + 客户端拉邮件 + 领取 → `GrantOnAcquire` → Resolve case 5 → ResolveAndApply EVENT 分支 → `GrantUnlock(player, avt_star_entry)` → `UnlockedAvatarIds` 含 3 + `SavePlayer` 落盘 → 重启 Play → `LoadPlayer` 读回 `UnlockedAvatarIds` 仍含 3 + `IsUnlocked(player, avt_star_entry)` 返 true
- **E2 OneShot 永发跨会话**:E1 完成后第 8/9/... 次登录 → server 段 `lastClaimedCycleKey=1` 不变 → 不重发 EVENT 邮件
- **E3 Fantasy 仓 + 客户端工程 git diff 范围正确**:Fantasy 仓零本子单 diff;客户端工程 diff 仅含 itemdef.xlsx / giftrandom.xlsx / Luban 重导产物 / ItemGrant.cs / Luban 生成类(若有)/ 三处领奖路径调用点(MailboxService/RemoteMailService/RedeemService)+ 新增 EditMode 单测

### BLOCKED 边界

- 本机 MongoDB(27017)/ Fantasy 服务端(Main.exe)/ Unity Play 模式不可达 → E1/E2 判 **BLOCKED 非 FAIL**(沿 31/32/33/35/37/39/40 口径 + memory `local-mongodb-for-server-roundtrip` + test memory「feedback-blocked-vs-fail」)
- Luban 工具链不可达 → CV1 列 **BLOCKED 非 FAIL**(沿 numeric-system / 16 §六 BLOCKED 边界 + memory「Luban 配置表类设计」);CV2-CV6 纯逻辑单测仍可单独跑(经 `ItemConfigMgr.InitForTest` / `AvatarConfigMgr.InitForTest` 注入,不依赖 .bytes 文件)
- 客户端工程 EditMode 跑通(CV2-CV6)是本子单**必验**;真往返(E1/E2)力争 PASS、不可达 BLOCKED

## 已拍板决策(decisions,自治默认推进,要改另开增量)

- **设计稿 = 新建 41-event-unlock-client.md**(`nav.js` GROUPS 已注册「全栈 / 服务端」组,放在 40 之前;独立成稿描述 EVENT 解锁中继客户端段职责)
- **同步改 40-event-unlock-relay.md §8.3 E3 状态**(从「客户端段下一刀实做后联调验」→「由 41 §8.2 E1 收口」);沿 conventions §6 「改一处即同步被它过时的他篇」原则
- **同步改 16-item-system.md §3.7 EVENT 旁注节状态**(server/client 段交付分述 + 解析表加 EVENT 行 + 调用方落点表加 EVENT 行);沿 conventions §6
- **同步改 39-activity-server.md §七 O5 状态**(从「由 40 兑现」→「由 40+41 联合兑现 Tier 4 第 2 子单」);沿 conventions §6
- **D1-D8 全部据安全默认推进**(详见 §安全默认采纳节);全部记 decisions,不入 blockers
- **简报字面「16 UseEffect 加 EffectType=5 EVENT handler」+ 「类型=EVENT」措辞与现状有出入**:经调查后据证拍板取「`use_effect` 字段直接填整数 5 + `type=MATERIAL`」,理由见 D1+D2 + 41 立项框「需求降层 c」;**这是「调查后据证定最优解」的第 ② 类阶梯**,不入 blockers
- **EVENT 道具 `use_value=3` 占位指向 18 §3.5 头像表 `avt_star`**:沿 40 决策 + §二审计第 3 行,本子单守 `avatar.xlsx id=3 avt_star` 实存 + 适配器查 entry → 调 GrantUnlock 写入头像集合(由 AvatarEntry.Type 自动分流)

## 影响半径(本次改动 + 同步落点)

- **设计稿**:
  - **新建**:`design-docs/41-event-unlock-client.md`(EVENT 头像解锁通路 · client 段;`nav.js` GROUPS 已注册;首页卡片 / 侧边栏自动同步)
  - **同步改写**(conventions §6,本任务内同步):
    - `design-docs/40-event-unlock-relay.md` §8.3 E3 状态(由 41 §8.2 E1 收口,Tier 4 第 2 子单全栈联调由 41 完成)
    - `design-docs/16-item-system.md` §3.7 EVENT 旁注节状态(server/client 段交付分述,`__enums__` 不加 EUseEffect 注明)+ §3.7 「单个道具的解析」表加 EVENT 行(`use_effect=5 → 产出种类 EVENT 解锁`)+ §3.7 「调用方落点」表加 EVENT 行(落点 `AvatarUnlockService.GrantUnlock` + 落盘由调用方按 D3 触发)+ 「为什么不在 ActivityDef 加 EventUnlockId 字段」段去重(原有一段保留)
    - `design-docs/39-activity-server.md` §七 O5 状态(由 40+41 联合兑现 Tier 4 第 2 子单)
  - **不同步改**:14 / 18 / 21 / 30 / 31 / 32 / 33 / 35 / 36 / 37 / 38 全部正交;41 §六 关系表已列「零改动」
- **代码**(dev):
  - 客户端新增:`Configs/GameConfig/Datas/itemdef.xlsx` 加 EVENT 道具行 + `giftrandom.xlsx` 加 EVENT 礼包行 + Luban 重导
  - 客户端改:`Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Item/ItemGrant.cs` 三处加分支(GrantKind 枚举 + Resolve switch + ResolveAndApply switch)
  - 客户端改:三处领奖路径调用点(`MailboxService.cs` / `RemoteMailService.cs` / `RedeemService.cs`)按 D3 触发 SavePlayer
  - 客户端新建:EditMode 单测(`ItemSystemTests` 内加 EVENT 段或新建 `EventUnlockClientTests.cs`)
  - 协议生成物:**零新增协议**(本子单纯客户端代码 + 配置增量,无客户端 RPC 改)
- **不同步**(明令不改):
  - Fantasy 仓任何代码 / 配置 / 协议(本子单纯客户端段)
  - 30/31/32/33/35/36/37/38/39/40 既有 design / proto / handler / 集合 schema
  - 32 SendMailTo 签名(沿 40 守不变量)
  - 39 ActivityDef schema(沿 40 守不变量)
  - 18 `AvatarUnlockService.GrantUnlock` API 签名(本子单调用方)
  - 16 `ItemGrant.Resolve` / `GrantOnAcquire` / `ResolveAndApply` 外部签名(仅在 switch 内增分支)
  - 16 道具表 21 字段结构(仅加新行)
  - avatar 头像表(仅引用现有 id=3)
  - `PlayerInfo` / `MergeMetaSave` DTO 结构

## 自检(plan 收尾必做,conventions §「收尾必做」)

- [x] **过程性内容不在正文**:41 设计稿无 diff 叙事 / 无「按你说的改成」/ 无对话痕迹;§读前必看 + §立项 + §一-§九 全是事实陈述与设计契约;§二现状审计是基线证据陈述非过程
- [x] **正文无可推导事实的副本**:本子单引用 `AvatarUnlockService.GrantUnlock` / `AvatarConfigMgr.GetAvatar` / `ItemGrant.Resolve` / `GameContext.Instance.Player` / `PlayerInfo.UnlockedAvatarIds` 仅在 §二现状审计 + §3.5 行为级完成定义 + §六关系表作 dev 落地锚(沿 38 「Tier 收口 / 接缝定义型 design-docs 允许引接线点位置」例外:本子单是「客户端段实做接线」的明确接缝定义型 design-docs);设计稿正文规范本体的代码符号为既有引用(夹在「适配器调用、解析层加分支」语境中)+ 本设计稿声明的新概念(EVENT 道具 30101 / EVENT 礼包 6101 / `GrantKind.EventUnlock` 枚举档)
- [x] **正文无拟人 / 口语比喻**:`grep -nE '钉死|焊死|绑死|打死|死在' design-docs/41-event-unlock-client.md` 零命中
- [x] **工作态内容可识别所属任务**:本 state 文件头部明标「Tier 4 客户端段 · 第 2 子单 EVENT 头像解锁通路 client 段」+ 全栈联调 E1 收口标识
- [x] **被改动规范的旁注仍成立**:本子单改 40 §8.3 E3(状态更新,与既有 SV1-SV12 + E1-E2 无矛盾);改 16 §3.7 EVENT 旁注节(状态更新分述 server/client 段交付,与既有 5 档 + 旁注「为什么不在 ActivityDef 加字段」无矛盾)+ 16 §3.7 解析表 + 调用方落点表加 EVENT 行(纯加行无冲突);改 39 §七 O5(状态更新,与 O1-O7 其余项无矛盾);均无孤儿理由
- [x] **过时正文已重写或删除**:40 §8.3 E3 状态(从「客户端段下一刀」→「由 41 收口」)、16 §3.7 EVENT 旁注节(server/client 段交付分述,旧「客户端段下一刀实做」改写为现状)、39 §七 O5(从「由 40 兑现」→「由 40+41 联合兑现」),均是 conventions §6 「现行体系仍有活引用 → 覆盖式重写到现状」类;不加勘误注叠旧。40 设计稿其余正文(§读前必看 / §需求降层 / §五崩法 / §六关系表)中「客户端段下一刀」表述是 server 段交付时的设计意图叙述(server 段独立稿视角),客户端段已由 41 兑现这一事实通过 §六 关系表 + §8.3 E3 落点同步,不需要重写 40 整篇 — 40 正文从 server 段视角看仍准确
- [x] **design-docs 正文 code-free 大体守住**:41 正文规范本体的代码符号为本子单声明的新概念(`GrantKind.EventUnlock` / EVENT 道具 30101 / EVENT 礼包 6101)+ 接缝定义型 dev 落地锚(`ItemGrant.Resolve` / `ResolveAndApply` / `AvatarUnlockService.GrantUnlock` / `AvatarConfigMgr.GetAvatar` / `GameContext.Instance.Player` 在 §二/§3.5/§六作 dev 落地锚,沿 38 例外:Tier 收口 / 接缝定义型 design-docs 允许引接线点位置);**正文规范本体无随意符号点缀**,与 30-40 既有全栈稿同口径
- [x] **设计稿章节骨架完整**:立项信息表(类型 / 方向约束 / 范围 + 需求降层 a/b/c 含简报方案与现状审计对照)→ 现状审计(§二)→ 改什么与为什么(贯穿 §一-§三)→ 方案正文(§三 + §3.6 时序图)→ 整局走查 + 崩法 6 类(§五)→ 验收点(§八 CV / E)→ 待拍板清单(§七 O1-O8)→ 风险表(§九)+ 每个功能点标核心 / 增强 / 可砍三档(§七末)
- [x] **整局走查崩法 6 类齐**(§五:case 5 配置错指 / GrantUnlock 重复调幂等 / 本地存档可改 / 三处漏 SavePlayer / GrantUnlock 头像 vs 框 / 跨设备双登)
- [x] **范围闸**:每个功能点已标核心 / 增强 / 可砍三档;砍掉所有可砍档(O5/O6/O7/O8)后核心循环「邮件领取 → EVENT 解析 → GrantUnlock → SavePlayer → 重启持久」仍成立 = 范围已收敛(UI 三态切换显示由 25 / 未来头像选择窗后续刀承接,本子单内存断言 + 重启读回是可验收的端到端边界)
- [x] **整局走查向上对体验**:① 玩家:累计登录 / 签到 EVENT 行为有真实跨会话头像解锁激励 + 重启持久 + 跨设备同 UUID 服务端权威邮件兜底;② 运营:抓手做「累计登录 7 天送限定头像」类长期激励(本子单 1 套通路接通,9 套 EVENT 头像活动后续逐套刀只动配置不动代码);③ 工程:整条链复用 5 个子系统(40/39/32/16/18),不引入新协议、不引入新集合、不引入新代码大改面(`ItemGrant.cs` 三处加分支 + 三处调用点小改 + Luban 加两行 + EditMode 单测);④ 反作弊:登录类计数由服务端记不可被客户端伪造(沿 40),本地存档可改是 18 §3.8 固有限制(承认诚实边界 §五末,Tier 3 跨设备同步是后续)。四类都答得上「让什么体验 / 工程目标更好」

## 自治审计(本环节自主拍板的取舍,简记)

- **简报字面措辞处置**:① 简报「16 UseEffect 加 EffectType=5 EVENT handler」与现状有出入(`EffectType` 在工程并不存在,字段是 `int UseEffect`,无枚举 handler 体系),按现状照 D1 取「整数 5 + switch case 5」;② 简报「类型=EVENT」与现状有出入(`EItemType` 五档无 EVENT 值),按 D2 取 `type=MATERIAL` + `automatic=1` 立即结算配组;③ 简报「30101 plan 自决可能 `avatar_event_7day` 或类似」按 40 §3.3 已固化 `use_value=3` 指 avatar id=3 avt_star 不动(server 段已 PASS 共识对齐);④ **不停机走 boss 重派的理由**:简报字面措辞虽与现状有出入但**底层目的清晰**(让 server 段已固化的 EVENT 邮件附件 → 客户端解析 → 头像解锁),按现状照实做即可,**调查后据证拍板 = 第 ② 类阶梯**,记 decisions 不入 blockers
- **范围闸**:核心档 = Luban 加两行 + ItemGrant.cs 三处加分支 + 三处领奖调用点 SavePlayer 触发(D3) + EditMode 单测三类(本子单完整范围,「EVENT 全栈联调收口」);增强档 = 无(本子单纯配置 + 代码扩展增量);可砍档(全部砍)= 9 套 EVENT 头像活动清单(O5)+ EVENT 头像选择窗 UI(O6)+ 跨设备同步(O7)+ 多语言(O8)+ automatic=0 进背包(决策已固化 1);砍后核心循环仍成立
- **整局走查崩法 6 类齐**(§五:case 5 配置错指 / GrantUnlock 重复调幂等 / 本地存档可改 / 三处漏 SavePlayer / GrantUnlock 头像 vs 框 / 跨设备双登);§诚实边界(§五末)明示守 5 项 + 不守 6 项防 test/boss 误判范围
- **整局走查向上对体验**:四类(玩家 / 运营 / 工程 / 反作弊)都答得上(详见上节)
- **设计稿独立成稿(41 vs 40 增订)**:取 41 独立成稿。理由:① 同 30-39 既有「server 段独立稿 + client 段独立稿」(30/31 + 32/33 + 35/36 + 37/38 + 40/41)范式同源;② 40 是 server 段独立交付稿,client 段独立成稿便于后续审查 / 单刀维护;③ conventions §6 「改一处即同步被它过时的他篇」由本任务内同步改 40 §8.3 E3 + 16 §3.7 EVENT 旁注 + 39 §七 O5 兑现
- **D3 落盘归调用方分析**:取调用方触发(三处领奖路径各自调用点末尾按 produced 列表含 EventUnlock 判断触发 SavePlayer)。理由:① 既有适配器(`ApplyNumeric` / `ApplyPattern`)都只写运行期态;② 一封邮件多 EVENT 多次落盘 = N 次 IO 浪费;③ 三处落盘策略不一,适配器不替它们决定;④ EditMode 单测可纯逻辑跑,不需要模拟 GameContext / MergeMetaPersistence 整链
