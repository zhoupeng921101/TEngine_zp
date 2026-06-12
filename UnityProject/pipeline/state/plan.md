# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:核心玩法补全(仅策划阶段,不进开发)

把四份原始策划稿(方块玩法/订单/宝箱/女神)的漏洞补成完整自洽可验收设计,术语对齐 merge-order 工程现状。

### 产出

- 设计稿:`design-docs/11-core-loop-completion.html`(已加入 index 侧栏 + 卡片,内部锚点全通,2 张内联 SVG 经 preview 核验无溢出)
- 八个挂账问题逐条有确定答案(见文档 §一 处置一览表)

### 交接区(给 test 逐条核对 → 文档 §十一 验收标准)

验收分两类,test 按系统是否已落地选用:
- **(A) 与现状对齐项** — 可立即对照代码核验(体力档=MergeOrderConfig、封顶 Lv3=MaxLevel、订单自动配对约束已在 OrderPool 注释钉死)
- **(B) 新增系统设计项** — dev 落地后按文档 §十一 各小节核验(并发特殊轨/连消倍率/智能生成优先级链/宝箱/女神)

涉及模块/符号(给 dev 定位,均为现状真实符号):
- `Module/BlockBlast/MergeOrderConfig.cs`(体力/合成/订单/得分映射常量 + OrderPool)
- `Module/BlockBlast/MergeOrderState.cs`(收集区库存/订单/体力/悔棋状态机)
- `Module/BlockBlast/MergeElementVisual.cs`(9 种 MergeElement 枚举 + Glyph/ColorOf)
- `Module/BlockBlast/BlockScoring.cs`(ClearScore 计分,连消倍率须只乘显示分)
- 智能生成 R4 = 现状 `dynamicWeight`(见 02 文档),R1–R3 + 优先级链为本篇新增上层仲裁

### 拍板状态

文档 §十二 全部 8 条决策已于 2026-06-12 经用户确认,按默认选择执行(明细见文档 §十二,此处不复制)。设计稿即定稿,可作为 dev 简报的设计基线。

> 本任务为手动寻址 @plan,仅策划阶段,未派 dev。任务关闭时本节由 boss 关单事务清空/归档。
