# 角色记忆:开发(跨任务经验)

> 开工随角色卡一起读;收尾把新的可复用经验沉淀进来(一条一行,过时即删)。
> 准入:只记跨任务可复用、且角色卡/设计文档/CLAUDE.md/references 未覆盖的经验。

- 图标/元素类表现的零美术方案:`Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` 渲染 ◆★ 等 glyph;内置资源无需 YooAsset 释放(2026-06,collect)
- 给既有玩法加模式开关的安全做法:加法式扩展 + 单开关门控 + 所有离开路径调 Exit 清态;off 路径逐字节不变,靠原回归单测兜底(2026-06,CollectMode)
- 镜像既有窗口(如 GameWindow)做新切片窗口:逻辑层单测可全覆盖,风险集中在拖拽手势交互层,交接时显式标注(2026-06)
- 新增 UIWindow 必须配一份同名 prefab 到 `Assets/AssetRaw/UI/Prefabs/<location>.prefab`(`AssetRaw/UI` 按文件名寻址);即便 UI 全代码构建,prefab 也只是 Canvas+GraphicRaycaster 根,可照抄既有窗口 prefab 仅改 m_Name + .meta GUID。漏建则 LoadGameObjectAsync(location) 找不到资源(2026-06,merge-order)
- 2048 式自动合成(满 2 即升级)与"订单要求某等级 ×N":非封顶等级库存恒 ≤1,故 N≥2 的订单只在封顶等级(可堆积)能满足;低级订单数量只能为 1。设计若写「Lv1 ×3」类订单需先合规则(订单吃多个低级 / 允许低级堆积),否则永不可达(2026-06,merge-order)
