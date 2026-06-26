---
name: webgl-preload-into-pool-bypasses-sync-load
description: WebGL 禁同步资源加载时，把被同步 LoadGameObject/LoadAsset 取用的资源启动期异步预载并持有引用，运行时改 Object.Instantiate（实例化已加载预制，合法）/ 直接取缓存字节，零同步资源 LOAD——不靠「同步取用命中对象池」绕过
type: rule
---

rule: 客户端运行时代码**禁止**调用同步资源加载 API（`IResourceModule.LoadAsset<T>` / `LoadGameObject` 及其各重载、`IUIResourceLoader.LoadGameObject` 同步重载）。WebGL 上 YooAsset 的 `DWRFSLoadBundleOperation.InternalWaitForAsyncComplete` 对未完成 bundle 的同步加载**无条件** `Status=Failed` + LogError `"WebGL platform not support sync load method !"`。根治法：**启动期用异步 `LoadAssetAsync<T>(location)` 预载并持有资源引用**（预制 / 字体存进 `Dictionary<string, Object>`、配置字节存进 `Dictionary<string, byte[]>`），运行时取用走：

- **预制**：`UnityEngine.Object.Instantiate(缓存预制, parent)`——实例化已加载预制是合法操作（非同步资源 LOAD），同步、无弹出延迟，适配每格 widget / 每发特效 / 滚动项等高频复用路径。预制作模板**常驻、不 UnloadAsset**（归还引用计数会让其可被自动释放、缓存失效）。Instantiate 出的实例由各自 GameObject 生命周期销毁，不依赖 per-instance 引用计数（模板由预载持有常驻），无泄漏。
- **字体**：直接返回缓存的 `Font` 引用给 `Text.font`。
- **配置二进制（一次性读完即弃）**：`await LoadAssetAsync<TextAsset>` 后把 `.bytes` 拷进 `Dictionary<string,byte[]>`，**随即 `UnloadAsset(textAsset)`**；Luban `Tables` 的 loader 改为先读字节缓存、命中即 `new ByteBuf(cached)`，不触 bundle。

> 反对「保留同步取用 + 启动期预载进对象池让同步命中缓存」：ResourceModule 同步 `LoadGameObject` 命中对象池分支确实绕开 YooAsset 同步调用，但用户明确否决该路径——运行时不留任何同步资源加载调用点（即便它在 WebGL 上恰好安全），改为预载持有引用 + Object.Instantiate / 取缓存。`Object.Instantiate(已加载预制)` 允许（实例化），禁的是同步资源 LOAD。

