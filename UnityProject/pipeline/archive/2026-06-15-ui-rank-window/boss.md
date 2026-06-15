# Boss 关单总结:ui-rank-window 排行榜窗 · 表现层

- **日期**:2026-06-15
- **模式**:自治(纯 UI 补完);末屏
- **参与环节**:full(plan→opus / dev→opus / boss 桥直验);0 打回
- **结论**:PASS(逻辑档)。兑现遗留 #27(rank 表现层)。塔罗 UI 纯 UI 补完范围最后一屏。

## 任务定义

新建 RankWindow 显示榜单(榜单列表/我的名次条),数据层 rank(#27 RankService)已就绪,复用设置/个人信息窗范式。设计稿 `design-docs/28-rank-window-art.html`。**art 受限**:塔罗素材无排行榜专属切图 → 复用 Sheet_settings + 占位(榜行/徽章/头像)。

## 决策(plan + dev,纯 UI 补完默认)

- GameContext 扩持有 RankService(统一上下文末项兑现)。
- B1 邮件服务 = 真实 `MailboxService`(非 stub;点赞/结算奖经它真实下发收件箱,mail 表现层 #26 落地后复用同键即可见)。
- 行渲染 = 代码生成行(方案 B;Widget+池路线工程零先例、成本高;映射抽纯方法 BuildRowModels/MyRankText 供单测)。
- 点赞按钮默认省略(效果图无,D2,留接线点);多榜页签/奖励预览/结算触发/红点 本屏不做(D1/D4/D5/D6)。

## 验收(boss 主会话桥直验)

- **EditMode 427 全绿**(BlockBlast 415[407 + 8 例 rank window] + UIAtlasPacker 12),0 failed,零回归。
- 8 例:H2(GameContext 持 RankService + InitRankWithDeps 注入不连网不污染)、H3(查榜分降序+名次回填+IsSelf)、W3(board→行 VM + 我的名次条文本映射纯方法贯通真实数据)、W6(空榜/未入榜/null 不崩)。
- Code Review:范式合规、绑定 path 对齐、只加 GameContext 一成员+按钮、不动玩法/数据层。
- V 组(Play 对位 排行榜.png/列表渲染/三种关闭/不破坏玩法)= 人工 Play 手验遗留。

## 交付物

- 新增:`GameScripts/HotFix/GameLogic/UI/RankWindow.cs`;改 `GameContext.cs`(+Rank 成员/InitRank/InitRankWithDeps)、`UI/BlockBlastUI/MainMenuWindow.cs`(+BtnRank)
- 资源:`AssetRaw/UI/Prefabs/RankWindow.prefab`(复用 Sheet_settings,无新切图)
- 测试:`Editor/Tests/BlockBlast/RankWindowTests.cs`(8 例)
- 设计稿:`design-docs/28-rank-window-art.html`(+ nav.js)

## 遗留转出

- **[BLK1·无网络模块]** 真实全服榜 + 他人成绩:RemoteRankSource 返空,本屏榜单 = 本机 + 注入陪榜;网络模块就绪后实现远程源、服务层零改。
- **[BLK2·依赖 mail 表现层 #26]** 点赞/结算奖经邮件真实下发,但无邮件窗看不到。
- **[美术]** 榜行底/名次徽章(金银铜)/头像切图缺失,当前占位;prefab 静态文本未绑 Font(代码生成行运行期赋内置字体正常,静态标题待补字)。
- **[人工 Play 手验]** V 组对位/渲染/关闭。
