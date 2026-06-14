# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前被测任务:虔诚币 + 神庙修复 长期主线(design 13)

验收判据 `design-docs/13-piety-temple-repair.html`,验收标准 `pipeline/state/plan.md` §验收标准 T1–T12。

### 总判定:PASS

四类验证全通过。UnityMCP 连到正确实例(UnityProject@02a6dcaa,场景 main),四类全在 Editor 内运行验证,无环境阻塞。

### 验证环境

- `/unity-check` 三步通过:server 9.7.1 在跑、active_instance=UnityProject@02a6dcaa、`manage_scene get_active` 返回 main 场景,绑定实例响应。

---

### 1. 编译验证 — PASS

- `refresh_unity` 返回 `resulting_state:"idle"`(编译完成、未在 isCompiling)。
- `read_console`(error/warning)0 条 CS 编译诊断。控制台仅 3 条 test-framework 生命周期消息(IPrebuildSetup/IPostBuildCleanup/TestResults.xml 保存通知)+ 1 条 MCP 桥消息(`McpLog.cs` "Cannot access a disposed object",MCP 基础设施日志,非游戏侧)。
- HybridCLR 热更程序集 GameLogic 在 Play 模式实测可加载:TempleConfig / MergeOrderState / TempleWindow / MergeOrderWindow 四型均从 GameLogic 程序集解析到,证热更编译 + 类型导入通过。

### 2. 单元测试 — PASS

- `run_tests(EditMode, filter=BlockBlast.Tests)` job c451c49d:**168/168 passed,0 failed,0 skipped**,resultState=Passed,耗时 2.19s。
- 基线 149 + 新增 19 = 168,零回归(plan 基线 149 与 dev 自跑数一致)。
- 新增用例 `Assets/Editor/Tests/BlockBlast/TempleRepairTests.cs` 覆盖 T1–T12 + 配置自洽:
  - T1 普通订单逐档发币(30/60/120/240)+ 旧奖励不变;T2 特殊订单 ×倍率(480)+ 盲盒附赠不变;T3 不可交付不发币;
  - T4 三前置门控 + 已修回修 false + 失败状态不变;T5 扣币标记推进;T6 经验=造价 + 体力满血不溢出;
  - T7 等级曲线逐档(0→1、500→2、1300→3、2400→4、3800→5、5500→6、7500→7);T8 单级 + 真跨多级(Cost11=3250→Lv4 跨 3 级);
  - T9 全厅修完标记;T10 累积只增(负/0 不增、多次累加、大值不截断);T11 悔棋回滚主线字段 + 修复位 + 修复清栈不倒回;T12 零回归。
- 静态手推复核两处关键曲线均与代码一致:
  - GuardianLevelFor 累计门槛(ExpToNext(L)=500+(L-1)*300):Lv2@500、Lv3@1300、Lv4@2400、Lv5@3800、Lv6@5500、Lv7@7500,与 T7 断言一致。
  - Cost(11)=3250 经验 → GuardianLevelFor=4(3250−500−800−1100=850<1400),跨 3 级,与 T8 一致。
- **测试覆盖缺口**:无。新增逻辑(发币/修复/曲线/快照回滚/零回归)均有对应用例;UI 接线层无单测但已由 Play 手验覆盖(见第 3 类)。

### 3. 手动功能验证 — PASS(Play 模式,经真实 UI 路径,非测试驱动假象)

实测路径:主菜单 → 点 BtnMerge「合成订单 DEMO」进真实对局 → 注入 Piety=600/Exp=400(贴近升级门槛)→ 点「神庙」按钮开 TempleWindow → 点 repair_0 修第 0 厅 → 点 × 关窗回 MergeOrderWindow。证据 = 逐步反射读 UI 节点 + 数据字段。截图 `Assets/Screenshots/temple_window_repaired.png`(TempleWindow 修复后态,game_view)。

- **验证点 1(MergeOrderWindow 顶部虔诚币 + 神庙按钮 + 不丢局)**:✓
  - 进对局后 MergeOrderWindow 实例化,Piety 节点渲染「✦ 0」、Temple 按钮渲染「神庙」。
  - 点神庙后 TempleWindow 实例化、MergeOrderWindow 仍存活(叠层不丢局)。关窗后 MergeOrderWindow 仍存活并经 OnDestroy→`_onClosed`→RefreshPiety 把 Piety 从「✦ 0」刷新为「✦ 100」(UserData Action 回调链通)。
