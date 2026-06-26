---
name: webgl-preload-into-pool-bypasses-sync-load
description: WebGL 禁同步 bundle 加载时，把被同步 LoadGameObject/LoadAsset 取用的资源在启动期异步预载进资源模块对象池（注册为 spawned），同步取用即命中池缓存、不触 YooAsset 同步加载——不必把整条同步实例化链改异步
type: rule
---

rule: TEngine `ResourceModule` 的同步 `LoadAsset<T>` / `LoadGameObject(location)` **先查对象池** `_assetPool.Spawn(GetCacheKey(location))`：命中即返回缓存资源（GameObject 路径就地 `AssetsReference.Instantiate`），**不调** `GetHandleSync`→`YooAssets.LoadAssetSync`。WebGL 上 YooAsset 的 `DWRFSLoadBundleOperation.InternalWaitForAsyncComplete` 对未完成的 bundle 同步加载**无条件** `Status=Failed` + LogError `"WebGL platform not support sync load method !"`。因此让同步取用在 WebGL 安全的根治法：**启动期用异步 `LoadAssetAsync<T>(location)` 把这些资源预载进池**——它 `Register(assetObject, spawned:true)`（spawnCount=1，`IsInUse`，永不被 AutoRelease 释放），之后同一 location 的同步取用恒命中池缓存。无需把 `CreateWidgetByType`→`CreateWidgetByPath`→`LoadGameObject` 整条同步链改 async（避免 async 级联铺爆高频实例化路径：每格 widget / 每发特效 / 滚动项）。

关键配套：
- **预载预制（作实例化模板）**：用非实例化的 `LoadAssetAsync<GameObject>(location)`，返回值丢弃、**不 UnloadAsset**——预制须留池作模板供后续同步 `LoadGameObject` 克隆；归还引用计数会让它可被自动释放、缓存失效。
- **预载配置二进制（一次性读完即弃）**：`await LoadAssetAsync<TextAsset>` 后把 `.bytes` 拷进 `Dictionary<string,byte[]>`，**随即 `UnloadAsset(textAsset)`**（已不需 TextAsset 存活，省内存）；Luban `Tables` 的 loader 改为先读该字节缓存、命中即 `new ByteBuf(cached)`，不触 bundle。
- **预载清单与生成物耦合**：被同步取用的 location 清单（配置表名 / widget 预制名）须人维护成显式数组——Luban `Tables.cs`、widget 预制由工具/美术产出，运行时无 tag 可枚举（本工程 AssetBundleCollector 各组 `AssetTags` 全空，`GetAssetInfos("PRELOAD")` 恒空、预载机制形同休眠）。漏一项 → WebGL 上该资源首次同步取用落回 YooAsset 同步而报错；非 WebGL 同步合法、不暴露。
- **预载时序**：必须在首个同步消费者之前 `await` 完成。本工程把 `GameApp.StartGameLogic` 依赖配置的尾段（`WeightCfgConfigMgr.InitDynamicWeight`）拆进 `async UniTaskVoid PreloadThenStart()`，先 `await ConfigSystem.PreloadAsync()` + `UIPreloader.PreloadGameplayWidgetsAsync()` 再跑。
- **预载入闸 + 闸窗别一起挪进 await（血泪坑）**：若预载所在异步流程同时承载「启动闸窗（ConnectingWindow）的 show」与「玩法窗的开」，把闸窗 show 推到 `await 预载` 之后会破坏既有时序——网络登录与预载并行，登录+快照可能先于预载完成、此时闸窗尚未 show，登录回调里的 `CloseUI<闸窗>` 落空(no-op)，待预载完成闸窗才摆上且再无人关 → 闸窗永久盖死玩法窗。两条铁律：① 闸窗 show 留在**同步**启动路径、先于预载并行摆上（登录回调回来时闸窗必在栈，Close 才命中）；② 预载完成本身要作为**入口闸的一个信号**（如 `_preloadDone`，与 `_loginSucceeded`/`_snapshotApplied` 并列三者俱备才放行），否则登录+快照先到会在 widget 预载驻留**前**开玩法窗 → WebGL widget 同步加载报错。复位路径（清档软重启）同步复位该信号并重跑预载（幂等：配置字节缓存去重、预制池命中），否则闸第三信号永缺、永不放行。闸窗 show 加 `_mainMenuOpened` 守卫防重入盖窗。
- **asmdef 不传递**：GameProto 用 `UniTask` 须在其 asmdef 直接加 UniTask 引用（GUID `f51ebe6a0ceec4240a699833d6309b23`），GameLogic 引了不代表 GameProto 能用。

Why: 误区是「WebGL 必须把所有同步加载改异步」（Explore 子代理初判即如此）。实际 ResourceModule 的池缓存命中分支**绕开** YooAsset 同步调用，故"预载驻留 + 保持同步取用"是合法且更省改动的路径——尤其对滚动项 / 每格 widget / 特效这类高频复用资源，逐用 async 会丢帧 / 弹出延迟。TEngine_block WebGL 包报 `WebGL platform not support sync load method !`，根因是 `ConfigSystem.LoadByteBuf` 同步 `LoadAsset<TextAsset>`（配置根，且 `GlobalConfigMgr.EnsureLoaded` try-catch 吞异常 → 全局配置静默退默认值）+ MergeOrderWindow OnCreate 内 `CreateWidgetByType` 同步 `LoadGameObject`。预载法落地后 540 EditMode 测零回归（EditMode 不预载、loader 走同步回退，与改前等价）。

How to apply: ① grep `\.LoadAsset<|\.LoadGameObject\(|\.LoadAsset\(`（排除 Async）列全部同步取用点，判每点是否 WebGL 可达（被 `ShowUIAsync` 开的窗 / 玩法渲染调到的才可达；零调用的库代码如 SuperScrollView Loop*View 可暂缓）。② 可达点按资源性质分治：配置→字节缓存 + loader 改读缓存；预制（窗口内容 / 高频复用）→ 预载进池 + 保持同步 `LoadGameObject` 克隆。③ 维护显式预载清单，启动期 `await` 预载在首个消费者前。④ 预载失败逐项记 Error 不静默吞默认、不阻断启动（缺项首次取用时落回同步暴露问题）。⑤ 验证：read_console 0 CS 错 + 跑全套 EditMode 测零回归；运行期 WebGL 行为交用户手测（编辑器 Play 不触发 WebGL 同步限制，单测覆盖不到预载时序）。
