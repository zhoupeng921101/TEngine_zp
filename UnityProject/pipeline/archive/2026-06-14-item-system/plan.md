# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:道具底层系统(item-system,xlsx 批次第二刀)

设计稿:`design-docs/16-item-system.html`(已挂 index.html + 全库 12 篇 sidebar 文档树同步 + 断链检查通过)。

### 一句话
用 Luban 道具表登记道具元数据 + 礼包开启(权重抽/自选)+ 基础背包容器(叠加 999 / 占格 / 容量 100);加法式,产出经 UseEffect 落既有数值/图案系统,不重构既有系统。

### 关键拍板(理由详见设计稿对应节)
- **新建 vs 扩展现有 item 表 → 新建**(§3.1):现有 `item.xlsx`/`Item.TbItem` 是 TEngine 模板示例(服装/price/exchange_stream,无业务消费者,grep 仅生成代码引用),字段与 spec 冲突。新建独立表 `item.TbItemDef`,导出 `item_tbitemdef.bytes`,与模板 `item_tbitem.bytes` 并存不撞。
- **品质枚举新建 `item.EItemQuality`(1–6),不复用 `EQuality`(1–4)**(§3.3):现有 EQuality 仅 4 档、色序(白蓝紫红)与 spec(白绿蓝紫橙红)不符,且被模板表引用。
- **spec「参数」字段拆成 use_value/use_num/use_level + param**(§3.2 字段补充说明):单一「参数」表达不下「目标id+数量+等级」;spec 字段全保留,只拆清过载字段。列 §七 O2 供 boss 确认。
- **EItemType 照 spec 跳过 value=4**(§3.3,O3)。
- **ItemGrant 只产出结构、不复制发奖逻辑**(§3.7):适配器调既有 `MergeOrderState.AddDirect`/`RefundEnergy`/`AddPiety`,避免两处漂移。
- **stub 项(字段进表、逻辑不做)**:限时整套 Term/TermTime/Compensate→Reward/CompensateEmail→邮件(O5)、背包溢出邮件补发(O6,改为丢弃+记TODO)、JumpList 跳转(O7)、真实Sprite/Light(O8)、背包UI/礼包UI(O9)、背包存档(O10)、货币迁进背包(O4)。

### 给开发的 dev 改动清单(符号经 grep 核实,详见设计稿 §五)
- **配置(L1–L3)**:`Configs/GameConfig/Datas/` 新建 3 数据 xlsx(itemdef/giftrandom/giftselect,表头四行 schema)+ `__tables__.xlsx` 加 3 行(read_schema_from_file=True、index、mode=map、group 走默认 c,s)+ `__enums__.xlsx` 加 2 枚举。导表:`$env:DOTNET_ROLL_FORWARD="Major"` 后跑 `gen_code_bin_to_project_lazyload.bat`;新表 luban_helper `--no-auto-import` + 手填 read_schema_from_file=True(dev memory Luban 条 24–26)。
- **代码(C1–C5)**:`ItemDef`/`GiftEntry` POCO、`ItemConfigMgr`(仿 `NumericConfigMgr.cs`)、`GiftOpener`(注入 System.Random)、`ItemGrant`(GrantPayload/Resolve/适配器)、`ItemBag`。放 `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/Item/`(或 Config/ 仿 NumericConfigMgr)。
- **既有方法只调不改**:`MergeOrderState.AddDirect`(MergeOrderState.cs:244)、`RefundEnergy`(:218)、`AddPiety`(:475)、`Exp`(:147);`MergeElement` 枚举值(MergeElementVisual.cs:10,Diamond=100…Stone=200)。
- **不碰**:现有 `item.xlsx`/`Item.TbItem`、`MergeOrderState` 字段、`NumericConfigMgr`、`MergeElementVisual`。
- **单测(T1)**:新建 `Assets/Editor/Tests/BlockBlast/ItemSystemTests.cs`(仿 `NumericSystemTests.cs`)。

## 交接区:验收标准(test 逐条核对)

