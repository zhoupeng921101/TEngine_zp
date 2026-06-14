# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前被测任务:save-system 时机层补全(对齐设计 14 §3.4 触发表两处缺口)

总判定:**PASS**

验收基线:`design-docs/14-save-system.html` §3.4 存/读时机策略(触发表)。环境:UnityProject@02a6dcaa,unity-check 通过(场景 main 响应、按名绑定正确)。本轮只补两处时机层接线,存档核心(序列化/存储/A1–A14)上一轮已 PASS,留档于本文件末。

### 被测两处缺口(均对齐设计 §3.4,非新设计)

- **缺口①(§3.4 ① 有意义元变更后 RequestSave)**:`MergeOrderWindow.PlaceAndResolve` 结算后,若该手 `ClearSettlement.Settle` 改了进盘字段(全清推女神 `AdvanceGoddess` / 全清或连消阈值发盲盒 `AddBlindBox`),用谓词 `metaChangedBySettle = settle.AllClearRewarded || settle.GoddessLeveledUp || settle.BlindBoxGained > 0` 判定为真后 `MarkAndFlushSave()` 标脏 + 落盘。闭合「全清攒到女神/盲盒进度后无后续交付/开盒/修复就退出 → 丢这一手进度」。
- **缺口②(§3.4 ③ 退出/暂停/销毁兜底中的 OnApplicationPause/Quit)**:`UIWindow` 非 MonoBehaviour,Unity 的 `OnApplicationPause`/`Quit` 魔法方法不会在其子类触发。改为 `OnCreate` 订阅 `Utility.Unity.AddOnApplicationPauseListener(OnAppPause)`、`OnDestroy` 解订阅;`OnAppPause(bool pause){ if(pause) FlushSaveIfDirty(); }`。

### 四类验证逐项结果

#### 1. 编译验证 — PASS
- `refresh_unity` compile=request scope=scripts mode=force → 进入 compiling,再轮询 resulting_state=idle(编译 + 域重载完成,ready)。
- `read_console`(types=error/warning 过滤):**0 条**。
- `read_console` filter_text=CS:仅 1 条桥 host banner(`StdioBridgeHost started on port 6401`),0 个 CSxxxx 编译诊断。
- EditMode 测试能跑起来本身即证明 GameLogic 热更程序集编译通过。

#### 2. 单元测试 — PASS
- `run_tests` EditMode,assembly_names=["BlockBlast.Tests"],job 008b1860 succeeded,resultState=Passed。
- **190 passed / 0 failed / 0 skipped**,durationSeconds≈0.23(= 188 基线 + 2 新增时机层谓词例)。
- 新增 2 例(直测 `ClearSettlement.Settle` 谓词,纯同步、不经窗口、不依赖 UniTask):
  - `Settle_AllClear_ChangesPersistedMeta_AndPredicateIsTrue`:全清场景谓词为真且 BlindBoxCount/GoddessLevel 确实变 — 绿。
  - `Settle_NoClear_DoesNotChangePersistedMeta_AndPredicateIsFalse`:无消除场景谓词为假且进盘字段(盲盒/女神等级/好评条)不变 — 绿。
- 静态手推核验(配合源码确认谓词与设计一致):
  - `BoxComboThreshold=4`,新例单次 Settle ComboChain 仅 1→2(≠4),不触连消盲盒,故全清例 `box+1` 断言不脆(只来自全清分支的一次 AddBlindBox)。
  - `GoddessRatingGoal=10`,例设 GoddessRating=9 → `AdvanceGoddess` 推到 10 → 清零 + GoddessLevel+1 + 返回 true,`GoddessLeveledUp` 与 `level+1` 成立。
  - 无消除分支(lines<=0)`Settle` 直接返回 (…false,false,…,0),不调 AddBlindBox/AdvanceGoddess,谓词假、字段不变成立。

#### 3. 手动功能验证(Play 模式 execute_code,走生产 PlayerPrefsProvider)— PASS
两处时机层缺口是设计 §3.3 明确「靠 PlayMode/人工冒烟」的层,EditMode 谓词例只锁判定一致性,本环节经 execute_code 直击窗口落盘路径 + 真实存储栈。

