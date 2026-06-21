# 角色记忆:服务端测试(跨任务经验)

> 开工先读本文件;收尾把新的可复用经验沉淀进来(一条一行,过时即删)。
> 准入:只记跨任务可复用、且 agent 定义 / fantasy-net skill / Fantasy CLAUDE.md / conventions 未覆盖的经验。

- **服务端内部触发特性可实现真往返 PASS**:进程内定时器触发(非客户端 RPC)的结算特性,无需客户端触发器:起服 + 活 MongoDB + 等节律间隔(60s)+ 日志观测 + MongoDB 直接查询(临时 .NET 控制台项目 + MongoDB.Driver 3.0.0,无需 mongosh)。第二次节律无新日志 = 幂等生效(SV6);重启后已结标记仍在 = SV7 生效。
- **MongoDB 临时查询探针**:无 mongosh 时,在 `D:\tmp` 下 `dotnet new console` + `dotnet add package MongoDB.Driver --version 3.0.0` + 用 `MongoClientSettings.ConnectTimeout/ServerSelectionTimeout=5s` 防卡死。注意新建项目默认 TargetFramework 为 net10.0(本机),运行时用 `dotnet run`(不加 `--framework net9.0`)。探针代码注意:类声明必须置于顶级语句之前(C# CS8803 规定),可将类定义放文件末尾,入口逻辑封装为 `static async Task Run()` 函数,顶级语句只写 `await Run();`。
- **便携版 mongod 起服**:mongod 不在运行时可直接启动 `D:\mongodb-portable\mongodb-win32-x86_64-windows-8.3.4\bin\mongod.exe --dbpath D:\mongodb-portable\data --port 27017 --logpath D:\mongodb-portable\mongod.log --logappend`,后台运行(Bash & 方式),3s 后检查 27017 端口即可。无需每次依赖已有 mongod 进程。
- **无客户端 RPC 触发器时运行验证策略**:服务端 ledger 类特性(ChangeProperty 旁路写,非定时触发)无法由客户端 RPC 触发,改由探针直接模拟 FindOneAndUpdate + InsertOne 路径验证 MongoDB 真往返;同时对代码关键路径做静态分析补充(AppendAsync 调用点位置 / 失败路径 return 位置),两者结合可达到 PASS 置信度而无需客户端 RPC。
- **SG 产物目录为空 = 零新 Handler/System 的充分证明**:带 `EmitCompilerGeneratedFiles=true` 编译后产物目录为空,说明本子单新建的文档模型类/枚举/静态 Helper 均不需要 SG 注册,编译 0 error 是 SG 产物正确的充分证明。
- **git status 区分 tracked 修改和 untracked 新增文件**:`git diff --name-only HEAD` 只显示已跟踪文件的修改,新建文件(untracked)不在其中;须用 `git status --short` 查全貌,`??` 前缀即为新建未跟踪文件。交接区文件清单核对时须两者结合。
