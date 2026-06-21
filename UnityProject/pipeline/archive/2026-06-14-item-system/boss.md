# 关单总结:item-system — 道具底层(2026-06-14;自治模式·放手默认)

**结论:PASS 交付**。两段:full(plan)→ dev-test。**打回 0**。git 基线 `31ed24a8`。设计基线 `design-docs/16-item-system.html`(验收 C1-6/R1-3/G1-4/U1-5/B1-5/Z1-2;详见同目录 plan/dev/test.md)。

## 运行验证(test 经 MCP,UnityProject@02a6dcaa)
- 编译 0 error;EditMode `BlockBlast.Tests` **234/234**(209 基线 + 25 新 ItemSystemTests),零回归。
- Play 走生产真实加载链(ConfigSystem→YooAsset→item_tbitemdef.bytes):GetItem(30006) 逐字段对、礼包池 6001/5001 命中、ResolveAndApply 随机礼包递归落点对。
- Code Review 5 红线全过;源 xlsx ##var 列序 ↔ 生成 ItemDef.cs 读序 ↔ ToItemDef 桥接逐字段对齐,字节布局无错位。

## 实现范围(加法式,不改既有代码路径)
Luban 3 表(item.TbItemDef 20 列 / TbGiftRandom / TbGiftSelect)+ 2 枚举(EItemQuality 1-6 / EItemType 1·2·3·5·6 跳 4)→ 导表生成 + .bytes;运行期 ItemConfigMgr(按 id/index 查 POCO)+ GiftOpener(权重抽,注入 Random(seed) 可测)+ ItemGrant(GrantPayload/Resolve + 适配器调既有 AddDirect/RefundEnergy/AddPiety/Exp,不复制发奖)+ ItemBag(叠加 999→999+/占格/容量 100)。文件:Luban 源 3 新+2 改、生成 9、手写逻辑 5、ItemSystemTests 25 例。

## 关键拍板
- 新建 item.TbItemDef(现有 item.xlsx 是 TEngine 模板示例、字段冲突、无业务消费者 → 不动它,新表并存);新建 EItemQuality 1-6(现有 EQuality 仅 4 档色序不符且被模板引用)。
- boss O2:接受 spec 单「参数」拆 use_value/use_num/use_level+param(单字段表达不下 target+num+level)。
- 钻石 num_id=3 无 MergeOrderState 字段 → ApplyNumeric 返 false 不落(去变现,数值系统未实装钻石)。
- stub(字段进表逻辑不做):限时 Term/Compensate→Reward/邮件、背包溢出邮件补发(改丢弃+TODO)、JumpList、真实 Sprite/Light、UI、背包存档、货币迁背包。

## 模型档 / 运行登记
- plan/dev/test=opus。第一段 Run `wf_30edb689-a6c`(Task `whstdgzrx`,full,BLOCKED@plan/O2);第二段 Run `wf_4be67e8f-db8`(Task `whj0ne33c`,dev-test)PASS。

## 遗留/观察(转主 boss.md #19)
- GrantOnAcquire(dev 自发新增的 Automatic 分流 + 礼包递归,深度上限 5)无单测兜底(本轮经 Play 反射直调验证逻辑正确);且源 xlsx 30006 automatic=0 与测试夹具 automatic=1 不一致(automatic 非任一验收项、无 test 调 GrantOnAcquire,故不影响验收)。两者都待 GrantOnAcquire 接到真实调用方时一并补单测 + 统一 automatic 语义。
