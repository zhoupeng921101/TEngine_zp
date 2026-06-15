# Boss 关单总结:ui-player-info-window 个人信息窗 · 表现层

- **日期**:2026-06-15
- **模式**:自治(纯 UI 补完范围)
- **参与环节**:full(plan→opus / dev→opus / boss 主会话桥直验);0 打回
- **结论**:PASS(逻辑档)。兑现遗留 #22(player-info 表现层)。塔罗 UI 自治线第 2 屏。

## 任务定义

新建 PlayerInfoWindow 弹窗(头像+改名/玩家名+改名/生日占位/确定/点任意处关),复用设置窗范式(GameContext 持有数据服务 + FindChildComponent + [Window Top 非全屏] + SetSubSprite 寻址)。数据层 player-info(#22,`GameLogic.BlockBlast.Player`)已就绪,本轮只做表现层 + 接线。设计稿 `design-docs/25-player-info-window-art.html`。

## 关键决策(plan + dev,均安全默认按自治推进)

- D1:玩家信息纳入 `GameContext.Player`(延续统一上下文,设计 23 §五预告)。
- **D2 生日 = UI 占位、不入存档**(数据层无生日字段、spec 未要求;真做须数据层加字段,属本轮范围外增量)。**提请 boss/产品复核**——效果图有、数据层无。
- D3 头像三态网格 / D4 等级槽·id 复制:本屏不做(设计 18 完整界面元素,效果图本屏未画,归后续屏)。D5 改名=就地输入框切换。
- B1 取档时机 = 同步加载(dev 选路 B 变体):`GameContext.OnInit` 同步 `MergeMetaPersistence.Load`(本就同步内存级读,不触阻塞 IO 红线),落盘对称 `SaveAsync().Forget()`。avatarValid 校验回调 try/catch 兜底(配置启动时序未就绪不卡启动)。

## 验收(boss 主会话桥 `UnityProject@02a6dcaa` 直跑)

- **EditMode 390/390 PASS**(366 既有 BlockBlast 零回归 + 12 player-info 新增 + 12 UIAtlasPacker),0 failed。
- player-info 新增 12 例:H2×3(GameContext 持有 Player 往返:注入 DTO 字段相等 / 无档 CreateDefault 缺省 / 同实例保真)、W3×5(改名贯通 4 拒因 + 合法名,确委托 PlayerRenameService 不自写)、W5×3(占位点击不抛)、StableColorFor×1。
- Code Review(plan C2 + 红线):异步优先(SaveAsync 异步、Load 同步内存级)、GameModule 访问、SetSubSprite 自管释放、热更边界(全 HotFix)、绑定路径 get_hierarchy 核对 30 节点一致。
- V 组(Play 视觉对位 个人信息.png / 指针改名闭环 / 三种关闭 / 重开保留落盘)= 人工 Play 手验遗留(MCP 不能模拟指针/输入法;占位图[头像色块/铅笔字符/下拉箭头]外观待美术补切图)。

## 交付物

- 新增:`GameScripts/HotFix/GameLogic/UI/PlayerInfoWindow.cs`;改 `GameContext.cs`(增 Player + LoadPlayer/SavePlayer/InitPlayerFromMeta)、`UI/BlockBlastUI/MainMenuWindow.cs`(个人信息入口,兑现 TODO 钩子)
- 资源:`AssetRaw/UI/Prefabs/PlayerInfoWindow.prefab`(30 节点;复用 `Sheet_settings` 精灵表,无新切图)
- 测试:`Editor/Tests/BlockBlast/PlayerInfoWindowTests.cs`(12 例)
- 设计稿:`design-docs/25-player-info-window-art.html`(+ nav.js)

## 遗留转出

- **[产品复核·D2]** 生日真做须数据层加字段(PlayerInfo + MergeMetaSave + 保底 + 单测)。
- **[后续屏·设计 18]** 头像三态选择网格、等级+经验槽、id 复制(数据层有 Level/PlayerLevelConfig/ClipboardUtil 可显)。
- **[美术]** 圆头像框/编辑铅笔/下拉箭头/头像 Sprite 切图缺失,当前占位;补图后替子图即生效、节点与脚本不返工。
- **[人工 Play 手验]** V 组(对位/指针改名/关闭/落盘)。
