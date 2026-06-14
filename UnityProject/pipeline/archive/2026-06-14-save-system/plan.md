# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:save-system(MergeOrderState 跨会话磁盘存档)

设计稿:`design-docs/14-save-system.html`(已挂 index.html 卡片 + 9 篇 sidebar 文档树同步 + 全库断链检查通过)。
本篇兑现设计 13 §七 O3 待办,已在 13 篇 §四加勘误 callout 指向 14。

### 设计要点(给 dev 定位,细节读设计稿)
- 三层分层:序列化层(纯逻辑同步,可单测)/ 存储层(磁盘 IO 异步 UniTask 外壳)/ 时机层(窗口生命周期)。复用工程既有 `Persistence.Provider` 接缝(生产 PlayerPrefs / 测试 InMemory),不自造存储栈。
- 持久化边界:只存元层进度,默认不存局内瞬态(棋盘/手牌/订单/合成区/悔棋栈每局重开)。进盘 13 字段见设计稿 §3.1 表。
- 悔棋快照(`MergeOrderState.Snapshot`)不动,与磁盘存档两条独立轨,互不调用。

### 验收标准(test 逐条核对;全部锚在同步纯方法 + InMemory Provider,不依赖真实磁盘/不依赖 UniTask 运行)
对应设计稿 §六 A1–A14:

| # | 验收点 | 完成定义 |
|---|--------|----------|
| A1 | 序列化往返保真 | 含全元字段非缺省值 state → ExportMeta → Serialize → Deserialize → ImportMeta 到新 state,§3.1「是」的 13 项逐一相等(同日无跨天干扰) |
| A2 | bool[12] 往返保真 | TempleRepaired/TempleDecorated 设若干 true 往返后长度仍 12 且逐位相等 |
| A3 | 无存档→缺省 | Provider 空时 Deserialize(null/"") 返回缺省;ImportMeta 产出与 Reset() 一致(Piety=0、GoddessLevel=1、神庙全未修、UnlockedChapter=0) |
| A4 | 缺字段补缺(version 匹配) | 手造缺 templeRepaired/goddessLevel 的 JSON → 加载后数组重建长 12 全 false、GoddessLevel==1,不抛 |
| A5 | 旧版本迁移 | version<CurrentVersion 的档 → 字段补缺合理、version 归一,无异常 |
| A6 | 未来版本降级重置 | version>CurrentVersion → 走缺省重置(等价首次),不用错位数据 |
| A7 | 非法/截断档兜底 | 非 JSON / 字段越界(nextRepairIndex=99、goddessLevel=0)→ ImportMeta 夹值到合法不变量(index∈[0,12]、level≥1),不抛、不污染玩法 |
| A8 | 跨天重置祈愿 | wishUsedToday=3 + lastWishResetDate=昨天 → 以「今天」ImportMeta → WishUsedToday==0 且日期更新今天 |
| A9 | 同日不重置祈愿 | wishUsedToday=2 + lastWishResetDate=今天 → ImportMeta → WishUsedToday==2 |
| A10 | 缺日期字段宽松重置 | 无 lastWishResetDate(旧档)→ ImportMeta → WishUsedToday==0 且日期设今天 |
| A11 | 悔棋快照不受影响 | 现有 MergeOrderTests/TempleRepairTests/TarotBlindBoxTests 悔棋(Capture/Undo)仍绿;ImportMeta 不触 _undoStack |
| A12 | 旧路径零回归 | 现有 168 例 EditMode 全绿;不进 merge-order/不调存档时 MergeOrderState 行为与改动前完全一致 |
| A13 | MergeOrderState 仍纯逻辑 | 不 using UniTask / 不含磁盘 IO 调用(ExportMeta/ImportMeta 可在纯 C# 单测同步调用) |
| A14 | 工程编译通过 | 含 SaveAsync/LoadAsync 异步外壳在内,GameLogic 程序集编译无错(异步外壳正确性靠编译+人工冒烟,非 EditMode 断言) |

### 涉及模块 / 改动清单(符号名经 grep `Module/BlockBlast/` 与 `UI/BlockBlastUI/` 核实,设计稿 §五)
1. 新增 `Module/BlockBlast/MergeMetaSave.cs`:`[Serializable]` DTO,字段含 version + 13 进盘字段 + lastWishResetDate(见设计稿 §3.2)
2. 新增 `Module/BlockBlast/MergeMetaPersistence.cs`:静态类,`CurrentVersion=1` + 同步纯方法 Serialize/Deserialize/Migrate/ApplyDailyReset + 异步外壳 SaveAsync/LoadAsync(内部用 Persistence.Provider 或沙盒文件)
3. `MergeOrderState.ExportMeta()` 新方法:读元字段填 DTO,纯方法无 IO
4. `MergeOrderState.ImportMeta(MergeMetaSave, today)` 新方法:DTO 覆盖元字段 + 逐字段保底(§3.5)+ 跨天重置(§3.6),纯方法
5. `BlockGameState.ResetForMergeOrder(board)`(现 line 334):在 `MergeState.Reset()` 之后织入「加载存档则 ImportMeta 覆盖元层」。**唯一改动旧逻辑处**,Reset 须先跑(字段不重叠,只覆盖元层)
6. `UI/BlockBlastUI/MergeOrderWindow.cs`:元动作(交付/修复/开盒/祈愿/女神升档)后 RequestSave 标脏;OnDestroy/ExitMergeOrder 前 + OnApplicationPause/Quit 兜底落盘
7. 沙盒路径接入(或降级 PlayerPrefs):若选沙盒文件,dev 用 `unity_reflect` 核实 YooAsset/TEngine 沙盒根 API(O4)
8. 新增 `Editor/Tests/BlockBlast/MergeMetaSaveTests.cs`:覆盖 A1–A13,SetUp 注入 InMemory Provider 仿 TempleRepairTests

### dev/test 执行约束
- IO 异步(UniTask),CLAUDE.md 红线禁同步 IO:序列化层同步、磁盘外壳异步(设计稿 §3.3)
- 单测只测同步序列化层 + InMemory Provider 往返,不测真实文件 IO / 不依赖 UniTask 运行(EditMode 不碰沙盒文件)
- dev/test 带 unityMCP 自行编译/跑测;MCP 不可达 test 判 BLOCKED 不判 FAIL

### 待 dev 实现选型(设计稿 §七,均有安全默认,不阻塞)
- O4 生产存储介质:沙盒 JSON 文件(建议,任务点名)vs PlayerPrefs(降级备选,零成本)。两方案验收点不变
- O5 落盘节流:每次元动作即落盘(默认)vs 帧末去抖合并
- O2 TotalScore 进盘当累计总分(默认进盘);若与窗口本局分显示冲突可降级不进盘
