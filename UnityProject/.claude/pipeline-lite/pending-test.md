# 待测清单(pipeline-lite)

> dev 交付物:lite 管线 dev 实现后把待测事项写入本文件,用户照单手测。dev 不在 Unity 内编译/运行/测试,全部验证(含编译能否通过)在此登记、由用户执行。
> 维护:每条测完由用户勾除或删行;一轮任务全部通过即清空该轮条目(工作状态层,不留过期条目)。条目格式「测什么 / 怎么测 / 预期结果」,面向非工程用户。

## 待测条目

### [ ] T1 · CLAUDE.md「项目结构」节生效

- 测什么:新增的「项目结构」节是否让 AI 收集信息时把服务端 Fantasy 纳入检索范围。
- 怎么测:开一个新会话,提一个跨端需求(如「订单/货币相关逻辑在哪实现」),观察 AI 是否主动去 `Fantasy/` 服务端工程检索,而非只搜客户端 `Assets/`。
- 预期结果:涉及玩家数据/联网/跨端的需求,AI 检索覆盖 `Fantasy/` 工程;纯客户端表现/UI 需求则不无谓去翻服务端。

### [ ] T2 · BlockBlast 生成器确定性 harness 编译通过

- 测什么:新增测试文件 `Assets/Editor/Tests/BlockBlast/GeneratorDeterminismHarnessTests.cs` + 对 `DynamicWeightDiff.cs` 加的内部只读 getter,能否随工程编译通过(无报错)。
- 怎么测:Unity 编辑器内等待脚本编译完成,看 Console 是否有红色 error;或打开 Window → General → Test Runner(EditMode 标签)能否正常列出 `GeneratorDeterminismHarnessTests` 两个用例(列得出即说明 BlockBlast.Tests 程序集编译成功)。
- 预期结果:无编译错误;Test Runner 的 EditMode 列表里出现 `GeneratorDeterminismHarnessTests`,含 `GoldenTrace_SameRuntime_IsReproducible` 与 `GoldenTrace_WrittenToFixtures` 两条。

### [ ] T3 · 同运行时确定性成立(核心 happy path)

- 测什么:同 seed + 同脚本化对局跑两遍,逐字符一致(发牌路径在同一运行时内确定性)。
- 怎么测:Test Runner → EditMode,单独运行 `GeneratorDeterminismHarnessTests.GoldenTrace_SameRuntime_IsReproducible`。
- 预期结果:用例 PASS(绿)。若 FAIL,失败信息会报「首个发散在第 N 行」——把该提示原样回报,说明发牌路径仍有未被基线复位的全局态或非确定性来源(本步即暴露隐患,属有效产出,不是 harness bug)。

### [ ] T4 · golden trace 落盘且人类可读(产物验收)

- 测什么:运行后是否在 `Assets/Editor/Tests/BlockBlast/Fixtures/` 生成 `generator_golden_trace.txt`,且自包含(含 seed / weightcfg / 输入脚本定义)。
- 怎么测:Test Runner 运行 `GeneratorDeterminismHarnessTests.GoldenTrace_WrittenToFixtures`(绿即通过);随后用文本编辑器打开 `Assets/Editor/Tests/BlockBlast/Fixtures/generator_golden_trace.txt`,看头部是否有 `seed=` / `weightcfg ...` / `player_strategy=` 等行,steps 段每行是否含 `trio=` / `boardHash=` / `dynamicWeight=` 等字段。
- 预期结果:文件存在、非空;头部含 seed 与全量 weightcfg、玩家策略定义;步进段逐手记录 trio/棋盘哈希/分数/生成器状态向量。格式说明见同目录 `README.md`。

### [ ] T5 · 异常路径:卡死续局不破坏确定性

- 测什么:脚本化对局中触发「三块皆无处可放」时,harness 用确定性清全盘续局,两遍 trace 仍一致(异常分支同样确定)。
- 怎么测:此路径已并入 T3 的两遍比对(脚本跑 240 步,棋盘填满后会触发卡死续局)。打开 T4 生成的 trace,在 steps 段搜 `forcedClear=1` 的行确认该分支确有被走到。
- 预期结果:trace 中存在 `forcedClear=1` 的步(说明异常分支被覆盖);且 T3 两遍一致(异常分支未引入非确定性)。若全程无 `forcedClear=1`,属正常(取决于落子策略是否填满盘),不算失败。

---

## 待测条目(第 1b 步:生成核心 .NET 移植 + 跨运行时确定性测量)

> 服务端半(.NET 编译 + 运行 + 两遍一致)dev 已自检通过(见交付报告),以下为用户在 Unity 侧需手测的客户端半 + 跨端 diff 入口。

### [ ] T6 · 共享 PRNG + gen-core harness 随客户端工程编译通过

- 测什么:新增的双端共享源 `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/PortableRng.cs`、`GenCoreDeterminismHarness.cs`、改动的 `RandomSource.cs`,以及新测试 `Assets/Editor/Tests/BlockBlast/GenCoreDeterminismHarnessTests.cs`,能否随工程编译通过(无报错)。
- 怎么测:Unity 编辑器内等脚本编译完成,看 Console 有无红色 error;或打开 Window → General → Test Runner(EditMode)能否列出 `GenCoreDeterminismHarnessTests`(列得出即说明编译成功)。
- 预期结果:无编译错误;EditMode 列表出现 `GenCoreDeterminismHarnessTests`,含 `..._IsReproducible_SystemRandom`、`..._IsReproducible_Portable`、`GenCoreTrace_WrittenToFixtures` 三条。既有 BlockBlast 测试(用 `RandomSource.SetSeed`)仍正常列出、不受 RandomSource 改动影响。

### [ ] T7 · 客户端 Mono 同运行时确定性成立(两种 RNG)

- 测什么:gen-core harness 在客户端 Mono 上,同 seed 两遍逐字符一致(System.Random / Portable 各一)。
- 怎么测:Test Runner → EditMode,运行 `GenCoreDeterminismHarnessTests` 的 `..._IsReproducible_SystemRandom` 与 `..._IsReproducible_Portable` 两个用例。
- 预期结果:两条均 PASS(绿)。若 FAIL,失败信息报「首个发散在第 N 行」,原样回报(说明客户端侧仍有未复位的全局态)。

### [ ] T8 · 产出两份客户端 Mono trace(供跨端 diff)

- 测什么:运行后在 `Assets/Editor/Tests/BlockBlast/Fixtures/` 生成 `gencore_trace_client_systemrandom.txt` 与 `gencore_trace_client_portable.txt`,各自包含且人类可读。
- 怎么测:Test Runner 运行 `GenCoreDeterminismHarnessTests.GenCoreTrace_WrittenToFixtures`(绿即通过);用文本编辑器打开两份文件,看头部有 `rng=` / `seed=1337` / `weightcfg ...`,steps 段每行含 `trio=` / `algo=` / `boardHash=` / `dynamicWeight=` 等字段。格式说明见同目录 `README.md` 的「生成核心跨运行时确定性 trace」节。
- 预期结果:两文件存在、非空;`systemrandom` 那份头部 `rng=System.Random`,`portable` 那份 `rng=XorShift128Plus`,两文件 step 段内容互不相同(RNG 不同)。

### [ ] T9 ·(可选,如本机装 .NET 8 SDK)复跑服务端 .NET 实验

- 测什么:服务端实验项目在用户机也能编译运行、两遍一致(复现 dev 自检)。
- 怎么测:命令行 `cd D:\work\TEngine_block\Fantasy\experiments\BlockBlastGenCore` 后 `dotnet run -c Debug`。
- 预期结果:输出 `[SystemRandom] 两遍逐字符一致 ✓` 与 `[Portable] 两遍逐字符一致 ✓`,末行 `[OK]`;两份服务端 trace 落到 `bin/Debug/net8.0/traces/`。
- 跨端 diff 入口(boss 做,非用户必做):把 T8 的客户端 trace 与服务端同名 trace(`...client_systemrandom` vs `...server_systemrandom`、`...client_portable` vs `...server_portable`)逐字符 diff——System.Random 对若发散即实测确认 c3;Portable 对若发散即定位 c1/c2 具体步。

---

## 待测条目(封验证余量:c1 排序加固 + 多 seed 扩测 + harness 移出生产模块)

> 本轮取代上面 T6–T8 的客户端半:harness 已移出生产模块、trace 文件名改为带 seed、测试改多 seed 参数化。
> 上轮单 seed 客户端 trace(`gencore_trace_client_portable.txt` / `..._systemrandom.txt`)已删除,由本轮带 seed 的新文件取代。
> 改动:c1 排序加固改变了 Portable 发牌输出(排序后 RNG 索引落到不同元素,属预期);harness 移到 Editor-only 不打包。

### [ ] T10 · 改动后客户端工程编译通过

- 测什么:c1 排序改动(`BlockAlgorithms.cs`)、harness 移到 `Assets/Editor/Tests/BlockBlast/GenCoreDeterminismHarness.cs`、测试 `GenCoreDeterminismHarnessTests.cs` 改写,能否随工程编译通过。
- 怎么测:Unity 编辑器内等脚本编译完成,看 Console 有无红色 error;打开 Window → General → Test Runner(EditMode)能否列出 `GenCoreDeterminismHarnessTests`。
- 预期结果:无编译错误;EditMode 列表出现 `GenCoreDeterminismHarnessTests`。生产模块 `Module/BlockBlast/` 下已无 `GenCoreDeterminismHarness.cs`(它现在在 Editor/Tests 下、不打进游戏包)。

### [ ] T11 · 既有 BlockBlast 单测仍绿(c1 排序未误伤其他分支)

- 测什么:c1 排序只影响 `BoardClearGreedyTrio` 清屏窗口路径的选块顺序,其余生成分支与既有单测不应受影响。
- 怎么测:Test Runner → EditMode,运行整个 `BlockBlast.Tests` 程序集(或至少 `DynamicWeightDiffTests`、`GeneratorDeterminismHarnessTests`、`BlockGameStateTests`)。
- 预期结果:全绿。若某用例因锚定了「旧的具体发牌序列」而红(典型是 golden trace / 序列断言类),把失败用例名与失败信息原样回报——本轮 c1 排序会改变 Portable 清屏路径的发牌输出,属预期变化,需 boss 判定是否更新基线,不是 bug。

### [ ] T12 · 多 seed 同运行时确定性成立(核心 happy path)

- 测什么:Portable(生产路径)对种子组 `{1337, 1, 7, 42, 99, 12345}` 逐 seed 各跑两遍,每 seed 两遍逐字符一致。
- 怎么测:Test Runner → EditMode,运行 `GenCoreDeterminismHarnessTests.GenCoreTrace_SameRuntime_IsReproducible_Portable`(参数化用例,展开后每 seed 一条);另运行 `..._IsReproducible_SystemRandom`(单 seed,保留的一条 c3 自检)。
- 预期结果:Portable 各 seed 全 PASS(绿)+ SystemRandom 一条 PASS。若某 seed FAIL,失败信息报「seed=X 首个发散在第 N 行」,原样回报。

### [ ] T13 · 逐 seed 产出客户端 Portable trace(供跨端 diff)

- 测什么:运行后在 `Assets/Editor/Tests/BlockBlast/Fixtures/` 为每个 seed 生成一份客户端 Portable trace,文件名带 seed。
- 怎么测:Test Runner 运行 `GenCoreDeterminismHarnessTests.GenCoreTrace_PortableTracesWrittenToFixtures`(绿即通过);到 Fixtures 目录确认 6 个文件存在,任挑一份用文本编辑器打开,看头部有 `rng=XorShift128Plus` / `seed=<对应值>` / `weightcfg ...`,steps 段每行含 `trio=` / `algo=` / `boardHash=` 等字段。
- 预期结果:生成以下 6 份(各非空、`rng=XorShift128Plus`、头部 seed 与文件名一致):
  - `gencore_trace_client_portable_seed1337.txt`
  - `gencore_trace_client_portable_seed1.txt`
  - `gencore_trace_client_portable_seed7.txt`
  - `gencore_trace_client_portable_seed42.txt`
  - `gencore_trace_client_portable_seed99.txt`
  - `gencore_trace_client_portable_seed12345.txt`
- 跨端 diff 入口(boss 做):各 seed 客户端 trace 与服务端同 seed Portable trace 逐字符 diff;共享入口 `GenCoreDeterminismHarness.RunAllSeeds()` 两端跑同一组 seed。

### [ ] T14 · 异常路径:卡死续局 / 空盘首发不破坏确定性

- 测什么:多 seed 对局中触发的异常分支(三块皆无处可放→确定性清全盘续局 `forcedClear=1`;空盘首发走 `OfferTrio`)在 Portable 下两遍仍一致。
- 怎么测:此路径已并入 T12 的逐 seed 两遍比对(每局 240 步)。打开 T13 任一 trace,在 steps 段搜 `forcedClear=1` 确认该分支有被走到。
- 预期结果:T12 各 seed 两遍一致;trace 中通常存在 `forcedClear=1` 的步(异常分支被覆盖)。若某 seed 全程无 `forcedClear=1` 属正常(取决于该 seed 的落子是否填满盘),不算失败。

---

## 待测条目(M1·核心段:生成核心去单例化为逐局实例 + 摆脱 Unity 依赖)

> 本轮把生成路径里的进程级全局可变状态收成逐局实例(`DynamicWeightDiff` 不再是单例,随机源 `IRandomSource` 显式贯穿 `BlockAlgorithms`),并去掉生成核心的 2 处 Unity 依赖。客户端发牌行为应零回归——以确定性 harness 同 trace + 既有单测全绿为安全网。

### [ ] T15 · 改动后客户端工程编译通过

- 测什么:本轮所有改动(`BlockAlgorithms.cs` / `DynamicWeightDiff.cs` / `RandomSource.cs` / `OfferOverrides.cs` / `Persistence.cs` / 新增 `PlayerPrefsProvider.cs` / `BlockGameState.cs` / `WeightCfgConfigMgr.cs` / `MergeOrderWindow.cs` / `GameApp.cs`,以及改写的 harness 与多个测试文件)能否随工程编译通过。
- 怎么测:Unity 编辑器内等脚本编译完成,看 Console 有无红色 error;打开 Window → General → Test Runner(EditMode)能否正常列出 `BlockBlast.Tests` 的用例(列得出即说明热更区 + 测试程序集都编译成功)。
- 预期结果:无编译错误;EditMode 列表正常列出 `DynamicWeightDiffTests` / `AlgorithmsTests` / `GeneratorDeterminismHarnessTests` / `GenCoreDeterminismHarnessTests` / `BlockGameStateTests` / `MergeOrderTests` / `WeightCfgLubanTests` / `BlockSkinStateTests` 等。新增文件 `Module/BlockBlast/PlayerPrefsProvider.cs` 被识别(其 `.meta` 已随附)。

### [ ] T16 · 既有 BlockBlast 单测全绿(去单例化未改变行为)

- 测什么:整个 `BlockBlast.Tests` 程序集在去单例化后仍全部通过——这是「客户端发牌行为零回归」的主安全网。
- 怎么测:Test Runner → EditMode,运行整个 `BlockBlast.Tests` 程序集(Run All,或至少选中上面 T15 列出的那批)。
- 预期结果:全绿。若某用例红,把用例名 + 失败信息原样回报。重点关注:`DynamicWeightDiffTests`(已改为 new 实例 API,含新增的 `Load_ReadsLegacyJsonFormat` 旧 JSON 兼容用例 + `NoPersistence_SaveLoadAreNoops` 服务端无持久化用例)、`AlgorithmsTests`(已改为显式传随机源)。

### [ ] T17 · 同运行时确定性 harness 两遍一致(去单例化后仍确定)

- 测什么:两套确定性 harness 切到实例化 API 后,同 seed 两遍仍逐字符一致(发牌路径无进程级残留态,逐局实例各跑各的)。
- 怎么测:Test Runner → EditMode,运行:
  - `GenCoreDeterminismHarnessTests.GenCoreTrace_SameRuntime_IsReproducible_Portable`(多 seed 参数化)+ `..._IsReproducible_SystemRandom`;
  - `GeneratorDeterminismHarnessTests.GoldenTrace_SameRuntime_IsReproducible`(经 `BlockGameState.RefillPieces` 客户端路径,已改为注入固定随机源的逐局调度器)。
- 预期结果:全部 PASS(绿)。若 FAIL,失败信息报「首个发散在第 N 行」/「seed=X ...」,原样回报。

### [ ] T18 · 本地调度态跨会话持久化零回归(含旧档兼容)

- 测什么:`DynamicWeightDiff` 的调度态(dynamicWeight/preDynamicWeight)落盘/读盘改用中立文本编码后,客户端仍能正常持久化;且旧客户端用 Unity JSON 落的存档能被新代码读出(不被清零)。
- 怎么测:① 自动覆盖——T16 里的 `DynamicWeightDiffTests.SaveAndLoad_RoundTrips`(新写新读)与 `Load_ReadsLegacyJsonFormat`(读旧 JSON 格式)绿即通过此项主体。② 端到端可选手验:正常进一局玩法(经 MergeOrderWindow),退出再进,确认 HUD/玩法表现与改动前一致(注意:进盘本就会 `Reset()` 调度权重,故跨会话表现差异本应不可见)。
- 预期结果:两条单测绿;端到端玩法表现与改动前无可见差异。

### [ ] T19 · 实机进玩法窗发牌正常(客户端调用方接线无误)

- 测什么:客户端调用方全部改到逐局实例用法后(`BlockGameState.Dynamic` 惰性创建 + `WeightCfgConfigMgr.InitDynamicWeight` 灌表 + `MergeOrderWindow` 走 `_state.Dynamic`),实机进玩法窗能正常发牌、落子、消除、补牌、计分。
- 怎么测:Play 模式正常登录进主菜单 → 开始游戏进玩法窗,玩几手:拖块落子、凑行/列消除、三块用完后自动补 3 块、分数随之增长;再退出玩法窗重进一次。
- 预期结果:发牌/落子/消除/补牌/计分全部正常,无报错(Console 无红);补出的 3 块无重复图形(去重逻辑生效);整体手感与改动前一致。重点关注:首次进窗时 `Persistence.Provider` 是否已被 `GameApp.StartGameLogic` 注册为 PlayerPrefs(若本地持久化失效——如重进后皮肤/进度丢——回报,可能是注册时序问题)。

