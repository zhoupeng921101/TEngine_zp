# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前被测任务:道具底层系统(item-system,xlsx 批次第二刀)

设计基线:`design-docs/16-item-system.html`。验收标准:`pipeline/state/plan.md` 交接区(C1-6/R1-3/G1-4/U1-5/B1-5/Z1-2)。

### 总判定:PASS

四类验证全部通过。运行验证全程可达(Unity 实例 `UnityProject@02a6dcaa` 响应正常),无环境阻塞。一处测试覆盖缺口(非缺陷)与一处数据·测试夹具值不一致(非验收项),记录于下,供后续投放参考,不阻断本单。

## 四类验证逐项结果

### 1. 编译验证 — 通过
- `refresh_unity`(force + compile request)触发刷新,编辑器返回 idle、场景 `main` 响应正常。
- `read_console` filter=error:0 条。filter=warning(含「Item」过滤):0 条。
- 仅一处生成代码 `Tables.cs` 为既有产物追加(3 个新表懒加载访问器,63 行纯新增);手写代码全在热更程序集 `GameLogic` + 生成代码在 `GameProto/GameConfig`(均热更),`GameScripts/Main` 无改动,符合热更边界红线。

### 2. 单元测试 — 通过
- Unity Test Runner,EditMode,assembly=`BlockBlast.Tests`:**234/234 通过**(0 失败 0 跳过,耗时 0.29s,job e5de3fca)。
- ItemSystemTests 子集(group=`GameLogic.BlockBlast.Tests.ItemSystemTests`):**25/25 通过**(job 52e51059)。
- 回归(Z2):234 总数 − 25 新增 = 209 原例全绿,末例为既有 `WeightCfgLubanTests`,确认原例全程参与运行。
- 覆盖映射:C1-C6(直读 .bytes)/ R1-R3 / G1-G4 / U1-U5+U5b / B1-B5 / Z1 各验收点均有对应用例且通过。

### 3. 手动功能验证 — 通过(无 UI/无 Play 交互,改以真实加载路径 Play 验证)
本批次为纯数据层 + 纯逻辑层,无 UI、无运行期入口接线,无指针/手势交互,故无传统 Play 手验项。EditMode 单测为隔离 `AssetDatabase` 直读,刻意绕过 YooAsset 真实加载链;补走生产真实路径以覆盖 `ItemConfigMgr.EnsureLoaded`(经 `ConfigSystem.Instance.Tables`,YooAsset):

- 进 Play 模式,HybridCLR + YooAsset 启动期 console 0 error。
- `ItemConfigMgr.GetItem(30006)` 经真实 `EnsureLoaded` 返回行:Id=30006 / Type=6 / UseEffect=4 / UseValue=6001 / Param=1 / Quality=5 / Stacking=0,与 C2 验收值逐字段一致 → `.bytes` 已注册进 YooAsset 清单、`Tables.cs` 绑定生效。
- `GetGiftRandom(6001).Count==4` 且权重和==100(C5);`GetGiftSelect(5001).Count==3`(C6),经真实加载路径复核。
- 随机池 6001 引用的全部 itemId(30001/30002/30004/30003)在真实 `item.TbItemDef` 表均命中,礼包奖品引用闭合。
- `ItemGrant.ResolveAndApply`(私有,反射直调)对随机礼包道具递归展开:抽中 itemId=30002 → 子项 Resolve 为 Numeric/TargetId=4/Amount=30,正确记入 produced。递归落点逻辑在真实配置下正确。

### 4. Code Review — 通过
对照 `CLAUDE.md`「核心原则(编码红线)」全部 5 条(读正本,非枚举副本):

1. 异步优先:道具系统无 IO;表访问经 `ConfigSystem.Instance.Tables`(框架既定配置访问范式,同既有 `NumericConfigMgr`),无新增同步 `LoadAsset`/Coroutine。grep `Resources.Load|LoadAsset|StartCoroutine|IEnumerator|.Result|.Wait()|Thread.Sleep` 全文件 0 命中。合规。
2. 模块访问:无 `ModuleSystem.GetModule<T>()`;用 `ConfigSystem.Instance` 单例(同既有范式)。合规。
3. 资源释放:无 `LoadAssetAsync`/`LoadGameObjectAsync` 调用,表由 ConfigSystem 管理生命周期,无新增泄漏面。合规。
4. 热更边界:手写代码全在 `GameScripts/HotFix/GameLogic`,生成代码在 `GameProto/GameConfig`,`GameScripts/Main` 无改动。合规。
5. 事件解耦:本批次无事件、无 UI,不涉及。无违规。

