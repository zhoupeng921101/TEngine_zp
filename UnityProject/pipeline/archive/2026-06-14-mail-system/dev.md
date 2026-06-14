# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:mail 通用邮件系统·数据逻辑层 + 服务器/运营接缝(自治·放手默认)

设计基线 `design-docs/21-mail-system.html`,验收锚 plan.md §六 25 条。已实现 + 自检完成,交测试。

### 交接区(交测试)

#### 1. 改动摘要(做了什么 / 为何这么做 / 关键决策)

按 plan.md §五 dev 改动清单实现命名空间 `GameLogic.Mail` 收件箱数据逻辑层 + 两道接缝,加法式落 `GameScripts/HotFix/GameLogic`(热更区),配置桥接归 `GameLogic.Config`。发奖复用 16、持久化复用既有 Provider、展示走 17 RewardView(本层只产 GrantPayload,UI 接时自转)。严格照 plan.md「关键设计点 8 条」:

- **配置桥接** `MailConfigMgr`(仿 `ItemConfigMgr`/`RedeemConfigMgr`):`EnsureLoaded` 走 `ConfigSystem.Instance.Tables.TbMail/TbMailGlobal` 桥接 POCO;`GetMail(id)` 查不到返 null 不抛;`Global` 表缺省兜底返默认 100/30(`_global ??= new MailGlobalConfig()`);全局表取首行(单行表,index=id 固定 1);`InitForTest`/`ResetForTest` 绕 ConfigSystem。
- **模型**:`MailItem` 字段全 JsonUtility 友好(`long SendTimeTicks` 存时间),`HasReward`/`HasRedDot`/`CanDelete` 是 expression-bodied **getter 属性**——JsonUtility 不序列化属性,故不进盘(plan 关键点 7)。`MailDraft.FromTemplate` 从 `MailConfigMgr.GetMail` 复制字段,模板不存在返 null。`ClaimStatus`/`ClaimResult`(Granted 永非 null,失败返空集合)、`MailText`(占位 textId 110701-110705,互不相同)。
- **持久化** `MailPersistence`(键 `Mail.Inbox`,包既有 `Persistence.Provider`,`JsonUtility` 序列化 `MailInboxSave` 容器 + version=1)/`InMemoryMailPersistence`。`Load` 对无键/空串/非法 JSON 用 try/catch 统一产空列表不抛(plan 关键点 8,仿 14 `MergeMetaPersistence.Deserialize`)。InMemory 的 Load/Save 走列表深拷贝,使新建服务实例读到独立副本(模拟重启,P1 验收)。
- **收件箱服务** `MailboxService : IMailService`:收件 `Send`(对外 API,本轮真做)插尾 + 收件后清过期 + 清超量 + 落盘;`List` 未读置顶(`Read?1:0` 升序)+ 组内 SendTimeTicks 降序(用 `List.Sort` 自定义 comparer,不引 Linq——HotFix GameLogic 全栈无 Linq,保持一致);`MarkRead` 幂等;`DeleteRead` 前置 `CanDelete`(已读 && (无奖||已领));`Claim` 顺序「抽奖落点 → 标已领+已读 → 落盘」二次领 `AlreadyClaimed` 不重发;`ClaimAll` 先清过期、有奖未领未过期的全领、**全部邮件**标已读(spec);`HasUnreadOrUnclaimed`/`UnreadCount`/`UnclaimedCount` 红点 getter。
- **发奖** `GrantPool`(private)严格复用 16:`GiftOpener.OpenRandom(reward_id,1,rng)` → 每项 `ItemConfigMgr.GetItem` → `ItemGrant.GrantOnAcquire(def,num,state,rng)`,汇总 GrantPayload。池缺/道具未登记返空列表不抛;`state==null` 走纯解析(GrantOnAcquire 内部已处理)。
- **自动清理**(注入 `NowProvider`):`CleanupExpired`(有效期=`ExpireDays>0?ExpireDays:Global.RetainDays`,`SendTime+有效期 < now` 删,用 `new DateTime(ticks).AddDays(days)` 还原)+ `CleanupOverflow`(>MaxCount 按 SendTimeTicks 升序删最早留最新 N,O4 总条数口径);收件后 + 列表前 + 红点查询前 + ClaimAll 前触发。
- **运营接缝** `IMailSource`/`InertMailSource`:`Pull` 返 `Array.Empty<MailDraft>()`,不抛不连网(stub)。
- **配置表** `Configs/GameConfig/Datas/mail.xlsx`(邮件模板表 id 1-5 / title=110711-110715 / desc=110721-110725 / expire=14 / reward=1002,原样录入)+ `mail_global.xlsx`(单行 id=1 / max_count=100 / retain_days=30);注册 `__tables__.xlsx` 两行(`mail.TbMail` index=id / `mail.TbMailGlobal` index=id,均 read_schema_from_file=True、group 默认 c,s)。已导表(`DOTNET_ROLL_FORWARD=Major` 直调 Luban.dll,绕 .bat 的 pause,exit 0),生成 `mail_tbmail.bytes`/`mail_tbmailglobal.bytes` + 行类 `GameConfig.Mail`/`GameConfig.MailGlobal` + 表类 `GameConfig.mail.TbMail`/`TbMailGlobal` + 重生 `Tables.cs`(新增 TbMail/TbMailGlobal lazy 属性)。

