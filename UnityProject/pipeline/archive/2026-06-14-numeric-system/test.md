# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前被测任务:数值底层系统(xlsx 批次第一刀)

设计基线 = 验收判据:`design-docs/15-numeric-system.html`(F1-9 / C1-3 / R1-4 / Z1-3)。
dev 交接区:`pipeline/state/dev.md`。

### 总判定:PASS

四类验证全绿,无代码缺陷。Unity 实例 `UnityProject@02a6dcaa` 全程响应。

### 1. 编译验证 — PASS

- `/unity-check` 三步:server 在(v9.7.1)、绑定实例活、工程对(场景 `main`)。
- `refresh_unity` → `resulting_state: idle`(编译完成、无挂起)。
- `read_console`(error/warning,含 CS 过滤)无 CSxxxx 编译诊断。仅有的 error 是 MCP 桥内部 "Cannot access a disposed object"(桥重启瞬态,非游戏代码);Play 期一条 ResourceModuleDriver NullReferenceException 是运行期资源事件,非编译错。

### 2. 单元测试 — PASS

- `run_tests(EditMode, BlockBlast.Tests)`:**209/209 passed,0 failed,0 skipped**(durationSeconds 0.58)。
- 基线 190 例 + 新增 19 例 = 209,数量吻合,证新 NumericSystemTests 19 例既编进程序集又跑绿(Z2 零回归同时成立)。
- 覆盖映射:F1-F9 + Format_Negative/Format_1999(格式化边界)、C1-C3(AssetDatabase 直读 num_tbnum.bytes)、R1-R4(InitForTest 注册表/helper)、Z1(纯逻辑无 ConfigSystem)。
- 测试资产:`Assets/Editor/Tests/BlockBlast/NumericSystemTests.cs`。无测试覆盖缺口。

### 3. 手动功能验证(Play 模式真实运行期路径)— PASS

EditMode 测试走 AssetDatabase 直读 .bytes 绕过 YooAsset;手验补走**生产真实路径**(ConfigSystem → YooAsset → num_tbnum.bytes → TbNum → ToEntry → 缓存),覆盖单测刻意绕开的 EnsureLoaded 真实加载。

- 真实加载链(ResetForTest 后强制走 ConfigSystem):
  - `Get(2)` 返虔诚币,逐字段 NumId=2 / NumType=2 / Quality=3 / IconName=icon_piety / NameTextId=100002 / DescTextId=200002 — 与 num.xlsx 源逐字段一致。
  - `Get(999)`=null(不抛);`GetByType(4)`=1 条(体力)。
  - `Abbreviate`:999999→999.9K、1500000→1.5M、1000→1K。
- O3 示范接入(MergeOrderWindow.RefreshPiety)经真实 UI 路径验(点 MainMenuWindow BtnMerge 进对局 → 反射注入 Piety → Invoke 私有 RefreshPiety → 读回 `_pietyText.text`):
  - Piety=1500000 → 显示 `✦ 1.5M`
  - Piety=999999 → 显示 `✦ 999.9K`(spec 钉死示例:截断不误进 M)
  - 复位 0 → `✦ 0`。✦ glyph 保留,仅数字走格式化。
- 截图:`Assets/Screenshots/screenshot-20260614-134735.png`(取景 game_view,经 UICamera)。

### 4. Code Review — PASS

文件清单 diff review,对照项目根 CLAUDE.md「核心原则(编码红线)」全部条目(以正本为准):
1. 异步优先:新文件无新 IO;无 Coroutine/Resources.Load/.Result/.Wait。注册表懒加载贴 ConfigSystem 既有口径(spec O5 明确同步加载是现状、不在本轮),非新增红线。PASS。
2. 模块访问 GameModule.XXX:无 `ModuleSystem.GetModule<T>()`;MergeOrderWindow 既有 `GameModule.UI.ShowUIAsync` 正确。PASS。
3. 资源必须释放:无 LoadAssetAsync/LoadGameObjectAsync(helper 仅给图标资源名,真实 Sprite 加载 O4 后续轮)。无释放义务。PASS。
4. 热更边界:新文件全在 `GameScripts/HotFix/`(GameLogic + GameProto),无 `GameScripts/Main` 改动。PASS。
5. 事件解耦:无新增事件;唯一运行期改动是 RefreshPiety 文本格式化,无 GameEvent/AddUIEvent 泄漏或风暴。PASS。

命名:NumericEntry/NumericConfigMgr/NumericFormat/NumericDisplay 公共词、描述性,NumericConfigMgr 仿既有 WeightCfgConfigMgr 体例。本轮无 prefab 节点/codegen,naming-rules UI 前缀规则不适用。PASS。

配置层一致性(交叉核验源↔产物):
- num.xlsx 表头四行 group:id=`c,s` desc=`c,s` func_name=`s` name=`c` icon=`c` num_type=`c,s` planner_notes=`e` quality=`c` — 与 dev 决策 #1(`c,s` 非 `cs`)、客户端目标只导 c 组吻合。
- 生成 Num.cs 客户端字段读序 Id/Desc/Name/Icon/NumType/Quality = xlsx 含 c 组列序(跳过 func_name(s)/planner_notes(e)),C1/C2/C3/R1 读真实 .bytes 通过,证字节布局自洽。

持久文件交叉检(对 dev 改动的 `pipeline/state/dev.md` 交接区执行 conventions.md lint + 抽查):
- 交接区属工作态层(任务期,关单由 boss 清/归档)。其 diff 叙述(「✦ {_merge.Piety} 改为 ...」「cs→c,s 重导」)是 dev→test 交接的应有内容(告知 test 验什么 + 理由),可识别所属任务,非正文腐烂。无拟人/口语比喻 offender。合规。

### 设计偏离记录(非缺陷,已核实合理)

- spec §3.3/§3.5 POCO 含 `FuncName`、FormatWith 用 `e?.FuncName`。实现去掉 FuncName:func_name 是 group=s,客户端目标(c 组)不导出,生成 GameConfig.Num 无 FuncName 字段 — spec 该字段在客户端不可编译。dev 改 FormatWith 标签兜底用 NameTextId。属 spec-vs-生成代码冲突,按 CLAUDE.md「优先信任代码实际实现」正确处置;验收 R4 只要求 FormatWith 含 "1.5K"(不验标签),不受影响。

### 证据索引

- 单测 job:209/209 passed(job 4b8133e8…、cb3a8b64… 两次均绿)。
- 运行期反射核验:Get(2) 字段、Get(999)=null、GetByType(4)=1、Abbreviate 三档。
- UI 实显:✦ 1.5M / ✦ 999.9K / ✦ 0。
- 截图:`Assets/Screenshots/screenshot-20260614-134735.png`。
