# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:数值底层系统(xlsx 批次第一刀)

设计稿:`design-docs/15-numeric-system.html`(已挂 index.html + 全库 sidebar 文档树同步,11-core-loop 为遗留 topbar 布局无树、不在同步范围)。

### 一句话
用 Luban 货币表统一登记各数值的元数据(名称文本id/图标资源名/类型/品质)+ 运行期按 num_id 查 + 全局显示格式化(0–999 / K / M 一位小数截断)+ 可复用显示 helper。加法式:只建框架,不重构现有 Soul/Piety/Exp/Energy 字段。

### 工程基线(已 grep 核实,dev 据此定位)
- Luban 源:仓库根 `D:/work/TEngine_block/Configs/GameConfig/`(与 UnityProject 同级)。`Datas/__tables__.xlsx`(注册)/`__enums__.xlsx`/`__beans__.xlsx` + 各数据 xlsx(schema 写表头四行 ##var/##type/##group/##)。范本:`item.xlsx`、`weightcfg.xlsx`。
- 导表脚本:`Configs/GameConfig/gen_code_bin_to_project_lazyload.bat`(Windows,`set AI_MODE=1 && ...`)。生成代码落 `Assets/GameScripts/HotFix/GameProto/GameConfig/`,二进制落 `Assets/AssetRaw/Configs/bytes/<module>_<table>.bytes`,`Tables.cs` 自动加懒加载属性。
- 运行期加载:`ConfigSystem.Instance.Tables.TbXxx`(`GameScripts/HotFix/GameProto/ConfigSystem.cs`),内部走 `ModuleSystem.GetModule<IResourceModule>()` + 同步 `LoadAsset<TextAsset>` + YooAsset。**EditMode/纯 C# 跑不通**。
- 配置桥接范本:`GameLogic/Config/WeightCfgConfigMgr.cs`(Luban 行 → POCO `WeightConfigEntry`,业务侧只认 POCO)。
- 配置表 EditMode 测试范本:`Editor/Tests/BlockBlast/WeightCfgLubanTests.cs`——`AssetDatabase.LoadAssetAtPath<TextAsset>(".../xxx.bytes")` → `new TbXxx(new Luban.ByteBuf(ta.bytes))`,绕过 YooAsset 直读二进制(已验证可在 EditMode 跑)。
- 现有 ad-hoc 货币:`MergeOrderState.Soul/Piety/Exp/Energy`(均 public int)。显示硬编码在 `MergeOrderWindow`(如 line 321 `RefreshPiety`:`_pietyText.text = $"✦ {_merge.Piety}"`)。`MergeOrderConfig.EnergyStart=20`/`EnergyCap=30`。
- 现成枚举范本:`item.EQuality`(WHITE=1/BLUE=2/PURPLE=3/RED=4)。
- 无既有 K/M 格式化 helper;merge-order UI 无 Sprite 加载(全 emoji glyph + 程序化 `UGuiFactory`),grep 无 `SetSprite`/`LoadSpriteAsync`。
- 现有 EditMode 测试约 190 例(BlockBlast 套件)。

### dev 改动清单(详见设计稿 §五,符号已 grep 核实)
1. `Configs/GameConfig/Datas/__enums__.xlsx`:追加枚举 `num.ENumType`(EXP/PIETY/DIAMOND/ENERGY = 1–4),仿 item.EQuality。
2. `Configs/GameConfig/Datas/__tables__.xlsx`:追加注册 `num.TbNum`(value_type=Num / input=num.xlsx / index=id / mode=map / read_schema_from_file=true)。
3. `Configs/GameConfig/Datas/num.xlsx`(新):8 字段 schema(id/desc/func_name/name/icon/num_type/planner_notes/quality)+ 4 行样例(覆盖 num_type 1–4)。字段类型/分组见设计稿 §3.1 表(planner_notes group=e 不导出运行期;func_name group=s;num_type 用 num.ENumType)。
4. 跑导表脚本生成 `GameConfig.Num`/`num.TbNum`/`num.ENumType` 代码 + `num_tbnum.bytes`(生成代码不手改)。
5. `GameLogic/Module/BlockBlast/Numeric/NumericEntry.cs`(新):POCO(NumId/DescTextId/FuncName/NameTextId/IconName/NumType/Quality)。
6. `GameLogic/Config/NumericConfigMgr.cs`(新):`ToEntry` + `EnsureLoaded`(经 ConfigSystem 懒加载缓存)+ `Get(numId)` + `GetByType` + `InitForTest`(测试注入)+ num_id 约定常量(Exp=1/Piety=2/Diamond=3/Energy=4),仿 WeightCfgConfigMgr。
7. `GameLogic/Module/BlockBlast/Numeric/NumericFormat.cs`(新):纯函数 `Abbreviate(long)` + 旋钮常量(K_THRESHOLD=1000/M_THRESHOLD=1000000/DECIMALS=1/TRUNCATE=true)。与配置无关。
8. `GameLogic/UI/BlockBlastUI/NumericDisplay.cs`(新):`Format`/`FormatWith(numId,amount)`/`IconName`/`QualityColor`。真实 Sprite 加载本轮不做。
9. `Editor/Tests/BlockBlast/NumericSystemTests.cs`(新):验收点见下。
10. (可选,默认不做)`MergeOrderWindow.RefreshPiety` 一处示范接入 helper。

### 验收标准(逐条 test 可核对;设计稿 §六)
格式化(纯逻辑,无依赖):
- F1 `Abbreviate(0)=="0"`
- F2 `Abbreviate(999)=="999"`
- F3 `Abbreviate(1000)=="1K"`(去尾随 0)
- F4 `Abbreviate(1500)=="1.5K"`
- F5 `Abbreviate(999999)=="999.9K"`(截断,不进位、不误进 M —— spec 示例)
- F6 `Abbreviate(1000000)=="1M"`
- F7 `Abbreviate(1500000)=="1.5M"`
- F8 `Abbreviate(9999999)=="9.9M"`
- F9 `Abbreviate(100000000)=="100M"`(超 spec 上界续用 M,本轮无 B 档)

配置表(AssetDatabase 直读 num_tbnum.bytes,仿 WeightCfgLubanTests):
- C1 加载 `DataList.Count==4`
- C2 `Get(2)`(虔诚币):num_type==2、quality==3、icon=="icon_piety"、name==100002,逐字段 == 表填值
- C3 4 行 num_type 恰为 {1,2,3,4} 各一;id 唯一 {1,2,3,4}

注册表 / helper(InitForTest 路径,纯逻辑):
- R1 `ToEntry(row)` 各字段 == Luban 行对应字段
- R2 `Get(2)` 返虔诚币 entry;`Get(999)` 返 null(不抛)
- R3 `GetByType(4)` 返体力 1 条;`GetByType(99)` 返空集合(不抛)
- R4 `NumericDisplay.Format(999999)=="999.9K"`;`FormatWith(Piety,1500)` 含 "1.5K";`IconName(2)=="icon_piety"`

回归 / 边界:
- Z1 NumericFormat 与 NumericConfigMgr(InitForTest 路径)不 using YooAsset、不调 ConfigSystem,可纯 C# 单测直接调
- Z2 现有 190 例 EditMode 全绿;现有货币字段读写零改动
- Z3 含生成代码 GameConfig.num.* + 4 新文件,GameLogic/GameProto 程序集编译无错

### BLOCKED 边界(任务硬约束)
Luban 导表工具链(dotnet/Luban.dll)或 unityMCP 桥不可达 → test 判 BLOCKED 不判 FAIL,交接区写清卡点。纯逻辑测试(F/R/Z1)不依赖导表,可先验;C 档依赖导表产物。

### 自主拍板(decisions,设计稿 §七)
- O1 现有货币数量值本轮不迁移到统一钱包(additive,统一钱包动核心循环+存档+悔棋,列后续轮)
- O2 灵力(Soul)本轮不登记(spec num_type 只列 1–4 无灵力,严格照 spec 填 4 行)
- O4 helper 图标本轮到「资源名」,真实 Sprite 加载列后续(无既有先例+无美术资源)
- O7 品质字段默认裸 int(严格照 spec int32),dev 可选复用 EQuality,验收不变
- 格式化默认截断(TRUNCATE=true)保 999999→999.9K 与 spec 示例一致;超 9999999 续用 M(O6 无 B 档)

### 交 dev/boss 的开关(设计稿 §七)
- O3 是否本轮把 MergeOrderWindow 货币显示改走 helper:默认不做(表现层重构,放大回归面),boss 要见效果可选一处示范接入
- O5 配置加载沿用 ConfigSystem 现有同步 LoadAsset(改异步影响全工程,不在本轮)
