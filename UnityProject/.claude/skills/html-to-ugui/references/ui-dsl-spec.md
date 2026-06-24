# UI-DSL 规范（占位 + 描述版）

AI 生成 HTML 时遵守以下规范。每个要转成 Unity 节点的元素携带 `data-u-name`（中文描述），
烘焙器据此输出描述 JSON，再在 Unity 里还原成视觉占位：有文字建文本，无文字建图片。
不标控件类型——真按钮/滑条等控件由人在 Unity 里手动转。

## 1. 结构与基准分辨率

- **唯一根节点**：最外层元素必须声明 `data-u-name`，作为整个界面的描述。
- **基准分辨率**：根节点 `style` 中必须指定 `width: Wpx; height: Hpx;`，子节点不可超出此尺寸。

## 2. 必需属性

每个要转换为 Unity 节点的元素只需一个属性：

- `data-u-name="中文描述"` — 说明该节点是什么（如"标题""保存按钮区""头像"）。这句描述会成为生成节点的名字。

> 不再需要 `data-u-type`。元素有可见文字就会被建成文本，否则建成图片占位。
> 绑定用的命名前缀（`m_btn_` 等）是人在 Unity 里手动转控件时才加的后置步骤，不在 HTML 阶段标注。

## 3. 文本与图片

- **有文字的元素** → 文本节点：元素的直系文字会被提取为 `text`，并带上 `font-size`、`color`、`text-align`。
- **无文字的元素**（纯色块、图标位、容器） → 图片节点：取 `background-color` 作占位色。

举例：

| 元素 | 烘焙结果 |
|------|---------|
| `<h1 data-u-name="标题">系统设置</h1>` | 文本节点，text="系统设置" |
| `<div data-u-name="背景" style="background:#2c3e50">` | 图片节点，占位色 #2C3E50 |
| `<div data-u-name="头像位" style="background:#888">` | 图片节点，占位色 #888 |
| `<div data-u-name="保存按钮区" style="background:#27ae60">保存</div>` | 该节点有直系文字"保存" → 文本节点（绿色块由其在 Unity 里手动转 Button 时补） |

## 4. 布局规则

- 使用 CSS Flexbox 布局（`display: flex; flex-direction: column/row;`）
- 所有尺寸使用 `px` 单位
- 颜色使用 `#RRGGBB` 或 `#RRGGBBAA` 格式
- 禁止 CSS 动画（`transition`/`animation`），会导致坐标计算偏移

## 5. 完整示例

```html
<div data-u-name="设置窗背景" style="width: 1080px; height: 1920px; background-color: #2c3e50; display: flex; flex-direction: column; padding: 40px; gap: 30px;">
    <h1 data-u-name="标题" style="color: white; font-size: 48px; text-align: center;">系统设置</h1>

    <!-- 纯色块 → 图片占位；要变成按钮时在 Unity 里手动加 Button -->
    <div data-u-name="保存按钮区" style="width: 200px; height: 90px; background-color: #27ae60; display: flex; align-items: center; justify-content: center;">
        <span data-u-name="按钮文字" style="color: white; font-size: 32px;">保存</span>
    </div>

    <!-- 图标位 → 图片占位，先用纯色 -->
    <div data-u-name="头像位" style="width: 120px; height: 120px; background-color: #7f8c8d;"></div>
</div>
```
