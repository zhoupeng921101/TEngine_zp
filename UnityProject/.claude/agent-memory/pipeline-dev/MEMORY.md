# pipeline-dev 共享经验库索引

> 重型 dev 与轻型 dev 共享。每条经验为独立结构化 .md（frontmatter + rule + Why + How to apply）。本文件只做索引，准入按 `.claude/rules/conventions.md`§规则准入。

| 文件 | 适用场景 |
|------|---------|
| [unitymcp-run-tests-filter.md](unitymcp-run-tests-filter.md) | 用 UnityMCP `run_tests` 跑 EditMode 单测时，filter 失效 + 失败列表截断，如何稳妥判定目标用例真绿 |
| [csharp-no-overload-on-generic-constraint.md](csharp-no-overload-on-generic-constraint.md) | 想让同名泛型调用按 T 类型自动分流到两套实现(如经典/Mono UI 路径)时，为何不能加约束不同的重载、正确放宽公共接口 + 运行时分流的做法 |
| [frozen-ui-sync-to-async-authority.md](frozen-ui-sync-to-async-authority.md) | 把被冻结 UI 同步调用的方法(bool 门控)迁成服务端权威 + 异步 RPC 而不碰调用点:乐观提交 + 异步对账退还 / 注入钩子解耦网络 / 复用每秒轮询当脏标记重绘 / 用实例开关而非静态防单测污染 / 双计避免「本地不加即无 delta」 |
| [rebind-baseline-on-session-state-rebuild.md](rebind-baseline-on-session-state-rebuild.md) | 服务端权威投影的基线-diff 上报模式:单例对账器的基线必须在每次重建会话 state(开窗/重进)后重对齐到新 state,否则首次 diff 把(缓存值-旧基线)当玩法变更上报、被服务端真扣(体力重进归零根因) |
| [webgl-preload-into-pool-bypasses-sync-load.md](webgl-preload-into-pool-bypasses-sync-load.md) | WebGL 禁同步 bundle 加载时,把被同步 LoadGameObject/LoadAsset 取用的资源启动期异步预载进资源模块对象池(注册 spawned),同步取用即命中池缓存绕开 YooAsset 同步加载——不必把整条同步实例化链改异步(高频复用资源逐用 async 会丢帧) |
