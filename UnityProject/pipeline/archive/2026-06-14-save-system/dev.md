# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:save-system 时机层补全(对齐设计 14 §3.4 触发表两处缺口)

存档核心(序列化/存储/A1–A14)已 PASS(EditMode 188 全绿)。本轮只补设计 14 §3.4 触发表实现遗漏的两处时机层接线,使元进度在所有退出路径可靠落盘。设计基线 `design-docs/14-save-system.html`。

### 本轮改动摘要(时机层补全)

补两处时机层缺口,均对齐设计 14 §3.4 触发表,非新设计:

- **缺口①:全清结算改元层后落盘**。`MergeOrderWindow.PlaceAndResolve` 结算后,若该手 `ClearSettlement.Settle` 改了进盘字段(全清触发 `AdvanceGoddess` 女神升档 / `AddBlindBox` 盲盒,或连消阈值发盲盒),标脏 + 落盘。原实现只在交付/开盒/修复后落盘,故「全清攒到女神/盲盒进度后无后续元动作就退出」会丢这一手进度。判定谓词 `metaChangedBySettle = settle.AllClearRewarded || settle.GoddessLeveledUp || settle.BlindBoxGained > 0`(`SettlementResult` 已暴露这三个标志,无需改结算层)。
- **缺口②:应用切后台/退出落盘**。`UIWindow` 非 MonoBehaviour(它是 `UIBase` 派生的普通类,生命周期由 `UIModule` 经 `Internal*` 方法手动驱动),Unity 的 `OnApplicationPause(bool)` / `OnApplicationQuit()` 魔法方法不会在其子类触发——直接加这两个方法会编译通过但永不被调用。改为订阅 TEngine 驱动器(`UpdateDriver`,真 MonoBehaviour,捕获 Unity 应用暂停后经事件转播)的应用暂停事件:`OnCreate` 里 `Utility.Unity.AddOnApplicationPauseListener(OnAppPause)`、`OnDestroy` 里 `RemoveOnApplicationPauseListener`;`OnAppPause(bool pause){ if(pause) FlushSaveIfDirty(); }`。`pause=true` = 进入后台(移动端切后台/锁屏),是移动端杀进程前的规范信号。正常关窗/退出经既有 `OnDestroy` 的 `FlushSaveIfDirty` 兜底,两路覆盖切后台与关窗。

> 缺口②的设计意图(切后台/杀进程落盘)由订阅驱动器事件完整兑现;与附加指令字面写的「加 OnApplicationPause/OnApplicationQuit 方法」的差异仅在实现机制——字面写法假设窗口是 MonoBehaviour,在本框架下是死代码。这是实现层取舍,非设计本身有错。

### 本轮文件清单

