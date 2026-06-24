---
name: html-to-ugui
description: "HTML原型转Unity UGUI视觉占位管线。通过AI生成带中文描述的HTML，烘焙为描述JSON，再导入Unity生成视觉占位节点树（有文字建文本、无文字建图片占位）。真按钮/滑条由人手动转。触发场景：(1) 需要快速生成Unity UGUI界面视觉布局 (2) 用自然语言描述UI需求并自动生成 (3) 创建UIWindow/面板的初始视觉占位 (4) 批量生成表单、设置、列表等标准界面的占位布局"
---

# HTML to UGUI 视觉占位生成管线

你是一个 UI 原型开发专家。根据用户需求生成 HTML UI 布局，导入 Unity 还原成 UGUI 视觉占位节点树。

把自然语言 UI 需求 → 带中文描述的 HTML → 描述 JSON → Unity UGUI 视觉占位节点树。

> **这条线只产视觉占位，不建控件**：有文字的节点建文本（Text，用方正 GBK 字体渲染中文），其余建图片（Image 占位色）。
> 哪个区域要变成真按钮/滑条，由人在 Unity 里手动转。本项目最终 UI 用图集精灵换皮，自动建的默认控件是丢弃品，故不建。

## 工作流（三步）

### Step 1: 生成 HTML

读取 [references/ui-dsl-spec.md](references/ui-dsl-spec.md) 获取完整规范，然后根据用户需求生成 HTML。

核心规则：
- 唯一根节点：带 `data-u-name`（中文描述）+ 明确 `width/height`。
- 每个要转成 Unity 节点的元素只需 `data-u-name="中文描述"`（如"标题""保存按钮区""头像位"）。
- 不标控件类型：有可见文字的元素会建成文本，无文字的建成图片占位。
- 使用 CSS Flexbox 布局，不超出根节点尺寸。

### Step 2: 烘焙 HTML → JSON

运行烘焙脚本将 HTML 转换为描述 JSON：

```bash
python scripts/bake_html_to_json.py input.html -o output.json -w 1080 -h 1920
```

加 `--stdout` 可输出到标准输出。JSON 结构参见 [references/json-schema.md](references/json-schema.md)。

### Step 3: 导入 Unity

将 JSON 粘贴到 Unity Editor 的 UGUI Baker 窗口：
1. 打开 `Tools > UI Architecture > UGUI Baker (JSON)`
2. 选择目标 Canvas，使用"粘贴 JSON"模式
3. 粘贴 JSON，点击"执行烘焙生成"

生成后由人手动把需要交互的图片占位转成真按钮/滑条等控件。

## 快速参考

### 分辨率预设

| 预设 | 尺寸 |
|------|------|
| Mobile 竖屏 | 1080x1920 |
| PC 横屏 | 1920x1080 |
| Pad 横屏 | 2048x1536 |

### 节点速查

| 元素 | 烘焙结果 |
|------|---------|
| 有直系文字的元素 | 文本节点（Text，渲染中文） |
| 无文字的色块/容器/图标位 | 图片节点（Image 占位色） |

## 典型使用场景

**场景1：快速生成设置界面布局**
> 用户："帮我做一个游戏设置界面，包含标题、音量区、保存按钮区"

→ 读取 ui-dsl-spec.md → 生成带描述的 HTML → 运行 bake 脚本 → 输出 JSON → 粘到 Unity 烤出视觉占位 → 手动把"保存按钮区"转成 Button。

**场景2：生成登录窗口布局**
> 用户："做一个登录界面，有账号区、密码区、登录按钮区"

→ 同上流程。账号/密码输入框先以占位图呈现，在 Unity 里手动转成 InputField。
