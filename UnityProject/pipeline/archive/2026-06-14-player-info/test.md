# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前被测任务:player-info 玩家信息系统·数据逻辑层(自治·放手默认)

设计基线 `design-docs/18-player-info.html` §六(30 验收点)。**总判定:PASS。**

### 环境自检
- `/unity-check` 三步通过:MCP server 9.7.1 在跑、绑定 `UnityProject@02a6dcaa`、`manage_scene get_active` 返场景 `main`(响应正常、工程对得上)。四类运行验证全程可达。

### 第 1 类 编译验证 — PASS
- `refresh_unity(force, compile=request, wait_for_ready)` → 编译完成、editor 回 idle。
- `read_console` filter error / warning(含 CS 过滤)= 0 条编译报错、0 条 CS 警告。

### 第 2 类 单元测试 — PASS
- `run_tests(EditMode, assembly=BlockBlast.Tests)`(job `e79c4c9ca3654981be5485ff878a5c84`):**279/279 PASS**(passed=279 / failed=0 / skipped=0,resultState=Passed,2.72s)。
- 明细核验:新增 `PlayerInfoTests` **28 例全部 state=Passed**(逐例从结果文件提取确认,非静默跳过);既有 251 例全绿 = Z2 零回归达标。
- 28 例 ↔ 30 验收点映射齐全:N1/N2 · R1–R5 · P1/P2 · L1–L4 · U1–U6 · C1/C2 · D1/D2 · S1–S3 · B1 · Z1(部分单例覆盖多点,如 U3 兼测 Unlocked+Locked、R5 兼测 Empty+TooLong)。

### 第 3 类 手动功能验证 — PASS
- 设计稿 §六 + dev 交接「需进 Play 模式手验:无」——本轮纯数据逻辑层,验收点 EditMode 即可全覆盖,无指针/手势/UI 表现。
- 补「生产真实路径」验证(test memory:Luban 桥接 EditMode 走 AssetDatabase 直读、刻意绕 YooAsset,须 Play 补真实加载链):
  - 进 Play 模式 → `execute_code` 反射调真实 `ConfigSystem.Instance.Tables.TbAvatar`(YooAsset 加载):DataList.Count=6。
  - `AvatarConfigMgr.ResetForTest()` 清缓存 → `GetAvatar(id)` 强制走真实 `EnsureLoaded`→ConfigSystem,反射读 POCO 6 行字段,逐字段比对源 `avatar.xlsx`:
    - id=1 type=1 avt_robot 300001 cond=1 param=1 ✓
    - id=2 type=1 avt_cat 300002 cond=1 param=5 ✓
    - id=3 type=1 avt_star 300003 cond=2 param=9001 ✓
    - id=101 type=2 frm_default 300101 cond=1 param=1 ✓
    - id=102 type=2 frm_gold 300102 cond=1 param=10 ✓
    - id=103 type=2 frm_event 300103 cond=2 param=9002 ✓
  - 全 6 行真实加载链字段 == 源 xlsx,无夹具/源漂移。Play 期 console 仅 2 条 MCP 桥 "Cannot access a disposed object"(域重载丢连接的传输噪声,非游戏运行时/编译错),停 Play 正常退出。
- 源↔产物交叉核验:`avatar.xlsx` 表头 `##group` = id/type(c,s)、image/unlock_text(c)、unlock_cond/unlock_param(c,s),6 字段全含 client(c)组、按列序进客户端字节;生成 `Avatar.cs` 行读序 Id→Type→Image→UnlockText→UnlockCond→UnlockParam 与之对齐,字节布局无错位。

### 第 4 类 Code Review — PASS
对照项目根 `CLAUDE.md`「核心原则(编码红线)」逐条(以正本为准):
1. 异步优先:新代码全纯逻辑无 IO;持久化复用既有 `MergeMetaPersistence.SaveAsync` 异步外壳,新增 `ExportToMeta`/`ImportFromMeta` 为纯同步无 IO 方法。无新增同步加载/Coroutine。✓
2. 模块访问:grep 确认无 `ModuleSystem.GetModule<T>()`;配置经 `ConfigSystem.Instance.Tables`(同 ItemConfigMgr/NumericConfigMgr 既有口径)。✓
3. 资源释放:新代码无 `LoadAssetAsync`/`LoadGameObjectAsync`/`Resources.Load`,配置走 ConfigSystem 既有懒加载。✓
4. 热更边界:全部新逻辑落 `GameScripts/HotFix/`(GameLogic + GameProto),H1 TODO 在 HotFix/GameLogic/UI;未碰 `GameScripts/Main`。✓
5. 事件解耦:纯数据逻辑层无新增事件,无泄漏/风暴风险。✓
- 命名/前缀:新代码为 C# 逻辑类型(非 UGUI 节点),naming-rules 的 `m_btn_`/`m_text_` 节点前缀不适用;C# PascalCase 命名合规,Luban 生成代码 dev 未手改。
- 零回归交叉核验:`git diff --stat` 确认 `MergeOrderState.cs`/`ItemGrant.cs`/`NumericConfigMgr.cs`/`ItemConfigMgr.cs`/`RewardDisplay.cs` 一行未改;`MergeMetaSave.cs` 纯追加 11 行(8 玩家字段 + 注释),既有 15 字段零改、CurrentVersion 不升;`MainMenuWindow.cs` 仅 +4 行 TODO 注释无实现。
- 持久文件交叉检(conventions lint + 抽查 dev 改的 `pipeline/state/dev.md` 交接区):指代词/diff 叙事 lint 0 命中、拟人比喻 lint 0 命中;「改动摘要」属当前任务工作态分节,合规。
- dev 自主决策复核(均安全默认、不偏离 spec):
  - 持久化挂载点改为 `PlayerInfo` 静态 Export/Import(不挂 MergeOrderState/悔棋快照)——设计稿 §2.2 显式认可「独立子对象做法这两处只是新增」,避免耦合两个无关域。✓
  - AvatarEntry Type/UnlockCond 存 int(非 Luban 枚举)——同 ItemDef 做法,使解锁服务纯逻辑、不引生成代码。✓
  - ImportFromMeta 的「在表内」校验经可选 `avatarValid`/`frameValid` 谓词注入,不把纯方法耦合 ConfigSystem。✓
  - 命名空间用 `GameLogic.BlockBlast.Player`——设计稿 §五末「(或沿用 GameLogic.BlockBlast)」已认可,非偏离。✓

### 证据
- 单测 job:`e79c4c9ca3654981be5485ff878a5c84`(279/279);明细结果文件含 28 例 PlayerInfoTests state=Passed。
- 生产路径 execute_code 输出:TbAvatar DataList.Count=6 + 6 行字段比对(见第 3 类)。
- 截图:本轮无 UI 表现,未取景(数据逻辑层无可视产物)。

### 记录项(不判 FAIL,供后续轮)
- O1–O8 范围开关均按本轮安全默认延后(UI/Sprite/活动发放/多语言/经验接入/钻石余额),设计稿 §七已列、boss 关单复核。无覆盖缺口:30 验收点全有对应通过用例,无「只被夹具设置、无验收项校验、无用例读取」的漂移温床字段。

上一单 reward-display 已于 2026-06-14 关单 PASS,归档 `pipeline/archive/2026-06-14-reward-display/test.md`。
