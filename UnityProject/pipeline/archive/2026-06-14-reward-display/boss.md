# 关单总结:reward-display — 通用奖励展示·归一层(2026-06-14;自治模式·放手默认)

**结论:PASS 交付(仅归一层;表现层 grid+fly-to 转遗留)**。两段:full(plan,在 plan 停一次,boss 复核 4 范围开关续推)→ dev-test。**打回 0**。git 基线 `519aea00`。设计基线 `design-docs/17-reward-display.html`。源 spec `C:\Users\pc\Downloads\1003通用奖励展示.xlsx`。

## 交付范围与 spec 的差(关单要点,非全 spec 完成)
源 spec「通用奖励展示」含两块:设计目的「通用数据方便调用」+ 界面逻辑两种展示(弹框类网格 溢出顶左/不足居中、基础类飞图标到锚点)。plan 据 设计目的 + 无美术 + UI 独立 的现状,定位本轮为**展示归一层**:三种异构奖励产出(道具 GrantPayload / 盲盒 ChestReward / 裸 num_id+数量)归一成统一 RewardView,任何 UI 同一套渲染。**两种可见展示(弹框网格、飞图标)documented-descope 到表现层后续轮**(设计 §一「不做」+ §七 O5)。

boss 复核结论:plan 的归一层先行定位**可接受**——与 numeric/item「数据层先行、UI 独立后续」同节奏;且 grid 布局计算与飞行锚点映射的契约**依赖尚不存在的渲染容器**(真实弹窗 RectTransform、HUD 锚点 Transform)+ spec 确认无美术,此刻盲建为投机性、易返工的工作,且仍交付不出可见展示。故不强行塞入本轮,**归一层单独成单 PASS**,表现层(grid+fly-to)记主 boss 遗留,待有美术 + 具体奖励 UI 流程触达时基于 RewardView 实现并 Play 手验。

过程留痕(供后续复盘,非规则化):本轮 boss pre-decisions 曾显式把 grid 布局计算、奖励类型→锚点映射 列为「验收锚点·须可单测」;plan 据现状把二者并入表现层 descope,未在 decisions/blockers 单独标「与 boss pre-decision 分歧」,boss 在 plan-BLOCKED 复核时聚焦 4 范围开关、漏察该分歧,关单时方发现。单次,不规则化(plan 角色卡已有「不确定先问 boss」)。

## 运行验证(test 经 MCP,UnityProject@02a6dcaa)
- 编译 0 error;EditMode `BlockBlast.Tests` **251/251**(234 基线 + 17 新 RewardDisplayTests),零回归(隔离 job 44422d9d / 全量 aa327058 同数,本工程 EditMode 仅此一程序集)。
- 第 3 类手验 = **N/A**(非 BLOCKED):本轮纯逻辑,C4 widget 不挂 prefab/不投放/不被实例化,无运行期 UI 可手验;运行验证手段本身可达,区别于环境阻塞。
- Code Review 5 红线全过;6 个声明只读的既有文件逐字节未写改,旧 4 档 QualityColor 冻结不被本层调用。

## 实现范围(加法式,既有产出/发奖路径只读不写)
- C1 `RewardView`(readonly struct;IconName/NameTextId/CountText/QualityColor/Badge/RawAmount)+ `RewardBadge` enum,落 `GameLogic/Module/BlockBlast/Reward/`。
- C2/C3 静态 `RewardDisplay`:From(GrantPayload)/From(ChestReward)/FromNumeric/FromItem/FromPattern + GiftView + 6 档权威品质色 QualityColor(q) + CountText(复用 NumericFormat)+ PatternCountText + PatternQuality + ChestRewardKind 私有展示映射。
- C4 `RewardItemWidget : UIWidget` 骨架(SetData(RewardView) 接法示范),落 `UI/BlockBlastUI/`,标 TODO「未挂 prefab/未投放」,编译通过即可、不要求运行。
- T1 `RewardDisplayTests.cs` 17 例(V1-9 转换 / Q1-3 品质色 / N1-2 数量文本 / B1-2 查无降级 / Z1 纯逻辑),落 `Assets/Editor/Tests/BlockBlast/`。
- 文档:新增 17-reward-display.html + 全库 12 篇 sidebar 文档树同步 + index 卡片。

## 关键拍板(boss 复核 plan 4 范围开关,均安全默认、无 spec/GDD 抵触)
- O2 写 C4 widget 骨架(给后续接 UI 留模板)。
- O3 本轮不收编旧 4 档 NumericDisplay.QualityColor(新 6 档为权威源,旧 4 档冻结仍被 NumericDisplay 自用)→ 遗留。
- O6 灵力 Soul 走本层私有展示映射,不补进 num 表(避 numeric-system 范围蔓延)。
- O7 盲盒 Pattern 用代表图案 Diamond + 等级映射 Lv1→精英(4)/Lv2→史诗(5)/Lv3→传说(6),不给盲盒奖池补具体图案(避盲盒系统范围蔓延)。
- 其余 plan 自拍板:6 档品质色对齐 EItemQuality、CountText 复用 NumericFormat、单件不显 x1、B1/B2 用 InitForTest 注入空集合精确命中查无降级分支。

## 模型档 / 运行登记
- plan/dev/test=opus。第一段 Run `wf_8acb7d8b-ceb`(full,BLOCKED@plan 的 4 范围开关);第二段 Run `wf_333d3360-ddd`(dev-test,round 0)PASS。

## 遗留/观察(转主 boss.md)
- **表现层后续轮**(转 #20):弹框类网格布局(溢出顶左/不足居中)+ 基础类飞图标到锚点(奖励类型→锚点映射),基于 RewardView 驱动。阻塞于:无美术(图标 Sprite)+ 需具体奖励 UI 流程(真实弹窗容器 / HUD 锚点 Transform)触达。届时 grid-calc 与 anchor-map 对真实容器实现 + Play 手验。
- **O3 收编旧 4 档品质色**(转 #21):独立正名任务,让 NumericDisplay 也走 RewardDisplay.QualityColor、删旧 4 档,须先核 UI 引用。
- 真实 Sprite 加载(O1)/ 名称多语言文本表(O4)/ 单件恒显 x1 重载(O8):均「接 UI / 文本表建好时做」,归一层不返工,低优,留设计 §七 备查,不单列主 boss。
