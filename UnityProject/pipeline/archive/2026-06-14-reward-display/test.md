# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前被测任务:reward-display 通用奖励展示(自治·放手默认,dev→test 段)

验收基线 `design-docs/17-reward-display.html`(§六 18 条)+ state/plan.md 交接区。

### 总判定:PASS

四类验证全过,18 条验收逐条由通过的单测背书,EditMode 全量零回归。

### 开工自检

- `/unity-check` 通过:MCP server v9.7.1 在跑,按名绑定 `UnityProject@02a6dcaa`,`manage_scene get_active` 返回场景 "main",实例响应、工程对得上。
- 静态 API 核验(碰运行验证前先核签名,防把签名错误留到导入):被测层依赖的全部既有符号经 grep/read 实证存在且签名对得上——`GrantPayload(GrantKind,int,int,int,int)`、`ChestReward(ChestRewardKind,int,int=0)`、`MergeElement.Diamond=100`、`NumericConfigMgr.{Energy=4,Get,InitForTest,ResetForTest}`、`ItemConfigMgr.{GetItem,InitForTest,ResetForTest}`、`NumericEntry/ItemDef` 字段名(NumId/NameTextId/IconName/NumType/Quality;Id/Name/Icon/Quality/Type)、`NumericFormat.Abbreviate`(5000→"5K"、999999→"999.9K" 手推核对)。`MergeElement`/`MergeElementVisual` 在父命名空间 `GameLogic.BlockBlast`,被测类在 `GameLogic.BlockBlast.Reward` 子空间内,无 using 也可见——非缺漏。

### 第 1 类 编译验证:PASS

- `refresh_unity`(force, compile=request)触发编译 + 域重载,轮询至 `resulting_state=idle`(ready_for_tools)。
- `read_console`:error 0 条、warning(filter CS)0 条、error+warning 合并查 0 条。编译 0 报错。
- C4 widget(`RewardItemWidget`)落 GameLogic 程序集,随该程序集一并编译通过(无 CS 诊断即证)。

### 第 2 类 单元测试:PASS

- 隔离跑 `run_tests(EditMode, assembly=BlockBlast.Tests)`:job 44422d9d,251/251 passed、0 failed、0 skipped。
- 全量 `run_tests(EditMode)`:job aa327058,251/251 passed、0 failed、0 skipped(本工程 EditMode 仅 BlockBlast.Tests 一个程序集,全量数与隔离同)。
- 17 个新增 `RewardDisplayTests` 用例全在其中,既有用例无一失败 → Z2 既有零回归成立。
- 测试覆盖缺口:无。18 条验收逐条有背书用例(下表)。

### 第 3 类 手动功能验证:N/A(非阻塞,设计明确排除)

本轮纯逻辑展示归一层,无运行期 UI 可手验:
- C4 `RewardItemWidget` 本轮按 O2 只写骨架、不挂 prefab、不投放任何窗口,因无 prefab 不被实例化(设计 §3.6 / §七 O2)。
- 设计 §六明确「验收锚在纯逻辑 helper,不要求 Widget 跑起来」;§四时序图标注真实 Sprite 加载发生在 Widget.SetData 内、本轮 Widget 不投放,渲染层留待接 UI 时验。
- 故无 Play 模式手验项可执行,记 N/A(非 BLOCKED:运行验证手段本身可达,是被测范围本轮不含运行期 UI)。无伪造 Play 记录。

### 第 4 类 Code Review:PASS

文件清单 diff review,逐条对照 CLAUDE.md「核心原则(编码红线)」全部 5 条:
1. 异步优先:三个新文件无任何 IO;POCO + 静态纯函数。Widget.SetData 用框架内置 `Image.SetSprite`(内置缓存池,ui-patterns 认可路径),无同步 LoadAssetAsync / Coroutine。无违规。
2. 模块访问:无 `ModuleSystem.GetModule<T>()`;查注册表走既有静态 Mgr,符合既有体例。无违规。
3. 资源必须释放:无 `LoadAssetAsync`;`SetSprite` 走内置缓存池无需手动释放。无泄漏。无违规。
4. 热更边界:C1/C2/C3 落 `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Reward/`,C4 落 `HotFix/GameLogic/UI/BlockBlastUI/`,均热更程序集;测试落 `Assets/Editor/Tests/`(editor-only,正确)。未碰 `GameScripts/Main`。无违规。
5. 事件解耦:本层无跨模块调用、无事件;纯只读归一 helper + 未接线 Widget 骨架,无事件泄漏 / 风暴风险。无违规。

