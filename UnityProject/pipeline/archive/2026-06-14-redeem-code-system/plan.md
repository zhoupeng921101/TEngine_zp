# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:redeem-code 通用兑换码系统·数据逻辑层 + 服务器接缝(自治·放手默认)

设计稿 `design-docs/20-redeem-code-system.html`。xlsx 系统底层批次第六个增量,兑现设计 19 §3.6 留的兑换码 TODO 钩子。

**本轮文档库结构变更已适配**:开工期间有人把 design-docs 全库迁到 `assets/nav.js` 数据驱动导航(侧边栏文档树 + 首页卡片单一信息源在 `nav.js` 的 `GROUPS`,各页只留 `<aside class="sidebar" id="sidebar"></aside>` + 末尾 `<script src="assets/nav.js?v=1">`,本页目录由 nav.js 扫 h2/h3 id 自动生成)。doc 20 已按新骨架写;新增文档**只在 `nav.js` GROUPS 加一行**即全库同步,不再逐篇改 sidebar(旧的「逐篇同步 sidebar」经验对本库已失效)。

### 交接区(给 dev / test)

本轮交付**数据逻辑层 + 服务器接缝**:兑换码校验(可注入接缝)+ 本地一次性去重 + 发奖(复用 16 道具系统落点)+ 结果编排。设计稿 §五是 dev 改动清单(8 项),§六是验收点(23 条),§七是范围开关(均取默认),§八是风险表。

**单一事实源 = 代码(已 grep 核实的真实符号)**:
- 发奖落点:`GameLogic.BlockBlast.Item.ItemGrant.Resolve(ItemDef,count)` → `GrantPayload`;`ApplyNumeric(MergeOrderState,payload)` / `ApplyPattern(...)` / `GrantOnAcquire(ItemDef,count,MergeOrderState,System.Random)`(`Module/BlockBlast/Item/ItemGrant.cs`)。道具查 `GameLogic.Config.ItemConfigMgr.GetItem(id)`(`Config/ItemConfigMgr.cs`,含 `InitForTest`/`ResetForTest` 注入口)。
- 持久化接缝:`GameLogic.BlockBlast.IPersistenceProvider`(`TryGet`/`Set`/`Remove`)+ `Persistence.Provider`(默认 `PlayerPrefsProvider` / 测试 `InMemoryPersistenceProvider`,`Module/BlockBlast/Persistence.cs`)。
- 配置桥接范本:`ItemConfigMgr`(Luban 行 → POCO,运行期 `EnsureLoaded` 走 `ConfigSystem.Instance.Tables`;EditMode 经 `InitForTest` 注入绕 YooAsset)。
- 设置入口钩子:`GameLogic.Settings.SettingsLinks`(`Module/Settings/SettingsLinks.cs`)留的 `OpenRedeemCode()` TODO 注释——本系统兑现它。
- 奖励展示(可选接):`GameLogic.BlockBlast.Reward.RewardView`(`Module/BlockBlast/Reward/RewardView.cs`,设计 17)。
- 工程**无网络模块**(grep 无 `INetworkModule`/`UnityWebRequest`/`HttpClient`,`Books/3-8-网络模块.md` 标「待补充」)——「服务器接缝」= 可注入校验器接口,不是真发请求。
- 源 xlsx 在仓库根 `Configs/GameConfig/Datas`(与 UnityProject 同级,同 num/item/avatar)。无既有 redeemcode 表(干净新建)。
- 测试 asmdef `Assets/Editor/Tests/BlockBlast/BlockBlast.Tests.asmdef` 已引用 `GameLogic`+`GameProto`+`TEngine.Runtime`,可达。