修改:
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWindow.cs` — ①`PlaceAndResolve` 加 `metaChangedBySettle` 判定 + 结算刷新后 `if(metaChangedBySettle) MarkAndFlushSave()`;②`OnCreate` 订阅 / `OnDestroy` 解订阅 `Utility.Unity` 应用暂停事件 + 新增私有 `OnAppPause(bool)`

新增测试:
- `Assets/Editor/Tests/BlockBlast/MergeMetaSaveTests.cs` 追加 2 例:`Settle_AllClear_ChangesPersistedMeta_AndPredicateIsTrue`(全清结算 → 谓词真且 blindBoxCount/goddessLevel 确实变)、`Settle_NoClear_DoesNotChangePersistedMeta_AndPredicateIsFalse`(无消除 → 谓词假且进盘字段不变)。纯同步,经 `ClearSettlement.Settle` 直测谓词,不经窗口、不依赖 UniTask

### 本轮验证点(给测试)

- **EditMode `BlockBlast.Tests` 零回归 + 2 新例**:`run_tests` EditMode,assembly_names=["BlockBlast.Tests"]。预期 190 passed / 0 failed(= 188 基线 + 2 新增)。dev 本地已跑:190/190 passed,0 failed,durationSeconds≈0.23。
- **缺口① 单测断言**:全清场景下谓词为真且 `BlindBoxCount`/`GoddessLevel` 确实改变;无消除场景谓词为假且进盘字段不变。这两例锁住「结算改元层 ⇔ 须落盘」的判定一致性。
- **缺口② PlayMode 冒烟(EditMode 范围外,须人工)**:①进 merge-order → 全清攒女神/盲盒 → 直接退出(不交付/开盒)→ 重进,女神好评条/盲盒计数不丢;②进 merge-order 后切后台(移动端 Home / 编辑器失焦触发 OnApplicationPause(true))→ 元层脏则落盘 → 回前台/重进不丢。订阅是否在真机触发须人工验。
- **标注**:不涉及 Luban 重生成;改动全在 `GameScripts/HotFix/GameLogic`(热更边界内),涉及热更程序集;无 Main 改动。

### 本轮 dev 自检结果

- unity-check 通过:绑定 `UnityProject@02a6dcaa`,场景 `main` 响应正常。
- 强制编译(`refresh_unity` compile=request scope=scripts)+ 域重载完成、recovered_from_disconnect 后 ready。
- `read_console` error 过滤:0 条;`CS` 过滤仅 1 条桥 host banner(非编译诊断),0 个 CSxxxx。编译 0 报错。
- `run_tests` EditMode BlockBlast.Tests:**190/190 passed,0 failed**(job succeeded)。

---

## 上一轮(存档核心)记录(已 PASS,留档)

设计基线 `design-docs/14-save-system.html`;验收标准见 plan.md 交接区 A1–A14。

### 改动摘要(做了什么 + 为何 + 关键决策)

按设计三层分层落地跨会话磁盘存档,把 MergeOrderState 元层进度(虔诚币/神庙/经验/灵力/盲盒/女神/订单数/今日祈愿)从单局尺度升为跨会话尺度。

- **序列化层(纯逻辑、可单测、不碰磁盘)**:新增 `MergeMetaSave` DTO + `MergeMetaPersistence` 静态类的同步纯方法 `Serialize/Deserialize/Migrate/ApplyDailyReset`,以及 `MergeOrderState.ExportMeta/ImportMeta`。断言全落在 string / DTO,单测同步调用、不依赖 UniTask 运行。
- **存储层(IO 异步)**:`MergeMetaPersistence.SaveAsync/LoadAsync` 是 `UniTask` 外壳,经既有 `Persistence.Provider`(生产 PlayerPrefs / 测试 InMemory)读写,沿用工程既有持久化接缝,不引入新存储栈。
- **时机层(窗口生命周期)**:元动作后标脏 `RequestSave` + 异步落盘 `SaveAsync().Forget()`;退出/通关/GameOver/OnDestroy 前兜底落盘;进入模式经 `ResetForMergeOrder` 同步加载覆盖元层。

关键决策(plan §七待拍板,均取安全默认自主拍板):
- **O4 存储介质**:取 `PlayerPrefsProvider`(经既有 `Persistence.Provider` 接缝),非沙盒文件。理由:既有 BlockGameState/DynamicWeightDiff 同款、零新存储栈、PlayerPrefs 读写非阻塞不触红线、InMemory 可注入测试。沙盒文件路径是独立升级轮次(设计 §七 O4 已注明两方案验收点不变)。
- **O2 TotalScore**:进盘当累计总分(默认,加法式无害)。
- **O3 Energy**:不进盘(局内瞬态,每局 ResetForMergeOrder 回 EnergyStart;跨会话保留会成养体力漏洞)。
- **O5 落盘节流**:每次元动作结束即异步落盘(默认,元动作频率低,够用)。
- **O1 局内棋盘断点续玩**:不做(任务边界,只存元层)。
- **加载路径同步、落盘路径异步**:加载织进 `ResetForMergeOrder`(同步 void,不便改 async),走同步 `MergeMetaPersistence.Load`(经 Provider 的非阻塞内存级读,与既有 BlockGameState.Load 同口径,不触「禁阻塞 IO」红线);写盘走 `SaveAsync` UniTask 外壳,满足异步红线。MergeOrderState 本身不 `using` UniTask、不碰磁盘,保持纯逻辑(A13)。

### 文件清单

新增:
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/MergeMetaSave.cs` — `[Serializable]` DTO,version + 13 进盘字段 + lastWishResetDate
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/MergeMetaPersistence.cs` — 静态类:CurrentVersion=1 + StorageKey="block_blast_merge_meta_v1" + 同步纯方法(Serialize/Deserialize/Migrate/ApplyDailyReset/Today)+ 同步 Load + 异步 SaveAsync/LoadAsync + Clear
- `Assets/Editor/Tests/BlockBlast/MergeMetaSaveTests.cs` — 20 个 [Test],覆盖 A1–A13(SetUp 注入 InMemory Provider,仿 TempleRepairTests)

修改:
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/MergeOrderState.cs` — 新增 ExportMeta/ImportMeta(纯方法)+ 落盘脏位 RequestSave/IsSaveDirty/ClearSaveDirty + 私有 NormalizeTempleArray 保底。**未动 Snapshot 悔棋类**(脏位不进快照、不进 Export/Import)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/BlockGameState.cs` — `ResetForMergeOrder` 在 `MergeState.Reset()` 之后织入 `MergeMetaPersistence.Load()` → `ImportMeta` 覆盖元层(唯一改动旧逻辑处;无存档时等价首次游玩,零回归)
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWindow.cs` — 交付(OnDeliverClicked)/开盒(OnOpenBoxClicked)后 MarkAndFlushSave;OnDestroy/TriggerWin/TriggerGameOver 前 FlushSaveIfDirty 兜底落盘;加 `using Cysharp.Threading.Tasks`
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/TempleWindow.cs` — 修复(OnRepairClicked)后标脏 + 异步落盘(与 MergeOrderWindow 共享同一 MergeState 引用);加 `using Cysharp.Threading.Tasks`

### 验证点(对应 plan 验收标准,告诉测试该验什么/怎么验/预期)

- **A1–A13 已由新单测 MergeMetaSaveTests 覆盖,本地全绿**。测试方法名一一对应:A1=`A1_RoundTrip_AllMetaFields_Equal_SameDay`、A2=`A2_BoolArrays_RoundTrip_Length12_BitwiseEqual`、A3=`A3_NoSave_Deserialize_ReturnsNull`+`A3_ImportNull_KeepsResetDefaults`、A4=`A4_MissingFields_...`、A5=`A5_OldVersion_Migrate_...`、A6=`A6_FutureVersion_...`、A7=`A7_GarbageJson_...`+`A7_OutOfRangeFields_...`+`A7_NegativeIndex_...`、A8=`A8_CrossDay_...`、A9=`A9_SameDay_...`、A10=`A10_MissingDate_...`、A11=`A11_ImportMeta_DoesNotTouchUndoStack`、A12=`A12_FreshLoad_...`+`A12_StorageLayer_RoundTrip_ViaProvider`+`A12_LoadedSave_OverwritesMetaOnEnter`、A13=`A13_ExportImport_AreSyncPureMethods`+`A13_DirtyFlag_...`,另加 `ApplyDailyReset_PureFunction_Behaviors`。
- **怎么验**:`run_tests` EditMode,assembly_names=["BlockBlast.Tests"]。预期 188 passed / 0 failed(= 168 基线 + 20 新增,A12 零回归靠基线全绿兜底)。
- **A14 工程编译**:含 SaveAsync/LoadAsync 异步外壳在内,GameLogic 程序集编译无错 → EditMode 能跑起来即证明相关程序集已编译通过(最强编译自检)。异步外壳正确性靠编译 + 人工冒烟,非 EditMode 断言。

### dev 自检结果(已做)

- unity-check 通过:绑定 UnityProject@02a6dcaa,场景响应正常。
- `run_tests` EditMode BlockBlast.Tests:**188/188 passed,0 failed**(job succeeded,durationSeconds≈2.29)。
- `read_console` error 过滤:仅 1 条 TestResults.xml 保存日志(非编译错),0 个 CSxxxx 编译诊断、0 warning。
- 编译 0 报错、域重载完成、非 Play 模式。

### 标注(给测试 + boss)

- **不涉及 Luban 重生成**(纯硬编码,不接配置表)。
- **涉及热更程序集**:改动全在 `GameScripts/HotFix/GameLogic`(热更边界内),无 Main 改动。
- **需进 Play 模式人工冒烟的功能点**(EditMode 验收范围外,设计 §3.3 已声明):①真机/Play 模式下进入 merge-order → 交付攒虔诚币 → 退出重进,元层不清零;②TempleWindow 修复后退回 MergeOrderWindow 虔诚币正确;③异步 SaveAsync().Forget() 落盘不阻塞主线程。这三点靠人工冒烟,非本轮 EditMode 断言。
- **加载路径同步说明(供 code review)**:`ResetForMergeOrder` 走同步 `MergeMetaPersistence.Load`(经 Provider 非阻塞读),写盘走异步 `SaveAsync`。红线针对阻塞 IO,PlayerPrefs 读写非阻塞;若 code review 认为加载也须 UniTask,可把 OnCreate 改 async 并 await LoadAsync——但当前同步加载与既有 BlockGameState.Load 同口径,未触红线。
