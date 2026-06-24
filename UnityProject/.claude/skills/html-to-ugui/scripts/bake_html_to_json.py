#!/usr/bin/env python3
"""
HTML to JSON UI Baker — Python 版坐标烘焙器

将符合 UI-DSL 规范的 HTML 文件烘焙为描述 JSON：每个标了 data-u-name 的节点产出
一个视觉占位描述（坐标 + 颜色 + 文字），供 Unity Editor UguiBaker 还原成文本/图片占位。
不读控件类型——baker 只按 text 有无建文本或图片，控件由人在 Unity 里手动转。

依赖安装：pip install playwright && playwright install chromium

用法：
  python bake_html_to_json.py input.html -o output.json
  python bake_html_to_json.py input.html --width 1920 --height 1080
  python bake_html_to_json.py input.html --stdout   # 输出到标准输出
"""

import argparse
import json
import sys
import os

def bake_html_to_json(html_content: str, width: int = 1920, height: int = 1080) -> dict:
    """使用 Playwright 渲染 HTML 并提取描述 JSON。"""
    try:
        from playwright.sync_api import sync_playwright
    except ImportError:
        print("错误: 需要安装 playwright。运行: pip install playwright && playwright install chromium", file=sys.stderr)
        sys.exit(1)

    # 构建完整 HTML 页面（内嵌烘焙脚本）
    full_html = f"""<!DOCTYPE html>
<html><head><meta charset="UTF-8">
<style>
body {{ margin: 0; padding: 0; }}
#canvas-sandbox {{ position: relative; width: {width}px; height: {height}px; }}
#canvas-sandbox * {{ box-sizing: border-box !important; }}
#canvas-sandbox [data-u-name] {{ min-width: 0; min-height: 0; }}
</style>
</head><body>
<div id="canvas-sandbox">{html_content}</div>
<script>
function rgb2hex(rgb) {{
    if (!rgb || rgb === 'rgba(0, 0, 0, 0)' || rgb === 'transparent') return '#FFFFFF00';
    const match = rgb.match(/^rgba?\\((\\d+),\\s*(\\d+),\\s*(\\d+)(?:,\\s*([\\d.]+))?\\)$/);
    if (!match) return '#FFFFFF';
    const r = ("0" + parseInt(match[1], 10).toString(16)).slice(-2);
    const g = ("0" + parseInt(match[2], 10).toString(16)).slice(-2);
    const b = ("0" + parseInt(match[3], 10).toString(16)).slice(-2);
    const a = match[4] ? ("0" + Math.round(parseFloat(match[4]) * 255).toString(16)).slice(-2) : "ff";
    return `#${{r}}${{g}}${{b}}${{a === 'ff' ? '' : a}}`;
}}

function traverseAndBake(element, rootRect) {{
    const uName = element.getAttribute('data-u-name');
    let nodeData = null;

    if (uName) {{
        const rect = element.getBoundingClientRect();
        const style = window.getComputedStyle(element);
        const relativeX = rect.left - rootRect.left;
        const relativeY = rect.top - rootRect.top;

        // 只取直系文本（子元素自己会被遍历到，避免父节点把子文字也算进来）
        let textContent = "";
        for (let i = 0; i < element.childNodes.length; i++) {{
            if (element.childNodes[i].nodeType === Node.TEXT_NODE) {{
                textContent += element.childNodes[i].textContent;
            }}
        }}

        let fontSize = 14;
        if (style.fontSize) fontSize = parseFloat(style.fontSize);
        let textAlign = style.textAlign || 'center';

        nodeData = {{
            name: uName,
            x: Math.round(relativeX),
            y: Math.round(relativeY),
            width: Math.round(rect.width),
            height: Math.round(rect.height),
            color: rgb2hex(style.backgroundColor),
            fontColor: rgb2hex(style.color),
            fontSize: Math.round(fontSize),
            textAlign: textAlign,
            text: textContent.trim(),
            children: []
        }};
    }}

    const childrenData = [];
    for (let i = 0; i < element.children.length; i++) {{
        const childResult = traverseAndBake(element.children[i], rootRect);
        if (childResult) childrenData.push(childResult);
    }}

    if (nodeData) {{
        nodeData.children = childrenData;
        return nodeData;
    }} else if (childrenData.length > 0) {{
        return childrenData.length === 1 ? childrenData[0] : {{
            name: "容器_" + Math.random().toString(36).substr(2, 5),
            x: 0, y: 0, width: 0, height: 0,
            color: "#FFFFFF00", fontColor: "#000000", fontSize: 14, textAlign: "center", text: "", children: childrenData
        }};
    }}
    return null;
}}

// 烘焙入口
const sandbox = document.getElementById('canvas-sandbox');
const rootElement = sandbox.querySelector('[data-u-name]');
if (!rootElement) {{
    throw new Error('未找到包含 data-u-name 的根节点');
}}
const rootRect = rootElement.getBoundingClientRect();
const result = traverseAndBake(rootElement, rootRect);
window.__BAKE_RESULT__ = result;
</script>
</body></html>"""

    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        page = browser.new_page(viewport={"width": width + 100, "height": height + 100})
        page.set_content(full_html, wait_until="networkidle")

        # 等待渲染完成
        page.wait_for_timeout(500)

        # 提取烘焙结果
        result = page.evaluate("window.__BAKE_RESULT__")

        browser.close()

    if result is None:
        raise ValueError("烘焙失败: 未找到有效节点，请检查 HTML 是否包含 data-u-name 属性")

    return result


def main():
    parser = argparse.ArgumentParser(description="HTML to JSON UI Baker — 将 UI-DSL HTML 烘焙为描述 JSON")
    parser.add_argument("input", help="输入 HTML 文件路径")
    parser.add_argument("-o", "--output", help="输出 JSON 文件路径（默认：同名 .json）")
    parser.add_argument("-w", "--width", type=int, default=1920, help="画布宽度（默认 1920）")
    parser.add_argument("-H", "--height", type=int, default=1080, help="画布高度（默认 1080）")
    parser.add_argument("--stdout", action="store_true", help="输出到标准输出而非文件")
    args = parser.parse_args()

    if not os.path.exists(args.input):
        print(f"错误: 文件不存在: {args.input}", file=sys.stderr)
        sys.exit(1)

    with open(args.input, "r", encoding="utf-8") as f:
        html_content = f.read()

    result = bake_html_to_json(html_content, args.width, args.height)
    json_str = json.dumps(result, ensure_ascii=False, indent=2)

    if args.stdout:
        print(json_str)
    else:
        output_path = args.output or os.path.splitext(args.input)[0] + ".json"
        with open(output_path, "w", encoding="utf-8") as f:
            f.write(json_str)
        print(f"烘焙完成: {output_path}")


if __name__ == "__main__":
    main()
