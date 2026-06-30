# BlockBlast 生成器 golden trace 产物

服务端权威发牌迁移(第 1a 步·确定性测量)的产物目录。运行时中立纯文本,供后续 .NET 服务端 replay runner 回放比对客户端生成序列。

## 产物

`generator_golden_trace.txt` —— 由 EditMode 测试 `GeneratorDeterminismHarnessTests.GoldenTrace_WrittenToFixtures` 生成(运行 Test Runner 后出现)。UTF-8 无 BOM,`\n` 行尾。

## 生成方式

Unity 菜单 Window → General → Test Runner → EditMode,运行 `GeneratorDeterminismHarnessTests` 两个用例。

## 文件格式

头部 `key=value` 行,使 trace 自包含可重放:

| 字段 | 含义 |
|------|------|
| `rng` | 随机源实现(`System.Random`)。服务端回放须用同一实现与同一调用序。 |
| `seed` | `RandomSource.SetSeed` 的种子。 |
| `steps` | 脚本化对局步数。 |
| `activation_score` / `board_clear_score_threshold` / `early_game_block_score_threshold` | 影响调度分支的分数阈值。 |
| `first_hand` | 首发固定 3 形状 ID。 |
| `player_strategy` | 玩家落子确定性策略定义。 |
| `stuck_recovery` | 卡死(三块皆无处可放)续局策略。 |
| `weightcfg ...` | 全量 weightcfg(tier odds / 分数区间 / factor 区间),逐 tier 一行。 |

`# --- steps ---` 之后每行一手,`key=value` 空格分隔:

| 字段 | 含义 |
|------|------|
| `step` | 步序(从 0)。 |
| `slot` / `pos` | 本步落子的槽位与起点 `col,row`。 |
| `lines` / `clearedCells` | 本步消除行列数 / 被清格数。 |
| `forcedClear` | 本步前是否触发卡死续局(清全盘)。 |
| `refilled` | 本步是否补牌(走真实发牌路径)。 |
| `trio` | 当前 3 槽快照 `shapeId/colorIdx/algo`(algo `-` 表示无标签,null 表示空槽)。 |
| `score` / `combo` | 落子后分数 / 连击。 |
| `boardHash` | 棋盘 8 行位掩码逐行 2 位 16 进制拼接(运行时中立)。 |
| `dynamicWeight` / `preDynamicWeight` / `refillIndex` | 调度器跨手累积态。 |
| `bcInWindow` / `bcCooldown` | 清屏窗口 / 冷却态。 |
| `lastAlgo` / `lastTier` | 最近一次调度的算法枚举值 / tier id。 |

## 已知比对前提

逐位一致只在「同 RNG 实现 + 同枚举序 + 同 double 分支」下成立。跨运行时(IL2CPP/Mono ↔ .NET)的发散风险见交付报告「确定性隐患清单」(c) 类——回放前须先消解,否则此 trace 不能直接当跨端基线。

---

# 生成核心跨运行时确定性 trace(第 1b 步)

「只测生成核心」harness 的产物。与 1a 不同:全程不经 `BlockGameState`、不抽颜色,直接驱动
`BinaryBoard` + `BlockScoring` + `DynamicWeightDiff.OfferTrio` + `AddWeight`,以隔离生成器并缩小 .NET 编译闭包。
共享 harness `GenCoreDeterminismHarness`(在 `Assets/Editor/Tests/BlockBlast/`,Editor-only 不打包;
客户端 Unity 测试与 .NET 服务端实验项目按文件链接同一份源,故保持零 Unity/NUnit 依赖、纯逻辑)。

聚焦 Portable PRNG(生产路径),多 seed 扩测:种子组 `{1337, 1, 7, 42, 99, 12345}`(单一事实源
`GenCoreDeterminismHarness.Seeds`,两端均引用)。每 seed 一局,`steps=240`。System.Random 模式(c3)
已测完,仅保留一条同运行时可复现自检,不再落 trace。

## 产物(每 seed 一份 Portable trace)

| 文件 | 产出端 | RNG | 用途 |
|------|--------|-----|------|
| `gencore_trace_client_portable_seed<seed>.txt` | 客户端 Mono(Unity) | XorShift128Plus | 与服务端同 seed Portable trace 逐位 diff,量化 c1/c2 |
| `gencore_trace_server_portable_seed<seed>.txt` | 服务端 .NET | XorShift128Plus | 同上(服务端半) |

`<seed>` 取种子组各值(如 `seed1337`)。玩家策略纯棋盘判定、不消耗 RNG,故两端各自独立跑出同一对局,无需输入回放。

## 客户端侧生成方式

Unity 菜单 Window → General → Test Runner → EditMode,运行 `GenCoreDeterminismHarnessTests`:
- `GenCoreTrace_SameRuntime_IsReproducible_Portable`(逐 seed 参数化):每 seed 同运行时两遍逐字符一致自检。
- `GenCoreTrace_SameRuntime_IsReproducible_SystemRandom`:System.Random 同运行时可复现自检(单 seed)。
- `GenCoreTrace_PortableTracesWrittenToFixtures`:逐 seed 把客户端 Portable trace 落到本目录。

## 服务端侧生成方式

`dotnet run` 服务端实验项目 `Fantasy/experiments/BlockBlastGenCore/`(链接同一份 harness,`RunAllSeeds()`),
逐 seed 落服务端 trace 到其 `bin/.../traces/`。

## 头部字段(gen-core trace)

`rng`(`System.Random` / `XorShift128Plus`)、`seed`、`steps`、各分数阈值、`player_strategy`、`stuck_recovery`、
`first_trio_source`(首手亦走 `OfferTrio`)、全量 `weightcfg`。每行字段:`step/slot/pos/lines/clearedCells/forcedClear/refilled/`
`trio`(仅 shapeId,空槽 null)/`algo`/`tier`/`score`/`combo`/`boardHash`/ 完整调度态向量
(`dynamicWeight`/`preDynamicWeight`/`refillIndex`/`bcInWindow`/`bcCooldown`/`lastAlgo`/`lastTier`)。
