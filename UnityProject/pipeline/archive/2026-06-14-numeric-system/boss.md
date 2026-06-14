# 关单总结:numeric-system — 数值底层(配置化数值系统;2026-06-14;自治模式·放手默认)

**结论:PASS 交付**。两段式:full(plan 出全设计)→ dev-test 实现(plan 在可选项 O3 上 BLOCKED,boss 解为「做示范接入」后续接)。**打回轮次 0**。git 基线 commit `108e620c`。设计基线 `design-docs/15-numeric-system.html`(F1-9/C1-3/R1-4/Z1-3;详见同目录 plan/dev/test.md)。本管线**首个真 Luban 配置表任务**。

## 运行验证(test 经 MCP,UnityProject@02a6dcaa)
- 编译 0 CS error。EditMode `BlockBlast.Tests` **209/209**(190 基线 + 19 新 NumericSystemTests),零回归。
- Play 手验走生产真实加载链(ConfigSystem→YooAsset→num_tbnum.bytes→TbNum→ToEntry):Get(2) 虔诚币逐字段对、Get(999)=null、GetByType(4)=1、Abbreviate 三档对。O3 示范接入经真实 UI 路径验:RefreshPiety 显示 ✦ 1.5M / ✦ 999.9K / ✦ 0。
- Code Review 5 红线全过;config 层源↔产物字节布局自洽核验过。

## 实现范围
Luban 货币表(num.TbNum + num.ENumType[经验/虔诚币/钻石/体力] + 4 行样例)→ 导表生成 GameConfig 代码 + num_tbnum.bytes → 运行期 NumericConfigMgr(按 num_id 查 POCO)+ NumericFormat(纯函数 K/M 整数截断)+ NumericDisplay helper + 示范接入 MergeOrderWindow.RefreshPiety。加法式:现有 Soul/Piety/Exp/Energy 字段读写零改动。文件:Luban 源 3 改/新(__enums__/__tables__/num.xlsx)+ 生成 4(Num/num.TbNum/num.ENumType/Tables 改)+ num_tbnum.bytes + 逻辑 4 新(NumericEntry/NumericConfigMgr/NumericFormat/NumericDisplay)+ MergeOrderWindow 1 行 + NumericSystemTests 19 例。

## 拍板归属
- boss 代决 O3=做示范接入(放手默认·立场朝结果:纯 helper 不接 UI = shelf-ware;接虔诚币显示证端到端,低风险一处)。
- plan/dev 自主 decisions:O1 现有货币不迁统一钱包(additive,统一钱包动核心+存档+悔棋列后续)、O2 灵力不登记(严格照 spec num_type 1-4)、O4 图标到资源名(无美术,Sprite 加载列后续)、O7 品质裸 int、格式化截断(保 999999→999.9K 与 spec 一致,超界续 M 无 B 档)。
- dev 合理偏离:POCO 去掉 FuncName(func_name group=s 服务器字段,客户端不导出,生成 Num 无此字段)→ FormatWith 标签用 NameTextId。

## Luban 导表环境(关键·已沉淀 dev memory,影响后续所有配置表轮次)
- 字段 ##group 多组写 `c,s` 不写 `cs`(luban.conf 仅 c/s/e 单字符组;写 cs 整列从 client 生成丢失致编译失败)。
- 本机无 .NET 7.0 runtime,Luban.dll 目标 .NET7:用 `DOTNET_ROLL_FORWARD=Major` 前滚到已装高版本运行;.bat 不改。后续 CI/出包重导表须带此环境变量或装 .NET 7.0。
- 新表用 luban_helper.py `table add` 须加 `--no-auto-import` 走传统 __tables__.xlsx 注册,并手动补 read_schema_from_file=True。

## 模型档 / 运行登记
- plan/dev/test=opus。第一段 Run `wf_a0c06eb3-c7a`(Task `w7cmsv77z`,full,BLOCKED@plan/O3);第二段 Run `wf_f7bb129b-9d1`(Task `wn5dj101b`,dev-test)PASS。

## 遗留/观察(转主 boss.md)
- Luban 重导表的 .NET7/DOTNET_ROLL_FORWARD 环境要求 → 出包/CI 注意项(主 boss.md 遗留)。
- 成就点系统(spec 标题提及,本表聚焦货币)未做;灵力登记进货币表、现有货币迁统一钱包、真实图标 Sprite 加载、配置异步加载 — 均列后续可选轮次。
