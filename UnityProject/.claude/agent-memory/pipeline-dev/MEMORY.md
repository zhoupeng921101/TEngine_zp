# pipeline-dev 共享经验库索引

> 重型 dev 与轻型 dev 共享。每条经验为独立结构化 .md（frontmatter + rule + Why + How to apply）。本文件只做索引，准入按 `.claude/rules/conventions.md`§规则准入。

| 文件 | 适用场景 |
|------|---------|
| [unitymcp-run-tests-filter.md](unitymcp-run-tests-filter.md) | 用 UnityMCP `run_tests` 跑 EditMode 单测时，filter 失效 + 失败列表截断，如何稳妥判定目标用例真绿 |
| [csharp-no-overload-on-generic-constraint.md](csharp-no-overload-on-generic-constraint.md) | 想让同名泛型调用按 T 类型自动分流到两套实现(如经典/Mono UI 路径)时，为何不能加约束不同的重载、正确放宽公共接口 + 运行时分流的做法 |
| [frozen-ui-sync-to-async-authority.md](frozen-ui-sync-to-async-authority.md) | 把被冻结 UI 同步调用的方法(bool 门控)迁成服务端权威 + 异步 RPC 而不碰调用点:乐观提交 + 异步对账退还 / 注入钩子解耦网络 / 复用每秒轮询当脏标记重绘 / 用实例开关而非静态防单测污染 / 双计避免「本地不加即无 delta」 |
| [rebind-baseline-on-session-state-rebuild.md](rebind-baseline-on-session-state-rebuild.md) | 服务端权威投影的基线-diff 上报模式:单例对账器的基线必须在每次重建会话 state(开窗/重进)后重对齐到新 state,否则首次 diff 把(缓存值-旧基线)当玩法变更上报、被服务端真扣(体力重进归零根因) |
| [webgl-preload-into-pool-bypasses-sync-load.md](webgl-preload-into-pool-bypasses-sync-load.md) | WebGL 禁同步资源加载:运行时禁用一切同步 LoadAsset/LoadGameObject,被同步取用的资源启动期异步预载并持有引用,运行时改 Object.Instantiate(已加载预制)/ 取缓存字节零同步 LOAD;编辑器/单测回退用 #if UNITY_EDITOR 包裹;预载入闸时序坑 + asmdef 不传递 |
| [exclude-optimistic-spend-baseline-inline.md](exclude-optimistic-spend-baseline-inline.md) | 「服务端权威扣 + 客户端乐观扣显示」的单笔同步乐观扣,要从基线-diff 上报器排除必须在扣减同步瞬间就抬基线(非等 RPC 响应),否则落盘边界抢在响应前跑会双扣;与既有另两种双计避免机制(不本地加 / 累加器排除)的分工判据 |
| [editor-swap-mscript-on-prefab.md](editor-swap-mscript-on-prefab.md) | Editor 工具原地改写 prefab 组件脚本类型(Image→子类)保留字段/引用、不破坏嵌套结构的三个静默失败陷阱:改 m_Script 后旧句柄失效须经 GameObject 重取;LoadPrefabContents 对象无稳定 localFileId 不能跨加载按 id 重定位、须 Scan/Apply 各自重判;来自其它 prefab 源(变体继承 / 嵌套实例)的组件须按「源非 null」整体跳过而非只挡变体;自测必须回读 .prefab 文本(含 PrefabInstance 块未被注入 override) |