---

## 待测条目(M2a·服务端段:服务端权威 Block Blast 发牌环路)

> 服务端工程 `Fantasy/`(分支 `block`)。本轮把发牌搬进真实 Fantasy 服务端:新建 3 条 Outer RPC(GameStart/Place/GameSnapshot)+ `GameSession` 权威对局实体,发牌走 M1 逐局实例 API(seed 服务端签发、形状服务端权威、place 只传输入)。
> dev 已自检:`dotnet build` 服务端编译 0 错误 0 警告(我新增文件 0 警告)、协议导出成功且客户端生成物已同步进 UnityProject、源生成器已注册 3 个 Handler、权威性回归自检(服务端权威发牌节律 vs golden gen-core trace)6 个 seed 逐字符一致。
> 以下 ST1/ST2 需用户在能起服(且本机可达 MongoDB)的环境手测;ST3 是纯命令行,任何装 .NET 8 SDK 的机器可跑。
> **前置(重要)**:起服 / 跑 `dotnet build Server.sln` 前,需先停掉两个遗留的旧服务端进程(`Main`,启动于今早 09:42,PID 见任务管理器),它们占着 `Fantasy/examples/Bin/Debug/net8.0/` 的 dll/pdb,导致 Server.sln 构建的最后拷贝步报 MSB3027 文件锁(编译本身已通过)。停掉后即可正常构建与起服。

### [ ] ST1 · 服务端整解决方案编译通过(0 错 0 警)

- 测什么:加入链接进来的发牌生成核心 + 3 个 Handler + GameSession 实体后,整个服务端解决方案能否干净编译。
- 怎么测:先停掉上面「前置」说的两个遗留 `Main` 进程;命令行 `cd D:\work\TEngine_block\Fantasy` 后 `dotnet build examples/Server/Server.sln`。
- 预期结果:`已成功生成`,本轮新增的 BlockBlast 文件 0 警告 0 错误(整 sln 仍有若干既有示例的历史 nullable 告警,非本轮引入)。若仍报 MSB3027/MSB3021「文件被 Main(...) 锁定」,说明遗留进程未停干净,停掉后重试。

### [ ] ST2 · 客户端 ↔ 服务端发牌环路往返(起服 Log 验)

- 测什么:开局 / 落子 / 快照三条 RPC 在真实服务端往返;形状全程服务端权威(place 协议无 shapeId 字段);step 幂等三分支各有处理。
- 怎么测:停掉遗留 `Main` 进程后,`cd D:\work\TEngine_block\Fantasy` 起服 `dotnet run --project examples/Server/APP/Main/Main.csproj -- --m Develop`(需本机 MongoDB 可达,登录链路依赖)。用客户端(或现有测试客户端)登录后依次发:
  - `C2G_GameStartRequest`(空请求)→ 看服务端日志 `[BlockBlast] GameStart playerId=... gameId=... seed=... initialTrio=[...]`,响应回带 gameId/seed/initialTrio(3 个)/step=0/generatorState。
  - `C2G_PlaceRequest{ gameId, baseStep=0, candidateIndex=<有合法落点的槽>, posX, posY }` → 响应 `ResultCode=StepAdvanced(0)`、step 变 1、board 更新;三候选用完那一手响应 `NewCandidate` 为新补入的 shapeId。
  - 幂等三分支:重发刚才那条(baseStep 仍为旧值 < 当前 step)→ `ResultCode=IdempotentReplay(1)`、不重复落子、回当前权威态;发一条 `baseStep` 大于当前 step 的 → `ResultCode=StepAhead(2)`、回当前态。
  - `C2G_GameSnapshotRequest{ gameId }` → `ResultCode=Ok(0)`,回带 board/score/step/candidateQueue/generatorState。
