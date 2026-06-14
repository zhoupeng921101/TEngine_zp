# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:reward-display 通用奖励展示(自治·放手默认,dev 段)

设计基线 `design-docs/17-reward-display.html` + state/plan.md 交接区(C1-C4 + 18 条验收)。boss 已拍板四个范围开关:O2=写 C4 widget 骨架(不挂 prefab)、O3=本轮不收编旧 4 档、O6=Soul 走本层私有映射、O7=盲盒 Pattern 用代表图案 Diamond + 等级降级。

### 改动摘要(做了什么 / 为何这么做)
- 纯加法式新增展示归一层:三种异构奖励产出(道具 `GrantPayload` / 盲盒 `ChestReward` / 裸 num_id+数量)归一成统一 `RewardView`,任何 UI 拿它用同一套渲染。归一层只读既有产出结构与元数据注册表,产出不含 Unity UI 类型的 POCO(唯一 Unity 类型 Color 为值类型),故全程可纯单测、不碰 YooAsset / Unity 运行时。
- 既有六个文件(`ItemGrant.cs` / `ChestSystem.cs` / `NumericConfigMgr.cs` / `ItemConfigMgr.cs` / `MergeElementVisual.cs` / `NumericDisplay.cs`)逐字节只读不写,旧路径零行为变化。
- 6 档权威品质色(白/绿/蓝/紫/橙/红)定在本层,对齐道具 `EItemQuality`;旧 4 档 `NumericDisplay.QualityColor` 冻结不删不改、不被本层调用(收编是独立任务 O3)。
- 灵力 Soul 无 num_id,走本层私有占位映射(图标名 "Soul"),不假设不存在的 num_id、不查 num 表。
- 盲盒 Pattern 只带等级不带具体图案,展示用代表图案 Diamond + 等级文案降级;等级→展示品质 Lv1→3 精英 / Lv2→4 史诗 / Lv3→5 传说。
- 数量文本复用既有 `NumericFormat.Abbreviate`(不另写一份格式化逻辑),前加 "x";单件(amount==1)不显 "x1"。
- C4 widget 骨架按 O2 写了,顶部标 TODO「未接 prefab / 未投放,待后续 UI 轮次」;因无 prefab 不被实例化,只作接法示范、不要求运行跑通(验收锚在归一层纯逻辑)。

### 文件清单
新增:
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Reward/RewardView.cs`(C1:`RewardView` readonly struct + `RewardBadge` enum)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Reward/RewardDisplay.cs`(C2+C3:静态 helper,各源转换 + 6 档品质色 + 数量文本 + ChestRewardKind 私有映射)
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/RewardItemWidget.cs`(C4:`UIWidget` 骨架,顶部 TODO 标未投放)
- `Assets/Editor/Tests/BlockBlast/RewardDisplayTests.cs`(T1:17 个 `[Test]`,覆盖 V1-V9 / Q1-Q3 / N1-N2 / B1-B2 / Z1)

落点说明:C1/C2/C3 放 `Module/BlockBlast/Reward/`(逻辑层,纯展示归一,不依赖 UnityEngine.UI);C4 放 `UI/BlockBlastUI/`(需 Image/Text)。三者均属 `GameLogic` 程序集。测试程序集 `BlockBlast.Tests` 引用 `GameLogic`+`GameProto`,可达本层符号。

不碰的文件(零回归):`ItemGrant.cs` / `ChestSystem.cs` / `NumericConfigMgr.cs` / `ItemConfigMgr.cs` / `MergeElementVisual.cs` / `NumericDisplay.cs`。

### 验证点(逐条对应验收标准,告诉测试该验什么 / 怎么验 / 预期)
全部纯逻辑,无 .bytes / YooAsset 依赖;注册表查询用 `NumericConfigMgr.InitForTest` / `ItemConfigMgr.InitForTest` 注入绕 ConfigSystem。验法:跑 EditMode `BlockBlast.Tests` 程序集。

- V1-V9(各源转换):见 `RewardDisplayTests.V1_*`~`V9_*`。V1 经验 num→"x5K"/Currency/RawAmount==5000;V2 钻石 Lv2×4→"pattern_100"/"Lv2 x4"/品质==QualityColor(PatternQuality(2));V3 None 材料走 FromItem→Material;V4 GiftRandom→Gift/CountText 反映 Times=3("x3");V5 Energy 走 FromNumeric(4,*)→"x12"/Currency;V6 Soul 不抛/Icon=="Soul";V7 Pattern Lv3→含 "Lv3"/品质对;V8 Undo/Wish→Function/"x2"/白;V9 直调字段对、FromItem 单件 CountText=="".
- Q1-Q3(品质色):`Q1` 六档两两不等且 1==白(0.85);`Q2` QualityColor(0/7/-1)==白;`Q3` 逐档 RGB==设计稿 §3.3(容差 0.01)。
- N1-N2(数量文本):`N1` CountText(200)=="x200" / (999999)=="x999.9K"(截断不进位)/ (1)=="" / (0)=="x0";`N2` PatternCountText(2,3)=="Lv2 x3" / (1,1)=="Lv1".
- B1-B2(查无降级不抛):`B1` 注入空集合后 FromNumeric(999,100)→icon null/name 0/白/CountText "x100"/不抛;`B2` FromItem(999999,1) 同理降级。
  - 注:B1/B2 用「注入空集合」而非完全不注入——FromNumeric/FromItem 内部 Get 会 EnsureLoaded,EditMode 无 ConfigSystem 运行时,不注入将触 ConfigSystem 而非走查无降级路径。注入空集合模拟「表已加载但无此 id」,精确命中降级分支。
- Z1(全链纯逻辑):`Z1_PureLogic_NoConfigSystem` 在 EditMode 跑通本身即证未触 YooAsset。
- Z2(既有零回归):EditMode 全量 251 用例全绿(含本层 17 新增),既有用例无一失败。

### 自检结果
- 编译 0 报错:域重载完成(refresh 后桥重连,read_console 仅 `MCP-FOR-UNITY: disposed object` 域重载瞬态,无任何 CSxxxx 诊断)。
- 运行验证:EditMode `BlockBlast.Tests` 跑通 251/251 passed、0 failed(job e38caa9c)。EditMode 能跑起来即证相关程序集编译通过(dev memory 第 20 条)。

### 给测试的标注
- 不涉及热更程序集的新接缝问题(本层落 `GameLogic` 热更程序集,常规热更代码)。
- 不需 Luban 重生成(无新增表 / 枚举,复用既有)。
- 无需进 Play 模式手验:本轮纯逻辑,C4 widget 不投放,无运行期 UI 可验。
- dev 动过的持久文件:仅本文件 `pipeline/state/dev.md`(交叉检 conventions lint 对象)。

上一单 item-system 已于 2026-06-14 关单 PASS,归档 `pipeline/archive/2026-06-14-item-system/dev.md`。
