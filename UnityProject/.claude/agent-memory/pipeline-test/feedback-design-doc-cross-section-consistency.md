---
name: feedback-design-doc-cross-section-consistency
description: 设计文档局部改写时,§一交付清单/效果图表/Mermaid stub 节点可能保留过时措辞,test 须逐节查内部一致性
metadata:
  type: feedback
---

conventions §6 交叉检:设计文档局部改写时,同文档内其他段(§一交付清单、效果图拆解表、Mermaid 图 stub 节点)可能保留过时措辞而未被 dev 同步改写;test 须逐节检查文档内部一致性,不只核「目标节是否改写」。

**Why:** 2026-06 tarot-hud-attr-bind SV6 实测,dev 改了目标节,但 §一清单的对应项、效果图拆解表的对应行、Mermaid 图的 stub 节点都没更新,造成稿内自相矛盾。

**How to apply:** 交叉检流程加全文一致性扫描:①§一交付清单 ②效果图拆解表 ③ Mermaid 图节点/边 ④顶部 WARNING/NOTE 块 → 都要与目标节改写后的现状对齐。