**关键约束(防 dev 做歪,设计稿读前必看四条 + 风险表)**:
1. **「服务器接缝」= 可注入 `IRedeemValidator` 接口,不真发 HTTP**:生产注 `LocalConfigRedeemValidator`(查本地 Luban 配置表);`RemoteRedeemValidator` 仅 stub 返 `SourceUnavailable`(不抛)。**禁引入 `UnityWebRequest`/`HttpClient`/真连后端**(离线 + 去变现方向)。
2. **发奖复用 16 既有落点,不另造**:遍历 `def.Rewards` → `ItemConfigMgr.GetItem` → `ItemGrant.GrantOnAcquire`,汇总 `GrantPayload` 列表。不复制「码→货币/图案」落点逻辑。
3. **去重经既有 `Persistence.Provider`,不另造存储栈**:本系统专用键 `"Redeem.Redeemed"`,存码集合(序列化字符串)。反序列化对任意输入产合法集合(空/无键→空集合不抛)。只去重 `once_per_player=1` 的码。
4. **记录已兑换在发奖成功之后**(失败不占名额);结果码顺序:空输入 → SourceUnavailable → NotFound → Expired → AlreadyRedeemed → 发奖 → MarkRedeemed。
5. **规整口径统一**:配置 key 与服务比对都走 `Normalize`(trim + `ToUpperInvariant`),配置表统一存大写。
6. **纯逻辑可单测 + 注入隔离**:POCO/校验/去重/服务纯内存;配置经 `RedeemConfigMgr.InitForTest`、去重经 `InMemoryRedeemStore`、发奖 `state` 可 null 走纯解析。Luban 直读验收锚 `AssetDatabase.LoadAssetAtPath<TextAsset>(.../redeemcode.bytes)` → `new TbRedeemCode(ByteBuf)`(绕 YooAsset,范本 `WeightCfgLubanTests`);导表工具链不可达列 BLOCKED 不判 FAIL。
7. **命名空间**:服务/校验/去重 `GameLogic.Redeem`(通用系统,同 19 `GameLogic.Settings`);配置桥接 `RedeemConfigMgr` 归 `GameLogic.Config`(与 `ItemConfigMgr` 并列);`GrantPayload` 仍在 `GameLogic.BlockBlast.Item`(复用不搬)。物理目录 `GameScripts/HotFix/GameLogic/Module/Redeem/`。
8. **不改框架/既有**:`ItemGrant`/`ItemConfigMgr`/`Persistence`/框架代码一律不动(只调用)。

**dev 新建文件(§五清单 8 项)**:`redeemcode.xlsx`(码主表 + 奖励子表)+ Luban schema 注册 / `RedeemCodeDef`+`RedeemReward` POCO / `RedeemConfigMgr`(桥接 + `Get` + `InitForTest`/`ResetForTest`)/ `IRedeemValidator`+`LocalConfigRedeemValidator`+`RemoteRedeemValidator`+`ValidationResult`/`ValidationStatus` / `IRedeemStore`+`PersistenceRedeemStore`+`InMemoryRedeemStore` / `RedeemService`+`RedeemResult`+`RedeemOutcome`+`RedeemText`+`TextIdFor` / 测试 `RedeemCodeSystemTests.cs`。把 `SettingsLinks.OpenRedeemCode()` TODO 注释指向本系统服务入口。

**验收点(设计稿 §六逐条断言定义,23 条)**:
- 配置 C1–C3:InitForTest 灌入后 Get 返对应 def + 奖励按 code 聚合 / key 用规整(大写)码、查无返 null 不抛 / Luban 直读 redeemcode.bytes 行数>0(工具链不可达列 BLOCKED)。
- 校验 V1–V3:Local 命中 Valid+Def / 未命中 NotFound+null;Remote stub 返 SourceUnavailable 不抛;Service 注 Remote → 返 SourceUnavailable(证换注入零改动服务层)。
- 去重 D1–D3:InMemory 标记往返;Persistence 注 InMemoryProvider 跨实例往返保真 + 空串/无键→空集合不抛;once=0 重复兑成功不进集合、once=1 第二次 AlreadyRedeemed。
- 服务 S1–S5:空/空白 → EmptyInput 不查表 / " abc " 规整命中存为 ABC / 有效码首次 Success+Granted 非空、once=1 再兑 AlreadyRedeemed 且不重复发奖 / 过期码 Expired、空 ExpireTime 不判过期 / 失败分支不发奖不写去重集合 + TextIdFor 六类各返非 0 互不相同 textId。
- 发奖 G1–G3:货币道具(use_effect=1)落 state 对应字段正确数量 / 一码多奖 Granted 列表对应配置 / state==null 仍 Success 且 Granted 含产出结构不抛。
- 回归 R1–R2:编译 0 error + 现有 EditMode 全绿(零回归)+ 新测全绿 / Code Review 5 红线(重点核「无真实网络调用」「PlayerPrefs 非阻塞不触同步 IO」「发奖复用 16 不复制落点」)。

**Play / 未来轮遗留(boss 授权,不在本轮验收)**:真实服务器校验(无网络模块,未来实现 RemoteRedeemValidator)/ 兑换码输入窗口 + 结果弹窗 UI + 真实 Sprite / 设置界面兑换码入口按钮接线。

**待拍板(均取安全默认,boss 自治授权内,decisions 已记;要改另开增量)**:O1 真实服务器校验 stub / O2 奖励复用道具 id×数量 / O3 非自动道具不接背包实例 / O4 限时码可注入 NowProvider 默认系统时钟 / O5 全局限量码不做(需后端) / O6 文案 textId 占位 / O7 UI 延后 / O8 仅 trim+大写规整不做格式预检。
