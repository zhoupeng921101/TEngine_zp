# 角色记忆:测试(跨任务经验)

> 开工随角色卡一起读;收尾把新的可复用经验沉淀进来(一条一行,过时即删)。
> 准入:只记跨任务可复用、且角色卡/设计文档/CLAUDE.md/references 未覆盖的经验。

- UI 经 UICamera(ScreenSpaceCamera)渲染,Play 手验/截图须经它取景(2026-06,collect 实测)
- MCP 无法可靠模拟指针拖拽(BlockPieceDragger),拖拽/手势类验证点一律标「需人工 Play 手验」;其逻辑层改用单测 + state 注入覆盖(2026-06)
- 单测先隔离跑 `run_tests(EditMode, assembly=BlockBlast.Tests)`;全量 EditMode 可能混入其他程序集的无关失败,先隔离再下判定(2026-06)
- 异步连开/连关窗口得到的瞬态读数不可直接判缺陷:改用正常 UI 路径复现,复现不出按测试驱动假象处理(2026-06,Diamond 9/6 案例)