- **验证点 2(TempleWindow 四态渲染 + 修复按钮置灰 + 点修复后刷新)**:✓
  - 开窗(Piety=600,NextRepairIndex=0):12 卡,hall0=可修(cost 标 + 可点修复按钮),hall1–11=未解锁(11 个 🔒)。
  - 点 repair_0 后数据层:Piety 600→100、Exp 400→900、GuardianLevel 1→2、UnlockedChapter 0→1、NextRepairIndex 0→1、TempleRepaired[0]=TempleDecorated[0]=true。
  - UI 层同步刷新:Header「✦ 100 / 守护者 Lv.2 / 经验 400/800 / 已解锁 第1章」;hall0 转已修(◈ 装饰 + ✓ 已修复);hall1 转币不足(✦ 750 + 还差 650 + 修复按钮 interactable=False 置灰)。本级经验进度 400/800 正确(Lv2 floor=500、cur=900−500=400、need=ExpToNext(2)=800)。
- **验证点 3(TempleWindow.prefab 可被 LoadGameObjectAsync 寻址)**:✓
  - TempleWindow 经真实「神庙」按钮 `ShowUIAsync<TempleWindow>` 成功实例化,证 location「TempleWindow」↔ prefab 文件名 ↔ m_Name 三处一致、寻址链未断。prefab.meta GUID 6db15220feb043ab99c2207556c617ea。
- Play 全程控制台无游戏侧运行错误(仅 MCP 桥 disposed-object 基础设施日志)。

### 4. Code Review — PASS

对照项目根 CLAUDE.md「核心原则(编码红线)」5 条逐项核(以正本枚举为准):

1. **异步优先**:✓ 两 UI 文件无 `Resources.Load`/同步加载/Coroutine/IEnumerator;TempleWindow 全部经 UGuiFactory 运行时建节点,prefab 由 UIWindow 框架经 [Window] 属性走异步加载。
2. **模块访问**:✓ 经 `GameModule.UI`(CloseUI/ShowUIAsync),无 `ModuleSystem.GetModule<T>()`。
3. **资源必须释放**:✓ 本轮无 `LoadAssetAsync`/`LoadGameObjectAsync` 直接调用(UI 走框架);无新建需手动释放的资源句柄。
4. **热更边界**:✓ TempleConfig/MergeOrderState/TempleWindow/MergeOrderWindow 均在 `GameScripts/HotFix/GameLogic` 树,实测从 GameLogic 热更程序集加载。
5. **事件解耦**:✓ 修复动作直接改共享 `BlockGameState.Instance.MergeState`(与 MergeOrderWindow 同款共享状态引用);UI 内回调用 UGUI 原生 `onClick.AddListener`,与全窗既有体例一致,无跨模块事件泄漏/风暴。

- **命名/节点前缀**:✓ 新节点(Piety/Temple/card_N/name_N/cost_N/need_N/lock_N/deco_N/stat_N/repair_N)为 code-built 窗口运行时查找名,naming-rules 的 `m_btn_`/`m_text_` 前缀对此类窗口不适用(test memory 2026-06 既有判例),与 MergeOrderWindow 全窗裸名体例一致,合规。
- **逻辑正确性核**:CanRepairTemple 三前置(顺序==NextRepairIndex / 未修 / 币足)+ 越界守卫齐;RepairTemple 扣币→标记→推进→发奖(经验=造价、体力 RefundEnergy 受 EnergyCap)→ while 跨级升级(每级 +1 章 + LevelUpEnergy)→ 清悔棋栈,顺序正确;AddPiety 仅正数;Snapshot Capture/Restore 6 主线字段对称、两数组 `.Clone()` 深拷贝带 `?.` 空安全;Reset() 6 字段全清零/重建数组。
- **持久文件交叉检**:对 dev 改过的 `pipeline/state/dev.md` 跑 conventions lint(diff 叙事 + 比喻 offender)0 命中;交接区内容为任务期工作态、可识别所属任务,合规。

### 非阻塞观察(报 boss / 知会用户,非缺陷)

- **持久化口径(dev 已标注,设计稿 §七 O3)**:MergeOrderState 整体不入磁盘、只入悔棋快照,与「长期主线」字面跨会话语义有落差(当前为单局尺度)。本轮按现状只入快照,与 `_soul`/`_blindBoxCount` 一致;跨会话存盘是独立大改,不在本轮范围。非代码缺陷。
- **预存代码 doc 注释 nit(非本轮引入)**:`MergeOrderState.DeliverSpecial` 第 357 行 doc-comment 写「发奖(体力 + 分数 + 灵力…)」,但该方法实际从不调 AddSoul(订单不发灵力)。git blame 证此注释属原始「核心玩法开发」commit 0bbb7f01,非本轮改动引入,且不影响行为。仅文档措辞陈旧,不阻塞。
- **DeliverSpecial 无 UI 入口(预存范围)**:特殊订单交付目前无任何 UI 窗口调用(特殊轨仅数据层存在,前序轮次即如此),故 MergeOrderWindow 无特殊交付的虔诚币刷新挂点——非本轮缺口。其发币逻辑由数据层单测 T2 直接覆盖。