源↔产物交叉核验:`ItemDef.cs` 生成构造器字段读序(Id→Name→Desc→Icon→Quality→Light→Automatic→Type→Param→UseEffect→UseValue→UseNum→UseLevel→Stacking→Term→TermPrompt→TermTime→Compensate→CompensateEmail→JumpList)与 `itemdef.xlsx` ##var 列序、与 `ItemConfigMgr.ToItemDef` 桥接逐字段对齐,字节布局无错位。`GiftRandom.cs`/`GiftSelect.cs` 读序(AutoId→Index→ItemId→Num→Rate)同 `ToGiftEntry` 一致。枚举 `EItemQuality` 1-6(MYTH=6)、`EItemType` 1/2/3/5/6(跳 4)与设计一致。
U5 级联断言核验:`AddDirect(Diamond,2,4)` 经 `AddToInventory` 向上单链(MaxLevel=3)→ 剩 2@Lv2 + 1@Lv3,测试断言与真实级联语义一致。
适配器只调不改:`MergeOrderState.Exp/AddPiety/RefundEnergy/AddDirect` 与 `NumericConfigMgr` 常量(Exp=1/Piety=2/Diamond=3/Energy=4)签名经 grep 核实存在,`MergeOrderState.cs`/`NumericConfigMgr.cs`/`MergeElementVisual.cs` 不在改动集,「加法式不改既有代码路径」成立。

持久文件交叉检(对 dev 改过的 `pipeline/state/dev.md`、`pipeline/memory/dev.md`):conventions lint(指代词/diff 叙事、拟人比喻)0 命中;人工通读语体平实,无可推导事实副本。合规。

## 记录项(非缺陷,不阻断本单)

### 记录 1:`GrantOnAcquire` 测试覆盖缺口
- `ItemGrant.GrantOnAcquire`(Automatic 分流 + 随机礼包递归展开 + 深度上限 5)为 dev 自发新增(dev.md 决策 4),但 25 个用例无一调用它——仅直接测了 `Resolve`/`ApplyPattern`/`ApplyNumeric`。该递归路径在本轮经 Play 模式反射直调私有 `ResolveAndApply` 验证为正确(随机礼包展开到终末 Numeric 产出并记入 produced),逻辑无误,但缺单测兜底。
- 性质:覆盖缺口,非缺陷。验收 U 类只列 Resolve+ApplyPattern,未要求 GrantOnAcquire,故不影响验收。
- 建议(交后续):补一个 EditMode 用例,经 `InitForTest` 注入含 automatic=1 的礼包道具 + 礼包池,断言 `GrantOnAcquire` 递归展开后的 produced 内容,锁住递归行为防回归。

### 记录 2:源 xlsx 的 automatic 取值与测试夹具不一致
- 源 `itemdef.xlsx`:30004 automatic=1,30001/30002 automatic=1,30005/30006/30007/30008 automatic=0(礼包/材料获取后进背包,非立即结算)。
- 测试夹具 `ItemSystemTests.GiftRandomItem()`(30006)硬编码 `Automatic = 1`,与源 xlsx 的 30006 automatic=0 不一致;dev.md 交接区描述 30006 时也按 automatic=1 行文。
- 性质:非缺陷、非验收项。`automatic` 不在任一验收判据(C/R/G/U/B/Z 均不校验 automatic),且无测试调用 `GrantOnAcquire`,故夹具的 automatic 值从未被断言,不影响任何用例结果与验收。设计稿 §六样例表本身不含 automatic 列,逐行 automatic 由 dev 自定。
- 实测旁证:Play 模式调真实 `GrantOnAcquire(GetItem(30006),...)` 返回 produced.Count=0(因真实数据 automatic=0,走进背包路径不立即结算),与源数据一致、行为正确;只是与夹具/交接区的 automatic=1 描述对不上。
- 建议(交后续接 GrantOnAcquire 调用方时定夺):统一 30006 的 automatic 语义——若随机礼包应「获取即开」则源 xlsx 改 automatic=1;若应「进背包手动开」则夹具与交接区描述改 automatic=0。本轮无调用方,任一取向都不影响当前验收。

## 复跑命令清单(环境侧参考)
- 编译 + 控制台:`refresh_unity(compile=request, mode=force)` → `read_console(types=[error])`
- 单测:`run_tests(EditMode, assembly_names=["BlockBlast.Tests"])` → `get_test_job(job_id)`;子集 group=`GameLogic.BlockBlast.Tests.ItemSystemTests`
- 真实加载路径:Play 模式 → `execute_code` 反射调 `ItemConfigMgr.GetItem/GetGiftRandom/GetGiftSelect`(先 `ResetForTest` 强制走 `EnsureLoaded`)
- 导表(如需重跑):`cd Configs/GameConfig` → `$env:DOTNET_ROLL_FORWARD="Major"; $env:AI_MODE="1"; & ".\gen_code_bin_to_project_lazyload.bat"`