- 预期结果:三条 RPC 均有响应、无服务端异常红日志;落子推进 step、补牌只在三候选用完时发生;幂等分支按 ==/</> 各自回对应 ResultCode。形状由服务端持有——place 请求里没有也无法传 shapeId(协议字段确实只有 candidateIndex + posX/posY)。
- (依赖 MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

### [ ] ST3 · 权威性回归自检复跑(纯命令行,无需起服)

- 测什么:服务端权威发牌节律与既有 golden gen-core trace 是否逐位一致(复现 dev 自检)。
- 怎么测:命令行 `cd D:\work\TEngine_block\Fantasy` 后 `dotnet run --project experiments/BlockBlastGenCore/BlockBlastGenCore.csproj -- --verify-server-cadence`。
- 预期结果:6 个 seed(1337/1/7/42/99/12345)逐条 `与 golden 逐字符一致 ✓ 行数=256`,末行 `[OK] 服务端权威发牌节律与 golden 逐位一致(全 seed)`。若任一 seed 报发散,会打印 `首个发散行=N` 与 golden/server 两行对照,原样回报。

---

## 待测条目(M3·客户端段:切换服务端权威发牌 + 本地预测对账)

> 本轮把现网客户端游戏循环从「本地权威发牌」直接切换为「服务端权威 + 客户端用同一份生成核心做乐观预测 + 服务端响应对账」。新增客户端发牌网络对接层(3 RPC 封装)+ 预测/对账引擎(纯逻辑可单测)+ 玩法窗接线;本地权威发牌旧路已短路(ServerDeal 注入后本地发牌入口全程不走)。
> 新增文件:`Module/BlockBlast/Player/IBlockGameGateway.cs`、`BlockGameGatewayProd.cs`、`BlockGameResults.cs`、`ServerDealSync.cs`、`Module/BlockBlast/BlockGenWeightConfig.cs`、测试 `Editor/Tests/BlockBlast/ServerDealSyncTests.cs`。
> 改动文件:`DynamicWeightDiff.cs`(加 RestoreState)、`BlockGameState.cs`(加 ServerDeal 投影 + RefillPieces 短路)、`GameContext.cs`、`GameApp.cs`、`MergeOrderWindow.cs`。
> CT1/CT2/CT3 在 Unity 内即可手测;CT4 真往返需用户起服(MongoDB + 服务端,前置同 ST2:先停遗留 Main 进程)。

### [ ] CT1 · 改动后客户端工程编译通过

- 测什么:本轮所有新增/改动文件能否随客户端工程编译通过(含热更区 GameLogic + 测试程序集 BlockBlast.Tests)。
- 怎么测:Unity 编辑器内等脚本编译完成,看 Console 有无红色 error;打开 Window → General → Test Runner(EditMode)能否列出 `ServerDealSyncTests`(列得出即说明 GameLogic + 测试程序集都编译成功)。
- 预期结果:无编译错误;EditMode 列表出现 `ServerDealSyncTests`,含 `Prediction_MatchesServer_NoReconcileCorrection`、`Reconcile_OverwritesWhenServerDiverges`、`Reconcile_IdempotentReplay_NoSpuriousCorrectionOnAgreement`、`Snapshot_LoadsAuthoritativeStateAndMarksCorrected` 四条。既有 `BlockBlast.Tests` 用例仍正常列出。

### [ ] CT2 · 预测与服务端口径逐位一致(核心 happy path,纯单测)

- 测什么:客户端预测发牌器(同服务端 seed + 服务端镜像权重配置)驱动一局 240 步脚本对局,每步与就地模拟的服务端权威态对账,断言「对账永不触发覆盖」(预测逐位等于权威);终局 step/score/盘面/候选与服务端一致。
- 怎么测:Test Runner → EditMode,运行 `ServerDealSyncTests.Prediction_MatchesServer_NoReconcileCorrection`。
- 预期结果:PASS(绿)。若 FAIL,失败信息会指出在第几步出现 `预测应与服务端逐位一致、对账不应覆盖`——把该步号 + 预测 step/score 原样回报(说明客户端镜像权重配置 `BlockGenWeightConfig.ServerMirror()` 与服务端 `DefaultWeightConfig()` 或发牌节律有偏,需 boss 核对两端配置)。

### [ ] CT3 · 对账分支与快照恢复正确(异常路径,纯单测)

- 测什么:① 故意构造与预测分歧的服务端响应(分数 +999、盘面清零、候选换值)→ 对账应以服务端权威整体覆盖;② 幂等响应且与预测一致 → 不误判覆盖;③ 快照恢复用服务端全态加载并标记需整屏重绘。
- 怎么测:Test Runner → EditMode,运行 `ServerDealSyncTests` 的 `Reconcile_OverwritesWhenServerDiverges`、`Reconcile_IdempotentReplay_NoSpuriousCorrectionOnAgreement`、`Snapshot_LoadsAuthoritativeStateAndMarksCorrected` 三条。
- 预期结果:三条均 PASS(绿)。

### [ ] CT4 · 实机往返:开局/落子/重连走服务端权威,对账正确(起服手测)

- 测什么:玩法窗开窗走 C2G_GameStart、落子走 C2G_Place(只传输入)、对账分支生效;形状全程服务端权威;幂等(快速重复/乱序)对账正确。
- 怎么测:前置同 ST2(停遗留 `Main` 进程 → 起服 `dotnet run --project examples/Server/APP/Main/Main.csproj -- --m Develop`,需 MongoDB 可达)。客户端 Play 模式正常登录进主菜单 →「开始游戏」进玩法窗:
  - 进窗瞬间手牌可能短暂为空(GameStart RPC 在飞),随即填入服务端首批 3 块——观察服务端日志 `[BlockBlast] GameStart playerId=... gameId=... seed=... initialTrio=[...]`。
  - 拖块落子若干手:盘面/分数随之更新,三块用完自动补 3 块;每次落子服务端日志应有 Place 推进(step 递增),无红色异常日志。
  - 观察 Console:正常情况下不应出现 `[MergeOrderWindow] C2G_Place 异常` / `GameNotFound` 之类告警(出现即回报)。
- 预期结果:开局走 GameStart 拿到服务端首批;落子走 Place 只传 candidateIndex + 落点(协议无 shapeId 字段,形状服务端权威);整体手感与改动前一致;Console 无红、无 Place 失败告警。
- 已知行为变化(请用户知悉,非 bug):**每次开窗 = 服务端新局**(GameStart 发新 gameId、空盘、step=0),原「本地无尽局内态续存(重开恢复上次盘面/分数)」在服务端权威下被新局取代;盘面/分数以服务端为准。体力/订单/合成区等经济仍本地续存(它们不属服务端发牌权威)。若用户期望「重开仍恢复上次对局」,这是服务端对局生命周期策略问题(GameSession 是否跨开窗持久),需 boss 与服务端确认——本轮按 boss 方案「开局走 GameStart」实现为每次新局。
- (依赖 MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

---

## 待测条目(M3b·服务端段:GameSession 按 playerId 持久续存 + 续局语义 + 发牌器全态可序列化)

> 服务端工程 `Fantasy/`(分支 `block`)。本轮把 `GameSession` 从「每次开窗新建的瞬态实体」改为「按 playerId 持久续存」:局内盘面/分数/步号/候选 + 发牌器全运行态(RNG 游标 + 5 调度标量 + LastAlgo/LastTier)落 MongoDB(集合 `block_blast_session`,_id=playerId),开窗走「续局语义」(有持久 Doc 则恢复、无则新建)。这根治上一轮 CT4 标注的「每次开窗=新局」——续局后盘面/分数恢复、后续发牌与中断前逐位接续。
> 发牌器扩展纯加性(`XorShift128PlusRng` 加导出/导入游标、`DynamicWeightDiff` 加 ExportFullState/ImportFullState),不改生成语义;协议 `BlockGenState` 增 RNG 游标(RngS0/RngS1)+ LastAlgo/LastTierId 两字段,`G2C_GameStartResponse` 增 Resumed/Score/Board。
> dev 已自检:`dotnet build examples/Server/Server.sln` 0 错误(整 sln 3 个既有示例历史 nullable 告警,非本轮引入;本轮新增文件 0 告警);协议导出成功且客户端生成物已同步进 UnityProject(`Assets/Fantasy/Generate/NetworkProtocol/OuterMessage.cs` 含 RngS0/Resumed);权威性回归 `--verify-server-cadence` 6 seed 仍逐字符一致;续局忠实性 `--verify-resume` 全 seed × 6 切点逐位接续。
> SST3/SST4 是纯命令行(任何装 .NET 8 SDK 的机器可跑);SST1/SST2 需用户在能起服(且本机可达 MongoDB)的环境手测。前置同 ST2/CT4:起服 / 跑 `Server.sln` 前先停遗留 `Main` 进程(占 `examples/Bin/Debug/net8.0/` dll 锁)。

### [ ] SST1 · 服务端整解决方案编译通过(0 错)

- 测什么:新增持久层(GameSessionDoc / GameSessionServiceComponent + System / GameSessionPersistHelper)+ 续局 Rehydrate/BuildDoc + 协议新字段 + 发牌器全态加性扩展后,整个服务端解决方案能否干净编译,且源生成器把新组件 System 与改动的 Handler 正确注册。
- 怎么测:先停掉遗留 `Main` 进程;命令行 `cd D:\work\TEngine_block\Fantasy` 后 `dotnet build examples/Server/Server.sln`。
- 预期结果:`已成功生成`;本轮新增 BlockBlast 持久文件 0 警告 0 错误(整 sln 仍有 3 个既有示例历史 nullable 告警,非本轮)。若报 MSB3027/MSB3021「文件被 Main(...) 锁定」,遗留进程未停干净,停掉重试。

### [ ] SST2 · 续局往返:重开窗/重连恢复上次盘面+分数+后续发牌接续(起服手测)

- 测什么:同一 playerId 第一次开窗新建对局并落几子后,关闭玩法窗(或断线重连/重登)再次开窗,服务端按 playerId 从持久 Doc 恢复对局——盘面/分数/步号/候选恢复到中断前,且续局后的补牌与不中断时一致。
- 怎么测:停遗留 `Main` → 起服 `dotnet run --project examples/Server/APP/Main/Main.csproj -- --m Develop`(需 MongoDB 可达)。客户端登录后:
  - 第一次开窗:服务端日志应为 `[BlockBlast] EnterGame NEW playerId=... gameId=... seed=... initialTrio=[...]`,响应 `Resumed=false`、step=0、空盘。
  - 落若干子(让 step 推进、分数增长,最好触发一次补牌);每子服务端有 Place 推进日志。
  - 关闭玩法窗再开(或断线重连后再开窗):服务端日志应为 `[BlockBlast] EnterGame RESUME playerId=... gameId=<同一 gameId> step=<上次的步号> score=<上次分数> candidates=[...]`,响应 `Resumed=true`、step/score/board/candidateQueue 全是中断前的值(非 0 态)。
  - 续局后再落子:补牌的 shapeId 应与「不中断一气玩到这步」相同(发牌逐位接续;此点严格性由 SST4 纯命令行自检兜底,手测只需观察续局后发牌正常、无重置)。
- 预期结果:首次 `Resumed=false` 新建、续局 `Resumed=true` 恢复;续局 gameId 与首次相同;盘面/分数/候选恢复到中断前;续局后发牌正常接续,无「盘面被清空/分数归零/重新发首批」的回归。
- (依赖 MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

### [ ] SST3 · 权威性回归复跑(发牌器全态加性扩展未伤确定性,纯命令行)

- 测什么:对发牌器加了 RNG 游标导出/导入 + 全态导出/导入后,服务端权威发牌节律与既有 golden 是否仍逐位一致(确认加性扩展零回归)。
- 怎么测:`cd D:\work\TEngine_block\Fantasy` 后 `dotnet run --project experiments/BlockBlastGenCore/BlockBlastGenCore.csproj -- --verify-server-cadence`。
- 预期结果:6 个 seed 逐条 `与 golden 逐字符一致 ✓ 行数=256`,末行 `[OK] 服务端权威发牌节律与 golden 逐位一致(全 seed)`。任一 seed 发散会打印 `首个发散行=N` 与对照,原样回报。

### [ ] SST4 · 续局忠实性自检(续存正确性核心,纯命令行)

- 测什么:模拟「建局→落 N 子→序列化发牌器全态到 Doc→从 Doc 重建→续落 M 子」,续接段的 trio/盘面/分数与「不中断一气跑完 N+M 子」逐位一致——证明 ExportFullState/ImportFullState + RNG 游标复位忠实,续局后发牌与中断前逐位接续。
- 怎么测:`cd D:\work\TEngine_block\Fantasy` 后 `dotnet run --project experiments/BlockBlastGenCore/BlockBlastGenCore.csproj -- --verify-resume`。
- 预期结果:6 seed × 6 切点(N,M ∈ {(1,50),(3,50),(4,50),(10,60),(37,80),(120,120)},含补批边界 N=3/4 与跨补批切点)逐条 `续局逐位接续 ✓`,末行 `[OK] 续局重建后发牌与中断前逐位接续(全 seed × 全切点)`。任一发散会打印 `首个发散行=N` 与 oneShot/resumed 两行对照,原样回报。

> SST3/SST4 是「C# 对象直比」自检(不过真 Bson / Mongo)。下面 ST2-INT 是补上真环境那层的自动集成验证,已由 dev 跑通。

---

## 待测条目(ST2-INT·服务端段:真 Bson + 真 Mongo 往返集成验证,dev 已自动验)

> 目的:补 SST3/SST4 之上缺的那层——SST3/SST4 是 C# 对象直比,不过真网络序列化 / 真 Bson / 真 Mongo 读写。本条做一个可重复运行的 headless 集成往返,驱动真实生产代码路径(`GameSessionHelper.Init/Place/BuildDoc/Rehydrate` + `GameSessionPersistHelper.Save/Load`)对 live MongoDB(`fantasy_main1`,集合 `block_blast_session`)做端到端往返,证明整条链在真 Bson + 真 Mongo 下确实转、续局逐位接续、游标 ulong↔int64↔Bson 无损。
> 实现路径:in-process 集成(非 headless 网络客户端)。新增非生产测试项目 `experiments/BlockBlastMongoRoundtrip/`(引用真实生产程序集 Entity + Hotfix,连真 Mongo),用专用测试 playerId `st2_test_player_roundtrip`,跑完自清理写入的测试 Doc(不污染库)。
> 覆盖层:**Bson 序列化 + Mongo 读写 + BuildDoc/Rehydrate/Init/Place/Save/Load 生产逻辑(均覆盖)**;**未过**真 socket / OuterMessage proto wire(该层是源生成、低风险,本步不强求过 socket——proto 往返由 M3/CT 客户端段单测 + ST2 起服手测覆盖)。
> dev 已自动跑通:85 个断言全 PASS,exit code 0,可重复(连跑两次均绿、自清理生效)。以下为用户(或后续会话)复跑入口。

### [ ] ST2-INT · 真 Mongo 往返集成验证复跑(纯命令行,需本机 MongoDB 可达)

- 测什么:建局(Resumed=false / 初始 trio / step0 / 空盘)→ 落 8 子(每步真落盘)→ 从 Mongo 读回原始 Bson 文档核对游标/步号/分数 → 重连续局(Load Doc → Rehydrate,Resumed=true,恢复态与中断前逐字段一致含 RNG 游标)→ 续落至棋盘自然 jam(全程与「不中断一气跑」逐位接续)→ 二次重连恢复 → Place step 幂等三分支(==/</>)+ 非法落点行为。
- 怎么测:确保本机 MongoDB 在跑(`127.0.0.1:27017`),`cd D:\work\TEngine_block\Fantasy` 后:
  `dotnet run --project experiments/BlockBlastMongoRoundtrip/BlockBlastMongoRoundtrip.csproj -c Debug`
- 预期结果:逐条 `[PASS] ...`(共 80+ 条),关键行包括:
  - `[PASS] 建局 Resumed=false` / `建局 step=0` / `建局棋盘全空` / `建局初始 trio 满 3 非空`
  - `[PASS] GameSessionDoc 已真实写入 Mongo(_id=playerId 可读回)`
  - `[PASS] Bson RngS0 == 内存游标 (...)` / `Bson RngS1 == 内存游标 (...)`
  - `[PASS] 游标 ulong↔int64↔Bson 往返无损 (...)`
  - `[PASS] 续局 Resumed=true` / `续局恢复态 == 中断前(board/score/step/候选/genState 含游标)`
  - 多条 `[PASS] 续落第 N 步后 resumed≡golden(逐位接续)` + `续落终态 resumed≡golden(整段逐位接续)`
  - `[PASS] 二次重连恢复态 == golden(续落后持久态可恢复)`
  - 幂等三分支 + `非法落点返 IllegalPlacement` 全 PASS
  - 末行 `[OK] ST2 全部断言通过:真 Bson + 真 Mongo 往返,续局逐位接续,游标无损`;`[cleanup] 测试 Doc 已删除`。
  - exit code 0(`echo $?` / `echo %ERRORLEVEL%` 应为 0)。
- 若 MongoDB 不可达:输出 `[BLOCKED] MongoDB 不可达`,exit code 2——属环境问题,在有库环境复跑。
- 测试数据隔离与清理:只读写 _id=`st2_test_player_roundtrip`,跑完(含异常路径)在 finally 删除;下一次复跑的 `[PASS] 建局前 Load 返 null(无残留对局)` 即证上次已清干净,不污染玩家数据。

---

## 待测条目(M3b·客户端段:消费续局语义 + RNG 游标完全复位)

> 客户端接上 M3b 服务端段:开窗 `C2G_GameStart` 已是续局语义,客户端按 `Resumed` 决定恢复上次对局或新局;服务端 genState 回带 PRNG 游标(RngS0/RngS1)+ LastAlgo/LastTierId,客户端经 `DynamicWeightDiff.ImportFullState` 把预测发牌器完全复位(含游标),根治 M3「真发散后游标无法复位、退化服务端推送」局限。
> CT5–CT7 是纯客户端单测(EditMode,任意能打开本工程的机器可跑);CT8 需起 Mongo + 服务端真往返(与 SST2 同环境,可合并一次手测)。

### [ ] CT5 · 改动后客户端工程编译通过

- 测什么:`GenStateView` / `GameStartResult` 加字段、`BlockGameGatewayProd` 映射新协议字段、`ServerDealSync` 改用 ImportFullState 完全复位、`BlockGameState.ProjectServerCandidates` 改幂等后,客户端 HotFix 程序集能否干净编译。
- 怎么测:用 Unity 打开本工程(`D:\work\TEngine_block\UnityProject`),等 Editor 编译完成;看 Console 是否有红色编译错误。或菜单栏触发一次脚本重编译(改任意脚本存盘 / Assets > Reimport)。
- 预期结果:Console 无编译错误(0 error)。本轮改动文件:`Player/BlockGameResults.cs`、`Player/BlockGameGatewayProd.cs`、`Player/ServerDealSync.cs`、`BlockGameState.cs`、`Editor/Tests/BlockBlast/ServerDealSyncTests.cs`。

### [ ] CT6 · ServerDealSync 单测全绿(含新增续局 / 游标复位用例,核心 happy path + 续存正确性)

- 测什么:既有预测对账单测仍绿(改 ImportFullState 未伤 happy path 逐位一致),且新增 4 个用例覆盖「全态 ImportFullState 后游标与服务端逐位一致」「真发散后完全复位续局再不发散」「Resumed=true 恢复盘面/分数/步号/候选并标记重绘」「Resumed=false 新建空盘 score0」。
- 怎么测:Unity 菜单 `Window > General > Test Runner` → EditMode → 跑 `GameLogic.BlockBlast.Tests.ServerDealSyncTests` 整个 fixture(或全量 EditMode)。
- 预期结果:`ServerDealSyncTests` 全部用例通过,含新增:`Snapshot_FullStateImport_GenCursorMatchesServer`、`AfterTrueDivergence_FullStateImport_SubsequentDealsReconverge`、`ApplyGameStart_Resumed_RestoresBoardScoreStepAndMarksCorrected`、`ApplyGameStart_NotResumed_StartsFreshEmptyBoardScoreZero`。同时既有 `Prediction_MatchesServer_NoReconcileCorrection` / `Reconcile_*` / `Snapshot_LoadsAuthoritativeStateAndMarksCorrected` 仍绿(无回归)。
- 已知风险/复核重点:Test Runner 的 filter 偶发失效 + 失败列表截断(见 dev 经验 unitymcp-run-tests-filter),若只跑单 fixture 没看到预期 7+ 条,改跑全量 EditMode 并按名核对上面 4 条新用例确实出现且为绿,而非被过滤掉当成「通过」。

### [ ] CT7 · 既有 BlockBlast EditMode 单测全绿(改动未波及发牌核心 / 经济层)

- 测什么:`ProjectServerCandidates` 改为对 PendingElements 幂等(只对新填槽出队)、`GenStateView` 加字段后,DynamicWeightDiff / 经济相关既有单测无回归。
- 怎么测:Test Runner → EditMode → 跑全量 BlockBlast 相关 fixture(`DynamicWeightDiffTests`、`MetaCurrencySyncTests`、`PlayerAttrServiceTests` 等 `Assets/Editor/Tests/BlockBlast/` 下全部)。
- 预期结果:全部通过,无新增失败。

### [ ] CT8 · 实机往返:开窗 Resumed 恢复上次对局 + 后续发牌接续(起服手测,与 SST2 合并)

- 测什么:客户端开窗发 `C2G_GameStart`(续局语义),按响应 `Resumed` 决定恢复或新局;续局时整屏重绘到中断前盘面/分数/候选并可继续落子(发牌接续);本地 cosmetic(颜色 / 元素 overlay)/ 经济层(体力 / 合成区 / 订单)与服务端权威盘占用一致、不双花元素预算。
- 怎么测:前置同 SST2(停遗留 `Main` → 起服 + MongoDB 可达 → 客户端登录)。
  1. 首次进玩法窗:盘面空、分数 0、手牌为服务端首批候选(`Resumed=false` 新局)。
  2. 落若干子(分数增长、最好触发一次补牌、若有元素玩法则让合成区 / 订单状态变化)。
  3. 关闭玩法窗再开(或断线重连后再开窗):盘面 / 分数 / 步号 / 手牌候选恢复到中断前(不是空盘 0 分新局);合成区 / 订单 / 体力等本地经济也与关窗前一致。
  4. 续局后继续落子:补牌正常(无「重新发首批 / 盘面被清 / 分数归零」),元素 overlay 跟随服务端盘占用(占用格才可能带元素,空格无元素)。
- 预期结果:`Resumed=true` 时恢复上次对局可继续玩;`Resumed=false` 时全新空局;续局后发牌逐位接续(严格性由 CT6/SST4 单测兜底,手测只需观察无重置 / 无明显发牌异常);元素预算不因重投影被重复消耗(同一格元素不凭空增减)。
- 已知风险/复核重点:① 续局后手牌候选的 shapeId 应与关窗前一致(服务端候选权威);若 shapeId 对得上但元素 overlay 丢失/翻倍,重点查 `ProjectServerCandidates` 幂等是否生效(同 shapeId 槽应原样保留、不重新出队 PendingElements)。② RTT 期间(GameStart 在飞)先显本地兜底盘,回包后才重绘到服务端态——若网络慢会看到短暂本地盘→服务端盘的切换,属预期。③ 若服务端判为新局(Resumed=false)但本地有旧局内存档,开窗瞬间可能先显旧本地盘,随即被服务端空盘覆盖,属预期(服务端盘权威)。

---

## 待测条目(M4a·服务端段:终局判定 + 权威分入榜 + GameSession 终结)

> 服务端工程 `Fantasy/`(分支 `block`)。本轮接「终局」:`C2G_Place` 成功落子续发后,服务端用 `BinaryBoard.CheckPutAllBlocks` 判当前候选是否无任一放置顺序可放(jam)→ jam=本局结束;终局时用 `GameSession.Score`(服务端权威)服务端侧直提到现有排行榜(in-process 调 `RankDecisionHelper.Submit`,周榜 1 + 总榜 2),并删持久 Doc 终结本局(下次进入走新建,不复活已结束局)。`G2C_PlaceResponse` 增 `GameOver`(bool)/`FinalScore`(int)/`BestScore`(int64,总榜入榜后最佳)三字段供 M4b 客户端段消费。
> 入榜路径=**优选(服务端直提)**:身份用会话 `Account.Name`(与客户端 `C2G_RankSubmitScore` 上报同键 → 服务端代提与客户端自报落同一行、myRank 一致),分用服务端权威 `Score`,过 rank 自身入榜门槛/最佳分比较/反作弊。形状服务端权威 + 分服务端权威 → 选块与报分作弊面全闭,无退路、无残留缝。
> Doc 终结=**删档**(`GameSessionPersistHelper.Delete`):删 Doc 后下次进入对局 `Load` 返 null → 新建分支(`Resumed=false`),复用既有「无档=新建」语义,不在 Doc/GameStart 增 ended 态。
> dev 已自检:`dotnet build examples/Server/Server.sln` 0 错误(整 sln 仍有 3 个既有示例历史 nullable 告警,非本轮;本轮新增/改动 BlockBlast 文件 0 告警);协议导出成功且客户端生成物已自动同步进 UnityProject(`Assets/Fantasy/Generate/NetworkProtocol/OuterMessage.cs` 的 `G2C_PlaceResponse` 含 `GameOver`/`FinalScore`/`BestScore`,导出工具直写客户端目录、无需手工拷贝);权威性回归 `--verify-server-cadence` 6 seed 仍逐字符一致、续局 `--verify-resume` 全 seed×6 切点仍逐位接续(终局改动未伤既有);新增终局逻辑自检 `--verify-gameover`(真 Mongo)全 PASS——游戏自然到 jam(43 步、finalScore=1805)、`IsGameOver` 在 jam 前 false 在 jam true、删档后 Load 返 null 且再进入走新建空盘。
> MST3/MST4/MST5 是纯命令行(MST3/5 需本机 MongoDB 可达,MST4 不需);MST1 任意装 .NET 8 SDK 机器可跑;MST2 需起服 + Mongo + 客户端手测。前置同既往:起服 / 跑 `Server.sln` 前先停遗留 `Main` 进程(占 `examples/Bin/Debug/net8.0/` dll 锁)。

### [ ] MST1 · 服务端整解决方案编译通过(0 错)

- 测什么:终局判定(`GameSessionHelper.IsGameOver`)+ 终局结算分支(`C2G_PlaceRequestHandler.SettleGameOver`,含 in-process rank 直提)+ 删档(`GameSessionPersistHelper.Delete`)+ 协议 `G2C_PlaceResponse` 三新字段后,整个服务端解决方案能否干净编译。
- 怎么测:先停掉遗留 `Main` 进程;命令行 `cd D:\work\TEngine_block\Fantasy` 后 `dotnet build examples/Server/Server.sln`。
- 预期结果:`已成功生成`;本轮改动 BlockBlast 文件 0 警告 0 错误(整 sln 仍有 3 个既有示例 nullable 告警:`ProductsController.cs` / `UsersController.cs` / `C2G_SubscribeSphereEventRequestHandler.cs`,非本轮引入)。若报 MSB3027/MSB3021「文件被 Main(...) 锁定」,遗留进程未停干净,停掉重试。

### [ ] MST2 · 实机往返:玩到 jam 触发终局 + 入榜 + 终结后再开走新局(起服手测)

- 测什么:玩到棋盘 jam(三候选无处可放)时服务端判终局、回带 GameOver=true/FinalScore=权威分;终局用权威分提交排行榜(周榜 1 + 总榜 2);终结后同 playerId 再开窗走新建(空盘新局,不复活已结束局)。
- 怎么测:停遗留 `Main` → 起服 `dotnet run --project examples/Server/APP/Main/Main.csproj -- --m Develop`(需 MongoDB 可达)。客户端登录后进玩法窗,落子直到棋盘满到无处可放(可故意往角落乱放制造 jam):
  - 触发 jam 那一手的 Place 响应:`GameOver=true`、`FinalScore=<当前权威分>`、`BestScore=<总榜入榜后最佳>`;服务端日志 `[BlockBlast] GameOver playerId=... gameId=... step=... finalScore=... totalBest=... board=[...]`。
  - 入榜:若 finalScore ≥ 100,周榜(rankId=1)应被刷新;总榜(rankId=2,入榜要求 0)恒被纳入。可随后用客户端查榜(`C2G_RankQueryRequest`,rankId=1 或 2)看自己名次/分数是否含本局 finalScore;或服务端日志 `排行榜上报 account=... rankId=1/2 score=... result=... best=...`。
  - 终结联动:终局后同一 playerId 再次开窗(`C2G_GameStart`)→ 服务端日志应为 `[BlockBlast] EnterGame NEW ...`、响应 `Resumed=false`、空盘 step=0(**不是**恢复到刚才的 jam 盘)。
- 预期结果:jam 那手回带 GameOver=true + 正确 finalScore;入榜后查榜能看到本局分(达门槛的榜);终局后再开是全新空局,已结束的 jam 局不被复活。Console/服务端无红色异常。
- 入榜身份核对(重要):服务端代提用的账号键 = 客户端 `C2G_RankSubmitScore`/`C2G_RankQuery` 用的同一个 `Account.Name`,故服务端代提的分与客户端查榜看到的「我的分」应是同一行——若查榜看不到本局分,核对会话是否已登录(`GateAccountFlagComponent` 在)。
- (依赖 MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

### [ ] MST3 · 终局逻辑自检复跑(终局判定 + 删档终结 + 终结后走新建,真 Mongo)

- 测什么:复现 dev 自检——建局落子贪心推进到 jam,断言 `IsGameOver` 在 jam 前为 false、jam 时为 true、终局权威分 > 0;`Delete` 删档后 `Load` 返 null;终结后再进入对局走新建(Resumed=false、空盘、step0)。
- 怎么测:确保本机 MongoDB 在跑(`127.0.0.1:27017`),`cd D:\work\TEngine_block\Fantasy` 后:
  `dotnet run --project experiments/BlockBlastMongoRoundtrip/BlockBlastMongoRoundtrip.csproj -- --verify-gameover`
- 预期结果:逐条 `[PASS]`,含 `建局态 IsGameOver=false` / `游戏自然推进到 jam(落子 N 步)` / `jam 态 IsGameOver=true` / `终局权威最终分 > 0` / `终结删档后 Load 返 null(已结束局不被复活)` / `终结后再进入对局 Resumed=false(走新建)` / `新局空盘`;末行 `[OK]`,`[cleanup] 测试 Doc 已删除`,exit code 0。MongoDB 不可达则 `[BLOCKED]` + exit 2(有库环境复跑)。说明:入榜(需完整 Scene + RankServiceComponent)不在本逻辑自检内,复用已验的 rank 核心,入榜真验走 MST2 起服手测。

### [ ] MST4 · 权威性回归复跑(终局改动未伤既有发牌确定性,纯命令行)

- 测什么:加终局判定/入榜/删档/协议字段后,服务端权威发牌节律与既有 golden 仍逐位一致 + 续局仍逐位接续(确认本轮零回归)。
- 怎么测:`cd D:\work\TEngine_block\Fantasy` 后依次:
  - `dotnet run --project experiments/BlockBlastGenCore/BlockBlastGenCore.csproj -- --verify-server-cadence`
  - `dotnet run --project experiments/BlockBlastGenCore/BlockBlastGenCore.csproj -- --verify-resume`
- 预期结果:cadence 6 seed 逐条 `与 golden 逐字符一致 ✓ 行数=256` + 末行 `[OK] 服务端权威发牌节律与 golden 逐位一致(全 seed)`;resume 6 seed × 6 切点逐条 `续局逐位接续 ✓` + 末行 `[OK] 续局重建后发牌与中断前逐位接续(全 seed × 全切点)`。任一发散打印 `首个发散行=N` 与对照,原样回报。

### [ ] MST5 · 真 Mongo 往返集成验证仍绿(终局改动未伤既有续局往返,纯命令行)

- 测什么:既有 ST2-INT 真 Bson + 真 Mongo 续局往返(建局/落子/读回 Bson/续局/二次重连/幂等三分支)在本轮改动后仍全绿(默认模式,不带 `--verify-gameover`)。
- 怎么测:本机 MongoDB 可达,`cd D:\work\TEngine_block\Fantasy` 后:
  `dotnet run --project experiments/BlockBlastMongoRoundtrip/BlockBlastMongoRoundtrip.csproj -c Debug`
- 预期结果:全部 `[PASS]`,末行 `[OK] ST2 全部断言通过:真 Bson + 真 Mongo 往返,续局逐位接续,游标无损`,exit code 0。

---

## 待测条目(M4b·客户端段:消费服务端终局信号 + 结算页 + 新局)

> 客户端接上 M4a 服务端终局:落子对账(`C2G_Place`)回带 `GameOver`/`FinalScore`/`BestScore` 三字段被客户端消费——网关 + DTO 拷字段、`ServerDealSync` 暴露终局态并在终局后拒绝再落子、玩法窗收到 `GameOver=true` 弹结算面板(最终分 + 服务端权威最佳分)并停止落子,结算面板「再来一局」关本窗重开 = 新局(服务端已删档,GameStart 走 Resumed=false)。终局判定**以服务端信号为准**,客户端不本地判 jam 自结算。最佳分以服务端 `BestScore` 投影展示(不新增本地权威分存储)。
> 改动文件:`Player/BlockGameResults.cs`(PlaceResult 加 GameOver/FinalScore/BestScore)、`Player/BlockGameGatewayProd.cs`(从 G2C_PlaceResponse 拷三字段)、`Player/ServerDealSync.cs`(暴露终局态 + 终局后拒落子 + 新局/快照复位)、`UI/BlockBlastUI/MergeOrderWindow.cs`(对账检测终局→弹结算+门控落子;运行时构建结算面板,无独立 prefab)、`Editor/Tests/BlockBlast/ServerDealSyncTests.cs`(加 3 个终局用例)。
> MCT1–MCT3 是纯客户端单测(EditMode,任意能打开本工程的机器可跑);MCT4 需起 Mongo + 服务端真往返(可与 MST2 合并一次手测)。

### [ ] MCT1 · 改动后客户端工程编译通过

- 测什么:本轮所有改动文件能否随客户端工程干净编译(含热更区 GameLogic + 测试程序集 BlockBlast.Tests)。PlaceResult 构造函数加了 3 个可选尾参,既有 7 参调用点(网关 fail / sim gateway)应不受影响。
- 怎么测:用 Unity 打开本工程(`D:\work\TEngine_block\UnityProject`),等 Editor 编译完成;看 Console 是否有红色编译错误。或菜单栏触发一次脚本重编译(改任意脚本存盘 / Assets > Reimport)。打开 `Window > General > Test Runner`(EditMode)能否正常列出 `ServerDealSyncTests`(列得出即说明 GameLogic + 测试程序集都编译成功)。
- 预期结果:Console 无编译错误(0 error);EditMode 列表出现 `ServerDealSyncTests`,且新增 3 条用例可见:`Reconcile_GameOver_ExposesFinalAndBestScore_AndMarksGameOver`、`AfterGameOver_PredictAndPlaceRejected`、`NewGameAfterGameOver_ResetsTerminalState`。

### [ ] MCT2 · ServerDealSync 终局态单测全绿(终局信号暴露 + 终局后拒落子 + 新局复位,核心 + 异常路径)

- 测什么:① 落子对账回 `GameOver=true` 时,`ServerDealSync.GameOver` 置位且 `FinalScore`/`BestScore` 暴露服务端权威值;② 终局后 `PredictPlace` 被拒(Accepted=false、不改 step/score)、`PlaceAsync` 短路回 `GameNotFound`(不发 RPC);③ 终局后再 `StartGameAsync`(新局,SimGateway 默认 Resumed=false)复位终局态(GameOver=false、FinalScore/BestScore 归 0)且可正常落子。
- 怎么测:Test Runner → EditMode → 跑 `GameLogic.BlockBlast.Tests.ServerDealSyncTests` 整个 fixture(或全量 EditMode)。
- 预期结果:`ServerDealSyncTests` 全部用例通过,含新增 3 条(见 MCT1)。同时既有 happy-path / 对账 / 快照 / 续局用例仍绿(`Prediction_MatchesServer_NoReconcileCorrection`、`Reconcile_*`、`Snapshot_*`、`ApplyGameStart_*` 无回归)。
- 已知风险/复核重点:Test Runner 的 filter 偶发失效 + 失败列表截断(见 dev 经验 unitymcp-run-tests-filter)。若只跑单 fixture 没看到预期条数,改跑全量 EditMode 并按名核对上面 3 条新用例确实出现且为绿,而非被过滤掉当成「通过」。

### [ ] MCT3 · 既有 BlockBlast EditMode 单测全绿(终局字段加性扩展未波及发牌核心 / 经济层)

- 测什么:PlaceResult 加 3 字段(尾部可选参,默认 false/0)、ServerDealSync 加终局态字段后,DynamicWeightDiff / 经济 / 其它 BlockBlast 既有单测无回归。
- 怎么测:Test Runner → EditMode → 跑 `Assets/Editor/Tests/BlockBlast/` 下全部 fixture(Run All,或至少 `DynamicWeightDiffTests`、`MetaCurrencySyncTests`、`MergeOrderTests`、`BlockGameStateTests`)。
- 预期结果:全部通过,无新增失败。

### [ ] MCT4 · 实机往返:玩到 jam 终局 → 结算页(最终分 + 最佳分)→ 停止落子 → 再开走新局(起服手测,与 MST2 合并)

- 测什么:落子触发服务端终局(GameOver=true)→ 客户端弹结算面板显示最终分 + 服务端权威最佳分、棋盘停止响应落子;结算面板「再来一局」开新局(空盘、Resumed=false);最佳分展示取服务端 BestScore。
- 怎么测:前置同 MST2(停遗留 `Main` → 起服 `dotnet run --project examples/Server/APP/Main/Main.csproj -- --m Develop`,需 MongoDB 可达 → 客户端 Play 登录进主菜单 →「开始游戏」进玩法窗)。
  1. 落子直到棋盘 jam(三候选无处可放,可故意往角落乱放制造 jam)。
  2. 触发 jam 那一手:观察是否弹出结算面板(半透明遮罩 + 居中卡片),卡片显示「游戏结束」「本局得分 <数字>」「最佳分 <数字>」,以及「再来一局」「返回」两个按钮。最终分应等于服务端权威分(与服务端日志 `[BlockBlast] GameOver ... finalScore=...` 一致)。
  3. 结算面板弹出后,尝试拖动棋盘下方候选块往棋盘落:应无法落子(块归位、不消除、不计分)——落子已被停止。
  4. 点「再来一局」:本窗关闭后重新打开,棋盘应为全新空盘、分数 0、手牌为服务端新批候选(Resumed=false 新局);服务端日志 `[BlockBlast] EnterGame NEW ...`。
  5. 点「返回」(可另起一局测):回到主菜单,主菜单 BEST 显示的最高分应已反映本局(若本局分更高);该值取自服务端 BestScore 投影。
- 预期结果:jam 触发结算面板正确显示最终分 + 最佳分;结算后棋盘停止落子;「再来一局」= 空盘新局(非恢复 jam 盘);「返回」回主菜单且 BEST 反映服务端最佳分;Console 无红色异常。
- 已知风险/复核重点:① 结算面板是运行时代码构建(非 prefab),用 `UGuiFactory` 在 1080×1920 设计坐标系居中——若面板位置 / 文字错位 / 遮罩没盖住棋盘,回报(布局参数需调,非逻辑 bug)。② 终局信号在落子对账的异步回包里到达(`SendPlaceAndReconcile`),触发 jam 那手落下后到弹面板有一个 RTT 的延迟,期间棋盘短暂可交互——若延迟内又落了一子,该子会正常上报但服务端对已删档局回 GameNotFound,本地态保留、下次开窗重建(不致命,但用户若观察到「终局后还能再落一子」属此时序,回报以便评估是否需落子后即时本地禁手)。③ 若服务端 BestScore 回 0(入榜服务不可用),结算页「最佳分 0」属预期(不抹本地既有展示;`ShowGameOverSettlement` 仅在 BestScore > 本地 HighScore 时才更新本地投影)。④ `FinalScore` 是服务端权威分,与客户端本地乐观结算的显示分在 happy path 下应一致(预测逐位对账);若两者不一致,说明该局曾发生过对账覆盖,以服务端 FinalScore 为准。
- (依赖 MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

---

## 待测条目(元层进度计数器上服务端骨架·服务端段)

> 服务端工程 `Fantasy/`(分支 `block`)。云存档 blob 迁服务端权威第 1 批:把六个元层数值进度计数器(女神等级 GoddessLevel / 女神评级 GoddessRating / 章节解锁数 UnlockedChapter / 盲盒计数 BlindBoxCount / 神庙修缮计数 TempleRepaired / 神庙修缮游标 NextRepairIndex)迁上既有 PropertyChange 骨架——**复用**已有的 `C2G_PropertyChangeRequest` 通道 + `PlayerPropertyServiceHelper.ChangeProperty`(限界信任 + ledger + delta-push)+ 登录快照 `G2C_PlayerInfoSnapshot`,不造新通道。本批**只做服务端段**(客户端消费=报增量 + 从快照读 + 从 blob 删,下一段做)。
> `highScore` 特判结论:**不纳入本批**——服务端排行榜(`RankScoreDoc.BestScore`,_id=`{account}|{rankId}`,经 `G2C_RankQueryResponse.MyScore` 下发)已是「个人最佳分」权威源,与客户端 blob 的 highScore 等价;客户端段应改从排行榜投影读、从 blob 删 highScore,不为它新增 PropertyType/字段。
> 产出定位:六计数器均走 PropertyChange 上报增量(客户端算增量 + 事由,服务端限界信任落账),**不建模产出逻辑**(玩法产出 = 女神/盲盒;动作产出 = 神庙修缮/游标;真正的产出建模留后续抽奖/合成批)。校验规则:单次 delta 上限(限界信任主杠杆,占位值,女神/章节 100、盲盒/修缮 1000)+ 类型上界(宽松 sanity 天花板)+ 同账号同属性 100ms 频率闸,均沿既有四货币口径。BlindBoxCount 可增可减(攒盒/开盒),其余单调递增但骨架不强制单调(限界信任只防异常大跳)。
> 协议改动:`PropertyType` 枚举加 7..12 六值(纯加性,不动既有 0..6);`C2G_QueryAttrLedger.Kind` 映射扩到 1..13。协议已重导,客户端生成物已自动同步进 UnityProject(`Assets/Fantasy/Generate/NetworkProtocol/OuterEnum.cs` 含 GoddessLevel=7..NextRepairIndex=12;`OuterOpcode.cs` 无变化 = 未增删消息,opcode 稳定,两端不错位)。
> dev 已自检:Entity + Hotfix 项目编译 0 错误(本批新增/改动文件 0 警告;`dotnet build Server.sln` 因遗留 Main 进程锁 SourceGenerator dll 报 CS2012 属环境,单编 Entity/Hotfix 佐证代码本身干净);6 值枚举两端一致、schema 版本 4→5、旧档 $ifNull 补字段已加。
> PC1 需能起服 + 本机可达 MongoDB 的环境手测(PropertyChange 往返 + 快照回带 + 限界信任拒异常);编译核对(PC0)任意装 .NET 8 SDK 机器可跑。前置同既往:起服 / 跑 `Server.sln` 前先停遗留 `Main` 进程(占 `examples/Bin/Debug/net8.0/` dll 锁)。

### [ ] PC0 · 服务端编译通过(0 错;新增/改动文件 0 警)

- 测什么:六计数器加入 `PlayerDoc` 字段 + `PropertyType` 枚举 + `PlayerPropertyServiceComponent` 配置 + `PlayerPropertyServiceHelper`(TryGetTypeMeta / GetFieldValue / InitOrLoad setOnInsert / MigrateSchemaIfNeeded $ifNull / ResetToNewbie / SendPlayerInfoTo)+ ledger kind 映射后,整个服务端能否干净编译,且源生成器把导出的 `PropertyType` 枚举正确产出。
- 怎么测:先停掉遗留 `Main` 进程;命令行 `cd D:\work\TEngine_block\Fantasy` 后 `dotnet build examples/Server/Server.sln`。若仍因 `Main` 锁 SourceGenerator dll 报 CS2012,退而单编两项佐证代码本身:`dotnet build "examples/Server/APP/Entity/Entity.csproj"` 与 `dotnet build "examples/Server/APP/Hotfix/Hotfix.csproj"`。
- 预期结果:`已成功生成`;本批改动的 BlockBlast/Player 文件 0 警告 0 错误(整 sln 仍有 3 个既有示例历史 nullable 告警:`ProductsController.cs` / `UsersController.cs` / `C2G_SubscribeSphereEventRequestHandler.cs`,非本批引入)。Entity 项目应 0 警告 0 错误(含生成的 `PropertyType` 枚举),Hotfix 项目仅上述 3 个既有告警。

### [ ] PC1 · 六计数器 PropertyChange 往返落账 + 推送 + 登录快照回带 + 限界信任拒异常(起服手测)

- 测什么:六个新计数器各自走既有 `C2G_PropertyChangeRequest` 通道往返——服务端限界信任校验后 `$inc` 落 `players` 文档 + 写 ledger + delta-push 回同一 UUID 会话;登录快照 `G2C_PlayerInfoSnapshot` 回带这六项权威初值;异常增量(超单次上限)被拒不落账。
- 怎么测:停遗留 `Main` → 起服 `dotnet run --project examples/Server/APP/Main/Main.csproj -- --m Develop`(需 MongoDB 可达,登录链路依赖)。客户端(或测试客户端)登录后:
  - **登录快照回带**:登录成功即收 `G2C_PlayerInfoSnapshot`,其 `Info.Properties` 数组应含六项新 PropertyType(Type=7..12,首登玩家 Amount 均为 0);老档(schema<5)首次登录时服务端会 `MigrateSchemaIfNeeded` 补齐字段,快照同样带 6 项(present 值 = 各自既有值或补的 0)。
  - **PropertyChange 往返落账 + 推送**:逐个发 `C2G_PropertyChangeRequest{ Type=<7..12 之一>, Delta=<正数,如 GoddessLevel +1 / BlindBoxCount +5>, Reason="test_xxx" }`:
    - 响应 `G2C_PropertyChangeResponse{ ResultCode=Success(0), Type=<回声>, NewAmount=<累加后新值> }`;
    - 随即收到 `G2C_PropertyDeltaPush{ Type=<同>, NewAmount=<同>, Reason=<回声> }`(推送到同 UUID 在线会话);
    - 服务端日志 `PlayerProperty 变更成功 account=... type=GoddessLevel delta=1 reason='test_xxx' newAmount=1`。
    - 再发同 Type 一次(注意间隔 >100ms 避开频率闸)→ NewAmount 应继续累加(证明 `$inc` 落账、非覆盖)。
  - **BlindBoxCount 可减**:发 `Type=BlindBoxCount(10), Delta=-2`(前提当前值 ≥ 2)→ Success、NewAmount 减 2(证明可增可减,余额下界 0 由骨架条件过滤保证:扣到负数会返 NotEnough(4))。
  - **限界信任拒异常增量**:发 `Type=GoddessLevel(7), Delta=100000`(远超单次上限 100)→ 响应 `ResultCode=InvalidRequest(3)`、不落账;服务端日志 `PropertyChange 拒因=SingleDeltaLimit account=... type=GoddessLevel delta=100000 limit=100`。再发一条同 account 同 type 100ms 内的合法请求 → 可能命中频率闸返 InvalidRequest(3) + 日志 `拒因=RateLimited`(属预期,间隔开即可正常)。
  - **持久性**:变更几项后重登(断开重连再登录)→ 新的 `G2C_PlayerInfoSnapshot` 应回带刚才变更后的值(证明落库持久,非内存态)。
- 预期结果:六计数器 PropertyChange 往返均 Success + 累加落账 + delta-push;登录快照含六项且回带权威值;超单次上限的异常增量被 `InvalidRequest` 拒且不落账;重登后值保持。无服务端红色异常日志。
- (依赖 MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

---

## 待测条目(元层进度计数器上服务端骨架·客户端段)

> 云存档 blob 迁服务端权威第 1 批·客户端段:六个元层数值进度计数器(女神等级 GoddessLevel / 女神评级 GoddessRating / 章节解锁数 UnlockedChapter / 盲盒计数 BlindBoxCount / 神庙已修厅数 TempleRepaired / 神庙修缮游标 NextRepairIndex)在服务端段已权威后,客户端来消费它们、并从 blob 剔除;highScore 改从排行榜个人最佳分投影读、从 blob 剔除。
> 客户端做法照四货币 `MetaCurrencySync` 范式扩展:①`AttrType` 加 6 值(7..12,对齐服务端 PropertyType);②落盘边界 `ReportPending` 对 6 计数器算净 delta 经 `C2G_PropertyChange` 上报、服务端 NewAmount 覆盖本地视图 + 基线;③登录快照 `ApplySnapshot` 从 6 个权威值读初值覆盖本地字段 + 缓存 + 对齐基线;④`ApplyDeltaPush` 认这 6 个 type;⑤`CloudSaveCodec` 从 blob 剔除 6 计数器 + highScore(下载合并不拷 + 上传 sanitize 清零);⑥主菜单 BEST + 终局结算页最佳分改读排行榜投影(`RankService.GetMyBest`),终局服务端权威最佳分刷进排行榜投影缓存。
> 神庙已修厅数(`TempleRepaired`)特判:服务端是标量计数,客户端活态是布尔数组,「已修计数」= true 项数(顺序解锁,恒 = NextRepairIndex);标量 ↔ 数组转换(前 N 项 true)由 `MetaCurrencySync`/`GameContext` 内部处理。
> 改动文件:`Player/MetaCurrencySync.cs`、`Player/AttrType.cs`、`Player/CloudSaveCodec.cs`、`GameContext.cs`、`GameApp.cs`、`UI/BlockBlastUI/MainMenuWindow.cs`、`UI/BlockBlastUI/MergeOrderWindow.cs`、`Assets/Fantasy/Scripts/FantasyNetwork.cs`(PlayerInfoView 加 6 字段)、`Assets/Fantasy/Scripts/Handlers/G2C_PlayerInfoSnapshotHandler.cs`(从快照属性列表提 6 值);测试 `MetaCurrencySyncTests.cs`(加 C1-C6)、`CloudSaveTests.cs`(改 C1/C2)。
> dev 已自检:Unity batchmode 编译 0 错误 + EditMode 全绿(602 例 584 通过 0 失败 18 跳过,含本轮新增 6 个 MetaCurrencySync 计数器用例 + 改写 2 个 CloudSave 用例)。EC1/EC2 是纯客户端单测(EditMode,任意能打开本工程的机器可跑);EC3/EC4/EC5 需起 Mongo + 服务端真往返(可与服务端段 PC1 合并一次手测)。

### [ ] EC0 · 改动后客户端工程编译通过

- 测什么:本轮所有新增/改动文件能否随客户端工程干净编译(含热更区 GameLogic + 非热更 `Assets/Fantasy/Scripts` + 测试程序集 BlockBlast.Tests)。`MetaCurrencySync.ApplySnapshot` 签名从 4 货币参扩到 4 货币 + 6 计数器(共 10 个数值参),所有调用点(GameContext / 测试)已同步改;`PlayerInfoView` 构造函数加 6 参,handler 调用点已同步改。
- 怎么测:用 Unity 打开本工程(`D:\work\TEngine_block\UnityProject`),等 Editor 编译完成;看 Console 是否有红色编译错误。打开 `Window > General > Test Runner`(EditMode)能否列出 `MetaCurrencySyncTests` / `CloudSaveTests`(列得出即说明编译成功)。
- 预期结果:Console 无编译错误(0 error);EditMode 列表出现 `MetaCurrencySyncTests`(含新增 `C1_SnapshotAll_OverwritesSixCounters_AndAlignsBaseline`、`C2_SixCounters_ReportNetDeltaPerCounter`、`C3_BlindBoxCount_CanDecrease_ReportsNegativeDelta`、`C4_ApplyDeltaPush_OverwritesCounterByType`、`C5_RebindBaseline_RealignsSixCountersToState`、`C6_SnapshotOnly_NoCounterReport`)与 `CloudSaveTests`(含改写的 `C1_BuildBlob_ExcludesCurrencyCountersHighScoreAndIdentity`)。

### [ ] EC1 · MetaCurrencySync 六计数器单测全绿(上报/对账/快照/delta-push/rebind,核心 + 异常路径)

- 测什么:六计数器与四货币同口径——①全量快照覆盖本地视图 + 对齐基线(随后无变化不发);②各净变化每计数器恰发一笔(聚合上报,delta = 当前 - 基线);③BlindBoxCount 可减(开盒,净负 delta 正常上报);④delta-push 按 type 覆盖本地字段 + 基线(含神庙已修厅数标量 → 布尔数组前 N 项 true);⑤RebindBaseline 把六计数器基线重对齐到活态(开窗 ImportMeta 后免首刀误报);⑥纯快照无产销不发。
- 怎么测:Test Runner → EditMode → 跑 `GameLogic.BlockBlast.Tests.MetaCurrencySyncTests` 整个 fixture(或全量 EditMode)。
- 预期结果:`MetaCurrencySyncTests` 全部用例通过,含新增 C1-C6 六条;既有四货币用例(T1-T18)仍绿(无回归)。
- 已知风险/复核重点:Test Runner filter 偶发失效 + 失败列表截断(见 dev 经验 `unitymcp-run-tests-filter`),若只跑单 fixture 没看到预期条数,改跑全量 EditMode 并按名核对 C1-C6 确实出现且为绿,而非被过滤掉当成「通过」。

### [ ] EC2 · CloudSaveCodec 剔除六计数器 + highScore 单测全绿(不再入 blob / 不被 blob 回灌)

- 测什么:①`BuildBlob` 组装的 blob 内元层货币 + 六计数器(goddessLevel/goddessRating/unlockedChapter/blindBoxCount/templeRepaired 数组/nextRepairIndex)+ highScore + playerId 全被清零/清空,其余非货币字段(templeDecorated / playerName / curAvatarId 等)保留;②下载合并 `ApplyBlob` 时六计数器 + highScore 不被 blob 覆盖,保留本地既有值(权威由服务端快照/上报维护)。
- 怎么测:Test Runner → EditMode → 跑 `GameLogic.BlockBlast.Tests.CloudSaveTests` 整个 fixture。
- 预期结果:全绿,重点关注改写的 `C1_BuildBlob_ExcludesCurrencyCountersHighScoreAndIdentity`(断言 6 计数器 + highScore 清零、templeDecorated 保留)与 `C2_ApplyBlob_PreservesLocalCurrencyAndIdentity`(断言 highScore/blindBoxCount/goddessLevel 保留本地值不被 blob 回灌)。其余 C3-C12 无回归。

### [ ] EC3 · 登录快照读六计数器初值 + 主菜单 BEST 读排行榜投影(起服手测)

- 测什么:登录时六计数器从服务端快照读初值覆盖本地(非 blob 旧值);主菜单 BEST 显示排行榜个人最佳分投影(非元层 highScore)。
- 怎么测:前置起服(停遗留 `Main` → `dotnet run --project examples/Server/APP/Main/Main.csproj -- --m Develop`,需 MongoDB 可达)。客户端 Play 登录:
  1. 用一个此前在服务端已有六计数器非零值的账号登录(或先经服务端段 PC1 给某账号变更过六计数器)。
  2. 进主菜单 → 开始游戏进玩法窗,观察女神等级/评级、盲盒计数、神庙修缮进度显示是否 = 服务端权威值(而非本地 PlayerPrefs 旧值)。可对照服务端登录快照日志(`G2C_PlayerInfoSnapshot` 的 Properties 含 Type=7..12)。
  3. 回主菜单看 BEST 数字:应 = 排行榜个人最佳分(此前玩到过的最高分入榜后的值)。
- 预期结果:六计数器显示 = 服务端快照值;主菜单 BEST = 排行榜个人最佳分投影;无 Console 红。
- 已知风险/复核重点:①首次登录(该账号服务端六计数器均为 0)时,玩法窗显示应为初始态(女神 1 级、盲盒 0、无神庙修缮),不被本地旧缓存盖。②BEST 若显示 0 而排行榜里其实有分,核对 `RankService.GetMyBest(1)` 缓存是否已被登录/查榜刷新过——本地投影缓存在从未查榜/提交的全新会话可能暂为 0(离线可丢缓存,查一次榜或玩一局终局入榜后即刷新),属预期,非 bug。
- (依赖 MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

### [ ] EC4 · 六计数器玩法变更经 PropertyChange 落账并对账(起服手测)

- 测什么:玩法中推进六计数器(全清推女神评级/升级、攒盒/开盒改盲盒、修神庙改已修厅数 + 游标 + 章节)时,落盘边界把净增量经 `C2G_PropertyChange` 上报服务端,服务端限界信任落账 + delta-push 回,本地视图以服务端 NewAmount 对账。
- 怎么测:前置同 EC3 起服。客户端进玩法窗:
  1. 触发全清(清空棋盘)→ 女神评级 +1(满档则升级 + 章节 +1);开/攒盲盒 → 盲盒计数变化;攒够虔诚币修一座神庙 → 已修厅数 +1、修缮游标 +1、章节可能 +1。
  2. 每次玩法事件后(落盘边界)观察服务端日志:应有对应 Type(7..12)的 `PlayerProperty 变更成功 ... type=GoddessRating/BlindBoxCount/... delta=... newAmount=...` + delta-push。
  3. 断开重连再登录 → 快照回带的六计数器应 = 刚才变更后的值(证明落库持久,非本地态)。
- 预期结果:六计数器玩法变更均经 PropertyChange 上报落账 + 对账;重登后值保持;盲盒开盒发净负 delta(可减)也正常。无 Console 红、无重复上报(同一事件每计数器一笔)。
- 已知风险/复核重点:①迁这六个后,落子/元动作在落盘边界会多发几笔 `C2G_PropertyChange`(这几个计数器的增量)——这是「步步上服务端」的预期代价,聚合上报(仅非零 delta)已限频。若观察到频繁 `RateLimited` 拒(100ms 频率闸),说明落盘边界触发过密,回报以便评估节流。②神庙已修厅数(TempleRepaired)与修缮游标(NextRepairIndex)每修一厅同步 +1,会各发一笔 delta=+1,两笔值恒相等,属预期(服务端两个独立 PropertyType)。
- (依赖 MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

### [ ] EC5 · blob 不再含六计数器 + highScore + 终局结算页最佳分正确(起服手测)

- 测什么:①云存档上传的 blob 里不再含六计数器 + highScore(sanitize 清零);下载合并不用 blob 旧值盖服务端权威;②玩到 jam 终局,结算页最佳分显示服务端权威 BestScore,且刷进排行榜投影使主菜单 BEST 同步。
- 怎么测:前置同 EC3 起服。
  1. 玩一局改变六计数器 + 拿一个高分,退出让云存档上传(落盘边界节流上传);可选:在服务端 Mongo 查该玩家云存档 blob 文档,确认其 meta 段六计数器 + highScore 均为 0(未入 blob)。
  2. 玩到棋盘 jam 触发终局 → 结算面板显示「最佳分 <数字>」= 服务端权威 BestScore(与服务端日志 `[BlockBlast] GameOver ... totalBest=...` 一致);若本局是新高,该值应 > 之前。
  3. 点「返回」回主菜单 → BEST 应已反映本局最佳分(结算时已把服务端 BestScore 刷进排行榜投影缓存,无需再查榜)。
- 预期结果:blob 不含六计数器 + highScore;结算页最佳分 = 服务端权威 BestScore;终局后主菜单 BEST 与之一致。无 Console 红。
- 已知风险/复核重点:①BestScore=0(入榜服务不可用)时结算页「最佳分 0」属预期(不抹既有排行榜投影;仅 BestScore>0 才刷缓存)。②若终局后返回主菜单 BEST 未更新,核对 `MergeOrderWindow.ShowGameOverSettlement` 是否调了 `Rank.SubmitScore(1, bestScore)`(取较大者刷投影),以及主菜单 `LoadBestScoreFromRank` 读的是 rankId=1。
- (依赖 MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

---

## 待测条目(改名服务端权威·服务端段·2a)

> 服务端工程 `Fantasy/`(分支 `block`)。本轮把昵称 + 改名次数迁上服务端权威,顺带把改名扣钻收成服务端裁定:
> 新增 `C2G_RenameRequest` / `G2C_RenameResponse`(Outer RPC,Gate Scene);PlayerDoc 加 `RenameCount`;登录快照 `G2C_PlayerInfoSnapshot` 加 `RenameCount`;改名费镜像到服务端 `RenameConfigServer`。
> 请求只带新昵称,费用/次数一律服务端按自己的 `PlayerDoc.RenameCount` 派生(RenameCount==0 免费,否则 `RenameConfigServer.PriceFor`=固定 100 钻);扣钻走 `PlayerPropertyServiceHelper.ChangeProperty(Diamond, -cost, serverAuthoritative:true, reason="player_rename")`(钻不足回拒、不改名),扣成功(或免费)后原子 `set Nickname + inc RenameCount`。
> dev 已自检:Entity + Hotfix 项目单编各 0 警告 0 错误(整 `Server.sln` 仅剩已知的 MSB3027/MSB3021 文件锁——遗留 `Main` 进程占 `examples/Bin/Debug/net8.0/` dll,非编译错;编译本身已过);协议已导出,客户端生成物已自动同步进 UnityProject(`Assets/Fantasy/Generate/NetworkProtocol/` 4 文件,与服务端生成物同一 diff、byte 对齐);源生成器已把 `C2G_RenameRequestHandler` 编入 Hotfix.dll。
> schema 版本已由 5 升至 6(旧档登录经 `MigrateSchemaIfNeeded` 用 `$ifNull` 补 `RenameCount=0`)。
> **opcode 说明(重要,已两端同步)**:新增 Rename 两条消息导出时占用了 `C2G_RenameRequest=268445486`/`G2C_RenameResponse=402663214`,原 `C2G_ClearPlayerDataRequest/Response` 顺移到 `268445487/402663215`。**客户端与服务端生成物在同一次导出中一起重生成、两端 opcode 表 byte 完全一致**,故此位移无害;但前提是两端生成物一并生效(勿只更新一端)。
> RT1 需用户在能起服(且本机可达 MongoDB)的环境手测;RT0 是纯编译核对(有 .NET 8 SDK 即可,先停遗留 Main 进程)。前置同 ST2/SST1:起服 / 跑 `Server.sln` 前先停掉遗留 `Main` 进程(占 dll 锁)。

### [ ] RT0 · 服务端编译通过(Entity + Hotfix 干净;整 sln 除文件锁外无编译错)

- 测什么:新增改名 RPC handler + RenameHelper + RenameConfigServer + PlayerDoc.RenameCount + 快照下发 + schema 迁移改动后,服务端能否干净编译,源生成器把新 handler 正确注册。
- 怎么测:先停掉遗留 `Main` 进程;命令行 `cd D:\work\TEngine_block\Fantasy` 后分别:
  - `dotnet build examples/Server/APP/Entity/Entity.csproj`(应 0 警告 0 错误)
  - `dotnet build examples/Server/APP/Hotfix/Hotfix.csproj`(应 0 警告 0 错误)
  - 可选整 sln:`dotnet build examples/Server/Server.sln`(若停干净遗留进程应 `已成功生成`;若仍报 MSB3027/MSB3021「文件被 Main(...) 锁定」,是遗留进程没停干净的环境问题,非本轮代码,停掉重试)。
- 预期结果:Entity / Hotfix 单编各 `已成功生成` 0 警告 0 错误;整 sln 干净或仅剩文件锁环境错。

### [ ] RT1 · 改名 RPC 往返 + 快照回带 + 重登持久(起服手测)

- 测什么:改名 RPC 在真实服务端往返;首次免费 / 二次扣钻 / 钻不足拒 / 名字非法拒四条分支各自正确;登录快照回带 Nickname + RenameCount;改名后重登持久。
- 怎么测:停遗留 `Main` → 起服 `dotnet run --project examples/Server/APP/Main/Main.csproj -- --m Develop`(需本机 MongoDB 可达)。用客户端(或测试客户端)登录后依次:
  1. **首次免费改名**:发 `C2G_RenameRequest{ NewNickname="阿狸" }` → 响应 `ResultCode=Success(0)`、`Nickname="阿狸"`、`RenameCount=1`、`Diamond=<不变的当前余额>`;服务端日志 `改名成功 account=... nickname='阿狸' renameCount=1 cost=0 diamond=...`。
  2. **二次扣钻改名(次数递增)**:先确保该账号有 ≥100 钻(可先发 `C2G_PropertyChange{Type=Diamond, Delta=+500, Reason="test_grant"}` 充值),再发 `C2G_RenameRequest{ NewNickname="狐狸" }` → `ResultCode=Success`、`Nickname="狐狸"`、`RenameCount=2`、`Diamond=<扣 100 后的余额>`;日志 `... renameCount=2 cost=100 ...`,并伴随一条 Diamond 的 `PlayerProperty 变更成功 ... type=Diamond delta=-100` + delta-push。
  3. **钻不足拒**:把钻石花到 <100(或用没充值的账号做第二次改名),发 `C2G_RenameRequest{ NewNickname="任意名" }` → `ResultCode=NotEnoughDiamond(3)`、`Nickname` 回带**当前权威昵称(未改)**、`RenameCount` 不变、`Diamond=<当前余额>`;服务端**无**「改名成功」日志。
  4. **名字非法拒**:发 `C2G_RenameRequest{ NewNickname="" }`(空串)或一个 >16 字符的超长名 → `ResultCode=InvalidName(2)`、回带当前昵称/次数、不扣钻、不改名。
  5. **快照回带 + 重登持久**:断开重连再登录 → 观察服务端登录快照日志 `G2C_PlayerInfoSnapshot`,其 `Info.Nickname` = 最后一次成功改名的名、`Info.RenameCount` = 累计成功次数(证明落库持久,非本地态)。
- 预期结果:四分支各返对应 ResultCode;成功时 Nickname/RenameCount 递进、扣钻分支 Diamond 扣 100;失败分支不改名不扣钻;重登快照回带最新 Nickname + RenameCount。无服务端异常红日志。
- (依赖 MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

## 改名服务端权威·客户端段(2a)

> dev 已自检:主工程 batchmode 编译 0 CS 错误 + EditMode 全测通过(599 用例 581 passed / 18 skipped / 0 failed,含本轮改名 RPC 成功/钻不足/非法/服务不可用/本地拦截各分支 + blob 剔除断言)。以下为门覆盖不到的部分(PlayMode / UI 手感 / 真机真服往返),交用户手测。
> 客户端改名已改走服务端权威 RPC(`C2G_RenameRequest`),不再本地扣钻、不再本地写名、不再落 blob;昵称显示读服务端权威快照 `PlayerAttrService.Nickname`。本节须与服务端段 RT1(起服往返)配套:客户端跑通的前提是服务端已起、协议两端 opcode 一致。

### [ ] CT1 · 改名走 RPC:首次免费改名成功(端到端)

- 测什么:客户端改名不再本地扣钻,而是发 RPC 由服务端裁决;首次(RenameCount=0)免费。
- 怎么测:登录进游戏(需服务端已起 + 可达),打开个人信息窗(PlayerInfoWindow)→ 点昵称旁编辑铅笔进改名态 → 输入合法新名(如「阿狸」)→ 确认(失焦提交)。
- 预期结果:昵称显示立即变为新名;钻石余额不变(首次免费);服务端日志有 `改名成功 ... cost=0`。若客户端有网络抓包/日志,应看到发出 `C2G_RenameRequest{NewNickname="阿狸"}` 并收到 `Success`。
- 已知风险:若服务端未起 / 未登录,应提示「网络异常」类文案且昵称不变(不冒进本地改名)——顺带验证这条降级。

### [ ] CT2 · 二次改名扣钻(服务端扣、客户端对账)

- 测什么:非首次改名由服务端扣 100 钻,客户端钻石视图按响应回带余额对齐(不客户端自扣)。
- 怎么测:承 CT1(此时 RenameCount≥1)。先确保账号钻石 ≥100(可经充值途径或服务端授予)。再次改名输入合法新名 → 确认。
- 预期结果:昵称变为新名;钻石余额减少 100(由服务端响应回带的新余额刷新,不是客户端本地先扣);服务端日志 `改名成功 ... cost=100` + 一条 Diamond 变更。观察钻石行数字与服务端扣后余额一致。

### [ ] CT3 · 钻石不足拒绝(不改名、视图对齐服务端)

- 测什么:钻石 <100 时二次改名被服务端拒,客户端不改名、钻石/昵称对齐服务端当前权威值。
- 怎么测:把账号钻石花到 <100(或用没充值、已改过一次名的账号)→ 再次改名输入新名 → 确认。
- 预期结果:提示「钻石不足」;昵称保持原样(未改);钻石余额显示服务端当前值(不是被本地虚扣)。服务端无「改名成功」日志。

### [ ] CT4 · 名字非法本地拦截(不发 RPC)

- 测什么:空名 / 全空白 / 超长(>16 字符)在客户端本地即被拦,不发 RPC(减一次往返)。
- 怎么测:改名态输入 ①空(直接确认)、②全空格、③粘贴一个 >16 字符的超长串,分别确认。
- 预期结果:各自提示对应文案(「名字不能为空」/「名字过长(上限 16)」);昵称不变;此时不应有 `C2G_RenameRequest` 发出(本地拦截)。若能看网络日志,确认这三种输入零 RPC。

### [ ] CT5 · 昵称显示读服务端权威快照(重登/切换验证)

- 测什么:PlayerInfoWindow 昵称读 `PlayerAttrService.Nickname`(登录快照下发),不再读本地 blob 的 `PlayerInfo.Name`。
- 怎么测:改名成功后完全退出客户端重登(或断线重连触发重新登录快照)→ 再打开个人信息窗看昵称。
- 预期结果:昵称显示 = 最后一次服务端成功改名的名(来自登录快照 Nickname),重登后仍持久;改名次数(内部 RenameCount)随快照回带,下次改名费预告据服务端权威次数计算。

### [ ] CT6 · Diamond 对账无客户端自报 player_rename

- 测什么:改名扣钻不再由客户端自发 `C2G_PropertyChange(Diamond, player_rename)`,钻石余额只由改名响应 + 服务端 delta-push 对齐。
- 怎么测:二次改名(CT2)时若能抓客户端上行包 / 看流水,确认扣钻不是客户端发的 PropertyChange 请求驱动的;打开「我的流水」窗查改名那笔钻石变动的来源是否为服务端记账(而非客户端自报)。
- 预期结果:改名扣钻在流水里体现为服务端裁决的一笔;客户端未发独立的 player_rename PropertyChange 请求。钻石视图最终与服务端一致。

### [ ] CT7 · blob 不含 playerName / playerRenameCount / playerExp(清档/云存档验证)

- 测什么:云存档 blob 上传时已剔除昵称 / 改名次数 / 账号经验三字段(改名服务端权威,不入 blob、不被下载回灌覆盖服务端权威)。
- 怎么测(偏工程侧,能看本地存档 / 抓上传包则可验):改名成功后触发一次云存档上传(进主游戏 / 存档边界)→ 若能 dump 上传的 blob JSON,确认 metaJson 内 `playerName` 为空、`playerRenameCount`=0、`playerExp`=0(与货币/身份一并被清)。反向:模拟一个含旧 playerName 的下载 blob 回灌,确认不会用它冲掉当前服务端权威昵称显示。
- 预期结果:上传 blob 的 metaJson 三字段已清;下载回灌不改昵称显示(昵称只认服务端快照)。此条 EditMode 已用 C1_BuildBlob 断言覆盖 strip 逻辑,手测重点在「真实上传/下载链路里也确实如此」。
- 已知风险:此条需能观察 blob 内容(工程日志 / 抓包),纯玩家视角不易验;若无手段,可依赖 EditMode C1 断言,标注「blob 内容层由单测保障」。

## 待测条目(头像/框服务端权威·服务端段·2c)

> 服务端工程 `Fantasy/`(分支 `block`)。本轮把头像 / 头像框迁上服务端权威:当前佩戴 id(头像 + 框)+ 已解锁集合(头像 + 框)服务端持有唯一事实源。
> 新增 `C2G_EquipCosmeticRequest`/`G2C_EquipCosmeticResponse`(换装 RPC)与 `C2G_UnlockCosmeticRequest`/`G2C_UnlockCosmeticResponse`(解锁上报 RPC),均 Outer RPC、Gate Scene、身份从会话 `GateAccountFlagComponent` 取(不接受客户端上报账号)。
> `PlayerDoc` 加 4 字段:`CurrentAvatarId`(int)/`CurrentFrameId`(int)/`UnlockedAvatarIds`(List<int>)/`UnlockedFrameIds`(List<int>)。登录快照 `PlayerInfo` 加对应 4 字段(2 个当前 id + 2 个 repeated 解锁集)。
> 换装:服务端校验目标 id 已在对应解锁集合内(未解锁拒 `NotUnlocked`),通过则原子 `$set` 当前 id,响应回带最新当前头像 id + 框 id。
> 解锁上报(client-report 限界信任):sanity(id 落合法段:头像 [1,100] / 框 [101,100000];集合大小上限 4096 防灌爆;同账号 100ms 频率闸)→ 原子 `$addToSet` 幂等加入集合(重复上报同 id 无副作用),响应回带更新后集合。
> **默认值对齐策略(选项 2 变体)**:当前佩戴 id 缺省与客户端默认对齐(头像 1 / 框 101,= 客户端 `PlayerInfo.DefaultAvatarId`/`DefaultFrameId`),使客户端登录拉快照时不会因服务端「未佩戴」误判;**已解锁集合首登缺省空**,由客户端登录后 bootstrap 上报默认解锁(id 1、id 101)填入。选这个而非「当前 id 缺省 0」是为避免客户端把 0 当「无佩戴」而闪默认头像;不在服务端建整套头像等级配置自算(饰品低危,解锁走 client-report,与 boss 决策一致)。
> schema 版本已由 6 升至 7(旧档登录经 `MigrateSchemaIfNeeded` 用 `$ifNull` 补当前 id 默认 + 解锁集合空数组);清档 `ResetToNewbie` 一并把 4 字段重置为默认。
> **opcode 说明(重要,已两端同步,本轮再位移一次)**:新增 Cosmetic 两条 RPC 导出时排在 proto 处理序中段(文件名 `CosmeticMessage.proto` 排在 `EnterMainGame` 之前),占用 `C2G_EquipCosmeticRequest=268445464`/`G2C_EquipCosmeticResponse=402663192`/`C2G_UnlockCosmeticRequest=268445465`/`G2C_UnlockCosmeticResponse=402663193`,其后所有 request/response opcode 顺移 +2(例:`C2G_RenameRequest` 现为 `268445488`、`C2G_ClearPlayerDataRequest` 现为 `268445489`)。**客户端与服务端生成物在同一次导出中一起重生成、两端 opcode 表 byte 完全一致**(dev 已 diff 核对 `OuterMessage.cs`/`OuterEnum.cs`/`OuterOpcode.cs` 服务端与客户端 4 文件逐字节相同),故此位移无害;前提是两端生成物一并生效(勿只更新一端,开发阶段无历史包袱、无旧连接)。
> dev 已自检:Entity + Hotfix 项目单编各 0 警告 0 错误(整 `Server.sln` 仅剩已知的 MSB3027/MSB3021 文件锁——遗留 `Main` 进程占 `examples/Bin/Debug/net8.0/` dll,非编译错);协议已导出,客户端生成物已自动同步进 UnityProject(`Assets/Fantasy/Generate/NetworkProtocol/`);源生成器已把两个新 handler 编入 Hotfix.dll(RPC ResponseType 绑定正确)。
> AT0 是纯编译核对(有 .NET 8 SDK 即可,先停遗留 Main 进程);AT1/AT2 需用户在能起服(且本机 MongoDB 可达)的环境手测。前置:起服 / 跑 `Server.sln` 前先停掉遗留 `Main` 进程(占 dll 锁)。

### [ ] AT0 · 服务端编译通过(Entity + Hotfix 干净;整 sln 除文件锁外无编译错)

- 测什么:新增换装 / 解锁 RPC handler + CosmeticHelper + PlayerDoc 4 字段 + 组件配置 + 快照下发 + schema 迁移改动后,服务端能否干净编译,源生成器把两个新 handler 正确注册。
- 怎么测:先停掉遗留 `Main` 进程;命令行 `cd D:\work\TEngine_block\Fantasy` 后分别:
  - `dotnet build examples/Server/APP/Entity/Entity.csproj`(应 0 警告 0 错误)
  - `dotnet build examples/Server/APP/Hotfix/Hotfix.csproj`(应 0 警告 0 错误)
  - 可选整 sln:`dotnet build examples/Server/Server.sln`(若停干净遗留进程应 `已成功生成`;若仍报 MSB3027/MSB3021「文件被 Main(...) 锁定」,是遗留进程没停干净的环境问题,非本轮代码,停掉重试)。
- 预期结果:Entity / Hotfix 单编各 `已成功生成` 0 警告 0 错误;整 sln 干净或仅剩文件锁环境错。

### [ ] AT1 · 换装 RPC 往返:已解锁可换 / 未解锁拒(起服手测)

- 测什么:换装 RPC 在真实服务端往返;目标已解锁则切换当前 id、未解锁则拒(NotUnlocked)、Kind 非法则拒(InvalidKind);响应回带当前权威两个 id。
- 怎么测:停遗留 `Main` → 起服 `dotnet run --project examples/Server/APP/Main/Main.csproj -- --m Develop`(需本机 MongoDB 可达)。用客户端(或测试客户端)登录后:
  1. **先解锁再换装(正路)**:先发 `C2G_UnlockCosmeticRequest{ Kind=1, Id=2 }`(解锁头像 id=2)→ 再发 `C2G_EquipCosmeticRequest{ Kind=1, Id=2 }` → 响应 `ResultCode=Success(0)`、`CurrentAvatarId=2`、`CurrentFrameId=101`(框未动,保持默认);服务端日志 `换装成功 ... kind=1 id=2 currentAvatar=2`。
  2. **换框**:先 `C2G_UnlockCosmeticRequest{ Kind=2, Id=102 }` → 再 `C2G_EquipCosmeticRequest{ Kind=2, Id=102 }` → `ResultCode=Success`、`CurrentFrameId=102`、`CurrentAvatarId` 保持上一步的 2。
  3. **未解锁拒**:发 `C2G_EquipCosmeticRequest{ Kind=1, Id=50 }`(id=50 未解锁)→ `ResultCode=NotUnlocked(3)`、回带当前权威两个 id(未切换,即头像仍 2 / 框仍 102);无「换装成功」日志。
  4. **Kind 非法拒**:发 `C2G_EquipCosmeticRequest{ Kind=9, Id=2 }` → `ResultCode=InvalidKind(2)`、回带当前两个 id、不切换。
- 预期结果:已解锁换装成功并切换对应种类当前 id;未解锁 / Kind 非法各返对应码且不切换、回带当前权威值。无服务端异常红日志。
- (依赖 MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

### [ ] AT2 · 解锁上报 RPC:加集合 / 幂等重报 / sanity 拒非法(起服手测)

- 测什么:解锁上报把 id 幂等加入对应集合;重复上报同 id 无副作用;越段 id / Kind 非法被 sanity 拒;高频上报被频率闸拒。
- 怎么测:承 AT1 起服环境,登录后:
  1. **首次解锁加集合**:发 `C2G_UnlockCosmeticRequest{ Kind=1, Id=3 }` → `ResultCode=Success(0)`、`UnlockedIds` 回带的头像解锁集合含 3;服务端日志 `解锁上报成功 ... kind=1 id=3`。
  2. **幂等重报**:紧接着(间隔 >100ms 避开频率闸)再发同一条 `{ Kind=1, Id=3 }` → 仍 `ResultCode=Success`、`UnlockedIds` 集合大小不变(仍只一个 3,不重复);无报错。
  3. **越段 id 拒**:发 `C2G_UnlockCosmeticRequest{ Kind=1, Id=500 }`(头像段 [1,100],500 越界)→ `ResultCode=InvalidId(3)`;发 `{ Kind=2, Id=50 }`(框段 [101,100000],50 越界)→ 同样 `InvalidId`;日志 `拒因=InvalidId`。集合不变。
  4. **Kind 非法拒**:发 `{ Kind=0, Id=3 }` 或 `{ Kind=5, Id=3 }` → `ResultCode=InvalidKind(2)`。
  5. **频率闸拒(可选)**:100ms 内连发两条不同合法解锁(如 `{1,4}` 紧跟 `{1,5}`)→ 第二条 `ResultCode=RateLimited(5)`、日志 `拒因=RateLimited`;间隔 >100ms 再发则正常成功(证明只是限速非永久拒)。
- 预期结果:合法解锁幂等入集合(重报不增大小);越段 / Kind 非法 / 高频各返对应码且不改集合。无服务端异常红日志。

### [ ] AT3 · 登录快照回带 4 字段 + 重登持久(起服手测)

- 测什么:登录快照 `PlayerInfo` 回带当前佩戴 id + 已解锁集合 4 字段;换装 / 解锁后重登持久(证明落库,非本地态)。
- 怎么测:承 AT1/AT2(此时该账号已换头像=2、框=102,解锁集合含若干 id)。断开重连再登录 → 观察服务端登录快照日志 `G2C_PlayerInfoSnapshot`,其 `Info.CurrentAvatarId`=2、`Info.CurrentFrameId`=102、`Info.UnlockedAvatarIds` 含此前解锁的头像 id、`Info.UnlockedFrameIds` 含此前解锁的框 id。
- 预期结果:重登快照回带最新当前佩戴 id + 完整解锁集合,与此前操作一致(持久)。全新账号首登快照:`CurrentAvatarId=1`/`CurrentFrameId=101`、两解锁集合为空(等客户端 bootstrap 上报默认解锁后才填)。
- (依赖 MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

## 头像/框服务端权威·客户端段(2c)

> 客户端段:换装走 RPC(乐观+对账)、登录 bootstrap 上报默认解锁、当前佩戴/解锁集读快照、blob 剔除 4 字段、重登持久。
> 编译 + EditMode 单测已过 batchmode 自检门(0 编译错误、CosmeticServiceTests 11 项 + 全量 610 测全绿);以下为门覆盖不到的联机/持久面,需起服 + 客户端连真服手测。

### [ ] CT0 · 客户端编译通过(最基本项,用户 Unity 编)

- 测什么:本批新增/改动的客户端代码在 Unity 编辑器里编译 0 error。
- 怎么测:打开 Unity 编辑器(FANTASY_UNITY 已开),等待编译完成,看 Console 无红色编译错误(warning 不卡)。重点看新增文件 `CosmeticResult.cs / ICosmeticGateway.cs / CosmeticGatewayProd.cs / CosmeticService.cs`、改动的 `GameContext.cs / GameApp.cs / FantasyNetwork.cs / G2C_PlayerInfoSnapshotHandler.cs / CloudSaveCodec.cs`。
- 预期结果:编译通过,无 error。
- (信心来源:batchmode 已编过、只引用生成物里确在的消息/字段/枚举;仍以用户本机 Unity 编为准。)

### [ ] CT1 · 换装走 RPC:已解锁可换 / 未解锁回滚 + 提示(连真服手测)

- 测什么:客户端换装经 `CosmeticService.EquipAsync`:乐观即时切显示 → 发 `C2G_EquipCosmetic` → 响应对账。
- 怎么测:起服 + 客户端连真服登录。当前无正式头像选择网格 UI(PlayerInfoWindow「编辑头像」仍是占位),故本条经临时入口手测:在能拿到 `GameContext.Instance.Cosmetic` 的调试入口(或临时按钮)对某已解锁头像 entry 调 `EquipAsync(player, entry)`,再对一个未解锁 entry 调一次。
  1. **已解锁换装**:选一个服务端解锁集里已有的头像(如首登默认头像 1 或已 bootstrap 上报的 id)→ 换装后 `PlayerInfoWindow` 头像占位色随 `CurrentAvatarId` 改变;服务端日志 `换装成功`;`EquipCosmeticResult.Success=true`。
  2. **未解锁回滚**:选一个服务端解锁集里没有的头像 id → 客户端先乐观切显示(短暂)→ 响应 `NotUnlocked` → 显示回退到服务端当前佩戴 id(不停在未解锁 id);有提示/日志。
  3. **断网**:关服后换装 → 乐观切显示后回滚到发前旧值,不停在目标 id;提示网络异常。
- 预期结果:已解锁换装成功并持久;未解锁乐观切后回退到服务端当前值;断网回退到旧值。全程不崩。
- 已知风险:目前无正式换装网格 UI,需临时调试入口触发 `EquipAsync`;正式 UI 落地后此路径复测。

### [ ] CT2 · 登录 bootstrap 上报默认解锁(连真服手测,首登账号)

- 测什么:全新账号首登(服务端解锁集空)后,客户端 bootstrap 把「按等级算出的应解锁集(含默认头像 1 / 框 101)」与服务端空集做差、对缺的每个 id 发一次 `C2G_UnlockCosmetic` 补齐;只报差集。
- 怎么测:用一个从未登录过的全新账号连真服登录。观察:
  1. 服务端收到若干 `C2G_UnlockCosmeticRequest`,至少含 `{Kind=1,Id=1}`(默认头像)与 `{Kind=2,Id=101}`(默认框),外加当前等级已达标的 LEVEL 头像/框 id;各返 `Success`。
  2. 重登该账号 → 登录快照 `UnlockedAvatarIds` 含 1(及已达标项)、`UnlockedFrameIds` 含 101(证明 bootstrap 已落库)。
  3. **只报差集**:再次重登(此时服务端集已含默认)→ bootstrap 不再重发已含的 id(客户端只对服务端集缺的 id 发上报);服务端不应收到对 1/101 的重复上报(幂等,重报也无害但应尽量不发)。
- 预期结果:首登补报默认 + 达标解锁并落库;重登只报仍缺的差集,不全量重发。
- 已知风险:bootstrap 依赖 `AvatarConfigMgr.All()`(配置表);配置未就绪时退「只兜默认 id」,请确认头像配置表在登录快照到达前已加载(否则只补默认 1/101,达标项延后到配置就绪后的下次登录/事件补)。

### [ ] CT3 · 当前佩戴 / 解锁集登录读快照(连真服手测)

- 测什么:客户端当前佩戴 + 解锁集的权威源是登录快照(不再读本地 blob)。
- 怎么测:
  1. 登录后打开 `PlayerInfoWindow`,头像占位色对应服务端快照 `CurrentAvatarId`(默认 1 时为 id=1 的稳定色)。
  2. **服务端改值验证权威**:在服务端(或经 CT1 换装)把当前头像改成 2 → 客户端重登 → 头像显示随快照变为 id=2 的色(证明读快照非读本地旧 blob)。
  3. **本地篡改被覆盖**:手动改本地存档 blob 里 curAvatarId(若可达)→ 重登 → 显示仍以服务端快照为准,不被本地 blob 值影响。
- 预期结果:显示 / 换装当前态 / 解锁判定均以服务端快照为准;登录不闪默认头像(服务端缺省 1/101 与客户端默认一致)。

### [ ] CT4 · blob 不含 4 字段(可离线手测,不需连服)

- 测什么:云存档 blob 组装 / 回灌都不含 curAvatarId/curFrameId/unlockedAvatarIds/unlockedFrameIds(绝不入 blob、不被下载回灌覆盖服务端权威)。
- 怎么测:本条已由 EditMode `CloudSaveTests.C1/C2` 覆盖(自检门已过):C1 断言 BuildBlob 后这 4 字段清零/清空;C2 断言 ApplyBlob 回灌不带这 4 字段。用户如需人工复核:在离线包里触发一次存档上传/下载,确认 blob JSON 里这 4 字段为 0 / 空。
- 预期结果:blob 里这 4 字段恒为 0 / 空;下载回灌不覆盖服务端权威当前佩戴 / 解锁集。

### [ ] CT5 · 重登头像持久(连真服手测,端到端)

- 测什么:换装 + 解锁后彻底重登(断连接重连),头像与解锁集持久(证明服务端落库,非本地态)。
- 怎么测:CT1 换到头像 2、CT2 已 bootstrap 解锁若干 → 完全退出重连重登 → `PlayerInfoWindow` 头像仍为 2;若有解锁网格,已解锁项仍显解锁态。清掉本地存档后重登 → 仍从服务端快照恢复为 2 + 完整解锁集(证明与本地 blob 无关)。
- 预期结果:重登后当前佩戴 + 解锁集与操作后一致,且清本地档也能从服务端快照恢复。

## 待测条目(祈愿服务端权威·服务端段·3a)

> 服务端工程 `Fantasy/`(分支 `block`)。本轮把祈愿(每日限领体力兑换)迁上服务端权威:每日祈愿次数闸 + 灵力扣 + 体力发一律服务端裁决。
> 新增 `C2G_WishForEnergyRequest`/`G2C_WishForEnergyResponse`(祈愿 RPC,Outer RPC、Gate Scene、身份从会话 `GateAccountFlagComponent` 取,请求无载荷——不带账号 / 费用 / 次数)。
> `PlayerDoc` 加 2 字段:`WishUsedToday`(int,今日已用次数)/`WishLastResetUnixMs`(long,上次每日重置时刻 UTC ms)。登录快照 `PlayerInfo` 加 `WishUsedToday` + `WishDailyLimit` 两字段。
> 配置镜像 `WishConfigServer`(= 客户端 `MergeOrderConfig` 同名常量):`WishSoulCost=20`、`WishEnergyGain=10`、`WishDailyLimit=3`;体力软上限复用 `MergeOrderConfigServer.EnergyCap`(读 Luban global id=4,默认 30)不另镜像,避免同一软上限两处分叉。
> RPC 裁决顺序(`WishHelper.TryWish`):① 懒每日重置(`ResetWishIfDue`:按服务端本地日期 `TimeHelper.Now.TransitionLocal().Date` 判跨天,跨天则 `WishUsedToday=0` + 刷新 `WishLastResetUnixMs`,CAS `WishLastResetUnixMs==读值` 防并发双重置)→ ② 门控(重置后 `WishUsedToday >= WishDailyLimit` → `DailyLimitReached`,不扣不发)→ ③ 扣灵力(`ChangeProperty(SoulPower, -20, serverAuthoritative:true, reason="wish")`;灵力不足 `NotEnough` → `NotEnoughSoul`、不发体力)→ ④ 发体力(先 `ReadEnergyAuthoritative` 读权威体力 E 含懒恢复结算,实发 `netDelta = min(EnergyCap, E+10) - E`;E 已到/超软上限时 `netDelta<=0` 不发,只扣灵力——复刻客户端 `Math.Min(EnergyCap, Energy+gain)`)→ ⑤ `WishUsedToday+1`(原子 `$inc`)。成功各资源变更起 delta 推送对齐 HUD。
> 体力夹 cap 与落子派生口径协调:祈愿发体力沿用与落子完全相同的「`ReadEnergyAuthoritative`(await 结算恢复取 E)→ 同步算 netDelta(无 await)→ `ChangeProperty($inc netDelta)`」范式;`ChangeProperty` 内部恢复结算因同 tick 变 no-op,两处不打架。
> 登录快照:`InitOrLoad` 在下发快照前先跑一次 `ResetWishIfDue`,使客户端登录看到的 `WishUsedToday` 是重置后的当日值。
> schema 版本已由 7 升至 8(旧档登录经 `MigrateSchemaIfNeeded` 用 `$ifNull` 补 `WishUsedToday=0` + `WishLastResetUnixMs=nowMs`,补 nowMs 而非 0 避免旧档补齐当次即被判 1970 年跨天);清档 `ResetToNewbie` 一并重置这 2 字段。
> **opcode 说明(已两端同步,本轮位移一次)**:新增 Wish 两条 RPC 导出时 `WishMessage.proto` 排在处理序末段(`Wish` 排在 `ClearPlayerData` 之前),占用 `C2G_WishForEnergyRequest=268445489`/`G2C_WishForEnergyResponse=402663217`,其后仅 `C2G_ClearPlayerDataRequest`/`G2C_ClearPlayerDataResponse` 顺移 +2(现为 `268445490`/`402663218`)。因本轮导出同时把工作树里此前已加但未导出的 Cosmetic proto 一并纳入扫描序,故相对上一次提交的服务端生成物,中段 opcode 另有 +2 整体位移——**客户端与服务端生成物在同一次导出中一起重生成、两端 opcode 表 byte 完全一致**(dev 已 diff 核对 `OuterOpcode.cs`/`OuterEnum.cs`/`OuterMessage.cs` 服务端与客户端 3 文件逐字节相同),故位移无害;前提两端生成物一并生效(勿只更新一端,开发阶段无历史包袱、无旧连接)。
> dev 已自检:Entity + Hotfix 项目单编各 0 警告 0 错误(整 `Server.sln` 仅剩已知的 MSB3027/MSB3021 文件锁——遗留 `Main` 进程占 `examples/Bin/Debug/net8.0/` dll,非编译错);协议已导出,客户端生成物已自动同步进 UnityProject(`Assets/Fantasy/Generate/NetworkProtocol/`);源生成器已把新 handler 编入 Hotfix.dll(RPC ResponseType 绑定正确)。
> WT0 是纯编译核对(有 .NET 8 SDK 即可,先停遗留 Main 进程);WT1/WT2/WT3/WT4 需用户在能起服(且本机 MongoDB 可达)的环境手测。前置:起服 / 跑 `Server.sln` 前先停掉遗留 `Main` 进程(占 dll 锁)。

### [ ] WT0 · 服务端编译通过(Entity + Hotfix 干净;整 sln 除文件锁外无编译错)

- 测什么:新增祈愿 RPC handler + WishHelper + WishConfigServer + PlayerDoc 2 字段 + 快照下发 + schema 迁移改动后,服务端能否干净编译,源生成器把新 handler 正确注册。
- 怎么测:先停掉遗留 `Main` 进程;命令行 `cd D:\work\TEngine_block\Fantasy` 后分别:
  - `dotnet build examples/Server/APP/Entity/Entity.csproj`(应 0 警告 0 错误)
  - `dotnet build examples/Server/APP/Hotfix/Hotfix.csproj`(应 0 警告 0 错误)
  - 可选整 sln:`dotnet build examples/Server/Server.sln`(若停干净遗留进程应 `已成功生成`;若仍报 MSB3027/MSB3021「文件被 Main(...) 锁定」,是遗留进程没停干净的环境问题,非本轮代码,停掉重试)。
- 预期结果:Entity / Hotfix 单编各 `已成功生成` 0 警告 0 错误;整 sln 干净或仅剩文件锁环境错。

### [ ] WT1 · 祈愿 RPC 往返:可领(扣灵力 + 发体力 + 次数 +1)(起服手测)

- 测什么:祈愿 RPC 在真实服务端往返;灵力足 + 未达每日上限时,扣 20 灵力、发体力(夹软上限 30)、今日次数 +1;响应回带最新权威值。
- 怎么测:停遗留 `Main` → 起服 `dotnet run --project examples/Server/APP/Main/Main.csproj -- --m Develop`(需本机 MongoDB 可达)。用客户端(或测试客户端)登录后,先确保该账号有 ≥20 灵力、体力 <30(如体力已 30 满,先落子/消耗降到 30 以下,否则本条只扣灵力不涨体力,见 WT4)。发 `C2G_WishForEnergyRequest{}`:
  1. **首次祈愿**:响应 `ResultCode=Success(0)`;`SoulPower` = 扣前 -20 后余额;`Energy` = 发后余额(= min(30, E+10));`WishUsedToday=1`;`WishDailyLimit=3`。服务端日志 `祈愿成功 ... wishUsedToday=1/3`,并各起一次 SoulPower / Energy 的 delta 推送。
  2. **连领至上限**:再连发两次(每次间隔避开 100ms 频率闸虽走 serverAuthoritative 绕过,但保守起见)→ 第 2/3 次仍 `Success`、`WishUsedToday` 递增到 2、3;每次扣 20 灵力、发体力夹 cap。
- 预期结果:可领时扣 20 灵力、发体力夹到 ≤30、次数递增;响应回带扣后灵力 / 发后体力 / 新次数 / 上限 3。无服务端异常红日志。
- (依赖 MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

### [ ] WT2 · 祈愿门控:灵力不足拒 / 当日次数满拒(起服手测)

- 测什么:灵力 <20 时拒 `NotEnoughSoul`、不扣不发;今日已用满 3 次时拒 `DailyLimitReached`、不扣不发。
- 怎么测:承 WT1 起服环境:
  1. **灵力不足**:把账号灵力降到 <20(如经消耗,或用一个灵力不足的账号)→ 发 `C2G_WishForEnergyRequest{}` → `ResultCode=NotEnoughSoul(3)`;`SoulPower` 回带当前实际灵力(未扣)、`Energy` 未变、`WishUsedToday` 未增。无「祈愿成功」日志。
  2. **当日次数满**:承 WT1 已连领到 `WishUsedToday=3` 的账号(灵力仍充足)→ 再发一次 → `ResultCode=DailyLimitReached(2)`;灵力 / 体力均未变、`WishUsedToday` 仍 3。无扣发日志。
- 预期结果:灵力不足 / 次数满各返对应码且不扣灵力、不发体力、不加次数;回带当前权威值供客户端回退。无异常红日志。

### [ ] WT3 · 跨天懒重置(次数归零可再领)+ 登录快照回带 + 重登持久(起服手测)

- 测什么:跨服务端本地日期后,懒重置把 `WishUsedToday` 归零、可再领;登录快照回带 `WishUsedToday`(重置后当日值);重登持久。
- 怎么测:承 WT1/WT2(账号已 `WishUsedToday=3`)。制造跨天有两种方式,任选:
  - **改机器日期**:把服务端机器系统日期 +1 天(或改时区跨过本地午夜),重启服(或直接触发一次祈愿 / 重登)→ 祈愿或登录时 `ResetWishIfDue` 判跨天 → `WishUsedToday` 归零。
  - **改库时刻**:MongoDB 里把该账号 `players` 文档的 `WishLastResetUnixMs` 手动改成昨天某时刻的 ms → 下次祈愿 / 登录触发懒重置。
  1. **重置后可再领**:跨天后重登(或直接发祈愿)→ 若走登录:观察 `G2C_PlayerInfoSnapshot` 的 `Info.WishUsedToday=0`、`Info.WishDailyLimit=3`;再发 `C2G_WishForEnergyRequest{}` → `Success`、`WishUsedToday=1`(证明归零后重新计数)。服务端日志 `祈愿每日重置 ... wishUsedToday=0`。
  2. **同日不重置**:同一天内多次登录 / 祈愿,`WishUsedToday` 不被重置(沿用当日累计值,只跨天才归零)。
  3. **重登持久**:领若干次后(如 `WishUsedToday=2`)当天断连重登 → 登录快照 `Info.WishUsedToday=2`(同日持久,未被误重置);跨天再重登 → `Info.WishUsedToday=0`。
- 预期结果:跨服务端本地日期归零并可再领;同日不重置;登录快照回带重置后当日次数;重登在同日持久、跨天归零。无异常红日志。
- (依赖 MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

### [ ] WT4 · 体力已满软上限时祈愿只扣灵力不涨体力(夹 cap 边界,起服手测)

- 测什么:体力已 ≥ EnergyCap(30)时祈愿,复刻客户端 `Math.Min(EnergyCap, Energy+gain)` 语义 —— 灵力仍扣 20、次数仍 +1,但体力不涨(netDelta ≤ 0 跳过发体力)。
- 怎么测:承起服环境,让账号体力 = 30(满软上限)、灵力 ≥20、当日次数 <3。发 `C2G_WishForEnergyRequest{}` → `ResultCode=Success`;`SoulPower` = 扣 20 后;`Energy` 仍 = 30(未涨);`WishUsedToday` +1。服务端日志有「祈愿成功」但 Energy 的 delta 推送不触发(netDelta<=0 跳过发体力)。
- 预期结果:体力满软上限时祈愿仍消耗灵力 + 计次,但体力不溢出软上限(与客户端夹 cap 逐位一致,避免登录/对账时体力跳变)。

---

## 皮肤/神庙装饰服务端权威·服务端段(3b)

> 服务端段:皮肤态(是否单色 + 当前单色 id)+ 神庙装饰(已装饰厅数标量)三个「设置状态」服务端权威。
> 新增一条 SET 语义上报 RPC `C2G_SetProfileStateRequest`(全量覆盖三态,非 delta 累加)+ 登录快照回带三字段。
> 起服前提:先关掉正在跑的旧服务端进程(否则占用锁旧二进制),用新编译产物重启;需 MongoDB 可达(改动落 players 集合)。MongoDB 不可达的用例标注「待有库环境手验」。

### [ ] PT0 · 服务端编译通过(Entity + Hotfix 干净;整 sln 除文件锁外无编译错)

- 测什么:本批改动(PlayerDoc 加 3 字段 + SchemaVersion 8→9 + ProfileStateHelper + Handler + 服务组件 sanity 配置 + 登录快照下发 + 协议导出生成物)能否随服务端工程编译通过。
- 怎么测:关掉正在跑的 Main 服务端进程(释放 Bin 目录 DLL 锁),在 `D:\work\TEngine_block\Fantasy` 跑 `dotnet build examples/Server/Server.sln`。
- 预期结果:0 error 0 warning。（dev 侧已单编 Entity 与 Hotfix 均 0 error 0 warning 佐证；整 sln 若仍有 `MSB3021/MSB3027 文件被 Main 锁定` 属旧进程未关的环境项,非编译错,关进程后即消失。）

### [ ] PT1 · 设置三态 RPC 往返:全量 set + 回带对账（起服手测，需 MongoDB）

- 测什么:客户端发 `C2G_SetProfileStateRequest{SkinMono, SkinMonoId, TempleDecorated}`，服务端 sanity 通过后原子 `$set` 三态到 players 文档，响应回带 set 后当前权威三态。
- 怎么测:起服（连 MongoDB）+ 登录一个账号。发合法值，如 `SkinMono=1, SkinMonoId=5, TempleDecorated=3`（SkinMonoId 落 [1,100000] 段内、TempleDecorated ∈ [0,100000]）。
- 预期结果:响应 `ResultCode=Success`；`SkinMono=1 / SkinMonoId=5 / TempleDecorated=3` 原样回带。服务端日志有「设置档案状态成功 account=... skinMono=1 skinMonoId=5 templeDecorated=3」。直查 MongoDB players 文档该账号，`SkinMono=1 / SkinMonoId=5 / TempleDecorated=3` 已落库。

### [ ] PT2 · 幂等重报同值无副作用（起服手测，需 MongoDB）

- 测什么:重复上报同一组三态，SET 覆盖语义下第二次无副作用（结果与第一次一致，不报错、不累加）。
- 怎么测:承 PT1，紧接再发一次完全相同的 `SkinMono=1, SkinMonoId=5, TempleDecorated=3`。
- 预期结果:第二次仍 `ResultCode=Success`，回带值不变（1/5/3）。MongoDB 文档该三字段值不变（SET 幂等，非 delta 累加，不会变成 6 之类）。

### [ ] PT3 · sanity 拒非法值 + 回带当前权威（起服手测，需 MongoDB）

- 测什么:越界值被 sanity 拒（`InvalidRequest`），不写库，且回带服务端当前权威三态供客户端回退。
- 怎么测:承 PT1（当前权威 = 1/5/3）。分别发三组非法值观察：
  - `SkinMono=2`（非 0/1）；
  - `SkinMonoId=0`（既非哨兵 -1、又 < 段下界 1）或 `SkinMonoId=200000`（超段上界）；
  - `TempleDecorated=-1` 或 `TempleDecorated=200000`（超上界）。
  另验合法哨兵 `SkinMonoId=-1`（彩色态）应放行不被拒。
- 预期结果:三组非法值均返 `ResultCode=InvalidRequest`，回带当前权威 1/5/3（未被覆盖）；MongoDB 文档三字段保持 1/5/3 不变。服务端日志有「ProfileStateHelper.TrySet 拒因=InvalidRequest ...」。`SkinMonoId=-1` 那次应 `Success`（哨兵放行）。

### [ ] PT4 · 登录快照回带 3 字段 + 重登持久（起服手测，需 MongoDB）

- 测什么:登录时服务端主动下发的 `G2C_PlayerInfoSnapshot.Info` 携带 `SkinMono / SkinMonoId / TempleDecorated` 三字段，且断线重登后值持久（上次 set 的值不丢）。
- 怎么测:承 PT1（已 set 1/5/3）。断开连接后重新登录同账号，抓登录快照 `G2C_PlayerInfoSnapshot`。
- 预期结果:快照 `Info.SkinMono=1 / Info.SkinMonoId=5 / Info.TempleDecorated=3`（重登读到上次 set 的值，服务端权威持久）。全新账号首登时三字段为缺省 `SkinMono=0 / SkinMonoId=-1 / TempleDecorated=0`（对齐客户端默认:彩色 / Unselected / 无装饰）。

### [ ] PT5 · 旧档补字段迁移（可选，需构造旧 schema 文档，需 MongoDB）

- 测什么:SchemaVersion < 9 的旧玩家文档登录时被补齐 3 个新字段（缺字段 → 补客户端默认 0/-1/0），SchemaVersion 升到 9。
- 怎么测:直接在 MongoDB 把某账号 players 文档的 `SchemaVersion` 改回 8 并删掉 `SkinMono/SkinMonoId/TempleDecorated` 三字段，然后用该账号登录。
- 预期结果:登录后文档 `SchemaVersion=9`，三字段 present 且为 `SkinMono=0 / SkinMonoId=-1 / TempleDecorated=0`；服务端日志有「MigrateSchemaIfNeeded 补字段成功 ... fromVersion=8 toVersion=9」。登录快照回带这三缺省值。

## 皮肤/神庙装饰服务端权威·客户端段(3b)

> dev 已跑自检门(batchmode 编译 + EditMode):PASS，total=619 passed=601 failed=0 skipped=18，compileErrors=0。新增/改动的 EditMode 单测已覆盖皮肤/装饰 SET 上报三态口径、快照读三态(标量↔数组 + SkinMonoId=-1 未选)、blob 剔除三字段(且 CopyNonCurrencyNonIdentity 清空)。以下为门覆盖不到、需连真服 / 实机手测的路径。

### [ ] PCT1 · 客户端编译通过(最基本项,用户 Unity 编)

- 测什么:本批客户端改动(新增 profile-state 网关 + 上报接入 + 快照读三态 + blob 剔除)能否随工程编译通过。
- 怎么测:Unity 编辑器等脚本编译完成,看 Console 无红色 error。
- 预期结果:0 编译错误(warning 不卡)。

### [ ] PCT2 · 皮肤全清换皮走 SET 上报(连真服手测)

- 测什么:融合主游戏里达成一次全清(PERFECT)触发换皮后,客户端把当前三态全量 SET 上报服务端。
- 怎么测:连真服进融合主游戏,玩到一次全清(棋盘清空,弹 PERFECT)。观察服务端是否收到一条 `C2G_SetProfileState`(带 SkinMono=1 + 当前单色 id + 当前已装饰厅数);或换皮后重登,看棋盘皮肤是否为上次换到的单色态(而非退回彩色)。
- 预期结果:全清后棋盘转单色/换一张单色皮肤;该三态经 RPC 落服务端;重登后皮肤态保持(单色不退回彩色)。已知风险:上报 fire-and-forget,若换皮瞬间网络抖动这一笔可能丢——但下次任意变更会再全量报、或重登由快照对齐,不应长期丢态。手测时若单次未同步,重开一局再触发一次全清复核。

### [ ] PCT3 · 神庙装饰走 SET 上报(连真服手测)

- 测什么:神庙窗口修复一厅(即置该厅已装饰)后,客户端把当前三态全量 SET 上报(已装饰厅数 +1)。
- 怎么测:连真服,攒够虔诚币,进神庙窗修复下一厅。观察服务端是否收到 `C2G_SetProfileState`(TempleDecorated = 修复后的已装饰厅数);或修复后重登,看该厅装饰态是否保持。
- 预期结果:修复一厅后已装饰厅数 +1 并经 RPC 落服务端;重登后装饰态保持。已知风险:同 PCT2 fire-and-forget,单次抖动可能丢;下次装饰/换皮再全量报或重登快照对齐。

### [ ] PCT4 · 登录快照读三态覆盖本地投影(连真服手测)

- 测什么:登录时用服务端快照三态(是否单色 + 当前单色 id + 已装饰厅数)覆盖本地投影,而非本地旧缓存 / 旧 blob。
- 怎么测:①先在账号 A 玩出单色皮肤 + 装饰若干厅并让其落服务端;②换设备 / 清本地 PlayerPrefs 后用账号 A 重登,进融合主游戏。观察棋盘皮肤态 + 神庙装饰态是否 = 服务端权威(而非清本地后的初始彩色 / 无装饰)。
- 预期结果:清本地后重登,皮肤态 + 装饰态仍显示服务端权威值(单色 + 已装饰厅数正确恢复)。重点核对:SkinMonoId 服务端为 -1(彩色/未选)时客户端按未选处理、显示彩色态,不误当有效 id 0 而显示 0 号皮肤。

### [ ] PCT5 · blob 不含三字段(可离线手测,不需连服)

- 测什么:云存档 blob 不再携带 skinMono / skinMonoId / templeDecorated(元层白名单已清空)。
- 怎么测:触发一次云存档上传后,查看上传的 blob 内容(或本批已由 EditMode 单测 C1/C1b 覆盖:blob strip 三字段 + ApplyBlob 不回灌)。若手动核对:构造本地皮肤=单色 + 装饰若干,触发上传,解出 blob metaJson,确认这三字段为清零/清空值。
- 预期结果:blob metaJson 里 skinMono=false / skinMonoId=0 / templeDecorated 空;下载他人 blob 不会把这三字段回灌本地(权威只走登录快照)。此项 happy path 已被 EditMode 单测锁死,手测仅在怀疑上传路径异常时复核。

---

## 待测条目(云存档退役·服务端段:局内切片折入 GameSessionDoc + 删 C2G_CloudSave* 通道)

> 局内 cosmetic + 合成叠加层作为不透明字符串切片 SliceJson 折入服务端 GameSessionDoc(服务端只搬运不解析,按 playerId 隔离,随会话档同生死):C2G_Place / ClearTool 上行搭车写入,G2C_GameStart(续局)/ GameSnapshot 下行回带。整个 C2G_CloudSave* 通道(上传 / 下载 handler、CloudSaveDoc、Service、Helper、proto)已删。协议双端重导,生成物逐位一致。
> 提醒:跑整 sln build / 起服前先停占 examples/Bin dll 锁的旧 Main 进程。

### [ ] SVT1 · 服务端编译通过(纯命令行)

- 测什么:Entity + Hotfix 两工程(及整 sln)在切片字段 + 删 CloudSave 通道后能否编译通过、源生成器无悬挂 CloudSave handler 注册。
- 怎么测:停掉占锁的旧 Main 进程后,dotnet build Fantasy.sln(或 Entity.csproj + Hotfix.csproj)。
- 预期结果:0 编译错误;生成物中无 CloudSave message,双端 OuterOpcode / OuterMessage / OuterEnum 逐位一致。

### [ ] SVT2 · 局内切片续存往返(起服手测,依赖 MongoDB)

- 测什么:切片经落子 / 消除道具上行写入 GameSessionDoc,续局 / 快照回带恢复;GameOver 删档后切片一并没、下一局 Resumed=false 回带空串。
- 怎么测:连真服玩若干步(合成 / 元素 / 订单态非空),断线重连或重进拉快照,核对回带切片非空且与中断前一致;玩到终局后再进,确认新建局(空切片)。
- 预期结果:续局回带切片恢复中断前局内现场;终局后无残留切片、走缺省新局。
- (MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

### [ ] SVT3 · CloudSave 通道已删且 EnterMainGame / ClearPlayerData 仍成立(起服手测)

- 测什么:C2G_CloudSave* 通道彻底移除后,依赖它的两条链路仍正常——EnterMainGame 只回带订单快照(无云存档段)、ClearPlayerData 改删 GameSessionDoc。
- 怎么测:连真服登录进主游戏(观察 EnterMainGame 往返正常、订单快照到位);触发清档,确认服务端删的是 GameSessionDoc(在局对局档),重登走新建 / 空局。
- 预期结果:无任何 C2G_CloudSave* 往返;EnterMainGame 订单快照正常;ClearPlayerData 后在局档被清、重登不复活旧局。
- (MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

---

## 待测条目(云存档退役·客户端段:局内现场改服务端会话切片承载)

> 云存档通道已彻底退役:客户端 CloudSaveSync / Codec / Payload / GatewayProd / ICloudSaveGateway + 本地融合局内持久化 MergeIngamePersistence 均已删。局内 cosmetic + 合成叠加层(颜色 / 元素 overlay / 合成区库存 / 订单 / 连消态)改由服务端会话文档的不透明切片 SliceJson 承载:落子 / 消除道具经 C2G_Place / ClearTool 上行搭车存档,G2C_GameStart / GameSnapshot 下行回带恢复。
> 自检门(batchmode 编译 + EditMode)结论见交付报告。以下为门覆盖不到、需连真服 / 实机手测的路径。CVT1 是最基本编译项。

### [ ] CVT1 · 客户端编译通过(最基本项,用户 Unity 编)

- 测什么:本轮收尾改动(PlayerDataLocalReset 去 MergeIngamePersistence/CloudSaveSync 引用 + 遗留键防御性清、EnterMainGameTests 重写去云存档断言、ServerDealSyncTests 桩补 sliceJson 参数、多处注释重写)能否随工程编译通过。
- 怎么测:Unity 编辑器等脚本编译完成,看 Console 无红色 error;Window > General > Test Runner(EditMode)能否列出 BlockBlast.Tests(尤其 EnterMainGameTests / ServerDealSyncTests / PlayerDataLocalResetTests)。
- 预期结果:0 编译错误(warning 不卡);EditMode 列表正常列出上述 fixture,EnterMainGameTests 含 E1-E4 四条(均围绕订单快照,不再有云存档 version/blob 断言)。

### [ ] CVT2 · 续局回带切片恢复局内现场(连真服 + 起服手测)

- 测什么:退出玩法窗重进(服务端仍持同一局)时,盘面颜色 / 元素叠加层 / 合成区库存 / 订单 / 连消态恢复到中断前(切片 SliceJson 下行 import)。
- 怎么测:连真服(需 MongoDB 可达)登录进融合主游戏,落若干子,让局内出现:盘面带颜色 / 元素 overlay、合成区有库存、订单进度非零、连消链非 1。关闭玩法窗再开(或断线重连后再开窗)。
- 预期结果:重进后 board 占用格的颜色 / 元素 overlay、合成区库存、订单状态、连消态恢复到关窗前,不是空白初始态。切片随 G2C_GameStart(Resumed=true)/ GameSnapshot 回带并 import。若某一类叠加层未恢复(如合成区清零),原样回报是哪一类丢失。
- (依赖 MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

### [ ] CVT3 · 落子/消除道具上行搭车切片存档 + 弱网重连一致(连真服 + 起服手测)

- 测什么:每步落子 / 用消除道具时,当前局内切片 SliceJson 随 C2G_Place / C2G_ClearTool 上行,服务端存档;弱网 / 掉线重连后快照回带的局内现场与掉线前一致。
- 怎么测:连真服进玩法,落几子 + 用一次消除道具(让合成 / 元素态变化);制造一次弱网或强制断线(如关网卡后重连),重连后再开窗观察局内现场。
- 预期结果:上行的每一步都带最新切片(服务端存档持续更新);重连后回带的盘面颜色 / 元素 / 合成 / 订单 / 连消与断线前最后一步一致,无回退到更早状态或丢失叠加层。重点核对:消除道具那一步的切片也上行了(不只落子上行)。
- (依赖 MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

### [ ] CVT4 · CloudSave 客户端彻底移除:无云存档往返 + OnSaved 不传 blob(连真服可观测)

- 测什么:客户端不再有 C2G_CloudSaveUpload / Download 往返;存档边界 OnSaved 钩子不再上传 blob,仅触发货币聚合上报。
- 怎么测:连真服进玩法,触发若干次存档边界(落子 / 消除 / 换皮等驱动 MergeMetaPersistence.SaveAsync)。抓客户端发出的 RPC 列表 / 服务端收到的消息类型。
- 预期结果:全程无 C2G_CloudSaveUpload / C2G_CloudSaveDownload(这两条协议客户端已无发起方);存档边界只观察到货币聚合上报(C2G_PropertyChange 类)。若仍看到任何云存档相关 RPC,原样回报(说明有残留调用点未清)。

### [ ] CVT5 · 清档后重登无旧局内 blob 复活(连真服 + 起服手测)

- 测什么:清档(PlayerDataLocalReset.ClearAll,含防御性清历史遗留融合局内 blob 键 block_blast_merge_ingame_v1)后重登,本地无旧融合局内数据复活,客户端从服务端快照 / 切片重建。
- 怎么测:先正常玩一局让本地有各类缓存;触发清档流程(客户端清档入口,服务端清档成功后调 ClearAll);重登同账号进玩法。
- 预期结果:重登后局内现场以服务端为准(服务端已清则空盘新局 / 服务端仍持则续局切片),本地不会冒出旧盘面 / 旧合成态。即使本地曾遗留 block_blast_merge_ingame_v1 旧 blob(历史版本写入),清档已防御性删除,不复活。
- (依赖 MongoDB 不可达则标注「待用户在有库环境手验」,本条暂挂。)

---

## 待测条目(死代码清理·经典棋盘本地存档退役)

> 本轮删经典时代遗留的本地持久化死代码:`BlockGameState.Save/Load` + `SaveData/PendingPieceData` 嵌套类 + `StorageKey`(键 `block_blast_save_v1`)已删,`MainMenuWindow.OnCreate` 里残留的 `state.Load()`(返回值未用)已删。经典 GameWindow 入口早已下线、无活写者,唯一读者是那句 vestigial 调用。
> 动态权重持久化(键 `block_blast_dynamic_v1`)本轮**未删**:核查发现它在现构建仍有活写盘路径(`MergeOrderWindow` 每次开窗经 `_state.Dynamic.Reset()` 写盘、`Dynamic.Init` 读盘),不属死代码,保留不动。
> `PlayerDataLocalReset` 对两键的防御性字面清除保留(清老玩家机上旧档)。
> 无行为变化预期——纯删死代码;安全网为 batchmode 编译 0 error + 全量 EditMode 单测通过(dev 自检门已过:total=604 passed=586 failed=0 skipped=18,CS 错误=0)。以下为门覆盖不到的实机 UI 路径手测。

### [ ] DC1 · 改动后客户端工程编译通过(Editor 内确认)

- 测什么:删 `BlockGameState.Save/Load/SaveData/PendingPieceData/StorageKey`、删 `MainMenuWindow` 的 `state.Load()` 两行、删两个失效单测(`BlockGameStateTests.SaveAndLoad_RoundTrips` / `Load_NoSaveExists_ReturnsFalse`)后,客户端工程能否随 Editor 编译通过。
- 怎么测:用 Unity 打开本工程(`D:\work\TEngine_block\UnityProject`),等 Editor 编译完成,看 Console 有无红色 error;Window > General > Test Runner(EditMode)能否正常列出 `BlockGameStateTests`(应少了上面两条已删用例,其余仍在)。
- 预期结果:0 编译错误(warning 不卡)。`BlockGameStateTests` 仍列出但不含 `SaveAndLoad_RoundTrips` / `Load_NoSaveExists_ReturnsFalse`;其余 BlockBlast.Tests 用例正常列出。

### [ ] DC2 · 主菜单正常打开、BEST 分正常显示(删 state.Load() 无副作用)

- 测什么:`MainMenuWindow.OnCreate` 删掉 `var state = BlockGameState.Instance; state.Load();` 后,主菜单仍能正常打开,BEST 分正常显示(BEST 来自排行榜个人最佳分投影,本就不依赖被删的 Load)。
- 怎么测:Play 模式正常登录 → 进主菜单。观察标题 / 「开始游戏」按钮 / BEST 分是否正常渲染;Console 有无红色异常。
- 预期结果:主菜单正常显示,BEST 分为排行榜个人最佳分(与改动前一致);无 Console 报错,无空引用异常。

### [ ] DC3 · 进玩法窗、玩一局、退出重进正常(经典本地存档删除后主流程无回归)

- 测什么:经典棋盘本地存档删除后,融合主玩法(MergeOrderWindow,唯一玩法入口)开局 / 落子 / 消除 / 补牌 / 计分 / 退出重进全链路正常。局内现场续存已是服务端权威切片,不依赖被删的 `block_blast_save_v1`。
- 怎么测:Play 模式登录 → 主菜单「开始游戏」进玩法窗,玩几手(落子、凑行列消除、三块用完自动补 3 块、分数增长);退出玩法窗回主菜单,再进一次玩法窗。
- 预期结果:开局 / 落子 / 消除 / 补牌 / 计分全部正常,Console 无红;退出重进不因经典本地存档缺失而报错或卡住(局内续存以服务端为准,不受本轮删除影响)。
