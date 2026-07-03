---
name: project-old-inline-window-add-node-regen
description: 给老式 UIWindowMono/UIWidgetMono 内联绑定窗(无 UIBindComponent、[SerializeField] m_xxx 字段 + ScriptGenerator() 只挂 onClick)增量加节点时,重生成绑定是低风险的:按节点名逐字段绑定(非按序索引),加唯一命名节点不会移位打乱现有绑定。
type: project
---

老式窗(如 MergeOrderWindow)绑定机制:根**无 UIBindComponent**,`_Gen.g.cs` 是 `[SerializeField] private T m_xxx;` 字段 + `ScriptGenerator()` 里只 `onClick.AddListener`(引用靠 prefab 序列化存,不在运行时 FindChildComponent)。给这类窗增量加节点 + 重生成,是**低风险**操作,不必因"怕打断现有绑定"而回避。

**Why:** 绑定是**按节点名**逐字段匹配(`UIBaseMonoEditor.BindSerializedFieldsByNodeName`),不是按遍历序的索引列表。所以加几个唯一命名的 `m_` 节点,重生成只是多几个字段,现有字段按同名节点原样绑回,不会移位。所有 `m_` 节点名唯一即安全(重复的非 `m_` 结构节点不参与绑定)。曾误判这类窗"重生成会索引错位伤及现有绑定"而建议不做 UI——那是把新式 UIBindComponent(按序索引)的风险错安到老式按名窗上。

**How to apply:**
- 判别机制:看窗口根有无 `UIBindComponent`。无 = 老式按名内联,按本条走;有 = 新式索引绑定,改树须两步一起重跑(见 pipeline-lite-ui SKILL「索引不变量」)。
- 加节点:`PrefabUtility.LoadPrefabContents` → 建唯一命名 `m_` 节点(如 `m_text_Goddess`/`m_btn_GoddessClaim`/`m_img_GoddessRedDot`,按钮下 `Label` 子节点无 `m_` 不绑定) → `SaveAsPrefabAsset`。脚本做**幂等**(先删旧同名容器再建),防超时重试重复。
- 重生成:`TEngine.Editor.UI.ScriptGenerator.GenerateCSharpScript(root, includeListener:true, isUniTask:false, isAutoGenerate:true, savePath:GetGenCodePath(), className:"<窗名>", uiGenTypeName:"UIWindowMono", isGenImp:false, null)`——`isGenImp:false` 不碰 impl 主文件、不覆盖用户逻辑。等编译。
- 绑定:`ScriptGenerator.FindNodeByNameWithWidgetBoundary(root, 字段名)`(public static)找同名节点 → `SerializedObject.FindProperty(字段名).objectReferenceValue = node.GetComponent(字段类型)` → `SaveAsPrefabAsset`。绑定**必须在重生成编译完成后**跑(新字段要先编译进类型,反射/FindProperty 才找得到)。
- 按钮回调:生成的 `private partial void OnClick_XxxBtn();` 是 **C# 9 扩展 partial(带 private 修饰符)必须有实现体**,否则 CS8795。在主文件加空 stub 让其编译(业务逻辑由后续填),不要指望"无实现体可省略"(那是无修饰符的旧式 partial)。
- 自检截图:老式大窗 `OnCreate` 重度依赖游戏态(单例/配置/服务端 RPC),裸 `Instantiate` 到 UICanvas 会因设计坐标空间被框架缩放而整窗出屏(scale=1 太大)。可靠办法:Play 态实例化后按窗口内容 **AABB 取景**渲染(相机对准内容包围盒),忠实还原相对布局验节点位置/样式,不依赖吃透绝对缩放。相关 [[project-editor-ui-screenshot-via-camera]]。
