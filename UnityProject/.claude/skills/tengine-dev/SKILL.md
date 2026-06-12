---
name: tengine-dev
description: TEngine Unity 游戏框架开发指导。触发词：TEngine, UIWindow, UIWidget, GameEvent, AddUIEvent, LoadAssetAsync, SetSprite, HybridCLR, YooAsset, Luban, GameModule, 热更, 资源加载, UI开发, 事件系统, 配置表
---

# TEngine 开发指导

TEngine 是基于 HybridCLR + YooAsset + UniTask + Luban 的 Unity 游戏框架。
本 skill 提供 AI 专用的精炼参考文档，确保生成的代码与框架 API 完全一致。

## 核心红线

编码红线以项目根 `CLAUDE.md`「核心原则（编码红线）」为唯一信息源，本文件不复制条文。各红线的展开说明见下方路由表对应文档（异步/资源 → resource-api.md，模块 → modules.md，热更边界 → hotfix-workflow.md，事件 → event-system.md）。

## 文档路由

根据任务类型，读取对应的 reference 文档：

| 任务类型 | 必读文档 | 进阶文档 | 优先级 |
|---------|---------|---------|--------|
| UI 开发 | [ui-lifecycle.md](references/ui-lifecycle.md) | [ui-patterns.md](references/ui-patterns.md) | P0 |
| 事件系统 | [event-system.md](references/event-system.md) | [event-antipatterns.md](references/event-antipatterns.md) | P0 |
| 资源加载 | [resource-api.md](references/resource-api.md) | [resource-patterns.md](references/resource-patterns.md) | P0 |
| 模块使用 | [modules.md](references/modules.md) | — | P0 |
| 热更代码 | [hotfix-workflow.md](references/hotfix-workflow.md) | — | P1 |
| 代码规范 | [naming-rules.md](references/naming-rules.md) | — | P1 |
| Luban 配置 | [luban-config.md](references/luban-config.md) | — | P1 |
| 项目结构 | [architecture.md](references/architecture.md) | — | P2 |
| 问题排查 | [troubleshooting.md](references/troubleshooting.md) | — | P2 |
| MCP 场景/GO/UI/脚本/Editor | [mcp-tools.md](references/mcp-tools.md) | — | P1 |
| MCP 材质/Shader/动画/VFX | [mcp-visual.md](references/mcp-visual.md) | — | P2 |
