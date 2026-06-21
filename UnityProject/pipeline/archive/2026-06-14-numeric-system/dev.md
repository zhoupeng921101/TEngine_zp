# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:数值底层系统(xlsx 批次第一刀)

设计基线:`design-docs/15-numeric-system.html`;验收标准 = plan.md 交接区 F1-9 / C1-3 / R1-4 / Z1-3。
boss O3:示范接入改为必做(item 10)。

### 交接区 → 给测试

#### 改动摘要

实现配置化数值系统:Luban 货币表(num.TbNum + num.ENumType 枚举 + 4 行覆盖 num_type 1-4)→ 导表生成 GameConfig 代码 + num_tbnum.bytes → 运行期注册表(NumericConfigMgr 按 num_id 查 POCO)+ 全局格式化(NumericFormat 整数→K/M 显示串)+ 显示 helper(NumericDisplay)。加法式:现有 Soul/Piety/Exp/Energy 字段读写零改动。

关键决策与实现细节:

1. **Luban group 写法 c,s 而非 cs**:luban.conf 的 group 名是单字符 c/s/e 三个,client 导出目标 groups=["c"]。字段 group 必须写逗号分隔的 `c,s`(两个有效组名),不能写 `cs`(无此组名,会被 client 目标判为不匹配而整列丢弃)。首次按设计字面写 `cs` 导出后 Num.cs 缺了 id/desc/num_type 三列、TbNum.cs 引用 _v.Id 编译失败;改 num.xlsx ##group 行 cs→c,s 重导即修复。num.xlsx 现状:id/desc/num_type=`c,s`、func_name=`s`、name/icon/quality=`c`、planner_notes=`e`。
2. **func_name(group=s)与 planner_notes(group=e)不导出到客户端**:client 目标只导 c 组,故生成的 GameConfig.Num 不含这两字段。NumericEntry POCO 随之不含 FuncName(客户端读不到)。NumericDisplay.FormatWith 的标签兜底改用 NameTextId(原设计 §3.5 用 FuncName,客户端不可得)。验收点不依赖 FuncName,不受影响。
3. **导表环境**:dotnet 装的是 8/9/10,Luban.dll 目标 .NET 7.0 runtime(本机无 7.0)。直接跑 .bat 报 "must install .NET 7.0"。用 `DOTNET_ROLL_FORWARD=Major` 环境变量让 7.0 应用在更高 runtime 上运行,导表成功。(.bat 脚本本身未改;若后续 CI 复跑需带此环境变量,或装 .NET 7.0 runtime。)
4. **NumericFormat 全整数运算**:Scale 用 `abs*pow/unit` 整数先乘后除向零截断,不走浮点,避免 999999 浮点误差误进位。F5(999999→999.9K)、F8(9999999→9.9M)逐档验证通过。负数保符号,long.MinValue 单独兜底防取绝对值溢出。
5. **示范接入(O3 必做)**:MergeOrderWindow.RefreshPiety 的 `✦ {_merge.Piety}` 改为 `✦ {NumericDisplay.Format(_merge.Piety)}`。选 Piety 因其无封顶、长期增长(Energy 封顶 30、Exp 也受限,不会触发 K/M)。保留 ✦ glyph,只把数字走格式化,最小改动证端到端 + 大数 K/M 生效。

#### 文件清单

Luban 源(仓库根 Configs/GameConfig/Datas/):
- 改 `__enums__.xlsx`:追加 num.ENumType(EXP=1/PIETY=2/DIAMOND=3/ENERGY=4)
- 改 `__tables__.xlsx`:追加 num.TbNum 注册(value_type=Num/input=num.xlsx/index=id/mode=map/read_schema_from_file=true)
- 新 `num.xlsx`:8 字段 schema + 4 行样例(num_type 1-4 各一)