> 重要前置:EditMode 测试 SetUp 把 `Persistence.Provider` 换成 InMemory 替身,跑完留为 live Provider 残留。本环节先反射还原为生产 `PlayerPrefsProvider` 再验(确认 `Provider.GetType().Name == "PlayerPrefsProvider"`),收尾再还原回 InMemory + 删验证键,不留生产写残留。

- **缺口① 全清结算 → 生产落盘往返(复刻 PlaceAndResolve 的 metaChangedBySettle 路径)**:
  - 构造 MergeState(Piety=333、AllClearArmed=true、GoddessRating=Goal-1),跑 `ClearSettlement.Settle(2,18,boardEmptyAfter=true,Diamond)`。
  - 谓词 metaChangedBySettle=True(AllClearRewarded=True / GoddessLeveledUp=True / BlindBoxGained=1);box 0→1、goddessLevel 1→2。
  - 复刻窗口 RequestSave → ExportMeta → ClearSaveDirty → SaveAsync:PlayerPrefs 键 `block_blast_merge_meta_v1` 真实写入,raw len=404,含 version/blindBoxCount/goddessLevel。
  - 退出重进模拟(new + Reset + Load + ImportMeta):reloaded Piety=333、box=1、lv=2、rating=0,MATCH box/level/piety 全真。**RESULT: PASS** — 全清这一手元进度即便无后续元动作,也可靠跨会话保真。
- **缺口② OnAppPause(true) 兜底落盘(复刻 FlushSaveIfDirty 路径)**:
  - 构造脏的 MergeState(Piety=9001、box=4、RequestSave 标脏),走 `if(pause==true && IsSaveDirty)` 落盘 → PlayerPrefs raw len=405,退出重进 reloaded Piety=9001、box=4,MATCH=True。**RESULT: PASS**。
- **缺口② 反向(OnAppPause(false) 恢复前台不落盘)**:
  - 清档后构造脏 state,走 `pause==false` 分支:PlayerPrefs 键不存在(HasKey=False)、state 仍 dirty(stillDirty=True)。**RESULT: PASS** — `if(pause)` 守卫正确,恢复前台不触发多余写盘。
- **真机 OnApplicationPause 事件真触发**:MCP 无法挂起编辑器进程模拟真机切后台,靠静态确认订阅链已接通(见 §4 code review 第 1 条),此点列「真机/人工冒烟」继续观察项,非本环节运行验证范围。

#### 4. Code Review — PASS
对照项目根 CLAUDE.md「核心原则(编码红线)」全部 5 条(以正本为准):
1. **异步优先/禁同步 IO**:符合。落盘走 `SaveAsync`(返回 UniTask)。本轮无新增同步 IO。订阅/解订阅是内存操作。
2. **模块访问 GameModule.XXX**:符合。窗口用 `GameModule.UI.*`;新增的 `Utility.Unity.AddOnApplicationPauseListener` 是 TEngine 公共工具静态接缝,非 `ModuleSystem.GetModule<T>()`。
3. **资源必须释放**:符合(本轮关键)。`OnCreate` 订阅、`OnDestroy` 解订阅,1:1 平衡 —— 已核 `UIWindow.InternalCreate` 用 `if(_isCreate==false)` 守卫 `OnCreate()` 单次触发,`CallDestroy→OnDestroy` 为其唯一对偶,无重复订阅、无泄漏。驱动器(`UpdateDriver` 内 `MainBehaviour : MonoBehaviour`,DontDestroyOnLoad 常驻)若不解订阅会持有已销毁窗口引用,dev 在 OnDestroy 解订阅正确规避。
4. **热更边界**:符合。改动全在 `GameScripts/HotFix/GameLogic`(MergeOrderWindow),无 Main 改动。
5. **事件解耦**:符合。订阅的是 TEngine 驱动器应用暂停事件(`Action<bool>`),非业务跨模块 GameEvent;`OnAppPause` 是窗口内私有方法。无事件风暴(暂停事件低频)、无泄漏(已解订阅)。

