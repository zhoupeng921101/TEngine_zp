# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:reward-display 通用奖励展示(自治·放手默认)

设计稿:`design-docs/17-reward-display.html`(已挂 index 卡片 + 全库 12 篇 sidebar 文档树;断链检查 0;TOC 锚点 17/17 解析)。
设计基调:**纯逻辑展示归一层**——把三种已实装的奖励产出结构归一成统一 `RewardView`,任何 UI 复用同一套渲染。加法式只读不写既有产出/发奖路径,UI 投放本轮不做(无美术 + UI 独立后续)。

### 设计基线(经 grep 核实的真实符号,dev 据此定位)
- 奖励源结构(只读,不改一行):
  - `GameLogic.BlockBlast.Item.GrantPayload`(`ItemGrant.cs`):`GrantKind {None,Numeric,Pattern,GiftSelect,GiftRandom}` + TargetId + Amount + Level + Times。None 的 TargetId = 道具 id(见 `ItemGrant.Resolve` default 分支 `def.Id`)
  - `GameLogic.BlockBlast.ChestReward`(`ChestSystem.cs:24`):`ChestRewardKind {Soul,Energy,Pattern,UndoCharge,WishCharge}` + Amount + PatternLevel(仅 Pattern 有意义 1–3)
  - 裸货币:num_id + 数量
- 既有元数据注册表(只读查):
  - `NumericConfigMgr.Get(numId)`(`NumericConfigMgr.cs:55`)→ `NumericEntry`(IconName / NameTextId / Quality);常量 Exp=1/Piety=2/Diamond=3/Energy=4
  - `ItemConfigMgr.GetItem(id)` → `ItemDef`(Icon / Name / Quality / Type)
  - `NumericFormat.Abbreviate(long)`(`NumericFormat.cs:26`):0–999/K/M 缩写,截断保 999999→"999.9K"
  - `MergeElement` 枚举(`MergeElementVisual.cs`):Diamond=100…Stone=200
- 既有 4 档品质色 `NumericDisplay.QualityColor`(`NumericDisplay.cs`):白(1)/蓝(2)/紫(3)/红(4)——与本层新 6 档色序不一致,本层 6 档是新权威,旧 4 档冻结不删不改(收编是独立任务 O3)

### 关键自治决策(详见设计稿 §一注 / §3.3 / §2.1 注)
1. 定位为纯逻辑 helper + POCO,UI 投放不做(沿用 15/16「数据层先行、UI 独立后续」基调)
2. 新建权威 6 档品质色(对齐道具 EItemQuality:白/绿/蓝/紫/橙/红),收编旧 4 档但本轮不删旧
3. 灵力(Soul)无 num_id(num 表只有 Exp/Piety/Diamond/Energy),走本层私有展示映射,不假设不存在的 num_id
4. 盲盒 Pattern 只带等级不带具体图案,展示用代表图案 Diamond + 等级文案降级
5. 数量文本复用 NumericFormat 不重写;单件不显 "x1"

---

## 交接区(交开发)

### dev 改动清单(详见设计稿 §五;符号名经 grep 核实)
- **C1** 新建 `RewardView`(readonly struct)+ `RewardBadge`(enum)。位置建议 `GameLogic/UI/BlockBlastUI/`(与 `NumericDisplay.cs` 同目录)或 `GameLogic/Module/BlockBlast/Reward/`。字段:IconName(string)/NameTextId(int)/CountText(string)/QualityColor(Color)/Badge(RewardBadge)/RawAmount(long)。RewardBadge:None/Currency/Pattern/Material/Gift/Function
- **C2** 新建静态 `RewardDisplay`:`From(GrantPayload)` / `From(ChestReward)` / `FromNumeric(numId,amount)` / `FromItem(itemId,count)` / `FromPattern(MergeElement,level,count)` / `GiftView` / `FunctionView` / `ChestCurrencyView` + `QualityColor(int)` 6 档 + `CountText(long)` / `PatternCountText(int,long)` + `PatternQuality(int)`。只读既有注册表,不改既有方法
- **C3** `ChestRewardKind` → 展示元数据私有映射(内置 RewardDisplay):Soul/Energy/Pattern/UndoCharge/WishCharge
- **C4**(可选,O2 建议写)新建 `RewardItemWidget : UIWidget`,`ScriptGenerator` + `SetData(RewardView)`(仿 ui-patterns 模板)。**不挂 prefab、不投放**,编译通过即可。`Image.SetSprite` 内置缓存池无需手动释放
- **T1** 新建 `RewardDisplayTests.cs`(仿 `NumericSystemTests.cs` 位置 `Assets/Editor/Tests/BlockBlast/`):V/Q/N/B 类。涉及注册表查询用 `NumericConfigMgr.InitForTest` / `ItemConfigMgr.InitForTest` 注入绕 ConfigSystem
- **不碰的文件(零回归)**:`ItemGrant.cs`/`ChestSystem.cs`/`NumericConfigMgr.cs`/`ItemConfigMgr.cs`/`MergeElementVisual.cs`/`NumericDisplay.cs` 全部只读不写

