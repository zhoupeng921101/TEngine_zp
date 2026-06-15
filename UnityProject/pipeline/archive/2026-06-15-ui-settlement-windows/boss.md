# Boss 关单总结:ui-settlement-windows 结算窗 reskin

- **日期**:2026-06-15
- **模式**:自治(纯 UI 补完)
- **参与环节**:full(plan→opus / dev→sonnet / boss 桥直验);0 打回
- **结论**:PASS(逻辑/回归档)。塔罗 UI 自治线第 3 屏。

## 任务定义

reskin 两个在跑的 code-built 结算窗:GameOverWindow(游戏结束)+ MergeOrderWinWindow(恭喜通关)。路 A 轻量换皮——`UGuiFactory.CreateImage/CreateButton` 返回的 Image 链 `.SetSubSprite("Sheet_settings", 子图名)` 换木质贴图,底色 white 透出木纹。结算逻辑/UserData 取参/重开·返回回调一行不改。设计稿 `design-docs/26-settlement-window-art.html`。

## 决策(plan + dev,自治默认)

- reskin 方式 = 路 A 轻量(不取路 B prefab 重构,避免重写在跑窗口)。
- B4 子图映射:box1=主面板、box2=标题木牌底、button=所有按钮底。
- D1 装饰层(效果图三图标 星/心/草 + 太阳奖励格)= 取方案 B **省略**(Sheet_settings 无对应切图、数据层无星/心/草/太阳统计字段;摆纯色方块美观度不达标)。**真做需产品先定义这些图标各统计什么 + 给数据源**——遗留。
- B3 通关窗按钮文案「再来一局」(回调 ShowUIAsync<MergeOrderWindow> 不改;DEMO 无关卡序列,「下一关」语义误导)。
- B2 分数沿用 Text(digits 散图未打表,位图数字为可选增强)。

## 验收(boss 主会话桥直验)

- **EditMode 405 全绿**(BlockBlast 393[含新增 15 例结算窗回归 R1-R4/C1-C2/V1 链路] + UIAtlasPacker 12),0 failed。**零回归硬验收达成**(两窗被 Classic + 订单结束 + 通关三路复用,结算逻辑/三处调用传参/四回调目标经回归测试核对未改)。
- 注:dev 报「378」是其会话新测试文件未及重编的过时计数;boss 强制 refresh+重编后 BlockBlast=393,15 例结算窗回归全绿。
- V 组(Play 视觉对位 游戏结束.png/恭喜通关.png、真机三路弹窗、按钮跳转)= 人工 Play 手验遗留。

## 交付物

- 改:`UI/BlockBlastUI/GameOverWindow.cs`(+64 视觉)、`MergeOrderWinWindow.cs`(+51 视觉)
- 测试:`Editor/Tests/BlockBlast/SettlementWindowRegressionTests.cs`(15 例回归)
- 设计稿:`design-docs/26-settlement-window-art.html`(+ nav.js)
- 复用 Sheet_settings,无新切图。

## 遗留转出

- **[产品定义·D1]** 结算图标(星/心/草统计 + 太阳奖励)语义无 spec、无数据源、无切图——省略中;真做需产品定义 + 美术切图。
- **[人工 Play 手验]** V 组对位/真机弹窗/跳转。
- **[可选增强·B2]** 分数位图数字(digits 用打表工具打表后可换)。
