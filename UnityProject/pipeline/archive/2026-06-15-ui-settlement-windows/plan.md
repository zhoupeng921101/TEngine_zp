# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:结算窗 reskin（游戏结束 GameOverWindow + 恭喜通关 MergeOrderWinWindow）· 塔罗 UI 自治线第 3 屏 · 纯 UI 补完

设计稿:`design-docs/26-settlement-window-art.html`(已写、已注册 nav.js「表现层 / 换皮」组、断链 / BOM / lint 通过)。
自治模式 · 放手默认:有安全默认按默认推进、记 decisions;无安全默认 / 抵触 GDD / 不可逆才入 blockers。本轮 blockers 为空。

## 交接区（交开发）

### 范围一句话
把两个**已在运行**的 code-built 结算窗换皮成塔罗木质风格。**纯视觉换皮**:结算逻辑 / UserData 取参 / 重开·返回回调**全部保留不动**,只把 `UGuiFactory` 纯色 Image / 按钮底换成贴 `Sheet_settings` 木板 / button 子图。复用设计 23 的 `Image.SetSubSprite` 取图链路,无新资源、无新文件。**零回归是硬验收**(两窗在跑)。

### reskin 方式:路 A（轻量换皮）选定
- 路 A:保留窗口 code-built 结构 + 结算逻辑,对 `UGuiFactory.CreateImage` 返回的 `Image`、`CreateButton` 的 `out Image bgImage` 直接链 `.SetSubSprite("Sheet_settings", 子图名)` 换皮。改动最小、回归面最小。
- **不取路 B**(prefab + FindChildComponent 重构):会重写两个在跑窗口、回归面大,与「纯 UI 补完」相悖。
- 论证见设计稿 §三。

### 涉及文件（dev 定位）
- 改:`Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/GameOverWindow.cs`(72 行,改 OnCreate 视觉)
- 改:`Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWinWindow.cs`(61 行,改 OnCreate 视觉)
- 新建测试:`Assets/Editor/Tests/...` 结算窗回归(静态核对 + 编译)
- **不改**:`GameWindow.cs:336` / `MergeOrderWindow.cs:731,718`(三处调用点);`UGuiFactory` / `BlockLayout` / `BlockGameState` / `MergeOrderState` / `GameContext`;`Sheet_settings.png` + 收集器(复用)
- 取图 API:`Image.SetSubSprite("Sheet_settings", 子图名)`(`SetSpriteExtensions.cs:43`);子图名 = setting/ 下 22 张切图文件名(木板 box1/box2/base_plate*,按钮 button,关闭 icon_x/x,图标若干)

### 功能点逐条 + 完成定义（test 可核）

**逻辑回归 / 编译（EditMode，硬验收，设计稿 §8.1 R 组）**
- C1 两窗 + 现有 EditMode 编译 0 error、全绿(零回归)
- C2 Code Review 5 红线(SetSubSprite 自管引用计数无裸 LoadAssetAsync / 热更区 / onClick / GameModule.UI / 异步取图)
- **R1**(硬)GameOverWindow · Classic 路径:`UserData=previousHigh` 解析不变;`finalScore`/`high`/`isNewBest` 计算与换皮前一致;调用点 `GameWindow.cs:336` 传参未改
- **R2**(硬)GameOverWindow · 订单结束路径:`MergeOrderWindow.cs:731` 传 `0` 不变;`previousHigh=0` 时 `isNewBest=finalScore>0` 分支保留
- **R3**(硬)MergeOrderWinWindow · 通关路径:`UserData as List<string>` 解析 + 逐行渲染循环保留;调用点 `:718` 传 `lines` 未改
- **R4**(硬)回调目标零回归:重试→GameWindow;GameOver 返回→MainMenuWindow;再来一局→MergeOrderWindow;通关返回→MainMenuWindow
- R 组怎么测:**静态核对 + 编译为主**(运行期单例 + UIWindow 生命周期反射成本高)——逐条核对 UserData 解析行 / 结算计算行 / 三处调用传参 / 四个回调目标均未改。任一逻辑被改即不通过(换皮越界)。

**视觉对位 / 真机（需 Play / 人眼，手验遗留，设计稿 §8.2）**
- V1 切图经 SetSubSprite 在结算窗正常显示(木质风格,证明链路通)
- V2 GameOverWindow 对位 游戏结束.png(木牌「游戏结束」+ 木板 + 分数 + 「重试」黄条 + 「返回」;无 X、无遮罩关窗)
- V3 MergeOrderWinWindow 对位 恭喜通关.png(木牌「恭喜通关」+ 木板 + 结算行 + 按钮)
- V4 三条触发路径真机弹窗正常(Classic 死局 / 订单结束 / 通关)
- V5 按钮真机点击跳转正确
- V6 装饰占位(三图标 / 太阳格)不报错 / 不挡按钮 / 不穿帮

### 涉及模块 / UI / 事件 / 配置（给 dev 定位）
- 模块:UI(`GameModule.UI` ShowUIAsync/CloseUI)、资源(`SetSubSprite`)
- UI 窗:GameOverWindow(Top, fullScreen:true)、MergeOrderWinWindow(Top, fullScreen:true)
- 事件:无新增(按钮仍 onClick.AddListener,回调不动)
- 配置 / 资源:Sheet_settings 精灵表(复用,无新资源)
- 触发与回调全图见设计稿 §七结构图(本轮换皮不改任一条线)

## decisions（安全默认推进，交 boss 关单复核）

- **D1（最值得复核）效果图三图标(星/心/草各60)+太阳奖励格:无切图 + 无数据源 → 装饰占位**。`BlockGameState` 经 grep 只有 Score/HighScore/Combo,无星/心/草/太阳统计字段;Sheet_settings 也无对应切图。安全默认 = 装饰占位(摆位对位骨架、数字接已得 finalScore 或省略、不绑统计、不动逻辑);占位图勉强则退「整组省略只保分数+按钮」。**真做与否需产品定义这些图标各统计什么 + 提供数据源**,本轮不因它停机(占位即可交付完整换皮)。设计稿 §4.3。
- **B2 分数数字默认沿用 Text**(非位图 digits)。digits_white/yellow 是散 PNG 未打表,SetSubSprite 取不到;位图数字须先用设计 24 工具打表,属可选增强。设计稿 §4.4。
- **B3 通关窗 BtnAgain 文案默认「再来一局」**(非「下一关」)。回调是 `ShowUIAsync<MergeOrderWindow>`(重开同一局),DEMO 无关卡序列,「下一关」语义误导;**回调目标不改**,仅文案 boss 可拍。设计稿 §2.2。
- B4 木板子图 dev 读图选最贴效果图者(推断 box1/box2/button)。
- B5 「重试/下一关」黄条 vs「返回」木条默认同一张 button + 文本区分(切图无黄/木分态)。
- 全部可逆、不抵触去变现/离线还原方向 → 按 plan 红线不入 blockers。

## blockers（无安全默认 / 抵触 GDD / 不可逆）

无。本轮所有范围开关均有安全默认可走,无非裁决不可推进的方向问题。

## taskFlaw（任务定义硬伤）

无。任务定义自洽:两窗确为 code-built 在跑、效果图存在、路 A 可行、Sheet_settings 已打通,与工程现状无冲突。
