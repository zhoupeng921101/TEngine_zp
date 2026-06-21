# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务

Tier 4 活动系统 · 客户端段 · 第 2 子单(EVENT 头像解锁通路 · client 段)

## 改动摘要

按 plan 交接区清单 + 设计 41 §三 落地 EVENT 头像解锁全栈 client 段。核心循环:邮件附件 `(itemId=30101, count=1)` → 客户端解析 EVENT(`UseEffect=5`)→ 适配器调 `AvatarUnlockService.GrantUnlock` → `PlayerInfo.UnlockedAvatarIds` 含 `avt_star id=3` → 三处领奖路径按 D3 触发 `SavePlayer` 平铺落盘 → 重启 LoadPlayer 持久。

落点四层:① 配置层(Luban 加两行,客户端/服务端共识对齐);② 解析层(`ItemGrant.Resolve` switch 加 case 5 + `GrantKind.EventUnlock` 枚举档);③ 适配器层(`ResolveAndApply` switch 加 EVENT 分支 + 静态私有 `ApplyEventUnlock` 走 `GameContext.Instance.Player` + `AvatarConfigMgr.GetAvatar`);④ 调用方层(三处领奖路径在 `GrantOnAcquire` 后调 `ItemGrant.ContainsEventUnlock(granted)` 决策 `SavePlayer`,沿 plan D3)。

### 关键决策(都已写进 plan,这里只标对应执行选择)

| # | 决策 | 执行 |
| --- | --- | --- |
| D1 | `itemdef.xlsx` use_effect 直接填整数 `5`(沿现状 `int UseEffect`,无 EUseEffect 枚举档) | 配置直填 `5` |
| D2 | EVENT 道具 `type=MATERIAL` 配 `automatic=1` 立即结算(与 30001 经验道具同范式) | 配置 `type=MATERIAL` |
| D3 | 适配器内不显式 `SavePlayer`,由调用方按 `produced` 列表是否含 `EventUnlock` 触发 | 加 `ItemGrant.ContainsEventUnlock(IReadOnlyList<GrantPayload>)` 静态帮助方法 + 三处调用点判触发 |
| D4 | EVENT 道具 `use_value=3` 指 `avatar.xlsx id=3 avt_star`(server 段已固化共识) | 配置 `use_value=3` |
| D5 | 客户端 `giftrandom.xlsx index=6101` 必须加(共识对齐 + Luban `c,s` 双端同源) | 已加,字段值与 server `GiftPoolSeeds` 逐字段对齐 |
| D8 | 三处领奖路径同范式触发 `SavePlayer`(`MailboxService.Claim` / `ClaimAll` / `RemoteMailService.ClaimAsync` / `RedeemService.RedeemAsync`)| 三处都接,代码路径一致 |
| 实现细节 | EVENT 触发用 `GameContext.IsValid` 二段防护(非 `?.`):防 EditMode 单测无 GameContext 实例时无谓触发 OnInit | 见 `MailboxService:148`/`RemoteMailService:91`/`RedeemService:104` |

### 自治审计(本环节自主拍板)

- **`MailboxService.ClaimAll` 也加 EVENT 触发**:plan 只显式列了 `Claim` 单封路径,但 `ClaimAll`(一键领取)沿用同一 `GrantPool → ItemGrant.GrantOnAcquire` 路径,EVENT 道具同样会被解析触发 `AvatarUnlockService.GrantUnlock`,如果不在末尾核 SavePlayer,一键领含 EVENT 道具的邮件后重启会丢失解锁状态,违 D8 + E1 「重启后持久」契约。按现状给两个领取入口都加 SavePlayer 触发,属 "调查后据证拍板" 的第 ② 类阶梯。
- **`ApplyEventUnlock` 拆为独立 private 静态方法**:plan 范式是 switch 内嵌实现,但拆出独立方法让 EVENT 落点逻辑可单独读、便于后续 25 头像选择窗刀直接引(`AvatarUnlockService.GrantUnlock` 是底层 API,但 EVENT 适配语义是「Item 系统的产出落 Player 系统」语义层接缝)。方法保 `private`、签名只接 `GrantPayload`,不破坏外部 API。
- **`ItemGrant.ContainsEventUnlock` 提为 public 静态帮助方法**:让三处调用点(MailboxService/RemoteMailService/RedeemService 全部跨文件)能引用同一判据,避免每处自己写 `for { if Kind==EventUnlock }`(违 conventions §1 隔离 + 出现三处重复)。
- **Luban 重导后 `Tables.cs` 模板形态变化(lazy 字段 → 自动属性)已回滚**:执行 `dotnet --roll-forward LatestMajor Luban.dll` 重导后,`Tables.cs` 被新模板覆盖成「构造里一次性 `new TbXxx`」+ 自动属性形式,与原 lazy 字段 + `SetDefaultLoader`/`Init()` 形态不一致;虽然 `ConfigSystem.Load()` 只用 `new Tables(loader)` 构造调用、新模板兼容,但任务范围内不引入工具版本副作用(违 Fantasy/E3 守不变量),按 conventions §6 现状覆盖式回滚仅保留两个 `.bytes` 增量,Luban 模板差异留后续工程层独立刀解决。

