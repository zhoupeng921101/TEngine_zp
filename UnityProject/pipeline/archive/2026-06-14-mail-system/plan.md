# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:mail 通用邮件系统·数据逻辑层 + 服务器/运营接缝(自治·放手默认)

设计稿已出:`design-docs/21-mail-system.html`(nav.js GROUPS 已加一行,全库 22 侧边栏自动同步;浏览器预览确认侧边栏 active 正、本页目录 19 项含立项信息置顶、零 JS 错)。

### 交接区(交开发)

**中心**:命名空间 `GameLogic.Mail` 收件箱数据逻辑层 + 两道接缝。全部落 `GameScripts/HotFix/GameLogic`(热更区),配置桥接归 `GameLogic.Config`。加法式:不改框架、不另造发奖/存储栈,发奖复用 16 礼包库+落点、持久化复用既有 Provider、展示复用 17 RewardView。设计正文与逐条验收完成定义见 `21-mail-system.html` §三/§五/§六;本节给 dev 定位锚点。

**新建符号(§五 dev 改动清单)**:
- `Config/MailConfigMgr.cs`:桥接 Luban,`EnsureLoaded`(走 `ConfigSystem.Instance.Tables`,范本 `ItemConfigMgr`)+ `GetMail(id)` + `Global`(缺省返默认 100/30)+ `InitForTest`/`ResetForTest`。
- `Module/Mail/MailDef.cs`:`MailDef`(Id/TitleTextId/BodyTextId/ExpireDays/RewardPoolId)+ `MailGlobalConfig`(MaxCount=100/RetainDays=30)POCO。
- `Module/Mail/`:`MailItem`(模型,含 getter `HasReward`/`HasRedDot`/`CanDelete`,时间存 `long SendTimeTicks`)、`MailDraft`(收件入参 + `FromTemplate`)、`MailInboxSave`(`[Serializable]` DTO + version)、`ClaimStatus`/`ClaimResult`、`MailText`(textId 占位)。
- `Module/Mail/`:`IMailPersistence`/`MailPersistence`(键 `Mail.Inbox`,`JsonUtility` 序列化,范本 `MergeMetaPersistence`)/`InMemoryMailPersistence`。
- `Module/Mail/`:`IMailService`(对外收件 API,本轮真做)/`MailboxService`(收件/列表/读/领单+一键/删/清理/红点;注入 `NowProvider`+`RngProvider`+`IMailPersistence`)。
- `Module/Mail/`:`IMailSource`/`InertMailSource`(运营接缝 stub,离线返空不连网)。
- `Configs/GameConfig/Datas/mail.xlsx`:邮件模板表(id/title/desc/expire_days/reward_id,5 条 demo 原样录入 reward=1002/expire=14)+ 全局配置表(max_count/retain_days)。导表带 `DOTNET_ROLL_FORWARD=Major`(boss 遗留 #18)。
- `Assets/Editor/Tests/BlockBlast/MailSystemTests.cs`:新建测试。

**复用既有(经 grep 核实,不改)**:
- 发奖:`GameLogic.BlockBlast.Item.GiftOpener.OpenRandom(index,times,rng)`(`Item/GiftOpener.cs`)→ `GiftEntry.ItemId/Num` → `ItemConfigMgr.GetItem(id)` → `ItemGrant.GrantOnAcquire(def,num,state,rng)`(`Item/ItemGrant.cs`)。`MaxGiftDepth=5` 已防礼包递归环。
- 持久化:`GameLogic.BlockBlast.IPersistenceProvider` + `Persistence.Provider`(`Persistence.cs`,生产 PlayerPrefs/测试 `InMemoryPersistenceProvider`)。
- 序列化范本:`MergeMetaPersistence.Serialize/Deserialize`(`MergeMeta*.cs`,JsonUtility + null 保底)。
- 展示(可选接):`GameLogic.BlockBlast.Reward.RewardView`(`Reward/RewardView.cs`)。

**关键设计点(dev 须照做)**:
1. 奖励附件 = 礼包随机库 id(spec「Reward表id=奖励随机库表id」),非道具 id 列表;领取经 `GiftOpener.OpenRandom(reward_id,1,rng)` 抽奖。
2. 两道接缝区分:`IMailService.Send`(游戏内发奖入口,本轮真实实现,下轮排行榜接它)vs `IMailSource`(外部服务器/运营推送,stub 离线 inert)。Code Review 核「无 UnityWebRequest/HttpClient」。
3. 删除前置:`CanDelete = Read && (!HasReward || Claimed)`(spec「已读且奖励已领或无奖励」)。
4. 领取顺序:抽奖落点成功 → 标已领+已读 → 落盘;二次领 `AlreadyClaimed` 不重发。一键 `ClaimAll`:有奖未领未过期的全领、全部邮件标已读。
5. 排序:未读置顶(`Read?1:0` 升序),组内 `SendTimeTicks` 降序。
6. 自动清理两条(注入 `NowProvider`):过期(SendTime+有效期天<today,有效期=`ExpireDays>0?ExpireDays:Global.RetainDays`)+ 超量(>MaxCount 删最早,留最新 N)。收件后+列表前+红点查询前触发。
7. 时间存 `long Ticks`(DateTime 非 JsonUtility 友好);红点/可删用 getter 表达不进盘。
8. 持久化 Load 对无键/空串/非法 JSON 统一产空列表(try/catch 包 FromJson),不抛。

**验收强度(§六,25 条)**:配置 C1-3 / 收件 N1-2 / 排序 SO1-2 / 已读 RD1 / 领取 CL1-4 / 删除 DEL1-2 / 清理 CU1-3 / 红点 RDOT1-2 / 持久化 P1-2 / 接缝 SK1-2 / 回归编译 R1-2。纯逻辑全 EditMode 可测(POCO + InitForTest + InMemoryMailPersistence + 注入 NowProvider);Luban 直读 C3 工具链不可达列 BLOCKED 不判 FAIL,纯逻辑条须全绿。零回归 + Code Review 5 红线(重点:无真实网络、PlayerPrefs/JsonUtility 非阻塞、发奖复用 16、持久化复用既有 Provider)。

**本轮不做(boss 授权遗留)**:真实服务器后台发删/定时邮件(无网络模块)、区服多选(离线单区)、邮件界面/详情/无邮件态/全部删除确认 UI、红点显示、邮件 icon、奖励真实 Sprite、主界面入口接线、多语言真实查表(textId 占位)→ 表现层延后轮 + 远程未来轮。详见 §七 待拍板清单 O1-O8(均取安全默认,boss 预先拍板内)。

### 自治决策(范围开关取默认推进,记 decisions 供 boss 复核)

- 超量计数口径(O4):取「总条数上限 N」(spec 同写「总上限 N」与「未领取数>N」两句;总条数上限语义清晰、与「插尾+删最早」FIFO 一致;若只数未领取则已读已领旧邮件不占名额、收件箱可无限堆,反不符「总上限」)。要改换 `CleanupOverflow` 判据。
- 排序「已读>未读」解读为未读置顶(玩家先看新内容),已读沉底。
- 邮件 id 本地服务分配(O8,自增/时间戳);未来服务器邮件用服务器下发全局 id。
- 奖励附件复用礼包随机库 id 而非自定义结构(spec 原文「奖励随机库表id」);最终汇到 16 `ItemGrant`,与兑换码 20 同源不同入口。
- 全局配置默认 maxCount=100/retainDays=30(spec 默认值),`MailConfigMgr.Global` 表缺省时兜底返默认。
