# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:整理旧设计文档,与融合后现实对齐(plan-only,加注为主)

### 交接区(交 boss 验收)
plan-only,无代码改动。对 01/02/09/10/11/12/13 七篇加状态/勘误注,正文留存。逐篇:

| 篇 | 加了什么 | 指向 |
|---|---|---|
| **01** | ① §二 Combo 死钩子 callout 内追加勘误句(已点亮为连消倍率链、Combo 降视觉镜像)② 顶部状态 callout 补一行「保留六项机制去向」 | 11·§5.3(`#combo-table`)/ 29·§3.5(`#v5`)/ 29·§4.1(`#keep`) |
| **02** | 篇首加状态 callout:DDA 现为融合底层引擎 R4,强度信号源待定(有安全默认) | 29·§3.8(`#v8`)/ 29·§七#2(`#decisions`) |
| **09** | ① §五配置表前加勘误 callout:掉率/保底三行(InjectChance/类型池/PityThreshold)已退役,替代旋钮 ScorePerElement 等 ② §七挂接点前加符号映射 callout(见下「就地改名」) | 10·§2.3(`#map`)/ 10·§四(`#hook`) |
| **10** | §3.3 misnomer 节顶加状态 callout:当时取「保留原名」,其后已正名,给现行符号映射;正文(决策史)冻结 | 自篇 `#keep`/`#decide`/`#removed` |
| **11** | ① 篇首加状态 callout:写于融合前,补全系统即融合玩法完整设计 ② §六经济加旁注:融合后是唯一经济(单栏页,不引 nav.js) | 29·§三(`#verdict`)/ 29·§五(`#dev`)/ 29·§4.1(`#keep`) |
| **12** | §3.5「现状边界」callout 内,把裸文「设计 11 §三」改为真链 | 11·§三(`#concurrency`) |
| **13** | §二.1 三层主线图后加脚注:进度已由 14 升为跨会话尺度 | 14·§一(`#what`)/ 自篇 §七O3(`#open`) |

### 三处「硬过时点」处置实况(与简报预期的偏差,需 boss 知会)
- **09 §四风险1 勘误**:简报列为待加,实查**已存在**(callout「本节注入机制已更新」指向 10),未重复加。
- **13 §七 O3 勘误**:简报列为待加,实查**已存在**(callout「勘误(2026-06-14)」指向 14·§一,体例同 14 §一),未重复加。本任务只补了 §二.1 三层图脚注。
- **01 Combo / 09 InjectChance** 两处硬不符:按简报就地加勘误句完成。

### Collect* 改名:文档对齐「代码已落地的正名」(关键事实更正)
简报要求「先核引用后就地改新名」。grep 工程现状(`Assets/GameScripts/HotFix/GameLogic`)结论:**正名其后已在代码落地**——`CollectElement`→`MergeElement`、`CollectDemo`→`MergeElementVisual`、`CollectWinWindow`→`MergeOrderWinWindow`(含同名预制 location)、`CollectClearedElements`→`HarvestClearedElements`;旧名在 GameLogic **0 命中**(已死)。
- 据此,09/10 不逐处改写历史正文(违「不改写正文」红线、且会把决策史改成 Frankenstein),改为**在 09 §七 / 10 §3.3 各加一条「旧名→现行符号」映射 callout**:既让读者不会去代码找已删符号(满足就地修正意图),又冻结正文。
- 简报遗留 #10(09/10 Collect* 旧名)据此闭合。注:10 §3.3 当时还列过一组**未采用**的假想新名(`CollectDemo→BlockElementVisual` 等),映射 callout 已明确给「实际落地」的名,防误导。

### 自检与死链核验
- conventions「收尾必做」过:所加注均为状态/理由层、脱离对话成立(无「刚才/现在改成」类 diff 叙事;陈述现行事实+指针);语体简洁说明文;`grep 钉死|绑死|死在|收口` 命中全部为**既有正文**,我的新增 0 命中(既有比喻属正文,不在本轮改写范围)。
- 死链:新增跨篇指针 11 个锚点(29 的 verdict/v5/v8/keep/dev/decisions、11 的 concurrency/combo-table/economy、10 的 map/hook/removed/keep/decide、14 的 what、13 的 open)逐一 grep 确认对应 `id=` 存在,**零死链**;9 个被引文件均存在。
- nav.js 未动;新增全部是 callout/`<p>`,无新 `h2/h3` id,各页 auto-TOC 不受影响(11/archive 本就单栏不引 nav.js)。
- 正文段落未删改(仅插入注 + 12 一处裸文转真链,语义不变);11/02 历史设计正文留存。

(上一任务:gameplay-fusion 玩法融合统一设计稿,plan-only PASS 关单,归档 `pipeline/archive/2026-06-16-gameplay-fusion/`)