### 验收标准(18 条,test 逐条核对;全纯逻辑,无 .bytes/YooAsset 依赖)
| # | 验收点 | 完成定义 |
|---|--------|---------|
| V1 | GrantPayload(Numeric)→View | 灌经验 num(id=1),`From(GrantPayload{Numeric,1,5000})`→ Icon/Name==注入值,CountText=="x5K",Badge==Currency,RawAmount==5000 |
| V2 | GrantPayload(Pattern)→View | `From(GrantPayload{Pattern,100,4,2})`(钻石Lv2×4)→ Icon=="pattern_100",Badge==Pattern,CountText=="Lv2 x4",QualityColor==QualityColor(PatternQuality(2)) |
| V3 | GrantPayload(None材料)→View | 灌材料道具,`From(GrantPayload{None,道具id,3})`→走 FromItem,Icon/Name==道具值,Badge==Material |
| V4 | GrantPayload(Gift*)→View | `From(GrantPayload{GiftRandom,6001,0,0,3})`→Badge==Gift,CountText 反映 Times |
| V5 | ChestReward(Energy)→View | 灌体力 num(id=4),`From(ChestReward{Energy,12})`→走 FromNumeric(4,12),CountText=="x12",Badge==Currency |
| V6 | ChestReward(Soul)→View | `From(ChestReward{Soul,200})`→Badge==Currency,Icon=="Soul",不抛(灵力无 num_id 也不崩) |
| V7 | ChestReward(Pattern)→View | `From(ChestReward{Pattern,1,3})`(Lv3×1)→Badge==Pattern,CountText 含 "Lv3",品质==QualityColor(PatternQuality(3)) |
| V8 | ChestReward(Undo/Wish)→View | `From(ChestReward{UndoCharge,2})`→Badge==Function,CountText=="x2",品质退化白 |
| V9 | FromNumeric/FromItem 直调 | `FromNumeric(1,200)` 字段对;`FromItem(道具id,1)` CountText=="" (单件不带 x1) |
| Q1 | 6 档品质色齐全互异 | `QualityColor(1..6)` 两两不等;1==白(0.85,0.85,0.85) |
| Q2 | 品质越界退化白 | `QualityColor(0)/(7)/(-1)` 均==白 |
| Q3 | 品质色与设计稿一致 | 逐档 RGB==§3.3 表(2绿/3蓝/4紫/5橙/6红,容差0.01) |
| N1 | CountText 复用 NumericFormat | `CountText(200)=="x200"`;`CountText(999999)=="x999.9K"`(截断);`CountText(1)==""`;`CountText(0)=="x0"` |
| N2 | PatternCountText 带等级 | `PatternCountText(2,3)=="Lv2 x3"`;`PatternCountText(1,1)=="Lv1"` |
| B1 | num_id 查无降级不抛 | 不 InitForTest,`FromNumeric(999,100)`→Icon==null/Name==0/品质白,CountText 仍 "x100",不抛 |
| B2 | itemId 查无降级不抛 | `FromItem(999999,1)`→降级(icon null/name 0/白),不抛 |
| Z1 | 全链纯逻辑无 ConfigSystem | V/Q/N/B 全部直调/InitForTest 跑通(即证未触 YooAsset/Unity 运行时) |
| Z2 | 既有 EditMode 零回归 | EditMode 全量跑,既有用例全绿,新增另计 |

- **BLOCKED 条件**:本系统纯逻辑无 .bytes 依赖(与 16 不同,无 C 类配置直读测试),理论无 BLOCKED;唯一可能 unityMCP 桥不可达致 EditMode 跑不起来(no_session)→判 BLOCKED 不判 FAIL,可备选 batchmode 跑 EditMode(boss memory 11)

### 待 boss/用户裁决(范围开关,不阻塞,默认按本轮不做)
- O2:本轮是否写 RewardItemWidget 骨架(默认建议写,给后续接 UI 留模板,成本低)
- O3:何时收编旧 4 档 NumericDisplay.QualityColor(独立正名任务)
- O6:灵力 Soul 是否补进 num 表统一管理(默认走本层私有映射)
- O7:盲盒 Pattern 是否补具体图案种类 + 图案品质映射档位(默认代表图案 Diamond + 等级)
- O1/O4/O5/O8 见设计稿 §七

上一单 item-system 已于 2026-06-14 关单 PASS,归档 `pipeline/archive/2026-06-14-item-system/plan.md`。