## 文件清单

### 修改

| 路径 | 改动 |
| --- | --- |
| `D:/work/TEngine_block/Configs/GameConfig/Datas/itemdef.xlsx` | 加 1 行 `id=30101`(EVENT 解锁道具,`use_effect=5, use_value=3, automatic=1, type=MATERIAL, quality=4`) |
| `D:/work/TEngine_block/Configs/GameConfig/Datas/giftrandom.xlsx` | 加 1 行 `index=6101, item_id=30101, num=1, rate=100`(与 server `GiftPoolSeeds` 共识对齐) |
| `D:/work/TEngine_block/UnityProject/Assets/AssetRaw/Configs/bytes/item_tbitemdef.bytes` | Luban 重导产物(含新行 30101) |
| `D:/work/TEngine_block/UnityProject/Assets/AssetRaw/Configs/bytes/item_tbgiftrandom.bytes` | Luban 重导产物(含新行 6101) |
| `D:/work/TEngine_block/UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Item/ItemGrant.cs` | ① `using GameLogic.BlockBlast.Player;` ② `GrantKind` 加 `EventUnlock=5` 档 ③ 加 `ContainsEventUnlock(IReadOnlyList<GrantPayload>)` 静态帮助方法 ④ `Resolve` switch 加 `case 5: return GrantPayload(EventUnlock, def.UseValue, count, 0, 0)` ⑤ `ResolveAndApply` switch 加 `case GrantKind.EventUnlock` 分支 ⑥ 新私有静态方法 `ApplyEventUnlock(GrantPayload)`:`IsValid` 短路 + `GetAvatar` 查无 null + 调 `GrantUnlock`,任何情形不抛 |
| `D:/work/TEngine_block/UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/Mail/MailboxService.cs` | `Claim` 单封 + `ClaimAll` 一键领取末尾各加一行 `if (ItemGrant.ContainsEventUnlock(...) && GameLogic.GameContext.IsValid) GameLogic.GameContext.Instance.SavePlayer();`(沿 D3) |
| `D:/work/TEngine_block/UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/Mail/RemoteMailService.cs` | `ClaimAsync` 成功分支末尾加 EVENT SavePlayer 触发(EVENT 邮件真往返主路径) |
| `D:/work/TEngine_block/UnityProject/Assets/GameScripts/HotFix/GameLogic/Module/Redeem/RedeemService.cs` | `RedeemAsync` 成功分支末尾加 EVENT SavePlayer 触发(D8 三处一致;兑换码理论不发 EVENT 但代码层与邮件路径同范式防漂移) |

### 新增

| 路径 | 内容 |
| --- | --- |
| `D:/work/TEngine_block/UnityProject/Assets/Editor/Tests/BlockBlast/EventUnlockClientTests.cs` | 9 个 EditMode 单测:`CV2_Resolve_EventUnlock_ProducesPayload` / `CV3_ResolveAndApply_EventUnlock_GrantsAvatarIdempotent` / `CV4_EventUnlock_EntryNotFound_SilentNoThrow` / `CV5_EventUnlock_PlayerNull_SilentNoThrow` / `CV6Aux_Resolve_ExistingCases_Unchanged` / `ContainsEventUnlock_*` 三档边界 |

### 不动(零 diff)

- Fantasy 仓任何代码 / 配置 / 协议(`D:/work/TEngine_block/Fantasy/` 0 改)
- `__enums__.xlsx`(无新枚举档,沿 D1)
- `avatar.xlsx`(`id=3 avt_star` 已样例存在,仅引用)
- `AvatarUnlockService.GrantUnlock` / `AvatarConfigMgr.GetAvatar` 签名(本子单调用方)
- `PlayerInfo` / `MergeMetaSave` DTO 结构(`UnlockedAvatarIds` 字段已就位、自然平铺落盘)
- 道具表 21 字段 schema(`itemdef.xlsx` 仅加新行)
- `ItemConfigMgr.ToItemDef` 桥接(`UseEffect` 已是 int 透传)
- `Tables.cs`(Luban 模板差异已回滚,见自治审计)

