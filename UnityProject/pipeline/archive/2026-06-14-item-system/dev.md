# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:道具底层系统(item-system,xlsx 批次第二刀)

设计基线:`design-docs/16-item-system.html`。验收标准见 `pipeline/state/plan.md` 交接区(C1-6/R1-3/G1-4/U1-5/B1-5/Z1-2)。

## 交接区:给测试

### 改动摘要(做了什么、为何这么做)

加法式新建道具系统的数据层 + 纯逻辑层,不改任何既有代码路径(MergeOrderState/MergeElementVisual/NumericConfigMgr/模板 item 表全未动)。四层:

1. **配置层(Luban,3 表 + 2 枚举)**:新建 `item.TbItemDef`(20 列:18 spec 字段 + use_value/use_num/use_level 拆分 + jump_list)、`item.TbGiftRandom`、`item.TbGiftSelect`(均 5 列 auto_id/index/item_id/num/rate)。新建枚举 `item.EItemQuality`(1-6)、`item.EItemType`(1/2/3/5/6,跳过 4)。全部 schema-in-file(`read_schema_from_file=True`)、`mode=map`、group=c,s。按 boss O2 拍板拆 `参数`→use_value/use_num/use_level + param(已实现,未改回单字段)。
2. **注册表层**:`ItemConfigMgr`(静态,懒加载缓存,Luban 行→POCO 桥接,按 id/index 查;`InitForTest` 注入绕 ConfigSystem)。POCO:`ItemDef` / `GiftEntry`(隔离 Luban 类型)。
3. **礼包层**:`GiftOpener`(`RollRandom` 权重抽注入 System.Random / `OpenRandom` 开 N 次 / `ListSelectable` 自选返候选)。
4. **产出层**:`ItemGrant`(`GrantPayload` 结构 + `Resolve` 解析 UseEffect + 适配器 `ApplyNumeric`/`ApplyPattern` 调既有方法 + `GrantOnAcquire` 按 Automatic 分流,随机礼包递归带深度上限 5)。
5. **背包层**:`ItemBag`(StackCap=999 / SlotCap=100 / Add 返实际放入量 / Remove / Count / SlotUsed / FormatCount 纯函数)。

关键决策:
- 产出落点适配器只**调**既有 `MergeOrderState.Exp/AddPiety/RefundEnergy/AddDirect`,不复制发奖逻辑(避免漂移)。num_id→字段映射用 `NumericConfigMgr` 常量(Exp=1/Piety=2/Diamond=3/Energy=4)。**钻石(num_id=3)无既有字段,`ApplyNumeric` 返 false 不落**(去变现,数值系统未实装钻石字段)。
- Luban 生成的道具行类是 `GameConfig.ItemDef`,与我的 POCO `GameLogic.BlockBlast.Item.ItemDef` 同名——ConfigMgr 桥接处全限定 `GameConfig.ItemDef` 消歧,编译通过无冲突。
- `ItemBag` 查叠不叠走 `ItemConfigMgr.GetItem(id).Stacking`,查不到当不可叠(保守占格)。
- 背包溢出(超 999 / 超 100 格)按 O6 丢弃,代码注释标 TODO,不发邮件。

### 文件清单

**Luban 源(仓库根 Configs/,新增/改)**:
- 新增 `Configs/GameConfig/Datas/itemdef.xlsx` / `giftrandom.xlsx` / `giftselect.xlsx`
- 改 `Configs/GameConfig/Datas/__tables__.xlsx`(加 3 行注册)、`__enums__.xlsx`(加 2 枚举)

**导表产物(已跑 `gen_code_bin_to_project_lazyload.bat`,DOTNET_ROLL_FORWARD=Major + AI_MODE=1)**:
- `Assets/AssetRaw/Configs/bytes/item_tbitemdef.bytes` / `item_tbgiftrandom.bytes` / `item_tbgiftselect.bytes`(+ .meta)
- 生成代码 `Assets/GameScripts/HotFix/GameProto/GameConfig/`:`ItemDef.cs` / `GiftRandom.cs` / `GiftSelect.cs` / `item.TbItemDef.cs` / `item.TbGiftRandom.cs` / `item.TbGiftSelect.cs` / `item.EItemQuality.cs` / `item.EItemType.cs` / `Tables.cs`(改:加 3 表属性)