**关键决策**:
- 排序、计数用显式循环 / `List.Sort` comparer,不引 `System.Linq`(HotFix GameLogic 全栈零 Linq,避免风格 + AOT 顾虑漂移)。
- `NextId` 用 `max(now.Ticks, lastId+1)` 单调自增,防同 tick 连发 id 冲突 + 重启后从已有最大 id 续号(O8 本地分配)。
- `CleanupOverflow` 对 `MaxCount<0` 视作不限(防误配把收件箱清空);`MaxCount==0` 时清空(符合「上限 0」语义)。
- `MailItem` 加 `[System.Serializable]`(JsonUtility 嵌套列表元素需要)。
- 文案 textId 用 110701-110705 真实占位值(非全 0),与 19/20 占位约定一致,验收互不相同。

#### 2. 文件清单

**新增(源码,热更区 GameLogic):**
- `Assets/GameScripts/HotFix/GameLogic/Module/Mail/MailDef.cs`(MailDef + MailGlobalConfig POCO)
- `Assets/GameScripts/HotFix/GameLogic/Module/Mail/MailItem.cs`(MailItem + MailDraft + ClaimStatus + ClaimResult + MailText)
- `Assets/GameScripts/HotFix/GameLogic/Module/Mail/MailPersistence.cs`(MailInboxSave + IMailPersistence + MailPersistence + InMemoryMailPersistence)
- `Assets/GameScripts/HotFix/GameLogic/Module/Mail/MailboxService.cs`(IMailService + MailboxService)
- `Assets/GameScripts/HotFix/GameLogic/Module/Mail/MailSource.cs`(IMailSource + InertMailSource)
- `Assets/GameScripts/HotFix/GameLogic/Config/MailConfigMgr.cs`(配置桥接)

**新增(测试):**
- `Assets/Editor/Tests/BlockBlast/MailSystemTests.cs`(24 个测试方法,覆盖 §六 25 条)

**新增(Luban 配置 + 生成物):**
- `Configs/GameConfig/Datas/mail.xlsx`、`Configs/GameConfig/Datas/mail_global.xlsx`
- `Assets/AssetRaw/Configs/bytes/mail_tbmail.bytes`(+ .meta)、`mail_tbmailglobal.bytes`(+ .meta)
- `Assets/GameScripts/HotFix/GameProto/GameConfig/Mail.cs`、`MailGlobal.cs`、`mail.TbMail.cs`、`mail.TbMailGlobal.cs`(Luban 自动生成)

**修改:**
- `Configs/GameConfig/Datas/__tables__.xlsx`(注册两表)
- `Assets/GameScripts/HotFix/GameProto/GameConfig/Tables.cs`(Luban 重生,新增 TbMail/TbMailGlobal lazy 属性)

**未改:** `GiftOpener`/`ItemGrant`/`ItemConfigMgr`/`Persistence`/框架代码(只调用不修改);UI 零改动(本轮不建窗口)。

#### 3. 验证点(逐条对应 §六 25 条;dev 已自跑全绿)

dev 已跑 `run_tests EditMode assembly=BlockBlast.Tests`:**335/335 全绿,0 失败 0 跳过**(含 24 个 Mail 新测,其中 C3 Luban 直读 PASS 非 BLOCKED——bytes 已导入可读)。原 311 套零回归。逐条:

