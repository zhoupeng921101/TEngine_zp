# Boss 关单总结:gameplay-fusion 玩法融合统一设计文档(2026-06-16)

## 任务定义
把「经典无尽」与「合成订单」两套玩法融合成一套,产出统一策划设计文档。源起:用户「两套玩法要融合到一起」,@ design-docs/01 + 11。主会话 plan 模式先与用户对齐 3 个难回退决策,经 ExitPlanMode 批准计划后走 `/pipeline plan`。

## 参与环节
**plan-only**(用户 `/pipeline plan` 显式指定)。无 test → 仅 boss 验收(产出完整 + conventions 交叉检),无代码正确性验证(产出为设计文档,本阶段不改码)。

## 用户锁定决策(plan 模式 AskUserQuestion 拍板,2026-06-16)
1. 融合产物 = 一份统一策划设计文档(非直接改码、非仅调和两文档)
2. 经典**完全吸收**进合成订单,移除「纯无尽」独立入口
3. 单局**保留体力预算**(合成订单现状)作约束

## 设计基线
已批准计划 `~/.claude/plans/design-docs-01-gameplay-overview-html-d-serialized-rabbit.md`;参考 design-docs/01、02、09、10、11 + 代码 Module/BlockBlast/。

## 验收结论:PASS(plan-only,boss 验收)
- 磁盘交付物三件全部核实存在:`design-docs/29-gameplay-fusion.html`(367 行,新建)、`01-gameplay-overview.html`(顶部状态说明 callout)、`assets/nav.js`(注册 29 + 修订 01/11 主题,`node --check` 通过)。git status 非空。
- 交叉检(conventions「收尾必做」):8 冲突点逐条三栏齐全无悬而未决;「现状/本篇目标」逐处标注、未把目标写成现状;3 决策落 §八拍板记录带日期、脱离对话成立;无 diff 叙事/指代词;术语(休眠/橡皮筋/死钩子)属工程公共词,非黑话。
- 0 打回。

## plan 关键增量(超出 boss 原计划的勘察)
plan 做了真实代码勘察,把隐患核到比原计划更准:① `HandGenerationArbiter`(R1-R3 仲裁)已写成纯逻辑类+单测**但未接入任一窗口**,现状发牌只走 R4;② `dynamicWeight` 持久化到盘 + 两窗只 `BeginGame` 不 `Reset` = 跨局/跨模式/跨重启累积(真实残留);③ 合成订单局内 `Score` 恒 0 → DDA 8 算法权重段从不触发(DDA 做局休眠);④ Combo/Score 退出不清零是被「进入即重置」掩盖,非当前活 bug。设计稿据此标注,未照旧设计稿快照设计。

## 待用户/boss 知悉:4 项范围开关(§七,均有安全默认、可逆,本篇按默认推进、不入 blockers)
1. 智能生成 R1-R3 是否接入融合窗口 —— 默认不接(维持 R4,零回归)
2. DDA 强度信号源 —— 默认维持 Score(融合里继续休眠);备选改接经营进度量启用做局
3. dynamicWeight 跨局归一 —— 默认每局重置(消除残留);备选刻意累积
4. 融合后玩家可见主分数口径 —— 默认沿用 TotalScore;备选恢复经典即时大分数
四项默认组合自洽(最小变更/零回归/行为可预测)。任一改备选 = 局部增量,不返工已落地系统。这些是**代码落地阶段**的取舍,届时 `/pipeline dev` 开工前由用户/boss 拍。

## 遗留(转 boss 主文件遗留事项)
- gameplay-fusion **代码落地**(主菜单单入口 / 两窗合一 / 三隐患归一 / 存档合并 / 4 范围开关)= 后续独立 `/pipeline dev` 任务,基线 = 29 设计稿 §五 + §七。两窗合一含美术换皮归属理清(GameWindow 已贴塔罗木质皮 #27)。
