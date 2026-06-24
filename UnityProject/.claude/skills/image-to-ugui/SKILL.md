---
name: image-to-ugui
description: "截图/效果图 → Unity UGUI 视觉占位预制体。看图产出描述 JSON(中文描述 + 占位色 + 文字),经 Editor 工具 ImageToUguiBuilder 烤成 .prefab、截图比对迭代。有文字建文本、无文字建图片占位,控件人工转。触发场景:给一张 UI 截图或效果图,要在 Unity 场景里生成对应的 UGUI 视觉占位预制体。"
---

# 截图 → UGUI 视觉占位预制体

把一张 UI 截图/效果图，搭成 Unity 场景里的视觉占位预制体：有文字的区域建文本，其余建图片占位。
真按钮/滑条等控件由人在 Unity 里手动转。

## 能力边界（本版）

- 产物 = 纯视觉占位 prefab（节点树 + 文本/图片 + 布局），存 `Assets/AssetRaw/UI/Prefabs/`，当前场景留一个链接实例。
- 文本 = legacy `Text` + 配置字体（默认方正 GBK，渲染中文）。
- 图片 = `Image` 占位色块。
- **自适应布局**：烘焙器据"子框相对父框"几何自动推断锚点（拉伸/居中/锚边），产物随屏幕尺寸自适应——贴边的留在边、居中的随中心、占满父框的随父框拉伸，根节点全屏拉伸。设计分辨率下版面与硬摆一致，换尺寸时各就各位。可在节点填 `anchor` 字段显式覆盖。**前提是正确的容器化嵌套**（详见 [json-schema.md](../html-to-ugui/references/json-schema.md) 「自适应锚点」节）。
- 不做：真实素材切图导入、控件生成、UIBindComponent、`_Gen.g.cs`、impl 骨架——这些是后续步骤（见末尾）。

后端实现：`Assets/Editor/ImageToUgui/ImageToUguiBuilder.cs`，复用 `Assets/Editor/UguiBaker/` 的 `UguiBaker.Bake`。

## 数据流

```
截图  →（看图量布局）→  描述 JSON  →  ImageToUguiBuilder.BuildPrefab  →  .prefab + 场景实例
                                                       ↓ Screenshot 留证
                                           比对原图 → 改 JSON 迭代
```

## 工作流

### 1. 量图 → 产 JSON

- 定设计分辨率：取截图原生尺寸（竖屏游戏常用 1080×1920）。
- 自顶向下识别每个可见元素：包围盒（x/y/w/h，左上角为原点）、文字内容（若有）、字号、主色调 hex。
- 按嵌套结构输出 JSON。**格式契约见 [json-schema.md](../html-to-ugui/references/json-schema.md)**（与 html-to-ugui 同一契约，不在此重复）。
- `name` 写**中文描述**（说明该区域是什么，如"标题""保存按钮区""头像位"）。
- 有可见文字的节点填 `text`（建成文本）；纯色块/图标位不填 `text`、填 `color` 占位色（建成图片）。
- 图标/插画先用纯色或半透明色块占位。
- **文本节点只画文字，其 `color` 不作背景**：要做"按钮"这类带底色 + 文字的占位，用父图片节点（填 `color` 底色）+ 子文本节点（填 `text` 文字），别把 `text` 和 `color` 放同一节点（`color` 会被静默忽略）。
- **容器化嵌套是自适应的前提**：按"容器装其内容"组织层级——面板/卡片/行作父节点，其内部元素作 `children`（行里的头像/名字/分数胶囊挂行下，胶囊里的图标/数字挂胶囊下，屏幕级按钮挂屏幕级容器或根下）。坐标仍写相对根的绝对坐标，容器化只改父子关系、不改坐标值。**层级拍平（全平铺到根下）会让每个元素相对整屏定位，换尺寸时自适应退化。**

> 不在 JSON 阶段加控件命名前缀（`m_btn_` 等）：本管线不建控件、也不生成绑定。
> 真按钮/滑条等在烤完后由人在 Unity 里手动转；要生成 TEngine 绑定时，命名前缀由人在转控件那步按 `.claude/skills/tengine-dev/references/naming-rules.md` 后置添加。

### 2. 烤 prefab + 截图（unityMCP execute_code）

```csharp
string json = @"...";  // 第 1 步产出
var build = EditorTools.ImageToUgui.ImageToUguiBuilder.BuildPrefab(json, "<PrefabName>", 1080, 1920);
var shot  = EditorTools.ImageToUgui.ImageToUguiBuilder.Screenshot(
    "Assets/AssetRaw/UI/Prefabs/<PrefabName>.prefab",
    System.IO.Path.Combine(Application.dataPath, "Screenshots/<PrefabName>.png"), 1080, 1920);
return build + "\n" + shot;
```

- `BuildPrefab`：`UguiBaker.Bake` 搭节点（有文字建文本、无文字建图片）→ 存 prefab + 场景留实例。
- `Screenshot`：从 prefab 离屏渲一张 PNG（自包含，不依赖场景状态）。

### 3. 比对迭代

`Read` 截图 PNG，与原图并排比。偏差大就改 JSON 回第 2 步重跑（同名覆盖）。收敛判据：主结构（分区、占位位置、文字）对得上；占位色允许与原图不同。

## 坐标约定

- 原点 = 根节点左上角，X 右增、Y 下增；子节点 x/y 填**相对根节点的绝对坐标**（`Bake` 内部自动减去父偏移）。
- 透明容器 `color` 用 `#FFFFFF00`。

## 字体配置

- 文本节点的字体取 Project Settings → TEngine → UISettings → "默认 UI 字体"，开箱指向工程内方正 `GBK.ttf`，正常渲染中文。
- 换字体即改该设置后重烤。

## 已知限制 / 后续升级

- **占位色块**非真实素材。升级切图：PNG 拷进 Assets + 设 Sprite 导入参数 + Image 引用 sprite，JSON 增 sprite 字段。
- **控件人工转**：本版只产文本/图片占位。要交互的区域（按钮、滑条、输入框等），在 Unity 里给对应占位节点手动加 Button/Slider/InputField 等组件。
- **升级完整 TEngine 窗口**：手动转好控件并按前缀表命名后，对 prefab 跑 `ScriptAutoGenerator` 生成 `_Gen.g.cs` + impl 骨架，节点树无需返工。