- **C1-C3 配置**:`MailConfigMgr` InitForTest 往返 / Global 默认 100·30 与注入值 / **C3 Luban 直读 `mail_tbmail.bytes` 5 行 + reward=1002 + expire=14 + 全局 100/30**(本轮工具链可达,GREEN)。
- **N1-N2 收件**:Send +1 未读未领 SendTimeTicks==now / FromTemplate 复制字段 + 模板不存在返 null。
- **SO1-SO2 排序**:未读置顶 + 组内时间降序 / 混合 4 封 = [未读新,未读旧,已读新,已读旧]。
- **RD1 已读**:MarkRead 幂等 + UnreadCount 减。
- **CL1-CL4 领取**:有奖 Claim Success + 落点 Exp 增 + Claimed&&Read / 二次 AlreadyClaimed 不重发、无奖 NoReward、不存在 NotFound / ClaimAll 领有奖未领 + 全标已读 + 汇总 + 不重发 / state==null 仍 Success、库未登记 Granted 空仍 Success。
- **DEL1-DEL2 删除**:已读无奖/已读已领可删 / 未读、有奖未领拒删。
- **CU1-CU3 清理**:过期移除(含 ExpireDays<=0 用 RetainDays)/ 超量稳定 MaxCount 留最新 / 注入时钟今天无过期·一月后全过期。
- **RDOT1-RDOT2 红点**:全局红点状态矩阵 / 已读但未领仍亮、领后灭。
- **P1-P2 持久化**:跨实例往返读/领态保真 + 时间保真 / 无键·空串·非法 JSON 产空列表不抛 + 空收件箱可 Send。
- **SK1-SK2 接缝**:InertMailSource.Pull 空不抛 / IMailService.Send 经接口真实可用。
- **R1 编译/回归**:编译 0 error(read_console 无 CSxxxx,仅域重载期桥重连瞬态 `disposed object`,非编译错);335/335 含新测全绿,零回归。
- **R2 Code Review 5 红线**(test 复核重点):
  - 无真实网络:`grep UnityWebRequest|HttpClient|WebRequest|System.Net|Socket|WWW` 在 Mail 目录 0 命中(dev 已查,test 可全系统复 grep,锚 SK1)。
  - 非阻塞 IO:持久化走 PlayerPrefs KV(同 14/19/20,不触禁同步 IO 红线);本层无 LoadAssetAsync。
  - 发奖复用 16:`GrantPool` 调 `GiftOpener.OpenRandom`→`ItemConfigMgr.GetItem`→`ItemGrant.GrantOnAcquire`,无复制落点(锚 CL1/CL4)。
  - 持久化复用既有 Provider:`MailPersistence` 包 `Persistence.Provider` 键 `Mail.Inbox`,无第二套存储栈(锚 P1)。
  - 热更边界:全部源码在 `GameScripts/HotFix/GameLogic`;模块访问/事件/资源释放本层 N/A(无引擎模块访问、无事件、无资源加载)。

#### 4. 标注(给测试 / boss)

- **涉及热更程序集**:是。新增源码全在 `GameScripts/HotFix/GameLogic`(GameLogic asmdef)+ `GameProto/GameConfig`(GameProto asmdef,Luban 生成)。正式出包须 HybridCLR 重生热更 dll(同 boss 遗留 #2 口径)。Editor 直跑无需额外步骤。
- **需 Luban 重生成**:本轮已重生(exit 0,bytes + 代码已落)。CI / 他人复跑导表须带 `DOTNET_ROLL_FORWARD=Major`(boss 遗留 #18)。
- **需进 Play 模式手验的功能点**:无。本层纯逻辑 + 配置,已被 24 例 EditMode + Luban 直读全覆盖。UI 表现层(邮件界面/详情/红点显示/icon)本轮不做(plan §七 O7,转后续轮 + 美术),无 Play 手验项。
- **test 建议**:① 全量 `run_tests EditMode`(锚 R1 零回归 + 335 含新测);② 全系统复 grep 网络 API(R2/SK1);③ Code Review 5 红线对照本节;④ 交叉检 conventions lint(dev 改过的持久文件)。
