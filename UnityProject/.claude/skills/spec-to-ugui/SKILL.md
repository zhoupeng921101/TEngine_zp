---
name: spec-to-ugui
description: "文字描述 → Unity UGUI 视觉占位预制体(无需截图/HTML)。按描述 JSON 契约直接手写节点树,经 unityMCP ImageToUguiBuilder.BuildPrefab 直烤成 .prefab、截图比对迭代。主要用于测试界面/临时界面的快速搭建。有文字建文本、无文字建图片占位,控件人工转。触发场景:凭一句文字需求(非截图)在 Unity 里快速生成一个 UGUI 占位界面,尤其测试/临时面板。"
---

# 文字描述 → UGUI 视觉占位预制体(无图直烤)

凭一句需求(没有截图、没有 HTML)在 Unity 里搭出 UGUI 视觉占位预制体,主要用于**测试界面 / 临时面板**的快速搭建。
与 image-to-ugui 共用同一后端(`ImageToUguiBuilder.BuildPrefab` + `UguiBaker`),差别只在:坐标不靠量图,由你按标准布局直接编排。

## 何时用这条,何时用别的

- 有截图/效果图 → 用 **image-to-ugui**(量图取坐标)。
- 要浏览器级 flexbox 精细布局、且已装 playwright → **html-to-ugui**。
- 只要一个测试/临时面板、凭描述快速出占位 → **本 skill**(最轻:无浏览器依赖、无手动粘贴,经 unityMCP 直烤)。

## 能力边界

与 image-to-ugui 同:产物为纯视觉占位 prefab(文本/图片占位 + 布局),存 `Assets/AssetRaw/UI/Prefabs/`,当前场景留一个链接实例;真按钮/滑条等控件人工转;真实切图、绑定、`_Gen.g.cs`、impl 骨架是后续步骤。详见 [image-to-ugui/SKILL.md](../image-to-ugui/SKILL.md) 「能力边界」与「已知限制 / 后续升级」。

## 工作流

### 1. 定分辨率 + 先分组

- 分辨率按目标设备取(竖屏 1080×1920 / 横屏 PC 1920×1080 / Pad 2048×1536)。
- **先分组再填叶子**:先列容器树(面板、行、按钮条、表单区…),再填叶子——分组是自适应锚点的前提。两趟法、判据与「烤前自检」清单同 [image-to-ugui/SKILL.md](../image-to-ugui/SKILL.md) 第 1 步;容器化原理见 [json-schema.md](../html-to-ugui/references/json-schema.md) 「容器化嵌套」节。

### 2. 编排坐标(无图的关键)

没有截图可量,坐标由你按标准布局算。测试/临时界面常见几种版式,直接套:

- **竖排列表 / 按钮栈**:固定行高 + 行距,y 逐行递增;同列元素共 x、等宽。
- **表单**:每行 左标签 + 右字段(输入框先用占位色块),逐行下排。
- **按钮条**:等宽等距横排,x 均分。
- **标题 + 内容区 + 底部按钮**:纵向三段分屏。

每个节点给:包围盒 x/y/w/h(左上角原点,**相对根的绝对坐标**)、`name` 中文描述、有文字填 `text`(建文本)/纯色块填 `color` 占位色(建图片)。容器节点色用透明 `#FFFFFF00`。字段契约见 [json-schema.md](../html-to-ugui/references/json-schema.md)。

> 「按钮」这类带底色 + 文字的占位:父图片节点(填 `color` 底色)+ 子文本节点(填 `text`),别把 `text` 和 `color` 放同一节点(文本节点的 `color` 被静默忽略)。

### 3. 直烤(unityMCP execute_code)

```csharp
string json = @"...";  // 第 2 步产出;C# 逐字字符串里的双引号写成两个
var build = EditorTools.ImageToUgui.ImageToUguiBuilder.BuildPrefab(json, "<PrefabName>", W, H);
var shot  = EditorTools.ImageToUgui.ImageToUguiBuilder.Screenshot(
    "Assets/AssetRaw/UI/Prefabs/<PrefabName>.prefab",
    System.IO.Path.Combine(Application.dataPath, "Screenshots/<PrefabName>.png"), W, H);
return build + "\n" + shot;
```

> JSON 较大时改为写到临时文件、`System.IO.File.ReadAllText` 读入,避免逐字字符串转义。
> `BuildPrefab` 内置结构 lint:漏容器(同尺寸对等项平铺)会在返回串追加一条 `[分组提示]`,据此收进容器重烤。

### 4. 比对迭代

`Read` 截图 PNG,看分区结构、文字、对齐是否符合需求描述;不符改 JSON 回第 3 步重跑(同名覆盖)。无原图,收敛判据是**符合需求描述**(分区、层级、文字对得上),而非"像某张图"。

## 后续升级

控件人工转 → 按前缀表命名 → `ScriptAutoGenerator` 生成绑定,同 [image-to-ugui/SKILL.md](../image-to-ugui/SKILL.md) 「已知限制 / 后续升级」,不在此重列。
