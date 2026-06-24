# 描述 JSON 格式

烘焙器从 HTML 提取坐标后输出如下 JSON 结构，供 Unity Editor 的 UguiBaker 还原成视觉占位节点树。
每个节点只产出二选一的视觉占位：有可见文字（`text` 非空）建文本（Text），否则建图片（Image）。
不设控件类型字段——真按钮/滑条等控件由人在 Unity 里手动转。

## 节点结构

```json
{
  "name": "标题",            // 中文描述：说明该节点是什么（替代原控件类型），同时作为生成节点名
  "x": 0,                    // 相对根节点的 X 坐标 (px)
  "y": 0,                    // 相对根节点的 Y 坐标 (px)
  "width": 1920,             // 节点宽度 (px)
  "height": 1080,            // 节点高度 (px)
  "color": "#2C3E50",        // 图片占位色 (#RRGGBB 或 #RRGGBBAA)，透明为 #FFFFFF00；文本节点可不填
  "text": "显示文本",         // 有可见文字 → 建文本(Text)；为空 → 建图片(Image)
  "fontColor": "#FFFFFF",    // 仅文本用：字体颜色
  "fontSize": 24,            // 仅文本用：字体大小 (px)
  "textAlign": "center",     // 仅文本用：文本对齐 left/right/center
  "children": []             // 子节点数组，结构相同
}
```

## 文本 vs 图片判定

- `text` 非空字符串 → 文本节点：建 Text，应用 `fontColor/fontSize/textAlign`，用配置的默认 UI 字体渲染（中文走方正 GBK 字体）。
- `text` 为空 → 图片节点：建 Image，填 `color` 占位色；全透明（alpha≈0）时自动关闭射线，不挡下层。

> **文本节点只画文字，其 `color` 不作背景**：建成 Text 的节点不会渲染底色，`color` 被忽略。要做"按钮"这类带底色 + 文字的占位，用**父图片节点（填 `color` 底色）+ 子文本节点（填 `text` 文字）**，如下方示例的"保存按钮区"+"按钮文字"。把 `text` 和 `color` 写在同一节点会导致 `color` 被静默忽略。

## 坐标系说明

- 原点在根节点左上角
- X 向右递增，Y 向下递增（与 Unity UI 的 `anchoredPosition` 一致）
- Unity 端使用 `anchorMin/Max = (0,1)` + `pivot = (0,1)` 定位
- `anchoredPosition = (localX, -localY)`，其中 `localX/Y` = 子节点绝对坐标 - 父节点绝对坐标

## 完整示例

```json
{
  "name": "背景",
  "x": 0,
  "y": 0,
  "width": 1080,
  "height": 1920,
  "color": "#2C3E50",
  "children": [
    {
      "name": "标题",
      "x": 340,
      "y": 80,
      "width": 400,
      "height": 80,
      "text": "系统设置",
      "fontColor": "#FFFFFF",
      "fontSize": 48,
      "textAlign": "center"
    },
    {
      "name": "保存按钮区",
      "x": 440,
      "y": 300,
      "width": 200,
      "height": 90,
      "color": "#27AE60",
      "children": [
        {
          "name": "按钮文字",
          "x": 490,
          "y": 325,
          "width": 100,
          "height": 40,
          "text": "保存",
          "fontColor": "#FFFFFF",
          "fontSize": 32,
          "textAlign": "center"
        }
      ]
    }
  ]
}
```

> 示例里"保存按钮区"是一个绿色图片占位 + 内嵌文字，不是真 Button。要让它成为可点击按钮，在 Unity 里给该节点手动加 Button 组件。