实现机制核验(缺口② 的字面指令偏差是否成立):
- 设计 §3.4 ③ 与文件清单 #6 字面写「OnApplicationPause/Quit 兜底」。dev 改为订阅驱动器事件,理由是 UIWindow 非 MonoBehaviour 直接加魔法方法是死代码。已 grep + Read 实证:`Utility.Unity.AddOnApplicationPauseListener(Action<bool>)`/`Remove…` 静态方法存在 → 转发到 `UpdateDriver` → 其内 `MainBehaviour : MonoBehaviour` 的 Unity 魔法方法 `OnApplicationPause(bool pauseStatus)`(UpdateDriver.cs:361)raise `OnApplicationPauseEvent`。订阅链真实接通、确会在真机切后台触发,设计意图(切后台/杀进程落盘)完整兑现 —— 实现层取舍成立,非设计有错。
- `OnApplicationQuit` 未单独接:正常退出/关窗经既有 `OnDestroy` 的 `FlushSaveIfDirty` 兜底覆盖;移动端杀进程的规范信号是 `OnApplicationPause(true)`(已接)。两路覆盖切后台与正常关窗,符合设计 §3.4 ③「保证随手退出不丢末次元变更」的意图。

其他 review:
- 命名:`OnAppPause`/`metaChangedBySettle`/`MarkAndFlushSave`/`FlushSaveIfDirty` 均公共词、望文生义,符合 naming-rules。
- 谓词正确性:`metaChangedBySettle` 用 `SettlementResult` 已暴露的 `AllClearRewarded`/`GoddessLeveledUp`/`BlindBoxGained` 三公共只读字段,无需改结算层;三者并列(非仅 AllClearRewarded)对「全清隐含女神+盲盒」是冗余保险,语义无害。
- 落盘顺序:`PlaceAndResolve` 的 `if(metaChangedBySettle) MarkAndFlushSave()` 在刷新之后、通关/GameOver 判定之前;后续 TriggerWin/GameOver 退出前的 FlushSaveIfDirty 此时为无操作(脏已清),无重复写、无丢失。OnDestroy 解订阅在 FlushSaveIfDirty 之前,暂停事件不会在销毁落盘后再触发。

持久文件交叉检(conventions.md):dev 改的 `pipeline/state/dev.md` 交接区跑 lint —— 指代词/diff 叙事 0 命中、拟人比喻 0 命中;人工通读「实现机制差异」旁注(line 16)脱离对话成立(外人读为「原来如此」,解释字面指令为何在本框架是死代码),合规。

### 给开发的复现清单
无 FAIL 项。两处时机层缺口均已闭合并经生产 PlayerPrefs 路径验通。

### 观察项(非 FAIL,留给 boss/真机冒烟)
- **真机 OnApplicationPause 触发**:MCP 无法挂起编辑器模拟真机切后台/锁屏,订阅链已静态确认接通(驱动器 MonoBehaviour 魔法方法 → 事件 → 窗口监听),真机/打包后切后台是否落盘建议人工真机冒烟一次终验。属设计 §3.3 声明的时机层人工冒烟范畴,不阻塞验收。
- **拖拽落子全链 Play 手验**:MCP 无法模拟指针拖拽(test memory),全清结算的窗口侧落盘已用 execute_code 复刻 PlaceAndResolve 谓词路径 + 生产 Provider 往返替代,逻辑等价。

---

## 上一轮(存档核心)记录(已 PASS,留档)

总判定:**PASS**。验收基线 `design-docs/14-save-system.html` A1–A14。环境 UnityProject@02a6dcaa。

- **编译**:refresh idle、read_console 0 编译诊断 0 warning。
- **单元测试**:EditMode BlockBlast.Tests **188 passed / 0 failed**(168 基线 + 20 新增 MergeMetaSaveTests),A1–A13 全绿;基线 TempleRepairTests/TarotBlindBoxTests/MergeOrderTests 全绿印证 A11(悔棋快照不受影响)、A12(旧路径零回归)。
- **手动功能验证**:Play 模式经生产 PlayerPrefsProvider 验跨会话往返保真、跨天祈愿重置、局内瞬态(O3 Energy)不随档。
- **Code Review**:5 条红线符合/N-A;命名公共词、事件无泄漏/风暴、分层正确(ExportMeta 深拷贝、ImportMeta 不触 _undoStack、逐字段保底夹值)、顺序风险已规避。
- **关键决策(plan §七待拍板,均取安全默认)**:O4 取 PlayerPrefsProvider 经既有 Persistence.Provider 接缝;O2 TotalScore 进盘当累计;O3 Energy 不进盘;O5 每次元动作即异步落盘;O1 局内棋盘断点续玩不做(任务边界)。
- 上一轮观察项(时机层缺口①②)即本轮被测内容,已闭合。