导表生成产物(不手改):
- 新 `Assets/GameScripts/HotFix/GameProto/GameConfig/Num.cs`(行类型,6 客户端字段:Id/Desc/Name/Icon/NumType/Quality)
- 新 `Assets/GameScripts/HotFix/GameProto/GameConfig/num.TbNum.cs`(表类型,DataList/DataMap/GetOrDefault)
- 新 `Assets/GameScripts/HotFix/GameProto/GameConfig/num.ENumType.cs`(枚举)
- 改 `Assets/GameScripts/HotFix/GameProto/GameConfig/Tables.cs`(自动加 TbNum 懒加载属性,loader key "num_tbnum")
- 新 `Assets/AssetRaw/Configs/bytes/num_tbnum.bytes`(82 字节)

新增逻辑文件:
- 新 `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Numeric/NumericEntry.cs`(POCO)
- 新 `Assets/GameScripts/HotFix/GameLogic/Config/NumericConfigMgr.cs`(桥接+缓存+查询+InitForTest/ResetForTest+num_id 常量)
- 新 `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Numeric/NumericFormat.cs`(纯函数 Abbreviate+旋钮常量)
- 新 `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/NumericDisplay.cs`(helper:Format/FormatWith/IconName/QualityColor)

改动现有文件:
- 改 `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWindow.cs`(RefreshPiety 一行,O3 示范接入)

测试:
- 新 `Assets/Editor/Tests/BlockBlast/NumericSystemTests.cs`(19 例覆盖 F/C/R/Z)

#### 验证点(逐条对应验收标准)

dev 已在本机跑通,以下供测试复核(test 自带 unityMCP 可直接复跑):

- **F1-F9 格式化**:`NumericSystemTests` 中 F1_..F9_ 各一例,断言 Abbreviate 输出串。预期全过(已验)。
- **C1-C3 配置表**:AssetDatabase 直读 num_tbnum.bytes → new GameConfig.num.TbNum(ByteBuf)。C1=DataList.Count==4;C2=GetOrDefault(2) 虔诚币 num_type/quality/icon/name/desc 逐字段;C3=num_type/id 恰 {1,2,3,4}。预期全过(已验)。
- **R1-R4 注册表/helper**:R1=ToEntry 保真(从真实 .bytes 行转);R2=Get(2) 命中 / Get(999) 返 null;R3=GetByType(4) 一条 / GetByType(99) 空;R4=Format(999999)=="999.9K" + FormatWith(Piety,1500) 含 "1.5K" + IconName(2)=="icon_piety"。预期全过(已验)。
- **Z1 纯逻辑**:InitForTest 路径不碰 ConfigSystem;能在 EditMode 跑通即证明。预期过(已验)。
- **Z2 零回归**:运行 `BlockBlast.Tests` 全套 = 209/209 passed(原 190 + 新 19),0 failed。现有货币字段读写零改动。
- **Z3 编译**:GameLogic/GameProto 含生成代码 + 4 新逻辑文件 + RefreshPiety 改动,编译 0 报错(read_console 无 CSxxxx;EditMode 测试能跑起来即编译通过的最强证据)。

#### 标注

- **涉及热更程序集**:GameLogic + GameProto 均热更(GameScripts/HotFix/)。
- **需 Luban 重生成**:本轮已重生成(num.* 代码 + num_tbnum.bytes)。后续若改 num.xlsx 需重跑 `gen_code_bin_to_project_lazyload.bat`,本机须带 `DOTNET_ROLL_FORWARD=Major`(或装 .NET 7.0 runtime)。
- **需进 Play 模式手验**:无强制项。示范接入只在 MergeOrderWindow 实跑、Piety 超 999 时才显示 K/M;Piety 数值在正常对局难快速堆到 1000+,可不手验(格式化逻辑已被 F/R 单测全覆盖)。若 test 要眼见 K/M 效果,可临时把 MergeOrderState.Piety 设大值进 MergeOrder 窗口观察,非必须。

#### 自检结论

- read_console:无 CSxxxx 编译诊断(3 条非编译:MCP 桥重启 / 资源卸载 / 无栈 NullReference=Play 模式运行期事件)。
- EditMode:`BlockBlast.Tests` 209/209 passed,0 failed,0 skipped(已退 Play Mode 后跑通)。
- 核心路径已跑:配置表往返(C)、注册表查询(R)、格式化边界(F)均绿。

### 进度

已完成全部 9 项改动清单 + O3 示范接入(item 10)。交接测试。
