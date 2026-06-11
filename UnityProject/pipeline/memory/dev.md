# 角色记忆:开发(跨任务经验)

> 开工随角色卡一起读;收尾把新的可复用经验沉淀进来(一条一行,过时即删)。
> 准入:只记跨任务可复用、且角色卡/设计文档/CLAUDE.md/references 未覆盖的经验。

- 图标/元素类表现的零美术方案:`Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` 渲染 ◆★ 等 glyph;内置资源无需 YooAsset 释放(2026-06,collect)
- 给既有玩法加模式开关的安全做法:加法式扩展 + 单开关门控 + 所有离开路径调 Exit 清态;off 路径逐字节不变,靠原回归单测兜底(2026-06,CollectMode)
- 镜像既有窗口(如 GameWindow)做新切片窗口:逻辑层单测可全覆盖,风险集中在拖拽手势交互层,交接时显式标注(2026-06)