命名 / 节点前缀(对照 naming-rules.md):
- 类型:`RewardView`(struct)/`RewardBadge`(enum)/`RewardDisplay`(static)/`RewardItemWidget : UIWidget`(`XxxWidget` 规约)——全 PascalCase 合规。
- 私有字段 `_imgIcon`/`_imgQualityFrame`/`_textName`/`_textCount`——`_小驼峰` 合规。
- Widget 节点名 `m_img_Icon`/`m_img_QualityFrame`(→Image)、`m_text_Name`/`m_text_Count`(→Text)——前缀映射类型对得上。本 widget 是 prefab-bound(走 UIScriptGenerator),前缀规则适用且满足(非 code-built 裸名场景)。

零回归核验:6 个声明只读的既有文件(`ItemGrant.cs`/`ChestSystem.cs`/`NumericConfigMgr.cs`/`ItemConfigMgr.cs`/`MergeElementVisual.cs`/`NumericDisplay.cs`)未被本层写改;旧 4 档 `NumericDisplay.QualityColor` 冻结、不被本层调用(本层另定权威 6 档)。EditMode 全量零回归实测背书。

持久文件交叉检(dev 动过的持久文件):仅 `pipeline/state/dev.md`。
- lint(conventions 收尾清单正则)0 命中。
- 抽查:正文为自包含交接,无指代词 / 无 diff 叙事(改动摘要属当前任务工作态,合法)、无拟人比喻、工作态可识别所属任务。合规。

### 18 条验收 → 背书用例对照(全 PASS)

| 验收 | 背书用例 | 核对要点 |
|------|---------|---------|
| V1 | V1_Grant_Numeric | Icon/Name==注入值、CountText "x5K"、Badge Currency、RawAmount 5000 |
| V2 | V2_Grant_Pattern | Icon "pattern_100"、Badge Pattern、CountText "Lv2 x4"、Quality==QualityColor(PatternQuality(2)) |
| V3 | V3_Grant_None_Material | 走 FromItem、Icon/Name 道具值、Badge Material |
| V4 | V4_Grant_Gift | Badge Gift、CountText "x3"(反映 Times)、RawAmount 3 |
| V5 | V5_Chest_Energy | 走 FromNumeric(4,12)、Icon "icon_energy"、CountText "x12"、Badge Currency |
| V6 | V6_Chest_Soul_NoThrow | DoesNotThrow、Badge Currency、Icon "Soul"、CountText "x200" |
| V7 | V7_Chest_Pattern | Badge Pattern、CountText 含 "Lv3"、Quality==QualityColor(PatternQuality(3)) |
| V8 | V8_Chest_UndoWish_Function | Badge Function、CountText "x2"、退化白;WishCharge 同为 Function |
| V9 | V9_FromNumeric_FromItem_Direct | FromNumeric 字段对;FromItem(单件) CountText "" |
| Q1 | Q1_SixTiers_AllDistinct | 六档两两不等、1==白(0.85) |
| Q2 | Q2_OutOfRange_FallsBackWhite | 0/7/-1 均==白 |
| Q3 | Q3_RgbMatchesSpec | 逐档 RGB==§3.3(容差 0.01) |
| N1 | N1_CountText_ReusesNumericFormat | 200→"x200"、999999→"x999.9K"(截断)、1→""、0→"x0" |
| N2 | N2_PatternCountText_WithLevel | (2,3)→"Lv2 x3"、(1,1)→"Lv1" |
| B1 | B1_Numeric_MissDegradesNoThrow | 注入空集→FromNumeric(999,100) Icon null/Name 0/白/CountText "x100"/不抛 |
| B2 | B2_Item_MissDegradesNoThrow | FromItem(999999,1) 降级不抛 |
| Z1 | Z1_PureLogic_NoConfigSystem | EditMode 跑通即证未触 ConfigSystem/YooAsset |
| Z2 | 全量 EditMode 251/251 | 既有用例全绿、新增 17 另计,零回归 |

### 证据索引

- 编译:`refresh_unity` resulting_state=idle;`read_console` error/warning 0 条。
- 单测:隔离 job 44422d9d 251/251;全量 job aa327058 251/251。
- Code review:见第 4 类逐条。
- 截图:无(纯逻辑层,无运行期 UI 可截)。

### 自治拍板取舍

- 第 3 类记 N/A 非 BLOCKED:运行验证手段(Play/截图)本身可达,被测范围本轮不含运行期 UI(C4 不投放),故无手验项可跑——区别于环境阻塞致跑不了。
- 接受 dev 对 B1/B2「注入空集合而非完全不注入」的测法:EditMode 无 ConfigSystem 运行时,不注入会触 ConfigSystem.Instance(NRE/异常)而非走查无降级分支;注入空集合精确命中 `_cache != null` 早返回 → Get 返 null 降级。测法正确,验收点意图(查无降级不抛)被精确覆盖。
- 全量 EditMode 与隔离同为 251:本工程 EditMode 仅 BlockBlast.Tests 一个程序集,不存在其他程序集的无关失败混入风险,两次跑互证。
