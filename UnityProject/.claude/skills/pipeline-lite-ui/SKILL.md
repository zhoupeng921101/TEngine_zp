---
name: pipeline-lite-ui
description: 轻型流水线 UI 制作段。在 Unity 里搭建 UGUI prefab(节点层级/组件/容器/布局),按命名前缀表生成 BindComponent 绑定(_Gen.g.cs)+ impl 脚手架。由 pipeline-lite 主会话流程在含新 UI 窗口/prefab 的 client 段用 Skill 工具调用,先于业务逻辑实现,不单独触发。
---

# 轻型流水线 · UI 制作段

pipeline-lite 主会话在含新 UI 窗口/prefab 的 client 段调用本 skill:把 UI 意图**直接在 Unity 里搭成完整 UGUI prefab**——节点层级、组件、容器、锚点布局,按命名前缀表生成绑定骨架(`UIBindComponent` + `_Gen.g.cs`)。产出的节点清单进主流程,业务逻辑由后续 client 实现段填。**不走 html-to-ugui**(那是重型 pipeline-ui 的路)——直接 MCP 搭建,精确可控。

## 强制工作流 / 红线(继承项目规范,不复制)
- UIWindow/UIWidget 生命周期、节点绑定、事件:读 `.claude/skills/tengine-dev/references/ui-lifecycle.md` + `ui-patterns.md`;命名规范 `naming-rules.md#ui-节点命名规范`。
- 编码红线唯一信息源 `.claude/skills/tengine-dev/SKILL.md`「核心红线」。
- 写持久文件前遵 `.claude/rules/conventions.md`。
> 不复制条文:副本必漂移。

## 开工前(碰 Unity 前)
- 先跑 `/unity-check` 确认 MCP 连到正确实例(按名 UnityProject)。搭树全程依赖 Unity 响应,连不上时先解决连接再搭。
- 读共享 UI 经验库 `.claude/agent-memory/pipeline-ui/`(与重型 pipeline-ui 同一库,本 skill 不被自动注入,手动 Read)。

## 输入
主会话的 self-contained 简报:UI 意图(窗口/控件清单、布局关系、参照范式如「照 SettingsWindow」)+ 数据/交互期望。**无 design-docs、无 plan.md**。

## 工具链
- 节点树:MCP `manage_prefabs`(create/modify)、`manage_gameobject`、`manage_ui`、`manage_components`;纹理导入参 `manage_texture`。
- 驱动绑定生成:`execute_code` 调 `TEngine.Editor.UI.ScriptGenerator`(见「生成绑定」)。
- 参照本地范式:`AssetRaw/UI/Prefabs/SettingsWindow.prefab` + `UI/SettingsWindow.cs`(已搭好的标准窗)。

## 工作流
1. **规划节点树**:按 UI 意图定层级(面板/行/容器),区分「需绑定的控件」(给 `m_` 前缀)与「纯结构/装饰节点」(无 `m_`、用中文名;绑定与命名判据见 `naming-rules.md`§UI 节点命名规范——只绑代码会引用的)。
   - **层级即语义包含**:视觉上「A 在 B 里」就让 A 作 B 的子节点——按钮文字放按钮节点下、背景内的内容元素放背景节点下、卡片内图标/文字放卡片节点下。保绘制顺序 + 整体移动/显隐 + 按钮点击区与 Label 一致,不拍平成同级。
   - 动态内容区搭**空容器节点**(像 frog-client `m_rect_hightRoot`),运行时由后续 client 段代码塞 item。
2. **MCP 搭树**:建节点 + 组件。**窗口根只挂 Canvas + GraphicRaycaster(+ UIBindComponent),不挂 CanvasScaler**(缩放由 UIRoot 的 canvas + Content 节点 localScale 负责;CanvasScaler 误挂窗口根会让 root scale 塌成 0、整窗不可见)。根 RectTransform 必须 stretch:`anchorMin=(0,0)`/`anchorMax=(1,1)`/`localScale=(1,1,1)`/`pivot=(0.5,0.5)`/`sizeDelta=0`,对照 `GameWindow.prefab` 根约定。
3. **命名**:控件节点按前缀表命名(见下),工具据此匹配组件类型。
4. **布局**:锚点/pivot 适配分辨率;成排/列表用 LayoutGroup(`m_hlay`/`m_vlay`/`m_grid`),固定位用锚定。
5. **静态视觉**:**素材已就绪的静态图(底框/图标/卡等永不变的)直接烤进 prefab 的 `m_Sprite`+`m_Color`**——编辑器所见即所得,prefab 为静态视觉唯一来源(烤法见 dev 经验库 `project-bake-static-sprite-into-prefab-wysiwyg`:`PrefabUtility.LoadPrefabContents`→设 `img.sprite`→`SaveAsPrefabAsset`;源 PNG 须 Single 模式);仅尚无终稿美术的才留裸 Image + 占位纯色交后续。**动态换皮(随游戏状态变的棋盘格/候选/元素等)始终运行时 SetSprite、不烤**——这部分本就在空容器里由后续 client 段代码填。
6. **生成绑定**(见下)。

