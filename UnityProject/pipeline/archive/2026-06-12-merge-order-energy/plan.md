# 状态:策划(plan)

> 开工先读本文件 + roles/plan.md。每完成一步就更新这里。

## 当前任务
设计「元素合成 + 订单 + 体力 Demo 切片（merge-order-energy）」——在 08 收集切片（候选块携带元素→消除掉落）之上,叠加三套系统组成核心循环:体力→落子→消除→元素→合成→订单交付→奖励。设计文档 `design-docs/09-merge-order-energy.html`(已入 index 导航)。

## 交接区(给开发)——验收标准
> 开发从这里逐条读。每条「完成定义」可被测试核对。设计细节见 `design-docs/09-merge-order-energy.html`(章节锚点在「完成定义」中标注)。
> 总原则:全部新逻辑由 `MergeOrderMode` 门控;off 时 Classic + 08 收集模式行为逐字节一致(回归硬验收)。表现 glyph/纯色零美术。无任何变现/广告入口(项目红线)。
> 数值常量集中在新增 `MergeOrderConfig` 静态类(仿 08 `CollectDemo`),不接 Luban,改数即调难度。默认值见文档 §五配置表。

| # | 功能点 | 完成定义(测试可核对) | 涉及模块/UI/事件/配置 |
|---|--------|----------------------|----------------------|
| 1 | 模式门控 & 入口 | 新增 `MergeOrderMode` 门控开关。off 时 `BlockGameState` 的 Classic 落子/消除/补块/存档 + 08 收集模式行为与现状逐字节一致(Classic + 08 回归测试通过)。主菜单新增「合成订单 Demo」入口按钮 → 打开 `MergeOrderWindow`。反复进入每次从初始态(空棋盘+起始体力+初始订单+空合成区)开始,无上一局残留。 | `MainMenuWindow`(加按钮);新增 `MergeOrderWindow`;`MergeOrderState` 重置;Classic/08 回归 |
| 2 | 体力数据模型 & 落子扣体力 | 体力初值 `EnergyStart=20`、软上限 `EnergyCap=30`。每次从待选区落一块扣 `PlaceCost=1`。体力条实时显示 `当前/上限`。体力为 0 时不可再落子(见 #5)。 | `MergeOrderState.Energy`/`PlaceCost`;`MergeOrderWindow` 落子流程 + 体力条 UI;`MergeOrderConfig` |
| 3 | 消除返还体力 | 一次消除返还体力 = 本次被清行列数(消 2 行列 → +2)。返还后体力条立即更新。返还可叠加但受软上限约束(订单奖励可溢出,落子/消除返还不溢出)。 | `MergeOrderState.RefundEnergy(lines)`;`MergeOrderWindow.PlaceAndResolve` 消除分支 |
| 4 | 自然恢复(可简化) | 设计为 +1/120s 回到软上限。**demo 允许简化实现**(加速常量或暂不实装,在交接说明所选档);若实装,挂窗口 Update/计时,仅回到 `EnergyCap`,不溢出。 | `MergeOrderState.Regen`;`MergeOrderConfig.RegenPerTick/IntervalSec` |
| 5 | 体力耗尽兜底 | 体力 < `PlaceCost` 且场上仍有手持块(无法支付任何落子)→ 触发软 GameOver,复用 `GameOverWindow`(可传变体标题「精力耗尽」)。触发后锁输入。 | 复用 `GameOverWindow`;`MergeOrderWindow` 落子前/补块后判定 |
| 6 | 消除元素入合成区 | 复用 08 元素层(`ElementArr`/`PlacePiece` 转移)。消行/列时,被清格的元素**输出为本次被清元素列表**并逐个以 Lv1 摄入合成区(替换 08 的静态 `Collected` 累加,新模式下不再累加静态目标)。被清格 overlay 同时清除(沿用 08)。 | `BlockGameState.CollectClearedElements` 扩展(额外输出被清元素列表);`MergeOrderState.IngestElement(type)` |
| 7 | 合成区自动两两合并升级 | 合成区按 `(类型,等级)` 记数。摄入 Lv1 后,任一 `(类型,等级)` 数量≥2 即合并:数量-2、上一级+1,级联到无法再合并;封顶 `MaxLevel=3` 不再合并。测试:连续摄入同类型 2 个 → 出现 1 个 Lv2;摄入 4 个 → 1 个 Lv3;摄入 8 个 → 2 个 Lv3(封顶堆积)。 | `MergeOrderState.IngestElement` 自动配对逻辑;`MergeOrderConfig.MaxLevel` |
| 8 | 合成区 UI | 合成区面板显示各类型当前持有的 token(glyph + 等级标记 + 数量),实时随摄入/合并/交付刷新。glyph/纯色复用 `CollectDemo.Glyph/ColorOf`。 | 新增合成区面板 UI;`MergeOrderWindow` 刷新 |
| 9 | 订单数据模型 & 双订单显示 | 同时显示 `ActiveOrders=2` 张订单卡,每卡 = (元素类型, 要求等级, 数量),显示 glyph+Lv+数量。初始两单从订单池取前两项。 | `MergeOrderState.ActiveOrders[]`;订单卡 UI;`MergeOrderConfig` 订单池 |
| 10 | 订单交付(消耗+奖励) | 当合成区库存满足某单 (类型,等级)≥数量,该单「交付」按钮点亮;点击 → 扣除对应合成物 → 发奖(体力 `+8` 可溢出上限 + 分数 `等级×数量×50`)→ 完成单数 +1。库存不足时按钮置灰不可点。 | `MergeOrderState.CanDeliver/Deliver(slot)`;交付按钮 UI;`MergeOrderConfig.OrderReward` |
| 11 | 订单刷新 & 锯齿波 | 交付后该订单槽刷新为下一单。**demo 实现**:手编循环订单池(已编排锯齿波节奏:难单后必出简单单),`NextOrder()` 顺序取下一项,循环。完整程序化锯齿波规则见文档 §3.2(可不实装)。 | `MergeOrderState.NextOrder()`;`MergeOrderConfig` 循环订单池 |
| 12 | 需求拉动注入 + 保底 | 候选块注入的类型池 = 当前激活订单所需类型并集(非 08 的「所有仍需类型」)。注入概率 `InjectChance=0.30`。保底:记 `pitySinceNeeded`(距上次注入订单所需类型的落子数),超 `PityThreshold=8` → 下一候选块强制注入一个订单所需类型。测试:连续 8+ 次落子未出所需类型后,下一块必带所需类型。 | `BlockGameState.InjectElements` 扩展(类型池来源 + 保底分支,门控);`MergeOrderState` 保底计数器;`MergeOrderConfig.InjectChance/PityThreshold` |
| 13 | 落错子兜底(限次免费悔棋) | 每局 `UndoCharges=3` 次免费悔棋(无广告/无内购)。悔棋撤销**最后一次落子**:回滚棋盘/`ElementArr`/体力/合成区库存/订单进度/保底计数器到落子前快照,方块退回待选槽,退回该次扣的体力。次数耗尽后悔棋按钮置灰。**备选**:若全量快照成本高,可先实现「仅该次落子未触发消除时允许单步撤销」(交接说明所选版本)。 | `MergeOrderState` 悔棋快照栈 + `Undo()`;悔棋按钮 UI;`MergeOrderConfig.UndoCharges` |
| 14 | demo 通关 | 完成单数达 `DemoGoalOrders=5` → 弹通关面板(复用 `CollectWinWindow`,传完成单数/累计奖励摘要)。弹出后锁输入。 | `MergeOrderState.IsDemoComplete()`;复用 `CollectWinWindow`;`MergeOrderConfig.DemoGoalOrders` |
| 15 | 棋盘塞满兜底 | 手持块无处可放且未通关 → 触发 GameOver(沿用 08:`CanPutAnyOf` + `GameOverWindow`)。与体力耗尽(#5)为两个独立失败条件。 | 复用 `BinaryBoard.CanPutAnyOf` + `GameOverWindow` |

## 已知问题 / 待澄清(待 boss 收产出时拍板)
> 选项 + 策划建议。boss 拍板后再派开发;不替用户扩需求。
1. **自然恢复实装档(#4)**:A 暂不实装(单局测试意义小,主补给走订单奖励)/ B 加速版(如 +1/10s 便于演示)/ C 完整 2 分钟 1 点。**建议 A**(demo 范围克制,跨会话计时不在切片重点),B 仅在需要演示「恢复」机制时用。
2. **悔棋实现档(#13)**:A 全量单步快照回滚(完整,6 项状态)/ B 轻量版(仅未触发消除时允许撤销)。**建议 A**(体验完整、失误代价可控),若 dev 评估快照成本高再退 B。
3. **合成交互形态**:A demo 用自动配对库存(本设计)/ B 浪漫餐厅式空间拖拽合成板。**建议 A**(订单闭环优先,拖拽合成是另一套完整交互),空间拖拽列为后续增强。
4. **demo 终点(#14)**:A 完成 5 单即通关(有限终点便于测试)/ B 无限订单流仅靠失败条件结束(贴近生产)。**建议 A**(demo 需可核对终点),生产形态文档已注明为无限流。
5. **体力瓶**:demo 订单奖励直接给体力(本设计)/ 实装可合成体力瓶道具。**建议直接给体力**,体力瓶作为合成链产物列为文档化后续增强。

## 遗留(收尾自检已过)
- 改动文件:`design-docs/09-merge-order-energy.html`(新增)、`design-docs/index.html`(加导航卡)、本文件(交接区)。
- 已按 CONVENTIONS「收尾必做」自检:正文无指代词/无 diff 叙事;验收表「完成定义」可独立核对;待澄清项列选项+建议交 boss。
