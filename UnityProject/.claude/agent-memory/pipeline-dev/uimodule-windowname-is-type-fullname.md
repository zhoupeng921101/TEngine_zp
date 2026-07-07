---
name: uimodule-windowname-is-type-fullname
description: UIModule.GetTopWindow / WindowName 返回的是窗口类型的 type.FullName(含命名空间),与之比对窗名须用 typeof(T).FullName,不能用 nameof(T)(短名),否则永不命中
type: project
---

`UIModule.GetTopWindow()` / `GetTopWindow(int layer)` 返回 `UIPanelMono.WindowName`,该值由 `ShowUIImp` 以 `type.FullName` 赋入(如 `"GameLogic.UIMainMenuPanel"`,**含命名空间**)。拿它做窗名判定时,比对值必须用 `typeof(TargetWindow).FullName`,不能用 `nameof(TargetWindow)`(得到无命名空间的短名 `"UIMainMenuPanel"`)——否则字符串永不相等,判定恒 false。

**Why:** 短名与全名只差命名空间前缀,肉眼审读易漏;`nameof` 直觉上像「窗名」但语义是编译期短标识符,与运行时 `WindowName`(=FullName)不是一回事。曾据此写 HUD「顶层 UI 层窗 ∈ 白名单才显示」的显隐轮询,用 `nameof(UIMainMenuPanel)` 比对 → 白名单永不命中 → HUD 永久隐藏(编译无错、EditMode 测不到,纯 PlayMode 才暴露)。

**How to apply:**
- 任何与 `GetTopWindow` 返回值比对窗名的地方,用 `typeof(T).FullName`(可缓存进 `static readonly string` 避免每帧字符串工作)。
- 同理 `HasWindow<T>` / `CloseUI<T>` 内部亦以 `typeof(T).FullName` 作键(见 UIModule),这些泛型 API 自身正确;只有「手动拿 GetTopWindow 字符串去比」才需自己保证用全名。
- 排查此类「字符串判定恒 false」的显隐/分支 bug:先打印实际 `GetTopWindow` 返回值,确认是否带命名空间前缀。