## 命名前缀表(契约,完整以 `Assets/Editor/UIScriptGenerator/ScriptGeneratorSetting.cs` 的 `scriptGenerateRule` 为准)
`m_btn`→Button · `m_img`→Image · `m_tmp`→TextMeshProUGUI · `m_text`→Text · `m_toggle`→Toggle · `m_slider`→Slider · `m_scroll`→ScrollRect · `m_input`/`m_tmpInput`→InputField/TMP_InputField · `m_grid`→GridLayoutGroup · `m_hlay`/`m_vlay`→H/V LayoutGroup · `m_rect`→RectTransform · `m_go`→GameObject · `m_canvasGroup`→CanvasGroup · `m_item*`→UIWidget(**遍历到此停止递归**)。
> 需绑定的控件节点用 `m_` 前缀;字段名风格(`m_`/`_`)由 `ScriptGeneratorSetting.CodeStyle` 决定,工具自动转换。纯结构/装饰节点不加 `m_`、用中文名(不会被绑定、不进索引)。

## 生成绑定(本段核心 · BindComponent + `_Gen.g.cs`)
选中 prefab 根(`Selection.activeGameObject`),`execute_code` 依次调:
1. `ScriptGenerator.GenerateUIComponentScript()`:根加 `UIBindComponent`、按前缀表遍历把控件**按序**注册进索引列表(prefab 模式自动存盘)。
2. `ScriptGenerator.GenerateCSharpScript(includeListener:true, isAutoGenerate:true, savePath:"Assets/GameScripts/HotFix/GameLogic/UI/Gen", className:"<窗口名>", uiGenTypeName:"UIWindow", isGenImp:true, impSavePath:"Assets/GameScripts/HotFix/GameLogic/UI")`:写 `<窗口名>_Gen.g.cs`(只读:字段 + `ScriptGenerator()` 索引绑定 + 监听注册 + partial 回调声明)+ impl partial 脚手架(空回调体,供后续 client 段填)。

> **索引不变量**:两步用同一遍历序、索引对齐。**改了节点树就两步一起重跑**——只改 prefab 不重生成会让索引错位。`_Gen.g.cs` 是只读生成物,不手改(改名/改树 → 重生成)。
> BindComponent 是本工程从「内联 FindChildComponent(SettingsWindow 等)」转向的新约定:**新窗走 BindComponent,旧窗维持内联不强迁**,过渡期两种并存。建议把 `ScriptGeneratorSetting.UseBindComponent` 设 true,让人用的菜单也对齐到这条。

## 不做
- 业务逻辑(OnCreate 取数 / refresh / 回调体)→ 后续 client 实现段。
- 终稿美术 sprite → 重画同名图 / 代码寻址。
- 设计文档 → lite 无。

## 产出(进主流程)
1. **prefab 路径** + **`_Gen.g.cs` 路径** + **impl partial 路径**。
2. **节点清单**:所有 `m_` 控件节点 + 类型 + 索引(供后续 client 段直接用)。
3. 结构性约束(LayoutGroup 配置、ScrollRect 参数、动态容器节点名)+ 占位待美术项。

## 自检(交付前必做)
- prefab 能 `LoadGameObject` 加载;根仅 Canvas + GraphicRaycaster(+ UIBindComponent)、**无 CanvasScaler**;根 RectTransform `localScale==1` 且 `anchorMax==(1,1)`(对照 `GameWindow.prefab`)。
- **交付前 Play 开窗(或 `Instantiate` 进 UICanvas)目视渲染,不只信"控制台 0 错"**:root scale=0 / CanvasScaler 误挂这类故障在编译/控制台/节点核对里全部显示无错,只有 Play 截图或核 `root.GetWorldCorners` 才暴露。
- 命名逐项对前缀表合规(命名错 = 后续 client 段接手最常见返工源)。
- 绑定编译过:`read_console` 确认 `_Gen.g.cs` + impl 0 报错、`isCompiling=false`;`UIBindComponent` 索引数 = `_Gen.g.cs` 绑定条数。
- 占位 / 动态容器在「节点清单」标注。

## 红线
- 控件命名必用前缀表前缀,否则工具不生成其绑定。
- `_Gen.g.cs` 是只读生成物,不手改;改树重跑两步生成。
- 写持久文件前遵 `.claude/rules/conventions.md`。

## 收尾
仅当一条经验是**规则型、可复用、能改变未来同类任务行为**时,才沉淀到 `.claude/agent-memory/pipeline-ui/<slug>.md`(共享经验库,重型 pipeline-ui 也受益)。准入按 `.claude/rules/conventions.md`§规则准入:举得出「没有这条、下次会做错」的**复发**场景才写;单次事件、「修过 X / 解决了 Y」式过程记录、模型默认就会的事一律不写——这些进 git 历史,不进记忆。结构:独立结构化文件,frontmatter name/description/type,body rule + **Why:** + **How to apply:**;加索引到 MEMORY.md。
