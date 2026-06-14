# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:redeem-code 通用兑换码系统·数据逻辑层 + 服务器接缝(自治·放手默认)

设计稿 `design-docs/20-redeem-code-system.html`。已完成,待测试验收。

### 交接区(给 test)

**改动摘要(做了什么 / 为何这么做 / 关键决策)**

通用兑换码数据逻辑层 + 服务器接缝,四层(校验 / 去重 / 发奖 / 服务编排)+ Luban 配置表。全部加法式扩展,既有玩法零行为变化:

- **Luban 两表**:`redeemcode`(码主表,主键 `code` string,字段 code/name/once_per_player/expire_time + remark[group=e 不导出])+ `redeemreward`(奖励子表,主键 `auto_id` int,字段 auto_id/code/item_id/num,按 code 聚合)。注册进 `__tables__.xlsx`,导表生成 `GameConfig.RedeemCode`/`RedeemReward` 行类 + `GameConfig.redeem.TbRedeemCode`/`TbRedeemReward` 表类 + `redeem_tbredeemcode.bytes`/`redeem_tbredeemreward.bytes`。已成功导表(`DOTNET_ROLL_FORWARD=Major` 直调 Luban.dll,绕 .bat 的 pause)。
- **POCO + 桥接**:`RedeemCodeDef`/`RedeemReward`(GameLogic.Redeem)隔离 Luban 类型;`RedeemConfigMgr`(GameLogic.Config,仿 ItemConfigMgr)`EnsureLoaded` 走 ConfigSystem、`Get(规整码)` 查、`InitForTest`/`ResetForTest` 注入。字典 key 用规整后大写码,与服务层 `Normalize` 同口径。
- **校验接缝(=「服务器接缝」实义)**:`IRedeemValidator.Validate(规整码)` → `ValidationResult(Status, Def)`;`LocalConfigRedeemValidator`(默认,查本地配置)/ `RemoteRedeemValidator`(stub 返 `SourceUnavailable`,**不抛**,留 TODO,无任何网络调用)。
- **去重存储**:`IRedeemStore`;`PersistenceRedeemStore` 包既有 `Persistence.Provider`、专用键 `"Redeem.Redeemed"`、换行分隔串序列化、反序列化对空串/无键/脏数据产合法空集合不抛;`InMemoryRedeemStore`(测试)。
- **服务编排**:`RedeemService.Redeem(raw, state, rng)` 顺序 = 空输入 → SourceUnavailable → NotFound → Expired → AlreadyRedeemed → 发奖 → MarkRedeemed。**记录已兑换在发奖成功之后**(失败不占名额);只去重 once_per_player=1 的码。`Normalize`=trim+ToUpperInvariant。`NowProvider` 可注入(默认系统时钟)、`ExpireTime` 空不判过期、parse 失败按不过期。发奖**复用 16** `ItemGrant.GrantOnAcquire`(不复制落点),汇总立即结算产出为 `GrantPayload` 列表。`RedeemResult` 枚举 + `RedeemOutcome` 结构 + `RedeemText`(六类占位 textId 110601–110606,互不相同非 0)+ `TextIdFor`。
- **钩子接线**:`SettingsLinks.OpenRedeemCode()` TODO 注释更新为指向 `RedeemService` 服务入口(本轮不建窗口,O7)。

**关键决策**:
- 奖励复用「道具 id × 数量」经 16 落点,不另造奖励结构(plan O2)。
- 远程校验仅 stub、不引入网络(plan O1 / 风险表第 1 条)。
- 去重经既有 Persistence.Provider、不另造存储栈(plan O3 边界 / 风险表第 5 条)。
- C3 Luban 直读验收:导表工具链可达,已生成 .bytes,本轮 **C3 实测可跑(非 BLOCKED)**。
- 测试道具经 `ItemConfigMgr.InitForTest` 自控 `UseNum=1`,使产出 Amount == 奖励 num,便于精确断言。

**文件清单**