关键配套：
- **预载持有引用，暴露取用接口**：预载器持有 `Dictionary<string, GameObject>` / `Dictionary<string, Font>`，暴露 `GetPrefab(location)` / `Instantiate(location, parent)`（= `Object.Instantiate(缓存预制, parent)`）/ `GetFont(location)`。调用点（`CreateWidgetByPath` / `CreateByPath` / 特效 Spawn / 字体工厂）改用这些接口，不再调同步资源加载 API。取用未命中记 Error 返 null（暴露漏预载，不回退同步加载）。
- **预载清单与生成物耦合**：被运行时取用的 location 清单（配置表名 / widget 预制名 / 字体名）须人维护成显式数组——Luban `Tables.cs`、widget 预制 / 字体由工具/美术产出，运行时无 tag 可枚举（本工程 AssetBundleCollector 各组 `AssetTags` 全空，`GetAssetInfos("PRELOAD")` 恒空、tag 预载机制形同休眠）。漏一项 → 运行时该资源取用拿不到缓存（记 Error），WebGL 上原会落回同步加载报错。
- **编辑器/单测回退用 `#if UNITY_EDITOR` 包裹**：EditMode 单测不跑启动预载流程，配置 loader 等需无预载直接读资源——该同步回退仅允许在 `#if UNITY_EDITOR` 块内（编辑器 / 测试合法），运行时（含 WebGL）`#else` 分支抛异常或返 null，保证运行时零同步加载。
- **预载时序**：必须在首个运行时消费者之前 `await` 完成。本工程把 `GameApp.StartGameLogic` 依赖配置的尾段（`WeightCfgConfigMgr.InitDynamicWeight`）拆进 `async UniTaskVoid PreloadThenStart()`：先 `await ConfigSystem.PreloadAsync()` + `UIPreloader.PreloadGameplayWidgetsAsync()` + `PreloadFontsAsync()` 再跑。
- **预载入闸 + 闸窗别一起挪进 await（血泪坑）**：若预载所在异步流程同时承载「启动闸窗（ConnectingWindow）的 show」与「玩法窗的开」，把闸窗 show 推到 `await 预载` 之后会破坏既有时序——网络登录与预载并行，登录+快照可能先于预载完成、此时闸窗尚未 show，登录回调里的 `CloseUI<闸窗>` 落空(no-op)，待预载完成闸窗才摆上且再无人关 → 闸窗永久盖死玩法窗。两条铁律：① 闸窗 show 留在**同步**启动路径、先于预载并行摆上（登录回调回来时闸窗必在栈，Close 才命中）；② 预载完成本身要作为**入口闸的一个信号**（如 `_preloadDone`，与 `_loginSucceeded`/`_snapshotApplied` 并列三者俱备才放行），否则登录+快照先到会在 widget / 字体预载驻留**前**开玩法窗 → 取用拿不到缓存（WebGL 原同步加载报错）。复位路径（清档软重启）同步复位该信号并重跑预载（幂等：字节缓存 / 预制 / 字体缓存按 key 去重），否则闸第三信号永缺、永不放行。闸窗 show 加 `_mainMenuOpened` 守卫防重入盖窗。
- **asmdef 不传递**：GameProto 用 `UniTask` 须在其 asmdef 直接加 UniTask 引用（GUID `f51ebe6a0ceec4240a699833d6309b23`），GameLogic 引了不代表 GameProto 能用。

Why: 误区是「ResourceModule 池缓存命中分支绕开 YooAsset 同步调用，故可保留同步取用」。该绕过技术上在 WebGL 安全，但用户明确要求运行时不留任何同步资源加载调用点（即便恰好安全的也不留），改为「预载持有引用 + Object.Instantiate / 取缓存」。理由：同步取用调用点是潜伏炸点——预载清单漏项 / 预载时序错位时它会落回 YooAsset 同步而报错，且非 WebGL 平台不暴露；彻底移除同步调用点把这类隐患在编译期/代码审查期就能 grep 验收（运行时路径 `\.LoadAsset\b`/`\.LoadGameObject\b` 零残留，仅 `#if UNITY_EDITOR` 包裹的测试回退允许）。`Object.Instantiate(已加载预制)` 是实例化、不触 bundle 加载，性能等价于原同步取用（高频复用路径不丢帧）。

How to apply: ① grep `\.LoadAsset\b`/`\.LoadAsset<`/`\.LoadGameObject\b`/`\.LoadGameObject\(`（运行时路径）列全部同步取用点。② 按资源性质分治：配置→字节缓存 + loader 改读缓存（编辑器同步回退用 `#if UNITY_EDITOR` 包裹）；预制（窗口内容 / 高频复用 / 特效）→ 预载持有引用 + 运行时 `Object.Instantiate(缓存预制, parent)`；字体→预载持有引用 + 取缓存赋给 `Text.font`。③ 调用点改用预载器的 `GetPrefab`/`Instantiate`/`GetFont`，移除对同步资源加载 API 的调用；取用未命中记 Error 返 null，不回退同步加载。④ 死代码 / 库代码（零调用）的同步 LOAD：用 `#if UNITY_EDITOR` 包裹同步分支、`#else` 返 null + Error，不留运行时同步残留。⑤ 维护显式预载清单，启动期 `await` 预载在首个消费者前。⑥ 验证：read_console 0 CS 错 + 跑全套 EditMode 测零回归（基线 540 passed / 0 failed / 18 Explicit-skip）+ grep 运行时路径同步加载零残留（仅列 `#if UNITY_EDITOR` 回退）；运行期 WebGL 行为交用户手测（编辑器 Play 不触发 WebGL 同步限制）。