> 配置类(C)经 `AssetDatabase.LoadAssetAtPath<TextAsset>(".../bytes/item_tbitemdef.bytes")` 直读 → `new GameConfig.item.TbItemDef(new Luban.ByteBuf(ta.bytes))`(仿 NumericSystemTests,绕 YooAsset)。其余(R/G/U/B)纯逻辑 InitForTest/new。完整表见设计稿 §六。

**配置表(C1–C6)**
- C1 道具表导出且行数 = 样例行数(≥8):直读 item_tbitemdef.bytes,DataList.Count≥8
- C2 道具字段对源:id=30006 → type=6/use_effect=4/use_value=6001/param=1/quality=5/stacking=0
- C3 品质枚举 1–6 齐全:quality 取值覆盖 {1..6};(int)EItemQuality.MYTH==6
- C4 类型枚举符 spec(跳 4):GIFT_SELECT==5 && GIFT_RANDOM==6 && CURRENCY==1
- C5 随机礼包导出 + index 聚合:item_tbgiftrandom.bytes,index=6001 聚 4 项,权重和=100
- C6 自选礼包导出:item_tbgiftselect.bytes,index=5001 聚 3 项

**注册表(R1–R3)**
- R1 按 id 查命中/未命中:GetItem(30001) 非 null 字段对;GetItem(999999) 返 null 不抛
- R2 按 index 查礼包池:GetGiftRandom(6001).Count==4;查无返空集合不抛
- R3 POCO 桥接保真:从真实 .bytes 行 ToItemDef,逐字段 == row 原值

**礼包抽样(G1–G4)**
- G1 确定性:同 new Random(seed)+同奖池,RollRandom 连抽 N 次序列两次运行完全相同
- G2 分布(大样本):样例奖池抽 10000 次,各 item_id 命中频率落 {50%,30%,15%,5%} ±3%
- G3 边界:空池返 null;单项必中;全 0 权重退化取首项;负权重当 0;times≤0 当 1
- G4 自选返回完整候选:ListSelectable(5001).Count==3,顺序=表序

**UseEffect 解析(U1–U5)**
- U1 =1 货币:Resolve(经验道具,1) → Numeric,TargetId=1,Amount=5000
- U2 =2 图案(含等级):Resolve(钻石图案道具,2) → Pattern,TargetId=100,Amount=4,Level=2
- U3 =3/4 礼包:自选→GiftSelect,TargetId=5001;随机→GiftRandom,TargetId=6001,Times=param
- U4 =0 纯持有:Resolve(普通材料,3) → None
- U5 Pattern 适配器落既有 AddDirect:ApplyPattern 后 MergeOrderState.InventoryCount 反映注入

**背包(B1–B5)**
- B1 可叠累加占 1 格:同 id(stacking=1)Add 两次,Count=和,SlotUsed=1
- B2 叠加上限 999 + 显示 999+:Add 超 999 夹 999,返回实际放入量<请求量;FormatCount(1500)=="999+",FormatCount(998)=="998"
- B3 不可叠单独占格:stacking=0 Add 3 个,SlotUsed=3
- B4 容量上限 100 拒绝:占满 100 格后 Add 返回 0,已有不变
- B5 移除释放格子:Remove 到 0 该 id 消失,SlotUsed 减少

**隔离 / 回归(Z1–Z2)**
- Z1 全链纯逻辑无 ConfigSystem:G/U/B 类 InitForTest/new 跑通(本身即证未触 YooAsset)
- Z2 既有 209 例零回归:EditMode 全量跑,原 209 例(201 [Test]+8 [TestCase])全绿

**BLOCKED 条件**:Luban 工具链不可达(dotnet/Luban.dll 跑不起 / 导表失败)或 unityMCP 桥 no_session → test 判 BLOCKED 不判 FAIL,交接区写清卡点。导表 BLOCKED 时 C 类无法跑,但 R/G/U/B 的 InitForTest 纯逻辑路径仍可单独跑(不依赖 .bytes)。

## 待 boss/用户裁决(范围开关,均不阻塞本轮,默认按"本轮不做")
见设计稿 §七 O1–O10。重点:O2(参数字段拆分,需确认是否接受偏离 spec 单字段)。其余为 stub/后续投放开关,默认推进。