新增(C#):
- `Assets/GameScripts/HotFix/GameLogic/Module/Redeem/RedeemCodeDef.cs`(POCO)
- `Assets/GameScripts/HotFix/GameLogic/Module/Redeem/IRedeemValidator.cs`(校验接缝 + Local/Remote)
- `Assets/GameScripts/HotFix/GameLogic/Module/Redeem/IRedeemStore.cs`(去重 + Persistence/InMemory)
- `Assets/GameScripts/HotFix/GameLogic/Module/Redeem/RedeemService.cs`(服务 + RedeemResult/RedeemOutcome)
- `Assets/GameScripts/HotFix/GameLogic/Module/Redeem/RedeemText.cs`(textId 占位 + TextIdFor)
- `Assets/GameScripts/HotFix/GameLogic/Config/RedeemConfigMgr.cs`(桥接)
- `Assets/Editor/Tests/BlockBlast/RedeemCodeSystemTests.cs`(14 测试)

新增(Luban 生成,导表产物):
- `Assets/GameScripts/HotFix/GameProto/GameConfig/RedeemCode.cs` / `RedeemReward.cs`(行类)
- `Assets/GameScripts/HotFix/GameProto/GameConfig/redeem.TbRedeemCode.cs` / `redeem.TbRedeemReward.cs`(表类)
- `Assets/AssetRaw/Configs/bytes/redeem_tbredeemcode.bytes` / `redeem_tbredeemreward.bytes`

新增(Luban 源,仓库根 `Configs/GameConfig/Datas/`):
- `redeemcode.xlsx` / `redeemreward.xlsx`

修改:
- `Configs/GameConfig/Datas/__tables__.xlsx`(注册两表)
- `Assets/GameScripts/HotFix/GameProto/GameConfig/Tables.cs`(导表自动加 TbRedeemCode/TbRedeemReward 访问器)
- `Assets/GameScripts/HotFix/GameLogic/Module/Settings/SettingsLinks.cs`(OpenRedeemCode TODO 注释接线)

**验证点(逐条对应设计稿 §六 23 条验收点;test 该验什么 / 怎么验 / 预期)**

dev 已跑 EditMode `BlockBlast.Tests`:**311/311 全绿**(原 297 + 新 14,0 failed 0 skipped,2.95s)。test 复跑核对:
- **C1/C2/C3**:`C1_ConfigMgr_GetReturnsDef_RewardsAggregated`(聚合)/ `C2_ConfigMgr_KeyNormalizedUpper_MissReturnsNull`(规整 key + 查无 null)/ `C3_LubanDirectRead_RedeemCodeBytes_HasRows`(直读两 .bytes 行数>0 + WELCOME2026 字段映射 + MULTIGIFT 3 奖励项)。**C3 工具链已可达,实测跑过,非 BLOCKED**。
- **V1/V2/V3**:Local 命中/未命中 / Remote stub 不抛返 SourceUnavailable / Service 注 Remote 返 SourceUnavailable(证换注入零改动服务层)。
- **D1/D2/D3**:InMemory 往返 / Persistence 注 InMemoryProvider 跨实例往返 + 空串/无键不抛 / once=0 重复成功不进集合、once=1 第二次 AlreadyRedeemed。
- **S1–S5**:空白短路 EmptyInput / " abc " 规整命中 / 首次 Success+Granted、再兑 AlreadyRedeemed 不重复发奖 / 过期 Expired + 空 ExpireTime 不判 / 失败不发奖不写去重 + 六类 textId 非 0 互不相同。
- **G1–G3**:货币落 state.Exp 正确数量 / 一码多奖 3 产出 + Exp/Energy/图案落点 / state==null 仍 Success + Granted 含 Numeric 结构不抛。
- **R1**:编译 0 error(read_console error 类 0 条)+ 311/311 全绿(零回归)。
- **R2**:Code Review 5 红线 —— 已 grep 确认 Redeem 目录无 `UnityWebRequest`/`HttpClient`/`WebRequest`/同步阻塞(唯一命中是 IRedeemValidator.cs:66 的禁用注释);无 `ModuleSystem.GetModule`(走 Persistence.Provider 接缝);本层无资源加载、无事件;发奖复用 16 不复制落点;PlayerPrefs 经既有 Provider 非阻塞。

**标注**
- 涉及热更程序集:是(全部落 `GameScripts/HotFix/GameLogic`)。
- 需 Luban 重生成:**已重生成**(test 若重导表用 `$env:DOTNET_ROLL_FORWARD="Major"` 直调 Luban.dll,见 memory)。
- 需进 Play 模式手验:无(本轮纯逻辑层 + 接缝,EditMode 全覆盖;UI 输入/结果弹窗 O7 延后,不在本轮)。

**待拍板(plan 已记,均取安全默认推进,无阻塞)**:O1 远程 stub / O2 道具 id×数量 / O3 不接背包实例 / O4 NowProvider 默认系统时钟 / O5 全局限量不做 / O6 textId 占位 / O7 UI 延后 / O8 仅 trim+大写规整。无非裁决不可推进的方向问题。
