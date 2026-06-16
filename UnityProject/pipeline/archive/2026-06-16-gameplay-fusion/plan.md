# 状态:策划(plan)— 归档(2026-06-16 gameplay-fusion 关单)

## 任务:玩法融合统一设计稿(plan-only)

### 交接区
产出(三件,一次改动内一起做,新旧无矛盾):
1. **新建** `design-docs/29-gameplay-fusion.html` —— 统一裁决设计稿。结构:立项信息(#intro)→ §一改什么与为什么 → §二融合总览(含 SVG)→ §三 8 冲突点逐条裁决(3.1~3.8,每条经典现状×合成订单现状×融合后取定+落点)→ §四经典去向(保留6项/被覆盖移除3项)→ §五代码层融合落点(5.1 主菜单单入口/5.2 两窗合一/5.3 三隐患/5.4 存档合并/5.5 R1-R3 接线)→ §六验收(A 文档自检+B 落地核验)→ §七待拍板4开关(默认+备选)→ §八拍板记录(2026-06-16)→ §九风险。
2. **改** `design-docs/01-gameplay-overview.html` 顶部加状态说明 callout(经典已被融合吸收为底层引擎,指向 29),正文未重写。
3. **改** `design-docs/assets/nav.js`:GROUPS「切片设计」组注册 29;修订 01/11 一句话主题反映融合关系(侧边栏+index 卡片自动同步)。

### 取件路径
- 设计稿:`design-docs/29-gameplay-fusion.html`
- 连带改:`design-docs/01-gameplay-overview.html`、`design-docs/assets/nav.js`

### 关键勘察结论(供后续 dev,代码现状已远超旧设计稿快照)
- 经典/合成订单共用 `BlockGameState`/`BlockScoring`/`RefillPieces`,靠 `MergeOrderMode` bool 门控分流(融合=去分流)。
- `ClearSettlement`(连消/多消/全清结算)、`HandGenerationArbiter`(R1-R3 仲裁)+`HandGenContext` 均已写成纯逻辑类+单测,**但 HandGenerationArbiter 未接入任一窗口**(发牌只走 DynamicWeightDiff R4)。
- 三隐患核实:① Combo/Score 退出不清零但进入即清零(非活 bug,单窗口须保进入重置不变量);② `dynamicWeight` 持久化到盘+两窗只 `BeginGame` 不 `Reset` → 跨局/跨模式/跨重启累积(真实残留);③ 合成订单局内 `Score` 恒 0 → DDA 8 算法权重段从不触发、全程清屏窗口(DDA 做局休眠)。

### 自检结论
已过 conventions「收尾必做」:正文脱离对话成立(无指代词/无 diff 叙事,grep 核实)、无可推导副本、无拟人比喻(grep 核 钉死/焊死/打死/醒过来 等,1 处「醒过来」已改「激活做局」)、「现状/本篇目标」逐处标注未把目标写成现状、决策落 §八拍板记录脱离对话成立。链路检查全过:nav.js `node --check` OK、29 无 BOM、29 内部锚点全解析、跨篇 href+#anchor 全落实际 id、GROUPS 各 href 落实际文件、SVG 标签平衡、h2/h3 除 .related 外全带 id(auto-TOC 用)。
