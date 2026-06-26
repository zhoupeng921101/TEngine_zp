---
name: editor-swap-mscript-on-prefab
description: Editor 工具中原地改写 prefab 资产里组件的脚本类型（如 Image→子类）时，正确保留字段/引用并不破坏嵌套结构的三个非显然陷阱：改 m_Script 后旧托管句柄失效；LoadPrefabContents 对象无稳定 localFileId 不能跨加载按 id 重定位；来自其它 prefab 源的组件（变体继承 / 嵌套实例）不能改脚本类型，须按「源非 null」整体跳过而非只挡变体。
type: rule
---

# 在 prefab 资产上原地改写组件脚本类型（保留字段与引用）

**rule**：要把 prefab 内某组件从基类换成子类（或同字段布局的另一脚本）而保留全部序列化字段+外部引用，唯一安全做法是 `SerializedObject` 改 `m_Script`，禁止「删旧组件 + AddComponent」。但有三个会导致**静默失败/字段丢失/结构破坏**的陷阱必须同时规避：

1. **改 m_Script 并 Apply 后，原 C# 组件句柄立即失效**。底层 native 对象被重解释为新类型，旧基类 wrapper 变 `null`。绝不能继续用旧句柄写子类新增字段（`new SerializedObject(oldHandle)` 抛 `ArgumentException: Object at index 0 is null`）。正确：先存 `var go = comp.gameObject`，Apply 后用 `go.GetComponent<新类型>()` 取变身后的组件再写新字段。GameObject 引用全程有效。

2. **`PrefabUtility.LoadPrefabContents` 出来的对象没有稳定 localFileId**。`AssetDatabase.TryGetGUIDAndLocalFileIdentifier` 对这些对象返回 `false / fileId=0`；`GetInstanceID()` 又随每次加载变化。所以「Dry-Run 时加载一次记下组件 id，Apply 时再加载一次按 id 重定位」必然失败，Apply 把每个目标都判成「定位不到」而空跑——且不报编译错，纯运行期静默漏改。正确：Scan 与 Apply 各自独立加载、各自重新判定每个组件（按节点/sprite-guid 等稳定属性），Dry-Run 列表仅供用户预览，不携带跨加载的组件句柄。

3. **来自其它 prefab 源的组件不能改脚本类型，跳过条件是「源非 null」而非「是变体」**。一个组件若 `PrefabUtility.GetCorrespondingObjectFromSource(comp) != null`，说明它来自另一个 prefab 资产——两种情况都触发：变体继承自 base 的组件，**以及非变体 prefab 内嵌的 prefab 实例（nested instance）的组件**。脚本类型不能作为实例 override 写入：`SaveAsPrefabAsset` 会破坏覆盖关系/产生孤儿。常见错写是只挡 `isVariant && 源非 null`，漏掉「普通 Window prefab 里嵌了 Widget 实例」这一支，照样改坏。正确：不看 isVariant，只要源非 null 一律跳过；该组件留待其源 prefab 自身被扫描时在源资产上转换（源转后，所有嵌套实例自动继承到新类型，无需碰实例）。

> **Why**：三者都不会编译报错、Apply 也不抛异常（前两个失败被吞成「0 处改写」，第三个则悄悄写坏嵌套实例），只能靠改写后回读 prefab YAML 才发现——前两个看 m_Script 没变、第三个看 PrefabInstance 的 m_Modifications 里被注入了 m_Script override / m_AddedComponents 非空。本工具首版前两坑全踩、二轮 review 抓出第三坑（守卫只挡变体）。GameObject 句柄稳定、native 重解释丢旧 wrapper、内存态 InstanceID 非持久 id、嵌套实例组件源非 null，都是 Unity Editor 的固有行为，换个改写脚本类型的工具照样复发。

> **How to apply**：写「改组件脚本类型」类 Editor 工具时，(a) swap 函数签名里先取 gameObject，apply 后 GetComponent 重取；(b) 不在两次 LoadPrefabContents 间用任何 id/句柄传递组件身份，把判定逻辑抽成单个方法让 Scan 和 Apply 共用、各自现场重判；(c) 判定里用 `GetCorrespondingObjectFromSource(comp) != null` 整体跳过「来自其它 prefab 源」的组件，不要带 isVariant 前置条件；(d) 自测必须回读改写后的 .prefab 文本：核对目标 m_Script guid 与新字段真的写入，且嵌套实例的 PrefabInstance 块 m_Modifications 无 m_Script override、m_AddedComponents/m_RemovedComponents 为空——不能只看「Apply 无报错 + summary 数字」，这类失败专门骗过运行期自检。测试场景必须含「非变体 prefab 内嵌 prefab 实例」这一支，否则漏测第三坑。