**手写热更代码(GameLogic 程序集,全热更)**:
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Item/ItemDef.cs`(POCO)
- `.../Item/GiftEntry.cs`(POCO)
- `.../Item/GiftOpener.cs`
- `.../Item/ItemGrant.cs`
- `Assets/GameScripts/HotFix/GameLogic/Config/ItemConfigMgr.cs`

**单测**:
- `Assets/Editor/Tests/BlockBlast/ItemSystemTests.cs`(新增 25 例)

### 验证点(逐条对应验收标准,该验什么、怎么验、预期)

dev 已本机自验:`run_tests` EditMode 全量 **234/234 通过**(原 209 + 新 25,0 失败 0 跳过)。编译 0 报错(read_console 无 CS,域重载完成 idle)。逐条:

- **C1-C6(配置表,直读 .bytes)**:已过。C2 锚 id=30006(type=6/use_effect=4/use_value=6001/param=1/quality=5/stacking=0);C3 品质覆盖 1-6 且 MYTH==6;C4 EItemType 1/2/3/5/6(跳 4);C5 随机池 6001 聚 4 项权重和 100;C6 自选池 5001 聚 3 项。
- **R1-R3(注册表)**:已过。R1 GetItem 命中/未命中(999999→null);R2 GetGiftRandom(6001).Count==4 + 查无空集;R3 从真实行 ToItemDef 逐字段保真。
- **G1-G4(礼包抽样)**:已过。G1 同种子(12345)连抽 50 次序列两次完全相同;G2 抽 10000 次频率落 {50/30/15/5}% ±3%;G3 边界(空 null/单项必中/全 0 取首/负当 0/times≤0 当 1);G4 自选返 3 项按表序。
- **U1-U5(+U5b,UseEffect)**:已过。U1 经验→Numeric/1/5000;U2 钻石图案→Pattern/100/Amount=4(use_num2×count2)/Level=2;U3 自选→GiftSelect/5001,随机→GiftRandom/6001/Times=param;U4 普通材料→None;U5 ApplyPattern 经既有 AddDirect 落收集区(注意:AddDirect 单链级联,Diamond Lv2×4 → 剩 2 个 Lv2 + 1 个 Lv3,非 2 个 Lv3——测试断言已按真实级联语义修正);U5b ApplyNumeric 经验落 Exp、钻石(num_id=3)返 false 不落。
- **B1-B5(背包)**:已过。B1 可叠累加占 1 格;B2 夹 999 + 返实际放入量 + FormatCount(1500/999)=="999+"、(998)=="998";B3 不可叠 3 个占 3 格;B4 占满 100 格后 Add 返 0 已有不变;B5 Remove 到 0 释放格子。
- **Z1**:已过(InitForTest 路径全在 EditMode 跑通,本身即证未触 YooAsset)。
- **Z2 零回归**:已过。234 总数 - 25 新增 = 209 原例全绿。

### 标注

- **涉及热更程序集**:是。手写代码全在 `GameScripts/HotFix/GameLogic`(全热更)+ 生成代码在 `GameProto/GameConfig`(热更)。
- **需 Luban 重生成**:已完成(本轮已导表,.bytes + 生成 .cs 已落盘并 Unity 导入)。test 若要复跑导表:`cd Configs/GameConfig` → PowerShell `$env:DOTNET_ROLL_FORWARD="Major"; $env:AI_MODE="1"; & ".\gen_code_bin_to_project_lazyload.bat"`。
- **需进 Play 模式手验**:无。本轮纯数据层 + 纯逻辑,无 UI、无运行期入口接线(GrantOnAcquire 已实现但本轮无调用方接它,O9 UI 后续)。EditMode 单测全覆盖。

### BLOCKED 说明

无阻塞。Luban 工具链可达(已成功导表)、unityMCP 桥可达(已 run_tests)。

## 自主拍板的取舍(decisions)

1. 按 boss O2 拍板拆 use_value/use_num/use_level + param 实现(未改回 spec 单字段)。
2. itemdef.xlsx 各样例行补了 name/desc 占位文本 id(110001-110008 / 210001-210008),icon 占位名(icon_item_*/icon_gift_*),term 全填 0(本轮 stub)。设计稿只给关键列,补全列是必须的(Luban 整行)——按 §3.3 样例语义补,不偏离。
3. 用 openpyxl 直接写三张数据 xlsx 的四表头 schema-in-file(helper 的 table add --no-auto-import 不填 read_schema_from_file,且数据行需精确控制)——枚举仍走 helper(它处理 *items 布局)。
4. ItemGrant 加 `GrantOnAcquire` + 递归深度上限 5(设计稿 §八 风险表要求防礼包自指环),并补 U5b 测 ApplyNumeric 落点(设计稿 §3.7 适配器示范,验收 U 类只列 Resolve+ApplyPattern,我加 ApplyNumeric 覆盖更全)。