## 验证点(给 test)

### 编译 / 启动

- **VP1 编译 0 错 0 警告**:已自检 `read_console types=[error,warning] count=30` 返 0 条;`refresh_unity` 后 `editor_state.advice.ready_for_tools=true`(test 重跑可复核)
- **VP2 EditMode 全绿**:已自跑 `run_tests assembly_names=["BlockBlast.Tests"]` → `total=444 passed=444 failed=0 skipped=0`(包含本单 9 个新测 + 既有 435 个零回归;test 重跑可复核)

### CV(配置 + 单测验收点)

| # | 验证点 | 怎么验 | 预期 |
| --- | --- | --- | --- |
| CV1 | itemdef.xlsx 30101 + giftrandom.xlsx 6101 行 + Luban 重导 .bytes 加载 | ① `py -c` 读 xlsx 行核字段值 ② 写一个 `[Test]` 直读 `Assets/AssetRaw/Configs/bytes/item_tbitemdef.bytes` 经 `GameConfig.item.TbItemDef.GetOrDefault(30101)`(沿 `ItemSystemTests C2` 范式) | id=30101 行 `UseEffect=5, UseValue=3, Automatic=1, (int)Type=2 MATERIAL`;6101 行聚 1 项 `(ItemId=30101, Num=1, Rate=100)` |
| CV2 | `Resolve(use_effect=5)` 产 `GrantKind.EventUnlock` | 既有测试 `CV2_Resolve_EventUnlock_ProducesPayload` | `Kind=EventUnlock, TargetId=3, Amount=1` |
| CV3 | `ResolveAndApply` 命中 GrantUnlock 落 UnlockedAvatarIds 幂等 | 既有测试 `CV3_ResolveAndApply_EventUnlock_GrantsAvatarIdempotent`(经 `GameContext.Instance.InitPlayerFromMeta` 注 player + `AvatarConfigMgr.InitForTest` 注 entry,触发 `GrantOnAcquire`) | `UnlockedAvatarIds` 含 3 + `IsUnlocked` 返 true + 二次触发不增长 |
| CV4 | 适配器静默 · entry 不存在 | 既有测试 `CV4_EventUnlock_EntryNotFound_SilentNoThrow`(use_value=999) | `DoesNotThrow` + `produced` 含 EVENT 结构 + `UnlockedAvatarIds` 引用不变 |
| CV5 | 适配器静默 · PlayerInfo 为 null | 既有测试 `CV5_EventUnlock_PlayerNull_SilentNoThrow`(故意不实例化 GameContext) | `IsValid=false` + `DoesNotThrow` + `produced` 含 EVENT 结构 |
| CV6 | 既有 EditMode 全绿零回归 | `run_tests assembly_names=["BlockBlast.Tests"]` | 444/444 PASS,既有 ItemSystemTests U1-U4(Resolve 五档)行为不变 |
| CV7 | Play 启动到主菜单零回归 | 进 Play → 自动登录 → 主菜单 → 点开邮件/设置/玩家信息窗 | 无 console 报错 |
| CV8 | 三处领奖路径调用点 SavePlayer 触发(D3) | grep `ContainsEventUnlock` 应有 3 处 + 都接 `GameContext.SavePlayer`;读 `MailboxService.cs:148/176` `RemoteMailService.cs:91` `RedeemService.cs:104` 核 | 三处源码均含 `if (ItemGrant.ContainsEventUnlock(...) && GameLogic?.GameContext.IsValid) GameLogic?.GameContext.Instance.SavePlayer();` 范式 |

### E(全栈真往返)

