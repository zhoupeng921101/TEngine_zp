---
name: MergeOrderWindow BindComponent prefab 编辑器构建经验
description: MCP 直接在 Unity 编辑器里用 execute_code 搭 UGUI prefab 并生成 BindComponent 绑定的关键约束
type: project-feedback
---

## Rule

### 1. MCP 编辑器构建流程（execute_code）

用 execute_code 直接在场景内逐批创建节点，最后用 `PrefabUtility.SaveAsPrefabAsset` 覆盖磁盘 prefab，再 `AssetDatabase.Refresh()`。比手写 YAML 稳定：组件引用自动正确，m_Children 自动维护。

关键步骤顺序：
1. 创建根 GameObject（加 Canvas/CanvasScaler/GraphicRaycaster）
2. 创建 Content 子节点（750×1334，center 锚点，localScale=1.44）
3. Content 下批量创建所有 m_ 控件节点（Image/Text/Button）
4. 创建 6 个空锚点动态层（不加 m_ 前缀，不绑定）
5. `PrefabUtility.SaveAsPrefabAsset` 保存
6. 打开 prefab stage → 选中根节点 → `GenerateUIComponentScript()` 挂 UIBindComponent + 填索引
7. 在 prefab stage 内 `GenerateCSharpScript(includeListener:false, isAutoGenerate:true, ...)` 生成 _Gen.g.cs（不带 listener，避免 partial void 编译问题）
8. 手动补写 _Gen.g.cs（含完整 namespace + sealed partial class 声明），替换生成器输出

### 2. sealed partial class 编译约束

`_Gen.g.cs` 要求 `partial class`，而现有窗口代码可能声明为 `sealed class`（无 partial）。两个 partial 定义必须命名空间完全一致。修复：给现有 .cs 加 `partial` 修饰符（`public sealed partial class`），不改其他逻辑。

### 3. BindComponent 生成器的 includeListener 行为

`GenerateCSharpScript(includeListener:false)` 仍然输出 `AddListener(OnClick_*Btn)` 调用，但不输出 partial void 声明——这会导致编译错误（引用了不存在的方法）。对于已有业务逻辑的窗口，最干净的解法是：

- 生成器只作参考，最终手写 _Gen.g.cs 内容
- _Gen.g.cs 只包含：字段声明 + ScriptGenerator() 中的 BindComponent 索引绑定
- 不在 _Gen.g.cs 里写 AddListener（由 dev 在现有 OnCreate 中手动绑定，或迁移时重写）

### 4. UIRaw/Atlas 散图 sprite 模式（game_main 等）

`Assets/AssetRaw/UIRaw/Atlas/game_main/` 下的散图收集规则：`AddressByFileName`（文件名即 location）+ `PackDirectory`（同目录打一个 bundle）。

正确模式：`m_Sprite: {fileID: 0}`（prefab 里空引用），运行时 `image.SetSprite("icon_coin")` 按文件名寻址。不可以把散图 GUID 烤进 prefab 的 m_Sprite——静态引用会跨 bundle 依赖，导致 bundle 重复打包或运行时解析失败。

### 5. Content 坐标系不变量

- Content 面板：750×1334，center 锚点&pivot，localScale=1.44（设计坐标 → 参考分辨率 1080×1920 的比例）
- 设计坐标转 anchoredPosition：`(cx - 375, 667 - cy)`（左上原点 Y 向下 → 中心原点 Y 向上）
- 棋盘框中心 design (375, 636)，尺寸 688×688（BoardPixels=672，+16 外框）
- 候选槽中心 design (375, 1100)，尺寸 750×200
- 6 个动态层（BoardLayer/ElemLayer/GhostLayer/SlotLayer/OrderLayer/SynthLayer）：anchoredPosition=(0,0)，sizeDelta=(0,0)，不加 m_ 前缀，代码运行时填内容

## Why

- YAML 手写在 sprite fileID / m_Children 引用上容易出错；MCP execute_code 方式自动维护引用完整性。
- partial void 无实现体会触发 CS8795（C# 要求 accessible partial method 必须有实现），而已有业务类不可能总是 partial。
- 烤进散图 GUID 的 prefab 在 Editor 预览看起来正常，但 AssetBundle 打包时产生跨 bundle 依赖，运行时按 location 加载的 YooAsset 无法找到静态引用，造成资产丢失。

## How to apply

1. 搭 prefab 时所有 Image 的 sprite 一律空（fileID:0），在节点清单中标注"运行时 SetSprite/SetSubSprite"。
2. 生成绑定骨架时先用生成器生成草稿，再手动补写完整文件头（namespace/sealed partial class）并去掉 AddListener 行。
3. 检查现有窗口 .cs 是否带 `partial`，没有则加上——这是无逻辑改动，只改类声明。
