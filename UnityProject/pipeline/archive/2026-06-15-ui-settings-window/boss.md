# Boss 关单总结:ui-settings-window 设置窗美术换皮

- **日期**:2026-06-15
- **模式**:常规(用户在场)
- **参与环节**:full(plan→opus / dev→opus / test→opus),0 打回
- **结论**:PASS。兑现遗留 #24(settings 表现层)。

## 任务定义与范围

本工程**第一个美术驱动 UI 窗口** + **切图寻址基础设施** + **GameContext 运行期上下文**,目的是打通整条换皮链路(切图 → 每屏一图集 → prefab(m_ 前缀)→ ScriptGenerator/FindChildComponent 绑定 → [Window] 加载 → SetSubSprite 取图 → 热更)并成为后续约 20 个塔罗界面换皮的范式。数据逻辑层(设计 19 `GameLogic.Settings`)已于 2026-06-14 交付,本轮只做表现层 + 接线。

- 美术源:`C:\Users\pc\Downloads\塔罗\塔罗\效果图\setting.png`(扁平 PNG,1080×1920)+ 切图 `…\设置\`(21 张已命名)。
- 设计稿:`design-docs/23-settings-window-art.html`。

## 用户拍板决策

- **B1 运行期持有者 = 方案 B**:新建 `GameLogic.GameContext`(SimpleSingleton),统一持有「数据层已建、运行期无主」的系统;本轮先纳入 SettingsService,接口薄而通用,后续 player-info/item/mail/rank 逐个挂入,统一接存档。理由:同类摩擦在源头一次性解决。
- 切图寻址 = 每屏一图集 + `Image.SetSubSprite(图集location, 子图名)`;绑定 = FindChildComponent + m_ 前缀;窗口 = UIWindow + [Window(Top,"SettingsWindow",false)],prefab 根挂 Canvas;UI 代码在 GameScripts/HotFix。
- plan 其余 decisions(B2 清存档二次确认 / B3 Toggle / B4 音乐+音效双开关读图核实进窗 / B5 入口落主菜单 / B6 子图映射读图 / 社交·协议·客服 URL 占位+反馈)按安全默认推进。

## 寻址技术偏离(经 test 独立复核确认正确)

用户拍板的「SpriteAtlas v2」在本工程 SetSubSprite+YooAsset 下**技术不可行**:YooAsset 的 SubAssets 加载走 `AssetDatabase.LoadAllAssetRepresentationsAtPath`,对 `.spriteatlasv2` 不返回被打包的 Sprite(它们是源 PNG 的子表示,非图集文件的),故 `GetSubAssetObjects<Sprite>(图集)` = 0;无配置可救。dev 用**等价替代**:单张精灵表 PNG(Sprite Mode=Multiple,21 命名子精灵)`Sheet_settings`,SubAssets count=21,`SetSubSprite("Sheet_settings", 子图名)` 端到端取图成功。API 形态/子图名/节点树/验收全等价,且更贴「每屏一图集」目标(子图名只需屏内唯一、合批省 drawcall)。plan §3.2 已预判此风险。test 独立复核技术结论成立、**采纳精灵表 PNG 为项目级范式**。

附微调:AudioSink 接线从非热更区 ProcedureLaunch 移到热更入口 `GameApp.StartGameLogic()`(程序集边界所致)。

## 交付物

- 新增代码(HotFix):`GameLogic/GameContext.cs`、`GameLogic/UI/SettingsWindow.cs`
- 改既有(HotFix):`GameApp.cs`(接 AudioSink + GameContext 初始化)、`UI/BlockBlastUI/MainMenuWindow.cs`(设置入口按钮)
- 资源:`AssetRaw/UIRaw/Atlas/setting/`(21 切图)、`AssetRaw/UIRaw/Atlas/Sheet_settings.png`(精灵表)、`AssetRaw/UI/Prefabs/SettingsWindow.prefab`(43 节点)
- 测试:`Editor/Tests/BlockBlast/GameContextTests.cs`(7 例:H1/H2/H3a/H3b/W2×2/AudioSink)
- 设计稿:`design-docs/23-settings-window-art.html`(+ nav.js 加「表现层/换皮」组)

## 验收结论(test PASS)

- 编译 0 报错;EditMode 366/366 全绿(settings 数据层零回归);GameContextTests 7 例反射逐例真验非空跑。
- Code Review:5 条编码红线全合规;GameContext 接口薄而通用、单例生命周期/Load 时机正确、测试注入接缝干净;遮罩 z 序穿透已规避。
- Play 独立复验:V2 寻址 100% 打通(Sheet_settings SubAssets 21、26/26 应贴图节点全贴、对位 setting.png faithful);V3 走生产 PlayerPrefs 持久化往返通;V5 占位 7 钮「待建」反馈无死按钮。
- V1 点击关窗(指针)、V3 真实实听、V4 玩法手感 = 留人工 Play 手验遗留(MCP 不能模拟指针/音频)。

## 遗留转出(见主 boss.md 遗留事项)

1. **范式工具化**:散切图目录 → 合成 Multiple 精灵表 PNG 的 Editor 打表工具,建议第二屏换皮前补齐(否则 20 屏手工成本线性累积)。
2. **孤儿资源清理**:`Assets/AssetArt/Atlas/Atlas_setting.spriteatlasv2`(+.meta,单数)是 dev 失败尝试残留、真孤儿,应删。
3. **设置窗占位接线**:社交外链/联系客服/更多游戏/语言切换/退出登录/清除存档当前占位 + 反馈;协议·隐私接 OpenURL 占位 URL;兑换码入口待接 GameLogic.Redeem。真实 URL/行为待产品提供或对应系统接入。
4. **人工 Play 手验**:设置窗 X/遮罩指针关窗、音效真实实听。
5. **后续界面**:塔罗成套美术其余约 19 屏换皮(分换皮已有 + 新功能需先做数据层两类),按系统逐个走 /pipeline。
