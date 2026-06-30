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
