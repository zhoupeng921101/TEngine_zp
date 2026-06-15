# Boss 关单总结:ui-tarot-hud tarot_mode 主玩法 HUD reskin

- **日期**:2026-06-15
- **模式**:自治(纯 UI 补完);centerpiece、风险最高
- **参与环节**:full(plan→opus / dev→opus / boss 桥直验);0 打回
- **结论**:PASS(回归档)。塔罗 UI 自治线第 4 屏。首个用打表工具产新精灵表的真实换皮屏。

## 调查结论(plan)

tarot_mode = Classic `GameWindow`(435 行)的再主题(非合成订单 MergeOrderWindow;证据:大居中分数、无订单卡/合成区/体力条)。顶栏 3 资源条多数无数据源(BlockGameState 只 Score/HighScore/Combo);更换/删除/提示 = 三个新玩法机制,Classic+数据层均无。

## 任务定义 + 范围

路 A 轻量换皮,**只改 `GameWindow.BuildStaticUI` 静态视觉壳**:背景/棋盘外框贴 `Sheet_tarot_mode` 子图、新增静态顶栏(头像占位+3 资源条+齿轮)+ 3 动作按钮。玩法逻辑/坐标映射/数据层绝不碰。新机制=视觉占位+stub。设计稿 `design-docs/27-tarot-mode-hud-art.html`。

## 决策(均 plan 默认,纯 UI 补完)

- D1 资源条占位(第 1 条接 HighScore,余占位;加号去变现不接购买)——**真做需产品定义 3 资源各是什么+数据源**。
- D5 三动作按钮(更换/删除/提示)stub(点击 Log 待建)——**真机制需产品定义,本范围外另开轮**。
- D2 头像占位圆(不把 PlayerInfo 接进 Classic);D3 广告切图不投放(去变现);D4 退出钮独立保留;B1 格底保纯色微调;B2 分数沿用 Text。

## 验收(boss 主会话桥直验)

- **EditMode 419 全绿**(BlockBlast 407[393+14 例换皮回归 R1-R5 零回归] + UIAtlasPacker 12),0 failed。**零回归硬验收达成**——玩法逻辑方法体零增删(git diff + 14 例静态核对双证:RenderBoard/落子/消除/补块/GameOver/坐标常量/数据层字段/退出回调/MergeOrderWindow 全未改)。
- 打表实测:`UIAtlasPacker.Pack` 产 `Sheet_tarot_mode.png`(13 子图、2048×1024、Multiple、九宫格 border 正确继承);importer 与已验证寻址通的 `Sheet_settings` 完全一致、落同收录树 → 寻址同源。EditMode 验产出表子图元数据完整(13 命名子精灵);运行期 SetSubSprite 取图 = V1 人工 Play 手验(YooAsset 非 Play 态未初始化,EditMode 不能跑运行期寻址)。
- V 组(Play 对位 tarot_mode.png / Classic 整局实玩零回归 / 棋盘落子不偏 / 齿轮开设置 / 占位 stub 不报错)= 人工 Play 手验遗留。

## 交付物

- 改:`UI/BlockBlastUI/GameWindow.cs`(+104/-16,全落 BuildStaticUI 视觉 + BuildTopBar/BuildActionButtons 两新方法;玩法逻辑零增删)
- 测试:`Editor/Tests/BlockBlast/GameWindowReskinRegressionTests.cs`(14 例回归)
- 资源:`AssetRaw/UIRaw/Atlas/tarot_mode/`(13 切图,ASCII 目录)+ `Sheet_tarot_mode.png`(打表产物)
- 设计稿:`design-docs/27-tarot-mode-hud-art.html`(+ nav.js)

## 遗留转出

- **[打表工具维护·真实缺陷]** 单张源图超 MaxAtlasSize(2048)会被 PackTextures 静默丢弃,工具仍报 Success=源数(实际子图少)。设计 24 §四的 2048 校验只防总面积、未防单图越界。dev 已剔除超大图(image/tarot_mode/advertisement)重打得 13 张正确,本任务无影响;但工具应补「单图超表」校验 + 丢图即报错。
- **[产品定义·D1/D5]** tarot HUD 3 资源条语义+数据源、3 动作按钮(更换/删除/提示)机制规则——需产品定义后另开数据层+玩法轮。
- **[人工 Play 手验]** V1-V7(运行期寻址 + 整局实玩零回归 + 对位)。
- **[环境]** dev 跑测时 Unity 在 Play(疑手验),已 stop 退出;当前 Edit 态。
- **[自动产物·噪声]** `AssetArt/Atlas/Atlas_tarot_mode.spriteatlasv2` 是 Atlas 子目录自动生成的 v2 图集(不参与寻址、删了重生成),同既有 Atlas_blocks_* 噪声,未纳入提交。
