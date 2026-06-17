# UI-DSL 规范（全控件版）

AI 生成 HTML 时必须严格遵守以下规范。所有节点需携带自定义数据属性（Data Attributes），用于导入 Unity 全自动生成 UGUI 界面。

## 1. 结构与基准分辨率

- **唯一根节点**：最外层必须声明 `data-u-type="div"` 和 `data-u-name="rootName"`
- **基准分辨率**：根节点 `style` 中必须指定 `width: Wpx; height: Hpx;`，子节点绝对不可超出此尺寸

## 2. 必需属性

所有需转换为 Unity 对象的节点必须包含：

- `data-u-name="nodeName"` — 唯一标识，**必须遵循第 2.1 节命名前缀规范**
- `data-u-type="nodeType"` — 组件类型，仅允许以下 8 种

### 2.1 命名前缀规范（TEngine ScriptGenerator 兼容）

`data-u-name` 必须使用**带下划线分隔**的 TEngine 格式：`m_{前缀}_{PascalCase语义名}`。
Baker 在 Unity 6 中生成 TMP 组件（legacy Text 已移除），前缀须与实际的 Unity 组件类型匹配：

| `data-u-type` | TEngine 前缀 | 示例 | Baker 生成的 Unity 组件 |
|---------------|-------------|------|------------------------|
| `div` | `m_` | `m_bg_Panel`、`m_tf_Container` | Image（背景色块）|
| `text` | `m_tmp_` | `m_tmp_Title`、`m_tmp_Desc` | TextMeshProUGUI |
| `image` | `m_img_` | `m_img_Icon`、`m_img_Avatar` | Image |
| `button` | `m_btn_` | `m_btn_Save`、`m_btn_Close` | Button + Image + TextMeshProUGUI |
| `input` | `m_tmpInput_` | `m_tmpInput_Account`、`m_tmpInput_Pwd` | TMP_InputField |
| `scroll` | `m_scroll_` | `m_scroll_ItemList`、`m_scroll_Chat` | ScrollRect + Image + Mask |
| `toggle` | `m_toggle_` | `m_toggle_Fullscreen`、`m_toggle_Sound` | Toggle + Image + TextMeshProUGUI |
| `slider` | `m_slider_` | `m_slider_Volume`、`m_slider_Brightness` | Slider |
| `dropdown` | `m_tmpDropdown_` | `m_tmpDropdown_Quality`、`m_tmpDropdown_Server` | TMP_Dropdown |

> **格式规则**：`m_{类型前缀}_{PascalCase语义名}`，例如 `m_btn_Save`、`m_tmp_Title`。
> 前缀下划线分隔必须——TEngine UIScriptGenerator 的 regex 匹配模式带下划线（如 `m_btn_`），不带下划线的 `m_btnSave` 不会匹配为 Button 绑定。
> `div` 做纯容器用 `m_tf_` 前缀（Transform），做背景色块用 `m_` 前缀（GameObject）。

## 3. 控件类型详解

### 基础排版

| 类型 | 语义 | 示例标签 |
|------|------|---------|
| `div` | 纯容器或背景色块 | `<div data-u-type="div" data-u-name="m_tf_Panel">` |
| `image` | 图片占位符 | `<div data-u-type="image" data-u-name="m_img_Icon">` |
| `text` | 纯文本显示，支持 `text-align` | `<span data-u-type="text" data-u-name="m_tmp_Title">` |

### 交互控件

| 类型 | 语义 | 特殊属性 |
|------|------|---------|
| `button` | 按钮 | 内部文本作为 Button Label |
| `input` | 输入框 | `placeholder` 属性作为占位文本 |
| `scroll` | 滚动列表 | `data-u-dir="v"`(垂直) 或 `"h"`(水平)，子节点挂载到 Content |

### 高级复合控件

| 类型 | 语义 | 特殊属性 |
|------|------|---------|
| `toggle` | 单选/复选框 | `data-u-checked="true"` 设置默认开启，内部文本作为 Label |
| `slider` | 滑动条 | `data-u-value="0.5"` 设置默认进度 (0.0~1.0) |
| `dropdown` | 下拉菜单 | **必须使用 `<select>` 标签**，内含 `<option>` 提取选项文本 |

## 4. 布局规则

- 使用 CSS Flexbox 布局（`display: flex; flex-direction: column/row;`）
- 所有尺寸使用 `px` 单位
- 颜色使用 `#RRGGBB` 或 `#RRGGBBAA` 格式
- 禁止使用 CSS 动画（`transition`/`animation`），会导致坐标计算偏移

## 5. 完整示例

```html
<div data-u-type="div" data-u-name="m_tf_SettingsWindow" style="width: 1920px; height: 1080px; background-color: #2c3e50; display: flex; flex-direction: column; padding: 40px; gap: 30px;">
    <h1 data-u-type="text" data-u-name="m_tmp_Title" style="color: white; font-size: 48px; text-align: center;">系统设置</h1>

    <!-- Toggle 开关 -->
    <div data-u-type="toggle" data-u-name="m_toggle_Fullscreen" data-u-checked="true" style="width: 200px; height: 40px; color: white; font-size: 24px;">
        全屏模式
    </div>

    <!-- Slider 滑动条 -->
    <div data-u-type="slider" data-u-name="m_slider_Volume" data-u-value="0.8" style="width: 400px; height: 30px; background-color: #7f8c8d;"></div>

    <!-- Dropdown 下拉菜单 -->
    <select data-u-type="dropdown" data-u-name="m_tmpDropdown_Quality" style="width: 300px; height: 50px; font-size: 24px; background-color: #ecf0f1;">
        <option>低画质 (Low)</option>
        <option>中画质 (Medium)</option>
        <option>高画质 (High)</option>
    </select>

    <!-- Scroll 滚动列表 -->
    <div data-u-type="scroll" data-u-name="m_scroll_ItemList" data-u-dir="v" style="width: 600px; height: 400px; background-color: #34495e;">
        <div data-u-type="div" data-u-name="m_tf_Item1" style="height: 80px; background-color: #3d566e; margin: 5px;">
            <span data-u-type="text" data-u-name="m_tmp_Item1" style="color: white; font-size: 24px;">项目一</span>
        </div>
    </div>

    <!-- Button 按钮 -->
    <button data-u-type="button" data-u-name="m_btn_Save" style="width: 200px; height: 60px; background-color: #27ae60; color: white; font-size: 24px;">
        保存设置
    </button>
</div>
```
