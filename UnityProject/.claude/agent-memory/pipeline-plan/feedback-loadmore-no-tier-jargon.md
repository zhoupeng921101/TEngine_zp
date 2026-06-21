---
name: feedback-loadmore-no-tier-jargon
description: 客户端段 UI 文案禁用「Tier N+」「下一刀」「Tier 2+」等内部分期术语,改 plain-language 用户友好版
metadata:
  type: feedback
---

UI 玩家可见文案禁用「Tier 2+」「Tier N+」「下一刀」「待 Tier 2+ 协议扩展」等内部开发分期术语;改写为玩家友好的「已加载本次全部」「更多功能即将推出」「敬请期待」等中性 plain-language。

**Why:** 设计稿写「加载更多按钮置灰提示『翻旧页 Tier 2+』」时,「Tier 2+」是开发期内部分期术语,通过 UI 文案泄漏给玩家会让玩家困惑(玩家不知 Tier 2+ 是什么);沿 conventions §5「语体·简洁说明文」+ 用户偏好 `plain-language-no-pipeline-jargon`(2026-06-13 用户拍板「拟人/口语比喻 + 圈内术语都算黑话」)。该问题在 2026-06-21 设计 46 加载更多按钮自查时识别。

**How to apply:** 写设计稿涉及 UI 投放(用户可见文案 / 按钮提示 / Toast / 错误兜底)时,自查「文案是否含开发期分期术语」:
1. 设计阶段描述用「Tier N+」「下一刀」「O3」等内部术语**仅限**设计稿正文(供 dev / test 读),交付到 UI 的实际文案**必须**改写;
2. 验收点(PV / SV)显式拦截 UI 文案含开发期术语(典型「Code Review 拦字面量『Tier 2+』」);
3. 风险表显式声明此风险 + 改写示范(典型替代词:「已加载本次全部」「敬请期待更多」「暂未开放」);
4. 不止于「加载更多」按钮——任何「占位 / 待建 / 后续开放」类 UI 都套此检查(典型场景:HUD 加号待建 / 设置项暂未开放 / 待建功能 Toast)。

关联:[[feedback-design-doc-code-blind]](plan 不读工程代码 + 设计稿不含代码符号);conventions.md §5(语体·简洁说明文);用户全局 memory `plain-language-no-pipeline-jargon`。