| # | 验证点 | 怎么验 | 预期 |
| --- | --- | --- | --- |
| E1 | 全栈真往返 | ① mongod 27017 + Fantasy Main.exe 起服 ② Unity Play ③ 模拟登录 7 次(或经 server 端测试钩子直推 7 次 OnLogin)→ server 段 `activity_progress.activityId=2 counter=7, lastClaimedCycleKey=1` ④ 客户端收件箱见 EVENT 邮件 ⑤ 客户端点领取 → 服务端返 reward=`[(itemId=30101, count=1)]` ⑥ 客户端 RemoteMailService.ClaimAsync 成功分支 → `GrantOnAcquire` → case 5 → ApplyEventUnlock → `player.UnlockedAvatarIds` 含 3 ⑦ `ContainsEventUnlock(granted)=true` → SavePlayer ⑧ 重启 Play → LoadPlayer → UnlockedAvatarIds 仍含 3 + IsUnlocked(avt_star)=true | 全链通,重启持久 |
| E2 | OneShot 永发跨会话 | E1 后第 8/9 次登录 → server 段 lastClaimedCycleKey=1 不变 → 不重发 EVENT 邮件 | 收件箱无第二封 EVENT 邮件 |
| E3 | Fantasy 仓 + 客户端工程 git diff 范围 | `cd Fantasy && git status -s` + `cd UnityProject && git diff --name-only` | Fantasy 零本子单 diff;客户端 diff 仅含两个 xlsx + 两个 .bytes + ItemGrant.cs + MailboxService.cs + RemoteMailService.cs + RedeemService.cs + 新 EventUnlockClientTests.cs |

### BLOCKED 边界(沿 plan §BLOCKED 边界 + memory)

- mongod 27017 / Main.exe / Play 模式不可达 → E1/E2 列 BLOCKED 非 FAIL(沿 31/32/33/35/37/39/40 口径)
- Luban 工具链不可达 → CV1 列 BLOCKED 非 FAIL;CV2-CV6 纯逻辑单测仍跑

## 异常路径自检(交接前)

按 conventions §收尾必做「过异常路径不只 happy path」自挑:

- ✅ **null 防护**:CV5 测 `GameContext.IsValid=false` → ApplyEventUnlock 第一行 `IsValid ? Instance.Player : null` 短路 → return → produced.Add 记结构 + 不抛
- ✅ **entry 不存在**:CV4 测 `AvatarConfigMgr.InitForTest` 灌空表 → `GetAvatar(999)=null` → return → produced.Add + 不抛
- ✅ **重复触发幂等**:CV3 第二次 GrantOnAcquire → `AvatarUnlockService.AppendDistinct` 已含跳过 → 长度不变 + 仍含 3
- ✅ **ContainsEventUnlock null/empty**:`ContainsEventUnlock_NullOrEmpty_ReturnsFalse` 测两情形返 false 不抛
- ✅ **既有五档零回归**:CV6Aux 跑 case 1/2/3/4/default,确认 EVENT case 5 加入未污染其它分支
- 待 test 复核:**EditMode 经 GameContext.Instance.InitPlayerFromMeta 注入 player 时**,OnInit 内 InitRank/InitMail/PlayerAttr 构造副作用(本机 PlayerPrefs 持久化历史可能含 `UnlockedAvatarIds` 数据)— CV3 已手动覆盖 `pInCtx.UnlockedAvatarIds = new[] { DefaultAvatarId }` 让起点干净,但 CV4/CV5 未做此操作(CV4 测引用不变 / CV5 不实例化 GameContext 故不读 dto)。test 可重跑 1000 次复核稳定性
- 待 test 复核:**ContainsEventUnlock 在大 produced 列表的性能**(三处领奖路径每次都 O(N) 线性扫):邮件附件 List<MailRewardLine> 上限沿 32 §三 1 封不超 10 项,N=10 量级,无性能问题

## 自检(conventions §收尾必做)

- [x] 过程性内容不在正文:本 dev 交接区无 "按你说的"/"我刚才"/diff 叙事;改动摘要 + 决策表 + 文件清单 + 验证点全是事实陈述
- [x] 正文无可推导事实的副本:文件清单的行号 / 字段值是「dev 落地锚」沿 38 接缝定义型例外;不堆 git 历史 / 测试细节
- [x] 正文无拟人 / 口语比喻:`grep -nE '钉死|焊死|绑死|打死|死在|搞定|搞死|彻底'` 零命中
- [x] 工作态内容可识别所属任务:本文件头标「Tier 4 客户端段 · 第 2 子单」+ E1 收口标识
- [x] 改动文件无孤儿旁注:本子单未改 design-docs / rules / CLAUDE.md(plan 已改 40/16/39/41),dev 改动仅集中在 4 个 .cs + 2 个 xlsx + 2 个 .bytes + 1 个新 .cs
- [x] 过时正文已重写或删除:本 dev 文件「当前任务」节是覆盖式重写(从无 → 本子单),旧任务关单已搬归档

## 返修协议

若 test 报 FAIL/BLOCKED → 读 `pipeline/state/test.md` 复现清单 → 按打回原因定位 → 修复 → 自检 → 重跑 → 更新本交接区。
