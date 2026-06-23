---
name: image-to-ugui
description: "截图/效果图 → Unity UGUI 视觉预制体。看图产出 UIDataNode JSON(项目命名前缀 + 占位色),经 Editor 工具 ImageToUguiBuilder 烤成 .prefab、截图比对迭代。触发场景:给一张 UI 截图或效果图,要在 Unity 场景里生成对应的 UGUI 预制体。"
---

# 截图 → UGUI 视觉预制体

把一张 UI 截图/效果图,用项目标准 UGUI 组件重搭成 Unity 场景里的视觉预制体。

## 能力边界（本版）

- 产物 = 纯视觉 prefab(节点树 + 组件 + 布局 + 占位色),存 `Assets/AssetRaw/UI/Prefabs/`,当前场景留一个链接实例。
- 文本 = legacy `Text` + `LegacyRuntime` 字体(渲染中文,与项目现有 UI 一致)。
- 不做:真实素材切图导入、UIBindComponent、`_Gen.g.cs`、impl 骨架——这些是后续升级(见末尾)。

后端实现:`Assets/Editor/ImageToUgui/ImageToUguiBuilder.cs`,复用 `Assets/Editor/HtmlToUGUI/HtmlToUGUIBaker.cs` 的 `Bake`。

## 数据流

```
截图  →（看图量布局）→  UIDataNode JSON  →  ImageToUguiBuilder.BuildPrefab  →  .prefab + 场景实例
                                                          ↓ Screenshot 留证
                                              比对原图 → 改 JSON 迭代
```

## 工作流

### 1. 量图 → 产 JSON

- 定设计分辨率:取截图原生尺寸(竖屏游戏常用 1080×1920)。
- 自顶向下识别每个可见元素:类型、包围盒(x/y/w/h,左上角为原点)、文字内容、字号、主色调 hex。
- 按 UIDataNode 嵌套结构输出 JSON。**格式契约见 [json-schema.md](../html-to-ugui/references/json-schema.md)**(与 html-to-ugui 同一契约,不在此重复)。
- `type` 限 9 种:`div` `image` `text` `button` `input` `scroll` `toggle` `slider` `dropdown`。
- `name` 用项目命名前缀(下表),使命名与组件类型一致 —— 本版虽不生成绑定,但命名对齐后,将来升级绑定生成器可直接接上。
- `color` 填占位色;图标/插画先用纯色或半透明色块占位。

命名前缀(源:`Assets/Editor/UIScriptGenerator/ScriptGeneratorSetting.asset`,常用项):

| 元素 | 前缀 | 例 |
|------|------|----|
| 背景/容器 | `m_img_` / `m_tf_` | `m_img_Bg` |
| 文本 | `m_text_` | `m_text_Title` |
| 按钮 | `m_btn_` | `m_btn_Save` |
| 图片 | `m_img_` | `m_img_Icon` |
| 滑条 | `m_slider_` | `m_slider_Vol` |
| 开关 | `m_toggle_` | `m_toggle_Full` |
| 下拉 | `m_tmpDropdown_` | `m_tmpDropdown_Quality` |
| 输入 | `m_tmpInput_` | `m_tmpInput_Name` |

### 2. 烤 prefab + 截图（unityMCP execute_code）

```csharp
string json = @"...";  // 第 1 步产出
var build = EditorTools.ImageToUgui.ImageToUguiBuilder.BuildPrefab(json, "<PrefabName>", 1080, 1920);
var shot  = EditorTools.ImageToUgui.ImageToUguiBuilder.Screenshot(
    "Assets/AssetRaw/UI/Prefabs/<PrefabName>.prefab",
    System.IO.Path.Combine(Application.dataPath, "Screenshots/<PrefabName>.png"), 1080, 1920);
return build + "\n" + shot;
```

- `BuildPrefab`:`Bake` 搭节点 → 文本项目化(TMP→legacy Text)→ 存 prefab + 场景留实例。
- `Screenshot`:从 prefab 离屏渲一张 PNG(自包含,不依赖场景状态)。

### 3. 比对迭代

`Read` 截图 PNG,与原图并排比。偏差大就改 JSON 回第 2 步重跑(同名覆盖)。收敛判据:主结构(分区、控件位置、文字)对得上;占位色允许与原图不同。

## 坐标约定

- 原点 = 根节点左上角,X 右增、Y 下增;子节点 x/y 填**相对根节点的绝对坐标**(`Bake` 内部自动减去父偏移)。
- 透明容器 `color` 用 `#FFFFFF00`。

## 已知限制 / 后续升级

- **中文**只在 legacy `Text` 与按钮标签上正常;`input` 占位符、`dropdown` 选项是 TMP 控件,无 CJK TMP 字体资产时其中文显示为空框(英文/数字不受影响)。补法:生成一个 CJK TMP 字体资产并赋给这两类控件。
- **占位色块**非真实素材。升级切图:PNG 拷进 Assets + 设 Sprite 导入参数 + Image 引用 sprite,JSON 增 sprite 字段。
- **升级完整 TEngine 窗口**:命名已按前缀表,对 prefab 跑 `ScriptAutoGenerator` 即生成 `_Gen.g.cs` + impl 骨架,节点树无需返工。
