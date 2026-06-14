# 关单总结:piety-temple-repair — 虔诚币 + 神庙修复主线(2026-06-14;自治模式·放手默认)

**结论:PASS 交付**。baton=full(plan→dev→test),**打回轮次 0**(一轮过,plan 零 blocker)。git 基线 commit `84b4c4d4`(开跑前自动提交)。设计基线 `design-docs/13-piety-temple-repair.html`(§六 T1–T12 + 验收标准见同目录 plan.md)。

## 运行验证(test 子会话经 MCP 直跑,桥连 `UnityProject@02a6dcaa`)
- 编译 0 CS error;HybridCLR 热更程序集 GameLogic 实测可加载(TempleConfig/MergeOrderState/TempleWindow/MergeOrderWindow 四型解析到)。
- EditMode `BlockBlast.Tests` **168/168 全绿**(基线 149 + 新增 19),零回归(job c451c49d)。
- Play 手验经真实 UI 路径(主菜单 BtnMerge → 对局 → 神庙按钮 → TempleWindow → repair_0 → 关窗):虔诚币显示/刷新、12 厅四态、修复扣币+发奖+升级、叠层不丢局、关窗回调刷新底层,全符合。截图 `Assets/Screenshots/temple_window_repaired.png`。
- Code Review 5 红线 + 命名全过;test 对 dev 的 `state/dev.md` 跑 conventions lint 0 命中。

## 实现范围
加法式引入第二货币虔诚币 + 神庙修复 + 经验/守护者等级,完全不动现有 灵力(Soul) 经济与核心循环。文件:4 新增(`TempleConfig.cs`、`TempleWindow.cs`、`TempleWindow.prefab` GUID `6db15220feb043ab99c2207556c617ea`、`TempleRepairTests.cs` 19 例)+ 2 改(`MergeOrderState.cs` 主线字段/方法/快照/Deliver·DeliverSpecial 发币、`MergeOrderWindow.cs` 虔诚币显示 + 神庙入口)。数值硬编码进 `TempleConfig`,不接 Luban。

## 拍板归属
- **范围 = boss 放手默认自定**(2026-06-14):虔诚币 + 神庙修复 + 经验/等级主线,加法式不重构 灵力。
- **plan/dev/test 自主拍板 20 条**(放手默认,无一上交用户),全文见同目录 plan.md/dev.md/test.md。关键:
  - 虔诚币与 灵力 并存、出口互斥(灵力兑体力/养成,虔诚币唯一出口=修神庙);订单发币 = 难度×30,特殊×2,挂现有发奖后纯追加。
  - 12 厅大阿尔卡那命名,造价 `500+i×250`(累计 22500);顺序解锁(严格 `index==NextRepairIndex`);修复发奖 经验=造价(1:1)+体力 30+装饰 bool。
  - 守护者等级 = 经验纯函数(不存独立等级值),`500+(L-1)×300`;修完 12 厅≈11 级。升级解锁只做体力 + 章节计数标记(金币/图案包不做:工程无金币、图案包触去变现红线)。
  - 神庙面板 `TempleWindow` 叠层开在 MergeOrderWindow 顶部(不丢局),关窗经 UserData Action 回调刷新底层;主菜单入口本轮省(可选低优)。
  - prefab 照抄 MergeOrderWindow.prefab 仅改 m_Name + 新 GUID(AssetRaw/UI 按文件名寻址)。

## 模型档 / 运行登记
- plan=opus、dev=opus、test=opus。
- Workflow `pipeline-auto` Run `wf_09b9a592-e28`(Task `war816mro`),baton=full,一轮 PASS,3 agent / 约 45 万 token / 31 分钟。

## 遗留/观察(转主 boss.md 遗留事项)
- **持久化单局尺度(关键·非缺陷)**:`MergeOrderState` 整体不入磁盘、只入悔棋快照(与 `_soul`/`_blindBoxCount` 一致),故虔诚币/神庙/经验/等级这条「长期主线」当前实为单局尺度——与 GDD「长期主线」字面有落差。本轮按现状(设计稿 §七 O3 显式标注),跨会话存盘是独立大改。并入主 boss.md 遗留 #14。
- 预存 nit(非本轮):`MergeOrderState.DeliverSpecial` doc-comment(:357)残留「灵力」字样,git blame 证属 commit `0bbb7f01`,代码不调 AddSoul,不影响行为。
- DeliverSpecial 无 UI 入口属设计 11 遗留(主 boss.md #13),发币由数据层单测 T2 覆盖。
